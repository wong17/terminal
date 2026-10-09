using System.Diagnostics;

namespace MiShell.Core.Jobs;

/// <summary>La tabla conserva todos los procesos y flujos hasta completar la tubería.</summary>
public sealed class BackgroundJob(int jobId, IReadOnlyList<Process> processes, string commandLine, Task<int> completion) : IDisposable
{
    public int JobId { get; } = jobId;
    public IReadOnlyList<Process> Processes { get; } = processes;
    public Process Process => Processes[^1];
    public string CommandLine { get; } = commandLine;
    public Task<int> Completion { get; } = completion;
    public bool HasExited => Completion.IsCompleted;

    public void Dispose()
    {
        foreach (var process in Processes) process.Dispose();
    }
}

public interface IJobTable
{
    IReadOnlyList<BackgroundJob> Jobs { get; }
    BackgroundJob Add(Process process, string commandLine);
    BackgroundJob Add(IReadOnlyList<Process> processes, string commandLine, Task<int> completion);
    BackgroundJob? RemoveFirstCompleted();
    bool TryGetByPrefix(string input, out BackgroundJob? job);
    void Remove(BackgroundJob job);
    void Kill(BackgroundJob job);
}
