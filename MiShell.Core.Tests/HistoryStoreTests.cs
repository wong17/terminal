using MiShell.Core.History;

namespace MiShell.Core.Tests;

public sealed class HistoryStoreTests
{
    [Fact]
    public void PersistsEntriesAcrossInstances()
    {
        var root = CreateTempDirectory();
        var path = Path.Combine(root, "history.json");

        try
        {
            using (var first = new HistoryStore(path))
            {
                first.Add("pwd");
                first.Add("echo hola");
            }

            using var second = new HistoryStore(path);
            Assert.Equal(["pwd", "echo hola"], second.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PersistenceFailureDoesNotTerminateTheSession()
    {
        var root = CreateTempDirectory();
        var path = Path.Combine(root, "history.json");
        Directory.CreateDirectory(path);

        try
        {
            using var history = new HistoryStore(path);
            history.Add("echo seguro");

            Assert.Equal(["echo seguro"], history.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mishell-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
