using MiShell.Commands.BuiltinCommands;
using MiShell.Core.Contracts;
using MiShell.Core.Execution;
using MiShell.Core.Jobs;
using MiShell.Core.Parsing;
using MiShell.UI.Rendering;
using MiShell.UI.Themes;
using System.Diagnostics;

namespace MiShell.Core.Tests;

internal sealed class ExecutionFixture : IAsyncDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "MiShell.Tests", Guid.NewGuid().ToString("N"));
    public JobTable Jobs { get; } = new();
    public ShellState State { get; }
    public Parser Parser { get; } = new(new FakeEnvironment());
    public SpectreRenderer Renderer { get; } = new(new ThemeManager());
    public StringWriter Output { get; } = new();
    public ShellExecutor Executor { get; }
    private readonly IDisposable _outputScope;

    public ExecutionFixture(Func<string, FileMode, Stream>? outputFactory = null)
    {
        Directory.CreateDirectory(DirectoryPath);
        _outputScope = Renderer.RedirectOutput(Output);
        State = new ShellState(DirectoryPath, new FakeEnvironment(), Jobs);
        IBuiltinCommand[] builtins =
        [
            new EchoCommand(Renderer), new PwdCommand(Renderer), new ExitCommand(),
            new ReadFileCommand(Renderer, "cat"), new ListDirectoryCommand(Renderer, "ls"),
        ];
        Executor = new ShellExecutor(builtins, new ExecutableResolver(), Renderer, Jobs, State, outputFactory);
    }

    public Task<int> Run(string input, CancellationToken token = default) => Executor.ExecuteAsync(Parser.Parse(input), token);
    public string FilePath(string name) => Path.Combine(DirectoryPath, name);
    public async ValueTask DisposeAsync()
    {
        await Jobs.DisposeAsync();
        _outputScope.Dispose();
        Output.Dispose();
        Directory.Delete(DirectoryPath, recursive: true);
    }
}

public sealed class ExecutionTests
{
    [Fact]
    public async Task BuiltinOutputTruncatesAndAppendsPlainText()
    {
        await using var f = new ExecutionFixture();
        Assert.Equal(0, await f.Run("echo first > output.txt"));
        Assert.Equal(0, await f.Run("echo second >> output.txt"));
        Assert.Equal($"first{System.Environment.NewLine}second{System.Environment.NewLine}", await File.ReadAllTextAsync(f.FilePath("output.txt")));
        Assert.Equal("", f.Output.ToString());
    }

