# Presentación de MiShell

Archivo final: `Presentacion_MiShell.pptx` (y su PDF) en la carpeta de clases. Son 16 diapositivas hechas con formas nativas de PowerPoint, así que todo el texto y los diagramas se pueden editar.

## Estructura

| Bloque | Diapositivas |
|---|---|
| Inicio | 1 portada con captura de MiShell (`echo Hola mundo`), 2 índice |
| A · Qué buscamos | 3 objetivos y problema, 4 contexto usuario → kernel → recursos |
| B · Cómo está hecho | 5 organización, 6 ciclo REPL con código, 7 parser y AST |
| C · Cómo ejecuta | 8 interno o externo, 9 crear/ejecutar/esperar, 10 tuberías, 11 `&&` y `||`, 12 segundo plano |
| D · Estado y robustez | 13 estado y scripts, 14 errores |
| E · Cierre | 15 conclusiones, 16 preguntas |

La demostración y las pruebas se hacen en vivo en la aplicación, por eso no hay diapositivas de resultados. Guion sugerido para la demo: `pwd`, `ls`, `mkdir`, `cd`, `echo`, `trace on`, `cmd.exe /c echo Hola | findstr.exe Hola`, `ls | findstr.exe txt`, `&`, `jobs`, `fg`, un error y `exit`.

## Qué hace que no parezca una presentación común

- Índice y barra de navegación con hipervínculos (◀, ⌂, ▶) en todas las diapositivas.
- Transición Morph entre diapositivas: los círculos y el anillo del fondo mantienen su nombre y se desplazan entre una diapositiva y otra.
- Animaciones de aparición por pasos que siguen el recorrido de una orden (se avanza con clic o flecha).
- Secciones de PowerPoint, notas del orador en cada diapositiva y texto alternativo en la captura.

## Cómo se generó

La presentación se construyó con `python-pptx`; luego PowerPoint (COM) aplicó animaciones y secciones, y se inyectó la transición Morph en el XML. Las capturas de terminal se obtienen ejecutando MiShell en un pseudoterminal y dibujando la salida en una ventana; no son capturas de Windows Terminal.
