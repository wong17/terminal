using MiShell.Core.Contracts;

namespace MiShell.Commands.BuiltinCommands;

/// <summary><c>time</c>: fecha y hora local con zona horaria, UTC, ISO 8601, semana, marca Unix y tiempo encendido.</summary>
public sealed class TimeCommand(IOutputRenderer renderer) : IBuiltinCommand
{
    public string Name => "time";

    public string Description => "Muestra fecha, hora, zona horaria, UTC, semana y tiempo encendido.";

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(string[] arguments, ICommandContext context, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        var culture = System.Globalization.CultureInfo.GetCultureInfo("es-ES");
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var week = System.Globalization.ISOWeek.GetWeekOfYear(now.DateTime);
        var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
        renderer.WriteLine(now.ToString("dddd, dd 'de' MMMM 'de' yyyy, ", culture) + MiShell.Core.ClockFormat.HourWithSeconds(now.DateTime), renderer.Theme.Accent);
        renderer.RenderList("Fecha y hora",
        [
            ("Fecha", MiShell.Core.ClockFormat.Date(now.DateTime)),
            ("Hora local", $"{MiShell.Core.ClockFormat.HourWithSeconds(now.DateTime)}  ({now.ToString("HH:mm:ss.fff", inv)})"),
            ("Zona horaria", $"{TimeZoneInfo.Local.StandardName} (UTC{(now.Offset < TimeSpan.Zero ? "-" : "+")}{now.Offset:hh\\:mm})"),
            ("UTC", now.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", inv)),
            ("ISO 8601", now.ToString("o", inv)),
            ("Día del año / semana ISO", $"{now.DayOfYear} / {week}"),
            ("Marca Unix", now.ToUnixTimeSeconds().ToString(inv)),
            ("Encendido desde hace", $"{(int)up.TotalDays} d {up.Hours:00}:{up.Minutes:00}:{up.Seconds:00}"),
        ]);
        return Task.FromResult(CommandResult.Ok);
    }
}
