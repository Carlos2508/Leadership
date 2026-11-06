param(
    [Parameter(Mandatory)][string]$File,
    [ValidateSet('plan', 'apply', 'reconcile')][string]$Mode = 'plan',
    [string]$PlanSha256,
    [Parameter(Mandatory)][string]$Actor,
    [Parameter(Mandatory)][string]$Database,
    [ValidateSet('Development', 'Staging', 'Production')][string]$Environment = 'Development',
    [string]$HostDll
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if ($Mode -eq 'apply' -and $PlanSha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Apply requiere el PlanSha256 del plan revisado.' }
$resolvedFile = (Resolve-Path -LiteralPath $File).Path
if (-not $HostDll) { $HostDll = Join-Path $repo 'src/SchoolPlatform.Host/bin/Release/net10.0/SchoolPlatform.Host.dll' }
if (-not (Test-Path -LiteralPath $HostDll)) { throw 'Compila Release o indica -HostDll del artefacto preparado.' }
$HostDll = (Resolve-Path -LiteralPath $HostDll).Path
$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$oldPassword = $env:ROSTER_OPERATOR_PASSWORD
$oldTotp = $env:ROSTER_OPERATOR_TOTP
try {
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    $secret = Read-Host 'Contraseña del operador' -AsSecureString
    $env:ROSTER_OPERATOR_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
    $second = Read-Host 'Código del autenticador si corresponde; Enter si no tiene MFA' -AsSecureString
    $env:ROSTER_OPERATOR_TOTP = [System.Net.NetworkCredential]::new('', $second).Password
    Push-Location (Join-Path $repo 'src/SchoolPlatform.Host')
    try {
        & dotnet $HostDll --import-roster "--Roster:File=$resolvedFile" "--Roster:Mode=$Mode" "--Roster:Actor=$Actor" "--Roster:Database=$Database" "--Roster:PlanSha256=$PlanSha256"
        if ($LASTEXITCODE -ne 0) { throw "Operación no completada sin observaciones (salida $LASTEXITCODE). Revisa el código emitido." }
    } finally { Pop-Location }
} finally {
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
    $env:ROSTER_OPERATOR_PASSWORD = $oldPassword
    $env:ROSTER_OPERATOR_TOTP = $oldTotp
}
