# Plan de cumplimiento - Desarrollo de un Shell Simple

Fecha de auditoría: 10 de septiembre de 2026.

Este archivo es el plan de preparación del proyecto. No es todavía el documento técnico impreso del entregable 1.

## 1. Fuentes y alcance

### Requisitos de la propuesta adjunta

La fuente es `Propuesta #1.pdf` (6 páginas), especialmente:

- Páginas 1-3: objetivo, entregables, requisitos mínimos, extras y demostración.
- Página 4: rúbrica del documento técnico.
- Página 5: rúbrica del shell funcional y defensa.
- Página 6: prioridad conceptual Usuario -> Shell -> llamadas al sistema -> kernel -> procesos -> recursos.

La propuesta exige:

1. Documento técnico con portada, introducción, objetivos, justificación, marco teórico, requisitos, diseño, comandos, llamadas al sistema, diagrama, pruebas, conclusiones, recomendaciones y bibliografía.
2. Shell funcional con prompt, lectura de comandos, comandos externos, `cd`, `pwd`, `clear`, `exit`, creación/ejecución/espera de procesos, errores básicos y manejo de directorios.
3. Como ampliación: `<`, `>`, `>>`, pipes, segundo plano, historial, variables de entorno y scripts simples.
4. Demostración de navegación, creación de directorio, `echo`, salida, proceso externo, espera y una tubería.
5. Participación de cada integrante y explicación de la interacción con el sistema operativo.

### Decisiones del usuario

- Lenguaje: C#.
- Plataforma: Windows.
- Prioridad: cerrar primero el shell y sus evidencias; elaborar el documento técnico después.

## 2. Conflicto de lenguaje y decisión

La propuesta contiene dos instrucciones distintas: en la página 1 permite C# (.NET), Java, Python o C++ y recomienda C# para Windows; en las páginas 2 y 5 también dice “lenguaje C” y califica “Código en C”. Además, pide `fork()`, `exec()` y `wait()`, que son nombres POSIX y no llamadas literales de Windows.

Decisión adoptada: conservar C#/.NET sobre Windows porque está permitido y recomendado en la misma propuesta. La implementación explicará las equivalencias de Windows y no presentará `Process.Start` como una llamada literal a `fork()` o `exec()`. El único riesgo externo que queda es que el docente interprete literalmente la frase “código en C”; antes de imprimir, conviene confirmar esa interpretación.

## 3. Matriz de cumplimiento del proyecto

### 3.1 Requisitos mínimos del shell

| Requisito | Estado | Evidencia |
|---|---|---|
| Mostrar prompt | Cumple | `MiShell.UI/Prompt/PromptRenderer.cs`; banner y prompt en ejecución |
| Leer comandos | Cumple | `MiShell.UI/Input/LineEditor.cs`; modo interactivo y entrada redirigida |
| Ejecutar comandos externos | Cumple | `MiShell.Core/Execution/ShellExecutor.cs`; `ProcessStartInfo` + `Process.Start` |
| `cd` | Cumple | `MiShell.Commands/BuiltinCommands/NavigationCommands.cs` |
| `pwd` | Cumple | `MiShell.Commands/BuiltinCommands/NavigationCommands.cs` |
| `clear` | Cumple | Alias añadido; en salida redirigida no aborta la sesión |
| `exit` | Cumple | `MiShell.Commands/BuiltinCommands/InfoCommands.cs` |
| Crear proceso | Cumple conceptualmente en Windows | `Process.Start`, equivalente de Windows a la creación de procesos |
| Ejecutar programa | Cumple conceptualmente en Windows | `ProcessStartInfo` + `Process.Start` |
| Esperar proceso | Cumple conceptualmente en Windows | `WaitForExitAsync` y `fg` |
| Manejar errores básicos | Cumple | errores de sintaxis, comando no encontrado, rutas, permisos y persistencia no disponible |
| Manejo de directorios | Cumple | `cd`, `pwd`, `ls`, `dir`, `mkdir` |

