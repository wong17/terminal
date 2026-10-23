using MiShell.Core.Contracts;
using MiShell.Core.Jobs;

namespace MiShell.Commands.BuiltinCommands;

/// <summary><c>kill</c>: termina trabajos (%id) o procesos (PID) con todo su árbol; acepta varios objetivos.</summary>
public sealed class KillCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "kill";

    public string Description => "Termina trabajos (%id) o procesos (PID) con todo su árbol; acepta varios.";

    public string Usage => "kill %<job-id> | <pid> [...]";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }

        var failed = arguments.Aggregate(false, (current, argument) => current | !KillOne(argument, context));

        return Task.FromResult(failed ? CommandResult.Failure : CommandResult.Ok);
    }

    private bool KillOne(string argument, ICommandContext context)
    {
        if (int.TryParse(argument, out var pid) && pid > 0)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
                var name = process.ProcessName;
                process.Kill(entireProcessTree: true);
                renderer.WriteSuccess($"Proceso {pid} ({name}) terminado, junto con sus procesos hijos.");
                return true;
            }
            catch (ArgumentException)
            {
                renderer.WriteError($"No existe el proceso {pid}.");
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                renderer.WriteError($"Sin permisos para terminar el proceso {pid}.");
                return false;
            }
            catch (InvalidOperationException)
            {
                renderer.WriteError($"El proceso {pid} ya había terminado.");
                return false;
            }
        }

        if (!context.Jobs.TryGetByPrefix(argument, out var job) || job is null)
        {
            renderer.WriteError($"No hay ningún trabajo que coincida con '{argument}'.");
            return false;
        }

        var pids = string.Join(", ", job.Processes.Select(p => p.Id));
        context.Jobs.Kill(job);
        renderer.WriteSuccess($"Trabajo [{job.JobId}] terminado (PID {pids}): {job.CommandLine}");
        return true;
    }
}
