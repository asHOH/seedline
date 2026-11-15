# Stage 1: integration

Standard-path checkpoints combine vanilla, SameSeed and optional RitsuLib state.

The initializer checks the v0.113.0 build `16ac0fe3` assembly hash before installing Harmony patches. Run entry rejects Catfood, multiplayer, daily, Blitz and custom runs.

## Checkpoint contract

Use `modded/profileN/saves/current_run.save` and its local `.backup`. The game handles discovery, deletion, cloud synchronization and completed-run cleanup. Continuing through this ordinary modded slot with SameSeed disabled can discard SameSeed state.

Keep SameSeed enabled and the mod setup unchanged throughout each run. RitsuLib is optional; compatibility covers v0.6.13 targeting the scoped game build and [current-schema run/player slots](ritsulib.md). Other-mod compatibility is outside scope. SameSeed retains its RNG behavior and state format with or without RitsuLib.

One checkpoint contains the game's `SerializableRun`, versioned `same_seed` state and any RitsuLib payload. Capture freezes all payloads; reload-count rewrites preserve the loaded snapshot. Required payload capture, injection or restoration failures abort the operation and preserve the prior checkpoint. Restore required state before room regeneration, preserving vanilla checkpoint timing and reconstruction payloads.

Load every payload from the same selected primary or backup. A missing or empty primary, or invalid JSON, may recover a complete valid backup. Missing or incompatible required state and invalid run data reject continuation and preserve files; partial recovery is insufficient. Ordinary saves cannot become SameSeed runs through continuation.

When RitsuLib is active, persist its envelope even with no entries; require its absence in standalone runs. This detects a missing payload or changed setup.

Start fresh after the filename change. Leave development saves untouched; omit migration.

## Integration boundaries

| Boundary | Integration |
|---|---|
| New run | `RunManager.SetUpNewSingleplayer` prefix: attach state before initialization. |
| Capture | `RunManager.ToSave` postfix: copy SameSeed state and export RitsuLib data directly onto the snapshot. |
| Save | `RunSaveManager.SaveRun(SerializableRun, bool)` prefix: serialize all frozen payloads before writing through `ISaveStore`; emit `Saved` after success. Run before RitsuLib's injection-context prefix. |
| Load | `RunSaveManager.LoadRunSave` prefix: validate and select a complete checkpoint. The final postfix reattaches its RitsuLib payload after RitsuLib's primary reread. |
| Restore | `RunManager.SetUpSavedSingleplayer` prefix: verify live RitsuLib slot values, then copy SameSeed state before reload-count rewriting and room regeneration. |

Writing the captured envelope directly avoids RitsuLib's fallible injection path. Its normal save prefix is skipped; explicit patch ordering covers either initializer order. Restoration checks inspect runtime values because RitsuLib can preserve JSON while silently rejecting its contents.

## Cloud and verification limits

The game store writes locally before uploading; failed remote writes retain the local checkpoint. At startup, remote data can replace newer local data when timestamps differ. If the remote file is absent and a local primary exists, synchronization deletes the local primary and backup. A lone local backup survives. Retain these game-store semantics.

The [validation command](../README.md#build-and-validate) exercises standard-path discovery, profile isolation, deletion, completed-run cleanup, paired backup recovery, rejected ordinary saves, snapshot isolation, disk failure and simulated cloud operations. Combined runs add [RitsuLib checks](ritsulib.md); both configurations exercise the foundation and replay checks. Evidence: `.cache/stage1-checks/<run>/result.json`, `.cache/ritsu-checks/<run>/result.json` and their `game.log` files.

Validated 2026-11-15: formatting and analyzer build, 32 unit tests, 77 standalone checks and 131 combined checks in each initialization order passed.

Live Steam synchronization and normal-UI disable/re-enable remain unverified; second-device checks are outside the available setup. Headless runs emit rendering/preload and shutdown diagnostics.
