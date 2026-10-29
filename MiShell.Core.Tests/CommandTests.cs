using MiShell.Commands.BuiltinCommands;
using MiShell.Commands.Help;
using MiShell.Core.Contracts;
using MiShell.Core.Environment;
using MiShell.Core.Execution;

namespace MiShell.Core.Tests;

/// <summary>Comprueba que los comandos internos muestren al menos la información de sus equivalentes de Windows.</summary>
public sealed class CommandTests
{
    private sealed class FakeServices(params IBuiltinCommand[] builtins) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IEnumerable<IBuiltinCommand>) ? builtins : null;
    }

    private static async Task<(int Code, string Output)> Run(ExecutionFixture f, IBuiltinCommand command, params string[] args)
    {
        f.Output.GetStringBuilder().Clear();
        var result = await command.ExecuteAsync(args, f.State, CancellationToken.None);
        return (result.ExitCode, f.Output.ToString());
    }

    [Fact]
    public async Task ListShowsModeDateSizeAndNameLikeDir()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("sample.txt"), "abc");
        Directory.CreateDirectory(f.FilePath("carpeta"));
        var (code, output) = await Run(f, new ListDirectoryCommand(f.Renderer, "ls"));
        Assert.Equal(0, code);
        Assert.Contains("Modo\tModificado\tTamaño\tNombre", output);
        Assert.Matches(@"-a----\t\d\d/\d\d/\d{4} \d\d:\d\d [AP]M\t3 B\tsample\.txt", output);
        Assert.Matches(@"d-----\t\d\d/\d\d/\d{4} \d\d:\d\d [AP]M\t\tcarpeta", output);
        Assert.Contains("1 archivos (3 B) · 1 directorios", output);
    }

    [Fact]
    public async Task ListLongAddsCreatedAndAccessedDates()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("a.txt"), "x");
        var (_, output) = await Run(f, new ListDirectoryCommand(f.Renderer, "ls"), "-l");
        Assert.Contains("Modo\tCreado\tModificado\tAccedido\tTamaño\tNombre", output);
    }

    [Fact]
    public async Task ListHidesHiddenFilesUnlessAllIsRequested()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("visible.txt"), "x");
        await File.WriteAllTextAsync(f.FilePath("oculto.txt"), "x");
        File.SetAttributes(f.FilePath("oculto.txt"), FileAttributes.Hidden);
        var ls = new ListDirectoryCommand(f.Renderer, "ls");
        Assert.DoesNotContain("oculto.txt", (await Run(f, ls)).Output);
        var all = (await Run(f, ls, "-a")).Output;
        Assert.Contains("oculto.txt", all);
        Assert.Contains("--h--", all.Replace("-a-h--", "--h--"));
    }

    [Fact]
    public async Task ListSupportsWildcardsRecursionAndHumanSizes()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllTextAsync(f.FilePath("a.txt"), new string('x', 2048));
        await File.WriteAllTextAsync(f.FilePath("b.png"), "x");
        Directory.CreateDirectory(f.FilePath("sub"));
        await File.WriteAllTextAsync(Path.Combine(f.FilePath("sub"), "inner.txt"), "x");
        var ls = new ListDirectoryCommand(f.Renderer, "ls");
        var filtered = (await Run(f, ls, "*.txt")).Output;
        Assert.Contains("a.txt", filtered);
        Assert.DoesNotContain("b.png", filtered);
        Assert.Contains("inner.txt", (await Run(f, ls, "-R")).Output);
        Assert.Contains("2.0 KB", (await Run(f, ls)).Output);
        Assert.Contains("2,048 B", (await Run(f, ls, "-b")).Output);
    }

    [Fact]
    public async Task ListReportsMissingPathsAndInvalidOptions()
    {
        await using var f = new ExecutionFixture();
        var ls = new ListDirectoryCommand(f.Renderer, "ls");
        Assert.Equal(1, (await Run(f, ls, "no_existe")).Code);
        Assert.Equal(1, (await Run(f, ls, "-z")).Code);
    }

    [Fact]
    public async Task MkdirCreatesParentsReportsExistingAndRejectsFileNames()
    {
        await using var f = new ExecutionFixture();
        var mkdir = new MakeDirectoryCommand(f.Renderer);
        var first = await Run(f, mkdir, "a/b/c");
        Assert.Equal(0, first.Code);
        Assert.True(Directory.Exists(f.FilePath("a/b/c")));
        Assert.Contains("Modo\tModificado\tNombre", first.Output);
        Assert.Contains(f.FilePath("a/b/c").Replace('/', '\\'), first.Output);
        Assert.Contains("Ya existía", (await Run(f, mkdir, "a/b/c")).Output);
        await File.WriteAllTextAsync(f.FilePath("archivo"), "x");
        Assert.Equal(1, (await Run(f, mkdir, "archivo")).Code);
    }

    [Fact]
    public async Task CatReadsSeveralFilesWithHeadersAndSupportsNumberingHeadAndTail()
    {
        await using var f = new ExecutionFixture();
        await File.WriteAllLinesAsync(f.FilePath("uno.txt"), ["a", "b", "c"]);
        await File.WriteAllLinesAsync(f.FilePath("dos.txt"), ["x", "y"]);
        var cat = new ReadFileCommand(f.Renderer, "cat");
        var both = (await Run(f, cat, "uno.txt", "dos.txt")).Output;
        Assert.Contains("==> uno.txt <==", both);
        Assert.Contains("==> dos.txt <==", both);
        Assert.Contains("     2  b", (await Run(f, cat, "-n", "uno.txt")).Output);
        var head = (await Run(f, cat, "--head", "1", "uno.txt")).Output;
        Assert.Contains("a", head);
        Assert.DoesNotContain("b", head);
        var tail = (await Run(f, cat, "--tail", "1", "uno.txt")).Output;
        Assert.Contains("c", tail);
        Assert.DoesNotContain("a", tail);
        Assert.Equal(1, (await Run(f, cat, "uno.txt", "falta.txt")).Code);
    }

    [Fact]
    public async Task CdSupportsPreviousDirectoryAndPathsWithSpaces()
    {
        await using var f = new ExecutionFixture();
        Directory.CreateDirectory(f.FilePath("mi carpeta"));
        var start = f.State.WorkingDirectory;
        var cd = new CdCommand(f.Renderer);
        Assert.Equal(0, (await Run(f, cd, "mi", "carpeta")).Code);
        Assert.Equal(f.FilePath("mi carpeta"), f.State.WorkingDirectory);
        Assert.Equal(0, (await Run(f, cd, "-")).Code);
        Assert.Equal(start, f.State.WorkingDirectory);
        Assert.Equal(1, (await Run(f, cd, "no_existe")).Code);
        var (_, file) = (0, f.FilePath("archivo.txt"));
        await File.WriteAllTextAsync(file, "x");
        Assert.Equal(1, (await Run(f, cd, "archivo.txt")).Code);
        System.Environment.CurrentDirectory = AppContext.BaseDirectory;
    }

    [Fact]
    public async Task PwdVerboseShowsDriveAndPreviousDirectory()
    {
        await using var f = new ExecutionFixture();
        var (code, output) = await Run(f, new PwdCommand(f.Renderer), "-v");
        Assert.Equal(0, code);
        Assert.Contains("Unidad", output);
        Assert.Contains("Espacio libre", output);
        Assert.Contains("Directorio anterior", output);
    }

    [Fact]
    public async Task EchoSupportsNoNewlineAndEscapes()
    {
        await using var f = new ExecutionFixture();
        var echo = new EchoCommand(f.Renderer);
        Assert.Equal("hola", (await Run(f, echo, "-n", "hola")).Output);
        Assert.Contains("a\tb", (await Run(f, echo, "-e", @"a\tb")).Output);
    }

    [Fact]
    public async Task SetListsFiltersAndDefinesVariablesAndUnsetRemovesThem()
    {
        await using var f = new ExecutionFixture();
        var env = new ProcessEnvironment();
        var set = new SetCommand(f.Renderer, env);
        var unset = new UnsetCommand(f.Renderer, env);
        try
        {
            var created = await Run(f, set, "MISHELL_PRUEBA_X=uno");
            Assert.Contains("(nueva)", created.Output);
            Assert.Contains("(antes: uno)", (await Run(f, set, "MISHELL_PRUEBA_X=dos")).Output);
            var shown = await Run(f, set, "MISHELL_PRUEBA");
            Assert.Contains("MISHELL_PRUEBA_X", shown.Output);
            Assert.Contains("dos", shown.Output);
            Assert.Equal(1, (await Run(f, set, "VARIABLE_QUE_NO_EXISTE_ABC")).Code);
            Assert.Equal(0, (await Run(f, unset, "MISHELL_PRUEBA_X")).Code);
            Assert.Null(env.GetVariable("MISHELL_PRUEBA_X"));
            Assert.Equal(1, (await Run(f, unset, "MISHELL_PRUEBA_X")).Code);
        }
        finally { env.RemoveVariable("MISHELL_PRUEBA_X"); }
    }

    [Fact]
    public async Task HistoryShowsNumbersLastEntriesAndSearches()
    {
        await using var f = new ExecutionFixture();
        f.State.History = ["ls", "cd ..", "echo hola", "ls -l"];
        var history = new HistoryCommand(f.Renderer);
        var all = (await Run(f, history)).Output;
        Assert.Contains("    1  ls", all);
        Assert.Contains("4 de 4 entradas", all);
        var last = (await Run(f, history, "2")).Output;
        Assert.DoesNotContain("cd ..", last);
        Assert.Contains("echo hola", last);
        var found = (await Run(f, history, "ls")).Output;
        Assert.Contains("2 de 4 entradas", found);
    }

    [Fact]
    public async Task WhichShowsTypeVersionAndSourceForBuiltinsAndPrograms()
    {
        await using var f = new ExecutionFixture();
        var echo = new EchoCommand(f.Renderer);
        var which = new WhichCommand(f.Renderer, new ExecutableResolver(), new FakeServices(echo));
        var builtin = await Run(f, which, "echo");
        Assert.Contains("interno", builtin.Output);
        var external = await Run(f, which, "cmd");
        Assert.Contains("externo", external.Output);
        Assert.Contains("cmd.exe", external.Output);
        Assert.Contains("Versión", external.Output);
        Assert.Equal(1, (await Run(f, which, "programa_que_no_existe_abc")).Code);
    }

    [Fact]
    public async Task PsDetailShowsExtendedInformationForOneProcess()
    {
        await using var f = new ExecutionFixture();
        var (code, output) = await Run(f, new PsCommand(f.Renderer), "--id", System.Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(0, code);
        foreach (var field in new[] { "Hilos", "Handles", "Memoria privada", "Inicio", "Prioridad", "Ruta" })
            Assert.Contains(field, output);
        Assert.Equal(1, (await Run(f, new PsCommand(f.Renderer), "--id", "2147483000")).Code);
    }

    [Fact]
    public async Task PsTableIncludesThreadsHandlesSessionAndStart()
    {
        await using var f = new ExecutionFixture();
        var (code, output) = await Run(f, new PsCommand(f.Renderer), "3", "--sort", "pid");
        Assert.Equal(0, code);
        Assert.Contains("PID\tNombre\tEstado\tHilos\tCPU\tMemoria\t% RAM\tInicio", output);
    }

    [Fact]
    public async Task KillAcceptsSeveralTargetsAndReportsEachFailure()
    {
        await using var f = new ExecutionFixture();
        var (code, _) = await Run(f, new KillCommand(f.Renderer), "2147483000", "%99");
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task TimeAndVersionShowDetailTables()
    {
        await using var f = new ExecutionFixture();
        var time = (await Run(f, new TimeCommand(f.Renderer))).Output;
        foreach (var field in new[] { "Zona horaria", "UTC", "ISO 8601", "Marca Unix", "Encendido desde hace" })
            Assert.Contains(field, time);
        var version = (await Run(f, new VersionCommand(f.Renderer))).Output;
        foreach (var field in new[] { "Runtime", "Sistema operativo", "Arquitectura del SO", "Equipo", "Usuario" })
            Assert.Contains(field, version);
    }

    [Fact]
    public void HelpCatalogLoadsGroupsSyntaxAndDetailsFromEmbeddedJson()
    {
        var catalog = HelpCatalog.LoadEmbedded();
        Assert.Contains(catalog.Groups, g => g.Name == "Archivos" && g.Rows.Any(r => r.Names.Contains("ls")));
        Assert.NotEmpty(catalog.Syntax);
        Assert.Contains("modo", catalog.Commands["LS"].Detail);
    }

    [Fact]
    public async Task HelpListsGroupsAndExplainsOneCommand()
    {
        await using var f = new ExecutionFixture();
        var echo = new EchoCommand(f.Renderer);
        var ls = new ListDirectoryCommand(f.Renderer, "ls");
        var help = new HelpCommand(f.Renderer, new FakeServices(echo, ls));
        var all = (await Run(f, help)).Output;
        Assert.Contains("Archivos", all);
        Assert.Contains("ls", all);
        var one = await Run(f, help, "ls");
        Assert.Equal(0, one.Code);
        Assert.Contains("Detalle", one.Output);
        Assert.Contains("Ejemplo", one.Output);
        Assert.Equal(1, (await Run(f, help, "no_existe")).Code);
    }

    [Fact]
    public async Task FormatChangesAndPersistsTheOutputStyle()
    {
        await using var f = new ExecutionFixture();
        var settings = new MiShell.Configuration.ShellSettings { DataDirectory = f.FilePath("perfil") };
        var format = new FormatCommand(f.Renderer, settings);
        Assert.Equal(0, (await Run(f, format, "boxed")).Code);
        Assert.Equal("boxed", f.Renderer.OutputStyle);
        Assert.Equal("boxed", settings.OutputStyle);
        Assert.Equal(1, (await Run(f, format, "otro")).Code);
        Assert.Equal(0, (await Run(f, format, "classic")).Code);
        Assert.Equal("classic", f.Renderer.OutputStyle);
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task TopOnceShowsCpuMemoryAndProcessTable()
    {
        await using var f = new ExecutionFixture();
        var (code, output) = await Run(f, new TopCommand(f.Renderer), "--once", "--count", "3");
        Assert.Equal(0, code);
        Assert.Contains("CPU", output);
        Assert.Contains("RAM", output);
        Assert.Contains("PID", output);
        Assert.Equal(1, (await Run(f, new TopCommand(f.Renderer), "--malo")).Code);
    }
}
