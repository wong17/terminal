using MiShell.Core.Contracts;
using MiShell.Core.Jobs;
using MiShell.Core.Parsing;
using System.Diagnostics;

namespace MiShell.Core.Execution;

/// <summary>Se lanza cuando un ejecutable no se encuentra en el PATH ni como ruta.</summary>
public sealed class CommandNotFoundException(string name) : Exception($"Comando no encontrado: '{name}'");

/// <summary>Estado mutable compartido del shell (directorio actual, entorno, historial y jobs).</summary>
public sealed class ShellState(string initialDirectory, IShellEnvironment environment, IJobTable jobs) : ICommandContext
{
    public string WorkingDirectory { get; private set; } = initialDirectory;

    public IShellEnvironment Environment { get; } = environment;

    public IJobTable Jobs { get; } = jobs;

    public IReadOnlyList<string> History { get; set; } = [];

    public bool ExitRequested { get; private set; }

    void ICommandContext.ChangeDirectory(string path) => ChangeDirectory(path);

    public void ChangeDirectory(string path) => WorkingDirectory = Path.GetFullPath(path, WorkingDirectory);

    public void RequestExit(int exitCode)
    {
        ExitRequested = true;
        PendingExitCode = exitCode;
    }

    public int PendingExitCode { get; private set; }

    public int ConsumeExitCode()
    {
        var code = PendingExitCode;
        PendingExitCode = 0;
        return code;
    }
}


