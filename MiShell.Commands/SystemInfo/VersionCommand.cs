using MiShell.Core.Contracts;
using MiShell.Core.Execution;

namespace MiShell.Commands.BuiltinCommands;

/// <summary><c>version</c>: versión de MiShell, del runtime de .NET y del sistema operativo.</summary>
public sealed class VersionCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "version";

    public string Description => "Muestra la versión de MiShell, de .NET y del sistema.";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var assembly = typeof(VersionCommand).Assembly.GetName().Version;
        var rows = new List<string[]>
        {
            new[] { "MiShell", $"1.0 (ensamblado {assembly})" },
            new[] { "Runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription },
            new[] { "Sistema operativo", System.Runtime.InteropServices.RuntimeInformation.OSDescription },
            new[] { "Versión de Windows", Environment.OSVersion.VersionString },
            new[] { "Arquitectura del SO", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString() },
            new[] { "Arquitectura del proceso", System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() },
            new[] { "Equipo", Environment.MachineName },
            new[] { "Usuario", Environment.UserName },
            new[] { "PID de MiShell", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        };
        renderer.WriteLine($"MiShell 1.0 · .NET {Environment.Version} · {Environment.OSVersion.VersionString}");
        renderer.RenderList("Versión", rows.Select(r => (r[0], r[1])));
        return Task.FromResult(CommandResult.Ok);
    }
}
