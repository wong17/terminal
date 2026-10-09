using MiShell.Commands.BuiltinCommands;
using MiShell.Core.Contracts;
using MiShell.UI.Input;

namespace MiShell.Core.Tests;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class ClosureTests
{
    [Theory]
    [InlineData("cat < input.txt > input.txt")]
    [InlineData("cat input.txt > input.txt")]
    [InlineData("findstr.exe hello < input.txt > input.txt")]
    [InlineData("cmd.exe /c echo hello > input.txt | findstr.exe hello > input.txt")]
    public async Task UnsafeRedirectionsPreserveTheFile(string command)
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("input.txt"), "hello");
        await Assert.ThrowsAsync<IOException>(() => f.Run(command));
        Assert.Equal("hello", await File.ReadAllTextAsync(f.FilePath("input.txt")));
    }

    [Fact]
    public async Task LockedOutputFailsWithoutChangingData()
    {
        await using var f = new ExecutionFixture();
        var path = f.FilePath("output.txt");
        await File.WriteAllTextAsync(path, "preserve");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            await Assert.ThrowsAsync<IOException>(() => f.Run("cmd.exe /c echo hello > output.txt"));
        Assert.Equal("preserve", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task FailedBackgroundOutputEndsProcessesAndReportsCause()
    {
        await using var f = new ExecutionFixture((_, _) => new FailingWriteStream());
        await f.Run("powershell.exe -NoProfile -Command \"'hello'; Start-Sleep -Seconds 30\" > output.txt &");
        var job = Assert.Single(f.Jobs.Jobs);
        await Assert.ThrowsAsync<IOException>(() => job.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.All(job.Processes, p => Assert.True(p.HasExited));
        Assert.Equal("fallido", job.Status);
        Assert.Contains("simulado", job.Detail);
        var result = await new FgCommand(f.Renderer).ExecuteAsync(["1"], f.State, default);
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(f.Jobs.Jobs);
    }

    [Fact]
    public async Task ExplicitInputOverridesTheIncomingPipe()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("input.txt"), "chosen\n");
        Assert.Equal(0, await f.Run("cmd.exe /c echo ignored | findstr.exe chosen < input.txt > output.txt"));
        Assert.Equal("chosen", (await File.ReadAllTextAsync(f.FilePath("output.txt"))).Trim());
    }

    [Fact]
    public async Task SystemInfoCancellationInterruptsSampling()
    {
        await using var f = new ExecutionFixture();
        using var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource();
        var command = new SysInfoCommand(f.Renderer, async token =>
        {
            started.SetResult();
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return 0;
        }, () => throw new InvalidOperationException("RAM should not run"));
        var task = command.ExecuteAsync([], f.State, cts.Token);
        await started.Task;
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task SystemInfoReportsUnavailableResources()
    {
        await using var f = new ExecutionFixture();
        var command = new SysInfoCommand(f.Renderer, _ => Task.FromResult(0d),
            () => throw new UnauthorizedAccessException("WMI no disponible"));
        Assert.Equal(1, (await command.ExecuteAsync([], f.State, default)).ExitCode);
    }

    [Fact]
    public async Task CompletionPreservesQuotedPathsAndReadsCurrentPath()
    {
        await using var f = new ExecutionFixture();
        Directory.CreateDirectory(f.FilePath("my folder"));
        await File.WriteAllTextAsync(f.FilePath("my folder/file name.txt"), "data");
        var env = new MutableEnvironment();
        var completion = new CompletionProvider(() => ["cat", "echo"], () => f.DirectoryPath, env);
        Assert.Contains("cat \"my folder\\file name.txt\"", completion.Complete("cat \"my folder\\fi"));
        Assert.Contains("cat 'my folder\\file name.txt'", completion.Complete("cat 'my folder\\fi"));
        Assert.Contains("cmd | echo", completion.Complete("cmd | ec"));
        Assert.Empty(completion.Complete("cat missing\\fi"));
        Directory.CreateDirectory(f.FilePath("tools"));
        await File.WriteAllTextAsync(f.FilePath("tools/live.exe"), "");
        Assert.Empty(completion.Complete("liv"));
        env.SetVariable("PATH", f.FilePath("tools"));
        Assert.Contains("live", completion.Complete("liv"));
        env.SetVariable("PATH", "");
        Assert.Empty(completion.Complete("liv"));
    }

    [Fact]
    public async Task DeniedOutputAccessLeavesSessionDataIntact()
    {
        await using var f = new ExecutionFixture();
        var file = new FileInfo(f.FilePath("protected.txt"));
        await File.WriteAllTextAsync(file.FullName, "preserve");
        var original = System.IO.FileSystemAclExtensions.GetAccessControl(file);
        var denied = System.IO.FileSystemAclExtensions.GetAccessControl(file);
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        denied.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(sid,
            System.Security.AccessControl.FileSystemRights.Write,
            System.Security.AccessControl.AccessControlType.Deny));
        try
        {
            System.IO.FileSystemAclExtensions.SetAccessControl(file, denied);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Run("echo changed > protected.txt"));
        }
        finally { System.IO.FileSystemAclExtensions.SetAccessControl(file, original); }
        Assert.Equal("preserve", await File.ReadAllTextAsync(file.FullName));
        Assert.Equal(0, await f.Run("echo session-alive"));
    }

    private sealed class MutableEnvironment : IShellEnvironment
    {
        private readonly Dictionary<string, string> _values = [];
        public string? GetVariable(string name) => _values.GetValueOrDefault(name);
        public void SetVariable(string name, string value) => _values[name] = value;
        public string Expand(string text) => text;
        public IEnumerable<(string Name, string Value)> EnumerateVariables() => _values.Select(p => (p.Key, p.Value));
    }

    private sealed class FailingWriteStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new IOException("fallo de escritura simulado"));
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromException(new IOException("fallo de escritura simulado"));
    }
}
