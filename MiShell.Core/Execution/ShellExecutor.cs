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

/// <summary>
/// Ejecutor principal: despacha comandos internos o construye tuberías de procesos externos
/// conectando stdout → stdin con flujos binarios (sin pérdida de codificación).
/// </summary>
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
        var lastExitCode = 0;
        foreach (var pipeline in commandLine.Pipelines)
        {
            switch (pipeline.Chain)
            {
                case ChainOperator.AndAlso when lastExitCode != 0:
                case ChainOperator.OrElse when lastExitCode == 0:
                    continue;
                case ChainOperator.Always:
                default:
                    lastExitCode = await RunPipelineAsync(pipeline.Commands, commandLine.RunInBackground, cancellationToken);
                    break;
            }
        }

        return lastExitCode;
    }

    private async Task<int> RunPipelineAsync(IReadOnlyList<CommandSpec> commands, bool runInBackground, CancellationToken cancellationToken)
    {
        var first = commands[0];
        if (commands.Count != 1 || !_builtins.TryGetValue(first.Executable, out var builtin))
            return await RunExternalPipelineAsync(commands, runInBackground, cancellationToken);

        if (first.Redirections.Count == 0)
        {
            return (await builtin.ExecuteAsync([.. first.Arguments], state, cancellationToken)).ExitCode;
        }

        // Redirección de un comando interno: se captura su salida de consola al archivo.
        var outputRedirect = first.Redirections.FirstOrDefault(r => r.Kind is RedirectionKind.OutputTruncate or RedirectionKind.OutputAppend);
        if (outputRedirect is null)
        {
            renderer.WriteError("Solo se admite redirección de salida (>/>>) en comandos internos.");
            return 1;
        }

        await using var file = new FileStream(
            Path.GetFullPath(outputRedirect.Path, state.WorkingDirectory),
            outputRedirect.Kind == RedirectionKind.OutputAppend ? FileMode.Append : FileMode.Create,
            FileAccess.Write);
        var originalOut = Console.Out;
        Console.SetOut(new StreamWriter(file, leaveOpen: false) { AutoFlush = true });
        try
        {
            return (await builtin.ExecuteAsync([.. first.Arguments], state, cancellationToken)).ExitCode;
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private async Task<int> RunExternalPipelineAsync(IReadOnlyList<CommandSpec> commands, bool runInBackground, CancellationToken cancellationToken)
    {
        var processes = new List<Process>();
        var copyTasks = new List<Task>();
        var openStreams = new List<Stream>();
        try
        {
            for (var i = 0; i < commands.Count; i++)
            {
                var spec = commands[i];
                var resolved = resolver.Resolve(spec.Executable)
                               ?? throw new CommandNotFoundException(spec.Executable);

                var isLast = i == commands.Count - 1;
                var outputRedirect = spec.Redirections.FirstOrDefault(r => r.Kind is RedirectionKind.OutputTruncate or RedirectionKind.OutputAppend);
                var inputRedirect = spec.Redirections.FirstOrDefault(r => r.Kind == RedirectionKind.Input);

                var startInfo = new ProcessStartInfo
                {
                    FileName = resolved,
                    WorkingDirectory = state.WorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    // stdout se redirige solo si hay consumidor (pipe o archivo); si no, hereda la consola.
                    RedirectStandardOutput = !isLast || outputRedirect is not null,
                    RedirectStandardInput = i > 0 || inputRedirect is not null,
                    RedirectStandardError = true,
                };

                foreach (var arg in spec.Arguments)
                {
                    startInfo.ArgumentList.Add(arg);
                }

                var process = new Process { StartInfo = startInfo };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is not null)
                    {
                        renderer.WriteLine(e.Data, renderer.Theme.ErrorColor);
                    }
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException($"No se pudo iniciar '{spec.Executable}'.");
                }

                process.BeginErrorReadLine();

                if (inputRedirect is not null)
                {
                    var file = new FileStream(Path.GetFullPath(inputRedirect.Path, state.WorkingDirectory), FileMode.Open, FileAccess.Read);
                    openStreams.Add(file);
                    copyTasks.Add(CopyAndCloseAsync(file, process.StandardInput.BaseStream));
                }

                if (outputRedirect is not null)
                {
                    var file = new FileStream(
                        Path.GetFullPath(outputRedirect.Path, state.WorkingDirectory),
                        outputRedirect.Kind == RedirectionKind.OutputAppend ? FileMode.Append : FileMode.Create,
                        FileAccess.Write);
                    openStreams.Add(file);
                    copyTasks.Add(CopyAndCloseAsync(process.StandardOutput.BaseStream, file, closeDestination: false));
                }
                else if (i > 0)
                {
                    // Conecta stdout del proceso anterior al stdin de este.
                    copyTasks.Add(CopyAndCloseAsync(processes[^1].StandardOutput.BaseStream, process.StandardInput.BaseStream));
                }

                processes.Add(process);
            }

            if (runInBackground)
            {
                var job = jobTable.Add(processes[^1], string.Join(" | ", commands.Select(c => c.Executable)));
                renderer.WriteInfo($"[{job.JobId}] PID {processes[^1].Id} en segundo plano");
                foreach (var p in processes[..^1])
                {
                    p.Dispose();
                }

                return 0;
            }

            async Task<int> AwaitPipelineAsync()
            {
                await Task.WhenAll(processes.Select(p => p.WaitForExitAsync(cancellationToken))).ConfigureAwait(false);
                await Task.WhenAll(copyTasks).ConfigureAwait(false);
                return processes[^1].ExitCode;
            }

            // El renderer muestra un indicador animado solo si la tubería tarda.
            return await renderer.RunWithSpinnerAsync<int>(
                $"Ejecutando '{commands[0].Executable}'…",
                _ => AwaitPipelineAsync(),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (var stream in openStreams)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }

            foreach (var p in processes.Where(p => p.HasExited))
            {
                p.Dispose();
            }
        }
    }

    private static async Task CopyAndCloseAsync(Stream source, Stream destination, bool closeDestination = true)
    {
        try
        {
            await source.CopyToAsync(destination).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // El proceso siguiente cerró su stdin antes de consumir todo: no es un error del shell.
        }

        if (closeDestination)
        {
            await destination.DisposeAsync().ConfigureAwait(false);
        }
    }
}
