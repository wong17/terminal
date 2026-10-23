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

    /// <summary>Color de la hora en el prompt, el banner y los monitores.</summary>
    ConsoleColor Time { get; }

    /// <summary>Color de <c>usuario@equipo</c>.</summary>
    ConsoleColor Host { get; }

    /// <summary>Color de las etiquetas secundarias (por ejemplo, el nombre del tema en el banner).</summary>
    ConsoleColor Tag { get; }

    /// <summary>Color de los nombres de comandos (en <c>help</c> y al escribir).</summary>
    ConsoleColor Command { get; }

    /// <summary>Color de los directorios en los listados.</summary>
    ConsoleColor Directory { get; }
}

/// <summary>Punto único de salida hacia el usuario. Core nunca llama a la UI directamente.</summary>
public interface IOutputRenderer
{
    IThemePalette Theme { get; }

    IDisposable RedirectOutput(TextWriter writer);

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

    /// <summary>
    /// Tabla genérica con encabezados. Según el estilo de salida se dibuja con columnas alineadas o con bordes;
    /// con la salida redirigida se escribe como texto separado por tabuladores.
    /// </summary>
    /// <param name="title">Línea que encabeza la tabla (puede ir vacía).</param>
    /// <param name="columns">Nombres de las columnas.</param>
    /// <param name="rows">Filas; cada una con una celda por columna.</param>
    /// <param name="rowColor">Opcional: devuelve el color con que se pinta cada fila (por ejemplo, directorios en el acento del tema).</param>
    /// <param name="cellColor">Opcional: color de una celda concreta (fila, índice de columna); tiene prioridad sobre <paramref name="rowColor"/>.</param>
    void RenderTable(string title, string[] columns, IEnumerable<string[]> rows,
        Func<string[], ConsoleColor?>? rowColor = null, Func<string[], int, ConsoleColor?>? cellColor = null);

    /// <summary>Lista de pares "etiqueta : valor" alineados, como Format-List de PowerShell.</summary>
    void RenderList(string title, IEnumerable<(string Label, string Value)> items);

    /// <summary>Estilo de tablas y listas: <c>classic</c> (columnas alineadas) o <c>boxed</c> (con bordes).</summary>
    string OutputStyle { get; set; }

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
/// <param name="Handles">Identificadores abiertos (columna Handles de Get-Process).</param>
/// <param name="Threads">Número de hilos.</param>
/// <param name="SessionId">Sesión de Windows (columna SI).</param>
/// <param name="PrivateMb">Memoria privada en MB (columna PM).</param>
/// <param name="StartTime">Momento de inicio, si se puede leer.</param>
public sealed record ProcessInfo(int Pid, string Name, double CpuSeconds, double WorkingSetMb,
    int Handles = 0, int Threads = 0, int SessionId = 0, double PrivateMb = 0, DateTime? StartTime = null);

/// <summary>Uso global de recursos del sistema.</summary>
/// <param name="CpuPercent">0–100.</param>
/// <param name="RamUsedMb">RAM en uso.</param>
/// <param name="RamTotalMb">RAM total.</param>
/// <param name="DiskUsedGb">Disco ocupado en la unidad del usuario.</param>
/// <param name="DiskTotalGb">Tamaño total de la unidad.</param>
public sealed record SystemUsage(double CpuPercent, double RamUsedMb, double RamTotalMb, double DiskUsedGb, double DiskTotalGb);
