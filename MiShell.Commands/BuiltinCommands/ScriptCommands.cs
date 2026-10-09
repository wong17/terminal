using MiShell.Core.Contracts;
using MiShell.Core.Execution;
using MiShell.Core.Parsing;
using System.ComponentModel;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Scripts por líneas; conservan el contexto y señalan archivo/línea al fallar.</summary>
public sealed class SourceCommand(IOutputRenderer renderer, IServiceProvider services) : IBuiltinCommand
{
    private readonly HashSet<string> _activeScripts = new(StringComparer.OrdinalIgnoreCase);
    public string Name => "source";
    public string Description => "Ejecuta un script .mish en la sesión actual.";
    public string Usage => "source <archivo.mish>";

    public async Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length != 1)
        {
            renderer.WriteError($"Uso: {Usage}");
            return CommandResult.Failure;
        }
        var scriptPath = Path.GetFullPath(arguments[0], context.WorkingDirectory);
        if (!File.Exists(scriptPath))
        {
            renderer.WriteError($"No existe el script: '{arguments[0]}'");
            return CommandResult.Failure;
        }
        if (!_activeScripts.Add(scriptPath))
        {
            renderer.WriteError($"Script recursivo: '{arguments[0]}' ya se está ejecutando.");
            return CommandResult.Failure;
        }

        try
        {
            var parser = services.GetService(typeof(Parser)) as Parser
                ?? throw new InvalidOperationException("El parser no está registrado.");
            var executor = services.GetService(typeof(IShellExecutor)) as IShellExecutor
                ?? throw new InvalidOperationException("El ejecutor no está registrado.");
            var exitCode = 0;
            var lineNumber = 0;
            foreach (var sourceLine in File.ReadLines(scriptPath))
            {
                lineNumber++;
                cancellationToken.ThrowIfCancellationRequested();
                var line = sourceLine.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                try
                {
                    var command = parser.Parse(line);
                    var code = await executor.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                    if (code != 0)
                    {
                        exitCode = code;
                        renderer.WriteError($"{Path.GetFileName(scriptPath)}:{lineNumber}: comando terminó con código {code}.");
                    }
                    if (context is ShellState { ExitRequested: true }) break;
                }
                catch (Exception ex) when (ex is SyntaxException or CommandNotFoundException or Win32Exception or
                    IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or KeyNotFoundException)
                {
                    renderer.WriteError($"{Path.GetFileName(scriptPath)}:{lineNumber}: {ex.Message}");
                    exitCode = 1;
                }
            }
            return new CommandResult(exitCode);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            renderer.WriteError($"No se pudo leer '{arguments[0]}': {ex.Message}");
            return CommandResult.Failure;
        }
        finally { _activeScripts.Remove(scriptPath); }
    }
}
