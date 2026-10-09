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
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        renderer.RenderBanner();
        var lastExitCode = 0;
        TimeSpan? lastDuration = null;

        while (!state.ExitRequested)
        {
            NotifyCompletedJobs();
            state.History = history.Entries;

            string? input;
            try
            {
                input = lineEditor.ReadLine(promptRenderer.Render(lastExitCode, lastDuration));
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                // Entrada estándar cerrada (Ctrl+Z / fin de pipe) o cancelación: salir limpio.
                break;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            history.Add(input.Trim());

            try
            {
                var commandLine = parser.Parse(input);
                if (commandLine.Pipelines.Count == 0)
                {
                    continue;
                }

                var stopwatch = Stopwatch.StartNew();
                var exitCode = await executor.ExecuteAsync(commandLine, cancellationToken);
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
                renderer.WriteLine();
                break;
            }
            catch (Exception ex)
            {
                // Última barrera: ningún error interno debe tumbar el shell.
                renderer.WriteError($"Error interno: {ex.Message}");
                lastExitCode = 1;
                lastDuration = null;
            }
        }

        renderer.WriteSuccess("Hasta pronto.");
        return state.ExitRequested ? state.ConsumeExitCode() : 0;
    }

    private void NotifyCompletedJobs()
    {
        while (state.Jobs.RemoveFirstCompleted() is { } job)
        {
            renderer.WriteInfo($"[{job.JobId}] terminado: {job.CommandLine}");
        }
    }
}