/// <summary>Ejecuta internos o tuberías externas y conserva sus recursos hasta terminar.</summary>
public sealed class ShellExecutor(
    IEnumerable<IBuiltinCommand> builtins,
    IExecutableResolver resolver,
    IOutputRenderer renderer,
    IJobTable jobTable,
    ShellState state) : IShellExecutor
{
    private readonly Dictionary<string, IBuiltinCommand> _builtins = builtins.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);

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

    private async Task<int> RunPipelineAsync(IReadOnlyList<CommandSpec> commands, bool background, CancellationToken cancellationToken)
    {
        var first = commands[0];
        if (commands.Count > 1 && commands.Any(c => _builtins.ContainsKey(c.Executable)))
        {
            renderer.WriteError("Las tuberías admiten programas externos. Ejemplo: cmd /c echo Hola | findstr Hola.");
            return 1;
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
        await using var writer = output is null ? null : new StreamWriter(
            Path.GetFullPath(output.Path, state.WorkingDirectory), output.Kind == RedirectionKind.OutputAppend);
        using var redirect = writer is null ? null : renderer.RedirectOutput(writer);
        try
        {
            if (reader is not null) Console.SetIn(reader);
            return (await builtin.ExecuteAsync([.. first.Arguments], state, cancellationToken).ConfigureAwait(false)).ExitCode;
        }
        finally { Console.SetIn(originalInput); }
    }

    private async Task<int> RunExternalPipelineAsync(IReadOnlyList<CommandSpec> commands, bool background, CancellationToken cancellationToken)
    {
        // Resuelve todos los comandos antes de iniciar el primero.
        var executables = commands.Select(c => resolver.Resolve(c.Executable)
            ?? throw new CommandNotFoundException(c.Executable)).ToArray();
        if (executables.Any(p => Path.GetExtension(p).ToLowerInvariant() is ".cmd" or ".bat" or ".ps1"))
        {
            renderer.WriteError("Ejecuta scripts del sistema con cmd /c archivo.cmd o powershell -File archivo.ps1.");
            return 1;
        }

        var processes = new List<Process>();
        var copies = new List<Task>();
        var inputs = new Stream?[commands.Count];
        var outputs = new Stream?[commands.Count];
        var transferred = false;
        var token = background ? CancellationToken.None : cancellationToken;
        try
        {
            for (var i = 0; i < commands.Count; i++)
            {
                var input = commands[i].Redirections.FirstOrDefault(r => r.Kind == RedirectionKind.Input);
                if (input is not null)
                    inputs[i] = new FileStream(Path.GetFullPath(input.Path, state.WorkingDirectory), FileMode.Open, FileAccess.Read);
            }

            for (var i = 0; i < commands.Count; i++)
            {
                var output = commands[i].Redirections.FirstOrDefault(r => r.Kind != RedirectionKind.Input);
                if (output is not null)
                    outputs[i] = new FileStream(Path.GetFullPath(output.Path, state.WorkingDirectory),
                        output.Kind == RedirectionKind.OutputAppend ? FileMode.Append : FileMode.Create, FileAccess.Write);
            }

            for (var i = 0; i < commands.Count; i++)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executables[i],
                    WorkingDirectory = state.WorkingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = i < commands.Count - 1 || outputs[i] is not null,
                    RedirectStandardInput = i > 0 || inputs[i] is not null || background,
                    RedirectStandardError = true,
                };
                foreach (var argument in commands[i].Arguments) startInfo.ArgumentList.Add(argument);
                var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                {
                    process.Dispose();
                    throw new InvalidOperationException($"No se pudo iniciar '{commands[i].Executable}'.");
                }
                processes.Add(process);
                copies.Add(DrainErrorAsync(process, token));
            }

            for (var i = 0; i < processes.Count; i++)
            {
                var process = processes[i];
                if (inputs[i] is { } input)
                    copies.Add(CopyAsync(input, process.StandardInput.BaseStream, true, token));
                else if (i == 0 && background || i > 0 && outputs[i - 1] is not null)
                    process.StandardInput.Close();

                if (outputs[i] is { } output)
                    copies.Add(CopyAsync(process.StandardOutput.BaseStream, output, false, token));
                else if (i < processes.Count - 1)
                {
                    // Una redirección explícita reemplaza el extremo correspondiente del pipe.
                    var destination = inputs[i + 1] is null ? processes[i + 1].StandardInput.BaseStream : Stream.Null;
                    copies.Add(CopyAsync(process.StandardOutput.BaseStream, destination, destination != Stream.Null, token));
                }
            }

            var completion = CompleteAsync();
            if (background)
            {
                var line = string.Join(" | ", commands.Select(c => string.Join(' ', new[] { c.Executable }.Concat(c.Arguments))));
                var job = jobTable.Add(processes, line, completion);
                transferred = true;
                renderer.WriteInfo($"[{job.JobId}] PID {string.Join(", ", processes.Select(p => p.Id))} en segundo plano");
                return 0;
            }

            // Los procesos heredan la consola: un spinner competiría con su salida.
            return await completion.ConfigureAwait(false);

            async Task<int> CompleteAsync()
            {
                try
                {
                    var exits = Task.WhenAll(processes.Select(p => p.WaitForExitAsync(token)));
                    var io = Task.WhenAll(copies);
                    await (await Task.WhenAny(exits, io).ConfigureAwait(false)).ConfigureAwait(false);
                    await exits.ConfigureAwait(false);
                    await io.ConfigureAwait(false);
                    return processes[^1].ExitCode;
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                        catch (InvalidOperationException) { }
                    }
                    await Task.WhenAll(processes.Select(p => p.WaitForExitAsync())).ConfigureAwait(false);
                    try { await Task.WhenAll(copies).ConfigureAwait(false); }
                    catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
                    await DisposeStreamsAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (!transferred)
            {
                foreach (var process in processes)
                {
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { /* Ya terminó. */ }
                }
                await Task.WhenAll(processes.Select(p => p.WaitForExitAsync())).ConfigureAwait(false);
                try { await Task.WhenAll(copies).ConfigureAwait(false); }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
                await DisposeStreamsAsync().ConfigureAwait(false);
                foreach (var process in processes) process.Dispose();
            }
        }

        async Task DisposeStreamsAsync()
        {
            foreach (var stream in inputs.Concat(outputs).OfType<Stream>())
                await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task DrainErrorAsync(Process process, CancellationToken token)
    {
        while (await process.StandardError.ReadLineAsync(token).ConfigureAwait(false) is { } line)
            await Console.Error.WriteLineAsync(line).ConfigureAwait(false);
    }

    private static async Task CopyAsync(Stream source, Stream destination, bool closeDestination, CancellationToken token)
    {
        try { await source.CopyToAsync(destination, token).ConfigureAwait(false); }
        catch (IOException) when (closeDestination) { /* El consumidor cerró stdin anticipadamente. */ }
        finally { if (closeDestination) await destination.DisposeAsync().ConfigureAwait(false); }
    }
}
