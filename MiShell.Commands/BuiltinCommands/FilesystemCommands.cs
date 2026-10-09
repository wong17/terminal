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


/// <summary>Lista archivos y directorios, con detalle opcional.</summary>
public sealed class ListDirectoryCommand(IOutputRenderer renderer, string name) : IBuiltinCommand
{
    public string Name { get; } = name;
    public string Description => "Lista archivos; -l añade tamaño y fecha.";
    public string Usage => $"{Name} [-l | --long] [ruta]";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var detailed = arguments.Contains("-l") || arguments.Contains("--long");
        var paths = arguments.Where(a => a is not "-l" and not "--long").ToArray();
        if (paths.Length > 1 || paths.Any(p => p.StartsWith('-')))
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }
        var requestedPath = paths.Length == 0 ? "." : paths[0];
        var fullPath = Path.GetFullPath(requestedPath, context.WorkingDirectory);
        try
        {
            var entries = new DirectoryInfo(fullPath).EnumerateFileSystemInfos()
                .OrderBy(e => e is DirectoryInfo ? 0 : 1)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            var rows = entries.Select(entry =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var type = entry is DirectoryInfo ? "DIR" : "FILE";
                return detailed
                    ? (string[])[type, entry.Name,
                        entry is FileInfo file ? file.Length.ToString("N0", System.Globalization.CultureInfo.CurrentCulture) : "-",
                        entry.LastWriteTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)]
                    : [type, entry.Name];
            }).ToArray();
            renderer.RenderTable($"Contenido de {fullPath}",
                detailed ? ["Tipo", "Nombre", "Tamaño (bytes)", "Modificado"] : ["Tipo", "Nombre"], rows);
            renderer.WriteInfo($"{entries.Count(e => e is DirectoryInfo)} directorios; {entries.Count(e => e is FileInfo)} archivos.");
            return Task.FromResult(CommandResult.Ok);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or IOException)
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

    public string Usage => $"{Name} <archivo>  ·  {Name} < archivo";
    public bool AcceptsInput => true;

    public async Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length > 1)
        {
            renderer.WriteError($"Uso: {Name} <archivo>");
            return CommandResult.Failure;
        }

        if (arguments.Length == 0)
        {
            while (await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                renderer.WriteLine(line);
            return CommandResult.Ok;
        }

        var requestedPath = arguments[0];
        var fullPath = Path.GetFullPath(requestedPath, context.WorkingDirectory);
        try
        {
            foreach (var line in File.ReadLines(fullPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                renderer.WriteLine(line);
            }

            return CommandResult.Ok;
        }
        catch (FileNotFoundException)
        {
            renderer.WriteError($"El archivo no existe: '{requestedPath}'");
            return CommandResult.Failure;
        }
        catch (DirectoryNotFoundException)
        {
            renderer.WriteError($"El directorio no existe: '{requestedPath}'");
            return CommandResult.Failure;
        }
        catch (UnauthorizedAccessException)
        {
            renderer.WriteError($"Sin permisos para leer: '{requestedPath}'");
            return CommandResult.Failure;
        }
        catch (IOException ex)
        {
            renderer.WriteError($"No se pudo leer '{requestedPath}': {ex.Message}");
            return CommandResult.Failure;
        }
    }
}
