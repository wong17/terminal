using System.Globalization;

namespace MiShell.Core;

/// <summary>Formato único de tamaños y duraciones: siempre con unidad, en todos los comandos.</summary>
public static class ByteFormat
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>Tamaño legible con unidad (base 1024, como el Explorador): <c>26 B</c>, <c>2.0 KB</c>, <c>397.2 GB</c>.</summary>
    public static string Human(long bytes)
    {
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1) { value /= 1024; unit++; }
        return unit == 0
            ? $"{(long)value} B"
            : value.ToString("F1", CultureInfo.InvariantCulture) + " " + Units[unit];
    }

    /// <summary>Tamaño legible a partir de MiB (como entrega la memoria de un proceso).</summary>
    public static string FromMegabytes(double megabytes) => Human((long)(megabytes * 1048576));

    /// <summary>Bytes exactos con separador de miles y unidad: <c>2,625 B</c>.</summary>
    public static string Exact(long bytes) => bytes.ToString("N0", CultureInfo.InvariantCulture) + " B";

    /// <summary>Duración como <c>h:mm:ss</c>; con días, <c>2 d 03:04:05</c>.</summary>
    public static string Duration(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return value.TotalDays >= 1
            ? $"{(int)value.TotalDays} d {value.Hours:00}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}";
    }
}
