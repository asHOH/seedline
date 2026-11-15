param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
$env:STS2_GAME_DIR = (Resolve-Path -LiteralPath $GameDir).Path
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_HOME = Join-Path $repo '.cache/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $repo '.cache/nuget'

& $Dotnet restore SameSeed.slnx --nologo -m:1
if ($LASTEXITCODE) { throw 'Restore failed.' }
& $Dotnet format SameSeed.slnx --verify-no-changes --no-restore
if ($LASTEXITCODE) { throw 'Formatting check failed.' }
& $Dotnet build SameSeed.slnx -c Release --no-restore --nologo -m:1
if ($LASTEXITCODE) { throw 'Build failed.' }
& (Join-Path $PSScriptRoot 'check-game.ps1') -GameDir $env:STS2_GAME_DIR

$package = Join-Path $repo 'artifacts/SameSeed'
New-Item -ItemType Directory -Path $package -Force | Out-Null
Copy-Item -LiteralPath 'src/SameSeed/bin/Release/net9.0/SameSeed.dll', 'src/SameSeed/SameSeed.json' -Destination $package
$unexpected = Get-ChildItem -LiteralPath $package -File | Where-Object Name -NotIn 'SameSeed.dll', 'SameSeed.json'
if ($unexpected) { throw 'Package contains unexpected files.' }
Write-Output "Stage 1 checks passed. Package: $package"
