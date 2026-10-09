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
            ["Job", "PID(s)", "Estado", "Resultado", "Comando"],
            jobs.Select(j => (string[])
            [
                j.JobId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Join(", ", j.Processes.Select(p => p.Id)),
                j.Status,
                j.Detail,
                j.CommandLine,
            ]));

        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class KillCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "kill";

    public string Description => "Termina un trabajo en segundo plano.";

    public string Usage => "kill %<job-id> | <pid> | <nombre>";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length != 1)
        {
            renderer.WriteError("Uso: kill %<job-id> | <pid> | <nombre>");
            return Task.FromResult(CommandResult.Failure);
        }

        if (int.TryParse(arguments[0], out var pid) && pid > 0)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
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
        try
        {
            var code = await job.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (code == 0) renderer.WriteInfo($"[{job.JobId}] terminó con código {code}.");
            else renderer.WriteError($"[{job.JobId}] falló con código {code}.");
            return new CommandResult(code);
        }
        catch (OperationCanceledException)
        {
            context.Jobs.Kill(job);
            try { await job.Completion.ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
            throw;
        }
        catch (IOException ex)
        {
            renderer.WriteError($"[{job.JobId}] falló: {ex.Message}");
            return CommandResult.Failure;
        }
        finally { context.Jobs.Remove(job); }
    }
}
