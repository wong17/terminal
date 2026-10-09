using MiShell.Core.Contracts;
using System.Diagnostics;
using System.Globalization;

namespace MiShell.Commands.BuiltinCommands;


public sealed class PsCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "ps";
    public string Description => "Lista procesos con CPU/RAM, filtro y orden; --chart muestra barras.";
    public string Usage => "ps [n] [--chart] [--name texto] [--sort ram|cpu|pid]";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var count = 15;
        var chart = false;
        var sort = "ram";
        string? filter = null;
        var sawCount = false;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            switch (argument)
            {
                case "--chart": chart = true; break;
                case "--name" when i + 1 < arguments.Length: filter = arguments[++i]; break;
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
                        process.WorkingSet64 / 1024.0 / 1024.0));
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
        renderer.WriteInfo("CPU (s): tiempo acumulado, no porcentaje instantáneo. RAM: memoria de trabajo. Barras relativas al mayor valor mostrado.");
        return Task.FromResult(CommandResult.Ok);

        Task<CommandResult> InvalidUsage()
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SysInfoCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "sysinfo";

    public string Description => "Panel con uso de CPU, RAM y disco.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            double cpuPercent = SampleCpuPercent();
            double ramTotalMb = QueryTotalRamMb();
            double ramAvailableMb = QueryAvailableRamMb();
            double diskTotalGb, diskUsedGb;
            var drive = new DriveInfo(Path.GetPathRoot(context.WorkingDirectory) ?? "C:\\");
            const double bytesPerGb = 1024.0 * 1024.0 * 1024.0;
            diskTotalGb = drive.TotalSize / bytesPerGb;
            diskUsedGb = (drive.TotalSize - drive.AvailableFreeSpace) / bytesPerGb;

            renderer.RenderSystemInfo(new SystemUsage(cpuPercent, ramTotalMb - ramAvailableMb, ramTotalMb, diskUsedGb, diskTotalGb));
            renderer.WriteInfo($"CPU: {cpuPercent:F1}%; RAM libre: {ramAvailableMb:F0} MiB; disco libre: {diskTotalGb - diskUsedGb:F1} GiB.");
            renderer.WriteInfo($"Disco consultado: {Path.GetPathRoot(context.WorkingDirectory)}. CPU es una muestra de 300 ms; RAM y disco son instantáneas.");
            return Task.FromResult(CommandResult.Ok);
        }
        catch (Exception ex) when (ex is System.Management.ManagementException or System.ComponentModel.Win32Exception
            or InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            renderer.WriteError($"No se pudieron consultar los recursos de Windows: {ex.Message}");
            return Task.FromResult(CommandResult.Failure);
        }
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
