$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repo ('.cache/release-package-tests/' + [guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path "$fixture/scripts", "$fixture/src/Seedline" -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'package-release.ps1') -Destination "$fixture/scripts/package-release.ps1"
'{"version":"0.1.0"}' | Set-Content -LiteralPath "$fixture/src/Seedline/Seedline.json"
'<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>' | Set-Content -LiteralPath "$fixture/src/Seedline/Seedline.csproj"
'Test license' | Set-Content -LiteralPath "$fixture/LICENSE"
# Exercise packaging without a game installation or recursively running the real verifier.
@'
param([string]$GameDir, [string]$Dotnet, [string]$RitsuLibDir)
if ($GameDir -eq 'fail') { throw 'Simulated verification failure' }
$repo = Split-Path -Parent $PSScriptRoot
$PSBoundParameters | ConvertTo-Json | Set-Content -LiteralPath "$repo/arguments.json"
New-Item -ItemType Directory -Path "$repo/artifacts/Seedline" -Force | Out-Null
'Test assembly' | Set-Content -LiteralPath "$repo/artifacts/Seedline/Seedline.dll"
Copy-Item -LiteralPath "$repo/src/Seedline/Seedline.json" -Destination "$repo/artifacts/Seedline/Seedline.json"
'Must not ship' | Set-Content -LiteralPath "$repo/artifacts/Seedline/sts2.dll"
'@ | Set-Content -LiteralPath "$fixture/scripts/verify.ps1"

& "$fixture/scripts/package-release.ps1" -GameDir 'game path' -Dotnet 'SDK path' -RitsuLibDir 'Ritsu path' | Out-Null
$arguments = Get-Content -Raw -LiteralPath "$fixture/arguments.json" | ConvertFrom-Json
if ($arguments.GameDir -ne 'game path' -or $arguments.Dotnet -ne 'SDK path' -or $arguments.RitsuLibDir -ne 'Ritsu path') {
    throw 'Packaging did not forward verification arguments.'
}
$zipPath = "$fixture/artifacts/Seedline-0.1.0.zip"
$zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $names = @($zip.Entries.FullName | Sort-Object)
    if (($names -join ',') -ne 'Seedline/LICENSE,Seedline/Seedline.dll,Seedline/Seedline.json') {
        throw 'Release archive contains unexpected entries.'
    }
    foreach ($name in 'Seedline.dll', 'Seedline.json', 'LICENSE') {
        $sourcePath = if ($name -eq 'LICENSE') { "$fixture/$name" } else { "$fixture/artifacts/Seedline/$name" }
        $reader = [IO.StreamReader]::new($zip.GetEntry("Seedline/$name").Open())
        try {
            if ($reader.ReadToEnd() -ne (Get-Content -Raw -LiteralPath $sourcePath)) {
                throw "Archive content differs from validated package: $name"
            }
        }
        finally { $reader.Dispose() }
    }
}
finally { $zip.Dispose() }
$expectedHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ((Get-Content -Raw -LiteralPath "$zipPath.sha256").Trim() -ne "$expectedHash  Seedline-0.1.0.zip") {
    throw 'Release checksum does not match the archive.'
}

'{"version":"0.2.0"}' | Set-Content -LiteralPath "$fixture/src/Seedline/Seedline.json"
$rejected = $false
try { & "$fixture/scripts/package-release.ps1" -GameDir 'game path' | Out-Null }
catch {
    if ($_.Exception.Message -notlike '*same release version*') { throw }
    $rejected = $true
}
if (!$rejected) { throw 'Mismatched release versions were accepted.' }

'<Project><PropertyGroup><Version>0.2.0</Version></PropertyGroup></Project>' | Set-Content -LiteralPath "$fixture/src/Seedline/Seedline.csproj"
$rejected = $false
try { & "$fixture/scripts/package-release.ps1" -GameDir 'fail' | Out-Null }
catch {
    if ($_.Exception.Message -ne 'Simulated verification failure') { throw }
    $rejected = $true
}
if (!$rejected -or (Test-Path -LiteralPath "$fixture/artifacts/Seedline-0.2.0.zip")) {
    throw 'A failed verification produced a release archive.'
}
Write-Output 'Release packaging checks passed.'
