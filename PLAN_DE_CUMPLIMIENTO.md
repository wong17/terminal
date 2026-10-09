# Auditoría de cumplimiento de la propuesta de clase

Revisión del **8 de octubre de 2026**, contra `Propuesta #1.pdf`, seis páginas, del curso Arquitectura de Sistemas Operativos. Las instrucciones del PDF se usan como criterios del trabajo solicitado; sus fechas históricas no programan acciones nuevas.

**Resultado:** la versión C# para Windows demuestra las funciones obligatorias y los extras indicados, dentro del subconjunto descrito abajo. Quedan por cerrar la interpretación C/POSIX de la rúbrica, la puesta al día del documento impreso y la defensa/inspección visual. Esta matriz no concede una calificación ni sustituye al docente.

## 1. Requisitos del programa

| Criterio y página | Estado y alcance | Evidencia actual |
|---|---|---|
| Objetivo: interacción usuario/SO y procesos (p. 1, 6) | Implementado en Windows | Flujo en `docs/technical-guide.md`; demo con procesos reales |
| Prompt (p. 2) | Funcional; aspecto final pendiente en Windows Terminal | `PromptRenderer`, sesión en pseudoterminal |
| Leer comandos (p. 2) | Implementado, incluidos EOF y cancelación de entrada | `LineEditor`, `SessionTests`, pseudoterminal |
| Ejecutar externos (p. 2) | PATH/rutas y argumentos; scripts del SO usan su intérprete | `ExecutableResolver`, `ShellExecutor`, `ExecutionTests` |
| `cd` y directorios (p. 2-3) | Implementado; cambio relativo usado en la demo | `NavigationCommands`, `demo.mish`, verificación del directorio final |
| `pwd` (p. 2-3) | Implementado; salida a archivo comprobada | `PwdCommand`, comprobación `WorkingDirectory` |
| `clear` (p. 2) | Disponible; seguro con salida redirigida. Limpieza visual interactiva pendiente | `ClearCommand`, `SpectreRenderer.ClearScreen`, verificación |
| `exit` (p. 2-3) | Implementado; código opcional y detención de encadenamientos | `ExitStopsRemainingChainedCommands`, `SessionTests` |
| Crear procesos / `fork()` (p. 2-3, 5) | Creación real con `Process.Start`; **no cumplimiento POSIX literal** | `ShellExecutor`; confirmar C# según ADR-001 |
| Ejecutar programas / `exec()` (p. 2-3, 5) | Programas reales con `ProcessStartInfo`; **no reemplazo POSIX del proceso actual** | Tuberías `cmd`/`findstr`; ADR-001 |
| Espera / `wait()`/`waitpid()` (p. 2-3, 5) | Espera con `WaitForExitAsync` y finalización de flujos | Pruebas foreground/background y `fg`; no llamadas POSIX literales |
| Errores básicos (p. 2, 5) | Sintaxis, comando inexistente, rutas/archivos faltantes, opciones inválidas y jobs inexistentes | Parser, sesiones e integración; mensajes de permisos en código. No se probaron todas las ACL |
| `ls`, `mkdir`, `echo`, navegación (p. 3) | Implementados; `ls -l` añade datos sin cálculo recursivo | Demo desde carpeta temporal |
| Finalizar procesos (p. 1) | `kill %job`, PID o prefijo; solicita terminar árboles de procesos del job | `ExplicitJobIdKillsTheEntirePipeline`; no prueba exhaustiva de descendientes |

## 2. Extras y rúbrica de E/S

Aunque p. 3 los presenta como adicionales, p. 5 califica redirección y pipes: se conservan y se prueban.

| Función | Estado y límites | Evidencia |
|---|---|---|
| `<` | Externos y `cat`/`type`; otros internos comunican que no leen stdin | Pruebas de stdin y stdout simultáneos |
| `>` y `>>` | Externos e internos, incluidas tablas; archivos sin ANSI | Contenido real, truncado/anexado y tabla redirigida |
| `\|` | Programas externos; internos rechazados explícitamente | Tres etapas y cierre anticipado del consumidor |
| `&` | Una tubería externa; conserva todos sus procesos y flujos | `BackgroundPipelineKeepsStreamsAndAllProcessesUntilFg` |
| Historial | JSON, máximo 500 entradas y flechas | `HistoryStoreTests`, flecha arriba en pseudoterminal |
| Variables | `set` y expansión al parsear; comillas dobles/simples diferenciadas | Parser, demo y entorno del proceso |
| Scripts | `source`, comentarios por línea, contexto compartido, ubicación de errores y rechazo de ciclos | `SessionTests` y `source demo.mish` |
| Recursos visibles | `jobs`, `ps`, gráfico y `sysinfo` | Demo; contadores/WMI consultados en este equipo |

