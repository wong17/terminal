using System.ComponentModel;
using System.Diagnostics;

namespace MiShell.Core.Jobs;

/// <summary>
/// Deduce el estado de un proceso a partir del estado de sus hilos, ya que Windows no lo expone directamente:
/// <b>Ejecutando</b> si algún hilo corre o espera CPU, <b>Suspendido</b> si todos están suspendidos y
/// <b>En espera</b> en el resto de casos (bloqueado en E/S, temporizador o evento).
/// </summary>
public static class ProcessStateReader
{
    public const string Running = "Ejecutando";
    public const string Waiting = "En espera";
    public const string Suspended = "Suspendido";
    public const string Unknown = "-";

    /// <summary>Lee el estado; devuelve <c>-</c> si el proceso es protegido o ya terminó.</summary>
    public static string Read(Process process)
    {
        try
        {
            var threads = process.Threads;
            if (threads.Count == 0) return Unknown;
            var suspended = 0;
            foreach (ProcessThread thread in threads)
            {
                var state = thread.ThreadState;
                switch (state)
                {
                    case System.Diagnostics.ThreadState.Running or System.Diagnostics.ThreadState.Ready or System.Diagnostics.ThreadState.Standby:
                        return Running;
                    case System.Diagnostics.ThreadState.Wait when thread.WaitReason == ThreadWaitReason.Suspended:
                        suspended++;
                        break;
                }
            }

            return suspended == threads.Count ? Suspended : Waiting;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return Unknown;
        }
    }
}
