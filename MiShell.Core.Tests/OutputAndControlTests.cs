using MiShell.Commands.BuiltinCommands;
using MiShell.Core.Contracts;
using MiShell.Core.Execution;
using MiShell.Core.Jobs;

namespace MiShell.Core.Tests;

/// <summary>Formatos de salida, borrado, mensajes de redirección, ayuda completa y control de procesos.</summary>
public sealed class OutputAndControlTests
{
    private static async Task<(int Code, string Output)> Run(ExecutionFixture f, IBuiltinCommand command, params string[] args)
    {
        f.Output.GetStringBuilder().Clear();
        var result = await command.ExecuteAsync(args, f.State, CancellationToken.None);
        return (result.ExitCode, f.Output.ToString());
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(26, "26 B")]
    [InlineData(2048, "2.0 KB")]
    [InlineData(1572864, "1.5 MB")]
    [InlineData(3221225472, "3.0 GB")]
    public void ByteFormatAlwaysShowsAUnit(long bytes, string expected) => Assert.Equal(expected, ByteFormat.Human(bytes));

    [Fact]
    public void DatesUseDayMonthYearAndTwelveHourClock()
    {
        var value = new DateTime(2026, 10, 22, 21, 5, 9);
        Assert.Equal("22/10/2026", ClockFormat.Date(value));
        Assert.Equal("22/10/2026 09:05 PM", ClockFormat.DateAndTime(value));
        Assert.Equal("0:01:05", ByteFormat.Duration(TimeSpan.FromSeconds(65)));
        Assert.Equal("2 d 03:04:05", ByteFormat.Duration(new TimeSpan(2, 3, 4, 5)));
    }

    [Fact]
    public async Task PwdPrintsPathHeaderLikeGetLocation()
    {
        await using var f = new ExecutionFixture();
        var (code, output) = await Run(f, new PwdCommand(f.Renderer));
        Assert.Equal(0, code);
        Assert.StartsWith("Path", output);
        Assert.Contains(f.DirectoryPath, output);
    }

    [Fact]
    public async Task EchoRedirectionReportsCreatedAppendedAndReplaced()
    {
        await using var f = new ExecutionFixture();
        await f.Run("echo uno > a.txt");
        Assert.Contains("Creado a.txt", f.Output.ToString());
        f.Output.GetStringBuilder().Clear();
        await f.Run("echo dos >> a.txt");
        Assert.Contains("Añadido a a.txt", f.Output.ToString());
        f.Output.GetStringBuilder().Clear();
        await f.Run("echo tres > a.txt");
        Assert.Contains("Reemplazado a.txt", f.Output.ToString());
    }

    [Fact]
    public async Task RedirectionToMissingFolderExplainsTheCause()
    {
        await using var f = new ExecutionFixture();
        var error = await Assert.ThrowsAsync<RedirectionException>(() => f.Run("echo x > no_existe/a.txt"));
        Assert.Contains("la carpeta", error.Message);
        Assert.Contains("no existe", error.Message);
    }

