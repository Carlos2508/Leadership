param([ValidateSet('Development', 'Staging', 'Production')][string]$Environment = 'Development')
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    $env:Bootstrap__Username = Read-Host 'Usuario del primer gestor de accesos'
    $secret = Read-Host 'Contraseña (mínimo 12 caracteres, mayúscula, minúscula, número y símbolo)' -AsSecureString
    $env:Bootstrap__Password = [System.Net.NetworkCredential]::new('', $secret).Password
    & dotnet run --project src/SchoolPlatform.Host --no-launch-profile -- --provision-owner
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el primer gestor. No se modifican gestores existentes.' }
} finally {
    Remove-Item Env:Bootstrap__Username, Env:Bootstrap__Password -ErrorAction SilentlyContinue
    Pop-Location
}
