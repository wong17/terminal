using MiShell.Core.Contracts;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Alias de <c>clear</c> con el nombre de la consola clásica de Windows.</summary>
public sealed class ClsCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "cls";

    public string Description => "Limpia la pantalla.";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        renderer.ClearScreen();
        return Task.FromResult(CommandResult.Ok);
    }
}
