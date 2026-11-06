param(
    [Parameter(Mandatory)][string]$ExportFile,
    [Parameter(Mandatory)][string]$OutputFile,
    [Parameter(Mandatory)][Guid]$TargetYearId,
    [Parameter(Mandatory)][string]$TargetYearLabel,
    [Parameter(Mandatory)][DateTime]$EnrolledOn,
    [Parameter(Mandatory)][string]$RoleKey
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$outputTarget = [System.IO.Path]::GetFullPath($OutputFile)
$privateRoots = @('.local', 'staging') | ForEach-Object { [System.IO.Path]::GetFullPath((Join-Path $repo $_)) + [System.IO.Path]::DirectorySeparatorChar }
if (-not @($privateRoots | Where-Object { $outputTarget.StartsWith($_, [System.StringComparison]::OrdinalIgnoreCase) }).Count) {
    throw 'Guarda el padrón únicamente dentro de .local/ o staging/ de esta instalación; ambas rutas están ignoradas por Git.'
}
if (Test-Path -LiteralPath $OutputFile) { throw 'El archivo de revisión ya existe; no se sobrescribe.' }
$raw = Get-Content -LiteralPath $ExportFile -Raw | ConvertFrom-Json
if ($raw.formatVersion -ne 1 -or $raw.artifactKind -ne 'raw-roster-export-needs-review') { throw 'Exportación no compatible.' }
$institution = Get-Content -LiteralPath (Join-Path $repo 'config/institution.json') -Raw | ConvertFrom-Json
$localConfig = Join-Path $repo 'config/institution.local.json'
if (Test-Path -LiteralPath $localConfig) {
    $local = Get-Content -LiteralPath $localConfig -Raw | ConvertFrom-Json
    if ($local.Institution.SchoolId) { $institution.Institution.SchoolId = $local.Institution.SchoolId }
}
$people = @(); $candidates = @(); $aliases = @(); $enrollments = @()
foreach ($s in $raw.students) {
    $id = [string]$s.legacyStudentId
    $key = "student-$id"
    $candidates += [ordered]@{ legacyStudentId = $id; familyLegacyId = $(if ($s.familyLegacyId) { [string]$s.familyLegacyId } else { $null }); decision = 'pending'; personKey = $key; reason = '' }
    $people += [ordered]@{ key = $key; givenNames = ''; familyNames = ''; birthDate = $s.birthDate; studentCode = $s.studentCodeCandidate;
        profiles = @('student'); contacts = @(); pendingUsername = "m-$([Guid]::NewGuid().ToString('N').Substring(0,20))"; distinctPersonConfirmed = $false }
    $aliases += [ordered]@{ entity = 'student'; legacyId = $id; discriminator = 'person'; personKey = $key }
    $enrollments += [ordered]@{ studentKey = $key; academicYearId = $TargetYearId.ToString(); gradeLevelId = [Guid]::Empty.ToString(); sectionId = $null;
        enrolledOn = $EnrolledOn.ToString('yyyy-MM-dd'); reviewed = $false }
}
$families = @($candidates | ForEach-Object { $_.familyLegacyId } | Where-Object { $_ } | Sort-Object -Unique | ForEach-Object {
    [ordered]@{ legacyFamilyId = $_; decision = 'pending'; personKeys = @(); reason = '' }
})
$review = [ordered]@{
    formatVersion = 2
    manifest = [ordered]@{ batchId = [Guid]::NewGuid().ToString(); schoolId = $institution.Institution.SchoolId; source = $raw.source;
        sourceYearId = [string]$raw.sourceYearId; sourceYearLabel = $raw.sourceYearLabel; exportedAt = $raw.exportedAt;
        sourceSha256 = (Get-FileHash -LiteralPath $ExportFile -Algorithm SHA256).Hash.ToLowerInvariant();
        scope = 'initial-students-guardians-enrollments-pending-accounts'; targetYearId = $TargetYearId.ToString(); targetYearLabel = $TargetYearLabel }
    candidates = $candidates; families = $families; people = $people; legacyAliases = $aliases; relationships = @(); enrollments = $enrollments
    accountPreparation = [ordered]@{ mode = 'pending-without-secrets'; roleKey = $RoleKey; requireEmptyEffectiveAdministrativePermissions = $true;
        isActive = $false; passwordHash = $null; issueActivationCodes = $false; sendMessages = $false }
}
$json = $review | ConvertTo-Json -Depth 20
$stream = [System.IO.File]::Open($outputTarget, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
try { $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($json); $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
Write-Output 'Borrador creado. Requiere separar nombres, decidir continuidad/familias y confirmar grados/fechas. El importador rechaza pendientes.'
