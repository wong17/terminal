using MiShell.Core.Contracts;
using MiShell.Core.Execution;
using MiShell.Core.Parsing;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Ejecuta un archivo de comandos simple con extensión <c>.mish</c>.</summary>
public sealed class SourceCommand(IOutputRenderer renderer, IServiceProvider services) : IBuiltinCommand
{
    public string Name => "source";

    public string Description => "Ejecuta un script .mish en la sesión actual.";

    public string Usage => "source <archivo.mish>";

    public async Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length != 1)
        {
            renderer.WriteError("Uso: source <archivo.mish>");
            return CommandResult.Failure;
        }

        string scriptPath;
        try
        {
            scriptPath = Path.GetFullPath(arguments[0], context.WorkingDirectory);
        }
        catch (ArgumentException ex)
        {
            renderer.WriteError($"Ruta de script inválida: {ex.Message}");
            return CommandResult.Failure;
        }

        if (!File.Exists(scriptPath))
        {
            renderer.WriteError($"No existe el script: '{arguments[0]}'");
            return CommandResult.Failure;
        }

        var parser = services.GetService(typeof(Parser)) as Parser
                     ?? throw new InvalidOperationException("El parser no está registrado.");
        var executor = services.GetService(typeof(IShellExecutor)) as IShellExecutor
                       ?? throw new InvalidOperationException("El ejecutor no está registrado.");
        var exitCode = 0;

        try
        {
            foreach (var sourceLine in File.ReadLines(scriptPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = sourceLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                try
                {
                    var commandLine = parser.Parse(line);
                    if (commandLine.Pipelines.Count == 0)
                    {
                        continue;
                    }

                    exitCode = await executor.ExecuteAsync(commandLine, cancellationToken).ConfigureAwait(false);
                    if (context is ShellState { ExitRequested: true })
                    {
                        break;
                    }
                }
                catch (SyntaxException ex)
                {
                    renderer.WriteError($"Error de sintaxis en script: {ex.Message}");
                    exitCode = 1;
                }
                catch (CommandNotFoundException ex)
                {
                    renderer.WriteError(ex.Message);
                    exitCode = 1;
                }
            }
        }
        catch (IOException ex)
        {
            renderer.WriteError($"No se pudo leer el script: {ex.Message}");
            return CommandResult.Failure;
        }
        catch (UnauthorizedAccessException)
        {
            renderer.WriteError($"Sin permisos para leer el script: '{arguments[0]}'");
            return CommandResult.Failure;
        }

        return new CommandResult(exitCode);
    }
}
