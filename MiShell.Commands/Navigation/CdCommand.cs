using MiShell.Core.Contracts;

namespace MiShell.Commands.Navigation;

/// <summary>Cambia de directorio: ruta, ~, .., cd - (anterior) y cd /d para cambiar de unidad, como cmd.</summary>
public sealed class CdCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "cd";

    public string Description => "Cambia el directorio de trabajo (~, .., - y /d unidad).";

    public string Usage => "cd [ruta]  ·  cd ..  ·  cd ~  ·  cd -  ·  cd /d D:\\ruta";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var parts = arguments.Where(a => !a.Equals("/d", StringComparison.OrdinalIgnoreCase)).ToArray();
        // cmd y PowerShell aceptan rutas con espacios sin comillas: se unen los fragmentos.
        var target = parts.Length == 0 ? home : string.Join(' ', parts);
        if (target == "~") target = home;
        else if (target.StartsWith("~/", StringComparison.Ordinal) || target.StartsWith("~\\", StringComparison.Ordinal))
            target = Path.Combine(home, target[2..]);
        else if (target == "-")
        {
            if (context.PreviousDirectory is null)
            {
                renderer.WriteError("No hay un directorio anterior.");
                return Task.FromResult(CommandResult.Failure);
            }

            target = context.PreviousDirectory;
        }

        string fullPath;
        try { fullPath = Path.GetFullPath(target, context.WorkingDirectory); }
        catch (ArgumentException)
        {
            renderer.WriteError($"Ruta inválida: '{target}'");
            return Task.FromResult(CommandResult.Failure);
        }

        if (!Directory.Exists(fullPath))
        {
            renderer.WriteError(File.Exists(fullPath)
                ? $"'{target}' es un archivo, no un directorio."
                : $"El directorio no existe: '{target}'");
            return Task.FromResult(CommandResult.Failure);
        }

        context.ChangeDirectory(fullPath);
        Environment.CurrentDirectory = fullPath;
        if (arguments.Length > 0 && parts.Length == 1 && parts[0] == "-")
            renderer.WriteLine(fullPath, renderer.Theme.Path);
        return Task.FromResult(CommandResult.Ok);
    }
}
