using System.Diagnostics;

namespace MiShell.Core.Jobs;

/// <summary>Un proceso lanzado en segundo plano con <c>&amp;</c>.</summary>
public sealed record BackgroundJob(int JobId, Process Process, string CommandLine)
{
    public bool HasExited => Process.HasExited;
}

/// <summary>Registro de trabajos en segundo plano (equivalente conceptual de la job table de un SO).</summary>
public interface IJobTable
{
    IReadOnlyList<BackgroundJob> Jobs { get; }

    BackgroundJob Add(Process process, string commandLine);

    /// <summary>Quita y devuelve el job cuyo proceso ya terminó; null si no hay ninguno finalizado.</summary>
    BackgroundJob? RemoveFirstCompleted();

    bool TryGetByPrefix(string input, out BackgroundJob? job);

    void Kill(BackgroundJob job);
}
