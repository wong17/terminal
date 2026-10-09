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
                case ConsoleKey.None:
                case ConsoleKey.Clear:
                case ConsoleKey.Pause:
                case ConsoleKey.Escape:
                case ConsoleKey.Spacebar:
                case ConsoleKey.PageUp:
                case ConsoleKey.PageDown:
                case ConsoleKey.Select:
                case ConsoleKey.Print:
                case ConsoleKey.Execute:
                case ConsoleKey.PrintScreen:
                case ConsoleKey.Insert:
                case ConsoleKey.Help:
                case ConsoleKey.D0:
                case ConsoleKey.D1:
                case ConsoleKey.D2:
                case ConsoleKey.D3:
                case ConsoleKey.D4:
                case ConsoleKey.D5:
                case ConsoleKey.D6:
                case ConsoleKey.D7:
                case ConsoleKey.D8:
                case ConsoleKey.D9:
                case ConsoleKey.A:
                case ConsoleKey.B:
                case ConsoleKey.C:
                case ConsoleKey.D:
                case ConsoleKey.E:
                case ConsoleKey.F:
                case ConsoleKey.G:
                case ConsoleKey.H:
                case ConsoleKey.I:
                case ConsoleKey.J:
                case ConsoleKey.K:
                case ConsoleKey.L:
                case ConsoleKey.M:
                case ConsoleKey.N:
                case ConsoleKey.O:
                case ConsoleKey.P:
                case ConsoleKey.Q:
                case ConsoleKey.R:
                case ConsoleKey.S:
                case ConsoleKey.T:
                case ConsoleKey.U:
                case ConsoleKey.V:
                case ConsoleKey.W:
                case ConsoleKey.X:
                case ConsoleKey.Y:
                case ConsoleKey.Z:
                case ConsoleKey.LeftWindows:
                case ConsoleKey.RightWindows:
                case ConsoleKey.Applications:
                case ConsoleKey.Sleep:
                case ConsoleKey.NumPad0:
                case ConsoleKey.NumPad1:
                case ConsoleKey.NumPad2:
                case ConsoleKey.NumPad3:
                case ConsoleKey.NumPad4:
                case ConsoleKey.NumPad5:
                case ConsoleKey.NumPad6:
                case ConsoleKey.NumPad7:
                case ConsoleKey.NumPad8:
                case ConsoleKey.NumPad9:
                case ConsoleKey.Multiply:
                case ConsoleKey.Add:
                case ConsoleKey.Separator:
                case ConsoleKey.Subtract:
                case ConsoleKey.Decimal:
                case ConsoleKey.Divide:
                case ConsoleKey.F1:
                case ConsoleKey.F2:
                case ConsoleKey.F3:
                case ConsoleKey.F4:
                case ConsoleKey.F5:
                case ConsoleKey.F6:
                case ConsoleKey.F7:
                case ConsoleKey.F8:
                case ConsoleKey.F9:
                case ConsoleKey.F10:
                case ConsoleKey.F11:
                case ConsoleKey.F12:
                case ConsoleKey.F13:
                case ConsoleKey.F14:
                case ConsoleKey.F15:
                case ConsoleKey.F16:
                case ConsoleKey.F17:
                case ConsoleKey.F18:
                case ConsoleKey.F19:
                case ConsoleKey.F20:
                case ConsoleKey.F21:
                case ConsoleKey.F22:
                case ConsoleKey.F23:
                case ConsoleKey.F24:
                case ConsoleKey.BrowserBack:
                case ConsoleKey.BrowserForward:
                case ConsoleKey.BrowserRefresh:
                case ConsoleKey.BrowserStop:
                case ConsoleKey.BrowserSearch:
                case ConsoleKey.BrowserFavorites:
                case ConsoleKey.BrowserHome:
                case ConsoleKey.VolumeMute:
                case ConsoleKey.VolumeDown:
                case ConsoleKey.VolumeUp:
                case ConsoleKey.MediaNext:
                case ConsoleKey.MediaPrevious:
                case ConsoleKey.MediaStop:
                case ConsoleKey.MediaPlay:
                case ConsoleKey.LaunchMail:
                case ConsoleKey.LaunchMediaSelect:
                case ConsoleKey.LaunchApp1:
                case ConsoleKey.LaunchApp2:
                case ConsoleKey.Oem1:
                case ConsoleKey.OemPlus:
                case ConsoleKey.OemComma:
                case ConsoleKey.OemMinus:
                case ConsoleKey.OemPeriod:
                case ConsoleKey.Oem2:
                case ConsoleKey.Oem3:
                case ConsoleKey.Oem4:
                case ConsoleKey.Oem5:
                case ConsoleKey.Oem6:
                case ConsoleKey.Oem7:
                case ConsoleKey.Oem8:
                case ConsoleKey.Oem102:
                case ConsoleKey.Process:
                case ConsoleKey.Packet:
                case ConsoleKey.Attention:
                case ConsoleKey.CrSel:
                case ConsoleKey.ExSel:
                case ConsoleKey.EraseEndOfFile:
                case ConsoleKey.Play:
                case ConsoleKey.Zoom:
                case ConsoleKey.NoName:
                case ConsoleKey.Pa1:
                case ConsoleKey.OemClear:
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
            var candidates = completionProvider(buffer.ToString());
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
            var lastSpace = buffer.ToString().LastIndexOf(' ');
            _ = buffer.Clear().Append(buffer.ToString()[..(lastSpace + 1)]).Append(completion);
            cursor = buffer.Length;
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
            var end = prefixWidth + buffer.Length;
            cursorRow = RowOf(end, width);
            cursorCol = ColOf(end, width);
            if (end > 0 && end % width == 0)
            {
                // El texto llenó la fila exacta: se fuerza el ajuste de línea pendiente
                // para que el cursor físico coincida con la geometría calculada.
                Console.Write("\x1b[1B");
            }

            MoveCursorTo(prefixWidth + cursor, width);
        }

        void Refresh()
        {
            ClearInputArea();
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
