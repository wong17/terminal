using MiShell.UI.Prompt;
using System.Text;

namespace MiShell.UI.Input;

/// <summary>
/// Editor de línea de consola con historial (↑/↓), edición (←/→, Inicio, Fin,
/// Supr, Retroceso) y autocompletado con Tab.
/// <para>
/// El prompt puede ser multilínea: la información vive en las líneas superiores y
/// este editor solo redibuja el área de escritura, de modo que esos datos nunca
/// desaparecen mientras el usuario escribe.
/// </para>
/// </summary>
public sealed class LineEditor(Func<IReadOnlyList<string>> historyProvider, Func<string, string[]> completionProvider)
{
    /// <summary>Mínimo de columnas para aplicar geometría; debajo se degrada a modo simple.</summary>
    private const int MinUsableWidth = 10;

    public string? ReadLine(string prompt)
    {
        // Con stdin redirigido (pipe/script) no hay teclado: se lee línea a línea.
        if (Console.IsInputRedirected)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        var width = Math.Max(MinUsableWidth, SafeWindowWidth());
        var buffer = new StringBuilder();
        var history = historyProvider();
        var historyIndex = history.Count;
        var cursor = 0;

        var inputPrefix = prompt.Split('\n')[^1];
        var prefixWidth = PromptRenderer.VisibleWidth(inputPrefix);

        Console.Write(prompt);
        // El cursor físico quedó justo tras el prefijo (última línea del prompt).
        var cursorRow = RowOf(prefixWidth, width);
        int cursorCol;

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            var currentWidth = Math.Max(MinUsableWidth, SafeWindowWidth());
            if (currentWidth != width)
            {
                width = currentWidth;
                cursorRow = RowOf(prefixWidth + PromptRenderer.VisibleWidth(buffer.ToString()[..cursor]), width);
            }
            if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key is ConsoleKey.C or ConsoleKey.Z)
            {
                Console.WriteLine();
                return key.Key == ConsoleKey.Z ? null : string.Empty;
            }

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return buffer.ToString();
                case ConsoleKey.Backspace:
                    if (cursor > 0)
                    {
                        _ = buffer.Remove(cursor - 1, 1);
                        cursor--;
                        Refresh();
                    }

                    break;
                case ConsoleKey.Delete:
                    if (cursor < buffer.Length)
                    {
                        _ = buffer.Remove(cursor, 1);
                        Refresh();
                    }

                    break;
                case ConsoleKey.LeftArrow:
                    if (cursor > 0)
                    {
                        cursor--;
                        Refresh();
                    }

                    break;
                case ConsoleKey.RightArrow:
                    if (cursor < buffer.Length)
                    {
                        cursor++;
                        Refresh();
                    }

                    break;
                case ConsoleKey.Home:
                    if (cursor != 0)
                    {
                        cursor = 0;
                        Refresh();
                    }

                    break;
                case ConsoleKey.End:
                    if (cursor != buffer.Length)
                    {
                        cursor = buffer.Length;
                        Refresh();
                    }

                    break;
                case ConsoleKey.UpArrow:
                    if (historyIndex > 0)
                    {
                        historyIndex--;
                        ReplaceLine(history[historyIndex]);
                    }

                    break;
                case ConsoleKey.DownArrow:
                    if (historyIndex < history.Count)
                    {
                        historyIndex++;
                        ReplaceLine(historyIndex < history.Count ? history[historyIndex] : string.Empty);
                    }

                    break;
                case ConsoleKey.Tab:
                    ShowCompletions();
                    break;
                default:
                    // Cualquier otra tecla imprime su carácter si no es de control.
                    if (!char.IsControl(key.KeyChar))
                    {
                        buffer.Insert(cursor, key.KeyChar);
                        cursor++;
                        Refresh();
                    }

                    break;
            }
        }

        void ReplaceLine(string text)
        {
            _ = buffer.Clear().Append(text);
            cursor = buffer.Length;
            Refresh();
        }

        void ShowCompletions()
        {
            var candidates = completionProvider(buffer.ToString()[..cursor]);
            switch (candidates.Length)
            {
                case 1:
                    CompleteWith(candidates[0]);
                    break;
                case > 1:
                    ListCandidates(candidates);
                    break;
            }
        }

        void CompleteWith(string completion)
        {
            var suffix = buffer.ToString()[cursor..];
            _ = buffer.Clear().Append(completion).Append(suffix);
            cursor = completion.Length;
            Refresh();
        }

        void ClearInputArea()
        {
            if (cursorRow > 0)
            {
                Console.Write($"\x1b[{cursorRow}A");
            }

            Console.Write("\r\x1b[J");
        }

        void RewriteBuffer()
        {
            Console.Write(buffer);
            var end = prefixWidth + PromptRenderer.VisibleWidth(buffer.ToString());
            cursorRow = RowOf(end, width);
            cursorCol = ColOf(end, width);
            if (end > 0 && end % width == 0)
            {
                // El texto llenó la fila exacta: se fuerza el ajuste de línea pendiente
                // para que el cursor físico coincida con la geometría calculada.
                Console.Write("\x1b[1B");
            }

            MoveCursorTo(prefixWidth + PromptRenderer.VisibleWidth(buffer.ToString()[..cursor]), width);
        }

        void Refresh()
        {
            ClearInputArea();
            Console.Write(inputPrefix);
            RewriteBuffer();
        }

        void ListCandidates(string[] candidates)
        {
            ClearInputArea();
            Console.WriteLine();
            Console.WriteLine(string.Join("  ", candidates));
            Console.Write(inputPrefix);
            cursorRow = RowOf(prefixWidth, width);
            cursorCol = ColOf(prefixWidth, width);
            RewriteBuffer();
        }

        void MoveCursorTo(int position, int columns)
        {
            var targetRow = RowOf(position, columns);
            var targetCol = ColOf(position, columns);
            if (cursorRow > targetRow)
            {
                Console.Write($"\x1b[{cursorRow - targetRow}A");
            }
            else if (cursorRow < targetRow)
            {
                Console.Write($"\x1b[{targetRow - cursorRow}B");
            }

            if (targetCol > cursorCol)
            {
                Console.Write($"\x1b[{targetCol - cursorCol}C");
            }
            else if (targetCol < cursorCol)
            {
                Console.Write($"\x1b[{cursorCol - targetCol}D");
            }

            cursorRow = targetRow;
            cursorCol = targetCol;
        }
    }

    /// <summary>
    /// Fila (relativa al inicio del área de escritura) donde queda el cursor tras
    /// escribir <paramref name="absoluteColumn"/> caracteres. Las posiciones que
    /// llenan una fila exacta ya provocaron el ajuste de línea del terminal.
    /// </summary>
    private static int RowOf(int absoluteColumn, int width) =>
        absoluteColumn <= 0 ? 0 : absoluteColumn / width;

    /// <summary>Columna dentro de la fila para una posición absoluta dada.</summary>
    private static int ColOf(int absoluteColumn, int width) => absoluteColumn % width;

    private static int SafeWindowWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            return MinUsableWidth;
        }
    }
}
