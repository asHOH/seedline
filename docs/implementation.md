# Implementation

For developers. See [compatibility and saves](compatibility.md) for player guidance.

## Build and validate

Install the .NET SDK pinned in [global.json](../global.json). In PowerShell 7, run from the repository root, replacing the example game path with your installation:

```powershell
./scripts/verify.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Integration checks use an isolated game copy, so allow disk space for it.

- For a portable SDK, add `-Dotnet 'path/to/dotnet.exe'`.
- With RitsuLib, add `-RitsuLibDir '<extracted RitsuLib package>'`, using the [complete package](compatibility.md#ritsulib).

The validated package is written to `artifacts/Seedline/`. Close the game, then deploy from the repository root using your game path:

```powershell
./scripts/deploy.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Enable Seedline in the game's mod loader and start a new run.

## Releases

Build and test locally against the supported game installation. Hosted CI lacks the required game assemblies and executable, so release packaging reuses `verify.ps1` instead of maintaining a separate CI build.

1. Set the same version in `src/Seedline/Seedline.json` and `src/Seedline/Seedline.csproj`, and add release notes in `docs/releases/<version>.md`.
2. Commit the release changes and start from a clean working tree.
3. Run the packaging command below. It runs all checks and creates `artifacts/Seedline-<version>.zip` and its `.sha256` checksum. Include RitsuLib when validating its support. `-Dotnet` accepts a portable SDK.

```powershell
./scripts/package-release.ps1 -GameDir 'D:/Games/Slay the Spire 2' -RitsuLibDir '<complete RitsuLib package>'
```

Tag the tested commit, push it, and create a draft with both assets. Replace the example version for each release. These commands require GitHub CLI authentication and repository write access.

```powershell
$version = '0.1.0'
git tag -a "v$version" -m "Seedline $version"
git push origin HEAD
git push origin "v$version"
gh release create "v$version" "artifacts/Seedline-$version.zip" "artifacts/Seedline-$version.zip.sha256" --verify-tag --draft --title "Seedline $version" --notes-file "docs/releases/$version.md"
```

Check the uploaded assets and notes, then publish with `gh release edit "v$version" --draft=false --latest`. Releases remain private while the repository is private. Do not reuse a published version for different binaries.

## State and checkpoints

### State ownership

`RunCheckpoints` snapshots counters, supplies, selection clocks, event state and removed-card history. `CombatStateScope` owns transient combat state rebuilt from checkpoints.

`CardOccurrences` binds logical card records to physical objects. `CardOffers` tracks offers and key assignment. `CardHistory` stores frozen removals. Register saved card metadata before property-cache bit widths finalize so checksums and replays preserve it.

### Checkpoint contract

Store vanilla, Seedline and required RitsuLib state in one checkpoint at the [modded save path](compatibility.md#saves), following the [saved-state guarantees](../design/DESIGN.md#saved-state).

- Capture freezes all payloads. Restore independent state copies, preserving parent-event reconstruction. Reload-count rewrites preserve the loaded snapshot.
- Load all payloads from the same primary or backup. Recover from a complete backup only when the primary is missing, empty or has malformed JSON syntax.
- Reject continuation for missing/incompatible required state, invalid run data or changed build/setup, preserving files. Required capture/write/restore failures abort, preserving prior checkpoints. Emit `Saved` only after successful writing.

Delegate discovery, deletion, completed-run cleanup and cloud operations to game facilities. Ordinary cloud writes exclude `.backup`. Single-file `CloudSaveStore.OverwriteCloudWithLocal` bypasses backup/repair filtering.

## Mod integration

Allow altered relic and card pool sizes.

Reject live run modifiers at startup, capture and restore. Preserve serialized save-only modifier payloads through checkpoint writes and reload-count rewrites, provided their owning mod consumes them before run setup.

### RitsuLib

Support current-schema run/player `RunSavedData` slots. Exclude model-attached data, slot migrations, custom pile behavior and compatibility with other mods. Apply the [untested-version warning](compatibility.md#ritsulib) once per session. Activation and checkpoint validation remain required.

- Envelope presence must match activation, including empty envelopes.
- Write captured bytes before RitsuLib's injection context and reattach the selected payload after its primary reread.
- Validate live slot values before setup because retained JSON can conceal failed imports.

RitsuLib does not change Seedline's RNG behavior or state format.

### Modded characters

- Retain alignment for rewards (including card offers from their pools), shops, chests, vanilla rooms/events, and vanilla enemy setup, HP and AI.
- Leave card shuffling, insertion, selection, copy routing, changes to existing permanent cards, random costs, combat card generation, orb generation and player-side random targeting to game/mod behavior.
