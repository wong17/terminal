# MiShell

Shell educativo de Windows escrito en C#/.NET 10. Ejecuta comandos internos y programas externos, conecta sus flujos de entrada/salida y administra trabajos básicos.

## Inicio rápido

Requiere Windows 10/11 y el SDK de .NET 10. Para la interfaz interactiva se recomienda Windows Terminal. PowerShell 7 permite ejecutar la verificación del repositorio.

```powershell
dotnet run --project .\MiShell.App\MiShell.App.csproj
```

Dentro de MiShell:

```text
help
help ps
help syntax
ls -l
ps 10 --name dotnet --sort ram
theme contrast
source demo.mish
exit
```

La demo crea `.mish-demo` en el directorio inicial y conserva la sesión abierta. Usa ejecutables de Windows (`cmd.exe`, `findstr.exe`, `powershell.exe`); no requiere herramientas de Unix.

## Colores y lectura

| Opción | Uso |
|---|---|
| `theme contrast` | Fondo oscuro; texto principal blanco, secundario gris claro y acento cian. Predeterminado para perfiles nuevos. |
| `theme paper` | Fondo claro; texto oscuro y acento azul. |
| `theme plain` | Texto sin color, tablas simples y sin animación de bienvenida. |
| `theme list` | Consulta todos los temas; se conservan dracula, synthwave, crt y minimal. |

Los temas no cambian el fondo de tu terminal. El resultado depende de la paleta y fuente configuradas allí; el color se acompaña de texto, símbolos y códigos. La preferencia existente del usuario se conserva. `NO_COLOR=1` desactiva el color de MiShell; los programas externos conservan su propio comportamiento. Los archivos redirigidos no reciben colores ni bordes.

## Sintaxis y límites

```text
echo Hola > saludo.txt
echo Otra linea >> saludo.txt
cat < saludo.txt
cmd.exe /c echo Hola | findstr.exe Hola > coincidencias.txt
powershell.exe -NoProfile -Command "Start-Sleep -Seconds 2" &
jobs
fg 1
kill %1
set SALUDO="Hola mundo"
echo $SALUDO
```

- `>` reemplaza el archivo; `>>` añade contenido; `<` lee entrada. Se admite una entrada y una salida por comando.
- Las tuberías y `&` conectan **programas externos**. Los internos admiten salida a archivo; `cat`/`type` también entrada desde archivo. `echo Hola | findstr Hola` se rechaza con una explicación.
- `&` solo puede terminar una tubería; no se combina con `&&`/`||`. `fg` espera toda la tubería y devuelve el código del último proceso.
- `kill %1` identifica un job; `kill 1234` identifica un PID. Salir cierra los trabajos que todavía pertenecen a MiShell.
- `&&`/`||` condicionan la ejecución según el código previo. Las variables se expanden al analizar la línea: define y utiliza una variable en líneas separadas.
- Las comillas dobles expanden variables; las simples conservan texto literal. No se implementa el lenguaje completo de Bash, escape con barra invertida, comodines ni sustitución de comandos.
- `.cmd`/`.bat` requieren `cmd.exe /c archivo.cmd`; `.ps1` requiere `powershell.exe -File archivo.ps1`.
- `source` ejecuta líneas y comentarios completos `#`; reporta archivo/línea y conserva un código de fallo si alguna línea falla.
- Ctrl+C cancela el comando actual; mientras escribes, borra esa entrada. Ctrl+Z o EOF termina la sesión. Flechas recuperan el historial y Tab completa nombres internos y rutas sencillas. El autocompletado de PATH y rutas entre comillas queda en el plan.

Configuración e historial se guardan en `%APPDATA%\MiShell`. `MISHELL_DATA_DIR` permite usar un perfil separado, como hacen las pruebas. Las órdenes introducidas se guardan localmente en el historial.

## Verificación

```powershell
dotnet build MiShell.slnx -c Release
dotnet test MiShell.slnx -c Release
pwsh -File .\scripts\verify.ps1
```

Para comprobar además los contadores y WMI del equipo:

```powershell
pwsh -File .\scripts\verify.ps1 -IncludeSystemInfo
```

El script usa una carpeta temporal y un perfil aislado, comprueba archivos y salidas, limita la duración de la demo y guarda resultados en `artifacts/validation/`, excluido de Git. `-SkipBuild` reutiliza una compilación Release ya realizada.

Resultado local del 8 de octubre de 2026: **61 pruebas, 0 fallos; Release con 0 advertencias; demo 11/11**. Consulta los alcances de esa evidencia en [validación](docs/validation.md).

## Organización

```text
MiShell.Core/             Parser, AST, procesos, jobs, historial y contratos
MiShell.Commands/         Comandos internos, archivos, procesos y scripts
MiShell.UI/               Prompt, editor de línea, temas y Spectre.Console
MiShell.Configuration/    Configuración persistente
MiShell.App/              Registro de servicios y ciclo REPL
MiShell.Core.Tests/       Pruebas unitarias, integración de procesos y sesiones
scripts/                 Verificación reproducible
docs/                    Cumplimiento, explicación técnica y defensa
```

Core no referencia Spectre.Console. Commands sí referencia UI y Configuration: la solución separa responsabilidades por proyectos, sin imponer una arquitectura nueva.

## Proyecto de clase

La propuesta permite y recomienda C# para Windows en su primera página, pero luego exige C y `fork`/`exec`/`wait`. MiShell utiliza las APIs de procesos de Windows/.NET y **no invoca esas llamadas POSIX literalmente**. La interpretación académica debe confirmarse; está documentada en [ADR-001](docs/decisions/ADR-001-csharp-windows.md).

- [Matriz completa de cumplimiento](PLAN_DE_CUMPLIMIENTO.md)
- [Explicación técnica del funcionamiento](docs/technical-guide.md)
- [Demostración y revisión del informe impreso](docs/defense.md)
- [Plan de pendientes y mejoras acotadas](PLAN.md)
- [Limpieza y conservación de archivos históricos](docs/cleanup.md)
