namespace MiShell.UI.Prompt;

/// <summary>Conjunto de glifos usados por el prompt.</summary>
/// <param name="Folder">Glifo de carpeta que antecede a la ruta.</param>
/// <param name="Chevron">Símbolo de la línea de escritura.</param>
public sealed record PromptIcons(string Folder, string Chevron);

/// <summary>
/// Resuelve qué glifos usar según la configuración y las capacidades del terminal,
/// para que el prompt se vea bien en cualquier computadora sin pasos manuales.
/// <para>Preferencias: <c>nerd</c>, <c>emoji</c>, <c>plain</c> o <c>auto</c> (default):
/// detecta Windows Terminal vía <c>WT_SESSION</c>; si no hay certeza, cae a texto plano.</para>
/// </summary>
public sealed class IconProvider(string preference)
{
    /// <summary>Glifos resueltos una sola vez al arrancar (la fuente no cambia en caliente).</summary>
    public PromptIcons Resolve() => preference.Trim().ToLowerInvariant() switch
    {
        "nerd" => new PromptIcons(Folder: "\uf07b ", Chevron: "\u276f"),
        "emoji" => new PromptIcons(Folder: "\U0001f4c2 ", Chevron: "\u276f"),
        "plain" => new PromptIcons(Folder: string.Empty, Chevron: ">"),
        // Auto: Windows Terminal renderiza emoji a color correctamente; la consola
        // clásica puede quedar con huecos, así que ahí se prefiere texto seguro.
        _ => IsWindowsTerminal()
            ? new PromptIcons(Folder: "\U0001f4c2 ", Chevron: "\u276f")
            : new PromptIcons(Folder: string.Empty, Chevron: ">"),
    };

    private static bool IsWindowsTerminal() =>
        Environment.GetEnvironmentVariable("WT_SESSION") is not null;
}
