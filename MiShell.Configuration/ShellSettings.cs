using System.Text.Json;

namespace MiShell.Configuration;

/// <summary>Ajustes persistidos del shell entre sesiones.</summary>
public sealed class ShellSettings
{
    public string Theme { get; set; } = "dracula";

    /// <summary>Muestra la hora en la primera línea del prompt.</summary>
    public bool PromptShowTime { get; set; } = true;

    /// <summary>Estilo de glifos del prompt: auto, nerd, emoji o plain.</summary>
    public string Icons { get; set; } = "auto";

    public string DataDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiShell");

    public string HistoryFilePath => Path.Combine(DataDirectory, "history.json");

    public string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");

    public void SetTheme(string theme) => Theme = theme;

    public static ShellSettings Load(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ShellSettings();
            }

            var loaded = JsonSerializer.Deserialize<ShellSettings>(File.ReadAllText(filePath));
            return loaded ?? new ShellSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new ShellSettings();
        }
    }

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public void Save(string filePath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
            File.WriteAllText(filePath, JsonSerializer.Serialize(this, WriteOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // La configuración es opcional: un perfil sin permisos no debe
            // impedir que el shell siga funcionando durante la sesión.
        }
    }
}
