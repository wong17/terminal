using MiShell.Core.Contracts;
using MiShell.Core.Execution;

namespace MiShell.Commands.BuiltinCommands;

public sealed class EchoCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "echo";

    public string Description => "Imprime texto en pantalla.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        renderer.WriteLine(string.Join(' ', arguments));
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class ExitCommand : IBuiltinCommand
{
    public string Name => "exit";

    public string Description => "Cierra el shell con un código opcional de salida.";
    public string Usage => "exit [0..255]";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length > 1 || arguments.Length == 1 &&
            (!int.TryParse(arguments[0], out var code) || code is < 0 or > 255))
            throw new ArgumentException("Uso: exit [0..255]");
        context.RequestExit(arguments.Length == 0 ? 0 : int.Parse(arguments[0], System.Globalization.CultureInfo.InvariantCulture));
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class HelpCommand(IOutputRenderer renderer, IServiceProvider services) : IBuiltinCommand
{
    public string Name => "help";

    public string Description => "Muestra los comandos disponibles.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        // Resolución diferida: pedir IEnumerable<IBuiltinCommand> en el constructor
        // crearía una dependencia circular (help es él mismo un comando).
        var builtins = (IEnumerable<IBuiltinCommand>?)services.GetService(typeof(IEnumerable<IBuiltinCommand>)) ?? [];
        renderer.RenderTable(
            "Comandos internos",
            ["Comando", "Descripción", "Uso"],
            builtins
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .Select(b => (string[])[b.Name, b.Description, b.Usage]));
        renderer.WriteInfo("Los demás comandos se buscan como programas externos en el PATH.");
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class VersionCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "version";

    public string Description => "Muestra la versión del shell y de .NET.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        renderer.WriteLine($"MiShell 1.0 · .NET {Environment.Version} · {Environment.OSVersion.VersionString}");
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class WhichCommand(IOutputRenderer renderer, IExecutableResolver resolver) : IBuiltinCommand
{
    public string Name => "which";

    public string Description => "Muestra la ruta de un ejecutable.";

    public string Usage => "which <comando>";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            renderer.WriteError("Uso: which <comando>");
            return Task.FromResult(CommandResult.Failure);
        }

        var resolved = resolver.Resolve(arguments[0]);
        if (resolved is null)
        {
            renderer.WriteError($"No se encontró '{arguments[0]}' en el PATH.");
            return Task.FromResult(CommandResult.Failure);
        }

        renderer.WriteLine(resolved, renderer.Theme.Success);
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class TimeCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "time";

    public string Description => "Muestra la fecha y hora actuales.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        renderer.WriteLine(DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy, HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture), renderer.Theme.Accent);
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class HistoryCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "history";

    public string Description => "Muestra el historial de comandos.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        for (var i = 0; i < context.History.Count; i++)
        {
            renderer.WriteLine($"{i + 1,4}  {context.History[i]}", renderer.Theme.Muted);
        }

        return Task.FromResult(CommandResult.Ok);
    }
}
