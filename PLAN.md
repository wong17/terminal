# Plan de pulido de MiShell

Auditoría iniciada el 8 de octubre de 2026 contra las seis páginas de la propuesta del curso. Este plan distingue trabajo de código, documentación y validación presencial.

## Orden y alcance

1. **Cumplimiento funcional:** corregir sintaxis incompleta, fin de entrada, redirecciones y ciclo de vida de procesos; cubrir los fallos con pruebas que ejecuten procesos reales de Windows. Conservar C#/.NET y explicar la ambigüedad de la consigna hasta conocer la decisión del docente.
2. **Limpieza:** conservar fuentes, pruebas, demo y documentación útil. Excluir compilados, cachés, configuración personal y resultados generados. Archivar documentos académicos y herramientas antiguas antes de retirarlos de la raíz.
3. **Legibilidad y detalle:** corregir el mapeo ANSI de colores claros/oscuros; ofrecer alto contraste y salida sin color; añadir help por comando, listados detallados, filtros sencillos de procesos y jobs que reflejen la tubería completa.
4. **Evidencias y documentación:** actualizar la matriz contra la propuesta, instrucciones reproducibles, guía de defensa y resultados actuales. No presentar el informe histórico ni imágenes reconstruidas como evidencia de esta versión.
5. **Publicación:** comprobar el remoto, inicializar Git si sigue vacío y agrupar cambios en commits de base limpia, correcciones, legibilidad y documentación. Subir a https://github.com/wong17/terminal.git sin sobrescribir historia remota.

## Hallazgos de partida

- Compila sin advertencias; pasan 14 pruebas del parser, historial y consulta de jobs. No cubren ejecución real de tuberías o redirecciones.
- El parser no distingue operadores entre comillas y acepta operadores sin comando siguiente.
- El REPL no sale al recibir EOF: una entrada redirigida sin exit puede repetir el prompt indefinidamente.
- Los comandos internos redirigen Console.Out, pero las tablas de Spectre mantienen su propia salida; hay que comprobar el archivo real.
- Los jobs guardan únicamente el último proceso y la ejecución libera recursos antes de finalizar el trabajo completo.
- fg descarta el código de salida; kill confunde identificadores de jobs con PID.
- El mapeo ANSI convierte gris claro en gris oscuro y omite colores del prompt.
- tmp, bin, obj y .vs reúnen cientos de archivos generados. Los documentos de la raíz son un informe académico de 32 páginas, no copias de la propuesta de seis páginas.
- No existe .git; la consulta inicial del remoto no devuelve referencias.

## Pendientes verificables

- [ ] Corregir parser, EOF, redirecciones y gestión de procesos; validar con pruebas de integración.
- [ ] Archivar artefactos históricos y añadir exclusiones.
- [ ] Mejorar colores y detalle de comandos existentes.
- [ ] Ejecutar build, pruebas y demo desde una carpeta temporal.
- [ ] Actualizar matriz de cumplimiento y guía técnica/defensa.
- [ ] Publicar commits agrupados y verificar la rama remota.
- [ ] Confirmar aceptación académica de C#/.NET frente a C/POSIX literal.
- [ ] Revisar una sesión real en Windows Terminal: flechas, Tab, Ctrl+C, tamaño de ventana y temas.
- [ ] Reconciliar el documento impreso con esta versión y confirmar participación de cada integrante.

## Mejoras posteriores acotadas

- Autocompletado de rutas con espacios y ejecutables en PATH, si la sesión real revela dificultades.
- Números de línea y contexto de errores en source, conservando scripts simples.
- Muestreo de recursos sin bloquear el teclado, si se identifica una necesidad real.

No se propone migrar la arquitectura, añadir plugins, pestañas, una GUI, un lenguaje de scripting completo, top ni un segundo backend.
