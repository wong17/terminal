using MiShell.Core.Parsing;

namespace MiShell.Core.Execution;

/// <summary>Ejecuta una línea ya parseada.</summary>
public interface IShellExecutor
{
    Task<int> ExecuteAsync(CommandLine commandLine, CancellationToken cancellationToken);
}

/// <summary>Localiza un ejecutable por nombre buscando en el PATH de Windows.</summary>
public interface IExecutableResolver
{
    string? Resolve(string name);
}
