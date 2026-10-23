using MiShell.Core.Contracts;
using MiShell.UI.Themes;
using Spectre.Console;
using System.Globalization;
using Console = System.Console;

namespace MiShell.UI.Rendering;

/// <summary>
/// Implementación de <see cref="IOutputRenderer"/> basada en Spectre.Console. Es el único lugar que
/// escribe con formato: los comandos piden "muestra una tabla" o "muestra un error" y esta clase decide
/// cómo se ve según el tema, el estilo de salida y si la salida está redirigida a un archivo.
/// </summary>
/// <remarks>
/// La clase está dividida en archivos parciales por responsabilidad:
/// <c>Banner</c> (bienvenida animada), <c>Tables</c> (tablas y listas) y <c>Charts</c> (barras y panel del sistema).
/// </remarks>
public sealed partial class SpectreRenderer(ThemeManager themeManager) : IOutputRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Espera antes de mostrar el indicador: comandos rápidos nunca lo ven.</summary>
    private const int SpinnerDelayMs = 400;

    private IAnsiConsole _console = AnsiConsole.Console;
    private TextWriter _writer = Console.Out;
    private int _redirectDepth;

    /// <inheritdoc />
    public IThemePalette Theme => themeManager.Current;

    /// <inheritdoc />
    public string OutputStyle { get; set; } = OutputStyles.Classic;

    /// <summary>
    /// Desvía todo lo que se escriba a <paramref name="writer"/> (un archivo o una tubería) sin colores
    /// ni bordes. Al liberar el objeto devuelto se restaura la consola anterior.
    /// </summary>
    public IDisposable RedirectOutput(TextWriter writer)
    {
        var previousConsole = _console;
        var previousWriter = _writer;
        _console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
        });
        _writer = writer;
        _redirectDepth++;
        return new OutputScope(() =>
        {
            _console = previousConsole;
            _writer = previousWriter;
            _redirectDepth--;
        });
    }

    private sealed class OutputScope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    /// <summary>True cuando no deben usarse colores ni adornos (tema plain, NO_COLOR o salida redirigida).</summary>
    private bool IsPlain => !AnsiColors.Enabled || themeManager.Current.Name == "plain" || _redirectDepth > 0;

    /// <inheritdoc />
    public void Write(string text, ConsoleColor? color = null) => _writer.Write(IsPlain ? text : Format(text, color));

    /// <inheritdoc />
    public void WriteLine(string text = "", ConsoleColor? color = null) => _writer.WriteLine(IsPlain ? text : Format(text, color));

    /// <inheritdoc />
    public void WriteError(string message) => Console.Error.WriteLine(
        Console.IsErrorRedirected ? $"Error: {message}" : Format($"✗ {message}", themeManager.Current.ErrorColor));

    /// <inheritdoc />
    public void WriteSuccess(string message) => WriteLine($"✓ {message}", themeManager.Current.Success);

    /// <inheritdoc />
    public void WriteInfo(string message) => WriteLine(message, themeManager.Current.Info);

    /// <inheritdoc />
    public void ClearScreen()
    {
        // En scripts o pruebas con stdout redirigido no existe una consola
        // interactiva que limpiar; el comando sigue siendo válido y no debe
        // abortar la sesión por un handle no asociado a una terminal.
        if (Console.IsOutputRedirected || _redirectDepth > 0)
        {
            return;
        }

        try
        {
            Console.Clear();
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
        {
            // Algunas terminales compatibles con ANSI no exponen Console.Clear.
        }
    }

    /// <inheritdoc />
    public async Task<T> RunWithSpinnerAsync<T>(string label, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        // Sin consola interactiva no hay dónde dibujar el indicador.
        if (Console.IsOutputRedirected)
        {
            return await work(cancellationToken).ConfigureAwait(false);
        }

        var task = work(cancellationToken);
        var completed = await Task.WhenAny(task, Task.Delay(SpinnerDelayMs, cancellationToken)).ConfigureAwait(false);
        if (completed != task && !cancellationToken.IsCancellationRequested)
        {
            var accent = HexColor(themeManager.Current.AccentHex);
            await _console.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(accent))
                .StartAsync(label, _ => task)
                .ConfigureAwait(false);
        }

        return await task.ConfigureAwait(false);
    }

    /// <summary>Envuelve el texto en la secuencia ANSI del color pedido (si hay color y el tema lo permite).</summary>
    private string Format(string text, ConsoleColor? color) =>
        color is null || !AnsiColors.Enabled || themeManager.Current.Name == "plain"
            ? text : AnsiColors.For(color.Value) + text + "\e[0m";

    /// <summary>Convierte "rrggbb" a sus tres componentes.</summary>
    private static (byte R, byte G, byte B) ParseHex(string hex) =>
        (Convert.ToByte(hex[0..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16));

    /// <summary>Convierte un color "rrggbb" o un nombre sencillo en un <see cref="Color"/> de Spectre.</summary>
    private static Color HexColor(string hexOrName)
    {
        if (hexOrName.Length == 6)
        {
            var (r, g, b) = ParseHex(hexOrName);
            return new Color(r, g, b);
        }

        return hexOrName.ToUpperInvariant() switch
        {
            "RED" => Color.Red,
            "GREEN" => Color.Green,
            "YELLOW" => Color.Yellow,
            "BLUE" => Color.Blue,
            "MAGENTA" => Color.Magenta,
            "CYAN" => Color.Cyan,
            "WHITE" => Color.White,
            "GRAY" or "GREY" => Color.Grey,
            _ => Color.White,
        };
    }

    /// <summary>Ancho de la ventana, o 80 si no se puede leer (salida redirigida).</summary>
    private static int SafeWindowWidth()
    {
        try
        {
            return Console.WindowWidth > 0 ? Console.WindowWidth : 80;
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            return 80;
        }
    }

    /// <summary>Barra de bloques proporcional (█ lleno, ░ vacío).</summary>
    private static string BuildMiniBar(double ratio, int width)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        var filled = (int)Math.Round(ratio * width);
        return new string('█', filled) + new string('░', width - filled);
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
