using System.Text.Json;

namespace MiShell.Configuration;

/// <summary>Ajustes persistidos del shell entre sesiones.</summary>
public sealed class ShellSettings
{
    /// <summary>Nombre del tema de colores activo.</summary>
    public string Theme { get; set; } = "contrast";

    /// <summary>Formato de tablas y listas: classic (columnas alineadas) o boxed (con bordes).</summary>
    public string OutputStyle { get; set; } = "classic";

    /// <summary>Muestra la hora en la primera línea del prompt.</summary>
    public bool PromptShowTime { get; set; } = true;

    /// <summary>Estilo de glifos del prompt: auto, nerd, emoji o plain.</summary>
    public string Icons { get; set; } = "auto";

    /// <summary>Carpeta donde se guardan el historial y los ajustes (puede cambiarse con MISHELL_DATA_DIR).</summary>
    public string DataDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiShell");

    /// <summary>Ruta del archivo history.json.</summary>
    public string HistoryFilePath => Path.Combine(DataDirectory, "history.json");

    /// <summary>Ruta del archivo settings.json.</summary>
    public string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");

    /// <summary>Cambia el tema en memoria; Save lo guarda en disco.</summary>
    public void SetTheme(string theme) => Theme = theme;

    /// <summary>Lee los ajustes del archivo; si no existe o está dañado devuelve los valores predeterminados.</summary>
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

    /// <summary>Guarda los ajustes en JSON; un fallo de permisos no interrumpe la sesión.</summary>
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
