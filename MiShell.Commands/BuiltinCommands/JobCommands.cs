using MiShell.Core.Contracts;
using MiShell.Core.Jobs;

namespace MiShell.Commands.BuiltinCommands;

public sealed class JobsCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "jobs";

    public string Description => "Lista los procesos lanzados en segundo plano.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var jobs = context.Jobs.Jobs;
        if (jobs.Count == 0)
        {
            renderer.WriteInfo("No hay trabajos en segundo plano.");
            return Task.FromResult(CommandResult.Ok);
        }

        renderer.RenderTable(
            "Trabajos en segundo plano",
            ["Job", "PID", "Estado", "Comando"],
            jobs.Select(j => (string[])
            [
                j.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                j.Process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                j.HasExited ? "terminado" : "corriendo",
                j.CommandLine,
            ]));

        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class KillCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "kill";

    public string Description => "Termina un trabajo en segundo plano.";

    public string Usage => "kill <job-id | pid | nombre>";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            renderer.WriteError("Uso: kill <job-id | pid | nombre>");
            return Task.FromResult(CommandResult.Failure);
        }

        if (int.TryParse(arguments[0], out var pid) && pid > 0)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(pid);
                process.Kill(entireProcessTree: true);
                renderer.WriteSuccess($"Proceso {pid} terminado.");
                return Task.FromResult(CommandResult.Ok);
            }
            catch (ArgumentException)
            {
                renderer.WriteError($"No existe el proceso {pid}.");
                return Task.FromResult(CommandResult.Failure);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                renderer.WriteError($"Sin permisos para terminar el proceso {pid}.");
                return Task.FromResult(CommandResult.Failure);
            }
        }

        if (!context.Jobs.TryGetByPrefix(arguments[0], out var job) || job is null)
        {
            renderer.WriteError($"No hay ningún trabajo que coincida con '{arguments[0]}'.");
            return Task.FromResult(CommandResult.Failure);
        }

        context.Jobs.Kill(job);
        renderer.WriteSuccess($"Trabajo [{job.JobId}] terminado.");
        return Task.FromResult(CommandResult.Ok);

    }
}

public sealed class FgCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "fg";

    public string Description => "Espera a que un trabajo en segundo plano termine.";

    public string Usage => "fg [job-id]";

    public async Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var allJobs = context.Jobs.Jobs;
        BackgroundJob? job = null;
        if (arguments.Length > 0)
        {
            if (!context.Jobs.TryGetByPrefix(arguments[0], out job) || job is null)
            {
                renderer.WriteError($"No hay ningún trabajo que coincida con '{arguments[0]}'.");
                return CommandResult.Failure;
            }
        }
        else if (allJobs.Count > 0)
        {
            job = allJobs[^1];
        }

        if (job is null)
        {
            renderer.WriteError("No hay trabajos en segundo plano.");
            return CommandResult.Failure;
        }

        renderer.WriteInfo($"Esperando [{job.JobId}] {job.CommandLine} (PID {job.Process.Id})…");
        await job.Process.WaitForExitAsync(cancellationToken);
        renderer.WriteSuccess($"[{job.JobId}] terminó con código {job.Process.ExitCode}.");
        return CommandResult.Ok;
    }
}
