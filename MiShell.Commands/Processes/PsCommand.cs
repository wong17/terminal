using MiShell.Core.Contracts;
using System.Diagnostics;
using System.Globalization;

namespace MiShell.Commands.BuiltinCommands;

/// <summary><c>ps</c>: lista procesos como Get-Process (PID, hilos, handles, sesión, CPU, memoria, inicio) y con <c>--id</c> detalla uno.</summary>
public sealed class PsCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "ps";
    public string Description => "Lista procesos (PID, hilos, handles, CPU, memoria, inicio); --id muestra el detalle de uno.";
    public string Usage => "ps [n | --all] [--chart] [--name texto] [--sort ram|cpu|pid] [--id PID]";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var count = 15;
        var chart = false;
        var sort = "ram";
        string? filter = null;
        int? id = null;
        var sawCount = false;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            switch (argument)
            {
                case "--chart": chart = true; break;
                case "--all": count = int.MaxValue; sawCount = true; break;
                case "--name" when i + 1 < arguments.Length: filter = arguments[++i]; break;
                case "--id" when i + 1 < arguments.Length && int.TryParse(arguments[i + 1], out var pid) && pid > 0: id = pid; i++; break;
                case "--sort" when i + 1 < arguments.Length:
                    sort = arguments[++i].ToLowerInvariant();
                    if (sort is not "ram" and not "cpu" and not "pid") return InvalidUsage();
                    break;
                default:
                    if (sawCount || !int.TryParse(argument, out count) || count < 1) return InvalidUsage();
                    sawCount = true;
                    break;
            }
        }

        if (id is { } wanted) return Task.FromResult(Detail(wanted));

        var accessible = new List<ProcessInfo>();
        var skipped = 0;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (filter is not null && !process.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                    accessible.Add(new ProcessInfo(process.Id, process.ProcessName, process.TotalProcessorTime.TotalSeconds,
                        process.WorkingSet64 / 1024.0 / 1024.0, process.HandleCount, process.Threads.Count, process.SessionId,
                        process.PrivateMemorySize64 / 1024.0 / 1024.0, SafeStart(process)));
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    skipped++;
                }
            }
        }

        var ordered = sort switch
        {
            "cpu" => accessible.OrderByDescending(p => p.CpuSeconds),
            "pid" => accessible.OrderBy(p => p.Pid),
            _ => accessible.OrderByDescending(p => p.WorkingSetMb),
        };
        var processes = ordered.Take(count).ToArray();
        if (chart)
            renderer.RenderBarChart($"Memoria de {processes.Length} procesos (orden: {sort})",
                processes.Select(p => (p.Name, Math.Round(p.WorkingSetMb, 1), "cyan")), "MiB");
        else renderer.RenderProcessTable(processes);

        renderer.WriteInfo($"Mostrados: {processes.Length} de {accessible.Count} coincidencias; omitidos por acceso/cierre: {skipped}.");
        renderer.WriteInfo("CPU: segundos acumulados · PM: memoria privada · RAM: memoria de trabajo · SI: sesión. Detalle: ps --id PID.");
        return Task.FromResult(CommandResult.Ok);

        Task<CommandResult> InvalidUsage()
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }
    }

    private static DateTime? SafeStart(Process process)
    {
        try { return process.StartTime; }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { return null; }
    }

    private static string Safe(Func<string> read)
    {
        try { return read(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { return "(sin acceso)"; }
    }

    private CommandResult Detail(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            var inv = CultureInfo.InvariantCulture;
            var rows = new List<string[]>
            {
                new[] { "PID", p.Id.ToString(inv) },
                new[] { "Nombre", p.ProcessName },
                new[] { "Ruta", Safe(() => p.MainModule?.FileName ?? "(desconocida)") },
                new[] { "Versión", Safe(() => p.MainModule?.FileVersionInfo.FileVersion ?? "-") },
                new[] { "Inicio", Safe(() => p.StartTime.ToString("yyyy-MM-dd HH:mm:ss", inv)) },
                new[] { "Sesión", p.SessionId.ToString(inv) },
                new[] { "Prioridad", Safe(() => p.PriorityClass.ToString()) },
                new[] { "Hilos", p.Threads.Count.ToString(inv) },
                new[] { "Handles", p.HandleCount.ToString(inv) },
                new[] { "CPU usuario (s)", Safe(() => p.UserProcessorTime.TotalSeconds.ToString("F2", inv)) },
                new[] { "CPU sistema (s)", Safe(() => p.PrivilegedProcessorTime.TotalSeconds.ToString("F2", inv)) },
                new[] { "Memoria de trabajo (MiB)", (p.WorkingSet64 / 1048576.0).ToString("F1", inv) },
                new[] { "Pico de trabajo (MiB)", (p.PeakWorkingSet64 / 1048576.0).ToString("F1", inv) },
                new[] { "Memoria privada (MiB)", (p.PrivateMemorySize64 / 1048576.0).ToString("F1", inv) },
                new[] { "Memoria virtual (MiB)", (p.VirtualMemorySize64 / 1048576.0).ToString("F1", inv) },
                new[] { "Responde", p.MainWindowHandle == IntPtr.Zero ? "(sin ventana)" : p.Responding ? "sí" : "no" },};
            renderer.RenderList($"Proceso {pid}", rows.Select(r => (r[0], r[1])));
            return CommandResult.Ok;
        }
        catch (ArgumentException)
        {
            renderer.WriteError($"No existe el proceso {pid}.");
            return CommandResult.Failure;
        }
    }
}
