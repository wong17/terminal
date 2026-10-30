using MiShell.Core.Contracts;
using System.Diagnostics;
using System.Globalization;

namespace MiShell.Commands.SystemInfo;

/// <summary><c>sysinfo</c>: panel de CPU, RAM y disco más información del equipo, procesador, arranque y unidades (equivale a systeminfo).</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SysInfoCommand(
    IOutputRenderer renderer,
    Func<CancellationToken, Task<double>>? cpuSampler = null,
    Func<(double Total, double Available)>? ramQuery = null) : IBuiltinCommand
{
    public string Name => "sysinfo";
    public string Description => "Panel con uso de CPU, RAM y disco.";

    /// <inheritdoc />
    public async Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cpuPercent = await (cpuSampler ?? SampleCpuPercentAsync)(cancellationToken).ConfigureAwait(false);
            var (ramTotalMb, ramAvailableMb) = (ramQuery ?? QueryRamMb)();
            cancellationToken.ThrowIfCancellationRequested();
            var drive = new DriveInfo(Path.GetPathRoot(context.WorkingDirectory) ?? "C:\\");
            const double bytesPerGb = 1024.0 * 1024.0 * 1024.0;
            var diskTotalGb = drive.TotalSize / bytesPerGb;
            var diskUsedGb = (drive.TotalSize - drive.AvailableFreeSpace) / bytesPerGb;
            renderer.RenderSystemInfo(new SystemUsage(cpuPercent, ramTotalMb - ramAvailableMb, ramTotalMb, diskUsedGb, diskTotalGb));
            if (cpuSampler is null && !Console.IsOutputRedirected) await RenderCoresAsync(cancellationToken).ConfigureAwait(false);
            RenderDetails();
            return CommandResult.Ok;
        }
        catch (Exception ex) when (ex is System.Management.ManagementException or System.ComponentModel.Win32Exception
            or InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            renderer.WriteError($"No se pudieron consultar los recursos de Windows: {ex.Message}");
            return CommandResult.Failure;
        }
    }

    /// <summary>
    /// Histograma de columnas con la carga de cada núcleo lógico durante 300 ms. Cada columna usa un bloque
    /// (▁ a █) según el porcentaje y el color del nivel: verde, amarillo o rojo.
    /// </summary>
    private async Task RenderCoresAsync(CancellationToken token)
    {
        try
        {
            var category = new PerformanceCounterCategory("Processor");
            var names = category.GetInstanceNames()
                .Where(n => n != "_Total" && !n.EndsWith("_Total", StringComparison.Ordinal))
                .OrderBy(n => n.Length).ThenBy(n => n, StringComparer.Ordinal).ToArray();
            var counters = names.Select(n => new PerformanceCounter("Processor", "% Processor Time", n, readOnly: true)).ToArray();
            try
            {
                foreach (var c in counters) _ = c.NextValue();
                await Task.Delay(300, token).ConfigureAwait(false);
                var values = counters.Select(c => Math.Clamp(c.NextValue(), 0, 100)).ToArray();
                const string Blocks = "▁▂▃▄▅▆▇█";
                renderer.Write("  Núcleos  ", renderer.Theme.Accent);
                foreach (var v in values)
                {
                    var block = Blocks[(int)Math.Min(Blocks.Length - 1, v / 100 * (Blocks.Length - 1) + 0.5)];
                    var color = v > 80 ? ConsoleColor.Red : v > 50 ? ConsoleColor.Yellow : ConsoleColor.Green;
                    renderer.Write($"{block}{block} ", color);
                }

                renderer.WriteLine();
                renderer.WriteLine($"           {values.Length} núcleos lógicos · promedio {values.Average():F0}% · máximo {values.Max():F0}%", renderer.Theme.Muted);
            }
            finally
            {
                foreach (var c in counters) c.Dispose();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            renderer.WriteInfo("No se pudo consultar la carga por núcleo.");
        }
    }

    private void RenderDetails()
    {
        var inv = CultureInfo.InvariantCulture;
        var rows = new List<string[]>
        {
            new[] { "Equipo", Environment.MachineName },
            new[] { "Usuario", Environment.UserName },
            new[] { "Sistema operativo", System.Runtime.InteropServices.RuntimeInformation.OSDescription },
            new[] { "Versión", Environment.OSVersion.VersionString },
            new[] { "Arquitectura", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString() },
            new[] { "Procesadores lógicos", Environment.ProcessorCount.ToString(inv) },
        };
        try
        {
            using var cpus = new System.Management.ManagementObjectSearcher("SELECT Name, NumberOfCores, MaxClockSpeed FROM Win32_Processor");
            foreach (var cpu in cpus.Get())
            {
                using (cpu)
                {
#pragma warning disable CA1507 // "Name" es el nombre de la propiedad WMI, no el de esta clase.
                    rows.Add(["Procesador", $"{cpu["Name"]}".Trim()]);
#pragma warning restore CA1507
                    rows.Add(["Núcleos físicos / frecuencia", $"{cpu["NumberOfCores"]} / {cpu["MaxClockSpeed"]} MHz"]);
                    break;
                }
            }

            using var os = new System.Management.ManagementObjectSearcher(
                "SELECT TotalVirtualMemorySize, FreeVirtualMemory, LastBootUpTime, BuildNumber FROM Win32_OperatingSystem");
            foreach (var item in os.Get())
            {
                using (item)
                {
                    rows.Add(["Compilación de Windows", $"{item["BuildNumber"]}"]);
                    var boot = System.Management.ManagementDateTimeConverter.ToDateTime($"{item["LastBootUpTime"]}");
                    rows.Add(["Último arranque", MiShell.Core.ClockFormat.DateAndTime(boot)]);
                    var total = Convert.ToDouble(item["TotalVirtualMemorySize"], inv) / 1024.0;
                    var free = Convert.ToDouble(item["FreeVirtualMemory"], inv) / 1024.0;
                    rows.Add(["Memoria virtual", $"{MiShell.Core.ByteFormat.FromMegabytes(total - free)} usados de {MiShell.Core.ByteFormat.FromMegabytes(total)}"]);
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is System.Management.ManagementException or System.Runtime.InteropServices.COMException
            or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            rows.Add(["Detalles de WMI", $"no disponibles ({ex.GetType().Name})"]);
        }

        var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
        rows.Add(["Tiempo encendido", $"{(int)up.TotalDays} d {up.Hours:00}:{up.Minutes:00}:{up.Seconds:00}"]);
        rows.Add(["Procesos activos", Process.GetProcesses().Length.ToString(inv)]);
        rows.Add(["Runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription]);
        rows.Add(["Directorio del sistema", Environment.SystemDirectory]);
        rows.Add(["Zona horaria", TimeZoneInfo.Local.DisplayName]);
        renderer.RenderList("Equipo y sistema", rows.Select(r => (r[0], r[1])));

        var drives = new List<string[]>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                var total = drive.TotalSize;
                var free = drive.AvailableFreeSpace;
                drives.Add([drive.Name, drive.DriveType.ToString(), drive.DriveFormat, MiShell.Core.ByteFormat.Human(total), MiShell.Core.ByteFormat.Human(free),
                    total > 0 ? UsageCell((double)(total - free) / total) : "-", drive.VolumeLabel]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        renderer.RenderTable("Unidades", ["Unidad", "Tipo", "Formato", "Total", "Libre", "Uso", "Etiqueta"], drives,
            cellColor: (row, column) => column == 5 ? UsageColor(row[5]) : null);
    }

    /// <summary>Porcentaje de uso y, si la salida es interactiva, una barra de diez posiciones.</summary>
    private static string UsageCell(double ratio)
    {
        var text = $"{ratio * 100:F0}%";
        if (Console.IsOutputRedirected) return text;
        var filled = (int)Math.Round(Math.Clamp(ratio, 0, 1) * 10);
        return $"{text,4} {new string('█', filled)}{new string('░', 10 - filled)}";
    }

    private static ConsoleColor? UsageColor(string cell) =>
        int.TryParse(cell.Split('%')[0].Trim(), out var pct) ? pct > 80 ? ConsoleColor.Red : pct > 50 ? ConsoleColor.Yellow : ConsoleColor.Green : null;

    private static async Task<double> SampleCpuPercentAsync(CancellationToken token)
    {
        using var cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
        _ = cpu.NextValue();
        await Task.Delay(300, token).ConfigureAwait(false);
        return Math.Clamp(cpu.NextValue(), 0, 100);
    }

    private static (double Total, double Available) QueryRamMb()
    {
        using var query = new System.Management.ManagementObjectSearcher(
            "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
        using var results = query.Get();
        foreach (var item in results)
        {
            using (item)
                return (Convert.ToDouble(item["TotalVisibleMemorySize"], CultureInfo.InvariantCulture) / 1024.0,
                    Convert.ToDouble(item["FreePhysicalMemory"], CultureInfo.InvariantCulture) / 1024.0);
        }
        throw new InvalidOperationException("WMI no devolvió información de memoria.");
    }
}
