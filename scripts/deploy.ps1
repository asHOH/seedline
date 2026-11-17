param([Parameter(Mandatory = $true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$game = (Resolve-Path -LiteralPath $GameDir).Path
$assembly = Join-Path $game 'data_sts2_windows_x86_64/sts2.dll'
if ((Get-FileHash -LiteralPath $assembly).Hash -ne '62ECBE91093E1FA862DE4329AAC865209DCB297804CAF0F0520D92D8C0B4A9C3') {
    throw 'Deployment requires v0.113.1 build d09d48e5.'
}
if (Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue) { throw 'Close the game before deployment.' }
$package = Join-Path $repo 'artifacts/Seedline'
foreach ($name in 'Seedline.dll', 'Seedline.json') {
    if (!(Test-Path -LiteralPath (Join-Path $package $name))) { throw 'Run scripts/verify.ps1 first.' }
}
$destination = Join-Path $game 'mods/Seedline'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $package 'Seedline.dll'), (Join-Path $package 'Seedline.json') -Destination $destination -Force
Write-Output "Deployed to $destination"
