using MiShell.Core.Contracts;
using MiShell.Core.Jobs;
using MiShell.Core.Parsing;
using System.Text;

namespace MiShell.Core.Execution;

/// <summary>
/// Recibe un <see cref="CommandLine"/> ya analizado y lo ejecuta: decide si cada comando es interno
/// (una función dentro de la sesión) o externo (un proceso nuevo), aplica <c>&amp;&amp;</c>, <c>||</c>,
/// redirecciones y segundo plano, y devuelve el código de salida de la última orden.
/// </summary>
public sealed class ShellExecutor(
    IEnumerable<IBuiltinCommand> builtins,
    IExecutableResolver resolver,
    IOutputRenderer renderer,
    IJobTable jobTable,
    ShellState state,
    Func<string, FileMode, Stream>? outputFactory = null) : IShellExecutor
{
    private readonly Dictionary<string, IBuiltinCommand> _builtins = builtins.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Recorre las tuberías de la línea en orden. Una tubería encadenada con <c>&amp;&amp;</c> se omite si la
    /// anterior falló y una con <c>||</c> se omite si tuvo éxito.
    /// </summary>
    /// <returns>El código de salida de la última tubería ejecutada.</returns>
    public async Task<int> ExecuteAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        var exitCode = 0;
        foreach (var pipeline in commandLine.Pipelines)
        {
            if (state.ExitRequested) break;
            if (pipeline.Chain == ChainOperator.AndAlso && exitCode != 0 ||
                pipeline.Chain == ChainOperator.OrElse && exitCode == 0) continue;
            exitCode = await RunPipelineAsync(pipeline.Commands, commandLine.RunInBackground, cancellationToken).ConfigureAwait(false);
        }

        return exitCode;
    }

    /// <summary>
    /// Punto de decisión: ¿interno o externo? Un interno puede abrir una tubería (<c>ls | findstr txt</c>)
    /// pero no ir en las demás etapas ni en segundo plano.
    /// </summary>
    private async Task<int> RunPipelineAsync(IReadOnlyList<CommandSpec> commands, bool background, CancellationToken cancellationToken)
    {
        ValidateRedirections(commands);
        var first = commands[0];
        switch (commands.Count)
        {
            case > 1 when commands.Skip(1).Any(c => _builtins.ContainsKey(c.Executable)):
                renderer.WriteError("Un comando interno solo puede ir al inicio de la tubería. Ejemplo: ls | findstr.exe txt.");
                return 1;
            case > 1 when _builtins.TryGetValue(first.Executable, out var producer):
                return await RunBuiltinIntoPipelineAsync(commands, producer, background, cancellationToken).ConfigureAwait(false);
        }

        if (!_builtins.TryGetValue(first.Executable, out var builtin))
            return await RunExternalPipelineAsync(commands, background, cancellationToken).ConfigureAwait(false);
        if (background)
        {
            renderer.WriteError("Los comandos internos se ejecutan en la sesión actual; '&' requiere un programa externo.");
            return 1;
        }

        var input = first.Redirections.FirstOrDefault(r => r.Kind == RedirectionKind.Input);
        var output = first.Redirections.FirstOrDefault(r => r.Kind != RedirectionKind.Input);
        if (input is not null && !builtin.AcceptsInput)
        {
            renderer.WriteError($"'{builtin.Name}' no lee stdin. Usa cat < archivo o un programa externo.");
            return 1;
        }

        var originalInput = Console.In;
        using var reader = input is null ? null : new StreamReader(Path.GetFullPath(input.Path, state.WorkingDirectory));
        // Un fallo al abrir stdin no debe truncar stdout.
        await using var writer = output is null ? null : new StreamWriter(OpenOutput(output));
        using var redirect = writer is null ? null : renderer.RedirectOutput(writer);
        try
        {
            if (reader is not null) Console.SetIn(reader);
            return (await builtin.ExecuteAsync([.. first.Arguments], state, cancellationToken).ConfigureAwait(false)).ExitCode;
        }
        finally { Console.SetIn(originalInput); }
    }

    /// <summary>
    /// Ejecuta un interno como primera etapa: su salida se captura en memoria y se entrega
    /// por stdin al primer programa externo (<c>ls | findstr txt</c>).
    /// </summary>
    private async Task<int> RunBuiltinIntoPipelineAsync(IReadOnlyList<CommandSpec> commands, IBuiltinCommand builtin, bool background, CancellationToken cancellationToken)
    {
        var first = commands[0];
        if (background)
        {
            renderer.WriteError("Los comandos internos se ejecutan en la sesión actual; '&' requiere un programa externo.");
            return 1;
        }

        if (first.Redirections.Any(r => r.Kind != RedirectionKind.Input))
        {
            renderer.WriteError($"La salida de '{builtin.Name}' ya va a la tubería; no la redirijas también a un archivo.");
            return 1;
        }

        var input = first.Redirections.FirstOrDefault(r => r.Kind == RedirectionKind.Input);
        if (input is not null && !builtin.AcceptsInput)
        {
            renderer.WriteError($"'{builtin.Name}' no lee stdin. Usa cat < archivo o un programa externo.");
            return 1;
        }

        // Resuelve las etapas externas antes de ejecutar el interno, igual que en una tubería normal.
        foreach (var command in commands.Skip(1))
            _ = resolver.Resolve(command.Executable) ?? throw new CommandNotFoundException(command.Executable);

        using var capture = new StringWriter();
        var originalInput = Console.In;
        using var reader = input is null ? null : new StreamReader(Path.GetFullPath(input.Path, state.WorkingDirectory));
        int code;
        try
        {
            using var redirect = renderer.RedirectOutput(capture);
            if (reader is not null) Console.SetIn(reader);
            code = (await builtin.ExecuteAsync([.. first.Arguments], state, cancellationToken).ConfigureAwait(false)).ExitCode;
        }
        finally { Console.SetIn(originalInput); }

        if (code != 0) return code;
        var seed = new UTF8Encoding(false).GetBytes(capture.ToString());
        return await RunExternalPipelineAsync([.. commands.Skip(1)], false, cancellationToken, seed).ConfigureAwait(false);
    }

    /// <summary>
    /// Camino de los programas externos: resuelve todas las rutas antes de iniciar el primero, abre los
    /// archivos de redirección y delega en <see cref="ExternalPipeline"/> el inicio, la espera y la limpieza.
    /// En segundo plano los procesos pasan a la tabla de trabajos y la tubería se limpia al terminar sola.
    /// </summary>
    private async Task<int> RunExternalPipelineAsync(IReadOnlyList<CommandSpec> commands, bool background, CancellationToken cancellationToken, byte[]? seedInput = null)
    {
        var executables = commands.Select(c => resolver.Resolve(c.Executable)
            ?? throw new CommandNotFoundException(c.Executable)).ToArray();
        if (executables.Any(p => Path.GetExtension(p).Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(p).Equals(".bat", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(p).Equals(".ps1", StringComparison.OrdinalIgnoreCase)))
        {
            renderer.WriteError("Ejecuta scripts del sistema con cmd /c archivo.cmd o powershell -File archivo.ps1.");
            return 1;
        }

        var pipeline = new ExternalPipeline(commands.Count, background, Trace, cancellationToken);
        var transferred = false;
        try
        {
            for (var i = 0; i < commands.Count; i++)
            {
                var input = commands[i].Redirections.FirstOrDefault(r => r.Kind == RedirectionKind.Input);
                if (input is not null)
#pragma warning disable CA2000 // La tubería recibe la propiedad del flujo y lo libera.
                    pipeline.SetInput(i, new FileStream(Path.GetFullPath(input.Path, state.WorkingDirectory), FileMode.Open, FileAccess.Read));
#pragma warning restore CA2000
            }

            if (seedInput is not null && !pipeline.HasInput(0)) pipeline.SetInput(0, new MemoryStream(seedInput));

            // La entrada se abre antes de truncar la salida: un archivo de entrada inexistente no destruye datos.
            for (var i = 0; i < commands.Count; i++)
            {
                var output = commands[i].Redirections.FirstOrDefault(r => r.Kind != RedirectionKind.Input);
                if (output is not null) pipeline.SetOutput(i, OpenOutput(output));
            }

            pipeline.Start(commands, executables, state.WorkingDirectory, background);
            var completion = pipeline.RunAsync();
            if (background)
            {
                var line = string.Join(" | ", commands.Select(c => string.Join(' ', new[] { c.Executable }.Concat(c.Arguments))));
                var job = jobTable.Add(pipeline.Processes, line, completion);
                transferred = true;
                renderer.WriteInfo($"[{job.JobId}] PID {string.Join(", ", pipeline.Processes.Select(p => p.Id))} en segundo plano");
                return 0;
            }

            // Los procesos heredan la consola: un spinner competiría con su salida.
            return await completion.ConfigureAwait(false);
        }
        finally
        {
            if (!transferred) await pipeline.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Muestra un mensaje de seguimiento en stderr cuando <c>trace on</c> está activo.</summary>
    private void Trace(string message)
    {
        if (state.TraceProcesses) Console.Error.WriteLine($"[trace] {message}");
    }

    /// <summary>Abre (o crea/trunca) el archivo de salida de una redirección; las pruebas pueden sustituir la fábrica.</summary>
    private Stream OpenOutput(Redirection output)
    {
        var path = Path.GetFullPath(output.Path, state.WorkingDirectory);
        var mode = output.Kind == RedirectionKind.OutputAppend ? FileMode.Append : FileMode.Create;
#pragma warning disable CA2000 // El llamador recibe la propiedad del flujo y lo libera.
        return outputFactory?.Invoke(path, mode) ?? new FileStream(path, mode, FileAccess.Write);
#pragma warning restore CA2000
    }

    /// <summary>
    /// Evita destruir datos: rechaza que un archivo sea a la vez entrada y salida, o que dos etapas
    /// escriban en el mismo archivo. Para cat, type y source cuentan también sus argumentos como entradas.
    /// </summary>
    private void ValidateRedirections(IReadOnlyList<CommandSpec> commands)
    {
        var inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in commands)
        {
            if (command.Executable.Equals("cat", StringComparison.OrdinalIgnoreCase)
                || command.Executable.Equals("type", StringComparison.OrdinalIgnoreCase)
                || command.Executable.Equals("source", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var argument in command.Arguments.Where(a => !a.StartsWith('-')))
                    inputs.Add(Path.GetFullPath(argument, state.WorkingDirectory));
            }

            foreach (var redirection in command.Redirections)
            {
                var path = Path.GetFullPath(redirection.Path, state.WorkingDirectory);
                if (redirection.Kind == RedirectionKind.Input)
                {
                    if (!File.Exists(path)) throw new FileNotFoundException($"No existe el archivo de entrada '{redirection.Path}'.");
                    inputs.Add(path);
                }
                else if (!outputs.Add(path))
                    throw new IOException("La misma tubería no puede escribir dos veces en el mismo archivo.");
            }
        }

        if (inputs.Overlaps(outputs))
            throw new IOException("El archivo de entrada y el de salida deben ser distintos para conservar los datos.");
    }
}
