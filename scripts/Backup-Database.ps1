<#
.SYNOPSIS
  Copy-only backup of this installation's database, with checksum verification and an optional restore test.

.DESCRIPTION
  - Writes a COPY_ONLY backup with CHECKSUM (does not alter the log/differential chain of the production plan).
  - Verifies the media with RESTORE VERIFYONLY.
  - With -VerifyRestore, restores into a new database named SchoolPlatform_RestoreCheck_<random>, runs DBCC CHECKDB,
    reports the applied migrations and drops ONLY that temporary database (its name is validated before dropping).
  The SQL Server service account must be able to write the destination folder; by default the instance backup folder.
  This script never modifies the source database and never touches the legacy PHP/MariaDB system.

.EXAMPLE
  ./scripts/Backup-Database.ps1 -Database SchoolPlatform_Development -VerifyRestore
#>
param(
    [Parameter(Mandatory = $true)][string]$Database,
    [string]$Server = 'localhost',
    [string]$Destination = '',
    [switch]$VerifyRestore
)
$ErrorActionPreference = 'Stop'
if ($Database -notmatch '^[A-Za-z0-9_]{1,100}$') { throw 'Nombre de base inválido.' }

function Invoke-Sql([string]$Query, [string]$Catalog = 'master') {
    $output = & sqlcmd -S $Server -E -C -b -d $Catalog -h -1 -W -Q "SET NOCOUNT ON; $Query" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL Server devolvió un error: $output" }
    return ($output | Where-Object { $_ -and $_.Trim() }) -join "`n"
}

if ((Invoke-Sql "SELECT COUNT(*) FROM sys.databases WHERE name = N'$Database'") -ne '1') { throw "La base $Database no existe en $Server." }
if (-not $Destination) { $Destination = Invoke-Sql "SELECT CONVERT(nvarchar(400), SERVERPROPERTY('InstanceDefaultBackupPath'))" }
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$file = Join-Path $Destination "$($Database)_$stamp.bak"

Write-Host "Respaldando $Database en $file ..."
Invoke-Sql "BACKUP DATABASE [$Database] TO DISK = N'$file' WITH COPY_ONLY, CHECKSUM, INIT, NAME = N'$Database copy-only $stamp'" | Out-Null
Invoke-Sql "RESTORE VERIFYONLY FROM DISK = N'$file' WITH CHECKSUM" | Out-Null
Write-Host 'Respaldo verificado (checksum).'

if ($VerifyRestore) {
    $check = 'SchoolPlatform_RestoreCheck_' + [guid]::NewGuid().ToString('N')
    $dataPath = Invoke-Sql "SELECT CONVERT(nvarchar(400), SERVERPROPERTY('InstanceDefaultDataPath'))"
    $files = & sqlcmd -S $Server -E -C -b -d master -h -1 -W -s '|' -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'$file'" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "No se pudo leer la lista de archivos: $files" }
    $moves = @()
    $index = 0
    foreach ($line in $files | Where-Object { $_ -match '\|' }) {
        $parts = $line.Split('|')
        $logical = $parts[0].Trim()
        $extension = if ($parts[2].Trim() -eq 'L') { 'ldf' } else { 'mdf' }
        $moves += "MOVE N'$logical' TO N'$(Join-Path $dataPath "$check`_$index.$extension")'"
        $index++
    }
    try {
        Write-Host "Restaurando en la base temporal $check ..."
        Invoke-Sql "RESTORE DATABASE [$check] FROM DISK = N'$file' WITH $($moves -join ', '), CHECKSUM, RECOVERY" | Out-Null
        Invoke-Sql "DBCC CHECKDB ([$check]) WITH NO_INFOMSGS" | Out-Null
        $identity = Invoke-Sql "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory" $check
        $school = Invoke-Sql "IF OBJECT_ID('school.__EFMigrationsHistory') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM school.__EFMigrationsHistory" $check
        Write-Host "Restauración correcta: DBCC CHECKDB sin errores; migraciones Identity=$identity, School=$school."
    }
    finally {
        if ($check -match '^SchoolPlatform_RestoreCheck_[a-f0-9]{32}$' -and (Invoke-Sql "SELECT COUNT(*) FROM sys.databases WHERE name = N'$check'") -eq '1') {
            Invoke-Sql "ALTER DATABASE [$check] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$check]" | Out-Null
            Write-Host "Base temporal $check eliminada."
        }
    }
}
Write-Host "Archivo de respaldo: $file"
