#requires -Version 7.0
[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$IncludeSystemInfo
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = Join-Path $projectRoot 'artifacts\validation'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Push-Location -LiteralPath $projectRoot
try {
    if (-not $SkipBuild) {
        dotnet build MiShell.slnx -c Release --nologo 2>&1 | Tee-Object -FilePath (Join-Path $outputRoot 'build.log')
        if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación.' }
        dotnet test MiShell.slnx -c Release --no-build --no-restore --nologo --logger "trx;LogFileName=tests.trx" --results-directory $outputRoot 2>&1 | Tee-Object -FilePath (Join-Path $outputRoot 'tests.log')
        if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas.' }
    }

    $appPath = Join-Path $projectRoot 'MiShell.App\bin\Release\net10.0\MiShell.App.dll'
    if (-not (Test-Path -LiteralPath $appPath)) { throw 'Compila Release antes de usar -SkipBuild.' }
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    $workPath = Join-Path $tempRoot ('MiShell.Verify-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $workPath | Out-Null
    $process = $null
    try {
        Copy-Item -LiteralPath (Join-Path $projectRoot 'demo.mish') -Destination (Join-Path $workPath 'demo.mish')
        $start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
        $start.ArgumentList.Add($appPath)
        $start.WorkingDirectory = $workPath
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardInput = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.StandardInputEncoding = [Text.UTF8Encoding]::new($false)
        $start.StandardOutputEncoding = [Text.Encoding]::UTF8
        $start.StandardErrorEncoding = [Text.Encoding]::UTF8
        $start.Environment['MISHELL_DATA_DIR'] = Join-Path $workPath 'profile'
        $start.Environment['NO_COLOR'] = '1'
        $process = [Diagnostics.Process]::Start($start)
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $commands = @(
            'source demo.mish',
            'pwd > pwd.txt',
            'help ps > help-ps.txt',
            'ls -l .mish-demo > listing.txt',
            'ps 3 --sort pid',
            'ps --chart --name missing_mishell_process_name',
            'clear'
        )
        if ($IncludeSystemInfo) { $commands += 'sysinfo' }
        $commands += 'exit'
        foreach ($command in $commands) { $process.StandardInput.WriteLine($command) }
        $process.StandardInput.Close()
        if (-not $process.WaitForExit(30000)) { throw 'La demo excedió 30 segundos.' }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        Set-Content -LiteralPath (Join-Path $outputRoot 'demo.stdout.txt') -Value $stdout -Encoding utf8NoBOM
        Set-Content -LiteralPath (Join-Path $outputRoot 'demo.stderr.txt') -Value $stderr -Encoding utf8NoBOM

        $checks = [ordered]@{
            ExitCode = $process.ExitCode -eq 0
            NoErrors = [string]::IsNullOrWhiteSpace($stderr)
            PlainOutput = -not $stdout.Contains([char]27)
            ExternalPipeline = $stdout.Contains('Hola desde un proceso externo')
            EnvironmentVariable = $stdout.Contains('Plataforma: Windows desde MiShell')
            InputAndOutput = (Get-Content -LiteralPath (Join-Path $workPath '.mish-demo\filtrado.txt') -Raw).Trim() -eq 'Hola desde MiShell'
            InternalPipeline = (Get-Content -LiteralPath (Join-Path $workPath '.mish-demo\archivos-txt.txt') -Raw).Contains('saludo.txt')
            Append = @(Get-Content -LiteralPath (Join-Path $workPath '.mish-demo\saludo.txt')).Count -eq 2
            BackgroundPipeline = (Get-Content -LiteralPath (Join-Path $workPath '.mish-demo\trabajo.txt') -Raw).Trim() -eq 'trabajo terminado'
            WorkingDirectory = (Get-Content -LiteralPath (Join-Path $workPath 'pwd.txt') -Raw).Trim() -eq $workPath
            CommandHelp = (Get-Content -LiteralPath (Join-Path $workPath 'help-ps.txt') -Raw).Contains('--sort ram|cpu|pid')
            DetailedListing = (Get-Content -LiteralPath (Join-Path $workPath 'listing.txt') -Raw).Contains('Modificado')
        }
        $summary = [ordered]@{
            TimestampUtc = [DateTimeOffset]::UtcNow.ToString('o')
            Platform = [Environment]::OSVersion.VersionString
            IncludeSystemInfo = [bool]$IncludeSystemInfo
            Checks = $checks
        }
        $summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputRoot 'summary.json') -Encoding utf8NoBOM
        $failed = @($checks.Keys | Where-Object { -not $checks[$_] })
        if ($failed.Count -gt 0) { throw "Falló la demo: $($failed -join ', '). Revisa artifacts/validation/demo.stderr.txt." }
        Write-Host "Demo: $($checks.Count)/$($checks.Count) comprobaciones correctas."
        Write-Host "Resultados locales: $outputRoot"
    }
    finally {
        if ($null -ne $process) {
            if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
            $process.Dispose()
        }
        $resolvedWorkPath = [IO.Path]::GetFullPath($workPath)
        if (-not $resolvedWorkPath.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
            -not [IO.Path]::GetFileName($resolvedWorkPath).StartsWith('MiShell.Verify-', [StringComparison]::Ordinal)) {
            throw 'No se limpia una ruta fuera del directorio temporal de la verificación.'
        }
        Remove-Item -LiteralPath $resolvedWorkPath -Recurse -Force
    }
}
finally { Pop-Location }
