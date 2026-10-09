using MiShell.Core.Contracts;
using MiShell.Core.Execution;

namespace MiShell.Commands.BuiltinCommands;

public sealed class EchoCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "echo";

    public string Description => "Imprime texto en pantalla.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        renderer.WriteLine(string.Join(' ', arguments));
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class ExitCommand : IBuiltinCommand
{
    public string Name => "exit";

    public string Description => "Cierra el shell con un código opcional de salida.";
    public string Usage => "exit [0..255]";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length > 1 || arguments.Length == 1 &&
            (!int.TryParse(arguments[0], out var code) || code is < 0 or > 255))
            throw new ArgumentException("Uso: exit [0..255]");
        context.RequestExit(arguments.Length == 0 ? 0 : int.Parse(arguments[0], System.Globalization.CultureInfo.InvariantCulture));
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class HelpCommand(IOutputRenderer renderer, IServiceProvider services) : IBuiltinCommand
{
    public string Name => "help";

    public string Description => "Muestra comandos, ayuda específica y sintaxis.";
    public string Usage => "help [comando | syntax]";

    private static readonly Dictionary<string, (string Detail, string Example)> Details = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ls"] = ("-l/--long añade tamaño en bytes y última modificación. Los directorios no calculan tamaño recursivo.", "ls -l ."),
        ["dir"] = ("Alias de ls. -l/--long añade tamaño en bytes y última modificación.", "dir -l ."),
        ["ps"] = ("Orden predeterminado: RAM descendente. CPU son segundos acumulados; no porcentaje instantáneo. Se omiten procesos protegidos o que terminaron.", "ps 10 --name dotnet --sort cpu"),
        ["kill"] = ("%1 identifica un job; un número sin % es un PID. Un job termina toda su tubería.", "kill %1"),
        ["fg"] = ("Espera todos los procesos y la copia de flujos del job. Devuelve el código del último proceso. Ctrl+C termina ese trabajo.", "fg 1"),
        ["cat"] = ("Lee un archivo o stdin. La redirección de entrada está disponible para cat y type.", "cat < saludo.txt"),
        ["type"] = ("Alias de cat; lee un archivo o stdin.", "type saludo.txt"),
        ["theme"] = ("contrast: fondo oscuro; paper: fondo claro; plain: sin color. La elección se guarda para próximas sesiones.", "theme contrast"),
        ["source"] = ("Ejecuta cada línea en la sesión actual. Ignora líneas vacías y comentarios #. Continúa tras errores, informa archivo/línea y devuelve fallo si hubo alguno.", "source demo.mish"),
        ["set"] = ("Define una variable de entorno para esta sesión y sus procesos hijos. Usa comillas para valores con espacios.", "set SALUDO=\"Hola mundo\""),
        ["cd"] = ("Sin argumento va al directorio personal. Acepta .., ~ y rutas entre comillas.", "cd \"C:\\Program Files\""),
        ["exit"] = ("Sale del shell con un código entre 0 y 255. El código predeterminado es 0.", "exit 0"),
        ["sysinfo"] = ("Muestra uso global de CPU, RAM y de la unidad actual. MiB/GiB son unidades binarias. Requiere contadores y WMI de Windows.", "sysinfo"),
        ["history"] = ("Muestra las últimas órdenes guardadas en el perfil local. Arriba/abajo permiten recuperarlas.", "history"),
        ["which"] = ("Busca programas externos en PATH. Los internos se consultan con help.", "which cmd.exe"),
    };

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        // Resolución diferida: pedir IEnumerable<IBuiltinCommand> en el constructor
        // crearía una dependencia circular (help es él mismo un comando).
        var builtins = (IEnumerable<IBuiltinCommand>?)services.GetService(typeof(IEnumerable<IBuiltinCommand>)) ?? [];
        if (arguments.Length > 1)
        {
            renderer.WriteError($"Uso: {Usage}");
            return Task.FromResult(CommandResult.Failure);
        }
        if (arguments.Length == 1)
        {
            var name = arguments[0];
            if (name.Equals("syntax", StringComparison.OrdinalIgnoreCase))
            {
                renderer.WriteLine("Comillas dobles expanden $VARIABLE; comillas simples conservan texto literal.");
                renderer.WriteLine("> reemplaza salida; >> añade; < lee entrada. Una entrada y una salida por comando.");
                renderer.WriteLine("| conecta programas externos; & inicia una tubería externa en segundo plano.");
                renderer.WriteLine("&& ejecuta si el anterior termina con 0; || ejecuta si termina con error.");
                renderer.WriteLine("Los internos admiten > y >>; cat/type también <. No participan en pipes ni en &.");
                renderer.WriteLine("Ejemplo: cmd.exe /c echo Hola | findstr.exe Hola > saludo.txt");
                return Task.FromResult(CommandResult.Ok);
            }
            var command = builtins.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (command is null)
            {
                renderer.WriteError($"No hay ayuda interna para '{name}'. Consulta help o la ayuda del programa externo.");
                return Task.FromResult(CommandResult.Failure);
            }
            renderer.WriteLine($"{command.Name}: {command.Description}");
            renderer.WriteLine($"Uso: {command.Usage}");
            if (Details.TryGetValue(command.Name, out var detail))
            {
                renderer.WriteLine(detail.Detail);
                renderer.WriteLine($"Ejemplo: {detail.Example}");
            }
            return Task.FromResult(CommandResult.Ok);
        }
        renderer.RenderTable(
            "Comandos internos",
            ["Comando", "Descripción", "Uso"],
            builtins
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .Select(b => (string[])[b.Name, b.Description, b.Usage]));
        renderer.WriteInfo("Usa help <comando> o help syntax para más detalle. Los programas externos se buscan en PATH.");
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class VersionCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "version";

    public string Description => "Muestra la versión del shell y de .NET.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        renderer.WriteLine($"MiShell 1.0 · .NET {Environment.Version} · {Environment.OSVersion.VersionString}");
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class WhichCommand(IOutputRenderer renderer, IExecutableResolver resolver) : IBuiltinCommand
{
    public string Name => "which";

    public string Description => "Muestra la ruta de un ejecutable.";

    public string Usage => "which <comando>";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            renderer.WriteError("Uso: which <comando>");
            return Task.FromResult(CommandResult.Failure);
        }

        var resolved = resolver.Resolve(arguments[0]);
        if (resolved is null)
        {
            renderer.WriteError($"No se encontró '{arguments[0]}' en el PATH.");
            return Task.FromResult(CommandResult.Failure);
        }

        renderer.WriteLine(resolved, renderer.Theme.Success);
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class TimeCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "time";

    public string Description => "Muestra la fecha y hora actuales.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        renderer.WriteLine(DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy, HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture), renderer.Theme.Accent);
        return Task.FromResult(CommandResult.Ok);
    }
}

public sealed class HistoryCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "history";

    public string Description => "Muestra el historial de comandos.";

    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        for (var i = 0; i < context.History.Count; i++)
        {
            renderer.WriteLine($"{i + 1,4}  {context.History[i]}", renderer.Theme.Muted);
        }

        return Task.FromResult(CommandResult.Ok);
    }
}
