using MiShell.Core.Contracts;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>
/// <c>help</c>: lista los comandos por grupos mostrando su nombre en Linux y en Windows, explica uno en detalle
/// (<c>help ls</c>), un tema (<c>help pipes</c>) o resume la sintaxis (<c>help syntax</c>). Los textos largos viven
/// en <c>help.json</c>; el nombre, la descripción corta y el uso salen de cada comando registrado.
/// </summary>
public sealed class HelpCommand(IOutputRenderer renderer, IServiceProvider services, HelpCatalog? catalog = null) : IBuiltinCommand
{
    private const int MaxCommandWidth = 46;

    private readonly HelpCatalog _catalog = catalog ?? HelpCatalog.LoadEmbedded();

    /// <inheritdoc />
    public string Name => "help";

    /// <inheritdoc />
    public string Description => "Muestra comandos, ayuda con ejemplos y temas.";

    /// <inheritdoc />
    public string Usage => "help [comando | tema | syntax]";

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
                renderer.WriteLine($"Temas con ejemplos: {TopicList()}", renderer.Theme.Muted);
                return Task.FromResult(CommandResult.Ok);
            case 1 when _catalog.Topics.TryGetValue(arguments[0], out var topic):
                ShowTopic(topic);
                return Task.FromResult(CommandResult.Ok);
            case 1:
                return Task.FromResult(ShowCommand(builtins, arguments[0]));
            default:
                ShowAll(builtins);
                return Task.FromResult(CommandResult.Ok);
        }
    }

    private string TopicList() => string.Join(", ", _catalog.Topics.Keys.Append("syntax"));

    /// <summary>Explica un comando: descripción, uso, detalle, equivalentes y ejemplos.</summary>
    private CommandResult ShowCommand(IEnumerable<IBuiltinCommand> builtins, string name)
    {
        var command = builtins.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (command is null)
        {
            renderer.WriteError($"No hay ayuda interna para '{name}'. Prueba help, help {TopicList().Split(',')[0]} o la ayuda del programa externo.");
            return CommandResult.Failure;
        }

        var rows = new List<(string, string)> { ("Comando", command.Name), ("Descripción", command.Description), ("Uso", command.Usage) };
        _catalog.Commands.TryGetValue(command.Name, out var entry);
        if (entry is not null)
        {
            rows.Add(("Detalle", entry.Detail));
            if (entry.Equivalent.Length > 0) rows.Add(("Equivale a", entry.Equivalent));
            if (entry.SeeAlso is { Count: > 0 }) rows.Add(("Ver también", string.Join(", ", entry.SeeAlso)));
        }

        renderer.RenderList(string.Empty, rows);
        if (entry is { Examples.Count: > 0 }) WriteExamples(entry.Examples);
        return CommandResult.Ok;
    }

    private void ShowTopic(HelpTopic topic)
    {
        renderer.WriteLine(topic.Title, renderer.Theme.Accent);
        foreach (var line in topic.Lines) renderer.WriteLine("  " + line);
        WriteExamples(topic.Examples);
    }

    /// <summary>Escribe los ejemplos como si se hubieran tecleado: la orden resaltada y, al lado, qué hace.</summary>
    private void WriteExamples(IReadOnlyList<HelpExample> examples)
    {
        renderer.WriteLine();
        renderer.WriteLine("Ejemplos", renderer.Theme.Accent);
        var width = Math.Min(MaxCommandWidth, examples.Max(e => e.Command.Length));
        foreach (var example in examples)
        {
            renderer.Write("  > ", renderer.Theme.PromptSymbol);
            renderer.Write(example.Command.PadRight(width), renderer.Theme.Command);
            if (example.Note.Length > 0) renderer.Write("  " + example.Note, renderer.Theme.Muted);
            renderer.WriteLine();
        }
    }

    /// <summary>Lista los comandos agrupados con su nombre en Linux y en Windows; los que no estén en ningún grupo van en "Otros".</summary>
    private void ShowAll(IEnumerable<IBuiltinCommand> builtins)
    {
        var byName = builtins.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var linuxWidth = Math.Max(5, _catalog.Groups.SelectMany(g => g.Rows).Select(r => r.Linux.Length).DefaultIfEmpty(0).Max());
        var windowsWidth = Math.Max(7, _catalog.Groups.SelectMany(g => g.Rows).Select(r => (r.Windows ?? string.Empty).Length).DefaultIfEmpty(0).Max());

        renderer.Write("  " + "Comando".PadRight(linuxWidth), renderer.Theme.Command);
        renderer.Write("  " + "Windows".PadRight(windowsWidth), renderer.Theme.Info);
        renderer.WriteLine("  Qué hace", renderer.Theme.Muted);
        foreach (var group in _catalog.Groups)
        {
            var rows = group.Rows.Where(r => r.Names.Any(byName.ContainsKey)).ToArray();
            if (rows.Length == 0) continue;
            renderer.WriteLine(group.Name, renderer.Theme.Accent);
            foreach (var row in rows)
            {
                var command = byName.GetValueOrDefault(row.Linux) ?? (row.Windows is null ? null : byName.GetValueOrDefault(row.Windows));
                WriteRow(row.Linux, row.Windows ?? string.Empty, command?.Description ?? string.Empty, linuxWidth, windowsWidth);
                listed.UnionWith(row.Names);
            }
        }

        var others = byName.Values.Where(b => !listed.Contains(b.Name)).OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (others.Length > 0)
        {
            renderer.WriteLine("Otros", renderer.Theme.Accent);
            foreach (var other in others) WriteRow(other.Name, string.Empty, other.Description, linuxWidth, windowsWidth);
        }

        renderer.WriteLine();
        renderer.WriteLine($"La columna Windows solo aparece cuando el nombre cambia; los demás se llaman igual en Linux y en Windows. help <comando> da ejemplos; temas: {TopicList()}.", renderer.Theme.Muted);
    }

    private void WriteRow(string linux, string windows, string description, int linuxWidth, int windowsWidth)
    {
        renderer.Write("  " + linux.PadRight(linuxWidth), renderer.Theme.Command);
        renderer.Write("  " + windows.PadRight(windowsWidth), renderer.Theme.Info);
        renderer.WriteLine("  " + description, renderer.Theme.Muted);
    }
}
