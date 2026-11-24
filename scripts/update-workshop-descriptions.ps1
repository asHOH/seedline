param([Parameter(Mandatory = $true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$source = Join-Path (Split-Path -Parent $PSScriptRoot) 'workshop'
$itemId = [ulong][IO.File]::ReadAllText("$source/mod_id.txt").Trim()
if (!$itemId) { throw 'mod_id.txt must contain the existing Workshop item ID.' }
$descriptions = [ordered]@{}
foreach ($language in 'english', 'schinese') {
    $text = [IO.File]::ReadAllText("$source/descriptions/$language.bbcode").Trim()
    if (!$text -or $text.Contains([char]0) -or [Text.Encoding]::UTF8.GetByteCount($text) -ge 8000) {
        throw "$language description must contain 1–7999 UTF-8 bytes and no NUL."
    }
    $descriptions[$language] = $text
}

# Reuse the Steam SDK shipped with the game. Steam must be running and signed in.
$managed = Join-Path (Resolve-Path -LiteralPath $GameDir).Path 'data_sts2_windows_x86_64'
[Reflection.Assembly]::LoadFrom((Join-Path $managed 'Steamworks.NET.dll')) | Out-Null
[Runtime.InteropServices.NativeLibrary]::Load((Join-Path $managed 'steam_api64.dll')) | Out-Null
$previousAppId = $env:SteamAppId
$initialized = $false
try {
    $env:SteamAppId = '2868840'
    $errorText = ''
    if ([Steamworks.SteamAPI]::InitEx([ref]$errorText) -ne [Steamworks.ESteamAPIInitResult]::k_ESteamAPIInitResult_OK) {
        throw "Steam initialization failed: $errorText"
    }
    $initialized = $true
    foreach ($language in $descriptions.Keys) {
        $update = [Steamworks.SteamUGC]::StartItemUpdate([Steamworks.AppId_t]::new(2868840), [Steamworks.PublishedFileId_t]::new($itemId))
        if (![Steamworks.SteamUGC]::SetItemUpdateLanguage($update, $language) -or
            ![Steamworks.SteamUGC]::SetItemDescription($update, $descriptions[$language])) {
            throw "Steam rejected the $language description."
        }
        $completion = @{ Done = $false; Failed = $false; Result = $null }
        $callback = [Steamworks.CallResult[Steamworks.SubmitItemUpdateResult_t]]::Create({
            param($result, $failed)
            $completion.Result = $result
            $completion.Failed = $failed
            $completion.Done = $true
        }.GetNewClosure())
        try {
            # No content, title, visibility or release-note changes.
            $call = [Steamworks.SteamUGC]::SubmitItemUpdate($update, $null)
            if ($call -eq [Steamworks.SteamAPICall_t]::Invalid) { throw 'Steam rejected the update.' }
            $callback.Set($call, $null)
            $timer = [Diagnostics.Stopwatch]::StartNew()
            while (!$completion.Done) {
                [Steamworks.SteamAPI]::RunCallbacks()
                if ($timer.Elapsed.TotalSeconds -ge 60) { throw "$language update timed out. Check Steam before retrying." }
                Start-Sleep -Milliseconds 50
            }
            if ($completion.Failed -or $completion.Result.m_eResult -ne [Steamworks.EResult]::k_EResultOK) {
                throw "$language update failed: $($completion.Result.m_eResult)"
            }
            if ($completion.Result.m_bUserNeedsToAcceptWorkshopLegalAgreement) {
                throw 'Accept the Workshop agreement in Steam before retrying.'
            }
            Write-Output "Updated $language description for Workshop item $itemId."
        }
        finally { $callback.Dispose() }
    }
}
finally {
    if ($initialized) { [Steamworks.SteamAPI]::Shutdown() }
    $env:SteamAppId = $previousAppId
}
