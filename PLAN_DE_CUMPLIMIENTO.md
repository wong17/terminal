# Auditoría de cumplimiento de la propuesta de clase

Revisión del **8 de octubre de 2026**, contra `Propuesta #1.pdf`, seis páginas. El PDF aporta criterios académicos; sus fechas históricas no programan acciones.

El docente autorizó C#, según la confirmación directa del equipo. La implementación, limpieza, mejoras y actualización del informe están cerradas dentro del alcance Windows. La evaluación presencial corresponde al curso.

## Programa y extras

| Criterio y página | Implementación y evidencia |
|---|---|
| Interacción usuario/SO (pp. 1, 6) | REPL, parser/AST, ejecución y resultado; guía técnica y diagrama |
| Prompt y lectura (p. 2) | contrast/paper/plain, edición, historial y Tab; EOF, Ctrl+C y Ctrl+Z comprobados |
| Externos (p. 2) | PATH/rutas, ArgumentList y procesos reales en pruebas y demo |
| cd, pwd, clear y exit (pp. 2–3) | Directorio compartido, salida a archivo, limpieza interactiva y cierre con código |
| Creación, ejecución y espera (pp. 2–3, 5) | APIs Windows/.NET autorizadas; espera de procesos y flujos; POSIX como teoría |
| Errores (pp. 2, 5) | Sintaxis, comandos, archivos, permisos, opciones y jobs; sesión recuperable |
| ls, mkdir y echo (p. 3) | Internos implementados; ls -l añade bytes, fecha y resumen |
| Terminación (p. 1) | kill PID, prefijo o %job; todas las etapas propias comprobadas |
| Pipes y E/S (pp. 3, 5) | Varias etapas externas, <, >, >>; cat/type leen stdin; tablas e internos redirigen sin ANSI |
| Archivos protegidos | Origen/destino iguales rechazados; bloqueo, ACL y fallo de escritura comprobados |
| Background (p. 3) | Todos los procesos/flujos, jobs/fg/kill; estado, código y causa |
| Historial y variables (p. 3) | JSON, flechas, set y expansión; comillas simples/dobles diferenciadas |
| Scripts y recursos | source con ubicación/ciclos; ps con filtro/orden/unidades; sysinfo cancelable y una consulta RAM |

Las tuberías y `&` se limitan a externos; los internos operan en la sesión. `&` no se combina con `&&`/`||`. MiShell implementa el subconjunto descrito en README.

## Catorce apartados del informe

Se revisó el informe oficial `Informe_MiShell_APA7_final.docx` indicado por el usuario, distinto de los archivos históricos archivados durante la limpieza.

| Apartado requerido (p. 2) | Cierre |
|---|---|
| Portada | Datos y formato institucional conservados |
| Introducción | Alcance y autorización de C# explícitos |
| Objetivos | General y específicos coherentes con la implementación |
| Justificación | Relación con la actividad académica conservada |
| Marco teórico | Shell, SO, procesos, POSIX, flujos, espera y códigos; stderr actualizado |
| Requisitos | 48 funcionales y 7 no funcionales reconciliados con resultados actuales |
| Diseño y estructura | Propiedad de toda la tubería, referencias reales, resolver y editor |
| Comandos | Ayuda, ls -l, ps, sysinfo, temas, %job y scripts actualizados |
| Llamadas al sistema | Correspondencia conceptual con APIs Windows autorizadas |
| Diagrama | Propiedad, espera y cierre de procesos y flujos |
| Pruebas y resultados | 110/110; Release sin advertencias; demo 12/12; transcripciones actuales |
| Conclusiones | Ajustadas a evidencia comprobada |
| Recomendaciones | Ayuda, temas, protección de datos y demo reproducible |
| Bibliografía | Lectura actual de stderr y letras de citas reconciliadas |

Campos e índice actualizados con Word, páginas renderizadas revisadas y código del informe con contraste corregido. El original tiene respaldo externo; el DOCX y la QA quedan fuera de Git.

## Rúbricas y defensa (pp. 4–5)

Funciones, arquitectura, relación con SO, errores, pruebas y comandos tienen evidencia en el informe y el repositorio. La distribución de la exposición está preparada en `docs/defense.md` por el orden de integrantes de la portada.

La revisión interactiva se realizó en pseudoterminal; las figuras son transcripciones representadas. Las pruebas no acreditan asistencia, exposición, impresión física o calificación.

## Evidencia

[Validación](docs/validation.md), [guía técnica](docs/technical-guide.md), [defensa](docs/defense.md) y [estado del plan](PLAN.md). Logs y perfiles de prueba están en `artifacts/`, ignorados por Git.

## Cambios del 22 de octubre de 2026

- `ls | findstr.exe txt`: un comando interno puede ser la primera etapa de una tubería (ejemplo `ls | grep txt` de la demostración obligatoria).
- `trace on|off`: muestra la creación, el PID, la espera y el código de cada proceso, para evidenciar crear, ejecutar y esperar durante la defensa.
- Editor: comando coloreado según exista, sugerencia gris del historial (la flecha derecha o Fin la acepta), `help` agrupado y tablas con título propio.
- Informe reescrito: planteamiento del problema, marco teórico ampliado, diagrama usuario, kernel y recursos, análisis de resultados, conclusiones y recomendaciones.
