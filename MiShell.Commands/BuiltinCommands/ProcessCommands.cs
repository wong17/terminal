using MiShell.Core.Contracts;
using System.Diagnostics;
using System.Globalization;

namespace MiShell.Commands.BuiltinCommands;

public sealed class PsCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "ps";

    public string Description => "Lista procesos con CPU y RAM; usa --chart para gráfico de barras.";

    public string Usage => "ps [n] [--chart]";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var chart = arguments.Contains("--chart");
        var count = arguments.Where(a => a != "--chart").Select(a => int.TryParse(a, out var n) ? n : 0).FirstOrDefault(n => n > 0, 15);

        var processes = Process.GetProcesses()
            .Select(p =>
            {
                try
                {
                    return new ProcessInfo(p.Id, p.ProcessName, p.TotalProcessorTime.TotalSeconds, p.WorkingSet64 / 1024.0 / 1024.0);
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Procesos del sistema protegidos: se omiten.
                    return null;
                }
            })
            .OfType<ProcessInfo>()
            .OrderByDescending(p => p.WorkingSetMb)
            .Take(count)
            .ToList();

        if (chart)
        {
            renderer.RenderBarChart(
                $"Memoria por proceso (top {processes.Count})",
                processes.Select(p => (p.Name, Math.Round(p.WorkingSetMb, 1), p.WorkingSetMb > 300 ? "red" : p.WorkingSetMb > 100 ? "yellow" : "green")),
                "MB");
        }
        else
        {
            renderer.RenderProcessTable(processes);
        }

        return Task.FromResult(CommandResult.Ok);
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SysInfoCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "sysinfo";

    public string Description => "Panel con uso de CPU, RAM y disco.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        double cpuPercent = SampleCpuPercent();
        double ramTotalMb = QueryTotalRamMb();
        double ramAvailableMb = QueryAvailableRamMb();
        double diskTotalGb, diskUsedGb;
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\");
            const double bytesPerGb = 1024.0 * 1024.0 * 1024.0;
            diskTotalGb = drive.TotalSize / bytesPerGb;
            diskUsedGb = (drive.TotalSize - drive.AvailableFreeSpace) / bytesPerGb;
        }
        catch (DriveNotFoundException)
        {
            diskTotalGb = 0;
            diskUsedGb = 0;
        }

        renderer.RenderSystemInfo(new SystemUsage(cpuPercent, ramTotalMb - ramAvailableMb, ramTotalMb, diskUsedGb, diskTotalGb));
        return Task.FromResult(CommandResult.Ok);
    }

    private static double SampleCpuPercent()
    {
        using var cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
        _ = cpu.NextValue(); // Primera lectura siempre es 0; se descarta.
        Thread.Sleep(300);
        return Math.Clamp(cpu.NextValue(), 0, 100);
    }

    private static double QueryTotalRamMb()
    {
        using var query = new System.Management.ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
        return query.Get().Cast<System.Management.ManagementBaseObject>().Select(item => Convert.ToDouble(item["TotalVisibleMemorySize"], CultureInfo.InvariantCulture) / 1024.0).FirstOrDefault();
    }

    private static double QueryAvailableRamMb()
    {
        using var query = new System.Management.ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
        return query.Get().Cast<System.Management.ManagementBaseObject>().Select(item => Convert.ToDouble(item["FreePhysicalMemory"], CultureInfo.InvariantCulture) / 1024.0).FirstOrDefault();
    }
}
