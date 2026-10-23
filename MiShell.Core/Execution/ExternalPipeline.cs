using MiShell.Core.Parsing;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace MiShell.Core.Execution;

/// <summary>
/// Una tubería de programas externos que se está ejecutando. Es la dueña de todo lo que se abre
/// para ella: los procesos, los archivos de redirección, las tareas que copian los flujos y el
/// token que las cancela. Así el ejecutor solo decide <em>qué</em> ejecutar y esta clase se encarga
/// de iniciarlo, esperarlo y liberarlo sin dejar procesos ni archivos abiertos.
/// </summary>
/// <remarks>
/// Ciclo de vida: <c>SetInput/SetOutput</c> → <see cref="Start"/> → <see cref="RunAsync"/>.
/// En primer plano se libera con <see cref="DisposeAsync"/>; en segundo plano los procesos pasan a la
/// tabla de trabajos y <see cref="RunAsync"/> limpia todo lo demás al terminar.
/// </remarks>
internal sealed class ExternalPipeline : IAsyncDisposable
{
    private readonly List<Process> _processes = [];
    private readonly List<Task> _copies = [];
    private readonly Stream?[] _inputs;
    private readonly Stream?[] _outputs;
    private readonly CancellationTokenSource _lifetime;
    private readonly Action<string> _trace;
    private Exception? _ioFailure;
    private bool _cleanedUp;

    /// <summary>Crea la tubería.</summary>
    /// <param name="stages">Cantidad de programas conectados.</param>
    /// <param name="background">En segundo plano la cancelación de la orden (Ctrl+C) no la alcanza.</param>
    /// <param name="trace">Recibe los mensajes de <c>trace on</c>.</param>
    /// <param name="cancellationToken">Cancelación del comando en curso.</param>
    public ExternalPipeline(int stages, bool background, Action<string> trace, CancellationToken cancellationToken)
    {
        _inputs = new Stream?[stages];
        _outputs = new Stream?[stages];
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(background ? CancellationToken.None : cancellationToken);
        _trace = trace;
    }

    /// <summary>Procesos ya iniciados, en el orden de la tubería.</summary>
    public IReadOnlyList<Process> Processes => _processes;

    /// <summary>Indica si la etapa tiene un archivo (o datos) como entrada.</summary>
    public bool HasInput(int stage) => _inputs[stage] is not null;

    /// <summary>Entrega el flujo que alimentará el stdin de la etapa. La tubería pasa a ser su dueña.</summary>
    public void SetInput(int stage, Stream stream) => _inputs[stage] = stream;

    /// <summary>Entrega el flujo donde se escribirá el stdout de la etapa. La tubería pasa a ser su dueña.</summary>
    public void SetOutput(int stage, Stream stream) => _outputs[stage] = stream;

    /// <summary>
    /// Inicia todos los procesos y conecta sus flujos: stderr siempre a la consola, stdout de cada etapa
    /// al stdin de la siguiente (o a su archivo de salida) y los archivos de entrada al stdin.
    /// </summary>
    /// <param name="commands">Comandos ya analizados.</param>
    /// <param name="executables">Rutas completas resueltas antes de iniciar el primero.</param>
    /// <param name="workingDirectory">Directorio de trabajo de la sesión.</param>
    /// <param name="background">Si es true, la primera etapa no hereda el teclado.</param>
    public void Start(IReadOnlyList<CommandSpec> commands, IReadOnlyList<string> executables, string workingDirectory, bool background)
    {
#pragma warning disable CA2025 // Las copias se esperan en CleanUpAsync antes de liberar procesos y flujos.
        var token = _lifetime.Token;
        for (var i = 0; i < commands.Count; i++)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executables[i],
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = i < commands.Count - 1 || _outputs[i] is not null,
                RedirectStandardInput = i > 0 || _inputs[i] is not null || background,
                RedirectStandardError = true,
            };
            foreach (var argument in commands[i].Arguments) startInfo.ArgumentList.Add(argument);
            var process = new Process { StartInfo = startInfo };
            _trace($"creando proceso: {Path.GetFileName(executables[i])} {string.Join(' ', commands[i].Arguments)}".TrimEnd());
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException($"No se pudo iniciar '{commands[i].Executable}'.");
            }

            _processes.Add(process);
            _trace($"proceso iniciado: PID {process.Id} ({Path.GetFileName(executables[i])}), etapa {i + 1} de {commands.Count}");
            _copies.Add(GuardAsync(DrainErrorAsync(process, token)));
        }

        for (var i = 0; i < _processes.Count; i++)
        {
            var process = _processes[i];
            if (_inputs[i] is { } input)
                _copies.Add(GuardAsync(CopyAsync(input, process.StandardInput.BaseStream, true, token)));
            else if (i == 0 && background || i > 0 && _outputs[i - 1] is not null)
                process.StandardInput.Close();

            if (_outputs[i] is { } output)
                _copies.Add(GuardAsync(CopyAsync(process.StandardOutput.BaseStream, output, false, token)));
            else if (i < _processes.Count - 1)
            {
                // Una redirección explícita reemplaza el extremo correspondiente del pipe.
                var destination = _inputs[i + 1] is null ? _processes[i + 1].StandardInput.BaseStream : Stream.Null;
                _copies.Add(GuardAsync(CopyAsync(process.StandardOutput.BaseStream, destination, destination != Stream.Null, token)));
            }
        }
    }
