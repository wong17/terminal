using MiShell.Configuration;
using MiShell.Core.Contracts;
using MiShell.UI.Rendering;

namespace MiShell.Commands.Session;

/// <summary>
/// <c>format</c>: elige cómo se dibujan las tablas y listas. <c>classic</c> imita a dir y Get-ChildItem
/// (columnas alineadas, sin bordes) y <c>boxed</c> usa tablas con bordes. La elección se guarda en settings.json.
/// </summary>
public sealed class FormatCommand(IOutputRenderer renderer, ShellSettings settings) : IBuiltinCommand
{
    /// <inheritdoc />
    public string Name => "format";

    /// <inheritdoc />
    public string Description => "Cambia el formato de las tablas: classic (alineado) o boxed (con bordes).";

    /// <inheritdoc />
    public string Usage => "format [classic | boxed]";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length > 1 || arguments.Length == 1 && !OutputStyles.All.Contains(arguments[0], StringComparer.OrdinalIgnoreCase))
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }

        if (arguments.Length == 0)
        {
            renderer.RenderList("Formato de salida", [("Actual", renderer.OutputStyle), ("Disponibles", string.Join(", ", OutputStyles.All))]);
            return Task.FromResult(CommandResult.Ok);
        }

        var style = OutputStyles.All.First(s => s.Equals(arguments[0], StringComparison.OrdinalIgnoreCase));
        renderer.OutputStyle = style;
        settings.OutputStyle = style;
        settings.Save(settings.SettingsFilePath);
        renderer.WriteSuccess($"Formato cambiado a '{style}'.");
        return Task.FromResult(CommandResult.Ok);
    }
}
