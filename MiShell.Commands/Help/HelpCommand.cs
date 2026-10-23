using MiShell.Core.Contracts;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>
/// <c>help</c>: lista los comandos por grupos, explica uno en detalle (<c>help ls</c>) o resume la
/// sintaxis (<c>help syntax</c>). Los textos largos viven en <c>help.json</c>; el nombre, la
/// descripción corta y el uso salen de cada comando registrado.
/// </summary>
public sealed class HelpCommand(IOutputRenderer renderer, IServiceProvider services, HelpCatalog? catalog = null) : IBuiltinCommand
{
    private readonly HelpCatalog _catalog = catalog ?? HelpCatalog.LoadEmbedded();

    /// <inheritdoc />
    public string Name => "help";

    /// <inheritdoc />
    public string Description => "Muestra comandos, ayuda específica y sintaxis.";

    /// <inheritdoc />
    public string Usage => "help [comando | syntax]";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        // Resolución diferida: pedir IEnumerable<IBuiltinCommand> en el constructor
        // crearía una dependencia circular (help es él mismo un comando).
        var builtins = (IEnumerable<IBuiltinCommand>?)services.GetService(typeof(IEnumerable<IBuiltinCommand>)) ?? [];
        switch (arguments.Length)
        {
            case > 1:
                renderer.WriteError($"Uso: {Usage}");
                return Task.FromResult(CommandResult.Failure);
            case 1 when arguments[0].Equals("syntax", StringComparison.OrdinalIgnoreCase):
                foreach (var line in _catalog.Syntax) renderer.WriteLine(line);
                return Task.FromResult(CommandResult.Ok);
            case 1:
                return Task.FromResult(ShowCommand(builtins, arguments[0]));
            default:
                ShowAll(builtins);
                return Task.FromResult(CommandResult.Ok);
        }
    }

    /// <summary>Explica un comando: descripción, uso, detalle y ejemplo.</summary>
    private CommandResult ShowCommand(IEnumerable<IBuiltinCommand> builtins, string name)
    {
        var command = builtins.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (command is null)
        {
            renderer.WriteError($"No hay ayuda interna para '{name}'. Consulta help o la ayuda del programa externo.");
            return CommandResult.Failure;
        }

        var rows = new List<(string, string)> { ("Comando", command.Name), ("Descripción", command.Description), ("Uso", command.Usage) };
        if (_catalog.Commands.TryGetValue(command.Name, out var entry))
        {
            rows.Add(("Detalle", entry.Detail));
            rows.Add(("Ejemplo", entry.Example));
        }

        renderer.RenderList(string.Empty, rows);
        return CommandResult.Ok;
    }

    /// <summary>Lista los comandos agrupados; los que no estén en ningún grupo van en "Otros".</summary>
    private void ShowAll(IEnumerable<IBuiltinCommand> builtins)
    {
        var byName = builtins.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var width = byName.Keys.Max(n => n.Length);
        foreach (var group in _catalog.Groups)
        {
            var members = group.Commands.Where(byName.ContainsKey).ToArray();
            if (members.Length > 0) ShowGroup(group.Name, members.Select(m => byName[m]), width);
            listed.UnionWith(members);
        }

        var others = byName.Values.Where(b => !listed.Contains(b.Name)).OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (others.Length > 0) ShowGroup("Otros", others, width);
        renderer.WriteInfo("Usa help <comando> para ver el detalle o help syntax para los operadores. Los programas externos se buscan en el PATH.");
    }

    private void ShowGroup(string title, IEnumerable<IBuiltinCommand> commands, int width)
    {
        renderer.WriteLine(title, renderer.Theme.Accent);
        foreach (var command in commands)
        {
            renderer.Write($"  {command.Name.PadRight(width)}", renderer.Theme.Command);
            renderer.WriteLine($"  {command.Description}", renderer.Theme.Muted);
        }
    }
}
