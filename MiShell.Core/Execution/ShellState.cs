using MiShell.Core.Contracts;
using MiShell.Core.Jobs;

namespace MiShell.Core.Execution;

/// <summary>
/// Lo que la shell recuerda entre un comando y el siguiente: directorio actual (y el anterior),
/// variables de entorno, tabla de trabajos, historial y la petición de salir. Los comandos internos
/// lo reciben como <see cref="ICommandContext"/>.
/// </summary>
public sealed class ShellState(string initialDirectory, IShellEnvironment environment, IJobTable jobs) : ICommandContext
{
    /// <inheritdoc />
    public string WorkingDirectory { get; private set; } = initialDirectory;

    /// <inheritdoc />
    public string? PreviousDirectory { get; private set; }

    /// <inheritdoc />
    public IShellEnvironment Environment { get; } = environment;

    /// <inheritdoc />
    public IJobTable Jobs { get; } = jobs;

    /// <inheritdoc />
    public IReadOnlyList<string> History { get; set; } = [];

    /// <summary>True cuando un comando (exit) pidió terminar el ciclo principal.</summary>
    public bool ExitRequested { get; private set; }

    /// <inheritdoc />
    public bool TraceProcesses { get; set; }

    /// <summary>Código con el que debe terminar la aplicación tras <c>exit</c>.</summary>
    public int PendingExitCode { get; private set; }

    void ICommandContext.ChangeDirectory(string path) => ChangeDirectory(path);

    /// <summary>Cambia el directorio de trabajo y recuerda el anterior para <c>cd -</c>.</summary>
    public void ChangeDirectory(string path)
    {
        var next = Path.GetFullPath(path, WorkingDirectory);
        if (!next.Equals(WorkingDirectory, StringComparison.OrdinalIgnoreCase)) PreviousDirectory = WorkingDirectory;
        WorkingDirectory = next;
    }

    /// <inheritdoc />
    public void RequestExit(int exitCode)
    {
        ExitRequested = true;
        PendingExitCode = exitCode;
    }

    /// <summary>Devuelve el código de salida pendiente y lo reinicia.</summary>
    public int ConsumeExitCode()
    {
        var code = PendingExitCode;
        PendingExitCode = 0;
        return code;
    }
}
