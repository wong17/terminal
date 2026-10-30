using MiShell.Core;
using MiShell.Core.Contracts;

namespace MiShell.Commands.Files;

/// <summary>
/// Elimina archivos y carpetas con los nombres de Linux y de Windows: <c>rm</c>, <c>del</c>/<c>erase</c> (archivos y patrones)
/// y <c>rmdir</c>/<c>rd</c> (carpetas). <c>-r</c> (o <c>/s</c>) entra en las carpetas y <c>-f</c> (o <c>/q</c>) evita la
/// confirmación. Borrar una carpeta con contenido pide confirmación; nunca se eliminan raíces de unidad ni la carpeta actual.
/// </summary>
public sealed class RemoveCommand(IOutputRenderer renderer, string name) : IBuiltinCommand
{
    private readonly bool _foldersOnly = name.Equals("rmdir", StringComparison.OrdinalIgnoreCase) || name.Equals("rd", StringComparison.OrdinalIgnoreCase);

    public string Name { get; } = name;

    public string Description => _foldersOnly
        ? "Elimina carpetas; -r (o /s) incluye su contenido y pide confirmación."
        : "Elimina archivos y patrones; -r borra también carpetas, -f evita preguntar.";

    public string Usage => _foldersOnly ? $"{Name} [-r] [-f] <carpeta>..." : $"{Name} [-r] [-f] <ruta|patrón>...";

    private sealed record Counters(int Files, int Folders, long Bytes);

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        bool recursive = false, force = false;
        var targets = new List<string>();
        foreach (var argument in arguments)
        {
            if (argument is ['/', _] && "sSqQ".Contains(argument[1]))
            {
                if (char.ToLowerInvariant(argument[1]) == 's') recursive = true; else force = true;
            }
            else if (argument.Length > 1 && argument[0] == '-' && !argument.StartsWith("--", StringComparison.Ordinal)
                && !File.Exists(argument) && !Directory.Exists(argument))
            {
                foreach (var flag in argument[1..])
                {
                    switch (flag)
                    {
                        case 'r' or 'R': recursive = true; break;
                        case 'f': force = true; break;
                        case 'v': break;
                        default: return Fail($"Uso: {Usage}");
                    }
                }
            }
            else if (argument is "--recursive") recursive = true;
            else if (argument is "--force") force = true;
            else targets.Add(argument);
        }

        if (targets.Count == 0) return Fail($"Uso: {Usage}");

