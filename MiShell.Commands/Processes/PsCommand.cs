using MiShell.Core;
using MiShell.Core.Contracts;
using MiShell.Core.Jobs;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>
/// <c>ps</c>: lista todos los procesos que Windows permite leer (PID, estado, hilos, CPU, memoria con unidad, inicio) y con
/// <c>--id</c> detalla uno. Un dato que el sistema no deja leer se muestra como <c>-</c> en vez de ocultar el proceso.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class PsCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "ps";
    public string Description => "Lista procesos con estado, CPU y memoria; --id muestra el detalle de uno.";
    public string Usage => "ps [n | --all] [--chart] [--group] [-l] [--name texto] [--sort ram|cpu|pid] [--id PID]";

    private sealed record Row(Process Process, ProcessInfo Info);

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var count = 15;
        var chart = false;
        var group = false;
        var detailed = false;
        var sort = "ram";
        string? filter = null;
        int? id = null;
        var sawCount = false;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            switch (argument)
            {
                case "--chart": chart = true; break;
                case "--group": group = true; break;
                case "-l" or "--long": detailed = true; break;
                case "--all": count = int.MaxValue; sawCount = true; break;
                case "--name" when i + 1 < arguments.Length: filter = arguments[++i]; break;
                case "--id" when i + 1 < arguments.Length && int.TryParse(arguments[i + 1], out var pid) && pid > 0: id = pid; i++; break;
                case "--sort" when i + 1 < arguments.Length:
                    sort = arguments[++i].ToLowerInvariant();
                    if (sort is not "ram" and not "cpu" and not "pid") return InvalidUsage();
                    break;
                default:
                    if (sawCount || !int.TryParse(argument, out count) || count < 1) return InvalidUsage();
                    sawCount = true;
                    break;
            }
        }

        if (id is { } wanted) return Task.FromResult(Detail(wanted));

        var totalRamMb = Math.Max(1, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1048576.0);
        var rows = new List<Row>();
        foreach (var process in Process.GetProcesses())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Safe(() => process.ProcessName, process.Id.ToString(CultureInfo.InvariantCulture));
            if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) { process.Dispose(); continue; }
            var workingSet = Safe(() => process.WorkingSet64 / 1048576.0, 0);
            rows.Add(new Row(process, new ProcessInfo(
                process.Id, name,
                Safe(() => process.TotalProcessorTime.TotalSeconds, -1),
                workingSet,
                Safe(() => process.HandleCount, 0),
                Safe(() => process.Threads.Count, 0),
                Safe(() => process.SessionId, 0),
                Safe(() => process.PrivateMemorySize64 / 1048576.0, 0),
                Safe<DateTime?>(() => process.StartTime, null),
                ProcessStateReader.Unknown,
                workingSet / totalRamMb * 100)));
        }

        try
        {
            var items = group ? Group(rows) : [.. rows.Select(r => r.Info)];
            var ordered = sort switch
            {
                "cpu" => items.OrderByDescending(p => p.CpuSeconds),
                "pid" => items.OrderBy(p => p.Pid),
                _ => items.OrderByDescending(p => p.WorkingSetMb),
            };
            var shown = ordered.Take(count).ToList();
            if (!group)
            {
                // El estado se lee solo para lo que se muestra: recorrer los hilos de cientos de procesos es lo más lento.
                var processes = rows.ToDictionary(r => r.Info.Pid, r => r.Process);
                shown = [.. shown.Select(p => p with { State = ProcessStateReader.Read(processes[p.Pid]) })];
            }

            if (chart)
            {
                var unit = group ? "programa" : "proceso";
                var title = group ? $"Memoria por programa: {shown.Count} con más RAM"
                    : sort == "ram" ? $"Memoria: {shown.Count} procesos con más RAM" : $"Memoria de {shown.Count} procesos (orden: {sort})";
                renderer.RenderBarChart(title,
                    shown.Select(p => (group ? p.Name : $"{p.Name} ({p.Pid})", Math.Round(p.WorkingSetMb, 1),
                        p.WorkingSetMb >= 1024 ? "red" : p.WorkingSetMb >= 500 ? "yellow" : "cyan")),
                    unit, ByteFormat.FromMegabytes);
            }
            else renderer.RenderProcessTable(shown, detailed);

            var restricted = shown.Count(p => p.CpuSeconds < 0);
            renderer.WriteLine($"{shown.Count} de {items.Count} {(group ? "programas" : "procesos")} · orden: {(sort == "ram" ? "memoria" : sort == "cpu" ? "CPU" : "PID")}", renderer.Theme.Muted);
            if (restricted > 0)
                renderer.WriteLine($"{restricted} de ellos son procesos protegidos del sistema (CPU e inicio no legibles); ejecuta MiShell como administrador para verlos.", renderer.Theme.Muted);
            return Task.FromResult(CommandResult.Ok);
        }
        finally
        {
            foreach (var row in rows) row.Process.Dispose();
        }

        Task<CommandResult> InvalidUsage()
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }
    }

    /// <summary>Suma los procesos de un mismo programa (brave ×12) para ver cuánto ocupa cada aplicación.</summary>
    private static List<ProcessInfo> Group(List<Row> rows) =>
    [
        .. rows.GroupBy(r => r.Info.Name, StringComparer.OrdinalIgnoreCase).Select(g =>
        {
            var members = g.Select(r => r.Info).ToList();
            var known = members.Where(m => m.CpuSeconds >= 0).ToList();
            var state = g.Select(r => ProcessStateReader.Read(r.Process)).Distinct().ToList();
            return new ProcessInfo(0, members.Count == 1 ? members[0].Name : $"{members[0].Name} ×{members.Count}",
                known.Count == 0 ? -1 : known.Sum(m => m.CpuSeconds), members.Sum(m => m.WorkingSetMb), members.Sum(m => m.Handles),
                members.Sum(m => m.Threads), members[0].SessionId, members.Sum(m => m.PrivateMb),
                members.Where(m => m.StartTime is not null).Select(m => m.StartTime).Min(),
                state.Contains(ProcessStateReader.Running) ? ProcessStateReader.Running
                    : state.Count == 1 ? state[0] : ProcessStateReader.Waiting,
                members.Sum(m => m.RamPercent));
        }),
    ];

    private static T Safe<T>(Func<T> read, T fallback)
    {
        try { return read(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { return fallback; }
    }

    private CommandResult Detail(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            var inv = CultureInfo.InvariantCulture;
            var cores = Safe(() => System.Numerics.BitOperations.PopCount((ulong)(long)p.ProcessorAffinity), 0);
            var rows = new List<(string, string)>
            {
                ("PID", p.Id.ToString(inv)),
                ("Nombre", p.ProcessName),
                ("Estado", ProcessStateReader.Read(p)),
                ("Ruta", Safe(() => p.MainModule?.FileName ?? "(desconocida)", "(sin acceso)")),
                ("Versión", Safe(() => p.MainModule?.FileVersionInfo.FileVersion ?? "-", "(sin acceso)")),
                ("Inicio", Safe(() => ClockFormat.Date(p.StartTime) + " " + ClockFormat.HourWithSeconds(p.StartTime), "(sin acceso)")),
                ("Sesión", Safe(() => p.SessionId.ToString(inv), "-")),
                ("Prioridad", Safe(() => p.PriorityClass.ToString(), "(sin acceso)")),
                ("Núcleos permitidos", cores == 0 ? "(sin acceso)" : $"{cores} de {Environment.ProcessorCount}"),
                ("Hilos", Safe(() => p.Threads.Count.ToString(inv), "-")),
                ("Handles", Safe(() => p.HandleCount.ToString(inv), "-")),
                ("CPU usuario", Safe(() => ByteFormat.Duration(p.UserProcessorTime), "(sin acceso)")),
                ("CPU sistema", Safe(() => ByteFormat.Duration(p.PrivilegedProcessorTime), "(sin acceso)")),
                ("Memoria de trabajo", ByteFormat.Human(p.WorkingSet64)),
                ("Pico de trabajo", ByteFormat.Human(p.PeakWorkingSet64)),
                ("Memoria privada", ByteFormat.Human(p.PrivateMemorySize64)),
                ("Memoria virtual", ByteFormat.Human(p.VirtualMemorySize64)),
                ("Responde", Safe(() => p.MainWindowHandle == IntPtr.Zero ? "(sin ventana)" : p.Responding ? "sí" : "no", "-")),
            };
            renderer.RenderList($"Proceso {pid}", rows);
            return CommandResult.Ok;
        }
        catch (ArgumentException)
        {
            renderer.WriteError($"No existe el proceso {pid}.");
            return CommandResult.Failure;
        }
    }
}
