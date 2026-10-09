using Microsoft.Extensions.DependencyInjection;
using MiShell.App;
using MiShell.Commands.BuiltinCommands;
using MiShell.Configuration;
using MiShell.Core.Contracts;
using MiShell.Core.Environment;
using MiShell.Core.Execution;
using MiShell.Core.History;
using MiShell.Core.Jobs;
using MiShell.Core.Parsing;
using MiShell.UI.Input;
using MiShell.UI.Prompt;
using MiShell.UI.Rendering;
using MiShell.UI.Themes;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var dataOverride = Environment.GetEnvironmentVariable("MISHELL_DATA_DIR");
var dataDirectory = string.IsNullOrWhiteSpace(dataOverride)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiShell")
    : Path.GetFullPath(dataOverride);
var settings = ShellSettings.Load(Path.Combine(dataDirectory, "settings.json"));
if (!string.IsNullOrWhiteSpace(dataOverride)) settings.DataDirectory = dataDirectory;

var services = new ServiceCollection();

// Infraestructura
var themeManager = new ThemeManager();
themeManager.SelectOrFallback(settings.Theme);
services.AddSingleton(themeManager);
services.AddSingleton(settings);
services.AddSingleton<IOutputRenderer, SpectreRenderer>();
services.AddSingleton<IShellEnvironment, ProcessEnvironment>();
services.AddSingleton<JobTable>();
services.AddSingleton<IJobTable>(sp => sp.GetRequiredService<JobTable>());
services.AddSingleton<IExecutableResolver, ExecutableResolver>();
services.AddSingleton(new HistoryStore(settings.HistoryFilePath));
services.AddSingleton(sp => new ShellState(Environment.CurrentDirectory, sp.GetRequiredService<IShellEnvironment>(), sp.GetRequiredService<JobTable>()));

// Parsing y ejecución
services.AddSingleton<Parser>();
services.AddSingleton<IShellExecutor, ShellExecutor>();

// Comandos internos (añadir uno nuevo = una línea aquí)
services.AddSingleton<IBuiltinCommand, CdCommand>();
services.AddSingleton<IBuiltinCommand, PwdCommand>();
services.AddSingleton<IBuiltinCommand, ClsCommand>();
services.AddSingleton<IBuiltinCommand>(sp => new ClearCommand(
    sp.GetRequiredService<IOutputRenderer>(), "clear"));
services.AddSingleton<IBuiltinCommand>(sp => new ListDirectoryCommand(
    sp.GetRequiredService<IOutputRenderer>(), "ls"));
services.AddSingleton<IBuiltinCommand>(sp => new ListDirectoryCommand(
    sp.GetRequiredService<IOutputRenderer>(), "dir"));
services.AddSingleton<IBuiltinCommand, MakeDirectoryCommand>();
services.AddSingleton<IBuiltinCommand>(sp => new ReadFileCommand(
    sp.GetRequiredService<IOutputRenderer>(), "type"));
services.AddSingleton<IBuiltinCommand>(sp => new ReadFileCommand(
    sp.GetRequiredService<IOutputRenderer>(), "cat"));
services.AddSingleton<IBuiltinCommand, EchoCommand>();
services.AddSingleton<IBuiltinCommand, ExitCommand>();
services.AddSingleton<IBuiltinCommand, SourceCommand>();
services.AddSingleton<IBuiltinCommand>(sp => new HelpCommand(
    sp.GetRequiredService<IOutputRenderer>(),
    sp.GetRequiredService<IServiceProvider>()));
services.AddSingleton<IBuiltinCommand, VersionCommand>();
services.AddSingleton<IBuiltinCommand, WhichCommand>();
services.AddSingleton<IBuiltinCommand, TimeCommand>();
services.AddSingleton<IBuiltinCommand, HistoryCommand>();
services.AddSingleton<IBuiltinCommand, JobsCommand>();
services.AddSingleton<IBuiltinCommand, KillCommand>();
services.AddSingleton<IBuiltinCommand, FgCommand>();
services.AddSingleton<IBuiltinCommand, PsCommand>();
services.AddSingleton<IBuiltinCommand, SysInfoCommand>();
services.AddSingleton<IBuiltinCommand, ThemeCommand>();
services.AddSingleton<IBuiltinCommand, SetCommand>();

// UI interactiva
services.AddSingleton(sp => new LineEditor(
    () => sp.GetRequiredService<HistoryStore>().Entries,
    line => CompletionsFor(sp, line)));
services.AddSingleton(new IconProvider(settings.Icons));
services.AddSingleton(sp => new PromptRenderer(
    sp.GetRequiredService<ThemeManager>(),
    sp.GetRequiredService<IconProvider>(),
    settings.PromptShowTime));
services.AddSingleton<Shell>();

await using var provider = services.BuildServiceProvider();
var shell = provider.GetRequiredService<Shell>();
ConsoleCancelEventHandler cancelHandler = (_, e) =>
{
    e.Cancel = true;
    shell.CancelCurrentCommand();
};
Console.CancelKeyPress += cancelHandler;
try { return await shell.RunAsync(CancellationToken.None); }
finally { Console.CancelKeyPress -= cancelHandler; }

static string[] CompletionsFor(IServiceProvider sp, string line)
{
    var builtins = sp.GetServices<IBuiltinCommand>().Select(b => b.Name);
    if (!line.Contains(' '))
    {
        return builtins.Where(n => n.StartsWith(line, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    // Autocompleta rutas del último argumento.
    var prefix = line.Split(' ')[^1];
    var dirPart = Path.GetDirectoryName(prefix);
    var baseDir = string.IsNullOrEmpty(dirPart) ? Environment.CurrentDirectory : Path.GetFullPath(dirPart, Environment.CurrentDirectory);
    var filePart = Path.GetFileName(prefix);

    try
    {
        return Directory.EnumerateFileSystemEntries(baseDir, filePart + "*")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Take(20)
            .ToArray();
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
    {
        return [];
    }
}
