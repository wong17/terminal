using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MiShell.Core.Jobs;

/// <summary>
/// Pausa y reanuda procesos de Windows. Windows no tiene señales SIGSTOP/SIGCONT; el equivalente es
/// <c>NtSuspendProcess</c>/<c>NtResumeProcess</c> de ntdll, que suspenden todos los hilos del proceso.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class ProcessControl
{
    private const uint ProcessSuspendResume = 0x0800;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [LibraryImport("ntdll.dll")]
    private static partial int NtSuspendProcess(nint processHandle);

    [LibraryImport("ntdll.dll")]
    private static partial int NtResumeProcess(nint processHandle);

    /// <summary>Suspende (<paramref name="suspend"/> true) o reanuda un proceso; lanza <see cref="Win32Exception"/> si Windows lo rechaza.</summary>
    public static void Set(int processId, bool suspend)
    {
        var handle = OpenProcess(ProcessSuspendResume, false, processId);
        if (handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var status = suspend ? NtSuspendProcess(handle) : NtResumeProcess(handle);
            if (status != 0) throw new Win32Exception($"Windows rechazó la operación (NTSTATUS 0x{status:X8}).");
        }
        finally { _ = CloseHandle(handle); }
    }
}
