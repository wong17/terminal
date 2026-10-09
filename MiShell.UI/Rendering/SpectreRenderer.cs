using MiShell.Core.Contracts;
using MiShell.UI.Prompt;
using MiShell.UI.Themes;
using Spectre.Console;
using System.Globalization;
using System.Text;
using Console = System.Console;

namespace MiShell.UI.Rendering;

/// <summary>Implementación de <see cref="IOutputRenderer"/> basada en Spectre.Console.</summary>
public sealed class SpectreRenderer(ThemeManager themeManager) : IOutputRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Espera antes de mostrar el indicador: comandos rápidos nunca lo ven.</summary>
    private const int SpinnerDelayMs = 400;

    private IAnsiConsole _console = AnsiConsole.Console;
    private TextWriter _writer = Console.Out;
    private int _redirectDepth;

    public IThemePalette Theme => themeManager.Current;

    public IDisposable RedirectOutput(TextWriter writer)
    {
        var previousConsole = _console;
        var previousWriter = _writer;
        _console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
        });
        _writer = writer;
        _redirectDepth++;
        return new OutputScope(() =>
        {
            _console = previousConsole;
            _writer = previousWriter;
            _redirectDepth--;
        });
    }

    private sealed class OutputScope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    private bool IsPlain => Console.IsOutputRedirected || _redirectDepth > 0;

    public void Write(string text, ConsoleColor? color = null) => _writer.Write(IsPlain ? text : Format(text, color));

    public void WriteLine(string text = "", ConsoleColor? color = null) => _writer.WriteLine(IsPlain ? text : Format(text, color));

    public void WriteError(string message) => Console.Error.WriteLine(
        Console.IsErrorRedirected ? $"Error: {message}" : Format($"✗ {message}", themeManager.Current.ErrorColor));

    public void WriteSuccess(string message) => WriteLine($"✓ {message}", themeManager.Current.Success);

    public void WriteInfo(string message) => WriteLine(message, themeManager.Current.Info);

    public void ClearScreen()
    {
        // En scripts o pruebas con stdout redirigido no existe una consola
        // interactiva que limpiar; el comando sigue siendo válido y no debe
        // abortar la sesión por un handle no asociado a una terminal.
        if (Console.IsOutputRedirected)
        {
            return;
        }

        try
        {
            Console.Clear();
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
        {
            // Algunas terminales compatibles con ANSI no exponen Console.Clear.
        }
    }

    public void RenderBanner()
    {
        if (IsPlain) return;
        var theme = themeManager.Current;
        var accent = HexColor(theme.AccentHex);

        _console.Write(new FigletText("MiShell").Centered().Color(accent));
        if (Console.IsOutputRedirected)
        {
            // Sin consola interactiva no tiene sentido animar: divisor estático.
            _console.MarkupLine(DividerMarkup(theme.AccentHex));
        }
        else
        {
            AnimateDivider(theme.AccentHex);
            _console.WriteLine();
        }

        var info = Markup.Escape(
            $"{PromptRenderer.ShortenPath(Environment.CurrentDirectory)} · {Environment.UserName}@{Environment.MachineName}" +
            $" · {DateTime.Now:HH:mm} · tema {theme.Name}");
        _console.MarkupLine($"[dim]{info}[/]");
        _console.MarkupLine("[dim]Escribe [bold]help[/] para ver todos los comandos[/]");
        _console.WriteLine();
    }

    /// <summary>Intensidades relativas de los tonos del degradado, de oscuro a claro.</summary>
    private static readonly double[] ShadeFactors = [0.45, 0.75, 1.0, 1.35];

    /// <summary>Frames totales y pausa entre frames de la ola cromática (~1.6 s).</summary>
    private const int WaveFrames = 36;

    private const int WaveFrameDelayMs = 45;

    /// <summary>Ciclos de la onda senoidal visibles a lo ancho del divisor.</summary>
    private const double WaveCycles = 2.0;

    private static string[] ShadePalette(string accentHex) =>
        [.. ShadeFactors.Select(f => ShadeHex(accentHex, f))];

    /// <summary>Divisor degradado: bloques ▄ en tonos derivados del acento del tema.</summary>
    private static string DividerMarkup(string accentHex)
    {
        var width = SafeWindowWidth();
        var shades = ShadePalette(accentHex);

        // Una columna de margen evita el ajuste de línea cuando el ancho no es exacto.
        var segmentWidth = Math.Max(1, (width - 1) / shades.Length);
        var builder = new StringBuilder(width + shades.Length * 16);
        foreach (var shade in shades)
        {
            _ = builder.Append(Invariant, $"[#{shade}]").Append('▄', segmentWidth).Append("[/]");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Anima una ola cromática que viaja de izquierda a derecha sobre el divisor y
    /// deja como frame final el degradado estático. Requiere consola interactiva.
    /// </summary>
    private void AnimateDivider(string accentHex)
    {
        var cells = Math.Max(8, SafeWindowWidth() - 1);
        var stops = ShadePalette(accentHex).Select(ParseHex).ToArray();

        for (var frame = 0; frame < WaveFrames; frame++)
        {
            var phase = (double)frame / WaveFrames;
            var builder = new StringBuilder((cells * 14) + 16);
            for (var i = 0; i < cells; i++)
            {
                var intensity = 0.5 + (0.5 * Math.Sin(2 * Math.PI * ((WaveCycles * i / cells) - phase)));
                _ = builder.Append(Invariant, $"[#{SampleGradient(stops, intensity)}]▄[/]");
            }

            Console.Write("\r");
            _console.Markup(builder.ToString());
            Console.Write("\x1b[K");
            Thread.Sleep(WaveFrameDelayMs);
        }

        // Frame final idéntico al divisor estático: transición perfecta al reposo.
        Console.Write("\r");
        _console.Markup(DividerMarkup(accentHex));
        Console.Write("\x1b[K");
    }

    /// <summary>Muestrea el degradado en un punto dado entre todos los tonos.</summary>
    private static string SampleGradient((byte R, byte G, byte B)[] stops, double intensity)
    {
        var scaled = Math.Clamp(intensity, 0, 1) * (stops.Length - 1);
        var index = Math.Min((int)scaled, stops.Length - 2);
        return LerpHex(stops[index], stops[index + 1], scaled - index);
    }

    /// <summary>Interpola linealmente dos colores hex y devuelve el resultado como hex.</summary>
    private static string LerpHex((byte R, byte G, byte B) from, (byte R, byte G, byte B) to, double amount)
    {
        static int Mix(int fromChannel, int toChannel, double t) => (int)Math.Round(fromChannel + ((toChannel - fromChannel) * t));

        return $"{Mix(from.R, to.R, amount):x2}{Mix(from.G, to.G, amount):x2}{Mix(from.B, to.B, amount):x2}";
    }

    /// <summary>Convierte "rrggbb" a sus tres componentes.</summary>
    private static (byte R, byte G, byte B) ParseHex(string hex) =>
        (Convert.ToByte(hex[0..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16));

    /// <summary>Multiplica un color hex por un factor, saturando en 0–255.</summary>
    private static string ShadeHex(string hex, double factor)
    {
        static int Channel(string hex, int offset) => Convert.ToInt32(hex[offset..(offset + 2)], 16);

        byte Scale(int channel) => (byte)Math.Clamp((int)Math.Round(channel * factor), 0, 255);

        return $"{Scale(Channel(hex, 0)):x2}{Scale(Channel(hex, 2)):x2}{Scale(Channel(hex, 4)):x2}";
    }

    private static int SafeWindowWidth()
    {
        try
        {
            return Console.WindowWidth > 0 ? Console.WindowWidth : 80;
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            return 80;
        }
    }

    /// <inheritdoc />
    public async Task<T> RunWithSpinnerAsync<T>(string label, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        // Sin consola interactiva no hay dónde dibujar el indicador.
        if (Console.IsOutputRedirected)
        {
            return await work(cancellationToken).ConfigureAwait(false);
        }

        var task = work(cancellationToken);
        var completed = await Task.WhenAny(task, Task.Delay(SpinnerDelayMs, cancellationToken)).ConfigureAwait(false);
        if (completed != task && !cancellationToken.IsCancellationRequested)
        {
            var accent = HexColor(themeManager.Current.AccentHex);
            await _console.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(accent))
                .StartAsync(label, _ => task)
                .ConfigureAwait(false);
        }

        return await task.ConfigureAwait(false);
    }

    public void RenderProcessTable(IEnumerable<ProcessInfo> processes)
    {
        var theme = themeManager.Current;
        var processInfos = processes.ToList();
        var maxRam = Math.Max(1, processInfos.Max(p => p.WorkingSetMb));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(HexColor(theme.AccentHex))
            .Title("[bold]Procesos[/]")
            .AddColumn(new TableColumn("PID").RightAligned())
            .AddColumn("Nombre")
            .AddColumn(new TableColumn("CPU (s)").RightAligned())
            .AddColumn(new TableColumn("RAM (MB)").RightAligned())
            .AddColumn("Uso de memoria");

        foreach (var p in processInfos)
        {
            var ratio = p.WorkingSetMb / maxRam;
            var barColor = ratio > 0.66 ? "red" : ratio > 0.33 ? "yellow" : "green";
            table.AddRow(
                p.Pid.ToString(Invariant),
                Markup.Escape(p.Name),
                p.CpuSeconds.ToString("F1", Invariant),
                p.WorkingSetMb.ToString("F0", Invariant),
                $"[{barColor}]{BuildMiniBar(ratio, 12)}[/]");
        }

        _console.Write(table);
    }

    public void RenderBarChart(string title, IEnumerable<(string Label, double Value, string ColorHint)> items, string valueSuffix)
    {
        var chart = new BarChart()
            .Width(60)
            .Label($"[bold]{Markup.Escape(title)}[/]")
            .CenterLabel();

        foreach (var (label, value, colorHint) in items)
        {
            chart.AddItem(Markup.Escape(Truncate(label, 28)), value, HexColor(colorHint));
        }

        _ = valueSuffix;
        _console.Write(chart);
    }

    public void RenderSystemInfo(SystemUsage usage)
    {
        var theme = themeManager.Current;
        var accent = theme.AccentHex;

        var grid = new Grid().Expand();
        _ = grid.AddColumn();
        _ = grid.AddColumn();

        _ = grid.AddRow(
            new Markup($"[bold #{accent}]MiShell[/] [dim]en[/] [bold]{Markup.Escape(Environment.MachineName)}[/]"),
            new Markup($"[dim]{DateTime.Now:HH:mm}[/]"));
        _ = grid.AddRow(
            new Markup($"[dim]OS[/] {Markup.Escape(Environment.OSVersion.VersionString)}"),
            new Text(string.Empty));
        _ = grid.AddRow(new Markup($"[dim]Usuario[/] {Markup.Escape(Environment.UserName)}"), new Text(string.Empty));
        _ = grid.AddRow(new Text(string.Empty), new Text(string.Empty));
        _ = grid.AddRow(new Markup("CPU"), new Markup(BuildRatioBar(usage.CpuPercent / 100)));
        _ = grid.AddRow(new Markup("RAM"), new Markup(BuildRatioBar(usage.RamUsedMb / Math.Max(1, usage.RamTotalMb))));
        _ = grid.AddRow(new Markup("Disco"), new Markup(BuildRatioBar(usage.DiskUsedGb / Math.Max(1, usage.DiskTotalGb))));
        _ = grid.AddRow(
            new Markup($"[dim]{usage.RamUsedMb.ToString("F0", Invariant)} / {usage.RamTotalMb.ToString("F0", Invariant)} MB · {usage.DiskUsedGb.ToString("F1", Invariant)} / {usage.DiskTotalGb.ToString("F1", Invariant)} GB[/]"),
            new Text(string.Empty));

        var panel = new Panel(grid)
            .Border(BoxBorder.Rounded)
            .BorderColor(HexColor(accent))
            .Header($"[bold #{accent}] Información del sistema [/]");
        _console.Write(panel);
    }

    public void RenderTable(string title, string[] columns, IEnumerable<string[]> rows)
    {
        if (IsPlain)
        {
            WriteLine(string.Join('\t', columns));
            foreach (var row in rows) WriteLine(string.Join('\t', row));
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(HexColor(themeManager.Current.AccentHex))
            .Title($"[bold]{Markup.Escape(title)}[/]");

        foreach (var column in columns)
        {
            _ = table.AddColumn(new TableColumn(column));
        }

        foreach (var row in rows)
        {
            table.AddRow(row.Select(Markup.Escape).ToArray());
        }

        _console.Write(table);
    }

    private static string BuildRatioBar(double ratio) =>
        $"[{RatioColor(ratio)}]{BuildMiniBar(ratio, 20)}[/] {((int)Math.Clamp(ratio * 100, 0, 100)).ToString(Invariant)}%";

    private static string RatioColor(double ratio) => ratio > 0.8 ? "red" : ratio > 0.5 ? "yellow" : "green";

    private static string BuildMiniBar(double ratio, int width)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        var filled = (int)Math.Round(ratio * width);
        return new string('█', filled) + new string('░', width - filled);
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";

    private static string Format(string text, ConsoleColor? color) =>
        color is null ? text : Ansi(color.Value) + text + "\e[0m";

    private static Color HexColor(string hexOrName)
    {
        if (hexOrName.Length == 6)
        {
            var (r, g, b) = ParseHex(hexOrName);
            return new Color(r, g, b);
        }

        return hexOrName.ToLowerInvariant() switch
        {
            "red" => Color.Red,
            "green" => Color.Green,
            "yellow" => Color.Yellow,
            "blue" => Color.Blue,
            "magenta" => Color.Magenta,
            "cyan" => Color.Cyan,
            "white" => Color.White,
            "gray" or "grey" => Color.Grey,
            _ => Color.White,
        };
    }

    private static string Ansi(ConsoleColor color) => color switch
    {
        ConsoleColor.Red or ConsoleColor.DarkRed => "\e[31m",
        ConsoleColor.Green or ConsoleColor.DarkGreen => "\e[32m",
        ConsoleColor.Yellow => "\e[33m",
        ConsoleColor.Blue => "\e[34m",
        ConsoleColor.Magenta or ConsoleColor.DarkMagenta => "\e[35m",
        ConsoleColor.Cyan or ConsoleColor.DarkCyan => "\e[36m",
        ConsoleColor.White => "\e[37m",
        ConsoleColor.Gray or ConsoleColor.DarkGray => "\e[90m",
        _ => string.Empty,
    };
}
