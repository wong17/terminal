using System.Text.Json;

namespace MiShell.Core.History;

/// <summary>Historial de comandos persistido como JSON en el perfil del usuario.</summary>
public sealed class HistoryStore : IDisposable
{
    private const int MaxEntries = 500;
    private readonly List<string> _entries = [];
    private readonly string _filePath;
    private bool _persistenceAvailable = true;

    public HistoryStore(string filePath)
    {
        _filePath = filePath;
        if (!File.Exists(filePath)) return;

        try
        {
            var loaded = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(filePath));
            if (loaded is not null)
            {
                _entries.AddRange(loaded);
            }
        }
        catch (JsonException)
        {
            // Historial corrupto: se arranca limpio en vez de bloquear el shell.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El historial es opcional: si el perfil no es escribible, el shell
            // continúa funcionando con historial solo en memoria.
            _persistenceAvailable = false;
        }
    }

    public IReadOnlyList<string> Entries => _entries;

    public void Add(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine) || _entries.Count > 0 && _entries[^1] == commandLine)
        {
            return;
        }

        _entries.Add(commandLine);
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }

        Persist();
    }

    private void Persist()
    {
        if (!_persistenceAvailable)
        {
            return;
        }
        
        var directory = Path.GetDirectoryName(_filePath);
        try
        {
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(_entries));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No se debe perder la sesión interactiva por una limitación del
            // sistema de archivos o del perfil del usuario.
            _persistenceAvailable = false;
        }
    }

    public void Dispose() => Persist();
}
