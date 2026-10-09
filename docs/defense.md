# Preparar la demostración de clase

Guía para ejecutar y explicar el proyecto. La defensa y la participación de cada integrante no quedan verificadas por pruebas de software.

## Antes de presentar

1. Confirmar aceptación de C# para Windows frente a C/POSIX literal; consultar ADR-001.
2. Ejecutar `pwsh -File .\scripts\verify.ps1 -IncludeSystemInfo`.
3. Elegir `theme contrast` con fondo oscuro o `theme paper` con fondo claro.
4. En Windows Terminal comprobar fuente, colores, texto/órdenes largas, ventana estrecha, flechas, Tab, Ctrl+C, Ctrl+Z y `clear`.
5. Conservar capturas reales. Los logs automatizados son transcripciones, no capturas visuales.

## Recorrido obligatorio

En una carpeta vacía dedicada a la demostración:

```text
pwd
ls
mkdir proyecto
cd proyecto
pwd
echo Hola
cd ..
clear
```

Después:

```text
cmd.exe /c echo Hola
echo Hola > saludo.txt
echo Otra linea >> saludo.txt
cat < saludo.txt
cmd.exe /c echo Hola | findstr.exe Hola
powershell.exe -NoProfile -Command "Start-Sleep -Seconds 2; 'fin'" &
jobs
fg 1
set DEMO="Hola mundo"
echo $DEMO
help ps
ps 5 --sort ram
exit
```

`source demo.mish` ofrece el recorrido reproducible y conserva la sesión abierta. Sus archivos se limitan a `.mish-demo`, pero `>` reemplaza archivos con esos nombres si ya existen: conviene usar una carpeta vacía. Para mostrar `kill`, iniciar otro proceso de prueba y usar `kill %<job-id>`.

Explicar PID, espera en primer plano, conexión stdout/stdin, cierre de etapas y códigos de salida.

## Repartir la explicación

Asignar entre integrantes:

- Lectura, parser, AST, sintaxis y errores.
- Creación/ejecución/espera y diferencias Windows/POSIX.
- Pipes, redirecciones, jobs, historial y variables.
- Pruebas observadas, recursos y límites.

La asignación y asistencia deben registrarse por el equipo; no se inventa participación.

## Actualizar el informe impreso

Conservar el DOCX del respaldo y trabajar sobre una copia. Revisar:

1. Jobs de toda la tubería, con tarea que espera procesos y flujos.
2. Los 61 tests actuales y build completo Release; conservar cifras anteriores solo con sus fechas.
3. Retirar MSB3491 como bloqueo actual; la compilación completa pasó en esta auditoría.
4. Ayuda, `ls -l`, `ps`, `kill %id`, temas y redirección de tablas/cat.
5. Transcripciones actuales y capturas reales, diferenciadas de diagramas e imágenes reconstruidas.
6. Requisitos, conclusiones, recomendaciones y referencias acordes al código final.
7. Índice, tablas, figuras, citas y paginación después de exportar. La revisión de texto no valida impresión ni formato visual.
