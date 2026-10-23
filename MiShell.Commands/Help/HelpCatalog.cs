using System.Reflection;
using System.Text.Json;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Un grupo de comandos que <c>help</c> muestra junto (Archivos, Procesos, Sesión, Sistema).</summary>
/// <param name="Name">Título del grupo.</param>
/// <param name="Commands">Nombres de los comandos del grupo, en el orden en que se muestran.</param>
public sealed record HelpGroup(string Name, IReadOnlyList<string> Commands);

/// <summary>Explicación larga de un comando para <c>help &lt;comando&gt;</c>.</summary>
/// <param name="Detail">Qué hace y qué opciones acepta.</param>
/// <param name="Example">Un ejemplo listo para copiar.</param>
public sealed record HelpEntry(string Detail, string Example);

/// <summary>
/// Texto de ayuda de la shell cargado desde <c>help.json</c> (recurso embebido en el ensamblado).
/// Mantener el texto en un archivo aparte permite corregir o traducir la ayuda sin tocar el código.
/// </summary>
public sealed class HelpCatalog
{
    private const string ResourceName = "MiShell.Commands.Help.help.json";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Grupos en el orden en que se muestran.</summary>
    public IReadOnlyList<HelpGroup> Groups { get; init; } = [];

    /// <summary>Líneas de la ayuda de sintaxis (<c>help syntax</c>).</summary>
    public IReadOnlyList<string> Syntax { get; init; } = [];

    /// <summary>Detalle y ejemplo de cada comando, por nombre.</summary>
    public IReadOnlyDictionary<string, HelpEntry> Commands { get; init; } = new Dictionary<string, HelpEntry>();

    /// <summary>Lee el catálogo embebido; si faltara el recurso devuelve un catálogo vacío para que <c>help</c> siga funcionando.</summary>
    public static HelpCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream is null) return new HelpCatalog();
        var loaded = JsonSerializer.Deserialize<HelpCatalog>(stream, Options) ?? new HelpCatalog();
        // Los nombres de comandos no distinguen mayúsculas.
        return new HelpCatalog
        {
            Groups = loaded.Groups,
            Syntax = loaded.Syntax,
            Commands = new Dictionary<string, HelpEntry>(loaded.Commands, StringComparer.OrdinalIgnoreCase),
        };
    }
}
