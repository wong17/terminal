# Limpieza del repositorio

Se inicializó Git porque el directorio no tenía metadatos. El remoto indicado no devolvía ramas ni tags al inspeccionarlo.

## Conservación

Se conservan fuentes, seis proyectos, pruebas, solución, demo, verificación y documentación. Las dependencias se restauran desde los proyectos; no se publican DLL ni copias de paquetes.

Antes de limpiar se creó una carpeta hermana **`terminal-archive-20261008`**, fuera del repositorio. Su `manifest.json` enumera el traslado:

| Origen | Archivos preservados |
|---|---:|
| `tmp` | 309 |
| `evidence` | 14 |
| Cuatro scripts históricos | 4 |
| Informe DOCX y PDF | 2 |
| **Total** | **329** |

Los documentos son un informe académico de 32 páginas, distinto de la consigna de seis páginas; no se borraron. El respaldo conserva también informes intermedios y figuras bajo `tmp` para no perder trabajo único.

Los scripts históricos usaban rutas personales y generaban informes/figuras. `scripts/verify.ps1` registra ejecución real con rutas relativas y perfil aislado. El respaldo no se publica.

## Exclusiones

`.gitignore` excluye `bin`, `obj`, `.vs`, temporales, resultados, evidencias, `.mish-demo` y configuración personal. `artifacts/validation` conserva localmente el resultado de esta ejecución.

Se retiraron los directorios de compilación y caché de la copia de trabajo después de verificar. La compilación volverá a crear `bin`/`obj`; esos archivos quedan excluidos de Git. No se añadió una licencia sin decisión del propietario.

## Grupos publicados

1. Base de fuentes limpia y exclusiones.
2. Correcciones de parser, E/S, EOF, cancelación y procesos.
3. Colores, ayuda y detalle de comandos.
4. Demo, verificación y documentación.

La publicación usa una rama normal y no sobrescribe historia mediante force push.
