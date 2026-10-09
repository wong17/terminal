using System.Diagnostics;

namespace MiShell.Core.Jobs;

public sealed class JobTable : IJobTable
{
    private readonly Lock _gate = new();
    private readonly List<BackgroundJob> _jobs = [];
    private int _nextJobId = 1;

    public IReadOnlyList<BackgroundJob> Jobs
    {
        get
        {
            lock (_gate)
            {
                return _jobs.ToArray();
            }
        }
    }

    public BackgroundJob Add(Process process, string commandLine)
    {
        lock (_gate)
        {
            var job = new BackgroundJob(_nextJobId++, process, commandLine);
            _jobs.Add(job);
            return job;
        }
    }

    public BackgroundJob? RemoveFirstCompleted()
    {
        lock (_gate)
        {
            var completed = _jobs.FirstOrDefault(j => j.HasExited);
            if (completed is not null)
            {
                _jobs.Remove(completed);
            }

            return completed;
        }
    }

    public bool TryGetByPrefix(string input, out BackgroundJob? job)
    {
        lock (_gate)
        {
            job = _jobs.FirstOrDefault(j => int.TryParse(input, out var id)
                ? j.JobId == id
                : j.CommandLine.StartsWith(input, StringComparison.OrdinalIgnoreCase));
            return job is not null;
        }
    }

    public void Kill(BackgroundJob job)
    {
        lock (_gate)
        {
            _jobs.Remove(job);
        }

        if (!job.Process.HasExited)
        {
            job.Process.Kill(entireProcessTree: true);
        }
    }
}
