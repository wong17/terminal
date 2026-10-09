using System.Text.RegularExpressions;

namespace MiShell.Core.Environment;

/// <summary>Variables de entorno respaldadas por las del proceso, con soporte de <c>$VAR</c>.</summary>
public sealed partial class ProcessEnvironment : Contracts.IShellEnvironment
{
    [GeneratedRegex(@"\$([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex VariablePattern();

    public string? GetVariable(string name) => System.Environment.GetEnvironmentVariable(name);

    public void SetVariable(string name, string value) => System.Environment.SetEnvironmentVariable(name, value);

    public string Expand(string text) =>
        VariablePattern().Replace(text, m => GetVariable(m.Groups[1].Value) ?? m.Value);

    public IEnumerable<(string Name, string Value)> EnumerateVariables() =>
        System.Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Select(e => ((string)e.Key, (string?)e.Value ?? string.Empty))
            .OrderBy(v => v.Item1, StringComparer.OrdinalIgnoreCase);
}
