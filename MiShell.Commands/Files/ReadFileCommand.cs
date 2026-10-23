using MiShell.Core.Contracts;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Muestra uno o varios archivos de texto (como type, cat y Get-Content).</summary>
public sealed class ReadFileCommand(IOutputRenderer renderer, string name) : IBuiltinCommand
{
    public string Name { get; } = name;

    public string Description => "Muestra archivos de texto; -n numera, --head y --tail limitan líneas.";

    public string Usage => $"{Name} [-n] [--head N] [--tail N] [archivo...]  ·  {Name} < archivo";
    public bool AcceptsInput => true;

    /// <inheritdoc />
    public async Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var number = false; int? head = null, tail = null;
        var files = new List<string>();
        for (var i = 0; i < arguments.Length; i++)
        {
            switch (arguments[i])
            {
                case "-n" or "--number": number = true; break;
                case "--head" when i + 1 < arguments.Length && int.TryParse(arguments[i + 1], out var h) && h >= 0: head = h; i++; break;
                case "--tail" when i + 1 < arguments.Length && int.TryParse(arguments[i + 1], out var t) && t >= 0: tail = t; i++; break;
                case var a when a.StartsWith("--", StringComparison.Ordinal): renderer.WriteError($"Uso: {Usage}"); return CommandResult.Failure;
                default: files.Add(arguments[i]); break;
            }
        }

        if (files.Count == 0)
        {
            var lines = new List<string>();
            while (await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                lines.Add(line);
            Write(lines, number, head, tail);
            return CommandResult.Ok;
        }

        var failed = false;
        foreach (var requestedPath in files)
        {
            var fullPath = Path.GetFullPath(requestedPath, context.WorkingDirectory);
            try
            {
                var lines = new List<string>();
                foreach (var line in File.ReadLines(fullPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lines.Add(line);
                }

                if (files.Count > 1) renderer.WriteLine($"==> {requestedPath} <==", renderer.Theme.Accent);
                Write(lines, number, head, tail);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException or IOException)
            {
                renderer.WriteError(ex switch
                {
                    FileNotFoundException => $"El archivo no existe: '{requestedPath}'",
                    DirectoryNotFoundException => $"El directorio no existe: '{requestedPath}'",
                    UnauthorizedAccessException => $"Sin permisos para leer: '{requestedPath}'",
                    _ => $"No se pudo leer '{requestedPath}': {ex.Message}",
                });
                failed = true;
            }
        }

        return failed ? CommandResult.Failure : CommandResult.Ok;
    }

    private void Write(List<string> lines, bool number, int? head, int? tail)
    {
        var start = 0;
        var count = lines.Count;
        if (tail is { } t && t < count) { start = count - t; count = t; }
        if (head is { } h && h < count) count = h;
        for (var i = start; i < start + count; i++)
            renderer.WriteLine(number ? $"{i + 1,6}  {lines[i]}" : lines[i]);
    }
}
