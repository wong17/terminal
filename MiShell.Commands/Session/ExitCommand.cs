using MiShell.Core.Contracts;

namespace MiShell.Commands.Session;

/// <summary><c>exit</c>: pide terminar el ciclo principal con un código de salida entre 0 y 255.</summary>
public sealed class ExitCommand : IBuiltinCommand
{
    public string Name => "exit";

    public string Description => "Cierra el shell con un código opcional de salida.";
    public string Usage => "exit [0..255]";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length > 1 || arguments.Length == 1 &&
            (!int.TryParse(arguments[0], out var code) || code is < 0 or > 255))
            throw new ArgumentException("Uso: exit [0..255]");
        context.RequestExit(arguments.Length == 0 ? 0 : int.Parse(arguments[0], System.Globalization.CultureInfo.InvariantCulture));
        return Task.FromResult(CommandResult.Ok);
    }
}
