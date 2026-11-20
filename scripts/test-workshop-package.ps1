$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repo ('.cache/workshop-package-tests/' + [guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path "$fixture/scripts", "$fixture/src/Seedline", "$fixture/artifacts",
    "$fixture/workshop/descriptions", "$fixture/workshop/release-notes", "$fixture/workshop/artwork/gallery" -Force | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/prepare-workshop.ps1" -Destination "$fixture/scripts/prepare-workshop.ps1"
# Exercise preparation independently of live release versions, page drafts and artwork.
$manifest = [pscustomobject]@{ id = 'Seedline'; version = '0.1.0' }
$manifest | ConvertTo-Json | Set-Content -LiteralPath "$fixture/src/Seedline/Seedline.json"
$version = $manifest.version
'{"visibility":"private","tags":[],"dependencies":[],"contentDescriptors":[]}' |
    Set-Content -LiteralPath "$fixture/workshop/workshop.json"
'{"english":"Test mod","schinese":"测试模组"}' | Set-Content -LiteralPath "$fixture/workshop/titles.json"
'Test description' | Set-Content -LiteralPath "$fixture/workshop/descriptions/english.bbcode"
'English release note' | Set-Content -LiteralPath "$fixture/workshop/release-notes/$version.en.txt"
'中文发布说明' | Set-Content -LiteralPath "$fixture/workshop/release-notes/$version.zh-CN.txt"
$archivePath = "$fixture/artifacts/Seedline-$version.zip"
$png = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=')
[IO.File]::WriteAllBytes("$fixture/workshop/artwork/image.png", $png)
[IO.File]::WriteAllBytes("$fixture/workshop/artwork/gallery/01-test.png", $png)

function Write-TestArchive([string]$PackedVersion = $version, [switch]$UnexpectedEntry) {
    $stream = [IO.File]::Open($archivePath, [IO.FileMode]::Create)
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $entries = [ordered]@{
            'Seedline/Seedline.dll' = 'Test assembly'
            'Seedline/Seedline.json' = (@{ id = $manifest.id; version = $PackedVersion } | ConvertTo-Json)
            'Seedline/LICENSE' = 'Test license'
        }
        if ($UnexpectedEntry) { $entries['Seedline/sts2.dll'] = 'Must not ship' }
        foreach ($entry in $entries.GetEnumerator()) {
            $writer = [IO.StreamWriter]::new($zip.CreateEntry($entry.Key).Open())
            try { $writer.Write($entry.Value) } finally { $writer.Dispose() }
        }
    } finally { $zip.Dispose(); $stream.Dispose() }
    $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    "$hash  Seedline-$version.zip" | Set-Content -LiteralPath "$archivePath.sha256"
}
function Prepare([switch]$Update, [switch]$IncludeGallery) {
    $output = & "$fixture/scripts/prepare-workshop.ps1" -Update:$Update -IncludeGallery:$IncludeGallery
    ($output | Where-Object { $_ -like 'Prepared workspace: *' }) -replace '^Prepared workspace: ', ''
}
function Expect-Failure([scriptblock]$Action, [string]$Message) {
    try { & $Action | Out-Null } catch {
        if ($_.Exception.Message -notlike $Message) { throw }
        return
    }
    throw "Expected failure: $Message"
}

Write-TestArchive
$initial = Prepare
$config = Get-Content -Raw -LiteralPath "$initial/workshop.json" | ConvertFrom-Json -AsHashtable
if ($config.visibility -ne 'private' -or !$config.title -or !$config.description) { throw 'Initial page metadata is incomplete.' }
$expectedNotes = @('en', 'zh-CN' | ForEach-Object {
    (Get-Content -Raw -LiteralPath "$fixture/workshop/release-notes/$version.$_.txt").Trim()
}) -join "`n`n"
if ($config.changeNote -ne $expectedNotes) {
    throw 'Initial change note is not bilingual.'
}
if ((@(Get-ChildItem -LiteralPath "$initial/content" -Name | Sort-Object) -join ',') -ne 'LICENSE,Seedline.dll,Seedline.json') {
    throw 'Workshop content layout is incorrect.'
}
if ((Get-Content -Raw -LiteralPath "$initial/content/Seedline.dll") -ne 'Test assembly') { throw 'Assembly changed during staging.' }
if (Test-Path -LiteralPath "$initial/previews") { throw 'Default staging would modify the online gallery.' }
if (Test-Path -LiteralPath "$initial/mod_id.txt") { throw 'Initial staging contains an item ID.' }
$withGallery = Prepare -IncludeGallery
if (!(Test-Path -LiteralPath "$withGallery/previews/01-test.png")) { throw 'Gallery was not staged.' }

'1234567890' | Set-Content -LiteralPath "$fixture/workshop/mod_id.txt"
Expect-Failure { Prepare } '*Use -Update*'
$update = Prepare -Update
$config = Get-Content -Raw -LiteralPath "$update/workshop.json" | ConvertFrom-Json -AsHashtable
if ((@($config.Keys | Sort-Object) -join ',') -ne 'changeNote,dependencies') { throw 'Update staging would overwrite online page metadata.' }
if ($config.dependencies.Count -ne 0) { throw 'Optional dependencies became required.' }
if ((Get-Content -Raw -LiteralPath "$update/mod_id.txt").Trim() -ne '1234567890') { throw 'Update lost the item ID.' }
if (!(Test-Path -LiteralPath "$initial/workshop.json")) { throw 'Preparation removed an earlier workspace.' }
'not-an-id' | Set-Content -LiteralPath "$fixture/workshop/mod_id.txt"
Expect-Failure { Prepare -Update } '*existing Workshop item ID*'
Remove-Item -LiteralPath "$fixture/workshop/mod_id.txt"

[IO.File]::WriteAllBytes("$fixture/workshop/artwork/image.png", [byte[]]::new(1MB))
Expect-Failure { Prepare } '*under 1 MiB*'
Remove-Item -LiteralPath "$fixture/workshop/artwork/image.png"
Expect-Failure { Prepare } '*Add the Workshop thumbnail*'
[IO.File]::WriteAllBytes("$fixture/workshop/artwork/image.png", $png)
'bad hash' | Set-Content -LiteralPath "$archivePath.sha256"
Expect-Failure { Prepare } '*checksum mismatch*'
Write-TestArchive -UnexpectedEntry
Expect-Failure { Prepare } '*must contain only*'
Write-TestArchive -PackedVersion '99.0.0'
Expect-Failure { Prepare } '*version differs*'
Write-TestArchive
'{"visibility":"public"}' | Set-Content -LiteralPath "$fixture/workshop/workshop.json"
Expect-Failure { Prepare } '*requires private visibility*'

Write-Output 'Workshop preparation checks passed (no Steam calls).'
