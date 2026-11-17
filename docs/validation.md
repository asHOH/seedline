# Validation

**Done means:** the required checks below pass for the tested build and mod setup. Stop at the first failure and record the seed, setup, action, expected result and actual result.

## Scope

| Change | Required work |
|---|---|
| Documentation | Check changed links, headings and word limits. |
| Code or dependencies | Run the [verification command](../README.md#build-and-validate) once on the final change. Pass: it finishes with `All checks passed`. Include RitsuLib when it is part of the intended setup. |
| Startup or save/load changes | Also perform the manual check below. |
| Gameplay change or reported bug | Check the affected behavior through existing automated coverage or one focused reproduction. State its expected result first. |

During fixes, use focused tests. Rare-source combinations belong in deterministic test fixtures. Reuse manual evidence while the tested behavior and setup remain unchanged.

## Manual check

**Budget:** one character, one seed, one mod setup, one normal fight and two reloads.

Start a fresh run and win the first normal fight. Note the offered cards, gold, deck and balance before claiming rewards.

| Action | Pass criterion |
|---|---|
| Claim gold and one card, then return to the menu and Continue while still in the reward room. | The same offers return; gold balance and deck return to their pre-claim state. |
| Claim rewards again, enter the next room, then return to the menu and Continue. | Collected gold and card persist exactly once, and the next room opens normally. |

Stop after both rows pass. An unfinished required check remains unverified.

## Current evidence

2026-11-17, v0.113.1: formatting/analyzers, 64 unit tests, 917 standalone checks and 971 RitsuLib checks per initialization order passed.

2026-11-16: Ironclad A0/A10 wins with RitsuLib included 11 menu reloads with unchanged rewards/chests. Interactive validation of the completed combat implementation remains pending.

Cloud backup/repair and run-history filtering passed 14 isolated checks. Live Steam synchronization remains unverified; single-file `CloudSaveStore.OverwriteCloudWithLocal` bypasses backup/repair filtering.
