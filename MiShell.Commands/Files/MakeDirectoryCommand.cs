using MiShell.Core.Contracts;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Crea uno o varios directorios (con padres, como mkdir -p y como mkdir de Windows) y muestra lo creado.</summary>
public sealed class MakeDirectoryCommand(IOutputRenderer renderer, string name = "mkdir") : IBuiltinCommand
{
    public string Name { get; } = name;

    public string Description => "Crea directorios (con sus padres) y muestra el resultado.";

    public string Usage => $"{Name} [-p] <ruta> [ruta...]";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var paths = arguments.Where(a => a is not "-p" and not "--parents").ToArray();
        if (paths.Length == 0)
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }

        var created = new List<DirectoryInfo>();
        var existing = new List<string>();
        var failed = false;
        foreach (var argument in paths)
        {
            try
            {
                var fullPath = Path.GetFullPath(argument, context.WorkingDirectory);
                if (File.Exists(fullPath))
                {
                    renderer.WriteError($"Ya existe un archivo con ese nombre: '{argument}'");
                    failed = true;
                    continue;
                }

                if (Directory.Exists(fullPath)) { existing.Add(fullPath); continue; }
                created.Add(Directory.CreateDirectory(fullPath));
            }
            catch (ArgumentException ex)
            {
                renderer.WriteError($"Ruta inválida: {ex.Message}");
                failed = true;
            }
            catch (UnauthorizedAccessException)
            {
                renderer.WriteError($"Sin permisos para crear '{argument}'.");
                failed = true;
            }
            catch (IOException ex)
            {
                renderer.WriteError($"No se pudo crear '{argument}': {ex.Message}");
                failed = true;
            }
        }

        if (created.Count > 0)
        {
            renderer.RenderTable("Directorios creados", ["Modo", "Modificado", "Nombre"],
                created.Select(d => (string[])[FileFormat.Mode(d), FileFormat.Date(d.LastWriteTime), d.FullName]),
                cellColor: (_, column) => column == 2 ? renderer.Theme.Path : renderer.Theme.Muted);
        }

        foreach (var path in existing)
            renderer.WriteInfo($"Ya existía: {path}");
        if (paths.Length > 1) renderer.WriteSuccess($"{created.Count} creados · {existing.Count} ya existían.");
        return Task.FromResult(failed ? CommandResult.Failure : CommandResult.Ok);
    }
}
