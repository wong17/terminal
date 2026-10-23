using MiShell.Core;
using MiShell.UI.Rendering;
using MiShell.UI.Themes;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MiShell.UI.Prompt;

/// <summary>
/// Prompt de tres líneas inspirado en "emodipt-extend" de Oh My Posh: la información
/// (hora, usuario, ruta y desenlace del comando anterior) se dibuja en las líneas
/// superiores, que permanecen intactas mientras el usuario escribe en la última.
/// </summary>
public sealed partial class PromptRenderer(ThemeManager themeManager, IconProvider iconProvider, bool showTime)
{
    /// <summary>Duración a partir de la cual un comando exitoso muestra su tiempo.</summary>
    private const int SlowCommandThresholdMs = 500;

    private const string Reset = "\x1b[0m";

    private readonly PromptIcons _icons = iconProvider.Resolve();

    /// <summary>Construye el prompt completo; termina en el símbolo de escritura, sin salto de línea.</summary>
    /// <param name="lastExitCode">Código de salida del último comando ejecutado.</param>
    /// <param name="duration">Duración de esa ejecución, si aplica.</param>
    public string Render(int lastExitCode, TimeSpan? duration)
    {
        var theme = themeManager.Current;
        var succeeded = lastExitCode == 0;
        var symbolColor = succeeded ? theme.PromptSymbol : theme.ErrorColor;

        var line1 = ComposeInfoLine(theme, lastExitCode, duration);
        var line2 = $"{AnsiColors.For(theme.Path, theme.Name != "plain")}{_icons.Folder}{ShortenPath(Environment.CurrentDirectory)}{Reset}";
        var line3 = $"{AnsiColors.For(symbolColor, theme.Name != "plain")}{_icons.Chevron}{Reset} ";

        var prompt = $"{line1}\n{line2}\n{line3}";
        return !AnsiColors.Enabled || theme.Name == "plain"
            ? AnsiSequenceRegex().Replace(prompt, string.Empty)
            : prompt;
    }

    private string ComposeInfoLine(ShellTheme theme, int lastExitCode, TimeSpan? duration)
    {
        var left = new StringBuilder();
        if (showTime)
        {
            left.Append(AnsiColors.For(theme.Time, theme.Name != "plain"))
                .Append(ClockFormat.Hour(DateTime.Now))
                .Append(' ')
                .Append(Reset);
        }

        left.Append(AnsiColors.For(theme.Host, theme.Name != "plain"))
            .Append(Environment.UserName)
            .Append('@')
            .Append(Environment.MachineName)
            .Append(Reset);

        var right = ResultBadge(theme, lastExitCode, duration);
        return PadToEdge(left.ToString(), right) + right;
    }

    /// <summary>Insignia derecha: código de salida en rojo si falló, o duración si fue lento.</summary>
    private static string ResultBadge(ShellTheme theme, int lastExitCode, TimeSpan? duration) =>
        lastExitCode != 0
            ? $"{AnsiColors.For(theme.ErrorColor, theme.Name != "plain")}✗ código {lastExitCode.ToString(CultureInfo.InvariantCulture)}{Reset}"
            : duration is { TotalMilliseconds: >= SlowCommandThresholdMs }
                ? $"{AnsiColors.For(theme.Success, theme.Name != "plain")}✓ {duration.Value.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)}s{Reset}"
                : string.Empty;

    /// <summary>Rellena con espacios para llevar la insignia al borde derecho de la ventana.</summary>
    private static string PadToEdge(string left, string right)
    {
        if (right.Length == 0)
        {
            return left;
        }

        var width = SafeWindowWidth();
        if (width is int.MaxValue)
        {
            // Terminal sin ancho conocido (salida redirigida): separación simple.
            return $"{left}   ";
        }

        var gap = width - VisibleWidth(left) - VisibleWidth(right) - 2;
        return gap <= 0 ? $"{left}   " : $"{left}{new string(' ', gap)}";
    }

    private static int SafeWindowWidth()
    {
        try
        {
            return Console.WindowWidth > 10 ? Console.WindowWidth : int.MaxValue;
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            return int.MaxValue;
        }
    }

    /// <summary>Ancho visible real: ignora secuencias ANSI y cuenta caracteres anchos por dos.</summary>
    public static int VisibleWidth(string text)
    {
        var width = 0;
        foreach (var rune in AnsiSequenceRegex().Replace(text, string.Empty).EnumerateRunes())
        {
            width += IsWide(rune.Value) ? 2 : 1;
        }

        return width;
    }

    /// <summary>Abrevia la ruta personal como <c>~/…</c> y normaliza separadores.</summary>
    public static string ShortenPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && (path.Equals(home, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            ? "~" + path[home.Length..].Replace('\\', '/')
            : path.Replace('\\', '/');
    }

    /// <summary>Aproximación de East Asian Width para los rangos que importan aquí (emoji).</summary>
    private static bool IsWide(int codePoint) =>
        codePoint is >= 0x1100 and <= 0x115F
            or >= 0x2E80 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE30 and <= 0xFE4F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x1F300 and <= 0x1F64F
            or >= 0x1F900 and <= 0x1F9FF
            or >= 0x20000;

    [GeneratedRegex(@"\x1b\[[0-9;]*[A-Za-z]")]
    private static partial Regex AnsiSequenceRegex();
}
