using System.Text.RegularExpressions;

namespace MiShell.Core.Environment;

/// <summary>Variables de entorno respaldadas por las del proceso, con soporte de <c>$VAR</c>.</summary>
public sealed partial class ProcessEnvironment : Contracts.IShellEnvironment
{
    [GeneratedRegex(@"\$([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex VariablePattern();

    /// <summary>Lee una variable del entorno del proceso de la shell.</summary>
    public string? GetVariable(string name) => System.Environment.GetEnvironmentVariable(name);

    /// <summary>Define una variable; la heredan los procesos que se inicien después, pero no se guarda en Windows.</summary>
    public void SetVariable(string name, string value) => System.Environment.SetEnvironmentVariable(name, value);

    /// <summary>Elimina una variable del entorno del proceso.</summary>
    public void RemoveVariable(string name) => System.Environment.SetEnvironmentVariable(name, null);

    /// <summary>Sustituye cada $VARIABLE por su valor; si no existe la deja tal cual.</summary>
    public string Expand(string text) =>
        VariablePattern().Replace(text, m => GetVariable(m.Groups[1].Value) ?? m.Value);

    /// <summary>Todas las variables ordenadas por nombre.</summary>
    public IEnumerable<(string Name, string Value)> EnumerateVariables() =>
        System.Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Select(e => ((string)e.Key, (string?)e.Value ?? string.Empty))
            .OrderBy(v => v.Item1, StringComparer.OrdinalIgnoreCase);
}
