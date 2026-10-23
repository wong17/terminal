using MiShell.Configuration;
using MiShell.Core.Contracts;
using MiShell.UI.Themes;

namespace MiShell.Commands.BuiltinCommands;

/// <summary><c>theme</c>: cambia (y guarda) el tema de colores, o lista los temas disponibles.</summary>
public sealed class ThemeCommand(IOutputRenderer renderer, ThemeManager themeManager, ShellSettings settings) : IBuiltinCommand
{
    public string Name => "theme";

    public string Description => "Cambia el tema de colores del shell.";

    public string Usage => "theme [nombre]  ·  theme list";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length > 1)
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }
        if (arguments.Length == 0 || arguments[0] == "list")
        {
            renderer.RenderTable(
                "Temas disponibles",
                ["Tema", "Uso recomendado", "Estado"],
                ThemeManager.Themes.Keys.Select(name => (string[])
                    [name, name switch { "contrast" => "Fondo oscuro; texto claro", "paper" => "Fondo claro; texto oscuro",
                        "plain" => "Sin color ni gráficos", _ => "Fondo oscuro" },
                        name == themeManager.Current.Name ? "activo" : string.Empty]));
            return Task.FromResult(CommandResult.Ok);
        }

        if (!themeManager.TrySelect(arguments[0], out _))
        {
            renderer.WriteError($"Tema desconocido '{arguments[0]}'. Usa: {string.Join(", ", ThemeManager.Themes.Keys)}");
            return Task.FromResult(CommandResult.Failure);
        }

        settings.SetTheme(arguments[0]);
        settings.Save(settings.SettingsFilePath);
        renderer.WriteSuccess($"Tema cambiado a '{arguments[0]}'.");
        return Task.FromResult(CommandResult.Ok);
    }
}
