using MiShell.Core.Contracts;
using MiShell.UI.Rendering;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>
/// <c>top</c>: monitor en vivo que se redibuja cada segundo en la pantalla alternativa del terminal
/// (como top o htop). Muestra barras de CPU y RAM y los procesos con más memoria, con su porcentaje de CPU
/// calculado entre dos lecturas. Sale con q, Esc o Ctrl+C. Con <c>--once</c> imprime un solo cuadro.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class TopCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <inheritdoc />
    public string Name => "top";

    /// <inheritdoc />
    public string Description => "Monitor en vivo de CPU, RAM y procesos; q para salir.";

    /// <inheritdoc />
    public string Usage => "top [--count N] [--every S] [--once]";

    /// <summary>Lectura anterior de CPU de un proceso, para calcular su porcentaje entre dos cuadros.</summary>
    private readonly Dictionary<int, TimeSpan> _previousCpu = [];

    /// <inheritdoc />
    public async Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var count = 10;
        var every = 1.0;
        var once = false;
        for (var i = 0; i < arguments.Length; i++)
        {
            switch (arguments[i])
            {
                case "--once": once = true; break;
                case "--count" when i + 1 < arguments.Length && int.TryParse(arguments[i + 1], out var n) && n > 0: count = n; i++; break;
                case "--every" when i + 1 < arguments.Length && double.TryParse(arguments[i + 1], NumberStyles.Float, Invariant, out var s) && s > 0.1: every = s; i++; break;
                default:
                    renderer.WriteError($"Uso: {Usage}");
                    return CommandResult.Failure;
            }
        }

        try
        {
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
            using var freeRam = new PerformanceCounter("Memory", "Available MBytes", readOnly: true);
            _ = cpuCounter.NextValue();
            var interactive = !once && !Console.IsInputRedirected && !Console.IsOutputRedirected;
            if (!interactive)
            {
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                foreach (var line in BuildFrame(cpuCounter.NextValue(), freeRam.NextValue(), count, 0.3, false)) renderer.WriteLine(line);
                return CommandResult.Ok;
            }

            await RunLiveAsync(cpuCounter, freeRam, count, every, cancellationToken).ConfigureAwait(false);
            return CommandResult.Ok;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            renderer.WriteError($"No se pudieron leer los contadores de Windows: {ex.Message}");
            return CommandResult.Failure;
        }
    }

    /// <summary>Redibuja el cuadro cada <paramref name="every"/> segundos hasta que el usuario pulse q o Esc.</summary>
    private async Task RunLiveAsync(PerformanceCounter cpu, PerformanceCounter freeRam, int count, double every, CancellationToken token)
    {
        Console.Write("\e[?1049h\e[?25l");
        try
        {
            var clock = Stopwatch.StartNew();
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(every), token).ConfigureAwait(false);
                var frame = new StringBuilder("\e[H");
                foreach (var line in BuildFrame(cpu.NextValue(), freeRam.NextValue(), count, clock.Elapsed.TotalSeconds, true))
                    frame.Append(line).Append("\e[K\n");
                frame.Append("\e[J");
                Console.Write(frame);
                clock.Restart();
                while (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key is ConsoleKey.Q or ConsoleKey.Escape || key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                        return;
                }
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

    /// <summary>Arma las líneas de un cuadro: encabezado, barras de CPU y RAM y la tabla de procesos.</summary>
    private List<string> BuildFrame(float cpuPercent, float freeRamMb, int count, double elapsedSeconds, bool color)
    {
        var theme = renderer.Theme;
        string Paint(string text, ConsoleColor c) => color ? AnsiColors.For(c) + text + "\e[0m" : text;
        var totalRam = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1048576.0;
        var usedRam = Math.Max(0, totalRam - freeRamMb);
        var lines = new List<string>
        {
            Paint($"  top · {MiShell.Core.ClockFormat.HourWithSeconds(DateTime.Now)} · {Environment.ProcessorCount} procesadores · q o Esc para salir", theme.Accent),
            string.Empty,
            $"  CPU  {Bar(cpuPercent / 100, 30, Paint)} {cpuPercent,5:F1}%",
            $"  RAM  {Bar(usedRam / Math.Max(1, totalRam), 30, Paint)} {usedRam:F0} / {totalRam:F0} MiB",
            string.Empty,
            Paint($"  {"PID",7}  {"Nombre",-24} {"CPU %",6} {"RAM (MiB)",10} {"Hilos",6}", theme.Accent),
        };

        var rows = new List<(int Pid, string Name, double Cpu, double Ram, int Threads)>();
        var current = new Dictionary<int, TimeSpan>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var total = process.TotalProcessorTime;
                    current[process.Id] = total;
                    var delta = _previousCpu.TryGetValue(process.Id, out var before) ? (total - before).TotalSeconds : 0;
                    var percent = elapsedSeconds > 0 ? delta / elapsedSeconds / Environment.ProcessorCount * 100 : 0;
                    rows.Add((process.Id, process.ProcessName, percent, process.WorkingSet64 / 1048576.0, process.Threads.Count));
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Proceso protegido o ya terminado: se omite.
                }
            }
        }

        _previousCpu.Clear();
        foreach (var (pid, total) in current) _previousCpu[pid] = total;
        foreach (var (pid, name, cpu, ram, threads) in rows.OrderByDescending(r => r.Ram).Take(count))
        {
            var shown = name.Length > 24 ? name[..23] + "…" : name;
            lines.Add($"  {pid,7}  {shown,-24} {cpu,6:F1} {ram,10:F0} {threads,6}");
        }

        return lines;
    }

    /// <summary>Barra de bloques cuyo color pasa de verde a amarillo y rojo según la carga.</summary>
    private static string Bar(double ratio, int width, Func<string, ConsoleColor, string> paint)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        var filled = (int)Math.Round(ratio * width);
        var color = ratio > 0.8 ? ConsoleColor.Red : ratio > 0.5 ? ConsoleColor.Yellow : ConsoleColor.Green;
        return paint(new string('█', filled), color) + new string('░', width - filled);
    }
}
