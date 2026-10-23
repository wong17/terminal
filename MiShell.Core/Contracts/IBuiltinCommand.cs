namespace MiShell.Core.Contracts;

/// <summary>Resultado de ejecutar un comando interno.</summary>
/// <param name="ExitCode">Código de salida (0 = éxito).</param>
public sealed record CommandResult(int ExitCode)
{
    /// <summary>Resultado exitoso (código 0).</summary>
    public static CommandResult Ok { get; } = new(0);

    /// <summary>Resultado de fallo genérico (código 1).</summary>
    public static CommandResult Failure { get; } = new(1);
}

/// <summary>Contexto de ejecución compartido por los comandos internos.</summary>
public interface ICommandContext
{
    /// <summary>Directorio de trabajo actual del shell.</summary>
    string WorkingDirectory { get; }

    /// <summary>Directorio en el que estaba el shell antes del último cd (para cd -).</summary>
    string? PreviousDirectory { get; }

    /// <summary>Cambia el directorio de trabajo del shell.</summary>
    void ChangeDirectory(string path);

    /// <summary>Variables de entorno del shell (incluye las del proceso).</summary>
    IShellEnvironment Environment { get; }

    /// <summary>Historial de comandos.</summary>
    IReadOnlyList<string> History { get; }

    /// <summary>Solicita al shell que termine su bucle principal.</summary>
    void RequestExit(int exitCode);

    /// <summary>Tabla de procesos en segundo plano.</summary>
    Jobs.IJobTable Jobs { get; }

    /// <summary>Muestra la creación, espera y fin de cada proceso externo.</summary>
    bool TraceProcesses { get; set; }
}

/// <summary>Un comando interno del shell (cd, pwd, ps, theme...).</summary>
public interface IBuiltinCommand
{
    /// <summary>Nombre con el que se invoca el comando (ej. "cd").</summary>
    string Name { get; }

    /// <summary>Descripción corta para <c>help</c>.</summary>
    string Description { get; }

    /// <summary>Sintaxis de uso mostrada en <c>help</c>.</summary>
    string Usage => Name;

    bool AcceptsInput => false;

    Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken);
}
