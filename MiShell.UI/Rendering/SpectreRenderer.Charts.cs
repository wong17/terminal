using MiShell.Core.Contracts;
using Spectre.Console;

namespace MiShell.UI.Rendering;

// Gráficos de barras y panel de recursos del sistema (sysinfo, ps --chart).
public sealed partial class SpectreRenderer
{
    /// <inheritdoc />
    public void RenderBarChart(string title, IEnumerable<(string Label, double Value, string ColorHint)> items, string valueSuffix, Func<double, string>? format = null)
    {
        var list = items.ToArray();
        string Value(double v) => format?.Invoke(v) ?? v.ToString("F1", Invariant);
        var heading = format is null ? $"{title} ({valueSuffix})" : title;
        if (IsPlain)
        {
            RenderTable(heading, ["Nombre", valueSuffix.Length > 0 ? $"Valor ({valueSuffix})" : "Valor"],
                list.Select(i => (string[])[i.Label, Value(i.Value)]));
            return;
        }

        if (OutputStyle == OutputStyles.Boxed)
        {
            var chart = new BarChart()
                .Width(60)
                .Label($"[bold]{Markup.Escape(heading)}[/]")
                .CenterLabel();
            foreach (var (label, value, colorHint) in list)
                chart.AddItem(Markup.Escape(Truncate(label, 28)), value, HexColor(colorHint));
            _console.Write(chart);
            return;
        }

        var theme = themeManager.Current;
        _writer.WriteLine(Format($"  {heading}", theme.Accent));
        var max = Math.Max(1, list.Length == 0 ? 1 : list.Max(i => i.Value));
        var labelWidth = list.Length == 0 ? 0 : list.Max(i => Math.Min(24, i.Label.Length));
        var valueWidth = list.Length == 0 ? 0 : list.Max(i => Value(i.Value).Length);
        foreach (var (label, value, hint) in list)
        {
            var color = hint switch { "red" => theme.ErrorColor, "yellow" => theme.Info, _ => theme.Accent };
            _writer.WriteLine($"  {Truncate(label, 24).PadRight(labelWidth)} {Format(BuildMiniBar(value / max, 40), color)} {Value(value).PadLeft(valueWidth)}");
        }
    }

    /// <inheritdoc />
    public void RenderSystemInfo(SystemUsage usage)
    {
        var accent = themeManager.Current.AccentHex;
        if (IsPlain)
        {
            RenderTable("Sistema", ["Recurso", "Uso"],
            [
                ["CPU", $"{usage.CpuPercent:F1}%"],
                ["RAM", $"{MiShell.Core.ByteFormat.FromMegabytes(usage.RamUsedMb)} / {MiShell.Core.ByteFormat.FromMegabytes(usage.RamTotalMb)}"],
                ["RAM libre", MiShell.Core.ByteFormat.FromMegabytes(usage.RamTotalMb - usage.RamUsedMb)],
                ["Disco", $"{MiShell.Core.ByteFormat.Human((long)(usage.DiskUsedGb * 1073741824))} / {MiShell.Core.ByteFormat.Human((long)(usage.DiskTotalGb * 1073741824))}"],
                ["Disco libre", MiShell.Core.ByteFormat.Human((long)((usage.DiskTotalGb - usage.DiskUsedGb) * 1073741824))],
            ]);
            return;
        }

        var cpu = usage.CpuPercent / 100;
        var ram = usage.RamUsedMb / Math.Max(1, usage.RamTotalMb);
        var disk = usage.DiskUsedGb / Math.Max(1, usage.DiskTotalGb);
        if (OutputStyle != OutputStyles.Boxed)
        {
            var theme = themeManager.Current;
            _writer.WriteLine(Format("  Recursos", theme.Accent));
            foreach (var (name, ratio, detail) in new[]
            {
                ("CPU", cpu, string.Empty),
                ("RAM", ram, $"{MiShell.Core.ByteFormat.FromMegabytes(usage.RamUsedMb)} / {MiShell.Core.ByteFormat.FromMegabytes(usage.RamTotalMb)}"),
                ("Disco", disk, $"{MiShell.Core.ByteFormat.Human((long)(usage.DiskUsedGb * 1073741824))} / {MiShell.Core.ByteFormat.Human((long)(usage.DiskTotalGb * 1073741824))}"),
            })
            {
                _writer.WriteLine($"{name,-6} {Format(BuildMiniBar(ratio, 24), RatioConsoleColor(ratio))} {(int)Math.Clamp(ratio * 100, 0, 100),3}%  {detail}");
            }

            return;
        }

        var grid = new Grid().Expand();
        _ = grid.AddColumn();
        _ = grid.AddColumn();
        _ = grid.AddRow(
            new Markup($"[bold #{accent}]MiShell[/] en [bold]{Markup.Escape(Environment.MachineName)}[/]"),
            new Markup(Markup.Escape(MiShell.Core.ClockFormat.Hour(DateTime.Now))));
        _ = grid.AddRow(new Markup($"OS {Markup.Escape(Environment.OSVersion.VersionString)}"), new Text(string.Empty));
        _ = grid.AddRow(new Markup($"Usuario {Markup.Escape(Environment.UserName)}"), new Text(string.Empty));
        _ = grid.AddRow(new Text(string.Empty), new Text(string.Empty));
        _ = grid.AddRow(new Markup("CPU"), new Markup(BuildRatioBar(cpu)));
        _ = grid.AddRow(new Markup("RAM"), new Markup(BuildRatioBar(ram)));
        _ = grid.AddRow(new Markup("Disco"), new Markup(BuildRatioBar(disk)));
        _ = grid.AddRow(
            new Markup($"{MiShell.Core.ByteFormat.FromMegabytes(usage.RamUsedMb)} / {MiShell.Core.ByteFormat.FromMegabytes(usage.RamTotalMb)} · {MiShell.Core.ByteFormat.Human((long)(usage.DiskUsedGb * 1073741824))} / {MiShell.Core.ByteFormat.Human((long)(usage.DiskTotalGb * 1073741824))}"),
            new Text(string.Empty));

        _console.Write(new Panel(grid)
            .Border(BoxBorder.Rounded)
            .BorderColor(HexColor(accent))
            .Header($"[bold #{accent}] Información del sistema [/]"));
    }

    private static string BuildRatioBar(double ratio) =>
        $"[{RatioColor(ratio)}]{BuildMiniBar(ratio, 20)}[/] {((int)Math.Clamp(ratio * 100, 0, 100)).ToString(Invariant)}%";

    /// <summary>Verde hasta 50 %, amarillo hasta 80 % y rojo por encima.</summary>
    private static string RatioColor(double ratio) => ratio > 0.8 ? "red" : ratio > 0.5 ? "yellow" : "green";

    private static ConsoleColor RatioConsoleColor(double ratio) =>
        ratio > 0.8 ? ConsoleColor.Red : ratio > 0.5 ? ConsoleColor.Yellow : ConsoleColor.Green;
}
