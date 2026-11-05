param([int]$Port = 5080, [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    if (-not $SkipBuild) {
        & "$PSScriptRoot/Invoke-Web.ps1" ci
        if ($LASTEXITCODE -ne 0) { throw 'Falló la instalación del frontend.' }
        & "$PSScriptRoot/Invoke-Web.ps1" run build
        if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del frontend.' }
    }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    & dotnet run --project src/SchoolPlatform.Host --no-launch-profile --urls "http://localhost:$Port"
} finally { Pop-Location }
