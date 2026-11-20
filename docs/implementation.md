# Implementation and compatibility

[DESIGN.md](../design/DESIGN.md) owns gameplay rules; [CARD_ORDER.md](../design/CARD_ORDER.md) owns ranking.

## State and checkpoints

### State ownership

`RunCheckpoints` snapshots counters, supplies, selection clocks, event state and removed-card history. `CombatStateScope` owns transient combat state rebuilt from checkpoints.

`CardOccurrences` binds logical card records to physical objects. `CardOffers` tracks offers and key assignment. `CardHistory` stores frozen removals. Register saved card metadata before property-cache bit widths finalize so checksums and replays preserve it.

### Checkpoint contract

Implement [saved-state guarantees](../design/DESIGN.md#saved-state) with one checkpoint combining vanilla, Seedline (schema 6) and required RitsuLib payloads at the [standard modded save path](../README.md#saves).

- Capture freezes all payloads. Restore independent state copies, preserving parent-event reconstruction. Reload-count rewrites preserve the loaded snapshot.
- Load all payloads from the same primary or backup. Recover from a complete backup only when the primary is missing, empty or has malformed JSON syntax.
- Reject continuation for missing/incompatible required state, invalid run data or changed build/setup, preserving files. Required capture/write/restore failures abort, preserving prior checkpoints. Emit `Saved` only after successful writing.

Delegate discovery, deletion, completed-run cleanup and cloud operations to game facilities. Ordinary cloud writes exclude `.backup`. Single-file `CloudSaveStore.OverwriteCloudWithLocal` bypasses backup/repair filtering.

## Compatibility

Allow altered relic and card pool sizes.

Reject live run modifiers at startup, capture and restore. Preserve serialized save-only modifier payloads through checkpoint writes and reload-count rewrites, provided their owning mod consumes them before run setup.

### RitsuLib

Support current-schema run/player `RunSavedData` slots. Exclude model-attached data, slot migrations, custom pile behavior and compatibility with other mods. Untested versions warn once per session without a version-based block. Activation and checkpoint validation remain required.

- Envelope presence must match activation, including empty envelopes.
- Write captured bytes before RitsuLib's injection context and reattach the selected payload after its primary reread.
- Validate live slot values before setup because retained JSON can conceal failed imports.

RitsuLib does not change Seedline's RNG behavior or state format.

### Modded characters

- Retain alignment for rewards (including card offers from their pools), shops, chests, vanilla rooms/events, and vanilla enemy setup, HP and AI.
- Leave card shuffling, insertion, selection, copy routing, changes to existing permanent cards, random costs, combat card generation, orb generation and player-side random targeting to game/mod behavior.

### Tested mods

- BaseLib
- RitsuLib
- JmcModLib
- Loadout 2 — requires BaseLib. Tested only for card grants, adding Innate, and event jumps.
- Auto-Anthonyology (东尼算法)
- The Minions (仆从们)
- Quick SL (快速SL) — requires JmcModLib
- Quick Restart 2 (快速重启 2) — requires BaseLib
- Minty Spire 2 — requires BaseLib
- 劣人TV之尖塔MOD
- Intent Graph (意图状态机)
- DanmakuSpire (弹幕尖塔)
- RegentFX (万象辉星)

### Incompatible mods

- CardAscension — replaces reward cards and crashes Seedline.
- Combat Solver (战斗求解器) — rejects Seedline's card-play patches.

## Verification limits

Live Steam synchronization remains unverified.
