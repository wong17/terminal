using MiShell.Core.Jobs;

namespace MiShell.Core.Tests;

public sealed class JobTableTests
{
    [Fact]
    public void MissingJobIsReportedWithoutThrowing()
    {
        var table = new JobTable();

        var found = table.TryGetByPrefix("999", out var job);

        Assert.False(found);
        Assert.Null(job);
    }
}
