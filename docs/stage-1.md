# Stage 1: integration

The initializer checks the v0.113.0 build `16ac0fe3` assembly hash before installing Harmony patches. Run entry rejects Catfood, multiplayer, daily, Blitz and custom runs.

## Checkpoints

One JSON file combines the game's `SerializableRun` with versioned `same_seed` state. Each profile uses `same_seed_run.save` and its local `.backup`.

| Boundary | Integration |
|---|---|
| New run | `RunManager.SetUpNewSingleplayer` prefix: attach state before initialization. |
| Capture | `RunManager.ToSave` postfix: copy state onto the returned snapshot. |
| Save | `RunSaveManager.SaveRun(SerializableRun, bool)` prefix: freeze both halves before writing through the manager's `ISaveStore`; emit `Saved` after success. |
| Load | `RunSaveManager.LoadRunSave` prefix: validate schema, provenance and required run fields; attach state to the loaded snapshot. |
| Restore | `RunManager.SetUpSavedSingleplayer` prefix: copy state into the run before reload-count rewriting and room regeneration. |
| Discovery / deletion | `RunSaveManager.CurrentRunSavePath` getter prefix: override the path; preserve vanilla-save copying by the loader. |
| Cloud / stale cleanup | `SaveManager` postfixes: `EnumerateCloudSyncTasks` and `EnumerateOverwriteCloudWithLocalTasks` include the dedicated filename for all three profiles; `CleanupStaleCurrentRunSaves` applies vanilla history-based cleanup to validated completed runs. |

Reload-count rewrites use the loaded snapshot's state. Saving requires associated mod state, preventing adoption of vanilla runs. Checkpoint timing and reconstruction payloads remain vanilla.

The game store provides atomic replacement and backups. Missing, empty or torn primaries can recover a complete backup. Invalid metadata or run data rejects the load and preserves both files. Reads leave files unchanged; backup copying protects valid JSON from a torn primary. Deeper gameplay payloads rely on game restoration.

## Cloud and verification limits

Failed remote writes retain the local checkpoint; retries upload the whole file. Vanilla startup synchronization gives the cloud authority: differing remote data replaces local data; an absent remote file deletes the local primary and backup. Keep an external backup for cloud-failure testing.

[Verification](../README.md#build-and-validate) exercises the real loader, serializers, run managers and disk writer, plus the cloud coordinator with a mock remote. Evidence: `.cache/stage1-checks/<run>/result.json` and `game.log`.

Live Steam synchronization and normal-UI disable/re-enable remain unverified. Second-device checks are outside the current single-computer setup. Headless runs emit rendering/preload and shutdown diagnostics.
