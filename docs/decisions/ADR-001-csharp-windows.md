# ADR-001 Usar C# y .NET sobre Windows

## Estado

Aceptada. El equipo confirmó el 8 de octubre de 2026 que el docente autorizó expresamente C#.

## Contexto y decisión

La propuesta recomienda C# para Windows y utiliza C y nombres POSIX en otras secciones. La autorización del docente resuelve el lenguaje. MiShell utiliza C#/.NET 10 para demostrar creación, ejecución, espera, comunicación y terminación de procesos.

`ProcessStartInfo` y `Process.Start` inician programas; `WaitForExitAsync` espera; los streams conectan stdout/stdin; `Process.Kill` solicita terminar procesos y sus árboles.

## Consecuencias

Se conserva Windows, PerformanceCounter y WMI. No hace falta migrar el proyecto.

La explicación distingue `fork`, `execve` y `waitpid` de sus correspondencias conceptuales en Windows. La aprobación del lenguaje no modifica las semánticas técnicas de esas interfaces.

Cada job conserva todos los procesos y flujos; fg devuelve el código final y los fallos muestran su causa.