        var totals = new Counters(0, 0, 0);
        var failed = false;
        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var (full, label) in Expand(target, context.WorkingDirectory, force, ref failed))
            {
                var result = RemoveOne(full, label, recursive, force, context.WorkingDirectory);
                if (result is null) failed = true;
                else totals = new Counters(totals.Files + result.Files, totals.Folders + result.Folders, totals.Bytes + result.Bytes);
            }
        }

        if (totals.Files + totals.Folders > 1)
            renderer.WriteSuccess($"{totals.Files} archivos y {totals.Folders} carpetas eliminados · {ByteFormat.Human(totals.Bytes)} liberados.");
        return Task.FromResult(failed ? CommandResult.Failure : CommandResult.Ok);
    }

    private Task<CommandResult> Fail(string message)
    {
        renderer.WriteError(message);
        return Task.FromResult(CommandResult.Failure);
    }

    /// <summary>Resuelve un argumento a rutas concretas; los comodines se expanden en la carpeta indicada.</summary>
    private List<(string Full, string Label)> Expand(string target, string workingDirectory, bool force, ref bool failed)
    {
        var result = new List<(string, string)>();
        string full;
        try { full = Path.GetFullPath(target, workingDirectory); }
        catch (ArgumentException ex)
        {
            renderer.WriteError($"Ruta inválida '{target}': {ex.Message}");
            failed = true;
            return result;
        }

        if (target.AsSpan().IndexOfAny('*', '?') < 0)
        {
            if (File.Exists(full) || Directory.Exists(full)) result.Add((full, target));
            else if (!force)
            {
                renderer.WriteError($"No existe '{target}'.");
                failed = true;
            }

            return result;
        }

        var directory = Path.GetDirectoryName(full);
        if (directory is null || !Directory.Exists(directory))
        {
            renderer.WriteError($"No existe la carpeta de '{target}'.");
            failed = true;
            return result;
        }

        var regex = FileFormat.Wildcard(Path.GetFileName(full));
        var matches = new DirectoryInfo(directory).EnumerateFileSystemInfos()
            .Where(e => regex.IsMatch(e.Name) && (e is DirectoryInfo == _foldersOnly))
            .Select(e => (e.FullName, Path.Combine(Path.GetDirectoryName(target) ?? string.Empty, e.Name)))
            .ToList();
        if (matches.Count == 0 && !force)
        {
            renderer.WriteError($"No hay coincidencias para '{target}'.");
            failed = true;
        }

        return matches;
    }

    /// <summary>Elimina un archivo o una carpeta; devuelve lo eliminado, o <c>null</c> si falló o se canceló (ya informado).</summary>
    private Counters? RemoveOne(string full, string label, bool recursive, bool force, string workingDirectory)
    {
        var isDirectory = Directory.Exists(full);
        if (Guard(full, label, isDirectory, workingDirectory) is { } blocked)
        {
            renderer.WriteError(blocked);
            return null;
        }

        try
        {
            if (!isDirectory)
            {
                if (_foldersOnly)
                {
                    renderer.WriteError($"'{label}' no es una carpeta; usa rm o del para archivos.");
                    return null;
                }

                var file = new FileInfo(full);
                var size = file.Length;
                if (file.IsReadOnly)
                {
                    if (!force)
                    {
                        renderer.WriteError($"'{label}' es de solo lectura; usa -f (o /q) para eliminarlo.");
                        return null;
                    }

                    file.IsReadOnly = false;
                }

                file.Delete();
                renderer.WriteSuccess($"Eliminado: {label} ({ByteFormat.Human(size)})");
                return new Counters(1, 0, size);
            }

            var directory = new DirectoryInfo(full);
            var isLink = directory.LinkTarget is not null;
            var inside = isLink ? new Counters(0, 0, 0) : Inventory(directory);
            var empty = inside.Files + inside.Folders == 0;
            if (!empty && !recursive)
            {
                renderer.WriteError(_foldersOnly
                    ? $"La carpeta '{label}' no está vacía; usa {Name} -r (o /s) para borrar también su contenido."
                    : $"'{label}' es una carpeta; usa rm -r para eliminarla con su contenido.");
                return null;
            }

            if (empty && !_foldersOnly && !recursive)
            {
                renderer.WriteError($"'{label}' es una carpeta; usa rmdir o rm -r.");
                return null;
            }

            if (!empty && !force && !Confirm(label, inside)) return null;
            if (force && !isLink) ClearReadOnly(directory);
            if (isLink) directory.Delete();
            else directory.Delete(recursive: true);
            renderer.WriteSuccess(empty
                ? $"Eliminada la carpeta: {label}"
                : $"Eliminada la carpeta: {label} ({inside.Files} archivos, {inside.Folders} carpetas, {ByteFormat.Human(inside.Bytes)})");
            return new Counters(inside.Files, inside.Folders + 1, inside.Bytes);
        }
        catch (UnauthorizedAccessException)
        {
            renderer.WriteError($"Sin permisos para eliminar '{label}' (¿solo lectura o en uso? prueba con -f).");
            return null;
        }
        catch (IOException ex)
        {
            renderer.WriteError($"No se pudo eliminar '{label}': {ex.Message}");
            return null;
        }
    }

    /// <summary>Rechaza lo que nunca debe borrarse: raíces de unidad, la carpeta actual y sus padres, y las carpetas del sistema.</summary>
    private static string? Guard(string full, string label, bool isDirectory, string workingDirectory)
    {
        var path = Path.TrimEndingDirectorySeparator(full);
        var root = Path.GetPathRoot(full);
        if (root is not null && string.Equals(Path.TrimEndingDirectorySeparator(root), path, StringComparison.OrdinalIgnoreCase))
            return $"No se elimina '{label}': es la raíz de una unidad.";
        if (!isDirectory) return null;
        var current = Path.TrimEndingDirectorySeparator(workingDirectory);
        if (current.Equals(path, StringComparison.OrdinalIgnoreCase)
            || current.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return $"No se elimina '{label}': contiene el directorio actual. Sal de ella con cd .. primero.";
        string[] protectedFolders =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        ];
        return protectedFolders.Any(p => p.Length > 0 && Path.TrimEndingDirectorySeparator(p).Equals(path, StringComparison.OrdinalIgnoreCase))
            ? $"No se elimina '{label}': es una carpeta protegida del sistema."
            : null;
    }

    /// <summary>Pregunta antes de borrar una carpeta con contenido; sin entrada interactiva exige -f.</summary>
    private bool Confirm(string label, Counters inside)
    {
        if (Console.IsInputRedirected)
        {
            renderer.WriteError($"'{label}' tiene contenido y no hay entrada interactiva para confirmar; usa -f (o /q).");
            return false;
        }

        renderer.Write($"¿Eliminar '{label}' y todo su contenido ({inside.Files} archivos, {inside.Folders} carpetas, {ByteFormat.Human(inside.Bytes)})? [s/N] ", renderer.Theme.Info);
        var answer = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
        if (answer is "s" or "si" or "sí" or "y" or "yes") return true;
        renderer.WriteInfo("Cancelado; no se eliminó nada.");
        return false;
    }

    private static Counters Inventory(DirectoryInfo directory)
    {
        int files = 0, folders = 0;
        long bytes = 0;
        try
        {
            foreach (var entry in directory.EnumerateFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true }))
            {
                if (entry is FileInfo file) { files++; bytes += file.Length; }
                else folders++;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        return new Counters(files, folders, bytes);
    }

    private static void ClearReadOnly(DirectoryInfo directory)
    {
        foreach (var file in directory.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true }))
        {
            if (file.IsReadOnly) file.IsReadOnly = false;
        }
    }
}
