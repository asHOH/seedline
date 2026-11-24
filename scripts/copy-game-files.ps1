param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [Parameter(Mandatory = $true)][string]$HostDir
)
$ErrorActionPreference = 'Stop'

function Copy-ChangedFile([IO.FileInfo]$Source, [string]$Destination) {
    $target = Get-Item -LiteralPath $Destination -ErrorAction SilentlyContinue
    # Reuse unchanged runtime/assets without reading or rewriting the large game pack.
    if (!$target -or $target.Length -ne $Source.Length -or $target.LastWriteTimeUtc -ne $Source.LastWriteTimeUtc) {
        Copy-Item -LiteralPath $Source.FullName -Destination $Destination -Force
    }
}

$managed = Join-Path $HostDir 'data_sts2_windows_x86_64'
New-Item -ItemType Directory -Path $HostDir, $managed -Force | Out-Null
foreach ($name in 'SlayTheSpire2.exe', 'release_info.json') {
    Copy-ChangedFile (Get-Item -LiteralPath (Join-Path $GameDir $name)) (Join-Path $HostDir $name)
}
foreach ($file in Get-ChildItem -LiteralPath $GameDir -File | Where-Object Extension -In '.dll', '.pck') {
    Copy-ChangedFile $file (Join-Path $HostDir $file.Name)
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $GameDir 'data_sts2_windows_x86_64') -File) {
    Copy-ChangedFile $file (Join-Path $managed $file.Name)
}
