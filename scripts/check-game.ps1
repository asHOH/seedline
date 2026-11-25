param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$RitsuLibDir,
    [switch]$RitsuFirst
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$game = (Resolve-Path -LiteralPath $GameDir).Path
$assemblyHash = (Get-FileHash -LiteralPath (Join-Path $game 'data_sts2_windows_x86_64/sts2.dll')).Hash
if ($assemblyHash -ne 'DDCEB87905B6096DEB7BE975922E24A53992453675DF26DF545B1C46B1826C5D') {
    throw 'Checks require v0.114.0 build a7b9a9c0.'
}
$suite = if ($RitsuLibDir) { 'ritsu' } else { 'standalone' }
$hostDir = Join-Path $repo ".cache/$suite-host"
$runDir = Join-Path $repo (".cache/$suite-checks/" + [guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$settingsDir = Join-Path $runDir 'SlayTheSpire2/default/1'
New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
$modOrder = if ($RitsuFirst) { @('STS2-RitsuLib', 'Seedline', 'Seedline.Checks') } else { @('Seedline', 'STS2-RitsuLib', 'Seedline.Checks') }
$settings = @{
    schema_version = 8
    mod_settings = @{ mods_enabled = $true; mod_list = @($modOrder | ForEach-Object { @{ id = $_; source = 1; is_enabled = $true } }) }
    skip_intro_logo = $true
    seen_ea_disclaimer = $true
    language = 'eng'
}
$settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $settingsDir 'settings.save') -Encoding utf8

# Keep the installed game and its saves outside the test host.
if ($game -ne $hostDir) {
    & (Join-Path $PSScriptRoot 'copy-game-files.ps1') -GameDir $game -HostDir $hostDir
}
# Rebuild the isolated host's mod set so previous installations cannot load alongside this build.
$hostMods = Join-Path $hostDir 'mods'
if (Test-Path -LiteralPath $hostMods) { Remove-Item -LiteralPath $hostMods -Recurse -Force }
foreach ($name in 'Seedline', 'Seedline.Checks') {
    $modDir = Join-Path $hostDir "mods/$name"
    New-Item -ItemType Directory -Path $modDir -Force | Out-Null
    $sourceDir = if ($name -eq 'Seedline') { 'src/Seedline' } else { 'tests/Seedline.Checks' }
    Copy-Item -LiteralPath (Join-Path $repo "$sourceDir/bin/Release/net9.0/$name.dll"), (Join-Path $repo "$sourceDir/$name.json") -Destination $modDir -Force
}

if ($RitsuLibDir) {
    $ritsuPackage = (Resolve-Path -LiteralPath $RitsuLibDir).Path
    $manifest = Get-Content -LiteralPath (Join-Path $ritsuPackage 'mod_manifest.json') -Raw | ConvertFrom-Json
    # RitsuLib names its v0.113.1-compatible runtime directory 0.113.0.
    if ($manifest.id -ne 'STS2-RitsuLib' -or
        !(Test-Path -LiteralPath (Join-Path $ritsuPackage 'compat/0.113.0/STS2-RitsuLib.dll'))) {
        throw 'Checks require the complete RitsuLib package, including compat/0.113.0.'
    }
    $ritsuTarget = Join-Path $hostDir 'mods/STS2-RitsuLib'
    New-Item -ItemType Directory -Path $ritsuTarget -Force | Out-Null
    Get-ChildItem -LiteralPath $ritsuPackage | Copy-Item -Destination $ritsuTarget -Recurse -Force
}

$start = [System.Diagnostics.ProcessStartInfo]::new()
$start.FileName = Join-Path $hostDir 'SlayTheSpire2.exe'
$start.WorkingDirectory = $hostDir
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$start.Environment['APPDATA'] = $runDir
$start.Environment['LOCALAPPDATA'] = $runDir
$start.Environment['SEEDLINE_CHECK_ROOT'] = $runDir
$start.Environment['SEEDLINE_RITSU_CHECKS'] = if ($RitsuLibDir) { '1' } else { '0' }
$start.Environment['SEEDLINE_RITSU_VERSION'] = if ($RitsuLibDir) { $manifest.version } else { '' }
$start.Environment['SEEDLINE_RITSU_FIRST'] = if ($RitsuFirst) { '1' } else { '0' }
foreach ($argument in '--headless', '--audio-driver', 'Dummy', '--force-steam=off', '--log-file', (Join-Path $runDir 'game.log')) {
    $start.ArgumentList.Add($argument)
}
$process = [System.Diagnostics.Process]::Start($start)
if (!$process.WaitForExit(180000)) {
    $process.Kill()
    throw "Game checks timed out. Inspect $runDir/game.log"
}
$resultPath = Join-Path $runDir 'result.json'
if (!(Test-Path -LiteralPath $resultPath)) { throw "Game checks produced no result. Inspect $runDir/game.log" }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (!$result.success -or $process.ExitCode -ne 0) { throw "Game checks failed: $($result.error) Log: $runDir/game.log" }
Write-Output "$($result.checks.Count) in-engine checks passed. Evidence: $resultPath"
