param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$Dotnet = 'dotnet',
    [ValidateRange(1, 10)][int]$Pairs = 3
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$game = (Resolve-Path -LiteralPath $GameDir).Path
if ((Get-FileHash -LiteralPath (Join-Path $game 'data_sts2_windows_x86_64/sts2.dll')).Hash -ne '62ECBE91093E1FA862DE4329AAC865209DCB297804CAF0F0520D92D8C0B4A9C3') {
    throw 'Measurements require v0.113.1 build d09d48e5.'
}
$env:STS2_GAME_DIR = $game
$env:DOTNET_CLI_HOME = Join-Path $repo '.cache/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $repo '.cache/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
foreach ($project in 'src/Seedline/Seedline.csproj', 'research/performance/Seedline.Performance.csproj') {
    & $Dotnet build (Join-Path $repo $project) -c Release --nologo -m:1
    if ($LASTEXITCODE) { throw "Build failed for $project" }
}
$root = Join-Path $repo ('.cache/performance/' + [guid]::NewGuid().ToString())
$hostDir = Join-Path $root 'host'
New-Item -ItemType Directory -Path $hostDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $game 'SlayTheSpire2.exe'), (Join-Path $game 'release_info.json') -Destination $hostDir
# Each invocation owns a fresh host and saves. The installed game is never modified.
Get-ChildItem -LiteralPath $game -File | Where-Object Extension -In '.dll', '.pck' | Copy-Item -Destination $hostDir
Copy-Item -LiteralPath (Join-Path $game 'data_sts2_windows_x86_64') -Destination $hostDir -Recurse
foreach ($entry in @(@('Seedline', 'src/Seedline'), @('Seedline.Performance', 'research/performance'))) {
    $name, $source = $entry
    $target = Join-Path $hostDir "mods/$name"
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo "$source/bin/Release/net9.0/$name.dll"), (Join-Path $repo "$source/$name.json") -Destination $target
}
$metadata = @{
    date = [DateTimeOffset]::UtcNow.ToString('o')
    cpu = (Get-ItemPropertyValue -LiteralPath 'HKLM:/HARDWARE/DESCRIPTION/System/CentralProcessor/0' -Name 'ProcessorNameString').Trim()
    game = 'v0.113.1/d09d48e5'
    mod_sha256 = (Get-FileHash -LiteralPath (Join-Path $hostDir 'mods/Seedline/Seedline.dll')).Hash
    probe_sha256 = (Get-FileHash -LiteralPath (Join-Path $hostDir 'mods/Seedline.Performance/Seedline.Performance.dll')).Hash
    pairs = $Pairs
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'environment.json') -Encoding utf8
Write-Output "Evidence directory: $root"
for ($pair = 0; $pair -lt $Pairs; $pair++) {
    $modes = if ($pair % 2 -eq 0) { @('vanilla', 'seedline') } else { @('seedline', 'vanilla') }
    foreach ($mode in $modes) {
        $runDir = Join-Path $root "$pair-$mode"
        $settingsDir = Join-Path $runDir 'SlayTheSpire2/default/1'
        New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
        @{
            schema_version = 8
            mod_settings = @{ mods_enabled = $true; mod_list = @(
                @{ id = 'Seedline'; source = 1; is_enabled = ($mode -eq 'seedline') },
                @{ id = 'Seedline.Performance'; source = 1; is_enabled = $true }
            ) }
            skip_intro_logo = $true; seen_ea_disclaimer = $true; language = 'eng'
        } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $settingsDir 'settings.save') -Encoding utf8
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = Join-Path $hostDir 'SlayTheSpire2.exe'
        $start.WorkingDirectory = $hostDir
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.WindowStyle = 'Hidden'
        foreach ($name in 'APPDATA', 'LOCALAPPDATA', 'SEEDLINE_PERF_ROOT') { $start.Environment[$name] = $runDir }
        $start.Environment['SEEDLINE_PERF_MODE'] = $mode
        foreach ($argument in '--headless', '--audio-driver', 'Dummy', '--force-steam=off', '--log-file', (Join-Path $runDir 'game.log')) {
            $start.ArgumentList.Add($argument)
        }
        $process = [Diagnostics.Process]::Start($start)
        $deadline = [DateTime]::UtcNow.AddMinutes(10)
        while (!$process.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Timed out. Inspect $runDir/game.log" }
        }
        $last = Get-Content -LiteralPath (Join-Path $runDir 'measurements.jsonl') -Tail 1 | ConvertFrom-Json
        if ($process.ExitCode -ne 0 -or $last.op -ne 'done') { throw "Measurement failed. Inspect $runDir" }
        Write-Output "Completed pair $pair, $mode"
    }
}
Write-Output "Measurements complete: $root"
