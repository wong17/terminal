using System.Collections.Concurrent;

namespace MiShell.Core.Execution;

public sealed class ExecutableResolver : IExecutableResolver
{
    private static readonly string[] Extensions = [".exe", ".cmd", ".bat", ".com", ".ps1"];
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string? Resolve(string name)
    {
        return _cache.GetOrAdd(name, static n =>
        {
            if (n.Contains(Path.DirectorySeparatorChar) || n.Contains(Path.AltDirectorySeparatorChar))
            {
                return File.Exists(n) ? Path.GetFullPath(n) : null;
            }

            var pathDirs = (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var dir in pathDirs.Append(Directory.GetCurrentDirectory()))
            {
                foreach (var ext in Extensions)
                {
                    var candidate = Path.Combine(dir, n + ext);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }

                    // También permite nombres que ya traen extensión (ej. "script.cmd").
                    if (Path.GetExtension(n).Length > 0 && File.Exists(Path.Combine(dir, n)))
                    {
                        return Path.GetFullPath(Path.Combine(dir, n));
                    }
                }
            }

            return null;
        });
    }
}
