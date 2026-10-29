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

    /// <summary>Fecha y hora de 12 horas (<c>22/10/2026 09:41 PM</c>), igual para todos los listados.</summary>
    public static string Date(DateTime value) => MiShell.Core.ClockFormat.DateAndTime(value);

    /// <summary>Tamaño legible con unidad (B, KB, MB, GB, TB) a partir de bytes.</summary>
    public static string Human(long bytes) => MiShell.Core.ByteFormat.Human(bytes);

    /// <summary>Convierte un patrón con * y ? en una expresión regular que ignora mayúsculas.</summary>
    public static Regex Wildcard(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