### 3.2 Funcionalidades adicionales

| Funcionalidad | Estado | Evidencia |
|---|---|---|
| Redirección `<` | Cumple | copia archivo a `StandardInput` |
| Redirección `>` | Cumple | archivo truncado y conectado a `StandardOutput` |
| Redirección `>>` | Cumple | archivo abierto en modo append |
| Pipes `\|` | Cumple | conexión de streams entre procesos externos |
| Segundo plano `&` | Cumple | `JobTable`, `jobs`, `fg`, `kill` |
| Historial | Cumple | JSON en `%APPDATA%\MiShell`; falla de persistencia no termina el shell |
| Variables de entorno | Cumple | `set` y expansión `$NOMBRE` |
| Scripts simples | Cumple | `source archivo.mish`, comentarios de línea `#` |

## 4. Evidencias ejecutadas

### Validación estática y automatizada

| Validación | Resultado |
|---|---|
| `dotnet build MiShell.slnx --no-restore` | Correcto, 0 errores y 0 advertencias |
| `dotnet test MiShell.slnx --no-build --no-restore` | Correcto; pruebas del parser, historial y jobs pasan |
| Script `demo.mish` | Ejecutado en Windows; comprobó procesos, pipes, redirecciones, variables, jobs y scripts |
| Smoke test redirigido | Ejecutado; comprobó prompt, navegación, `ls`, `mkdir`, `clear`, lectura de archivo, `exit` y error de comando |

La primera compilación aislada no pudo restaurar NuGet por falta de red del entorno; al repetir con restauración habilitada, el build terminó correctamente. El resultado válido es el segundo.

### Evidencia que falta preparar para la defensa

- Capturas de una sesión interactiva real en Windows Terminal.
- Captura o salida legible de la demo completa.
- Tabla de casos de prueba con entrada, resultado esperado, resultado observado y estado.
- Diagrama Usuario -> MiShell -> .NET/Win32 -> procesos y recursos.
- Explicación verbal por integrante.

## 5. Plan de cierre por fases

### Fase A - Proyecto funcional

- [x] Corregir persistencia de historial/configuración sin permisos.
- [x] Implementar `clear` y aliases útiles para Windows.
- [x] Añadir `mkdir`, lectura de archivos y comandos de demostración.
- [x] Implementar `source` para scripts `.mish` con comentarios `#`.
- [x] Hacer que jobs inexistentes produzcan un error de usuario, no un error interno.
- [x] Crear `demo.mish`.

### Fase B - Verificación y evidencias

- [x] Build sin errores.
- [x] Pruebas automatizadas.
- [x] Smoke test no interactivo.
- [x] Smoke test externo con pipe, redirecciones y job en segundo plano.
- [ ] Repetir la demo en Windows Terminal con sesión interactiva.
- [ ] Guardar capturas y resultados finales.
- [ ] Confirmar con el docente la interpretación C# versus código C literal.

### Fase C - Documento técnico impreso

El documento se elaborará únicamente después de cerrar Fase B y debe seguir este orden:

1. Portada.
2. Introducción y planteamiento del problema.
3. Objetivo general y objetivos específicos medibles.
4. Justificación.
5. Marco teórico: shell, SO, procesos, llamadas al sistema, `fork`, `exec`, `wait`, pipes, redirección y variables.
6. Requisitos funcionales y no funcionales.
7. Diseño y arquitectura de MiShell.
8. Descripción de comandos internos y externos.
9. Equivalencias de llamadas POSIX con APIs de Windows/.NET.
10. Diagrama de funcionamiento.
11. Casos de prueba, evidencias y análisis.
12. Conclusiones.
13. Recomendaciones y limitaciones.
14. Bibliografía.

## 6. Criterio de “listo para documentar”

El proyecto estará listo para el documento cuando build/tests estén verdes, la demo se pueda repetir desde cero en Windows Terminal, cada fila de la matriz tenga evidencia y la decisión C#/.NET esté aceptada o explícitamente justificada frente a la ambigüedad de la propuesta.
