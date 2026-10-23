using MiShell.Core.Contracts;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MiShell.Commands.BuiltinCommands;

/// <summary>Formatos compartidos por los comandos de archivos.</summary>
internal static class FileFormat
{
    /// <summary>Atributos al estilo de Get-ChildItem: d a r h s l (directorio, archivo, solo lectura, oculto, sistema, enlace).</summary>
    public static string Mode(FileSystemInfo entry)
    {
        var a = entry.Attributes;
        return string.Concat(
            a.HasFlag(FileAttributes.Directory) ? 'd' : '-',
            a.HasFlag(FileAttributes.Archive) ? 'a' : '-',
            a.HasFlag(FileAttributes.ReadOnly) ? 'r' : '-',
            a.HasFlag(FileAttributes.Hidden) ? 'h' : '-',
            a.HasFlag(FileAttributes.System) ? 's' : '-',
            a.HasFlag(FileAttributes.ReparsePoint) ? 'l' : '-');
    }

    /// <summary>Fecha en formato yyyy-MM-dd HH:mm, igual para todos los listados.</summary>
    public static string Date(DateTime value) => value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Tamaño legible (KiB, MiB, GiB...) a partir de bytes.</summary>
    public static string Human(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var value = (double)bytes;
        var i = 0;
        while (value >= 1024 && i < units.Length - 1) { value /= 1024; i++; }
        return i == 0 ? $"{bytes} B" : $"{value.ToString("F1", CultureInfo.InvariantCulture)} {units[i]}";
    }

    /// <summary>Convierte un patrón con * y ? en una expresión regular que ignora mayúsculas.</summary>
    public static Regex Wildcard(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
