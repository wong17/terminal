namespace MiShell.Core.Contracts;

/// <summary>Acceso lectura/escritura a las variables de entorno del shell.</summary>
public interface IShellEnvironment
{
    string? GetVariable(string name);

    void SetVariable(string name, string value);

    /// <summary>Expande <c>$VAR</c> dentro de un token; deja tal cual si no existe.</summary>
    string Expand(string text);

    IEnumerable<(string Name, string Value)> EnumerateVariables();

    /// <summary>Elimina una variable del entorno del proceso.</summary>
    void RemoveVariable(string name) => SetVariable(name, string.Empty);
}
