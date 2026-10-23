using MiShell.Core;
using MiShell.UI.Prompt;
using Spectre.Console;
using System.Text;
using Console = System.Console;

namespace MiShell.UI.Rendering;

// Banner de bienvenida: título FIGlet y un divisor con una ola de color animada.
public sealed partial class SpectreRenderer
{
    /// <summary>Intensidades relativas de los tonos del degradado, de oscuro a claro.</summary>
    private static readonly double[] ShadeFactors = [0.45, 0.75, 1.0, 1.35];

    /// <summary>Frames totales y pausa entre frames de la ola cromática (~1.6 s).</summary>
    private const int WaveFrames = 36;

    private const int WaveFrameDelayMs = 45;

    /// <summary>Ciclos de la onda senoidal visibles a lo ancho del divisor.</summary>
    private const double WaveCycles = 2.0;

    /// <inheritdoc />
    public void RenderBanner()
    {
        if (IsPlain) return;
        var theme = themeManager.Current;
        var accent = HexColor(theme.AccentHex);

        if (Console.IsOutputRedirected) _console.Write(new FigletText("MiShell").Centered().Color(accent));
        else RevealGradientTitle(theme.AccentHex);
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

        // Una sola línea: ayuda, versión del runtime (en verde cálido), hora y tema. Usuario, equipo y ruta ya están en el prompt.
        var sep = Format(" · ", theme.Muted);
        var runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        var warmGreen = AnsiColors.Enabled && theme.Name != "plain" ? "\e[38;2;163;190;140m" + runtime + "\e[0m" : runtime;
        _writer.WriteLine(
            "Escribe " + Format("help", theme.Command) + " para ver todos los comandos" + sep +
            warmGreen + sep +
            Format(ClockFormat.Hour(DateTime.Now), theme.Time) + sep +
            Format($"tema {theme.Name}", theme.Tag));
        _console.WriteLine();
    }

    /// <summary>Color final del degradado del título (lavanda suave) al que llega el acento del tema.</summary>
    private const string TitleGradientEnd = "a78bfa";

    /// <summary>Texto bajo el título.</summary>
    private const string Tagline = "Shell para Windows · C# · .NET";

    /// <summary>
    /// Dibuja el título FIGlet con un degradado horizontal (del acento del tema a lavanda) y lo revela
    /// línea por línea; debajo centra una frase breve. La animación dura unos 0,4 s.
    /// </summary>
    private void RevealGradientTitle(string accentHex)
    {
        var width = Math.Max(20, SafeWindowWidth() - 1);
        using var buffer = new StringWriter();
        var plain = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(buffer),
            Interactive = InteractionSupport.No,
        });
        plain.Profile.Width = width;
        plain.Write(new FigletText("MiShell").Centered());
        var lines = buffer.ToString().ReplaceLineEndings(Environment.NewLine).Split(Environment.NewLine).Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        var from = ParseHex(accentHex);
        var to = ParseHex(TitleGradientEnd);
        var first = lines.Where(l => l.Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        var last = lines.Select(l => l.Length).DefaultIfEmpty(1).Max();
        var span = Math.Max(1, last - first);
        foreach (var line in lines)
        {
            var painted = new StringBuilder();
            for (var x = 0; x < line.Length; x++)
            {
                var ch = line[x];
                if (ch == ' ') { painted.Append(ch); continue; }
                var t = Math.Clamp((x - first) / (double)span, 0, 1);
                var r = (int)(from.R + (to.R - from.R) * t);
                var g = (int)(from.G + (to.G - from.G) * t);
                var b = (int)(from.B + (to.B - from.B) * t);
                painted.Append(Invariant, $"\e[38;2;{r};{g};{b}m{ch}");
            }

            _writer.WriteLine(painted + "\e[0m");
            if (AnsiColors.Enabled) Thread.Sleep(45);
        }

        var pad = Math.Max(0, (width - Tagline.Length) / 2);
        _writer.WriteLine(new string(' ', pad) + Format(Tagline, themeManager.Current.Muted));
    }

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

    /// <summary>Multiplica un color hex por un factor, saturando en 0–255.</summary>
    private static string ShadeHex(string hex, double factor)
    {
        static int Channel(string hex, int offset) => Convert.ToInt32(hex[offset..(offset + 2)], 16);

        byte Scale(int channel) => (byte)Math.Clamp((int)Math.Round(channel * factor), 0, 255);

        return $"{Scale(Channel(hex, 0)):x2}{Scale(Channel(hex, 2)):x2}{Scale(Channel(hex, 4)):x2}";
    }
}