    [Fact]
    public async Task BuiltinTableRedirectsToFileInsteadOfTerminal()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("[literal].txt"), "data");
        Assert.Equal(0, await f.Run("ls > listing.txt"));
        var content = await File.ReadAllTextAsync(f.FilePath("listing.txt"));
        Assert.Contains("[literal].txt", content);
        Assert.DoesNotContain('\u001b', content);
        Assert.Equal("", f.Output.ToString());
    }

    [Fact]
    public async Task BuiltinStdinAndStdoutCanBeRedirectedTogether()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("input.txt"), "hello\nsecond\n");
        Assert.Equal(0, await f.Run("cat < input.txt > output.txt"));
        Assert.Equal(["hello", "second"], await File.ReadAllLinesAsync(f.FilePath("output.txt")));
    }

    [Fact]
    public async Task ThreeExternalStagesCanRedirectTheirLastOutput()
    {
        await using var f = new ExecutionFixture();
        Assert.Equal(0, await f.Run("cmd.exe /c echo hello | findstr.exe hello | findstr.exe hello > output.txt"));
        Assert.Equal("hello", (await File.ReadAllTextAsync(f.FilePath("output.txt"))).Trim());
    }

    [Fact]
    public async Task ExternalStdinTruncateAndAppendPreserveOutput()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("input.txt"), "hello\n");
        Assert.Equal(0, await f.Run("findstr.exe hello < input.txt > output.txt"));
        Assert.Equal(0, await f.Run("findstr.exe hello < input.txt >> output.txt"));
        Assert.Equal(["hello", "hello"], await File.ReadAllLinesAsync(f.FilePath("output.txt")));
    }

    [Fact]
    public async Task BackgroundPipelineKeepsStreamsAndAllProcessesUntilFg()
    {
        await using var f = new ExecutionFixture();
        Assert.Equal(0, await f.Run("powershell.exe -NoProfile -Command \"Start-Sleep -Milliseconds 600; 'ready'\" | findstr.exe ready > output.txt &"));
        var job = Assert.Single(f.Jobs.Jobs);
        Assert.Equal(2, job.Processes.Count);
        var result = await new FgCommand(f.Renderer).ExecuteAsync(["1"], f.State, CancellationToken.None);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("ready", (await File.ReadAllTextAsync(f.FilePath("output.txt"))).Trim());
        Assert.Empty(f.Jobs.Jobs);
    }

    [Fact]
    public async Task FgReturnsTheActualExitCode()
    {
        await using var f = new ExecutionFixture();
        await f.Run("powershell.exe -NoProfile -Command \"Start-Sleep -Milliseconds 500; exit 7\" &");
        var result = await new FgCommand(f.Renderer).ExecuteAsync(["1"], f.State, CancellationToken.None);
        Assert.Equal(7, result.ExitCode);
    }

    [Fact]
    public async Task ExplicitJobIdKillsTheEntirePipeline()
    {
        await using var f = new ExecutionFixture();
        await f.Run("powershell.exe -NoProfile -Command \"Start-Sleep -Seconds 30\" | findstr.exe ready > output.txt &");
        var job = Assert.Single(f.Jobs.Jobs);
        var result = await new KillCommand(f.Renderer).ExecuteAsync(["%1"], f.State, CancellationToken.None);
        Assert.Equal(0, result.ExitCode);
        await job.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(job.Processes, p => Assert.True(p.HasExited));
    }

    [Fact]
    public async Task MissingInputDoesNotTruncateExistingOutput()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("output.txt"), "preserve");
        await Assert.ThrowsAsync<FileNotFoundException>(() => f.Run("findstr.exe hello < missing.txt > output.txt"));
        Assert.Equal("preserve", await File.ReadAllTextAsync(f.FilePath("output.txt")));
    }

    [Fact]
    public async Task MissingLaterExecutableDoesNotStartEarlierProcess()
    {
        await using var f = new ExecutionFixture();
        await Assert.ThrowsAsync<CommandNotFoundException>(() => f.Run(
            "powershell.exe -NoProfile -Command \"'started' | Set-Content started.txt\" | missing_mishell_command"));
        Assert.False(File.Exists(f.FilePath("started.txt")));
    }

    [Fact]
    public async Task ForegroundCancellationTerminatesTheProcess()
    {
        await using var f = new ExecutionFixture();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var task = f.Run("powershell.exe -NoProfile -Command \"$PID | Out-File -Encoding ascii pid.txt; Start-Sleep -Seconds 30\"", cts.Token);
        while (!File.Exists(f.FilePath("pid.txt"))) await Task.Delay(20, cts.Token);
        // Out-File can create the file slightly before completing the write.
        string pidText;
        do { await Task.Delay(20, cts.Token); pidText = await File.ReadAllTextAsync(f.FilePath("pid.txt"), cts.Token); }
        while (string.IsNullOrWhiteSpace(pidText));
        var pid = int.Parse(pidText.Trim(), System.Globalization.CultureInfo.InvariantCulture);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(pid));
    }

    [Fact]
    public async Task ConsumerMayExitWithoutReadingAllInput()
    {
        await using var f = new ExecutionFixture();
        var code = await f.Run("cmd.exe /c echo hello | cmd.exe /c exit 0").WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task ExitStopsRemainingChainedCommands()
    {
        await using var f = new ExecutionFixture();
        await f.Run("exit 3 && echo should_not_run");
        Assert.True(f.State.ExitRequested);
        Assert.Equal(3, f.State.PendingExitCode);
        Assert.Equal("", f.Output.ToString());
    }

    [Fact]
    public async Task UnsupportedBuiltinPipelineReportsAnError()
    {
        await using var f = new ExecutionFixture();
        Assert.Equal(1, await f.Run("echo hello | findstr.exe hello"));
    }

    [Fact]
    public async Task DetailedListingIncludesSizeAndModificationDate()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("sample.txt"), "abc");
        Assert.Equal(0, await f.Run("ls -l > listing.txt"));
        var output = await File.ReadAllTextAsync(f.FilePath("listing.txt"));
        Assert.Contains("Tamaño (bytes)", output);
        Assert.Contains("FILE\tsample.txt\t3\t", output);
        Assert.Contains("Modificado", output);
    }

    [Fact]
    public async Task ProcessFilterWithoutMatchesProducesEmptyState()
    {
        await using var f = new ExecutionFixture();
        var result = await new PsCommand(f.Renderer).ExecuteAsync(["--name", "missing_mishell_process_name"], f.State, CancellationToken.None);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No hay procesos", f.Output.ToString());
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("-3")]
    [InlineData("--sort", "unsupported")]
    [InlineData("--name")]
    public async Task InvalidProcessOptionsAreRejected(params string[] arguments)
    {
        await using var f = new ExecutionFixture();
        var result = await new PsCommand(f.Renderer).ExecuteAsync(arguments, f.State, CancellationToken.None);
        Assert.Equal(1, result.ExitCode);
    }
}
