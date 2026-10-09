# ADR-001: Usar C#/.NET sobre Windows para MiShell

## Estado

Aceptado con verificación pendiente del criterio literal “Código en C”.

## Fecha

2026-09-10

## Contexto

La propuesta del curso permite C# (.NET), Java, Python o C++ y recomienda C# para Windows. En otras secciones, sin embargo, pide un entregable “desarrollado en lenguaje C”, califica “Código en C” y usa los nombres POSIX `fork()`, `exec()` y `wait()`.

El proyecto debe ejecutarse en Windows y demostrar creación, ejecución, espera, pipes, redirección y manejo de procesos. El equipo eligió C# por su integración con Windows y por la facilidad de explicar las APIs de procesos y streams.

## Decisión

MiShell se implementa en C#/.NET 10 para Windows. La implementación usa:

- `Process.Start` y `ProcessStartInfo` para iniciar programas mediante el mecanismo de procesos de Windows.
- `WaitForExitAsync` para esperar la terminación.
- `Process.Kill` para finalizar procesos.
- `RedirectStandardInput` y `RedirectStandardOutput` para pipes y redirecciones.

La documentación distinguirá expresamente entre el concepto POSIX y su equivalente Windows. No se afirmará que el programa invoca literalmente `fork()`, `exec()` o `wait()`.

## Alternativas consideradas

### C en Linux/WSL

Ventaja: permitiría usar literalmente `fork`, `exec` y `wait` y coincidiría con la rúbrica POSIX.

Rechazo actual: se aleja de la elección del usuario y de la recomendación explícita de C# para Windows; además, cambiar de backend exigiría rehacer el proyecto.

### C# multiplataforma

Ventaja: conservaría el lenguaje elegido.

Rechazo actual: `PerformanceCounter` y `System.Management` se han diseñado para la experiencia Windows del proyecto; se documenta Windows como plataforma objetivo.

## Consecuencias

- El shell cumple la intención funcional de procesos, espera, pipes y redirecciones en Windows.
- La defensa debe explicar `CreateProcess`/`Process.Start` como la correspondencia de Windows, no como una llamada POSIX literal.
- Existe un riesgo académico residual si el docente exige literalmente código C pese a la lista de lenguajes permitidos; debe confirmarse antes de imprimir.
- El README, la matriz de cumplimiento y el documento técnico deben conservar esta distinción.
