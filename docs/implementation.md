# Implementation

State and integration contracts for developers. See [Development](development.md) for build, test and release instructions.

## State and checkpoints

### State ownership

`RunCheckpoints` snapshots counters, supplies, selection clocks, event state and removed-card history. `CombatStateScope` owns transient combat state rebuilt from checkpoints.

`CardOccurrences` binds logical card records to physical objects. `CardOffers` tracks offers and key assignment. `CardHistory` stores frozen removals. Append saved card metadata after ordinary property IDs and before bit widths finalize so solo checksums and replays preserve it without shifting ordinary IDs. Exclude this local metadata from the multiplayer hash.

### Checkpoint contract

Store vanilla, Seedline and required RitsuLib state in one checkpoint at the [modded save path](compatibility.md#saves), following the [saved-state guarantees](../design/DESIGN.md#saved-state).

- Capture freezes all payloads. Restore independent state copies, preserving parent-event reconstruction. Reload-count rewrites preserve the loaded snapshot.
- Load all payloads from the same primary or backup. Recover from a complete backup only when the primary is missing, empty or has malformed JSON syntax.
- Reject missing/incompatible required state, invalid run data or changed game version/setup, preserving files. Required capture/write/restore failures abort, preserving prior checkpoints. Emit `Saved` only after successful writing.

Delegate discovery, deletion, completed-run cleanup and cloud operations to game facilities. Ordinary cloud writes exclude `.backup`. Single-file `CloudSaveStore.OverwriteCloudWithLocal` bypasses backup/repair filtering.

## Mod integration

Allow altered relic and card pool sizes.

Bypass multiplayer, Blitz, Daily and unsupported [custom-run modifiers](../design/DESIGN.md#custom-run-modifiers).

Preserve save-only modifier payloads until their owning mod consumes them before run setup. Seedline envelopes require strict loading. Reject unsupported modifiers added mid-run at capture.

### RitsuLib

Support current-schema run/player `RunSavedData` slots. Exclude model-attached data, slot migrations, custom pile behavior and compatibility with other mods. Apply the [untested-version warning](compatibility.md#ritsulib) once per session. Activation and checkpoint validation remain required.

- Envelope presence must match activation, including empty envelopes.
- Write captured bytes before RitsuLib's injection context and reattach the selected payload after its primary reread.
- Validate live slot values before setup because retained JSON can conceal failed imports.

RitsuLib does not change Seedline's RNG behavior or state format.

### Modded characters

- Retain alignment for rewards (including card offers from their pools), shops, chests, vanilla rooms/events, and vanilla enemy setup, HP and AI.
- Leave card shuffling, insertion, selection, copy routing, changes to existing permanent cards, random costs, combat card generation, orb generation and player-side random targeting to game/mod behavior.
