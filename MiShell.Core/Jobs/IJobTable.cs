using System.Diagnostics;

namespace MiShell.Core.Jobs;

/// <summary>La tabla conserva todos los procesos y flujos hasta completar la tubería.</summary>
public sealed class BackgroundJob(int jobId, IReadOnlyList<Process> processes, string commandLine, Task<int> completion) : IDisposable
{
    /// <summary>Identificador del trabajo (el que se usa en %1).</summary>
    public int JobId { get; } = jobId;
    /// <summary>Todos los procesos de la tubería.</summary>
    public IReadOnlyList<Process> Processes { get; } = processes;
    /// <summary>Último proceso de la tubería; su código de salida es el del trabajo.</summary>
    public Process Process => Processes[^1];
    /// <summary>Texto de la orden tal como se escribió.</summary>
    public string CommandLine { get; } = commandLine;
    /// <summary>Momento en que se registró el trabajo.</summary>
    public DateTime StartedAt { get; } = DateTime.Now;
    /// <summary>Tarea que termina cuando acaban los procesos y las copias de flujos; su resultado es el código de salida.</summary>
    public Task<int> Completion { get; } = completion;
    /// <summary>True cuando la tubería ya terminó (con éxito, con fallo o cancelada).</summary>
    public bool HasExited => Completion.IsCompleted;
    /// <summary>corriendo, terminado o fallido.</summary>
    public string Status => !HasExited ? "corriendo" : Killed || Completion.IsCompletedSuccessfully && Completion.Result == 0 ? "terminado" : "fallido";
    /// <summary>Código de salida o causa del fallo para mostrar en jobs.</summary>
    public string Detail => Killed ? "terminado con kill" : Completion.IsFaulted ? Completion.Exception!.GetBaseException().Message
        : Completion.IsCanceled ? "cancelado" : Completion.IsCompletedSuccessfully ? $"código {Completion.Result}" : "en ejecución";

    /// <summary>True si el usuario lo terminó con <c>kill</c>; en ese caso no se avisa de un fallo al finalizar.</summary>
    public bool Killed { get; set; }

    /// <summary>Libera los objetos Process del trabajo.</summary>
    public void Dispose()
    {
        foreach (var process in Processes) process.Dispose();
    }
}

/// <summary>Contrato de la tabla de trabajos que usan el ejecutor y los comandos jobs, fg y kill.</summary>
public interface IJobTable
{
    IReadOnlyList<BackgroundJob> Jobs { get; }
    BackgroundJob Add(Process process, string commandLine);
    BackgroundJob Add(IReadOnlyList<Process> processes, string commandLine, Task<int> completion);
    BackgroundJob? RemoveFirstCompleted();
    bool TryGetByPrefix(string input, out BackgroundJob? job);
    void Remove(BackgroundJob job);
    void Kill(BackgroundJob job);
}
