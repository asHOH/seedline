$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repo ('.cache/verify-deploy-tests/' + [guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path "$fixture/scripts", "$fixture/game path", "$fixture/artifacts/Seedline",
    "$fixture/src/Seedline/bin/Release/net9.0" -Force | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/verify.ps1" -Destination "$fixture/scripts/verify.ps1"
'Fresh assembly' | Set-Content -LiteralPath "$fixture/src/Seedline/bin/Release/net9.0/Seedline.dll"
'{}' | Set-Content -LiteralPath "$fixture/src/Seedline/Seedline.json"
# Run the real verifier with isolated dependencies, without building or deploying to a game.
foreach ($name in 'test-release-package', 'test-workshop-package', 'test-copy-game-files', 'test-verify-deploy') {
    '' | Set-Content -LiteralPath "$fixture/scripts/$name.ps1"
}
@'
$failure = Get-Content -LiteralPath "$PSScriptRoot/failure.txt"
if ($args[0] -eq 'build' -and $failure -ne 'build') {
    'built' | Set-Content -LiteralPath "$PSScriptRoot/built.signal"
}
if ($args[0] -eq 'format') {
    if (!(Test-Path -LiteralPath "$PSScriptRoot/built.signal")) { throw 'Formatting started before build completed.' }
    'started' | Set-Content -LiteralPath "$PSScriptRoot/format-started.signal"
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (!(Test-Path -LiteralPath "$PSScriptRoot/checks.signal")) {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Formatting did not overlap game checks.' }
        Start-Sleep -Milliseconds 10
    }
    'done' | Set-Content -LiteralPath "$PSScriptRoot/format-done.signal"
}
$global:LASTEXITCODE = if ($args[0] -eq $failure) { 1 } else { 0 }
'@ | Set-Content -LiteralPath "$fixture/dotnet.ps1"
@'
param([string]$GameDir, [string]$RitsuLibDir, [switch]$RitsuFirst)
'started' | Set-Content -LiteralPath "$PSScriptRoot/../checks.signal"
$deadline = [DateTime]::UtcNow.AddSeconds(10)
while (!(Test-Path -LiteralPath "$PSScriptRoot/../format-started.signal")) {
    if ([DateTime]::UtcNow -gt $deadline) { throw 'Game checks did not overlap formatting.' }
    Start-Sleep -Milliseconds 10
}
if ((Get-Content -LiteralPath "$PSScriptRoot/../failure.txt") -eq 'checks') {
    throw 'Simulated game check failure'
}
'@ | Set-Content -LiteralPath "$fixture/scripts/check-game.ps1"
@'
param([string]$GameDir)
$repo = Split-Path -Parent $PSScriptRoot
if (!(Test-Path -LiteralPath "$repo/format-done.signal")) { throw 'Deployment ran before formatting completed.' }
if ((Get-Content -Raw -LiteralPath "$repo/artifacts/Seedline/Seedline.dll").Trim() -ne 'Fresh assembly' -or
    !(Test-Path -LiteralPath "$repo/artifacts/Seedline/Seedline.json")) {
    throw 'Deployment ran before packaging.'
}
if ((Get-Content -LiteralPath "$repo/failure.txt") -eq 'deploy') { throw 'Simulated deployment failure' }
$GameDir | Set-Content -LiteralPath "$repo/deployed.txt"
'@ | Set-Content -LiteralPath "$fixture/scripts/deploy.ps1"

# Keep verifier environment/location changes out of the caller with one child for all cases.
@'
param([string]$Fixture)
$ErrorActionPreference = 'Stop'
$originalLocation = Get-Location
$environment = @{}
foreach ($name in 'STS2_GAME_DIR', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE', 'DOTNET_CLI_HOME', 'NUGET_PACKAGES') {
    $environment[$name] = [Environment]::GetEnvironmentVariable($name)
}
$cases = [ordered]@{
    default = $null
    restore = 'Restore failed.'
    format = 'Formatting check failed.'
    build = 'Build failed.'
    run = 'Foundation tests failed.'
    checks = 'Simulated game check failure'
    'deploy-failure' = 'Simulated deployment failure'
    deploy = $null
}
foreach ($case in $cases.Keys) {
    Set-Location $originalLocation
    foreach ($name in $environment.Keys) { [Environment]::SetEnvironmentVariable($name, $environment[$name]) }
    $global:LASTEXITCODE = 0
    Get-ChildItem -LiteralPath $fixture -Filter '*.signal' -File | Remove-Item
    $failure = if ($case -eq 'deploy-failure') { 'deploy' } elseif ($case -in 'default', 'deploy') { 'none' } else { $case }
    $failure | Set-Content -LiteralPath "$fixture/failure.txt"
    'Stale assembly' | Set-Content -LiteralPath "$fixture/artifacts/Seedline/Seedline.dll"
    if (Test-Path -LiteralPath "$fixture/deployed.txt") { Remove-Item -LiteralPath "$fixture/deployed.txt" }
    $arguments = @{ GameDir = "$fixture/game path"; Dotnet = "$fixture/dotnet.ps1" }
    if ($case -ne 'default') { $arguments.Deploy = $true }
    $failureMessage = $null
    try { & "$fixture/scripts/verify.ps1" @arguments | Out-Null }
    catch { $failureMessage = $_.Exception.Message }
    if (Get-Job) { throw "Formatting job was not cleaned up after $case" }
    if ($failureMessage -ne $cases[$case] -or ($null -eq $cases[$case] -and $LASTEXITCODE -ne 0)) {
        throw "Unexpected verifier result for ${case}: exit code $LASTEXITCODE, error $failureMessage"
    }
    $deployed = Test-Path -LiteralPath "$fixture/deployed.txt"
    if ($deployed -ne ($case -eq 'deploy')) { throw "Unexpected deployment for $case" }
    if ($deployed -and (Get-Content -LiteralPath "$fixture/deployed.txt") -ne (Resolve-Path -LiteralPath "$fixture/game path").Path) {
        throw 'Deployment received the wrong game directory.'
    }
}
Write-Output 'All 8 cases passed.'
'@ | Set-Content -LiteralPath "$fixture/run-cases.ps1"
$pwsh = (Get-Process -Id $PID).Path
$output = & $pwsh -NoProfile -File "$fixture/run-cases.ps1" -Fixture $fixture 2>&1 | Out-String
if ($LASTEXITCODE -or $output.Trim() -ne 'All 8 cases passed.') { throw "Verify/deploy checks failed: $output" }
Write-Output 'Verify/deploy checks passed (no game installation changed).'
