using MiShell.Core.Contracts;
using MiShell.Core.Execution;

namespace MiShell.Commands.BuiltinCommands;

/// <summary><c>history</c>: muestra el historial numerado, las últimas N entradas o las que contengan un texto.</summary>
public sealed class HistoryCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "history";

    public string Description => "Muestra el historial numerado; admite un número (últimas N) o un texto a buscar.";

    public string Usage => "history [N | texto]";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var history = context.History;
        var indexes = Enumerable.Range(0, history.Count);
        switch (arguments.Length)
        {
            case 1 when int.TryParse(arguments[0], out var last) && last > 0:
                indexes = indexes.Skip(Math.Max(0, history.Count - last));
                break;
            case > 0:
            {
                var text = string.Join(' ', arguments);
                indexes = indexes.Where(i => history[i].Contains(text, StringComparison.OrdinalIgnoreCase));
                break;
            }
        }

        var shown = 0;
        foreach (var i in indexes)
        {
            {
                renderer.Write($"{i + 1,5}  ", renderer.Theme.Muted);
                renderer.WriteLine(history[i]);
            }
            shown++;
        }

        renderer.WriteInfo($"{shown} de {history.Count} entradas.");
        return Task.FromResult(CommandResult.Ok);
    }
}
