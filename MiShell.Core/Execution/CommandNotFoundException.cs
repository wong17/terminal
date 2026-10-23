namespace MiShell.Core.Execution;

/// <summary>Se lanza cuando un ejecutable no se encuentra en el PATH ni como ruta; Shell.RunAsync la convierte en un mensaje.</summary>
public sealed class CommandNotFoundException(string name) : Exception($"Comando no encontrado: '{name}'");
