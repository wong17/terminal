using MiShell.Core.Contracts;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Elimina variables de entorno de la sesión (Remove-Item Env:NOMBRE).</summary>
public sealed class UnsetCommand(IOutputRenderer renderer, IShellEnvironment environment) : IBuiltinCommand
{
    public string Name => "unset";

    public string Description => "Elimina una o varias variables de entorno de esta sesión.";

    public string Usage => "unset NOMBRE [NOMBRE...]";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }

        var failed = false;
        foreach (var name in arguments)
        {
            if (environment.GetVariable(name) is null)
            {
                renderer.WriteError($"La variable '{name}' no existe.");
                failed = true;
                continue;
            }

            environment.RemoveVariable(name);
            renderer.WriteSuccess($"{name} eliminada.");
        }

        return Task.FromResult(failed ? CommandResult.Failure : CommandResult.Ok);
    }
}
