# Validation

## Change checklist

Apply the relevant checks. Reuse manual results while the tested behavior and setup remain unchanged.

- [ ] **Documentation:** check changed links, headings and word limits.
- [ ] **Code or dependencies:** run the [verification command](../README.md#build-and-validate) once on the final change, including RitsuLib when used. Pass: `All checks passed`.
- [ ] **Gameplay or bug fixes:** state the expected result, then verify it with existing tests or one focused reproduction. Use deterministic fixtures for rare-source combinations.
- [ ] **Startup or save/load:** also complete the two reloads below.

## Save/load check

Use one character, seed and mod setup. Win the first normal fight in a fresh run; note offered cards, gold, deck and balance before claiming rewards.

- [ ] Claim gold and one card; return to the menu and Continue in the reward room. **Pass:** the same offers return; balance and deck return to their pre-claim state.
- [ ] Claim rewards again, enter the next room, then return to the menu and Continue. **Pass:** collected gold and card persist exactly once; the room opens normally.

Stop after both checks pass.

## Tested results

2026-11-17, v0.113.1: formatting/analyzers, 64 unit tests, 917 standalone checks and 971 RitsuLib checks per initialization order passed.

2026-11-16: Ironclad A0/A10 wins with RitsuLib included 11 menu reloads with unchanged rewards/chests.

Cloud backup/repair and run-history filtering passed 14 isolated checks.

## Verification limits

Interactive validation of the completed combat implementation and live Steam synchronization remain unverified. Single-file `CloudSaveStore.OverwriteCloudWithLocal` bypasses backup/repair filtering.
