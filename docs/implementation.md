# Implementation guide

[DESIGN.md](../design/DESIGN.md) owns gameplay rules; [CARD_ORDER.md](../design/CARD_ORDER.md) owns ranking.

## State ownership

`RunCheckpoints` groups run-owned counters, supplies, selection clocks, event state and removed-card history into frozen snapshots. `CombatStateScope` owns transient combat state rebuilt from checkpoints.

`CardOccurrences` binds logical card records to physical objects; `CardOffers` tracks offers and key assignment; `CardHistory` stores frozen removals. Register saved card metadata before property-cache bit widths finalize so checksums and replays preserve it.

## Checkpoint contract

Implement [saved-state guarantees](../design/DESIGN.md#saved-state) with one checkpoint combining vanilla, SameSeed (schema 6) and required RitsuLib payloads at the [standard modded save path](../README.md#saves). Delegate discovery, deletion, completed-run cleanup and cloud operations to game facilities; ordinary cloud writes exclude `.backup`.

Capture freezes every payload; restoration copies state independently. Reload-count rewrites preserve the loaded snapshot. Preserve parent-event reconstruction.

Load all payloads from the same primary or backup. Recover from a complete backup only when the primary file is missing, empty or has malformed JSON syntax. Reject continuation for missing/incompatible required state, invalid run data or changed build/setup, preserving files. Required capture/write/restore failures abort, preserving prior checkpoints; emit `Saved` after successful writing.

**RitsuLib scope:** official v0.6.14, current-schema run/player `RunSavedData` slots. Model-attached data, slot migrations, custom pile behavior and compatibility with other mods remain outside this claim.

RitsuLib envelope presence must match activation, including empty envelopes. Write captured bytes before its injection context; reattach the selected payload after its primary reread. Validate live slot values before setup because retained JSON can conceal failed imports. SameSeed RNG behavior and state format stay identical with or without RitsuLib.

## Validation

Follow the [bounded checks and pass criteria](validation.md).
