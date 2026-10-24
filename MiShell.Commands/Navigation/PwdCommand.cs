using MiShell.Core.Contracts;

namespace MiShell.Commands.BuiltinCommands;

/// <summary><c>pwd</c>: muestra el directorio actual; con <c>-v</c> añade unidad, formato, espacio libre y el directorio anterior (Get-Location ampliado).</summary>
public sealed class PwdCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "pwd";

    public string Description => "Muestra el directorio actual; -v añade unidad, espacio libre y anterior.";

    public string Usage => "pwd [-v]";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length > 1 || arguments.Length == 1 && arguments[0] is not ("-v" or "--verbose"))
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }

        // Como Get-Location: encabezado "Path", subrayado y la ruta en un color propio.
        renderer.RenderTable(string.Empty, ["Path"], [[context.WorkingDirectory]], cellColor: (_, _) => renderer.Theme.Directory);
        if (arguments.Length != 1) return Task.FromResult(CommandResult.Ok);
        var rows = new List<string[]>();
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(context.WorkingDirectory) ?? "C:\\");
            rows.Add(["Unidad", $"{drive.Name} ({drive.DriveType}, {drive.DriveFormat})"]);
            rows.Add(["Espacio libre", $"{MiShell.Core.ByteFormat.Human(drive.AvailableFreeSpace)} de {MiShell.Core.ByteFormat.Human(drive.TotalSize)}"]);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { }
        rows.Add(["Directorio anterior", context.PreviousDirectory ?? "(ninguno)"]);
        renderer.WriteLine();
        renderer.RenderList(string.Empty, rows.Select(r => (r[0], r[1])));

        return Task.FromResult(CommandResult.Ok);
    }
}
