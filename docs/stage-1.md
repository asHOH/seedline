# Stage 1: integration

The dedicated-file baseline is implemented. The standard-path and RitsuLib revision below is accepted and pending implementation.

The initializer checks the v0.113.0 build `16ac0fe3` assembly hash before installing Harmony patches. Run entry rejects Catfood, multiplayer, daily, Blitz and custom runs.

## Accepted checkpoint contract

Use `modded/profileN/saves/current_run.save` and its local `.backup`. Let the game handle discovery, deletion, cloud synchronization and completed-run cleanup. This removes dedicated-filename integration at the cost of protection when SameSeed is disabled: continuing through the ordinary modded slot can discard SameSeed state.

Keep SameSeed enabled and the mod setup unchanged throughout each run. RitsuLib is optional; initial compatibility covers v0.6.13 targeting the scoped game build. Other-mod compatibility is outside scope. SameSeed retains its RNG behavior and state format with or without RitsuLib.

One checkpoint contains the game's `SerializableRun`, versioned `same_seed` state and any RitsuLib payload. Capture freezes all payloads; reload-count rewrites preserve the loaded snapshot. Required payload capture, injection or restoration failures abort the operation and preserve the prior checkpoint. Restore required state before room regeneration, preserving vanilla checkpoint timing and reconstruction payloads.

Load every payload from the same selected primary or backup. A missing or empty primary, or invalid JSON, may recover a complete valid backup. Missing or incompatible required state and invalid run data reject continuation and preserve files; partial recovery is insufficient. Ordinary saves cannot become SameSeed runs through continuation.

RitsuLib's default load hook rereads the primary, and its injection hook can continue after an error. The integration must enforce the checkpoint rules above across those hooks.

Start fresh after the filename change. Leave development saves untouched; omit migration.

## Current integration

The baseline uses `same_seed_run.save`. These boundaries remain the starting points for the revision:

| Boundary | Integration |
|---|---|
| New run | `RunManager.SetUpNewSingleplayer` prefix: attach state before initialization. |
| Capture | `RunManager.ToSave` postfix: copy state onto the returned snapshot. |
| Save | `RunSaveManager.SaveRun(SerializableRun, bool)` prefix: freeze both halves before writing through `ISaveStore`; emit `Saved` after success. |
| Load | `RunSaveManager.LoadRunSave` prefix: validate schema, provenance and required run fields; attach state to the snapshot. |
| Restore | `RunManager.SetUpSavedSingleplayer` prefix: copy state into the run before reload-count rewriting and room regeneration. |

## Remaining work and checks

| Change | Required verification |
|---|---|
| Use the standard path for saving and loading; remove `SavePathPatch` and `CloudPatches.cs`. | Standard-path discovery, profile isolation, deletion and completed-run cleanup; preserve an existing ordinary save when continuation is rejected. |
| Connect optional RitsuLib at the capture, save and load boundaries. | Real RitsuLib with differing nonempty primary/backup payloads; patch ordering, paired restoration and repeated reload-count rewrites. |
| Extend checkpoint validation and snapshot handling to the combined payloads. | Missing/empty/broken primary recovery; rejected required state and partial recovery; snapshot isolation; payload capture/injection and disk failures preserve the prior complete checkpoint. |
| Extend the isolated test setup for standalone and combined configurations. | Identical SameSeed deterministic results, existing foundation checks and simulated cloud operations. |

## Cloud and verification limits

The game store writes locally before uploading; failed remote writes retain the local checkpoint. At startup, remote data can replace newer local data when timestamps differ. If the remote file is absent and a local primary exists, synchronization deletes the local primary and backup. A lone local backup survives. Retain these game-store semantics.

Current [verification](../README.md#build-and-validate) exercises the loader, serializers, run managers and disk writer, plus simulated cloud operations. Evidence: `.cache/stage1-checks/<run>/result.json` and `game.log`. Combined RitsuLib checks remain pending.

Live Steam synchronization and normal-UI disable/re-enable remain unverified; second-device checks are outside the available setup. Headless runs emit rendering/preload and shutdown diagnostics.
