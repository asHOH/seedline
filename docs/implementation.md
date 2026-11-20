# Implementation

For developers building, testing or changing Seedline. For supported mods and save precautions, see [player compatibility guidance](compatibility.md).

## Build and validate

Install the .NET SDK pinned in [global.json](../global.json). In PowerShell 7, run from the repository root, replacing the example game path with your installation:

```powershell
./scripts/verify.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

The command builds and checks the mod, using an isolated game copy for integration checks; allow disk space for that copy. Use `-Dotnet 'path/to/dotnet.exe'` for a portable SDK.

Add `-RitsuLibDir '<extracted RitsuLib package>'` when using RitsuLib. Keep the [complete RitsuLib package](compatibility.md#ritsulib) for these checks.

The validated package is written to `artifacts/Seedline/`. Follow the [deployment steps](../README.md#deploy) to install it.

## State and checkpoints

### State ownership

`RunCheckpoints` snapshots counters, supplies, selection clocks, event state and removed-card history. `CombatStateScope` owns transient combat state rebuilt from checkpoints.

`CardOccurrences` binds logical card records to physical objects. `CardOffers` tracks offers and key assignment. `CardHistory` stores frozen removals. Register saved card metadata before property-cache bit widths finalize so checksums and replays preserve it.

### Checkpoint contract

Implement [saved-state guarantees](../design/DESIGN.md#saved-state) with one checkpoint combining vanilla saved state, Seedline saved state and required RitsuLib payloads at the [standard modded save path](compatibility.md#saves).

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

Apply the [modded-character alignment boundary](compatibility.md#modded-characters), preserving game/mod behavior outside it.
