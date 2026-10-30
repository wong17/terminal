using MiShell.Core.Contracts;
using MiShell.Core.Jobs;

namespace MiShell.Commands.Processes;

/// <summary><c>fg</c>: trae un trabajo en segundo plano al primer plano esperando sus procesos y copias de flujos; devuelve el código de su última etapa.</summary>
public sealed class FgCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "fg";

    public string Description => "Espera a que un trabajo en segundo plano termine.";

    public string Usage => "fg [job-id]";

    /// <inheritdoc />
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
