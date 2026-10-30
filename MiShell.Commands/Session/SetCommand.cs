using MiShell.Core.Contracts;

namespace MiShell.Commands.Session;

/// <summary><c>set</c>: lista variables de entorno, las filtra por prefijo (como cmd) o define una indicando su valor anterior.</summary>
public sealed class SetCommand(IOutputRenderer renderer, IShellEnvironment environment) : IBuiltinCommand
{
    public string Name => "set";

    public string Description => "Lista, consulta o define variables de entorno (como set de cmd).";

    public string Usage => "set  ·  set PREFIJO  ·  set NOMBRE=valor";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            Show("Variables de entorno", environment.EnumerateVariables());
            return Task.FromResult(CommandResult.Ok);
        }

        var parts = arguments[0].Split('=', 2);
        if (arguments.Length == 1 && parts.Length == 1)
        {
            // set P: variables que empiezan por P (si coincide una exacta, solo esa).
            var all = environment.EnumerateVariables().ToArray();
            var exact = all.Where(v => v.Name.Equals(parts[0], StringComparison.OrdinalIgnoreCase)).ToArray();
            var matches = exact.Length > 0 ? exact : all.Where(v => v.Name.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0)
            {
                renderer.WriteError($"No hay variables que empiecen por '{parts[0]}'.");
                return Task.FromResult(CommandResult.Failure);
            }

            Show($"Variables que coinciden con '{parts[0]}'", matches);
            return Task.FromResult(CommandResult.Ok);
        }

        if (arguments.Length != 1 || parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            renderer.WriteError("Formato esperado: set NOMBRE=valor");
            return Task.FromResult(CommandResult.Failure);
        }

        var previous = environment.GetVariable(parts[0]);
        environment.SetVariable(parts[0], parts[1]);
        renderer.WriteSuccess(previous is null
            ? $"{parts[0]} = {parts[1]}  (nueva)"
            : $"{parts[0]} = {parts[1]}  (antes: {previous})");
        return Task.FromResult(CommandResult.Ok);
    }

    private void Show(string title, IEnumerable<(string Name, string Value)> variables)
    {
        var list = variables.ToArray();
        renderer.RenderTable(title, ["Nombre", "Valor"], list.Select(v => (string[])[v.Name, v.Value]),
            cellColor: (_, column) => column == 0 ? renderer.Theme.Command : null);
        renderer.WriteInfo($"{list.Length} variables.");
    }
}
