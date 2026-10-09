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
        ["contrast"] = new("contrast",
            ConsoleColor.Cyan, ConsoleColor.Cyan, ConsoleColor.White,
            ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Yellow,
            ConsoleColor.Cyan, ConsoleColor.Gray, "8be9fd"),
        ["paper"] = new("paper",
            ConsoleColor.DarkBlue, ConsoleColor.DarkBlue, ConsoleColor.Black,
            ConsoleColor.DarkGreen, ConsoleColor.DarkRed, ConsoleColor.DarkBlue,
            ConsoleColor.DarkBlue, ConsoleColor.DarkGray, "1e40af"),
        ["plain"] = new("plain",
            ConsoleColor.Gray, ConsoleColor.Gray, ConsoleColor.Gray,
            ConsoleColor.Gray, ConsoleColor.Gray, ConsoleColor.Gray,
            ConsoleColor.Gray, ConsoleColor.Gray, "cccccc"),
        ["dracula"] = new("dracula",
            ConsoleColor.Magenta, ConsoleColor.Cyan, ConsoleColor.Cyan,
            ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Yellow,
            ConsoleColor.Magenta, ConsoleColor.Gray, "bd93f9"),
        ["synthwave"] = new("synthwave",
            ConsoleColor.DarkMagenta, ConsoleColor.Magenta, ConsoleColor.Yellow,
            ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Cyan,
            ConsoleColor.Yellow, ConsoleColor.Gray, "ffcc00"),
        ["crt"] = new("crt",
            ConsoleColor.Green, ConsoleColor.Green, ConsoleColor.Green,
            ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Green,
            ConsoleColor.White, ConsoleColor.Gray, "33ff33"),
        ["minimal"] = new("minimal",
            ConsoleColor.White, ConsoleColor.Gray, ConsoleColor.Gray,
            ConsoleColor.White, ConsoleColor.Red, ConsoleColor.Gray,
            ConsoleColor.White, ConsoleColor.Gray, "cccccc"),
    };

    public ShellTheme Current { get; private set; } = Themes["contrast"];

    public bool TrySelect(string name, out ShellTheme theme)
    {
        if (!Themes.TryGetValue(name, out var selected))
        {
            theme = Current;
            return false;
        }

        theme = selected;
        Current = selected;
        return true;
    }

    public void SelectOrFallback(string? name)
    {
        if (string.IsNullOrEmpty(name) || !TrySelect(name, out _))
        {
            TrySelect("contrast", out _);
        }
    }
}
