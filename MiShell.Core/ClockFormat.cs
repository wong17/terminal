using System.Globalization;

namespace MiShell.Core;

/// <summary>Formato único de hora de 12 horas (con AM/PM) para el prompt, el banner y los monitores.</summary>
public static class ClockFormat
{
    /// <summary>Hora sin segundos, por ejemplo <c>8:17 PM</c>.</summary>
    public static string Hour(DateTime value) => value.ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>Hora con segundos, por ejemplo <c>8:17:05 PM</c>.</summary>
    public static string HourWithSeconds(DateTime value) => value.ToString("h:mm:ss tt", CultureInfo.InvariantCulture);
}
