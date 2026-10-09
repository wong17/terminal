namespace MiShell.Core.Execution;

public sealed class ExecutableResolver : IExecutableResolver
{
    private static readonly string[] Extensions = [".exe", ".com", ".cmd", ".bat", ".ps1"];

    public string? Resolve(string name)
    {
        if (name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar) || Path.IsPathRooted(name))
            return File.Exists(name) ? Path.GetFullPath(name) : null;

        var directories = (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(Directory.GetCurrentDirectory());
        foreach (var directory in directories)
        {
            if (Path.HasExtension(name))
            {
                var candidate = Path.Combine(directory, name);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            else
            {
                foreach (var extension in Extensions)
                {
                    var candidate = Path.Combine(directory, name + extension);
                    if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
            }
        }
        return null;
    }
}
