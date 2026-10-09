using MiShell.App;
using System.Diagnostics;

namespace MiShell.Core.Tests;

public sealed class SessionTests
{
    [Theory]
    [InlineData("echo eof-marker\n", 0)]
    [InlineData("exit 7\n", 7)]
    public async Task RedirectedInputEndsWithoutRepeatingPrompts(string input, int expectedCode)
    {
        var (code, stdout, stderr) = await RunSession(input);
        Assert.Equal(expectedCode, code);
        Assert.DoesNotContain('\u001b', stdout);
        Assert.DoesNotContain("Error interno", stderr);
        if (input.StartsWith("echo", StringComparison.Ordinal)) Assert.Contains("eof-marker", stdout);
    }

    [Fact]
    public async Task ErrorsDoNotPreventLaterCommands()
    {
        var (code, stdout, stderr) = await RunSession("echo hello |\nmissing_mishell_command\necho recovery-marker\nexit\n");
        Assert.Equal(0, code);
        Assert.Contains("recovery-marker", stdout);
        Assert.Contains("sintaxis", stderr);
        Assert.Contains("Comando no encontrado", stderr);
        Assert.DoesNotContain("Error interno", stderr);
    }


    [Fact]
    public async Task HelpAndProcessDetailsRemainReadableInRedirectedOutput()
    {
        var (code, stdout, stderr) = await RunSession("help ps\nhelp syntax\nps --name missing_mishell_process_name\nexit\n");
        Assert.Equal(0, code);
        Assert.Contains("--sort ram|cpu|pid", stdout);
        Assert.Contains("comillas simples", stdout);
        Assert.Contains("No hay procesos", stdout);
        Assert.Equal("", stderr);
    }

    [Fact]
    public async Task SourceReportsLineAndPreservesFailureAfterRecovery()
    {
        var (code, stdout, stderr) = await RunSession("source script.mish || echo script-failed\nexit\n",
            "echo broken |\necho recovered\n");
        Assert.Equal(0, code);
        Assert.Contains("script.mish:1:", stderr);
        Assert.Contains("recovered", stdout);
        Assert.Contains("script-failed", stdout);
    }

    [Fact]
    public async Task RecursiveSourceProducesErrorWithoutTerminatingTheSession()
    {
        var (code, stdout, stderr) = await RunSession("source script.mish || echo recursion-detected\nexit\n",
            "source script.mish\n");
        Assert.Equal(0, code);
        Assert.Contains("Script recursivo", stderr);
        Assert.Contains("recursion-detected", stdout);
        Assert.DoesNotContain("Error interno", stderr);
    }

    private static async Task<(int Code, string Stdout, string Stderr)> RunSession(string input, string? script = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "MiShell.SessionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (script is not null) await File.WriteAllTextAsync(Path.Combine(directory, "script.mish"), script);
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory,
        };
        start.ArgumentList.Add(typeof(Shell).Assembly.Location);
        start.Environment["MISHELL_DATA_DIR"] = Path.Combine(directory, "profile");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("No inició MiShell.");
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            return (process.ExitCode, await stdout, await stderr);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            Directory.Delete(directory, recursive: true);
        }
    }
}
