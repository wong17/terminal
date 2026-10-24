using System.Globalization;

namespace MiShell.Core;

/// <summary>Formato único de fecha y de hora de 12 horas (con AM/PM) para el prompt, el banner, los listados y los monitores.</summary>
public static class ClockFormat
{
    /// <summary>Hora sin segundos, por ejemplo <c>8:17 PM</c>.</summary>
    public static string Hour(DateTime value) => value.ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>Hora con segundos, por ejemplo <c>8:17:05 PM</c>.</summary>
    public static string HourWithSeconds(DateTime value) => value.ToString("h:mm:ss tt", CultureInfo.InvariantCulture);

    /// <summary>Fecha en día, mes y año, por ejemplo <c>22/10/2026</c>.</summary>
    public static string Date(DateTime value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Fecha y hora de ancho fijo para columnas, por ejemplo <c>22/10/2026 09:41 PM</c>.</summary>
    public static string DateAndTime(DateTime value) => value.ToString("dd/MM/yyyy hh:mm tt", CultureInfo.InvariantCulture);

    /// <summary>Momento corto para listas de procesos: <c>hoy 9:15 PM</c> o <c>22/10 9:15 PM</c>.</summary>
    public static string Moment(DateTime value) => value.Date == DateTime.Today
        ? "hoy " + Hour(value)
        : value.ToString("dd/MM", CultureInfo.InvariantCulture) + " " + Hour(value);
}
