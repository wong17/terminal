namespace MiShell.UI.Rendering;

/// <summary>Nombres de los estilos de salida de tablas y listas.</summary>
public static class OutputStyles
{
    /// <summary>Columnas alineadas con encabezado subrayado y sin bordes, como Get-ChildItem o dir (predeterminado).</summary>
    public const string Classic = "classic";

    /// <summary>Tablas con bordes redondeados de Spectre.Console.</summary>
    public const string Boxed = "boxed";

    /// <summary>Estilos válidos para el comando <c>format</c>.</summary>
    public static IReadOnlyList<string> All { get; } = [Classic, Boxed];
}
