using MiShell.Core.Contracts;

namespace MiShell.UI.Input;

/// <summary>Completa la última palabra y devuelve la línea completa, conservando rutas y comillas.</summary>
public sealed class CompletionProvider(
    Func<IEnumerable<string>> builtinNames,
    Func<string> workingDirectory,
    IShellEnvironment environment)
{
    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".com", ".cmd", ".bat", ".ps1" };

    public string[] Complete(string line)
    {
        var (start, command) = ActiveWord(line);
        var typed = line[start..];
        var prefix = typed.Replace("\"", "", StringComparison.Ordinal).Replace("'", "", StringComparison.Ordinal);
        var before = line[..start];
        try
        {
            if (command && !prefix.Contains('\\') && !prefix.Contains('/') && !Path.IsPathRooted(prefix))
            {
                var names = new HashSet<string>(builtinNames(), StringComparer.OrdinalIgnoreCase);
                foreach (var directory in (environment.GetVariable("PATH") ?? "")
                    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(directory))
                            if (ExecutableExtensions.Contains(Path.GetExtension(file)))
                                names.Add(Path.GetFileNameWithoutExtension(file));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
                }
                return names.Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Order(StringComparer.OrdinalIgnoreCase).Take(40).Select(n => before + Quote(n, typed)).ToArray();
            }

            var expanded = environment.Expand(prefix);
            var typedDirectory = Path.GetDirectoryName(prefix) ?? "";
            var expandedDirectory = Path.GetDirectoryName(expanded);
            var directoryPath = string.IsNullOrEmpty(expandedDirectory) ? workingDirectory()
                : Path.GetFullPath(expandedDirectory, workingDirectory());
            var namePrefix = Path.GetFileName(expanded);
            return Directory.EnumerateFileSystemEntries(directoryPath)
                .Where(p => Path.GetFileName(p).StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase).Take(40)
                .Select(p => before + Quote(Path.Combine(typedDirectory, Path.GetFileName(p)), typed)).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    private static string Quote(string value, string typed)
    {
        var quote = typed.StartsWith('\'') ? '\'' : '"';
        return typed.StartsWith('"') || typed.StartsWith('\'') || value.Any(char.IsWhiteSpace)
            ? $"{quote}{value}{quote}" : value;
    }

    private static (int Start, bool Command) ActiveWord(string line)
    {
        var start = 0;
        var expectingCommand = true;
        var activeCommand = true;
        var inWord = false;
        char? quote = null;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote is not null)
            {
                if (c == quote) quote = null;
                continue;
            }
            if (c is '"' or '\'')
            {
                if (!inWord) { start = i; activeCommand = expectingCommand; }
                inWord = true;
                quote = c;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (inWord) expectingCommand = false;
                inWord = false;
                start = i + 1;
                activeCommand = expectingCommand;
            }
            else if (c is '|' or '&' or '<' or '>')
            {
                expectingCommand = c is '|' or '&';
                inWord = false;
                start = i + 1;
                activeCommand = expectingCommand;
            }
            else if (!inWord)
            {
                start = i;
                activeCommand = expectingCommand;
                inWord = true;
            }
        }
        return (start, activeCommand);
    }
}
