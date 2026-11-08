[CmdletBinding()]
param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$backendRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source
$backendProcesses = @()

if (-not $NoBuild) {
    & $dotnetExecutable build (Join-Path $backendRoot 'KorpBackend.slnx') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o backend.' }
}

$runName = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$backendLogDirectory = Join-Path $backendRoot "artifacts/backend-local/$runName"
New-Item -ItemType Directory -Path $backendLogDirectory -Force | Out-Null

try {
    foreach ($service in @(
        @{ Name = 'Stock'; Port = 5213 },
        @{ Name = 'Billing'; Port = 5048 }
    )) {
        $serviceDirectory = Join-Path $backendRoot "src/$($service.Name)Service/$($service.Name)Service"
        $serviceDll = Join-Path $serviceDirectory "bin/Debug/net10.0/$($service.Name)Service.dll"
        if (-not (Test-Path -LiteralPath $serviceDll -PathType Leaf)) {
            throw "DLL nao encontrada: $serviceDll. Execute sem -NoBuild."
        }
        $backendArguments = @(
            ('"' + $serviceDll + '"'),
            '--urls', "http://localhost:$($service.Port)",
            '--environment', 'Development'
        )
        $serviceProcess = Start-Process -FilePath $dotnetExecutable -ArgumentList $backendArguments `
            -WorkingDirectory $serviceDirectory -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $backendLogDirectory "$($service.Name).log") `
            -RedirectStandardError (Join-Path $backendLogDirectory "$($service.Name).error.log")
        $backendProcesses += $serviceProcess
        Write-Host "$($service.Name): http://localhost:$($service.Port) (PID $($serviceProcess.Id))"
    }

    Write-Host "Logs: $backendLogDirectory"
    Write-Host 'Ctrl+C encerra os dois processos iniciados por este script.'
    while ($true) {
        foreach ($serviceProcess in $backendProcesses) {
            $serviceProcess.Refresh()
            if ($serviceProcess.HasExited) {
                throw "Uma API encerrou (PID $($serviceProcess.Id)). Consulte os logs; confira bancos e portas."
            }
        }
        Start-Sleep -Seconds 1
    }
}
finally {
    foreach ($serviceProcess in $backendProcesses) {
        $serviceProcess.Refresh()
        if (-not $serviceProcess.HasExited) {
            Stop-Process -Id $serviceProcess.Id -ErrorAction SilentlyContinue
            $serviceProcess.WaitForExit()
        }
        $serviceProcess.Dispose()
    }
}
