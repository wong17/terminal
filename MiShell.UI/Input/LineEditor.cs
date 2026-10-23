using MiShell.UI.Prompt;
using MiShell.UI.Rendering;
using MiShell.UI.Themes;
using System.Text;

namespace MiShell.UI.Input;

/// <summary>
/// Editor de línea de consola con historial (flechas arriba y abajo), edición (flechas izquierda y derecha, Inicio, Fin,
/// Supr, Retroceso) y autocompletado con Tab.
/// <para>
/// El prompt puede ser multilínea: la información vive en las líneas superiores y
/// este editor solo redibuja el área de escritura, de modo que esos datos nunca
/// desaparecen mientras el usuario escribe.
/// </para>
/// </summary>
public sealed class LineEditor(
    Func<IReadOnlyList<string>> historyProvider,
    Func<string, string[]> completionProvider,
    Func<string, bool>? isKnownCommand = null,
    Func<bool>? colorEnabled = null,
    Func<ShellTheme>? currentTheme = null)
{
    /// <summary>Mínimo de columnas para aplicar geometría; debajo se degrada a modo simple.</summary>
    private const int MinUsableWidth = 10;

    /// <summary>Muestra el prompt y edita una línea tecla por tecla (flechas, historial, Tab, sugerencia gris). Ctrl+C descarta la línea y conserva el prompt; Ctrl+Z devuelve null.</summary>
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

        var knownCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
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
            if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.C)
            {
                // Ctrl+C descarta lo escrito y deja el mismo prompt limpio, sin apilar prompts nuevos.
                _ = buffer.Clear();
                cursor = 0;
                historyIndex = history.Count;
                Refresh();
                continue;
            }

            if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.Z)
            {
                Console.WriteLine();
                return null;
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
                    else if (SuggestionFor(buffer.ToString()) is { Length: > 0 } accepted)
                    {
                        _ = buffer.Append(accepted);
                        cursor = buffer.Length;
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
                    else if (SuggestionFor(buffer.ToString()) is { Length: > 0 } accepted)
                    {
                        _ = buffer.Append(accepted);
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

        string SuggestionFor(string text)
        {
            if (colorEnabled?.Invoke() != true || text.Length == 0 || cursor != buffer.Length) return string.Empty;
            for (var i = history.Count - 1; i >= 0; i--)
            {
                var entry = history[i];
                if (entry.Length > text.Length && entry.StartsWith(text, StringComparison.Ordinal))
                    return entry[text.Length..];
            }

            return string.Empty;
        }

        bool Known(string word)
        {
            if (!knownCache.TryGetValue(word, out var known))
                knownCache[word] = known = isKnownCommand!(word);
            return known;
        }

        void RewriteBuffer()
        {
            var text = buffer.ToString();
            var highlight = isKnownCommand is not null && colorEnabled?.Invoke() == true;
            var palette = currentTheme?.Invoke();
            Console.Write(highlight
                ? Colorize(text, Known, palette?.Command ?? ConsoleColor.Green, palette?.Time ?? ConsoleColor.DarkCyan)
                : text);
            var end = prefixWidth + PromptRenderer.VisibleWidth(text);
            cursorRow = RowOf(end, width);
            cursorCol = ColOf(end, width);
            if (end > 0 && end % width == 0)
            {
                // El texto llenó la fila exacta: se fuerza el ajuste de línea pendiente
                // para que el cursor físico coincida con la geometría calculada.
                Console.Write("\x1b[1B");
            }
            else if (SuggestionFor(text) is { Length: > 0 } ghost && end + ghost.Length < width)
            {
                // Sugerencia del historial en gris; solo si cabe en la fila actual.
                Console.Write($"\x1b[90m{ghost}\x1b[0m\x1b[{ghost.Length}D");
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
    /// Colorea la línea sin cambiar su ancho visible: comando válido en verde, desconocido en rojo,
    /// operadores en cian, comillas en amarillo, variables en magenta y opciones (<c>-x</c>) en el color de opción.
    /// </summary>
    /// <param name="text">Línea a colorear.</param>
    /// <param name="isKnown">Indica si una palabra en posición de comando existe.</param>
    /// <param name="commandColor">Color de un comando válido (el rol Command del tema).</param>
    /// <param name="optionColor">Color de las opciones que empiezan con guion.</param>
    public static string Colorize(string text, Func<string, bool> isKnown,
        ConsoleColor commandColor = ConsoleColor.Green, ConsoleColor optionColor = ConsoleColor.DarkCyan)
    {
        const string Reset = "\x1b[0m";
        var result = new StringBuilder();
        var atCommand = true;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                result.Append(c);
                i++;
            }
            else if (c is '|' or '&' or '<' or '>')
            {
                var start = i;
                while (i < text.Length && text[i] is '|' or '&' or '<' or '>') i++;
                var op = text[start..i];
                result.Append("\x1b[96m").Append(op).Append(Reset);
                if (op is "|" or "&&" or "||" or "&") atCommand = true;
            }
            else if (c is '"' or '\'')
            {
                var start = i++;
                while (i < text.Length && text[i] != c) i++;
                if (i < text.Length) i++;
                result.Append("\x1b[93m").Append(text, start, i - start).Append(Reset);
                atCommand = false;
            }
            else
            {
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('|' or '&' or '<' or '>' or '"' or '\'')) i++;
                var word = text[start..i];
                var color = atCommand ? (isKnown(word) ? AnsiColors.Sequence(commandColor) : "\x1b[91m")
                    : word.StartsWith('$') ? "\x1b[95m"
                    : word.Length > 1 && word[0] == '-' ? AnsiColors.Sequence(optionColor) : string.Empty;
                result.Append(color).Append(word).Append(color.Length > 0 ? Reset : string.Empty);
                atCommand = false;
            }
        }

        return result.ToString();
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
