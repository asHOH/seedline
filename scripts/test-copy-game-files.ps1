$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repo ('.cache/copy-game-files-tests/' + [guid]::NewGuid().ToString())
$gameDir = Join-Path $fixture 'game path'
$hostDir = Join-Path $fixture 'host path'
$managedName = 'data_sts2_windows_x86_64'
New-Item -ItemType Directory -Path "$gameDir/$managedName", "$gameDir/mods/Other" -Force | Out-Null
$files = @('SlayTheSpire2.exe', 'release_info.json', 'native.dll', 'game.pck',
    "$managedName/sts2.dll", "$managedName/runtime.json")
foreach ($name in $files + @('notes.txt', 'mods/Other/Other.dll')) {
    [IO.File]::WriteAllText((Join-Path $gameDir $name), 'original')
}
function Copy-TestGame {
    & "$PSScriptRoot/copy-game-files.ps1" -GameDir $gameDir -HostDir $hostDir
}
function Assert-Copied([string]$Name) {
    $source = Get-Item -LiteralPath (Join-Path $gameDir $Name)
    $target = Get-Item -LiteralPath (Join-Path $hostDir $Name)
    if ([IO.File]::ReadAllText($source.FullName) -ne [IO.File]::ReadAllText($target.FullName) -or
        $source.LastWriteTimeUtc -ne $target.LastWriteTimeUtc) {
        throw "Game file was not refreshed: $Name"
    }
}

Copy-TestGame
foreach ($name in $files) { Assert-Copied $name }
if (@(Get-ChildItem -LiteralPath $hostDir -File -Recurse).Count -ne $files.Count) {
    throw 'Test host copied files outside the runtime/assets selection.'
}
# Read-only sharing makes an unnecessary overwrite fail, even if timestamps are preserved.
$locks = @()
try {
    foreach ($name in $files) {
        $locks += [IO.File]::Open((Join-Path $hostDir $name), 'Open', 'Read', 'Read')
    }
    Copy-TestGame
}
finally { foreach ($handle in $locks) { $handle.Dispose() } }

Remove-Item -LiteralPath "$hostDir/native.dll"
Copy-TestGame
Assert-Copied 'native.dll'
[IO.File]::WriteAllText("$gameDir/added.dll", 'new runtime dependency')
Copy-TestGame
Assert-Copied 'added.dll'

foreach ($case in @(
    @{ Name = 'game.pck'; Content = 'different length'; TimeOffset = 0 },
    @{ Name = 'SlayTheSpire2.exe'; Content = 'modified'; TimeOffset = 10 },
    @{ Name = "$managedName/sts2.dll"; Content = 'reverted'; TimeOffset = -10 }
)) {
    $sourcePath = Join-Path $gameDir $case.Name
    $originalTime = (Get-Item -LiteralPath $sourcePath).LastWriteTimeUtc
    [IO.File]::WriteAllText($sourcePath, $case.Content)
    [IO.File]::SetLastWriteTimeUtc($sourcePath, $originalTime.AddSeconds($case.TimeOffset))
    Copy-TestGame
    Assert-Copied $case.Name
}

$lockedPath = Join-Path $hostDir 'game.pck'
$handle = [IO.File]::Open($lockedPath, 'Open', 'Read', 'Read')
try {
    [IO.File]::WriteAllText("$gameDir/game.pck", 'must be copied again')
    $failed = $false
    try { Copy-TestGame } catch { $failed = $true }
    if (!$failed) { throw 'A failed game-file copy was silently accepted.' }
}
finally { $handle.Dispose() }
Write-Output 'Game-file copy checks passed.'
