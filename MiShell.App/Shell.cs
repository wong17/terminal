using MiShell.Core.Contracts;
using MiShell.Core.Execution;
using MiShell.Core.History;
using MiShell.Core.Parsing;
using MiShell.UI.Input;
using MiShell.UI.Prompt;
using System.ComponentModel;
using System.Diagnostics;

namespace MiShell.App;

/// <summary>Bucle principal del shell: leer → parsear → ejecutar → repetir.</summary>
public sealed class Shell(
    Parser parser,
    IShellExecutor executor,
    IOutputRenderer renderer,
    LineEditor lineEditor,
    PromptRenderer promptRenderer,
    ShellState state,
    HistoryStore history)
{
    private CancellationTokenSource? _currentCommand;

    /// <summary>Cancela el comando en ejecución (Ctrl+C) sin cerrar la sesión.</summary>
    public void CancelCurrentCommand()
    {
        try { _currentCommand?.Cancel(); }
        catch (ObjectDisposedException) { /* Terminó mientras llegaba Ctrl+C. */ }
    }

    /// <summary>Ciclo principal (REPL): muestra el prompt, lee una línea, la guarda en el historial, la analiza con el parser, la ejecuta y repite hasta exit, Ctrl+Z o fin de la entrada. Cada tipo de error se convierte en un mensaje y un código de salida sin cerrar la sesión.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        renderer.RenderBanner();
        var lastExitCode = 0;
        TimeSpan? lastDuration = null;

        while (!state.ExitRequested && !cancellationToken.IsCancellationRequested)
        {
            NotifyCompletedJobs();
            state.History = history.Entries;

            string? input;
            try
            {
                if (!Console.IsInputRedirected) Console.TreatControlCAsInput = true;
                input = lineEditor.ReadLine(Console.IsInputRedirected ? "" : promptRenderer.Render(lastExitCode, lastDuration));
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                // Entrada estándar cerrada (Ctrl+Z / fin de pipe) o cancelación: salir limpio.
                break;
            }

            finally
            {
                if (!Console.IsInputRedirected) Console.TreatControlCAsInput = false;
            }

            if (input is null) break;
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            history.Add(input.Trim());

            // En una terminal interactiva la salida se separa con una línea en blanco de la orden y del siguiente prompt.
            var spaced = !Console.IsInputRedirected;
            if (spaced) renderer.WriteLine();

            using var commandCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _currentCommand = commandCancellation;
            try
            {
                var commandLine = parser.Parse(input);
                if (commandLine.Pipelines.Count == 0)
                {
                    continue;
                }

                var stopwatch = Stopwatch.StartNew();
                var exitCode = await executor.ExecuteAsync(commandLine, commandCancellation.Token);
                stopwatch.Stop();

                lastExitCode = exitCode;
                lastDuration = stopwatch.Elapsed;
            }
            catch (SyntaxException ex)
            {
                renderer.WriteError($"Error de sintaxis: {ex.Message}");
                lastExitCode = 1;
                lastDuration = null;
            }
            catch (CommandNotFoundException ex)
            {
                renderer.WriteError(ex.Message);
                renderer.WriteInfo("Escribe 'help' para ver los comandos disponibles.");
                lastExitCode = 1;
                lastDuration = null;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                renderer.WriteError(ex.Message);
                lastExitCode = 1;
                lastDuration = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or KeyNotFoundException)
            {
                renderer.WriteError($"No se pudo completar la operación: {ex.Message}");
                lastExitCode = 1;
                lastDuration = null;
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested) break;
                renderer.WriteInfo("Comando cancelado.");
                lastExitCode = 130;
                lastDuration = null;
            }
            catch (Exception ex)
            {
                // Última barrera: ningún error interno debe tumbar el shell.
                renderer.WriteError($"Error interno: {ex.Message}");
                lastExitCode = 1;
                lastDuration = null;
            }
            finally
            {
                _currentCommand = null;
                if (spaced && !state.ExitRequested) renderer.WriteLine();
            }
        }

        renderer.WriteSuccess("Hasta pronto.");
        return state.ExitRequested ? state.ConsumeExitCode() : 0;
    }

    private void NotifyCompletedJobs()
    {
        while (state.Jobs.RemoveFirstCompleted() is { } job)
        {
            try
            {
                var code = job.Completion.GetAwaiter().GetResult();
                if (job.Killed) continue; // kill ya informó del resultado; el código de salida de un proceso terminado a la fuerza no es un fallo.
                if (code == 0) renderer.WriteInfo($"[{job.JobId}] terminado con código {code}: {job.CommandLine}");
                else renderer.WriteError($"[{job.JobId}] falló con código {code}: {job.CommandLine}");
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException)
            {
                renderer.WriteError($"[{job.JobId}] {ex.Message}");
            }
            finally { job.Dispose(); }
        }
    }
}
