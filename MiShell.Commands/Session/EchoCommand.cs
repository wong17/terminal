using MiShell.Core.Contracts;

namespace MiShell.Commands.Session;

/// <summary><c>echo</c>: imprime sus argumentos; <c>-n</c> omite el salto de línea y <c>-e</c> interpreta secuencias de escape.</summary>
public sealed class EchoCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "echo";

    public string Description => "Imprime texto; -n omite el salto de línea y -e interpreta secuencias de escape.";

    public string Usage => "echo [-n] [-e] [texto...]";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var noNewline = false;
        var escapes = false;
        var index = 0;
        for (; index < arguments.Length && arguments[index] is "-n" or "-e" or "-ne" or "-en"; index++)
        {
            noNewline |= arguments[index].Contains('n');
            escapes |= arguments[index].Contains('e');
        }

        var text = string.Join(' ', arguments.Skip(index));
        if (escapes) text = text.Replace(@"\n", "\n").Replace(@"\t", "\t").Replace(@"\\", "\\");
        if (noNewline) renderer.Write(text);
        else renderer.WriteLine(text);
        return Task.FromResult(CommandResult.Ok);
    }
}
