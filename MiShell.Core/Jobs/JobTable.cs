using System.ComponentModel;
using System.Diagnostics;

namespace MiShell.Core.Jobs;

public sealed class JobTable : IJobTable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly List<BackgroundJob> _jobs = [];
    private int _nextJobId = 1;

    public IReadOnlyList<BackgroundJob> Jobs
    {
        get { lock (_gate) return _jobs.ToArray(); }
    }

    public BackgroundJob Add(Process process, string commandLine) =>
        Add([process], commandLine, WaitAsync(process));

    public BackgroundJob Add(IReadOnlyList<Process> processes, string commandLine, Task<int> completion)
    {
        lock (_gate)
        {
            var job = new BackgroundJob(_nextJobId++, processes, commandLine, completion);
            _jobs.Add(job);
            return job;
        }
    }

    private static async Task<int> WaitAsync(Process process)
    {
        await process.WaitForExitAsync().ConfigureAwait(false);
        return process.ExitCode;
    }

    public BackgroundJob? RemoveFirstCompleted()
    {
        lock (_gate)
        {
            var completed = _jobs.FirstOrDefault(j => j.HasExited);
            if (completed is not null) _jobs.Remove(completed);
            return completed;
        }
    }

    public bool TryGetByPrefix(string input, out BackgroundJob? job)
    {
        lock (_gate)
        {
            var key = input.TrimStart('%');
            if (string.IsNullOrWhiteSpace(key)) { job = null; return false; }
            job = _jobs.FirstOrDefault(j => int.TryParse(key, out var id)
                ? j.JobId == id
                : j.CommandLine.StartsWith(key, StringComparison.OrdinalIgnoreCase));
            return job is not null;
        }
    }

    public void Remove(BackgroundJob job)
    {
        lock (_gate) _jobs.Remove(job);
        job.Dispose();
    }

    public void Kill(BackgroundJob job)
    {
        foreach (var process in job.Processes)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* Terminó entre la consulta y Kill. */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var job in Jobs)
        {
            try
            {
                Kill(job);
                await job.Completion.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or OperationCanceledException)
            {
                // El cierre libera los handles incluso si un proceso terminó por otra vía.
            }
            finally { Remove(job); }
        }
    }
}
