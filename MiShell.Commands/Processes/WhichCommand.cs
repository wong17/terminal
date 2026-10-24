using MiShell.Core.Contracts;
using MiShell.Core.Execution;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Localiza comandos como Get-Command / where: tipo, versión, tamaño y origen.</summary>
public sealed class WhichCommand(IOutputRenderer renderer, IExecutableResolver resolver, IServiceProvider services) : IBuiltinCommand
{
    private static readonly string[] Extensions = [".exe", ".com", ".cmd", ".bat", ".ps1"];

    public string Name => "which";

    public string Description => "Muestra tipo, versión y ruta de un comando (-a: todas las coincidencias).";

    public string Usage => "which [-a] <comando>...";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var all = arguments.Contains("-a") || arguments.Contains("--all");
        var names = arguments.Where(a => a is not "-a" and not "--all").ToArray();
        if (names.Length == 0)
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }

        var builtins = (IEnumerable<IBuiltinCommand>?)services.GetService(typeof(IEnumerable<IBuiltinCommand>)) ?? [];
        var rows = new List<string[]>();
        var failed = false;
        foreach (var name in names)
        {
            var found = false;
            var builtin = builtins.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (builtin is not null)
            {
                rows.Add(["interno", builtin.Name, "-", "-", "-", $"{builtin.Description} (uso: {builtin.Usage})"]);
                found = true;
            }

            foreach (var path in Candidates(name, context.WorkingDirectory).Take(all || builtin is null ? int.MaxValue : 1))
            {
                try
                {
                    var info = new FileInfo(path);
                    var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion ?? "-";
                    rows.Add(["externo", Path.GetFileName(path), version, MiShell.Core.ByteFormat.Human(info.Length),
                        MiShell.Core.ClockFormat.DateAndTime(info.LastWriteTime), path]);
                    found = true;
                    if (!all) break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    rows.Add(["externo", Path.GetFileName(path), "-", "-", "-", path]);
                    found = true;
                }
            }

            if (found) continue;
            renderer.WriteError($"No se encontró '{name}' como comando interno ni en el PATH.");
            failed = true;
        }

        if (rows.Count > 0)
            renderer.RenderTable("Comandos encontrados", ["Tipo", "Nombre", "Versión", "Tamaño", "Modificado", "Origen"], rows,
                cellColor: (row, column) => column switch
                {
                    0 => row[0] == "interno" ? renderer.Theme.Command : renderer.Theme.Info,
                    1 => renderer.Theme.Command,
                    5 => renderer.Theme.Path,
                    _ => renderer.Theme.Muted,
                });
        return Task.FromResult(failed ? CommandResult.Failure : CommandResult.Ok);
    }

    private IEnumerable<string> Candidates(string name, string workingDirectory)
    {
        if (name.Contains('\\') || name.Contains('/') || Path.IsPathRooted(name))
        {
            var full = Path.GetFullPath(name, workingDirectory);
            if (File.Exists(full)) yield return full;
            yield break;
        }

        var first = resolver.Resolve(name);
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(Directory.GetCurrentDirectory());
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (first is not null && seen.Add(first)) yield return first;
        foreach (var directory in directories)
        {
            var names = Path.HasExtension(name) ? new[] { name } : Extensions.Select(e => name + e).ToArray();
            foreach (var candidate in names)
            {
                string path;
                try { path = Path.GetFullPath(Path.Combine(directory, candidate)); }
                catch (ArgumentException) { continue; }
                if (File.Exists(path) && seen.Add(path)) yield return path;
            }
        }
    }
}
