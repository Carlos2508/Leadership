param([Parameter(ValueFromRemainingArguments = $true)][string[]]$NpmArguments)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$nodeDirectory = Join-Path $repo '.tools/node'
$nodeExecutable = Join-Path $nodeDirectory 'node_modules/node/bin/node.exe'
if (-not (Test-Path -LiteralPath $nodeExecutable)) {
    & npm.cmd install --prefix $nodeDirectory --no-package-lock --no-save node@24.15.0
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar Node 24.15.0.' }
}
$npmCli = Join-Path (Split-Path (Get-Command npm.cmd).Source) 'node_modules/npm/bin/npm-cli.js'
$env:PATH = (Split-Path $nodeExecutable) + [IO.Path]::PathSeparator + $env:PATH
Push-Location (Join-Path $repo 'web')
try { & $nodeExecutable $npmCli @NpmArguments; $result = $LASTEXITCODE }
finally { Pop-Location }
exit $result
