# Stage 1: integration

Standard-path checkpoints combine vanilla, SameSeed and optional RitsuLib state.

The initializer verifies the [supported builds](../README.md)' assembly hashes before installing Harmony patches. Run entry rejects Catfood, multiplayer, daily, Blitz and custom runs.

New runs receive the game's full unlock snapshot before initialization. Grants stay in the run; profile unlocks, Ascension limits and selected difficulty are preserved. Standard and Dread Start buttons list missing content with Proceed focused and Cancel available. Act selection also uses full unlocks. New-run compatibility checks remain at setup, after the UI fades out.

The full snapshot also supplies encounter-discovery flags and mature-run content rules. Character selection and tutorial prompts retain their profile rules.

## Checkpoint contract

Use `modded/profileN/saves/current_run.save` and its local `.backup`. The game handles discovery, deletion, cloud synchronization and completed-run cleanup. Continuing through this ordinary modded slot with SameSeed disabled can discard SameSeed state.

Keep the game build and mod setup fixed throughout each run. RitsuLib is optional; compatibility covers v0.6.14 and [current-schema run/player slots](ritsulib.md). Other-mod compatibility is outside scope. SameSeed retains its RNG behavior and state format with or without RitsuLib.

One checkpoint contains the game's `SerializableRun`, versioned `same_seed` state and any RitsuLib payload. Capture freezes all payloads; reload-count rewrites preserve the loaded snapshot. Required payload capture, injection or restoration failures abort the operation and preserve the prior checkpoint. Restore required state before room regeneration, preserving vanilla checkpoint timing and reconstruction payloads.

Load every payload from the same selected primary or backup. A missing or empty primary, or invalid JSON, may recover a complete valid backup. Missing or incompatible required state and invalid run data reject continuation and preserve files; partial recovery is insufficient. Ordinary saves cannot become SameSeed runs through continuation.

When RitsuLib is active, persist its envelope even with no entries; require its absence in standalone runs. This detects a missing payload or changed setup.

Checkpoints require their creating game build; cross-build continuation is rejected with files preserved.

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

The game store writes locally before uploading; failed uploads retain the local checkpoint. In v0.113.1, ordinary writes and directory synchronization keep `.backup`/`.pre-repair` files local and directory synchronization removes stale cloud copies. At startup, differing timestamps can replace newer local data. An absent remote primary deletes its local primary and backup; a lone local backup survives.

The [validation command](../README.md#build-and-validate) exercises standard-path discovery, profile isolation, deletion, completed-run cleanup, paired backup recovery, rejected ordinary saves, snapshot isolation, disk failure and simulated cloud operations. Combined runs add [RitsuLib checks](ritsulib.md); both configurations exercise the foundation and replay checks. Each run writes `result.json` and `game.log` under `.cache/stage1-checks/<run>/` or `.cache/ritsu-checks/<run>/`.

Validation: [build compatibility review](../references/v0.113.1-review.md).

Live Steam synchronization and normal-UI disable/re-enable remain unverified; second-device checks are outside the available setup.

**Warning baseline — 2026-11-16, v0.113.1 / RitsuLib 0.6.14:** `Invalid Task ID` reproduces with mods disabled. Exit leaks also occur on unmodded timed shutdown; normal-exit ownership remains unresolved. Missing Wuppy/Rocket/Crusher sounds originate in game calls, reported by RitsuLib. LieRenTVmod resource UIDs, IntentGraph states and D3D12 PSO messages belong to their respective components. Recheck these signatures only for changed symptoms or relevant version changes.
