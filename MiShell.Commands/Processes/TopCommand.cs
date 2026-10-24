using MiShell.Core;
using MiShell.Core.Contracts;
using MiShell.Core.Jobs;
using MiShell.UI.Rendering;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>
/// <c>top</c>: monitor en vivo que se redibuja en la pantalla alternativa del terminal (como top o htop). Muestra la carga de
/// cada núcleo, CPU y RAM con su historial, el número de procesos por estado y una tabla de procesos que llena la ventana.
/// Se ordena con c, m, p, n y e, se desplaza con las flechas y sale con q, Esc o Ctrl+C. Con <c>--once</c> imprime un
/// solo cuadro; con <c>--pid</c> o <c>--job</c> vigila un único proceso.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class TopCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const string Blocks = "▁▂▃▄▅▆▇█";
    private const int HistoryLength = 40;

    /// <inheritdoc />
    public string Name => "top";

    /// <inheritdoc />
    public string Description => "Monitor en vivo de núcleos, CPU, RAM y procesos; q para salir.";

    /// <inheritdoc />
    public string Usage => "top [--count N] [--every S] [--once] [--pid N | --job %N]";

    private sealed record Row(int Pid, string Name, string State, double CpuPercent, double Cores, int Threads, double RamMb);

    private sealed record Snapshot(float CpuTotal, float[] PerCore, double UsedRamMb, double TotalRamMb, List<Row> Rows, int Threads, int Running, int Waiting, int Suspended);

    /// <summary>Estado de la pantalla viva: orden, desplazamiento e historiales de las gráficas.</summary>
    private sealed class View
    {
        public string Sort = "mem";
        public int Offset;
        public readonly Queue<double> CpuHistory = new();
        public readonly Queue<double> RamHistory = new();
        public readonly Queue<double> ProcessCpuHistory = new();
        public readonly Queue<double> ProcessRamHistory = new();
    }

    private readonly Dictionary<int, TimeSpan> _previousCpu = [];
    private bool _color;

    /// <inheritdoc />
    public async Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        int? count = null;
        var every = 1.0;
        var once = false;
        int? pid = null;
        string? job = null;
        for (var i = 0; i < arguments.Length; i++)
        {
            switch (arguments[i])
            {
                case "--once": once = true; break;
                case "--count" when i + 1 < arguments.Length && int.TryParse(arguments[i + 1], out var n) && n > 0: count = n; i++; break;
                case "--every" when i + 1 < arguments.Length && double.TryParse(arguments[i + 1], NumberStyles.Float, Invariant, out var s) && s > 0.1: every = s; i++; break;
                case "--pid" when i + 1 < arguments.Length && int.TryParse(arguments[i + 1], out var p) && p > 0: pid = p; i++; break;
                case "--job" when i + 1 < arguments.Length: job = arguments[++i]; break;
                default:
                    renderer.WriteError($"Uso: {Usage}");
                    return CommandResult.Failure;
            }
        }

        if (job is not null)
        {
            if (!context.Jobs.TryGetByPrefix(job, out var found) || found is null)
            {
                renderer.WriteError($"No hay ningún trabajo que coincida con '{job}'.");
                return CommandResult.Failure;
            }

            pid = found.Process.Id;
        }

        try
        {
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
            using var freeRam = new PerformanceCounter("Memory", "Available MBytes", readOnly: true);
            var coreCounters = CreateCoreCounters();
            try
            {
                var interactive = !once && !Console.IsInputRedirected && !Console.IsOutputRedirected;
                _color = interactive;
                _ = cpuCounter.NextValue();
                foreach (var c in coreCounters) _ = c.NextValue();
                _ = Sample(cpuCounter, freeRam, coreCounters, 1);
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                if (!interactive)
                {
                    var snapshot = Sample(cpuCounter, freeRam, coreCounters, 0.3);
                    var frame = pid is { } one ? BuildProcessFrame(one, snapshot, new View(), 100, 40) : BuildFrame(snapshot, new View(), 100, count ?? 15);
                    foreach (var line in frame) renderer.WriteLine(line);
                    return pid is { } && !ProcessAlive(pid.Value) ? CommandResult.Failure : CommandResult.Ok;
                }

                await RunLiveAsync(cpuCounter, freeRam, coreCounters, count, every, pid, cancellationToken).ConfigureAwait(false);
                return CommandResult.Ok;
            }
            finally
            {
                foreach (var c in coreCounters) c.Dispose();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or UnauthorizedAccessException)
        {
            renderer.WriteError($"No se pudieron leer los contadores de Windows: {ex.Message}");
            return CommandResult.Failure;
        }
    }

    private static PerformanceCounter[] CreateCoreCounters()
    {
        try
        {
            var names = new PerformanceCounterCategory("Processor").GetInstanceNames()
                .Where(n => !n.EndsWith("_Total", StringComparison.Ordinal))
                .OrderBy(n => n.Length).ThenBy(n => n, StringComparer.Ordinal);
            return [.. names.Select(n => new PerformanceCounter("Processor", "% Processor Time", n, readOnly: true))];
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Redibuja cada <paramref name="every"/> segundos y al instante cuando se pulsa una tecla, hasta q o Esc.</summary>
    private async Task RunLiveAsync(PerformanceCounter cpu, PerformanceCounter freeRam, PerformanceCounter[] cores, int? count, double every, int? pid, CancellationToken token)
    {
        Console.Write("\e[?1049h\e[?25l");
        try
        {
            var view = new View();
            var clock = Stopwatch.StartNew();
            var snapshot = Sample(cpu, freeRam, cores, 0.3);
            Draw(snapshot, view, count, pid);
            var next = TimeSpan.FromSeconds(every);
            while (!token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key is ConsoleKey.Q or ConsoleKey.Escape || key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control)) return;
                    HandleKey(key.Key, view, snapshot.Rows.Count);
                    Draw(snapshot, view, count, pid);
                    continue;
                }

                if (clock.Elapsed >= next)
                {
                    var elapsed = clock.Elapsed.TotalSeconds - (next.TotalSeconds - every);
                    snapshot = Sample(cpu, freeRam, cores, Math.Max(0.1, elapsed));
                    next += TimeSpan.FromSeconds(every);
                    Draw(snapshot, view, count, pid);
                }

                await Task.Delay(40, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C: se sale del monitor y la sesión continúa.
        }
        finally
        {
            Console.Write("\e[?25h\e[?1049l");
        }
    }

    private static void HandleKey(ConsoleKey key, View view, int rows)
    {
        switch (key)
        {
            case ConsoleKey.UpArrow: view.Offset = Math.Max(0, view.Offset - 1); break;
            case ConsoleKey.DownArrow: view.Offset = Math.Min(Math.Max(0, rows - 1), view.Offset + 1); break;
            case ConsoleKey.PageUp: view.Offset = Math.Max(0, view.Offset - 10); break;
            case ConsoleKey.PageDown: view.Offset = Math.Min(Math.Max(0, rows - 1), view.Offset + 10); break;
            case ConsoleKey.Home: view.Offset = 0; break;
            case ConsoleKey.C: view.Sort = "cpu"; view.Offset = 0; break;
            case ConsoleKey.M: view.Sort = "mem"; view.Offset = 0; break;
            case ConsoleKey.P: view.Sort = "pid"; view.Offset = 0; break;
            case ConsoleKey.N: view.Sort = "name"; view.Offset = 0; break;
            case ConsoleKey.E: view.Sort = "state"; view.Offset = 0; break;
        }
    }

    private void Draw(Snapshot snapshot, View view, int? count, int? pid)
    {
        int width, height;
        try { width = Console.WindowWidth; height = Console.WindowHeight; }
        catch (IOException) { width = 100; height = 30; }
        var lines = pid is { } one ? BuildProcessFrame(one, snapshot, view, width, height) : BuildFrame(snapshot, view, width, count ?? int.MaxValue, height);
        var frame = new StringBuilder("\e[H");
        foreach (var line in lines.Take(height)) frame.Append(Clip(line, width - 1)).Append("\e[K\n");
        frame.Append("\e[J");
        Console.Write(frame);
    }

    /// <summary>Lee contadores y procesos; el porcentaje de CPU de cada proceso sale de la diferencia con la lectura anterior.</summary>
    private Snapshot Sample(PerformanceCounter cpu, PerformanceCounter freeRam, PerformanceCounter[] cores, double elapsedSeconds)
    {
        var totalRam = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1048576.0;
        var rows = new List<Row>();
        var current = new Dictionary<int, TimeSpan>();
        int threads = 0, running = 0, waiting = 0, suspended = 0;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                var state = ProcessStateReader.Read(process);
                var processThreads = Safe(() => process.Threads.Count, 0);
                threads += processThreads;
                switch (state)
                {
                    case ProcessStateReader.Running: running++; break;
                    case ProcessStateReader.Suspended: suspended++; break;
                    default: waiting++; break;
                }

                double delta = 0;
                try
                {
                    var total = process.TotalProcessorTime;
                    current[process.Id] = total;
                    if (_previousCpu.TryGetValue(process.Id, out var before)) delta = Math.Max(0, (total - before).TotalSeconds);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Proceso protegido: se lista igual, sin CPU.
                }

                var cpuCores = elapsedSeconds > 0 ? delta / elapsedSeconds : 0;
                rows.Add(new Row(process.Id, Safe(() => process.ProcessName, "?"), state,
                    cpuCores / Environment.ProcessorCount * 100, cpuCores, processThreads, Safe(() => process.WorkingSet64 / 1048576.0, 0)));
            }
        }

        _previousCpu.Clear();
        foreach (var (id, total) in current) _previousCpu[id] = total;
        return new Snapshot(cpu.NextValue(), [.. cores.Select(c => Math.Clamp(c.NextValue(), 0, 100))],
            Math.Max(0, totalRam - freeRam.NextValue()), totalRam, rows, threads, running, waiting, suspended);
    }

    private static T Safe<T>(Func<T> read, T fallback)
    {
        try { return read(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { return fallback; }
    }

    private static bool ProcessAlive(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception) { return false; }
    }

    /// <summary>Arma el cuadro general: encabezado, núcleos, CPU y RAM con historial, contadores y tabla de procesos.</summary>
    private List<string> BuildFrame(Snapshot s, View view, int width, int count, int height = 0)
    {
        var theme = renderer.Theme;
        string Paint(string text, ConsoleColor c) => _color ? AnsiColors.For(c) + text + "\e[0m" : text;
        Push(view.CpuHistory, s.CpuTotal);
        var ramRatio = s.UsedRamMb / Math.Max(1, s.TotalRamMb);
        Push(view.RamHistory, ramRatio * 100);
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var lines = new List<string>
        {
            Paint($"  top · {ClockFormat.HourWithSeconds(DateTime.Now)} · {Environment.ProcessorCount} procesadores · encendido {ByteFormat.Duration(uptime)} · q o Esc para salir", theme.Accent),
            string.Empty,
        };

        // Un bloque por núcleo, en columnas.
        if (s.PerCore.Length > 0)
        {
            const int cell = 20;
            var perRow = Math.Clamp((width - 2) / cell, 1, 4);
            for (var i = 0; i < s.PerCore.Length; i += perRow)
            {
                var line = new StringBuilder("  ");
                for (var j = i; j < Math.Min(s.PerCore.Length, i + perRow); j++)
                    line.Append(Paint($"{j,2} ", theme.Muted)).Append(Bar(s.PerCore[j] / 100, 8, Paint)).Append(" " + s.PerCore[j].ToString("F0", Invariant).PadLeft(3) + "%  ");
                lines.Add(line.ToString());
            }

            lines.Add(string.Empty);
        }

        lines.Add($"  CPU  {Bar(s.CpuTotal / 100, 30, Paint)} {s.CpuTotal,5:F1}%  {Spark(view.CpuHistory, Paint, byLevel: true)}");
        lines.Add($"  RAM  {Bar(ramRatio, 30, Paint)} {ByteFormat.FromMegabytes(s.UsedRamMb)} / {ByteFormat.FromMegabytes(s.TotalRamMb)} ({ramRatio * 100:F0}%)  {Spark(view.RamHistory, Paint, byLevel: false)}");
        lines.Add($"  Procesos {s.Rows.Count} · Hilos {s.Threads} · " +
            Paint($"Ejecutando {s.Running}", theme.Success) + " · " + $"En espera {s.Waiting} · " + Paint($"Suspendidos {s.Suspended}", s.Suspended > 0 ? theme.Info : theme.Muted));
        lines.Add(string.Empty);

        var nameWidth = Math.Clamp(width - 78, 12, 26);
        lines.Add(Paint($"  {"PID",7}  {"Nombre".PadRight(nameWidth)} {"Estado",-10} {"CPU %",6} {"Núcleos",7} {"Hilos",6} {"Memoria",10} {"% RAM",6}  Barra", theme.Accent));
        var ordered = view.Sort switch
        {
            "cpu" => s.Rows.OrderByDescending(r => r.CpuPercent).ThenByDescending(r => r.RamMb),
            "pid" => s.Rows.OrderBy(r => r.Pid),
            "name" => s.Rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
            "state" => s.Rows.OrderBy(r => r.State == ProcessStateReader.Running ? 0 : r.State == ProcessStateReader.Suspended ? 1 : 2).ThenByDescending(r => r.RamMb),
            _ => s.Rows.OrderByDescending(r => r.RamMb),
        };
        var available = height > 0 ? Math.Max(3, height - lines.Count - 2) : int.MaxValue;
        var maxRam = Math.Max(1, s.Rows.Count == 0 ? 1 : s.Rows.Max(r => r.RamMb));
        foreach (var row in ordered.Skip(view.Offset).Take(Math.Min(count, available)))
        {
            var shown = row.Name.Length > nameWidth ? row.Name[..(nameWidth - 1)] + "…" : row.Name;
            var stateColor = row.State == ProcessStateReader.Running ? theme.Success : row.State == ProcessStateReader.Suspended ? theme.Info : theme.Muted;
            var memory = ByteFormat.FromMegabytes(row.RamMb);
            lines.Add($"  {Paint($"{row.Pid,7}", theme.Muted)}  {Paint(shown.PadRight(nameWidth), theme.Command)} {Paint($"{row.State,-10}", stateColor)} {row.CpuPercent,6:F1} {row.Cores,7:F2} {row.Threads,6} {memory,10} {row.RamMb / Math.Max(1, s.TotalRamMb) * 100,6:F1}  {Paint(MiniBar(row.RamMb / maxRam, 10), theme.Accent)}");
        }

        if (height > 0)
        {
            while (lines.Count < height - 1) lines.Add(string.Empty);
            lines.Add(Paint($"  ↑ ↓ PgUp PgDn desplazar · ordenar: c CPU · m memoria · p PID · n nombre · e estado   (orden actual: {view.Sort}, desde la fila {view.Offset + 1})", theme.Muted));
        }

        return lines;
    }

    /// <summary>Cuadro de un solo proceso: estado, CPU y memoria con historial, hilos y núcleos permitidos.</summary>
    private List<string> BuildProcessFrame(int pid, Snapshot s, View view, int width, int height)
    {
        var theme = renderer.Theme;
        string Paint(string text, ConsoleColor c) => _color ? AnsiColors.For(c) + text + "\e[0m" : text;
        var row = s.Rows.FirstOrDefault(r => r.Pid == pid);
        if (row is null)
            return [Paint($"  top · el proceso {pid} ya no existe", theme.ErrorColor)];
        Push(view.ProcessCpuHistory, Math.Min(100, row.Cores * 100));
        Push(view.ProcessRamHistory, row.RamMb / Math.Max(1, s.TotalRamMb) * 100);
        var lines = new List<string>
        {
            Paint($"  top · proceso {pid} ({row.Name}) · {ClockFormat.HourWithSeconds(DateTime.Now)} · q o Esc para salir", theme.Accent),
            string.Empty,
            $"  Estado    {Paint(row.State, row.State == ProcessStateReader.Running ? theme.Success : row.State == ProcessStateReader.Suspended ? theme.Info : theme.Muted)}",
            $"  CPU       {Bar(Math.Min(1, row.Cores), 30, Paint)} {row.CpuPercent,5:F1}% del equipo · {row.Cores:F2} núcleos  {Spark(view.ProcessCpuHistory, Paint, byLevel: true)}",
            $"  Memoria   {Bar(row.RamMb / Math.Max(1, s.TotalRamMb), 30, Paint)} {ByteFormat.FromMegabytes(row.RamMb)} ({row.RamMb / Math.Max(1, s.TotalRamMb) * 100:F1}% de la RAM)  {Spark(view.ProcessRamHistory, Paint, byLevel: false)}",
        };
        try
        {
            using var process = Process.GetProcessById(pid);
            lines.Add($"  Núcleos permitidos  {AffinityText(process)}");
            lines.Add($"  Hilos     {process.Threads.Count} · Handles {process.HandleCount} · Prioridad {Safe(() => process.PriorityClass.ToString(), "-")}");
            lines.Add(string.Empty);
            lines.Add(Paint($"  {"Hilo",7}  {"Estado",-12} {"Motivo de espera",-18} {"Prioridad",-14} {"CPU",10}", theme.Accent));
            foreach (ProcessThread thread in process.Threads.Cast<ProcessThread>().OrderByDescending(t => Safe(() => t.TotalProcessorTime, TimeSpan.Zero)).Take(Math.Max(3, height - lines.Count - 2)))
            {
                var state = Safe(() => thread.ThreadState.ToString(), "-");
                var reason = state == "Wait" ? Safe(() => thread.WaitReason.ToString(), "-") : string.Empty;
                lines.Add($"  {thread.Id,7}  {state,-12} {reason,-18} {Safe(() => thread.PriorityLevel.ToString(), "-"),-14} {ByteFormat.Duration(Safe(() => thread.TotalProcessorTime, TimeSpan.Zero)),10}");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            lines.Add(Paint("  (el proceso terminó o no se pueden leer sus hilos)", theme.Muted));
        }

        return lines;
    }

    private static string AffinityText(Process process)
    {
        try
        {
            var mask = (ulong)(long)process.ProcessorAffinity;
            var allowed = Enumerable.Range(0, Math.Min(64, Environment.ProcessorCount)).Where(i => (mask >> i & 1) == 1).ToArray();
            return allowed.Length == Environment.ProcessorCount ? $"todos ({allowed.Length})" : $"{allowed.Length} de {Environment.ProcessorCount}: {string.Join(", ", allowed)}";
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { return "(sin acceso)"; }
    }

    private static void Push(Queue<double> history, double value)
    {
        history.Enqueue(Math.Clamp(value, 0, 100));
        while (history.Count > HistoryLength) _ = history.Dequeue();
    }

    /// <summary>Gráfica de las últimas lecturas con bloques de altura creciente (▁ a █).</summary>
    private static string Spark(Queue<double> history, Func<string, ConsoleColor, string> paint, bool byLevel)
    {
        var builder = new StringBuilder();
        foreach (var v in history)
        {
            var block = Blocks[(int)Math.Min(Blocks.Length - 1, v / 100 * (Blocks.Length - 1) + 0.5)].ToString();
            builder.Append(paint(block, byLevel ? (v > 80 ? ConsoleColor.Red : v > 50 ? ConsoleColor.Yellow : ConsoleColor.Green) : ConsoleColor.Cyan));
        }

        return builder.ToString();
    }

    /// <summary>Barra de bloques cuyo color pasa de verde a amarillo y rojo según la carga.</summary>
    private static string Bar(double ratio, int width, Func<string, ConsoleColor, string> paint)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        var filled = (int)Math.Round(ratio * width);
        var color = ratio > 0.8 ? ConsoleColor.Red : ratio > 0.5 ? ConsoleColor.Yellow : ConsoleColor.Green;
        return paint(new string('█', filled), color) + new string('░', width - filled);
    }

    private static string MiniBar(double ratio, int width)
    {
        var filled = (int)Math.Round(Math.Clamp(ratio, 0, 1) * width);
        return new string('█', filled) + new string('░', width - filled);
    }

    /// <summary>Recorta una línea al ancho visible de la ventana sin cortar a la mitad una secuencia de color.</summary>
    private static string Clip(string line, int max)
    {
        var builder = new StringBuilder();
        var visible = 0;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '\e')
            {
                var end = line.IndexOf('m', i);
                if (end < 0) break;
                builder.Append(line, i, end - i + 1);
                i = end;
                continue;
            }

            if (visible >= max) { builder.Append("\e[0m"); break; }
            builder.Append(line[i]);
            visible++;
        }

        return builder.ToString();
    }
}
