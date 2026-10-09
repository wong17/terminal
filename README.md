# MiShell

MiShell es un shell educativo para Windows desarrollado en C# y .NET 10. Su objetivo es mostrar, de forma visible, la relación entre el usuario, el intérprete de comandos, los procesos, los flujos de entrada/salida y los recursos del sistema operativo.

## Inicio rápido

Requisitos:

- Windows 10/11.
- .NET 10 SDK.

Desde la raíz del proyecto:

```powershell
dotnet run --project .\MiShell.App\MiShell.App.csproj
```

Dentro del shell se puede ejecutar la demostración completa con:

```text
source demo.mish
```

El script usa `cmd.exe` y `findstr.exe` para mostrar procesos externos y tuberías con herramientas disponibles en Windows.

## Verificación

```powershell
dotnet build .\MiShell.slnx
dotnet test .\MiShell.slnx
```

## Funcionalidades

### Requeridas

- Prompt y lectura de comandos.
- Comandos externos mediante `System.Diagnostics.Process`.
- `cd`, `pwd`, `clear`, `cls` y `exit`.
- Manejo de errores de sintaxis, comandos inexistentes, rutas y permisos.
- Manejo de directorios con `ls`, `dir` y `mkdir`.

### Adicionales

- Redirección de entrada `<` y salida `>` / `>>`.
- Tuberías `|` entre procesos externos.
- Segundo plano `&`, consulta `jobs`, espera `fg` y terminación `kill`.
- Historial persistente y navegación con las flechas arriba/abajo.
- Variables de entorno con `set NOMBRE=valor` y expansión `$NOMBRE`.
- Scripts `.mish` mediante `source archivo.mish`; las líneas que comienzan con `#` son comentarios.
- `ps`, `ps --chart`, `sysinfo`, temas, banner y autocompletado.

## Arquitectura

```text
MiShell.Core/             Parser, AST, ejecución, jobs, historial y contratos
MiShell.Commands/         Comandos internos, archivos, procesos y scripts
MiShell.UI/               Prompt, editor de línea, temas y Spectre.Console
MiShell.Configuration/    Configuración persistente
MiShell.App/              Composición de servicios y ciclo REPL
MiShell.Core.Tests/       Pruebas del parser, historial y tabla de jobs
```

Los comandos internos implementan `IBuiltinCommand`. El núcleo no depende de Spectre.Console: toda salida pasa por `IOutputRenderer`. La decisión de usar C#/.NET sobre Windows y la correspondencia con los conceptos POSIX de la consigna están registradas en [ADR-001](docs/decisions/ADR-001-csharp-windows.md).

## Correspondencia con la consigna

La propuesta menciona `fork()`, `exec()` y `wait()/waitpid()` porque describe el modelo POSIX. En Windows, MiShell usa las APIs equivalentes de .NET:

| Concepto de la propuesta | Implementación de MiShell |
|---|---|
| Crear proceso / `fork()` conceptual | `Process.Start` sobre Windows `CreateProcess` |
| Ejecutar programa / `exec()` conceptual | `ProcessStartInfo` + `Process.Start` |
| Esperar / `wait()` o `waitpid()` conceptual | `WaitForExitAsync` |
| Pipes | `RedirectStandardInput` / `RedirectStandardOutput` y copia entre streams |
| Terminar proceso | `Process.Kill` |

No se afirma que C# invoque literalmente `fork()` en Windows. Esta distinción debe explicarse durante la defensa; si el docente exige código C POSIX literal, habría que cambiar el backend o confirmar formalmente la aceptación de la opción C# recomendada en la propuesta.

## Plan de cierre

La matriz completa, con requisitos obligatorios, extras, evidencias y pendientes del documento técnico, está en [PLAN_DE_CUMPLIMIENTO.md](PLAN_DE_CUMPLIMIENTO.md).
