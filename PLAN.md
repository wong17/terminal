# Pendientes de pulido de MiShell

Plan creado antes de las modificaciones el **8 de octubre de 2026**. Se ejecutó primero la revisión contra la propuesta, después las correcciones y limpieza, luego legibilidad/detalle y documentación. Los resultados cerrados están en la matriz y en `docs/validation.md`; aquí permanecen los pendientes.

## Prioridad 1: cierre académico

- [ ] Confirmar con el docente C#/.NET frente a C y `fork`/`exec`/`wait` literales. La propuesta permite C# en p. 1 y exige C en p. 2-3, 5. Si exige POSIX literal, reevaluar alcance antes de migrar.
- [ ] Actualizar una copia del informe DOCX preservado: jobs de toda la tubería, 61 tests, build completo, opciones/colores y evidencias actuales. Revisar referencias, índice y paginación, exportar y comprobar antes de imprimir.
- [ ] Revisar en Windows Terminal el contraste con la paleta/fuente habitual, `clear`, texto y órdenes largas, ajuste de ventana, flechas, Tab, Ctrl+C y Ctrl+Z. Los casos básicos de edición/cancelación ya se comprobaron en pseudoterminal.
- [ ] Guardar capturas de la sesión real y asignar la defensa a los integrantes. La participación y aceptación académica se verifican presencialmente.

## Prioridad 2: mejorar opciones existentes si el uso lo justifica

| Mejora propuesta | Alcance mínimo | Cómo verificar |
|---|---|---|
| Tab con rutas entre comillas y espacios | Conservar prefijos completos y comillas, sin un nuevo parser de lenguaje | Usar un directorio de prueba con espacios, cambiar de ruta y completar |
| Tab para ejecutables de PATH | Consultar nombres junto con internos; sin indexador permanente | PATH pequeño de prueba y actualización después de `set PATH=...` |
| `sysinfo` cancelable durante muestreo | Sustituir la pausa de 300 ms por espera cancelable y reutilizar una consulta de RAM | Cancelar mientras muestrea y comprobar error legible con WMI indisponible |
| Cobertura de bordes de E/S y permisos | Fallos de escritura, entrada y salida al mismo archivo, redirecciones combinadas y ACL de archivos de prueba | Casos aislados, sin tocar permisos ni procesos ajenos |
| Indicador de trabajos con fallo | Conservar claramente código/causa cuando falla una copia de flujos en background | Archivo de destino bloqueado o escritor de prueba que falla |

Priorizar un caso de uso o defecto observado antes de implementar cada fila. Las funciones actuales se documentan como un subconjunto de shell para Windows.

## Límites de alcance

Mantener la arquitectura actual. No añadir GUI, pestañas, plugins, lenguaje completo de Bash, monitor `top`, backend alternativo ni abstracciones por cada comando. Para colores, empezar usando `contrast`, `paper` o `plain`; ajustar la paleta solo después de revisar el fondo real.

No quedan cambios funcionales obligatorios conocidos sin corregir dentro de la opción C# evaluada. Esa afirmación se limita a la evidencia actual y no resuelve el requisito literal C/POSIX ni todos los bordes enumerados arriba.