Las redirecciones explícitas reemplazan el extremo correspondiente del pipe. Se rechazan redirecciones duplicadas del mismo flujo. No se presenta MiShell como compatible con todo Bash ni con un sistema POSIX.

## 3. Documento técnico: catorce apartados

El archivo histórico `Propuesta #1 - Shell simple.pdf` es un **informe de 32 páginas**, distinto de la consigna de seis páginas. Se conserva junto con su DOCX en el respaldo externo. La extracción del texto identifica sus apartados; esto no equivale a revisar la paginación final.

| Apartado exigido (p. 2) | Resultado de revisión | Acción pendiente |
|---|---|---|
| 1. Portada | Presente con integrantes, curso y docente | Confirmar fecha y datos actuales |
| 2. Introducción | Presente | Revisar coherencia con alcance final |
| 3. Objetivos general/específicos | Presentes | Vincular objetivos comprobables a evidencias |
| 4. Justificación | Presente | Conservar si refleja la asignación del proyecto |
| 5. Marco teórico | Shell, SO, procesos, llamadas, fork, exec, wait, pipes y E/S presentes | Conservar distinción entre teoría POSIX y código Windows |
| 6. Requisitos funcionales/no funcionales | Matrices presentes | Reconciliar cobertura y límites de E/S |
| 7. Diseño/estructura | Presente | Cambiar la descripción que solo guarda el último proceso de un job |
| 8. Comandos | Tabla presente | Actualizar ayuda, colores, `ls -l`, `ps` y `kill %id` |
| 9. Llamadas al sistema | Correspondencias presentes | Confirmar interpretación del docente; no declarar fork literal |
| 10. Diagrama | Presente | Incluir espera/propiedad de todos los procesos y flujos |
| 11. Pruebas/resultados | Históricos: 14 tests y build completo no verificado por MSB3491 | Usar 61 tests, build completo actual y demo 11/11 |
| 12. Conclusiones | Presentes | Ajustar a evidencias y pendientes actuales |
| 13. Recomendaciones | Presentes | Tema según fondo, `help comando` y perfil aislado |
| 14. Bibliografía | Presente | Revisar citas; `BeginErrorReadLine` ya no describe la lectura actual de stderr |

El informe original se preservó; no se reescribió ni reexportó. `docs/technical-guide.md` y `docs/validation.md` permiten actualizarlo con datos verificables. La impresión, formato final y evaluación académica siguen abiertos.

## 4. Rúbricas y defensa

| Criterio (p. 4-5) | Evidencia / límite |
|---|---|
| Introducción, problema, objetivos, teoría | Apartados en informe; revisión editorial final pendiente |
| Arquitectura y relación usuario/Shell/kernel | Guía técnica y diagrama; dependencias directas documentadas |
| Descripción de comandos | README, ayuda específica y tabla del informe a reconciliar |
| Llamadas al sistema y código en C | Brecha literal reconocida; C# recomendado en p. 1 y exigencia C en p. 2-3, 5 |
| Interfaz, internos, externos | Demo y pseudoterminal; aspecto final pendiente |
| Procesos, pipes y E/S | Pruebas reales en Windows; no pruebas POSIX |
| Errores y pruebas | 61 casos, demo 11 comprobaciones; permisos no exhaustivos |
| Redacción, formato y bibliografía | Informe conservado; no se revisó visualmente su paginación completa |
| Demostración y participación | Protocolo en `docs/defense.md`; presentación presencial pendiente |

## 5. Evidencia y cierre

Resultados en [docs/validation.md](docs/validation.md). Pendientes vivos en [PLAN.md](PLAN.md). Los resultados generados quedan en `artifacts/validation`, fuera de Git; las cifras resumen la ejecución realizada.

No se modifica la elección histórica de C# ni se añade un backend C sin resolver primero el criterio académico.
