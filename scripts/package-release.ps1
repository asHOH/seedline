param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$Dotnet = 'dotnet',
    [string]$RitsuLibDir
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -Raw -LiteralPath (Join-Path $repo 'src/Seedline/Seedline.json') | ConvertFrom-Json
$project = [xml](Get-Content -Raw -LiteralPath (Join-Path $repo 'src/Seedline/Seedline.csproj'))
$version = $manifest.version
if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$' -or $project.Project.PropertyGroup.Version -ne $version) {
    throw 'Seedline.json and Seedline.csproj must declare the same release version.'
}

# Reuse the full validation path rather than maintaining a separate release build.
& (Join-Path $PSScriptRoot 'verify.ps1') @PSBoundParameters

$package = Join-Path $repo 'artifacts/Seedline'
$archivePath = Join-Path $repo "artifacts/Seedline-$version.zip"
# Explicit entries prevent game assemblies, test mods or stale files entering a release.
$entries = [ordered]@{
    'Seedline/Seedline.dll' = Join-Path $package 'Seedline.dll'
    'Seedline/Seedline.json' = Join-Path $package 'Seedline.json'
    'Seedline/LICENSE' = Join-Path $repo 'LICENSE'
}
$stream = [IO.File]::Open($archivePath, [IO.FileMode]::Create)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $entries.GetEnumerator()) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $entry.Value, $entry.Key, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
    $stream.Dispose()
}
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($archivePath))" | Set-Content -LiteralPath "$archivePath.sha256" -Encoding utf8NoBOM
Write-Output "Release assets: $archivePath and $archivePath.sha256"
