using MiShell.Core.Contracts;
using MiShell.UI.Prompt;
using Spectre.Console;
using System.Text;
using System.Text.RegularExpressions;

namespace MiShell.UI.Rendering;

// Tablas y listas. Hay dos estilos: "classic" (columnas alineadas, como dir/Get-ChildItem) y
// "boxed" (tablas con borde de Spectre). Con la salida redirigida siempre se escribe texto separado por tabuladores.
public sealed partial class SpectreRenderer
{
    private const int ColumnGutter = 2;
    private const int MinColumnWidth = 8;

    [GeneratedRegex(@"^-?[\d.,:]+( ?(B|KB|MB|GB|TB|KiB|MiB|GiB|TiB|%|s|MHz|d))?$")]
    private static partial Regex NumericCell();

    /// <inheritdoc />
    public void RenderTable(string title, string[] columns, IEnumerable<string[]> rows,
        Func<string[], ConsoleColor?>? rowColor = null, Func<string[], int, ConsoleColor?>? cellColor = null)
    {
        if (IsPlain)
        {
            WriteLine(string.Join('\t', columns));
            foreach (var row in rows) WriteLine(string.Join('\t', row));
            return;
        }

        if (OutputStyle == OutputStyles.Boxed) RenderBoxedTable(title, columns, rows);
        else RenderClassicTable(title, columns, [.. rows], rowColor, cellColor);
    }

    /// <inheritdoc />
    public void RenderList(string title, IEnumerable<(string Label, string Value)> items)
    {
        var list = items.ToArray();
        if (IsPlain)
        {
            foreach (var (label, value) in list) WriteLine($"{label}: {value}");
            return;
        }

        if (OutputStyle == OutputStyles.Boxed)
        {
            RenderBoxedTable(title, ["Dato", "Valor"], list.Select(i => new[] { i.Label, i.Value }));
            return;
        }

        var theme = themeManager.Current;
        if (title.Length > 0) _writer.WriteLine(Format($"  {title}", theme.Accent));
        var width = list.Length == 0 ? 0 : list.Max(i => PromptRenderer.VisibleWidth(i.Label));
        foreach (var (label, value) in list)
        {
            var padded = label + new string(' ', width - PromptRenderer.VisibleWidth(label));
            _writer.WriteLine($"{Format(padded, theme.Accent)} {Format(":", theme.Muted)} {value}");
        }
    }

    /// <inheritdoc />
    public void RenderProcessTable(IEnumerable<ProcessInfo> processes, bool detailed = false)
    {
        var theme = themeManager.Current;
        var infos = processes.ToList();
        if (infos.Count == 0) { WriteInfo("No hay procesos que coincidan."); return; }
        var maxRam = Math.Max(1, infos.Max(p => p.WorkingSetMb));
        string[] columns = detailed
            ? ["PID", "Nombre", "Estado", "Hilos", "Handles", "Sesión", "CPU", "Mem. privada", "Memoria", "% RAM", "Inicio"]
            : ["PID", "Nombre", "Estado", "Hilos", "CPU", "Memoria", "% RAM", "Inicio"];
        var rows = infos.Select(p =>
        {
            var cpu = p.CpuSeconds < 0 ? "-" : MiShell.Core.ByteFormat.Duration(TimeSpan.FromSeconds(p.CpuSeconds));
            var start = p.StartTime is { } s ? MiShell.Core.ClockFormat.Moment(s) : "-";
            var ram = MiShell.Core.ByteFormat.FromMegabytes(p.WorkingSetMb);
            var percent = p.RamPercent.ToString("F1", Invariant) + "%";
            var pid = p.Pid == 0 ? "-" : p.Pid.ToString(Invariant);
            return detailed
                ? (string[])[pid, p.Name, p.State, p.Threads.ToString(Invariant), p.Handles.ToString(Invariant), p.SessionId.ToString(Invariant),
                    cpu, MiShell.Core.ByteFormat.FromMegabytes(p.PrivateMb), ram, percent, start]
                : [pid, p.Name, p.State, p.Threads.ToString(Invariant), cpu, ram, percent, start];
        }).ToList();
        if (IsPlain)
        {
            RenderTable("Procesos", columns, rows);
            return;
        }

        // La barra de memoria es una columna más; lleva el color del tema.
        columns = [.. columns, "Barra"];
        for (var i = 0; i < rows.Count; i++)
            rows[i] = [.. rows[i], Format(BuildMiniBar(infos[i].WorkingSetMb / maxRam, 10), theme.Accent)];

        var state = Array.IndexOf(columns, "Estado");
        var memory = Array.IndexOf(columns, "Memoria");

        // PID atenuado, nombre destacado, estado con color; la memoria se resalta desde 500 MB y alerta desde 1 GB.
        ConsoleColor? Paint(string[] row, int column)
        {
            if (column == 0) return theme.Muted;
            if (column == 1) return theme.Command;
            if (column == state)
                return row[state] switch { MiShell.Core.Jobs.ProcessStateReader.Running => theme.Success, MiShell.Core.Jobs.ProcessStateReader.Suspended => theme.Info, "-" => theme.Muted, _ => theme.Muted };
            if (column != memory) return null;
            var mb = infos[rows.IndexOf(row)].WorkingSetMb;
            return mb >= 1024 ? theme.ErrorColor : mb >= 500 ? theme.Info : null;
        }

        RenderTable("Procesos", columns, rows, cellColor: Paint);
    }

