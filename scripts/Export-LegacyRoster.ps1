param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9.-]+$')][string]$HostName,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_]+$')][string]$Database,
    [ValidateRange(1,65535)][int]$Port = 3306,
    [ValidatePattern('^[1-9][0-9]{0,9}$')][string]$SourceYearId,
    [ValidatePattern('^[a-z0-9][a-z0-9-]{0,39}$')][string]$SourceKey,
    [string]$PhpPath = 'C:/xampp/php/php.exe',
    [switch]$Inspect
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $Inspect -and (-not $SourceYearId -or -not $SourceKey)) { throw 'Selecciona SourceYearId y SourceKey antes de extraer.' }
if (-not (Test-Path -LiteralPath $PhpPath)) { throw 'Indica la ruta del ejecutable PHP CLI con PDO mysql.' }
$oldDsn = $env:LEGACY_DSN; $oldUser = $env:LEGACY_DB_USER; $oldPassword = $env:LEGACY_DB_PASSWORD
try {
    $env:LEGACY_DSN = "mysql:host=$HostName;port=$Port;dbname=$Database;charset=utf8mb4"
    $env:LEGACY_DB_USER = Read-Host 'Usuario MariaDB de solo lectura'
    $secret = Read-Host 'Contraseña MariaDB de solo lectura' -AsSecureString
    $env:LEGACY_DB_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
    if ($Inspect) {
        & $PhpPath (Join-Path $PSScriptRoot 'Export-LegacyRoster.php') --inspect
    } else {
        $privateDirectory = Join-Path $repo '.local/migration'
        New-Item -ItemType Directory -Path $privateDirectory -Force | Out-Null
        $userGrant = '*' + [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value + ':(OI)(CI)F'
        & icacls.exe $privateDirectory '/inheritance:r' '/grant:r' $userGrant '*S-1-5-18:(OI)(CI)F' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo restringir acceso a la carpeta privada.' }
        $outputFile = Join-Path $privateDirectory ("raw-" + [Guid]::NewGuid().ToString('N') + '.json')
        & $PhpPath (Join-Path $PSScriptRoot 'Export-LegacyRoster.php') "--year=$SourceYearId" "--source=$SourceKey" "--output=$outputFile"
        if ($LASTEXITCODE -eq 0) { Write-Output "Archivo privado: $outputFile" }
    }
    if ($LASTEXITCODE -ne 0) { throw 'Extracción rechazada; no se produjo un archivo completo.' }
} finally {
    $env:LEGACY_DSN = $oldDsn; $env:LEGACY_DB_USER = $oldUser; $env:LEGACY_DB_PASSWORD = $oldPassword
}
