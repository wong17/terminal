using MiShell.Core.Contracts;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>
/// Lista archivos y directorios con la información de <c>dir</c> y <c>Get-ChildItem</c>:
/// atributos, fecha, tamaño y nombre, además de totales y espacio libre.
/// </summary>
public sealed class ListDirectoryCommand(IOutputRenderer renderer, string name) : IBuiltinCommand
{
    public string Name { get; } = name;

    public string Description => "Lista archivos: atributos, fecha, tamaño, nombre y totales.";

    public string Usage => $"{Name} [-l] [-a] [-b] [-R] [-t|-S] [-r] [ruta|patrón...]";

    private sealed record Options(bool Long, bool All, bool Bytes, bool Recurse, bool ByTime, bool BySize, bool Reverse);

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var (opts, targets) = Parse(arguments);
        if (opts is null)
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }

        if (targets.Count == 0) targets.Add(".");
        var failed = false;
        var totalFiles = 0; var totalDirs = 0; long totalBytes = 0;
        var showHeader = targets.Count > 1 || opts.Recurse;
        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var (dir, pattern) = Resolve(target, context.WorkingDirectory);
                var (f, d, b) = List(dir, pattern, opts, showHeader || pattern is not null, cancellationToken);
                totalFiles += f; totalDirs += d; totalBytes += b;
            }
            catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException or UnauthorizedAccessException or IOException)
            {
                renderer.WriteError($"No se pudo listar '{target}': {ex.Message}");
                failed = true;
            }
        }

        if (!failed || totalFiles + totalDirs > 0)
        {
            var summary = $"{totalFiles} archivos ({(opts.Bytes ? MiShell.Core.ByteFormat.Exact(totalBytes) : FileFormat.Human(totalBytes))}) · {totalDirs} directorios";
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(targets[0] is "." ? context.WorkingDirectory : targets[0], context.WorkingDirectory));
                if (root is not null) summary += $" · {FileFormat.Human(new DriveInfo(root).AvailableFreeSpace)} libres";
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { }
            renderer.WriteInfo(summary + ".");
        }

        return Task.FromResult(failed ? CommandResult.Failure : CommandResult.Ok);
    }

    private static (Options? Options, List<string> Targets) Parse(string[] arguments)
    {
        bool l = false, a = false, b = false, r = false, t = false, s = false, rev = false;
        var targets = new List<string>();
        foreach (var argument in arguments)
        {
            if (argument.Length > 1 && argument[0] == '-' && !Directory.Exists(argument) && !File.Exists(argument))
            {
                var flags = argument.StartsWith("--", StringComparison.Ordinal)
                    ? argument switch
                    {
                        "--long" => "l", "--all" => "a", "--human" => "h", "--bytes" => "b", "--recurse" => "R", "--reverse" => "r",
                        _ => "?",
                    }
                    : argument[1..];
                foreach (var c in flags)
                {
                    switch (c)
                    {
                        case 'l': l = true; break;
                        case 'a': a = true; break;
                        case 'h': break; // el tamaño ya es legible; se acepta por compatibilidad
                        case 'b': b = true; break;
                        case 'R': r = true; break;
                        case 't': t = true; break;
                        case 'S': s = true; break;
                        case 'r': rev = true; break;
                        default: return (null, targets);
                    }
                }
            }
            else targets.Add(argument);
        }

        return (new Options(l, a, b, r, t, s, rev), targets);
    }

    /// <summary>Separa la ruta del patrón con comodines (*.txt) o detecta un archivo individual.</summary>
    private static (string Directory, string? Pattern) Resolve(string target, string workingDirectory)
    {
        var full = Path.GetFullPath(target, workingDirectory);
        if (target.AsSpan().IndexOfAny('*', '?') >= 0 || File.Exists(full))
            return (Path.GetDirectoryName(full) ?? full, Path.GetFileName(full));
        return !Directory.Exists(full) ? throw new DirectoryNotFoundException($"no existe la ruta '{target}'") : (full, null);
    }

    private (int Files, int Dirs, long Bytes) List(string directory, string? pattern, Options opts, bool header, CancellationToken token)
    {
        var info = new DirectoryInfo(directory);
        var regex = pattern is null ? null : FileFormat.Wildcard(pattern);
        var entries = info.EnumerateFileSystemInfos()
            .Where(e => regex is null || regex.IsMatch(e.Name))
            .Where(e => opts.All || (e.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0);
        entries = Order(entries, opts);
        var list = entries.ToArray();
        var rows = new List<string[]>();
        int files = 0, dirs = 0; long bytes = 0;
        foreach (var entry in list)
        {
            token.ThrowIfCancellationRequested();
            var isDir = entry is DirectoryInfo;
            var length = entry is FileInfo f ? f.Length : 0;
            if (isDir) dirs++; else { files++; bytes += length; }
            var size = isDir ? string.Empty : opts.Bytes ? MiShell.Core.ByteFormat.Exact(length) : FileFormat.Human(length);
            var label = entry.Name;
            if (entry.LinkTarget is { } link) label += " -> " + link;
            rows.Add(opts.Long
                ? [FileFormat.Mode(entry), FileFormat.Date(entry.CreationTime), FileFormat.Date(entry.LastWriteTime), FileFormat.Date(entry.LastAccessTime), size, label]
                : [FileFormat.Mode(entry), FileFormat.Date(entry.LastWriteTime), size, label]);
        }

        var title = header ? $"Directorio: {directory}" : $"Contenido de {directory}";
        renderer.RenderTable(title,
            opts.Long ? ["Modo", "Creado", "Modificado", "Accedido", "Tamaño", "Nombre"] : ["Modo", "Modificado", "Tamaño", "Nombre"], rows, RowColor, CellColor);

        if (!opts.Recurse) return (files, dirs, bytes);
        foreach (var sub in list.OfType<DirectoryInfo>().Where(s => s.LinkTarget is null))
        {
            try
            {
                var (f2, d2, b2) = List(sub.FullName, null, opts, true, token);
                files += f2; dirs += d2; bytes += b2;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                renderer.WriteError($"No se pudo listar '{sub.FullName}': {ex.Message}");
            }
        }

        return (files, dirs, bytes);
    }

    /// <summary>
    /// Color de cada fila según el modo: directorios con el acento del tema, ocultos atenuados,
    /// enlaces en color informativo y programas ejecutables en verde.
    /// </summary>
    private ConsoleColor? RowColor(string[] row)
    {
        var mode = row[0];
        if (mode[0] == 'd') return renderer.Theme.Directory;
        if (mode[3] == 'h') return renderer.Theme.Muted;
        if (mode[5] == 'l') return renderer.Theme.Info;
        string[] programs = [".exe", ".com", ".cmd", ".bat", ".ps1"];
        return programs.Contains(Path.GetExtension(row[^1]), StringComparer.OrdinalIgnoreCase) ? renderer.Theme.Success : null;
    }

    /// <summary>Atenúa modo, fechas y tamaño para que el nombre sea lo que destaque.</summary>
    private ConsoleColor? CellColor(string[] row, int column) => column == row.Length - 1 ? RowColor(row) : renderer.Theme.Muted;

    private static IEnumerable<FileSystemInfo> Order(IEnumerable<FileSystemInfo> entries, Options opts)
    {
        var grouped = entries.OrderBy(e => e is DirectoryInfo ? 0 : 1);
        var ordered = opts switch
        {
            { ByTime: true } => grouped.ThenByDescending(e => e.LastWriteTime),
            { BySize: true } => grouped.ThenByDescending(e => e is FileInfo f ? f.Length : 0),
            _ => grouped.ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
        };
        return opts.Reverse ? ordered.Reverse() : ordered;
    }
}
