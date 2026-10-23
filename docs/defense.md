# Preparar la demostración de clase

Guía para ejecutar y explicar el proyecto. La defensa y la participación de cada integrante no quedan verificadas por pruebas de software.

## Antes de presentar

1. Explicar la elección de C# para Windows autorizada por el docente; consultar ADR-001.
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

## Distribución preparada para la defensa

Usar el orden de integrantes de la portada:

| Integrante | Parte y demostración | Tiempo orientativo |
|---|---|---|
| Primero | Problema, objetivos, lectura, parser, comillas y errores | 3 minutos |
| Segundo | Creación, ejecución y espera de procesos; pipes y redirecciones | 4 minutos |
| Tercero | Jobs, cancelación, historial, variables, temas, pruebas y conclusiones | 4 minutos |

Todos deben explicar una orden desde entrada hasta finalización. Esta distribución prepara la exposición; no acredita participación ya realizada.

## Informe actualizado

El informe oficial `Informe_Final_MiShell_Shell_Simple.docx` está en la carpeta de clases. Se actualizaron arquitectura, jobs, 110 pruebas, build completo, opciones, colores, evidencias, índice y referencias. El original se conserva en el respaldo externo.

Las figuras representan transcripciones verificadas; se diferencian de capturas de ventanas. Se preservaron la portada institucional y su fecha histórica; el cuerpo identifica la fecha de actualización.

El plan de preparación está cerrado. Para presentar, utilizar la verificación reproducible y el recorrido anterior.
