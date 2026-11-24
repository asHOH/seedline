$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repo ('.cache/verify-deploy-tests/' + [guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path "$fixture/scripts", "$fixture/game path", "$fixture/artifacts/Seedline",
    "$fixture/src/Seedline/bin/Release/net9.0" -Force | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/verify.ps1" -Destination "$fixture/scripts/verify.ps1"
'Fresh assembly' | Set-Content -LiteralPath "$fixture/src/Seedline/bin/Release/net9.0/Seedline.dll"
'{}' | Set-Content -LiteralPath "$fixture/src/Seedline/Seedline.json"
# Run the real verifier with isolated dependencies, without building or deploying to a game.
foreach ($name in 'test-release-package', 'test-workshop-package', 'test-verify-deploy') {
    '' | Set-Content -LiteralPath "$fixture/scripts/$name.ps1"
}
@'
$failure = Get-Content -LiteralPath "$PSScriptRoot/failure.txt"
$global:LASTEXITCODE = if ($args[0] -eq $failure) { 1 } else { 0 }
'@ | Set-Content -LiteralPath "$fixture/dotnet.ps1"
@'
param([string]$GameDir, [string]$RitsuLibDir, [switch]$RitsuFirst)
if ((Get-Content -LiteralPath "$PSScriptRoot/../failure.txt") -eq 'checks') {
    throw 'Simulated game check failure'
}
'@ | Set-Content -LiteralPath "$fixture/scripts/check-game.ps1"
@'
param([string]$GameDir)
$repo = Split-Path -Parent $PSScriptRoot
if ((Get-Content -Raw -LiteralPath "$repo/artifacts/Seedline/Seedline.dll").Trim() -ne 'Fresh assembly' -or
    !(Test-Path -LiteralPath "$repo/artifacts/Seedline/Seedline.json")) {
    throw 'Deployment ran before packaging.'
}
if ((Get-Content -LiteralPath "$repo/failure.txt") -eq 'deploy') { throw 'Simulated deployment failure' }
$GameDir | Set-Content -LiteralPath "$repo/deployed.txt"
'@ | Set-Content -LiteralPath "$fixture/scripts/deploy.ps1"

$pwsh = (Get-Process -Id $PID).Path
foreach ($case in 'default', 'deploy', 'restore', 'format', 'build', 'run', 'checks', 'deploy-failure') {
    $failure = if ($case -eq 'deploy-failure') { 'deploy' } elseif ($case -in 'default', 'deploy') { 'none' } else { $case }
    $failure | Set-Content -LiteralPath "$fixture/failure.txt"
    'Stale assembly' | Set-Content -LiteralPath "$fixture/artifacts/Seedline/Seedline.dll"
    if (Test-Path -LiteralPath "$fixture/deployed.txt") { Remove-Item -LiteralPath "$fixture/deployed.txt" }
    $arguments = @('-NoProfile', '-File', "$fixture/scripts/verify.ps1",
        '-GameDir', "$fixture/game path", '-Dotnet', "$fixture/dotnet.ps1")
    if ($case -ne 'default') { $arguments += '-Deploy' }
    $output = & $pwsh @arguments 2>&1 | Out-String
    $expectedSuccess = $case -in 'default', 'deploy'
    if (($LASTEXITCODE -eq 0) -ne $expectedSuccess) { throw "Unexpected verifier result for ${case}: $output" }
    $deployed = Test-Path -LiteralPath "$fixture/deployed.txt"
    if ($deployed -ne ($case -eq 'deploy')) { throw "Unexpected deployment for $case" }
    if ($deployed -and (Get-Content -LiteralPath "$fixture/deployed.txt") -ne (Resolve-Path -LiteralPath "$fixture/game path").Path) {
        throw 'Deployment received the wrong game directory.'
    }
}
Write-Output 'Verify/deploy checks passed (no game installation changed).'
