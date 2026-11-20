param(
    [string]$Archive,
    [switch]$Update,
    [switch]$IncludeGallery
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo 'workshop'
$manifest = Get-Content -Raw -LiteralPath "$repo/src/Seedline/Seedline.json" | ConvertFrom-Json
$version = $manifest.version
if (!$Archive) { $Archive = Join-Path $repo "artifacts/Seedline-$version.zip" }
$Archive = (Resolve-Path -LiteralPath $Archive).Path
$expectedHash = ((Get-Content -Raw -LiteralPath "$Archive.sha256").Trim() -split '\s+')[0]
if ((Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Release archive checksum mismatch. Run package-release.ps1 again.'
}

$idPath = Join-Path $source 'mod_id.txt'
if ($Update) {
    $itemId = (Get-Content -Raw -LiteralPath $idPath).Trim()
    $parsedId = [ulong]0
    if (![ulong]::TryParse($itemId, [ref]$parsedId) -or $parsedId -eq 0) {
        throw 'workshop/mod_id.txt must contain the existing Workshop item ID.'
    }
} elseif (Test-Path -LiteralPath $idPath) {
    throw 'A Workshop item ID already exists. Use -Update to prepare the same item.'
}

$image = Join-Path $source 'artwork/image.png'
if (!(Test-Path -LiteralPath $image)) {
    throw 'Add the Workshop thumbnail at workshop/artwork/image.png, then rerun preparation.'
}
$images = @(Get-Item -LiteralPath $image)
$gallery = @()
if ($IncludeGallery) {
    $gallery = @(Get-ChildItem -LiteralPath "$source/artwork/gallery" -File |
        Where-Object Extension -In '.png', '.jpg', '.jpeg', '.gif' | Sort-Object Name)
    $images += $gallery
}
foreach ($file in $images) {
    if ($file.Length -eq 0 -or $file.Length -ge 1MB) {
        throw "Workshop images must be nonempty and under 1 MiB: $($file.FullName)"
    }
}

# Omitted fields preserve online text, visibility, tags and descriptors.
# Official uploader v0.2.0 always reconciles dependencies, so keep them explicit.
$metadata = Get-Content -Raw -LiteralPath "$source/workshop.json" | ConvertFrom-Json -AsHashtable
if ($Update) {
    $config = [ordered]@{ dependencies = @($metadata.dependencies) }
} else {
    $config = $metadata
    if ($config.visibility -ne 'private') { throw 'Initial Workshop preparation requires private visibility.' }
    $titles = Get-Content -Raw -LiteralPath "$source/titles.json" | ConvertFrom-Json
    $config.title = $titles.english
    $config.description = (Get-Content -Raw -LiteralPath "$source/descriptions/english.bbcode").Trim()
}
# Steam's changeNote is shared, not selected by SetItemUpdateLanguage.
$notes = foreach ($language in 'en', 'zh-CN') {
    (Get-Content -Raw -LiteralPath "$source/release-notes/$version.$language.txt").Trim()
}
$config.changeNote = $notes -join "`n`n"

# Read only the release allowlist. Never extract arbitrary ZIP paths or copy a build directory.
$zip = [IO.Compression.ZipFile]::OpenRead($Archive)
try {
    $expectedEntries = @('Seedline/LICENSE', 'Seedline/Seedline.dll', 'Seedline/Seedline.json')
    if ((@($zip.Entries.FullName | Sort-Object) -join ',') -cne ($expectedEntries -join ',')) {
        throw 'Release archive must contain only Seedline.dll, Seedline.json and LICENSE under Seedline/.'
    }
    $reader = [IO.StreamReader]::new($zip.GetEntry('Seedline/Seedline.json').Open())
    try { $packedManifest = $reader.ReadToEnd() | ConvertFrom-Json }
    finally { $reader.Dispose() }
    if ($packedManifest.id -ne $manifest.id -or $packedManifest.version -ne $version) {
        throw 'Release archive mod ID or version differs from the current manifest.'
    }

    # A fresh workspace keeps prior upload IDs and logs intact without recursive cleanup.
    $workspace = Join-Path $repo ('artifacts/workshop/' + [guid]::NewGuid().ToString())
    New-Item -ItemType Directory -Path "$workspace/content" -Force | Out-Null
    foreach ($name in $expectedEntries) {
        [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry($name),
            (Join-Path "$workspace/content" ([IO.Path]::GetFileName($name))))
    }
} finally { $zip.Dispose() }

$config | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$workspace/workshop.json" -Encoding utf8NoBOM
Copy-Item -LiteralPath $image -Destination "$workspace/image.png"
if ($Update) { $itemId | Set-Content -LiteralPath "$workspace/mod_id.txt" -Encoding utf8NoBOM }
if ($IncludeGallery) {
    New-Item -ItemType Directory -Path "$workspace/previews" | Out-Null
    foreach ($file in $gallery) { Copy-Item -LiteralPath $file.FullName -Destination "$workspace/previews" }
}
Write-Output "Prepared workspace: $workspace"
Write-Output 'No upload performed. See workshop/README.md for publishing and item-ID retention.'
