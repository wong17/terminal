using System.Reflection;
using System.Text.Json;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>
/// Una fila de <c>help</c>: el nombre del comando en Linux y en Windows. Si <see cref="Windows"/> está vacío,
/// el comando se llama igual en los dos sistemas. <see cref="Also"/> son alias adicionales que no se muestran.
/// </summary>
/// <param name="Linux">Nombre estilo Linux/bash (o el único nombre).</param>
/// <param name="Windows">Nombre estilo cmd, si es distinto.</param>
/// <param name="Also">Otros alias cubiertos por esta fila.</param>
public sealed record HelpRow(string Linux, string? Windows = null, IReadOnlyList<string>? Also = null)
{
    /// <summary>Todos los nombres de comando que esta fila cubre.</summary>
    public IEnumerable<string> Names => new[] { Linux }.Concat(Windows is null ? [] : [Windows]).Concat(Also ?? []);
}

/// <summary>Un grupo de comandos que <c>help</c> muestra junto (Archivos, Procesos, Sesión, Sistema).</summary>
/// <param name="Name">Título del grupo.</param>
/// <param name="Rows">Filas del grupo, en el orden en que se muestran.</param>
public sealed record HelpGroup(string Name, IReadOnlyList<HelpRow> Rows);

/// <summary>Un ejemplo listo para copiar con una nota breve de lo que hace.</summary>
/// <param name="Command">La línea que se escribe.</param>
/// <param name="Note">Qué hace o qué se espera ver.</param>
public sealed record HelpExample(string Command, string Note = "");

/// <summary>Explicación larga de un comando para <c>help &lt;comando&gt;</c>.</summary>
/// <param name="Detail">Qué hace y qué opciones acepta.</param>
/// <param name="Examples">Ejemplos listos para copiar, del más simple al más completo.</param>
/// <param name="Equivalent">Cómo se llama en Linux, cmd y PowerShell.</param>
/// <param name="SeeAlso">Comandos relacionados.</param>
public sealed record HelpEntry(string Detail, IReadOnlyList<HelpExample> Examples, string Equivalent = "", IReadOnlyList<string>? SeeAlso = null);

/// <summary>Un tema transversal (tuberías, redirecciones, trabajos, variables) para <c>help &lt;tema&gt;</c>.</summary>
/// <param name="Title">Título.</param>
/// <param name="Lines">Explicación en líneas cortas.</param>
/// <param name="Examples">Ejemplos con nota.</param>
public sealed record HelpTopic(string Title, IReadOnlyList<string> Lines, IReadOnlyList<HelpExample> Examples);

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

    /// <summary>Detalle y ejemplos de cada comando, por nombre.</summary>
    public IReadOnlyDictionary<string, HelpEntry> Commands { get; init; } = new Dictionary<string, HelpEntry>();

    /// <summary>Temas transversales, por nombre (pipes, redirect, jobs, vars).</summary>
    public IReadOnlyDictionary<string, HelpTopic> Topics { get; init; } = new Dictionary<string, HelpTopic>();

    /// <summary>Lee el catálogo embebido; si faltara el recurso devuelve un catálogo vacío para que <c>help</c> siga funcionando.</summary>
    public static HelpCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream is null) return new HelpCatalog();
        var loaded = JsonSerializer.Deserialize<HelpCatalog>(stream, Options) ?? new HelpCatalog();
        // Los nombres de comandos y temas no distinguen mayúsculas.
        return new HelpCatalog
        {
            Groups = loaded.Groups,
            Syntax = loaded.Syntax,
            Commands = new Dictionary<string, HelpEntry>(loaded.Commands, StringComparer.OrdinalIgnoreCase),
            Topics = new Dictionary<string, HelpTopic>(loaded.Topics, StringComparer.OrdinalIgnoreCase),
        };
    }
}
