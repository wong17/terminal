using MiShell.Core.Contracts;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Alias compatible con la consigna y con la consola clásica de Windows.</summary>
public sealed class ClearCommand(IOutputRenderer renderer, string name) : IBuiltinCommand
{
    public string Name { get; } = name;

    public string Description => "Limpia la pantalla.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        renderer.ClearScreen();
        return Task.FromResult(CommandResult.Ok);
    }
}

/// <summary>Lista archivos y directorios sin depender de comandos del sistema anfitrión.</summary>
public sealed class ListDirectoryCommand(IOutputRenderer renderer, string name) : IBuiltinCommand
{
    public string Name { get; } = name;

    public string Description => "Lista archivos y directorios.";

    public string Usage => $"{Name} [ruta]";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var requestedPath = arguments.Length == 0 ? "." : arguments[0];
        var fullPath = Path.GetFullPath(requestedPath, context.WorkingDirectory);

        try
        {
            var rows = Directory.EnumerateFileSystemEntries(fullPath)
                .Select(entry =>
                {
                    var isDirectory = Directory.Exists(entry);
                    var displayName = Path.GetFileName(entry);
                    return (string[])
                    [
                        isDirectory ? "DIR" : "FILE",
                        string.IsNullOrEmpty(displayName) ? entry : displayName,
                    ];
                })
                .OrderBy(row => row[0], StringComparer.Ordinal)
                .ThenBy(row => row[1], StringComparer.OrdinalIgnoreCase)
                .ToArray();

            renderer.RenderTable($"Contenido de {fullPath}", ["Tipo", "Nombre"], rows);
            return Task.FromResult(CommandResult.Ok);
        }
        catch (DirectoryNotFoundException)
        {
            renderer.WriteError($"El directorio no existe: '{requestedPath}'");
            return Task.FromResult(CommandResult.Failure);
        }
        catch (UnauthorizedAccessException)
        {
            renderer.WriteError($"Sin permisos para leer: '{requestedPath}'");
            return Task.FromResult(CommandResult.Failure);
        }
        catch (IOException ex)
        {
            renderer.WriteError($"No se pudo listar '{requestedPath}': {ex.Message}");
            return Task.FromResult(CommandResult.Failure);
        }
    }
}

/// <summary>Crea uno o varios directorios usando la API del sistema de archivos.</summary>
public sealed class MakeDirectoryCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "mkdir";

    public string Description => "Crea uno o varios directorios.";

    public string Usage => "mkdir <ruta> [ruta...]";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            renderer.WriteError("Uso: mkdir <ruta> [ruta...]");
            return Task.FromResult(CommandResult.Failure);
        }

        try
        {
            foreach (var argument in arguments)
            {
                var fullPath = Path.GetFullPath(argument, context.WorkingDirectory);
                Directory.CreateDirectory(fullPath);
                renderer.WriteSuccess($"Directorio listo: {fullPath}");
            }

            return Task.FromResult(CommandResult.Ok);
        }
        catch (ArgumentException ex)
        {
            renderer.WriteError($"Ruta inválida: {ex.Message}");
            return Task.FromResult(CommandResult.Failure);
        }
        catch (UnauthorizedAccessException)
        {
            renderer.WriteError("Sin permisos para crear el directorio.");
            return Task.FromResult(CommandResult.Failure);
        }
        catch (IOException ex)
        {
            renderer.WriteError($"No se pudo crear el directorio: {ex.Message}");
            return Task.FromResult(CommandResult.Failure);
        }
    }
}

/// <summary>Lee un archivo de texto para verificar rápidamente redirecciones.</summary>
public sealed class ReadFileCommand(IOutputRenderer renderer, string name) : IBuiltinCommand
{
    public string Name { get; } = name;

    public string Description => "Muestra el contenido de un archivo de texto.";

    public string Usage => $"{Name} <archivo>";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length != 1)
        {
            renderer.WriteError($"Uso: {Name} <archivo>");
            return Task.FromResult(CommandResult.Failure);
        }

        var requestedPath = arguments[0];
        var fullPath = Path.GetFullPath(requestedPath, context.WorkingDirectory);
        try
        {
            foreach (var line in File.ReadLines(fullPath))
            {
                renderer.WriteLine(line);
            }

            return Task.FromResult(CommandResult.Ok);
        }
        catch (FileNotFoundException)
        {
            renderer.WriteError($"El archivo no existe: '{requestedPath}'");
            return Task.FromResult(CommandResult.Failure);
        }
        catch (DirectoryNotFoundException)
        {
            renderer.WriteError($"El directorio no existe: '{requestedPath}'");
            return Task.FromResult(CommandResult.Failure);
        }
        catch (UnauthorizedAccessException)
        {
            renderer.WriteError($"Sin permisos para leer: '{requestedPath}'");
            return Task.FromResult(CommandResult.Failure);
        }
        catch (IOException ex)
        {
            renderer.WriteError($"No se pudo leer '{requestedPath}': {ex.Message}");
            return Task.FromResult(CommandResult.Failure);
        }
    }
}
