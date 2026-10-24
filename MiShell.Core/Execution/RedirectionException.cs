namespace MiShell.Core.Execution;

/// <summary>Una redirección no pudo abrirse (carpeta inexistente, sin permisos, archivo en uso); el mensaje ya está listo para el usuario.</summary>
public sealed class RedirectionException : Exception
{
    public RedirectionException() { }

    public RedirectionException(string message) : base(message) { }

    public RedirectionException(string message, Exception innerException) : base(message, innerException) { }
}
