using MiShell.Core.Contracts;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Alias compatible con la consigna y con la consola clásica de Windows.</summary>
public sealed class ClearCommand(IOutputRenderer renderer, string name) : IBuiltinCommand
{
    public string Name { get; } = name;

    public string Description => "Limpia la pantalla.";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        renderer.ClearScreen();
        return Task.FromResult(CommandResult.Ok);
    }
}
