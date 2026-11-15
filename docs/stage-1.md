# Stage 1: integration

The initializer verifies the game assembly's SHA-256 against v0.113.0 build `16ac0fe3` before applying bundled Harmony patches. Run-entry checks reject Catfood, multiplayer, daily, Blitz and custom runs.

## Checkpoint boundary

One JSON snapshot contains vanilla `SerializableRun` fields, serialized with the game's metadata, and a `same_seed` object containing format version, game build, run UUID and seed. Stage 1 stores provenance only; future RNG derivation excludes the UUID.

| Operation | Interception and state owner | Commitment / restoration |
|---|---|---|
| New run | `RunManager.SetUpNewSingleplayer` prefix; state attached to that `RunState`. | Before initialization and room generation. |
| Capture | `RunManager.ToSave` postfix; state attached to the returned `SerializableRun`. | Snapshot creation; later serialization uses this association. |
| Save | `RunSaveManager.SaveRun(SerializableRun, bool)` prefix. | Freeze both halves before awaiting the game's store write; emit `Saved` after success. |
| Load | `RunSaveManager.LoadRunSave` prefix. | Validate envelope, game schema and provenance; associate state with the returned vanilla snapshot. |
| Restore | `RunManager.SetUpSavedSingleplayer` prefix. | Restore before `IncrementNumReloads`, saved-run initialization and room regeneration. |
| Existence / deletion | `RunSaveManager.CurrentRunSavePath` getter prefix. | Existing discovery, completion and abandon flows address the dedicated filename and backup. |
| Cloud and stale saves | `SaveManager` cloud task enumerators and stale-run cleanup postfixes. | Include the dedicated filename for all three profiles; validated completed runs use vanilla history-based cleanup. |

The public path factory retains the loader's vanilla-save copy behavior. Each manager's provider supplies the profile ID. Saving requires an associated mod state, preventing mid-run adoption of vanilla snapshots.

### Reloads and backups

Loaded snapshots retain their associated state during reload-count rewrites, independently of the live run.

The store owns atomic replacement and `.backup` creation. Missing, empty or syntactically torn primaries can recover a complete backup. Rejected metadata, versions, seeds and run data preserve both files without fallback. Reads leave files unchanged. Validation checks required top-level fields, non-null player/act entries and current-act bounds; deeper gameplay payloads rely on game restoration.

`GodotFileIo.CopyBackup` checks JSON completeness, protecting a valid backup from a torn primary during the next synchronous or asynchronous write.

### Cloud

Writes use the manager's existing `ISaveStore`, including `CloudSaveStore`. The mod adds its checkpoint to startup download and upload tasks for all three profiles; backups remain local. Failed remote writes preserve the local snapshot, and retries upload the complete file.

Vanilla cloud authority remains in effect: startup download replaces differing local contents with the remote snapshot and deletes a local primary and backup when the remote primary is absent. A failed upload therefore preserves local data for the current session but carries vanilla's loss risk at a later cloud-authoritative startup. Keep an external backup when testing cloud failures.

## Validation

The [verification command](../README.md#build-and-validate) exercises setup/restoration, reload rewrites, atomic disk writes, both save modes, backups, profile separation, rejection, deletion and cloud failure/retry/download behavior.

Evidence is written to `.cache/stage1-checks/<run>/result.json` and `game.log`. A failed assertion fails the verification command.

On 2026-11-15, all **57 in-engine assertions passed** with SDK 10.0.100 and the cached, hash-verified scoped build. Checks use the real loader, patches, serializers, run managers, disk writer and cloud coordinator, with the game's mock remote backend. Headless rendering emits resource/preload and shutdown diagnostics.

### Remaining live verification

Live Steam synchronization and normal-UI disable/re-enable remain unverified. On a disposable profile, verify both:

- Write on one device, synchronize, continue on another, and check provenance and reload count.
- Disable SameSeed, confirm its run is hidden, then re-enable it and continue the same run.

## Reference evidence

The implementation targets the local v0.113.0 assembly and its cached decompilation. Relevant methods: `ModManager.TryLoadMod`, `RunManager.SetUpSavedSingleplayer`, `RunManager.ToSave`, `SaveManager.IncrementNumReloads`, `RunSaveManager.SaveRun`, `GodotFileIo.WriteFile/WriteFileAsync/CopyBackup`, and `CloudSaveStore.WriteFileAsync/SyncCloudToLocal/ShouldSyncFileToCloud`.

Stage 1 leaves checkpoint timing, room and event reconstruction payloads, rewards and random generation with the game.
