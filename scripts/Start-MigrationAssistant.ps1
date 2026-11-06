param([switch]$ChooseDestination, [switch]$Rebuild, [switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$private = Join-Path $repo '.local/migration-assistant'
New-Item -ItemType Directory -Path $private -Force | Out-Null
$settingsPath = Join-Path $private 'launch.json'
$settings = if (Test-Path -LiteralPath $settingsPath) { Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json } else { $null }
$connection = $env:ConnectionStrings__SchoolDatabase
if (-not $connection -and $settings -and -not $ChooseDestination) {
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder.DataSource = $settings.server; $builder.InitialCatalog = $settings.database
    $builder.IntegratedSecurity = $true; $builder.Encrypt = $true; $builder.TrustServerCertificate = $true
    $connection = $builder.ConnectionString
}
if (-not $connection -or $ChooseDestination) {
    Write-Host 'Solo la primera vez: indica la base de la plataforma nueva.'
    $server = Read-Host 'Servidor SQL (Enter para localhost)'
    if (-not $server) { $server = 'localhost' }
    $database = Read-Host 'Nombre de la base nueva que ya preparaste'
    if (-not $database) { throw 'Es necesario elegir la base de destino. No se crea una base sin indicarlo.' }
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder.DataSource = $server; $builder.InitialCatalog = $database
    $builder.IntegratedSecurity = $true; $builder.Encrypt = $true; $builder.TrustServerCertificate = $true
    $connection = $builder.ConnectionString
    @{ server=$server; database=$database } | ConvertTo-Json | Set-Content -LiteralPath $settingsPath
}
$dll = Join-Path $repo '.local/migration-tool/SchoolPlatform.Host.dll'
$marker = Join-Path $repo '.local/migration-tool/assistant-version.txt'
$destination = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($connection)
$hostMetadata = Join-Path $private 'host.json'
if (-not $Rebuild -and (Test-Path -LiteralPath $hostMetadata)) {
    try {
        $existing = Get-Content -LiteralPath $hostMetadata -Raw | ConvertFrom-Json
        $existingUrl = [Uri]$existing.url
        $owned = Get-CimInstance Win32_Process -Filter "ProcessId=$([int]$existing.pid)"
        if ($owned -and $owned.CommandLine.Contains($dll) -and $owned.CommandLine.Contains('--migration-assistant') -and
            $existing.server -eq $destination.DataSource -and $existing.database -eq $destination.InitialCatalog -and
            $existingUrl.Scheme -eq 'https' -and $existingUrl.Host -eq 'localhost' -and $existingUrl.AbsolutePath -eq '/workspace/migration') {
            Invoke-WebRequest "https://localhost:$($existingUrl.Port)/health/live" -UseBasicParsing -TimeoutSec 2 | Out-Null
            if (-not $NoBrowser) { Start-Process $existingUrl.AbsoluteUri -WindowStyle Normal }
            Write-Host 'Tu asistente ya estaba abierto. Puedes continuar la revision.'
            return
        }
    } catch { } # A previous process may have ended. Never stop an unverified process.
}
Push-Location $repo
try {
    if ($Rebuild -or -not (Test-Path -LiteralPath $dll) -or -not (Test-Path -LiteralPath $marker)) {
        Write-Host 'Preparando el asistente. Esta primera apertura puede tardar un momento...'
        if (-not (Test-Path -LiteralPath 'web/node_modules')) { & "$PSScriptRoot/Invoke-Web.ps1" ci; if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar la interfaz.' } }
        & "$PSScriptRoot/Invoke-Web.ps1" run build
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo construir la interfaz.' }
        & dotnet publish src/SchoolPlatform.Host/SchoolPlatform.Host.csproj -c Release -o .local/migration-tool
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar el asistente.' }
        Set-Content -LiteralPath $marker -Value 'assistant-v1'
    }
    # HTTPS remains required with real data. Trust only the local .NET development certificate, if necessary.
    & dotnet dev-certs https --check --trust 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Confirma el certificado local para abrir el asistente con una conexion segura.'
        & dotnet dev-certs https --trust | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar HTTPS local.' }
    }
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
    $url = "https://localhost:$port/workspace/migration"
    $priorEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $priorConnection = $env:ConnectionStrings__SchoolDatabase
    $priorExporter = $env:MigrationAssistant__ExporterPath
    $priorStorage = $env:MigrationAssistant__StorageRoot
    try {
        $env:ASPNETCORE_ENVIRONMENT = 'Production'
        $env:ConnectionStrings__SchoolDatabase = $connection
        $env:MigrationAssistant__ExporterPath = Join-Path $repo 'scripts/Export-LegacyRoster.php'
        $env:MigrationAssistant__StorageRoot = $private
        $arguments = '"' + $dll + '" --migration-assistant --urls https://localhost:' + $port
        $process = Start-Process -FilePath dotnet -ArgumentList $arguments -WorkingDirectory (Split-Path $dll) -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $private "host-$port.out.log") -RedirectStandardError (Join-Path $private "host-$port.err.log")
    } finally {
        $env:ASPNETCORE_ENVIRONMENT = $priorEnvironment; $env:ConnectionStrings__SchoolDatabase = $priorConnection
        $env:MigrationAssistant__ExporterPath = $priorExporter; $env:MigrationAssistant__StorageRoot = $priorStorage
    }
    $ready = $false
    for ($i=0; $i -lt 60; $i++) {
        if ($process.HasExited) { throw 'El asistente no pudo iniciar. Revisa que la base elegida este disponible.' }
        try { Invoke-WebRequest "https://localhost:$port/health/live" -UseBasicParsing -TimeoutSec 1 | Out-Null; $ready=$true; break } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { Stop-Process -Id $process.Id; throw 'El asistente no pudo abrir su conexion local.' }
    @{ pid=$process.Id; url=$url; server=$destination.DataSource; database=$destination.InitialCatalog } | ConvertTo-Json | Set-Content -LiteralPath $hostMetadata
    if (-not $NoBrowser) { Start-Process $url -WindowStyle Normal }
    Write-Host 'Asistente abierto. Ingresa con tu cuenta de gestion y sigue los tres pasos.'
    Write-Host $url
} finally { Pop-Location }