    /// <summary>Estilo clásico: título, encabezado con subrayado de guiones y columnas alineadas sin bordes.</summary>
    private void RenderClassicTable(string title, string[] columns, IReadOnlyList<string[]> rows,
        Func<string[], ConsoleColor?>? rowColor, Func<string[], int, ConsoleColor?>? cellColor)
    {
        var theme = themeManager.Current;
        var count = columns.Length;
        var rightAligned = Enumerable.Range(0, count).Select(c => IsNumericColumn(rows, c)).ToArray();
        var widths = Enumerable.Range(0, count)
            .Select(c => Math.Max(PromptRenderer.VisibleWidth(columns[c]), rows.Count == 0 ? 0 : rows.Max(r => PromptRenderer.VisibleWidth(r[c]))))
            .ToArray();
        FitToWindow(widths, SafeWindowWidth() - 1);

        if (title.Length > 0) _writer.WriteLine(Format($"  {title}", theme.Accent));

        var header = new StringBuilder();
        var rule = new StringBuilder();
        for (var c = 0; c < count; c++)
        {
            if (c > 0) { header.Append(' ', ColumnGutter); rule.Append(' ', ColumnGutter); }
            header.Append(Fit(columns[c], widths[c], rightAligned[c]));
            rule.Append('-', widths[c]);
        }

        _writer.WriteLine(Format("\e[1m" + header, theme.Accent) + "\e[22m");
        _writer.WriteLine(Format(rule.ToString(), theme.Muted));
        foreach (var row in rows)
        {
            var line = new StringBuilder();
            var whole = rowColor?.Invoke(row);
            for (var c = 0; c < count; c++)
            {
                if (c > 0) line.Append(' ', ColumnGutter);
                var cell = Fit(row[c], widths[c], rightAligned[c]);
                if (c == count - 1) cell = cell.TrimEnd();
                line.Append(Format(cell, cellColor?.Invoke(row, c) ?? whole));
            }

            _writer.WriteLine(line.ToString());
        }
    }

    /// <summary>Estilo con bordes: tabla redondeada de Spectre con el título en una línea propia.</summary>
    private void RenderBoxedTable(string title, string[] columns, IEnumerable<string[]> rows)
    {
        var accent = themeManager.Current.AccentHex;
        var table = new Table().Border(TableBorder.Rounded).BorderColor(HexColor(accent));
        // El título va como línea propia: dentro de la tabla se partía con rutas largas.
        if (title.Length > 0) _console.MarkupLine($"[bold #{accent}]▸[/] [bold]{Markup.Escape(title)}[/]");
        foreach (var column in columns)
            _ = table.AddColumn(new TableColumn($"[bold #{accent}]{Markup.Escape(column)}[/]"));
        foreach (var row in rows)
            table.AddRow(row.Select(Markup.Escape).ToArray());
        _console.Write(table);
    }

    /// <summary>Una columna se alinea a la derecha si todas sus celdas con contenido son números (con unidad opcional).</summary>
    private static bool IsNumericColumn(IReadOnlyList<string[]> rows, int column)
    {
        var cells = rows.Select(r => r[column]).Where(c => c.Length > 0 && c != "-").ToList();
        return cells.Count > 0 && cells.All(c => NumericCell().IsMatch(c));
    }

    /// <summary>Reduce las columnas más anchas hasta que la tabla quepa en la ventana.</summary>
    private static void FitToWindow(int[] widths, int maxWidth)
    {
        var excess = widths.Sum() + (ColumnGutter * (widths.Length - 1)) - maxWidth;
        while (excess > 0)
        {
            var widest = Array.IndexOf(widths, widths.Max());
            if (widths[widest] <= MinColumnWidth) return;
            var cut = Math.Min(excess, widths[widest] - MinColumnWidth);
            widths[widest] -= cut;
            excess -= cut;
        }
    }

    /// <summary>Ajusta una celda al ancho de su columna: la recorta con "…" o la rellena según la alineación.</summary>
    private static string Fit(string cell, int width, bool right)
    {
        var visible = PromptRenderer.VisibleWidth(cell);
        if (visible > width && !cell.Contains('\e'))
        {
            var builder = new StringBuilder();
            var used = 0;
            foreach (var rune in cell.EnumerateRunes())
            {
                var w = PromptRenderer.VisibleWidth(rune.ToString());
                if (used + w > width - 1) break;
                builder.Append(rune.ToString());
                used += w;
            }

            cell = builder + "…";
            visible = used + 1;
        }

        var padding = new string(' ', Math.Max(0, width - visible));
        return right ? padding + cell : cell + padding;
    }
}
