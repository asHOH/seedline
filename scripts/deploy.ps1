param([Parameter(Mandatory = $true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$game = (Resolve-Path -LiteralPath $GameDir).Path
$assembly = Join-Path $game 'data_sts2_windows_x86_64/sts2.dll'
if ((Get-FileHash -LiteralPath $assembly).Hash -ne 'DDCEB87905B6096DEB7BE975922E24A53992453675DF26DF545B1C46B1826C5D') {
    throw 'Deployment requires v0.114.0 build a7b9a9c0.'
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
