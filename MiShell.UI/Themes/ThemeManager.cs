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
    ConsoleColor Time,
    ConsoleColor Host,
    ConsoleColor Tag,
    ConsoleColor Command,
    ConsoleColor Directory,
    string AccentHex) : IThemePalette;

/// <summary>Registro de temas disponibles y selección del activo.</summary>
public sealed class ThemeManager
{
    /// <summary>Temas disponibles por nombre.</summary>
    public static IReadOnlyDictionary<string, ShellTheme> Themes { get; } = new Dictionary<string, ShellTheme>(StringComparer.OrdinalIgnoreCase)
    {
        // Orden: Prompt, PromptSymbol, Path, Success, Error, Info, Accent, Muted, Time, Host, Tag, Command, Directory.
        ["contrast"] = new("contrast",
            ConsoleColor.Cyan, ConsoleColor.Cyan, ConsoleColor.DarkCyan,
            ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Yellow,
            ConsoleColor.Cyan, ConsoleColor.Gray,
            ConsoleColor.DarkCyan, ConsoleColor.Gray, ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Cyan, "8be9fd"),
        ["paper"] = new("paper",
            ConsoleColor.DarkBlue, ConsoleColor.DarkBlue, ConsoleColor.DarkMagenta,
            ConsoleColor.DarkGreen, ConsoleColor.DarkRed, ConsoleColor.DarkBlue,
            ConsoleColor.DarkBlue, ConsoleColor.DarkGray,
            ConsoleColor.DarkGray, ConsoleColor.DarkGray, ConsoleColor.DarkCyan, ConsoleColor.DarkGreen, ConsoleColor.DarkBlue, "1e40af"),
        ["plain"] = new("plain",
            ConsoleColor.Gray, ConsoleColor.Gray, ConsoleColor.Gray,
            ConsoleColor.Gray, ConsoleColor.Gray, ConsoleColor.Gray,
            ConsoleColor.Gray, ConsoleColor.Gray,
            ConsoleColor.Gray, ConsoleColor.Gray, ConsoleColor.Gray, ConsoleColor.Gray, ConsoleColor.Gray, "cccccc"),
        ["dracula"] = new("dracula",
            ConsoleColor.Magenta, ConsoleColor.Cyan, ConsoleColor.Green,
            ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Yellow,
            ConsoleColor.Magenta, ConsoleColor.Gray,
            ConsoleColor.Magenta, ConsoleColor.Gray, ConsoleColor.DarkGray, ConsoleColor.Cyan, ConsoleColor.Magenta, "bd93f9"),
        ["synthwave"] = new("synthwave",
            ConsoleColor.DarkMagenta, ConsoleColor.Magenta, ConsoleColor.Cyan,
            ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Cyan,
            ConsoleColor.Yellow, ConsoleColor.Gray,
            ConsoleColor.DarkMagenta, ConsoleColor.Gray, ConsoleColor.DarkYellow, ConsoleColor.Yellow, ConsoleColor.Yellow, "ffcc00"),
        ["crt"] = new("crt",
            ConsoleColor.Green, ConsoleColor.Green, ConsoleColor.Green,
            ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Green,
            ConsoleColor.White, ConsoleColor.Gray,
            ConsoleColor.DarkGreen, ConsoleColor.Green, ConsoleColor.Gray, ConsoleColor.White, ConsoleColor.White, "33ff33"),
        ["minimal"] = new("minimal",
            ConsoleColor.White, ConsoleColor.Gray, ConsoleColor.Gray,
            ConsoleColor.White, ConsoleColor.Red, ConsoleColor.Gray,
            ConsoleColor.White, ConsoleColor.Gray,
            ConsoleColor.Gray, ConsoleColor.Gray, ConsoleColor.Gray, ConsoleColor.White, ConsoleColor.White, "cccccc"),
    };

    /// <summary>Tema activo.</summary>
    public ShellTheme Current { get; private set; } = Themes["contrast"];

    /// <summary>Activa un tema por nombre; devuelve false si no existe.</summary>
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

    /// <summary>Activa el tema indicado o, si no existe, el predeterminado (contrast).</summary>
    public void SelectOrFallback(string? name)
    {
        if (string.IsNullOrEmpty(name) || !TrySelect(name, out _))
        {
            TrySelect("contrast", out _);
        }
    }
}
