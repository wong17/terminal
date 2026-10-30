using MiShell.Core.Contracts;
using MiShell.Core.Jobs;
using System.ComponentModel;
using System.Diagnostics;

namespace MiShell.Commands.Processes;

/// <summary>
/// <c>suspend</c> y <c>resume</c>: pausan y reanudan un proceso (por PID) o un trabajo (<c>%id</c>) sin terminarlo,
/// el equivalente en Windows de enviar SIGSTOP y SIGCONT. El estado Suspendido se ve en <c>jobs</c>, <c>ps</c> y <c>top</c>.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SuspendResumeCommand(IOutputRenderer renderer, bool suspend) : IBuiltinCommand
{
    public string Name => suspend ? "suspend" : "resume";

    public string Description => suspend ? "Pausa un proceso o trabajo sin terminarlo." : "Reanuda un proceso o trabajo pausado.";

    public string Usage => $"{Name} %<job-id> | <pid> [...]";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }

        var failed = false;
        foreach (var argument in arguments) failed |= !Apply(argument, context);
        return Task.FromResult(failed ? CommandResult.Failure : CommandResult.Ok);
    }

    private bool Apply(string argument, ICommandContext context)
    {
        if (!argument.StartsWith('%') && int.TryParse(argument, out var pid) && pid > 0)
        {
            var job = context.Jobs.Jobs.FirstOrDefault(j => !j.HasExited && j.Processes.Any(p => SafeId(p) == pid));
            if (!Change(pid, out var name)) return false;
            if (job is not null) job.Suspended = suspend;
            renderer.WriteSuccess($"Proceso {pid} ({name}) {(suspend ? "suspendido" : "reanudado")}.");
            return true;
        }

        if (!context.Jobs.TryGetByPrefix(argument, out var found) || found is null)
        {
            renderer.WriteError($"No hay ningún trabajo que coincida con '{argument}'. Usa jobs para ver los números.");
            return false;
        }

        if (found.HasExited)
        {
            renderer.WriteError($"El trabajo [{found.JobId}] ya terminó.");
            return false;
        }

        foreach (var process in found.Processes.Where(p => !SafeExited(p)))
        {
            if (!Change(process.Id, out _)) return false;
        }

        found.Suspended = suspend;
        renderer.WriteSuccess($"Trabajo [{found.JobId}] {(suspend ? "suspendido" : "reanudado")} (PID {string.Join(", ", found.Processes.Select(SafeId))}): {found.CommandLine}");
        return true;
    }

    /// <summary>Aplica la operación a un PID y explica el motivo si no es posible.</summary>
    private bool Change(int pid, out string name)
    {
        name = string.Empty;
        if (pid == Environment.ProcessId)
        {
            renderer.WriteError("No se puede suspender a MiShell: dejaría de responder.");
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            name = process.ProcessName;
            ProcessControl.Set(pid, suspend);
            return true;
        }
        catch (ArgumentException)
        {
            renderer.WriteError($"No existe el proceso {pid}.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            renderer.WriteError($"Sin permisos para {(suspend ? "suspender" : "reanudar")} el proceso {pid}: es un proceso protegido o de otro usuario.");
        }
        catch (Win32Exception ex)
        {
            renderer.WriteError($"No se pudo {(suspend ? "suspender" : "reanudar")} el proceso {pid}: {ex.Message}");
        }

        return false;
    }

    private static int SafeId(Process process)
    {
        try { return process.Id; }
        catch (InvalidOperationException) { return -1; }
    }

    private static bool SafeExited(Process process)
    {
        try { return process.HasExited; }
        catch (InvalidOperationException) { return true; }
    }
}
