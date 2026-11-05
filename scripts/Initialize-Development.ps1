param([switch]$CreateOperator)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    if ($CreateOperator) {
        $env:Bootstrap__Username = Read-Host 'Usuario del operador local'
        $secret = Read-Host 'Contraseña (12 caracteres mínimo, mayúscula, minúscula, número y símbolo)' -AsSecureString
        $env:Bootstrap__Password = [System.Net.NetworkCredential]::new('', $secret).Password
        & dotnet run --project src/SchoolPlatform.Host --no-launch-profile -- --create-operator
    } else {
        & dotnet run --project src/SchoolPlatform.Host --no-launch-profile -- --initialize-database
    }
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo inicializar el ambiente local.' }
} finally {
    Remove-Item Env:Bootstrap__Username, Env:Bootstrap__Password -ErrorAction SilentlyContinue
    Pop-Location
}
