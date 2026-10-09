namespace MiShell.Core.Contracts;

/// <summary>Define los colores semánticos del tema activo.</summary>
public interface IThemePalette
{
    string Name { get; }

    ConsoleColor Prompt { get; }

    ConsoleColor PromptSymbol { get; }

    ConsoleColor Path { get; }

    ConsoleColor Success { get; }

    ConsoleColor ErrorColor { get; }

    ConsoleColor Info { get; }

    ConsoleColor Accent { get; }

    ConsoleColor Muted { get; }
}

/// <summary>Punto único de salida hacia el usuario. Core nunca llama a la UI directamente.</summary>
public interface IOutputRenderer
{
    IThemePalette Theme { get; }

    void Write(string text, ConsoleColor? color = null);

    void WriteLine(string text = "", ConsoleColor? color = null);

    void WriteError(string message);

    void WriteSuccess(string message);

    void WriteInfo(string message);

    void ClearScreen();

    /// <summary>Muestra el banner de bienvenida del shell.</summary>
    void RenderBanner();

    /// <summary>Tabla de procesos: PID, nombre, CPU y RAM con barra proporcional.</summary>
    void RenderProcessTable(IEnumerable<ProcessInfo> processes);

    /// <summary>Gráfico de barras (RAM por proceso, uso de recursos...).</summary>
    void RenderBarChart(string title, IEnumerable<(string Label, double Value, string ColorHint)> items, string valueSuffix);

    /// <summary>Panel tipo neofetch con barras de uso de CPU/RAM/disco.</summary>
    void RenderSystemInfo(SystemUsage usage);

    /// <summary>Tabla genérica con encabezados.</summary>
    void RenderTable(string title, string[] columns, IEnumerable<string[]> rows);

    /// <summary>
    /// Ejecuta <paramref name="work"/> mostrando un indicador animado si la tarea
    /// tarda más de unos cientos de milisegundos. Core lo usa para comandos
    /// externos lentos sin conocer los detalles de la UI.
    /// </summary>
    Task<T> RunWithSpinnerAsync<T>(string label, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}

/// <summary>Instantánea de un proceso visible por el shell.</summary>
/// <param name="Pid">Identificador de proceso.</param>
/// <param name="Name">Nombre corto del ejecutable.</param>
/// <param name="CpuSeconds">Tiempo total de CPU consumido, en segundos.</param>
/// <param name="WorkingSetMb">Memoria de trabajo en MB.</param>
public sealed record ProcessInfo(int Pid, string Name, double CpuSeconds, double WorkingSetMb);

/// <summary>Uso global de recursos del sistema.</summary>
/// <param name="CpuPercent">0–100.</param>
/// <param name="RamUsedMb">RAM en uso.</param>
/// <param name="RamTotalMb">RAM total.</param>
/// <param name="DiskUsedGb">Disco ocupado en la unidad del usuario.</param>
/// <param name="DiskTotalGb">Tamaño total de la unidad.</param>
public sealed record SystemUsage(double CpuPercent, double RamUsedMb, double RamTotalMb, double DiskUsedGb, double DiskTotalGb);