    [Fact]
    public async Task RmDeletesFilesAndWildcardsButRefusesFoldersWithoutRecursion()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("a.tmp"), "x");
        await File.WriteAllTextAsync(f.FilePath("b.tmp"), "x");
        await File.WriteAllTextAsync(f.FilePath("keep.txt"), "x");
        Directory.CreateDirectory(f.FilePath("carpeta"));
        await File.WriteAllTextAsync(Path.Combine(f.FilePath("carpeta"), "dentro.txt"), "x");
        var rm = new RemoveCommand(f.Renderer, "rm");

        var wildcard = await Run(f, rm, "*.tmp");
        Assert.Equal(0, wildcard.Code);
        Assert.Contains("Eliminado: a.tmp", wildcard.Output);
        Assert.False(File.Exists(f.FilePath("a.tmp")));
        Assert.True(File.Exists(f.FilePath("keep.txt")));

        var folder = await Run(f, rm, "carpeta");
        Assert.Equal(1, folder.Code);
        Assert.True(Directory.Exists(f.FilePath("carpeta")));
        Assert.True(Directory.Exists(f.FilePath("carpeta")));
        Assert.Equal(1, (await Run(f, rm, "no_existe.txt")).Code);
        Assert.Equal(0, (await Run(f, rm, "-f", "no_existe.txt")).Code);
    }

    [Fact]
    public async Task RmRecursiveForcedRemovesFolderAndRmdirRefusesNonEmpty()
    {
        await using var f = new ExecutionFixture();
        Directory.CreateDirectory(Path.Combine(f.FilePath("carpeta"), "sub"));
        await File.WriteAllTextAsync(Path.Combine(f.FilePath("carpeta"), "sub", "a.txt"), "abc");
        Directory.CreateDirectory(f.FilePath("vacia"));

        Assert.Equal(1, (await Run(f, new RemoveCommand(f.Renderer, "rmdir"), "carpeta")).Code);
        Assert.Equal(0, (await Run(f, new RemoveCommand(f.Renderer, "rd"), "vacia")).Code);
        Assert.False(Directory.Exists(f.FilePath("vacia")));

        var result = await Run(f, new RemoveCommand(f.Renderer, "rm"), "-r", "-f", "carpeta");
        Assert.Equal(0, result.Code);
        Assert.Contains("Eliminada la carpeta: carpeta", result.Output);
        Assert.False(Directory.Exists(f.FilePath("carpeta")));
    }

    [Fact]
    public async Task RemoveNeverDeletesTheCurrentFolderOrADriveRoot()
    {
        await using var f = new ExecutionFixture();
        var rm = new RemoveCommand(f.Renderer, "rm");
        var current = await Run(f, rm, "-r", "-f", ".");
        Assert.Equal(1, current.Code);
        Assert.True(Directory.Exists(f.DirectoryPath));
        var root = Path.GetPathRoot(f.DirectoryPath)!;
        Assert.Equal(1, (await Run(f, rm, "-r", "-f", root)).Code);
    }

    [Fact]
    public async Task EveryCommandHasHelpWithAtLeastOneExample()
    {
        var catalog = HelpCatalog.LoadEmbedded();
        string[] registered =
        [
            "ls", "dir", "cd", "pwd", "cls", "clear", "mkdir", "md", "rm", "del", "erase", "rmdir", "rd", "type", "cat", "echo",
            "exit", "source", "help", "version", "which", "time", "history", "jobs", "kill", "fg", "ps", "sysinfo", "theme",
            "set", "unset", "format", "top", "trace", "start", "suspend", "resume",
        ];
        foreach (var name in registered)
        {
            Assert.True(catalog.Commands.TryGetValue(name, out var entry), $"Falta la ayuda de '{name}'.");
            Assert.NotEmpty(entry!.Examples);
        }

        var listed = catalog.Groups.SelectMany(g => g.Rows).SelectMany(r => r.Names).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(registered, name => Assert.Contains(name, listed));
        foreach (var topic in new[] { "pipes", "redirect", "jobs", "vars" })
            Assert.NotEmpty(catalog.Topics[topic].Examples);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task HelpShowsLinuxAndWindowsNamesAndTopics()
    {
        await using var f = new ExecutionFixture();
        var ls = new ListDirectoryCommand(f.Renderer, "ls");
        var dir = new ListDirectoryCommand(f.Renderer, "dir");
        var help = new HelpCommand(f.Renderer, new FakeServices(ls, dir));
        var all = (await Run(f, help)).Output;
        Assert.Contains("Linux", all);
        Assert.Contains("Windows", all);
        Assert.Matches(@"ls\s+dir", all);
        var topic = await Run(f, help, "pipes");
        Assert.Equal(0, topic.Code);
        Assert.Contains("findstr.exe", topic.Output);
    }

    [Fact]
    public async Task VersionListsTheTeamAndCourse()
    {
        await using var f = new ExecutionFixture();
        var output = (await Run(f, new VersionCommand(f.Renderer))).Output;
        foreach (var name in new[] { "Francis Dayan Flores Munguía", "Erick Hazael Morales Jarquín", "Denis Josué Wong Sánchez", "Arquitectura de Sistemas Operativos" })
            Assert.Contains(name, output);
        Assert.DoesNotContain("Docente", output);
    }

    [Fact]
    public async Task SuspendAndResumeChangeJobAndProcessState()
    {
        await using var f = new ExecutionFixture();
        Assert.Equal(0, await f.Run("powershell.exe -NoProfile -Command \"Start-Sleep -Seconds 30\" &"));
        var job = Assert.Single(f.Jobs.Jobs);
        var suspend = new SuspendResumeCommand(f.Renderer, suspend: true);
        var resume = new SuspendResumeCommand(f.Renderer, suspend: false);
        try
        {
            Assert.Equal(0, (await Run(f, suspend, $"%{job.JobId}")).Code);
            Assert.Equal("suspendido", job.Status);
            await Task.Delay(200);
            Assert.Equal(ProcessStateReader.Suspended, ProcessStateReader.Read(job.Process));
            Assert.Equal(0, (await Run(f, resume, $"%{job.JobId}")).Code);
            Assert.Equal("corriendo", job.Status);
        }
        finally { f.Jobs.Kill(job); }
    }

    [Fact]
    public async Task SuspendRejectsMissingTargetsAndItself()
    {
        await using var f = new ExecutionFixture();
        var suspend = new SuspendResumeCommand(f.Renderer, suspend: true);
        Assert.Equal(1, (await Run(f, suspend)).Code);
        Assert.Equal(1, (await Run(f, suspend, "%99")).Code);
        Assert.Equal(1, (await Run(f, suspend, "2147483000")).Code);
        Assert.Equal(1, (await Run(f, suspend, System.Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture))).Code);
    }

    private sealed class FakeServices(params IBuiltinCommand[] builtins) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IEnumerable<IBuiltinCommand>) ? builtins : null;
    }
}
