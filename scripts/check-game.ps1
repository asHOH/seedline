param([Parameter(Mandatory = $true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$game = (Resolve-Path -LiteralPath $GameDir).Path
if ((Get-FileHash -LiteralPath (Join-Path $game 'data_sts2_windows_x86_64/sts2.dll')).Hash -ne 'A6CC282C52B96AD2E01D5692B1144B33AA8E3BA4E969582F648ECB13B2F751D7') {
    throw 'Checks require v0.113.0 build 16ac0fe3.'
}
$hostDir = Join-Path $repo '.cache/stage1-host'
$runDir = Join-Path $repo ('.cache/stage1-checks/' + [guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $hostDir, $runDir -Force | Out-Null
$settingsDir = Join-Path $runDir 'SlayTheSpire2/default/1'
New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
@'
{"schema_version":8,"mod_settings":{"mods_enabled":true},"skip_intro_logo":true,"seen_ea_disclaimer":true,"language":"eng"}
'@ | Set-Content -LiteralPath (Join-Path $settingsDir 'settings.save') -Encoding utf8

# Keep the installed game and its saves outside the test host.
Copy-Item -LiteralPath (Join-Path $game 'SlayTheSpire2.exe'), (Join-Path $game 'release_info.json') -Destination $hostDir -Force
foreach ($file in Get-ChildItem -LiteralPath $game -File | Where-Object Extension -In '.dll', '.pck') {
    $target = Join-Path $hostDir $file.Name
    if (!(Test-Path -LiteralPath $target)) { Copy-Item -LiteralPath $file.FullName -Destination $target }
}
$managed = Join-Path $hostDir 'data_sts2_windows_x86_64'
New-Item -ItemType Directory -Path $managed -Force | Out-Null
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $game 'data_sts2_windows_x86_64') -File) {
    $target = Join-Path $managed $file.Name
    if (!(Test-Path -LiteralPath $target)) { Copy-Item -LiteralPath $file.FullName -Destination $target }
}
foreach ($name in 'SameSeed', 'SameSeed.Checks') {
    $modDir = Join-Path $hostDir "mods/$name"
    New-Item -ItemType Directory -Path $modDir -Force | Out-Null
    $sourceDir = if ($name -eq 'SameSeed') { 'src/SameSeed' } else { 'tests/SameSeed.Checks' }
    Copy-Item -LiteralPath (Join-Path $repo "$sourceDir/bin/Release/net9.0/$name.dll"), (Join-Path $repo "$sourceDir/$name.json") -Destination $modDir -Force
}

$start = [System.Diagnostics.ProcessStartInfo]::new()
$start.FileName = Join-Path $hostDir 'SlayTheSpire2.exe'
$start.WorkingDirectory = $hostDir
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$start.Environment['APPDATA'] = $runDir
$start.Environment['LOCALAPPDATA'] = $runDir
$start.Environment['SAMESEED_CHECK_ROOT'] = $runDir
foreach ($argument in '--headless', '--audio-driver', 'Dummy', '--force-steam=off', '--log-file', (Join-Path $runDir 'game.log')) {
    $start.ArgumentList.Add($argument)
}
$process = [System.Diagnostics.Process]::Start($start)
$deadline = [DateTime]::UtcNow.AddMinutes(3)
while (!$process.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $deadline) {
        $process.Kill()
        throw "Game checks timed out. Inspect $runDir/game.log"
    }
}
$resultPath = Join-Path $runDir 'result.json'
if (!(Test-Path -LiteralPath $resultPath)) { throw "Game checks produced no result. Inspect $runDir/game.log" }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (!$result.success -or $process.ExitCode -ne 0) { throw "Game checks failed: $($result.error) Log: $runDir/game.log" }
Write-Output "$($result.checks.Count) in-engine checks passed. Evidence: $resultPath"