#pragma warning restore CA2025

    /// <summary>
    /// Espera a que terminen todos los procesos y todas las copias de flujos, y devuelve el código de
    /// la última etapa. Al terminar (o fallar) libera todo salvo los objetos <see cref="Process"/>.
    /// </summary>
    public async Task<int> RunAsync()
    {
        try
        {
            var exits = Task.WhenAll(_processes.Select(p => p.WaitForExitAsync(_lifetime.Token)));
            var io = Task.WhenAll(_copies);
            await (await Task.WhenAny(exits, io).ConfigureAwait(false)).ConfigureAwait(false);
            _trace("esperando a que terminen los procesos (WaitForExitAsync)");
            await exits.ConfigureAwait(false);
            foreach (var process in _processes) _trace($"PID {process.Id} terminó con código {process.ExitCode}");
            await io.ConfigureAwait(false);
            return _processes[^1].ExitCode;
        }
        catch (Exception) when (_ioFailure is not null)
        {
            // Un fallo de E/S (por ejemplo un archivo bloqueado) explica mejor el problema que la cancelación derivada.
            ExceptionDispatchInfo.Capture(_ioFailure).Throw();
            throw;
        }
        finally
        {
            await CleanUpAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Termina lo que siga vivo, espera las copias y libera los procesos propios.</summary>
    public async ValueTask DisposeAsync()
    {
        await CleanUpAsync().ConfigureAwait(false);
        foreach (var process in _processes) process.Dispose();
    }

    /// <summary>
    /// Libera todo lo que no son procesos: mata los que sigan vivos, espera a que las copias de flujos
    /// terminen (así ninguna usa un flujo ya cerrado), cierra los archivos y por último el token.
    /// Es idempotente porque la llaman tanto <see cref="RunAsync"/> como <see cref="DisposeAsync"/>.
    /// </summary>
    private async Task CleanUpAsync()
    {
        if (_cleanedUp) return;
        _cleanedUp = true;
        foreach (var process in _processes)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* Ya terminó. */ }
        }

        await Task.WhenAll(_processes.Select(p => p.WaitForExitAsync())).ConfigureAwait(false);
        try { await Task.WhenAll(_copies).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { /* Ya se informó en _ioFailure. */ }
        foreach (var stream in _inputs.Concat(_outputs).OfType<Stream>())
            await stream.DisposeAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }

    /// <summary>Si una copia falla por E/S, guarda el error y cancela la tubería completa.</summary>
    private async Task GuardAsync(Task copy)
    {
        try { await copy.ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            Interlocked.CompareExchange(ref _ioFailure, ex, null);
            CancelLifetime();
            throw;
        }
    }

    private void CancelLifetime()
    {
        try { _lifetime.Cancel(); }
        catch (ObjectDisposedException) { /* La limpieza terminó antes de que esta copia fallara. */ }
    }

    /// <summary>Lee stderr línea por línea y lo escribe en la consola de la shell.</summary>
    private static async Task DrainErrorAsync(Process process, CancellationToken token)
    {
        while (await process.StandardError.ReadLineAsync(token).ConfigureAwait(false) is { } line)
            await Console.Error.WriteLineAsync(line).ConfigureAwait(false);
    }

    /// <summary>Copia un flujo en otro; al terminar cierra el destino para que el lector reciba el fin de datos.</summary>
    private static async Task CopyAsync(Stream source, Stream destination, bool closeDestination, CancellationToken token)
    {
        try { await source.CopyToAsync(destination, token).ConfigureAwait(false); }
        catch (IOException ex) when (closeDestination && (ex.HResult & 0xffff) is 109 or 232) { /* El consumidor cerró stdin anticipadamente. */ }
        finally { if (closeDestination) await destination.DisposeAsync().ConfigureAwait(false); }
    }
}
