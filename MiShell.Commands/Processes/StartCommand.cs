using MiShell.Core.Contracts;
using MiShell.Core.Execution;
using MiShell.Core.Parsing;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>
/// <c>start</c>: lanza un programa externo en segundo plano y devuelve el prompt. Equivale a terminar la línea con <c>&amp;</c>;
/// el programa queda en la tabla de trabajos para manejarlo con <c>jobs</c>, <c>fg</c>, <c>suspend</c>, <c>resume</c> y <c>kill</c>.
/// </summary>
public sealed class StartCommand(IOutputRenderer renderer, IServiceProvider services) : IBuiltinCommand
{
    public string Name => "start";

    public string Description => "Lanza un programa en segundo plano como trabajo (igual que &).";

    public string Usage => "start <programa> [argumentos...]";

    /// <inheritdoc />
    public async Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            renderer.WriteError($"Uso: {Usage}");
            return CommandResult.Failure;
        }

        // Resolución diferida: el ejecutor contiene a todos los comandos internos, incluido este.
        var executor = (IShellExecutor?)services.GetService(typeof(IShellExecutor));
        if (executor is null)
        {
            renderer.WriteError("No se pudo iniciar el programa: ejecutor no disponible.");
            return CommandResult.Failure;
        }

        var spec = new CommandSpec(arguments[0], arguments[1..], []);
        var line = new CommandLine([new ChainedPipeline([spec], ChainOperator.Always)], RunInBackground: true);
        var code = await executor.ExecuteAsync(line, cancellationToken).ConfigureAwait(false);
        if (code == 0) renderer.WriteLine("Usa jobs para verlo, fg para esperarlo, suspend o kill para controlarlo.", renderer.Theme.Muted);
        return new CommandResult(code);
    }
}
