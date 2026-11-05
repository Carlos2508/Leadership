param([switch]$WithoutSql)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    & dotnet restore --locked-mode --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Falló restore .NET.' }
    if ($WithoutSql) {
        Write-Warning 'Se omite integración SQL por solicitud explícita; no se valida persistencia.'
        & dotnet test --no-restore --filter 'Category!=SqlServer' --verbosity quiet
    } else { & dotnet test --no-restore --verbosity quiet }
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron pruebas .NET.' }
    & "$PSScriptRoot/Invoke-Web.ps1" ci
    if ($LASTEXITCODE -ne 0) { throw 'Falló npm ci.' }
    & "$PSScriptRoot/Invoke-Web.ps1" run build
    if ($LASTEXITCODE -ne 0) { throw 'Falló build web.' }
    if ($WithoutSql) {
        Write-Warning 'Se omiten también las pruebas E2E de cuentas: requieren su base SQL aislada.'
    } else {
        & "$PSScriptRoot/Invoke-Web.ps1" test
        if ($LASTEXITCODE -ne 0) { throw 'Fallaron pruebas E2E.' }
    }
} finally { Pop-Location }
