param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$Dotnet = 'dotnet',
    [string]$RitsuLibDir,
    [switch]$Deploy
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
$env:STS2_GAME_DIR = (Resolve-Path -LiteralPath $GameDir).Path
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_HOME = Join-Path $repo '.cache/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $repo '.cache/nuget'

& (Join-Path $PSScriptRoot 'test-release-package.ps1')
& (Join-Path $PSScriptRoot 'test-workshop-package.ps1')
& (Join-Path $PSScriptRoot 'test-copy-game-files.ps1')
& (Join-Path $PSScriptRoot 'test-verify-deploy.ps1')

& $Dotnet restore Seedline.slnx --nologo -m:1
if ($LASTEXITCODE) { throw 'Restore failed.' }
& $Dotnet format Seedline.slnx --verify-no-changes --no-restore
if ($LASTEXITCODE) { throw 'Formatting check failed.' }
& $Dotnet build Seedline.slnx -c Release --no-restore --nologo -m:1
if ($LASTEXITCODE) { throw 'Build failed.' }
& $Dotnet run --project tests/Seedline.Tests -c Release --no-build -- -noLogo
if ($LASTEXITCODE) { throw 'Foundation tests failed.' }
& (Join-Path $PSScriptRoot 'check-game.ps1') -GameDir $env:STS2_GAME_DIR
if ($RitsuLibDir) {
    & (Join-Path $PSScriptRoot 'check-game.ps1') -GameDir $env:STS2_GAME_DIR -RitsuLibDir $RitsuLibDir
    & (Join-Path $PSScriptRoot 'check-game.ps1') -GameDir $env:STS2_GAME_DIR -RitsuLibDir $RitsuLibDir -RitsuFirst
}

$package = Join-Path $repo 'artifacts/Seedline'
New-Item -ItemType Directory -Path $package -Force | Out-Null
Copy-Item -LiteralPath 'src/Seedline/bin/Release/net9.0/Seedline.dll', 'src/Seedline/Seedline.json' -Destination $package
$unexpected = Get-ChildItem -LiteralPath $package -File | Where-Object Name -NotIn 'Seedline.dll', 'Seedline.json'
if ($unexpected) { throw 'Package contains unexpected files.' }
Write-Output "All checks passed. Package: $package"
if ($Deploy) {
    & (Join-Path $PSScriptRoot 'deploy.ps1') -GameDir $env:STS2_GAME_DIR
}
