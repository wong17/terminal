using MiShell.Core.Contracts;
using System.Diagnostics;
using System.Globalization;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Activa o desactiva el seguimiento de la creación y espera de procesos.</summary>
public sealed class TraceCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "trace";

    public string Description => "Muestra cómo se crean y esperan los procesos externos.";

    public string Usage => "trace on  ·  trace off  ·  trace";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        switch (arguments.Length)
        {
            case > 1:
            case 1 when arguments[0] is not ("on" or "off"):
                renderer.WriteError($"Uso: {Usage}");
                return Task.FromResult(CommandResult.Failure);
            case 1:
                context.TraceProcesses = arguments[0] == "on";
                break;
        }

        renderer.WriteInfo($"trace {(context.TraceProcesses ? "activado" : "desactivado")}");
        return Task.FromResult(CommandResult.Ok);
    }
}
