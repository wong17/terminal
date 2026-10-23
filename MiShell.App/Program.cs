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
services.AddSingleton<IBuiltinCommand>(sp => new WhichCommand(sp.GetRequiredService<IOutputRenderer>(),
    sp.GetRequiredService<IExecutableResolver>(), sp));
services.AddSingleton<IBuiltinCommand, TimeCommand>();
services.AddSingleton<IBuiltinCommand, HistoryCommand>();
services.AddSingleton<IBuiltinCommand, JobsCommand>();
services.AddSingleton<IBuiltinCommand, KillCommand>();
services.AddSingleton<IBuiltinCommand, FgCommand>();
services.AddSingleton<IBuiltinCommand, PsCommand>();
services.AddSingleton<IBuiltinCommand, SysInfoCommand>();
services.AddSingleton<IBuiltinCommand, ThemeCommand>();
services.AddSingleton<IBuiltinCommand, SetCommand>();
services.AddSingleton<IBuiltinCommand, UnsetCommand>();
services.AddSingleton<IBuiltinCommand, FormatCommand>();
services.AddSingleton<IBuiltinCommand, TopCommand>();
services.AddSingleton<IBuiltinCommand, TraceCommand>();

// UI interactiva
services.AddSingleton(sp => new CompletionProvider(
    () => sp.GetServices<IBuiltinCommand>().Select(b => b.Name),
    () => sp.GetRequiredService<ShellState>().WorkingDirectory,
    sp.GetRequiredService<IShellEnvironment>()));
services.AddSingleton(sp => new LineEditor(
    () => sp.GetRequiredService<HistoryStore>().Entries,
    line => sp.GetRequiredService<CompletionProvider>().Complete(line),
    name => sp.GetServices<IBuiltinCommand>().Any(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        || sp.GetRequiredService<IExecutableResolver>().Resolve(name) is not null,
    () => AnsiColors.Enabled && sp.GetRequiredService<ThemeManager>().Current.Name != "plain",
    () => sp.GetRequiredService<ThemeManager>().Current));
services.AddSingleton(new IconProvider(settings.Icons));
services.AddSingleton(sp => new PromptRenderer(
    sp.GetRequiredService<ThemeManager>(),
    sp.GetRequiredService<IconProvider>(),
    settings.PromptShowTime));
services.AddSingleton<Shell>();

await using var provider = services.BuildServiceProvider();
provider.GetRequiredService<IOutputRenderer>().OutputStyle = settings.OutputStyle;
var shell = provider.GetRequiredService<Shell>();
ConsoleCancelEventHandler cancelHandler = (_, e) =>
{
    e.Cancel = true;
    shell.CancelCurrentCommand();
};
Console.CancelKeyPress += cancelHandler;
try { return await shell.RunAsync(CancellationToken.None); }
finally { Console.CancelKeyPress -= cancelHandler; }
