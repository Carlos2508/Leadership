param([string]$MariaDbBin = 'C:/xampp/mysql/bin', [string]$PhpPath = 'C:/xampp/php/php.exe')
$ErrorActionPreference = 'Stop'
$repo = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixtureBase = Join-Path $repo '.local/qa'
New-Item -ItemType Directory -Path $fixtureBase -Force | Out-Null
$fixtureDirectory = Join-Path $fixtureBase ('SchoolPlatform_MigrationFixture_' + [Guid]::NewGuid().ToString('N'))
$fixtureTarget = [System.IO.Path]::GetFullPath($fixtureDirectory)
if (-not $fixtureTarget.StartsWith($fixtureBase + [System.IO.Path]::DirectorySeparatorChar) -or
    (Split-Path $fixtureTarget -Leaf) -notmatch '^SchoolPlatform_MigrationFixture_[a-f0-9]{32}$') { throw 'Fixture fuera del workspace.' }
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback,0)
$listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
$server = $null
$oldDsn = $env:LEGACY_DSN; $oldUser = $env:LEGACY_DB_USER; $oldPassword = $env:LEGACY_DB_PASSWORD
try {
    & (Join-Path $MariaDbBin 'mysql_install_db.exe') "--datadir=$fixtureTarget" "--port=$port" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Falló la inicialización sintética.' }
    $server = Start-Process -FilePath (Join-Path $MariaDbBin 'mysqld.exe') -ArgumentList @('--no-defaults',
        "--basedir=$([System.IO.Path]::GetFullPath((Join-Path $MariaDbBin '..')))", "--datadir=$fixtureTarget", '--bind-address=127.0.0.1',
        "--port=$port", '--skip-name-resolve', '--console') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $fixtureTarget 'stdout.log') -RedirectStandardError (Join-Path $fixtureTarget 'stderr.log')
    $ready = $false
    for ($attempt=0; $attempt -lt 100; $attempt++) {
        if ($server.HasExited) { throw 'El servidor sintético terminó prematuramente.' }
        $probe = [System.Net.Sockets.TcpClient]::new()
        try { $probe.Connect('127.0.0.1',$port); $ready=$true; break } catch { Start-Sleep -Milliseconds 100 } finally { $probe.Dispose() }
    }
    if (-not $ready) { throw 'El servidor sintético no quedó disponible.' }
    $mysql = Join-Path $MariaDbBin 'mysql.exe'
    $baseArgs = @('--no-defaults','--protocol=TCP','--host=127.0.0.1',"--port=$port",'--user=root','--default-character-set=utf8mb4')
    Get-Content -LiteralPath (Join-Path $PSScriptRoot 'mariadb-fixture.sql') -Raw | & $mysql @baseArgs
    if ($LASTEXITCODE -ne 0) { throw 'Falló el esquema sintético.' }
    $env:LEGACY_DSN = "mysql:host=127.0.0.1;port=$port;dbname=SchoolPlatform_MigrationFixture;charset=utf8mb4"
    $env:LEGACY_DB_USER = 'roster_reader'; $env:LEGACY_DB_PASSWORD = ''
    $outputFile = Join-Path $fixtureTarget 'raw.json'
    & $PhpPath (Join-Path $repo 'scripts/Export-LegacyRoster.php') '--year=42' '--source=synthetic-source' "--output=$outputFile"
    if ($LASTEXITCODE -ne 0) { throw 'Falló extracción sintética.' }
    $rawText = Get-Content -LiteralPath $outputFile -Raw
    $raw = $rawText | ConvertFrom-Json
    if ($raw.students.Count -ne 3 -or $raw.families.Count -ne 1 -or $raw.historicalRelations.Count -ne 2 -or
        $raw.students[0].studentCodeCandidate -cne '000001' -or $raw.students[1].sourceSectionId -ne $null -or
        $raw.families[0].sourceYearId -ne 41 -or $rawText -match 'DO-NOT-EXPORT|unused-1|Fuera De Alcance') { throw 'Proyección o alcance incorrectos.' }
    $reviewPath = Join-Path $fixtureTarget 'review.json'
    & (Join-Path $repo 'scripts/New-RosterReview.ps1') -ExportFile $outputFile -OutputFile $reviewPath -TargetYearId ([Guid]::NewGuid()) -TargetYearLabel '2027' -EnrolledOn '2027-01-11' -RoleKey 'basic'
    $review = Get-Content -LiteralPath $reviewPath -Raw | ConvertFrom-Json
    if ($review.people.Count -ne 3 -or $review.families.Count -ne 2 -or @($review.candidates | Where-Object decision -ne 'pending').Count -ne 0 -or
        $review.people[0].givenNames -ne '' -or $review.accountPreparation.isActive -or $review.accountPreparation.issueActivationCodes -or
        $review.manifest.sourceSha256 -ne (Get-FileHash -LiteralPath $outputFile -Algorithm SHA256).Hash.ToLowerInvariant()) { throw 'Borrador no conserva decisiones pendientes/huella.' }
    # Read-only credentials must reject writes even if a future extractor regresses.
    $readerArgs = @('--no-defaults','--protocol=TCP','--host=127.0.0.1',"--port=$port",'--user=roster_reader','--database=SchoolPlatform_MigrationFixture','--execute=DELETE FROM student WHERE studentID=1')
    $denial = & $mysql @readerArgs 2>&1
    if ($LASTEXITCODE -eq 0) { throw 'El lector tiene permisos de escritura.' }
    & $PhpPath (Join-Path $repo 'scripts/Export-LegacyRoster.php') '--year=999' '--source=synthetic-source' "--output=$(Join-Path $fixtureTarget 'bad-year.json')" 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0 -or (Test-Path -LiteralPath (Join-Path $fixtureTarget 'bad-year.json'))) { throw 'Ciclo desconocido produjo exportación.' }
    Write-Output 'MariaDB real aislado: extracción, alcance, ceros, familias cruzadas, sección nula, confidenciales excluidos, borrador pendiente y rechazo de escritura/ciclo desconocido: correctos.'
} finally {
    $env:LEGACY_DSN = $oldDsn; $env:LEGACY_DB_USER = $oldUser; $env:LEGACY_DB_PASSWORD = $oldPassword
    if ($server -and -not $server.HasExited) { Stop-Process -Id $server.Id -Force; $server.WaitForExit() }
    # Only the exact random directory created above; never another instance or data directory.
    if ($fixtureTarget.StartsWith($fixtureBase + [System.IO.Path]::DirectorySeparatorChar) -and
        (Split-Path $fixtureTarget -Leaf) -match '^SchoolPlatform_MigrationFixture_[a-f0-9]{32}$' -and (Test-Path -LiteralPath $fixtureTarget)) {
        Remove-Item -LiteralPath $fixtureTarget -Recurse -Force
    }
}
