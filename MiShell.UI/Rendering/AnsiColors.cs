namespace MiShell.UI.Rendering;

/// <summary>Preserva la diferencia entre los ocho tonos normales y los ocho brillantes.</summary>
public static class AnsiColors
{
    /// <summary>True si la salida admite color (no está redirigida y NO_COLOR no está definido).</summary>
    public static bool Enabled => !Console.IsOutputRedirected &&
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    /// <summary>Secuencia ANSI del color pedido, o cadena vacía si el color está desactivado.</summary>
    public static string For(ConsoleColor color, bool enabled = true) =>
        !enabled || !Enabled ? string.Empty : Sequence(color);

    /// <summary>Secuencia ANSI del color pedido sin consultar si la salida admite color (para texto que se colorea antes de decidir).</summary>
    public static string Sequence(ConsoleColor color)
    {
        var code = color switch
        {
            ConsoleColor.Black => 30,
            ConsoleColor.DarkRed => 31,
            ConsoleColor.DarkGreen => 32,
            ConsoleColor.DarkYellow => 33,
            ConsoleColor.DarkBlue => 34,
            ConsoleColor.DarkMagenta => 35,
            ConsoleColor.DarkCyan => 36,
            ConsoleColor.Gray => 37,
            ConsoleColor.DarkGray => 90,
            ConsoleColor.Red => 91,
            ConsoleColor.Green => 92,
            ConsoleColor.Yellow => 93,
            ConsoleColor.Blue => 94,
            ConsoleColor.Magenta => 95,
            ConsoleColor.Cyan => 96,
            ConsoleColor.White => 97,
            _ => 39,
        };
        return $"\u001b[{code}m";
    }
}
