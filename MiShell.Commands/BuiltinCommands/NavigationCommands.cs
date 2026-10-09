using MiShell.Core.Contracts;

namespace MiShell.Commands.BuiltinCommands;

public sealed class CdCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "cd";

    public string Description => "Cambia el directorio de trabajo.";

    public string Usage => "cd [ruta]  ·  cd ..  ·  cd ~";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var target = arguments.Length == 0 || arguments[0] == "~" ? home : arguments[0];

        var fullPath = Path.GetFullPath(target, context.WorkingDirectory);
        if (!Directory.Exists(fullPath))
        {
            renderer.WriteError($"El directorio no existe: '{target}'");
            return Task.FromResult(CommandResult.Failure);
        }

        context.ChangeDirectory(fullPath);
        Environment.CurrentDirectory = fullPath;
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class PwdCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "pwd";

    public string Description => "Muestra el directorio de trabajo actual.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        renderer.WriteLine(context.WorkingDirectory, renderer.Theme.Path);
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class ClsCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "cls";

    public string Description => "Limpia la pantalla.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        renderer.ClearScreen();
        return Task.FromResult(CommandResult.Ok);
    }
}
