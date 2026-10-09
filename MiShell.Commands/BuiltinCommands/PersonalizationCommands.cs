using MiShell.Configuration;
using MiShell.Core.Contracts;
using MiShell.UI.Themes;

namespace MiShell.Commands.BuiltinCommands;

public sealed class ThemeCommand(IOutputRenderer renderer, ThemeManager themeManager, ShellSettings settings) : IBuiltinCommand
{
    public string Name => "theme";

    public string Description => "Cambia el tema de colores del shell.";

    public string Usage => "theme [nombre]  ·  theme list";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || arguments[0] == "list")
        {
            renderer.RenderTable(
                "Temas disponibles",
                ["Tema", "Estado"],
                ThemeManager.Themes.Keys.Select(name => (string[])
                    [name, name == themeManager.Current.Name ? "● activo" : string.Empty]));
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

public sealed class SetCommand(IOutputRenderer renderer, IShellEnvironment environment) : IBuiltinCommand
{
    public string Name => "set";

    public string Description => "Define una variable de entorno: set NOMBRE=valor.";

    public string Usage => "set NOMBRE=valor  ·  set (lista todo)";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            renderer.RenderTable(
                "Variables de entorno",
                ["Nombre", "Valor"],
                environment.EnumerateVariables().Select(v => (string[])[v.Name, v.Value]));
            return Task.FromResult(CommandResult.Ok);
        }

        var parts = arguments[0].Split('=', 2);
        if (parts.Length != 2)
        {
            renderer.WriteError("Formato esperado: set NOMBRE=valor");
            return Task.FromResult(CommandResult.Failure);
        }

        environment.SetVariable(parts[0], parts[1]);
        renderer.WriteSuccess($"{parts[0]} = {parts[1]}");
        return Task.FromResult(CommandResult.Ok);
    }
}
