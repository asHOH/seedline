# Implementation guide

[DESIGN.md](../DESIGN.md) owns gameplay rules; [CARD_ORDER.md](../CARD_ORDER.md) owns ranking.

## State ownership

`RunCheckpoints` groups run-owned counters, supplies, selection clocks, event state and removed-card history into frozen snapshots. `CombatStateScope` owns transient combat state rebuilt from checkpoints.

`CardOccurrences` binds logical card records to physical objects; `CardOffers` tracks offers and key assignment; `CardHistory` stores frozen removals. Register saved card metadata before property-cache bit widths finalize so checksums and replays preserve it.

## Checkpoint contract

Vanilla, SameSeed and required RitsuLib payloads form one versioned checkpoint at the [standard modded save path](../README.md#saves). Delegate discovery, deletion, completed-run cleanup and cloud operations to game facilities.

Capture freezes every payload; restoration copies state independently. Reload-count rewrites preserve the loaded snapshot. Restore before room regeneration, retaining vanilla checkpoint timing and parent-event reconstruction.

Load all payloads from the same primary or backup. Only missing/empty primaries or invalid JSON permit recovery from a complete backup. Missing/incompatible required state, invalid run data or changed build/setup reject continuation with files preserved. Required capture/write/restore failures abort, preserving prior checkpoints; emit `Saved` after successful writing.

RitsuLib envelope presence must match activation, including empty envelopes. Write captured bytes before its injection context; reattach the selected payload after its primary reread. Validate live slot values before setup because retained JSON can conceal failed imports. SameSeed RNG behavior and state format stay identical with or without RitsuLib.

## Validation

Follow the [bounded checks and pass criteria](validation.md).
