using MiShell.Core.Contracts;

namespace MiShell.UI.Themes;

/// <summary>Paleta concreta de un tema.</summary>
/// <param name="AccentHex">Color de acento en formato RRGGBB para Spectre.</param>
public sealed record ShellTheme(
    string Name,
    ConsoleColor Prompt,
    ConsoleColor PromptSymbol,
    ConsoleColor Path,
    ConsoleColor Success,
    ConsoleColor ErrorColor,
    ConsoleColor Info,
    ConsoleColor Accent,
    ConsoleColor Muted,
    string AccentHex) : IThemePalette;

/// <summary>Registro de temas disponibles y selección del activo.</summary>
public sealed class ThemeManager
{
    public static IReadOnlyDictionary<string, ShellTheme> Themes { get; } = new Dictionary<string, ShellTheme>(StringComparer.OrdinalIgnoreCase)
    {
        ["dracula"] = new("dracula",
            ConsoleColor.Magenta, ConsoleColor.Cyan, ConsoleColor.Cyan,
            ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Yellow,
            ConsoleColor.Magenta, ConsoleColor.DarkGray, "bd93f9"),
        ["synthwave"] = new("synthwave",
            ConsoleColor.DarkMagenta, ConsoleColor.Magenta, ConsoleColor.Yellow,
            ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Cyan,
            ConsoleColor.Yellow, ConsoleColor.DarkGray, "ffcc00"),
        ["crt"] = new("crt",
            ConsoleColor.Green, ConsoleColor.Green, ConsoleColor.Green,
            ConsoleColor.Green, ConsoleColor.DarkRed, ConsoleColor.Green,
            ConsoleColor.White, ConsoleColor.DarkGreen, "33ff33"),
        ["minimal"] = new("minimal",
            ConsoleColor.White, ConsoleColor.Gray, ConsoleColor.Gray,
            ConsoleColor.White, ConsoleColor.Red, ConsoleColor.Gray,
            ConsoleColor.White, ConsoleColor.DarkGray, "cccccc"),
    };

    public ShellTheme Current { get; private set; } = Themes["dracula"];

    public bool TrySelect(string name, out ShellTheme theme)
    {
        if (!Themes.TryGetValue(name, out theme!)) return false;

        Current = theme;
        return true;
    }

    public void SelectOrFallback(string? name)
    {
        if (string.IsNullOrEmpty(name) || !TrySelect(name, out _))
        {
            TrySelect("dracula", out _);
        }
    }
}
