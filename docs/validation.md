# Validación de la versión de octubre

Ejecutada el **8 de octubre de 2026 (America/Guatemala)**. Windows 10, versión 10.0.19045; .NET SDK 10.0.401; runtime 10.0.12.

## Resultado

| Comprobación | Resultado |
|---|---|
| Build inicial Debug | 0 errores, 0 advertencias |
| Suite inicial | 14 aprobadas, 0 fallidas |
| Build completo final Release | 0 errores, 0 advertencias |
| Suite final Release | **110 aprobadas, 0 fallidas, 0 omitidas** |
| Demo aislada con `-IncludeSystemInfo` | **12/12 comprobaciones aprobadas**, salida de error vacía |
| Edición en pseudoterminal | Tab completó pwd y una ruta entre comillas con espacios; flecha arriba recuperó una orden; clear, temas, orden larga y Ctrl+Z funcionaron |
| Ctrl+C en pseudoterminal | Canceló `Start-Sleep -Seconds 30`, mostró código 130 y permitió ejecutar otra orden |
| Diff | `git diff --check` sin problemas |

La pseudoterminal comprueba comportamiento de entrada/salida. No es una captura visual de Windows Terminal y no confirma contraste con todas las paletas, fuentes ni geometrías.

## Qué cubren las pruebas

- Parser: comillas, argumentos vacíos, concatenación de fragmentos, variables, operadores, cadenas, redirecciones y rechazo de sintaxis incompleta.
- Historial: persistencia y errores/corrupción.
- Jobs: búsqueda inexistente, espera real de una tubería, código de salida y terminación por `%id`.
- Ejecución: contenido real de archivos de salida, anexado, stdin, tablas, tres etapas, consumidor que cierra antes, comando posterior inexistente y cancelación con comprobación de finalización del PID.
- Sesiones de aplicación: EOF sin `exit`, código de salida, errores recuperables, ayuda legible, scripts con ubicación y conservación del fallo, ciclos de scripts.
- Listados y procesos: tamaño y fecha, filtro sin resultados y rechazo de opciones inválidas.

Se desactiva la ejecución paralela de este proyecto de pruebas porque algunos casos utilizan `Console.In` y el contexto de consola del mismo proceso.

## Qué comprueba la demo

1. Código final 0.
2. stderr vacío.
3. Salida sin ANSI.
4. Proceso externo y pipe con texto esperado.
5. Expansión de variable.
6. Contenido tras redirección de entrada/salida.
7. Dos líneas después de anexar.
8. Archivo completo de la tubería background al finalizar `fg`.
9. Directorio final esperado.
10. Ayuda específica de `ps`.
11. Columnas del listado detallado.

Además, la ejecución con `-IncludeSystemInfo` mostró CPU, RAM y disco sin errores de contadores/WMI. Los recursos dependen de la instalación y permisos del equipo; una consulta fallida comunica el error.

## Repetir

```powershell
pwsh -File .\scripts\verify.ps1 -IncludeSystemInfo
```

El script compila Release, ejecuta la suite y usa un perfil y un directorio temporal independientes. Guarda `build.log`, `tests.log`, `tests.trx`, `demo.stdout.txt`, `demo.stderr.txt` y `summary.json` en `artifacts/validation/`. Ese directorio no se publica: puede contener rutas y datos del equipo.

Para repetir solo la demo tras compilar: `pwsh -File .\scripts\verify.ps1 -SkipBuild`.

## Límites de la evidencia

El docente aceptó C# según la confirmación del equipo. La verificación se limita a Windows en este equipo; no acredita todas las ACL, procesos descendientes o paletas posibles. La exposición, asistencia, impresión física y evaluación corresponden al curso.

## Cierre del plan

- E/S: rechaza entrada/salida iguales y destinos duplicados sin pérdida de datos; archivo bloqueado y ACL de escritura denegada comprobados sobre temporales propios.
- Background: un escritor simulado falla mientras un proceso real sigue activo; el proceso termina, la causa se conserva y fg libera el job.
- Tab: rutas con espacios/comillas y PATH actualizado comprobados en pruebas; ruta real completada en pseudoterminal.
- sysinfo: cancelación durante muestreo, recursos indisponibles y consulta real satisfactoria.
- Informe oficial actualizado, índice recalculado con Word y páginas renderizadas revisadas.

Las figuras representan transcripciones de sesiones reales aisladas, separando stdout y stderr; no son capturas de Windows Terminal. El renderizador no encontró LibreOffice: Word actualizó campos y exportó la QA, y pypdfium2 generó las páginas revisadas. La portada y convenciones institucionales se preservaron.

Se corrigió la referencia de stderr a [StreamReader.ReadLineAsync](https://learn.microsoft.com/en-us/dotnet/api/system.io.streamreader.readlineasync?view=net-10.0), empleado por el código actual.
