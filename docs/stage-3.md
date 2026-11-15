# Stage 3: combat gold

Implements the [combat-gold policy](../DESIGN.md#combat-gold) using the existing run-owned random state and checkpoint format.

## Integration boundaries

| Operation | Interception and state |
|---|---|
| Identify ordinary combat gold | `RewardsSet.GenerateRewardsFor` postfix associates each generated gold reward with its encounter's normal or elite category. Tutorial overrides and extra/custom rewards bypass this boundary. |
| Generate and commit | `GoldReward.Populate` prefix samples the game's adjusted inclusive range from `gold reward + normal/elite + ordinal`. Each category advances once when a random offer is populated, including skipped offers. Repeated population retains the result. |
| Acquire | Vanilla reward selection and `PlayerCmd.GainGold` apply modifiers, truncation and history updates. |
| Restore | Existing checkpoint restoration restores counters before room reconstruction. Victory checkpoints regenerate the offer; next-room checkpoints retain consumption and the collected balance. |

The counters are run-wide and independent of floor, act, encounter identity and unrelated RNG consumption. Fixed payouts retain vanilla generation and leave mod counters unchanged. Gremlin Merc retains the game's reduced range, rounding and reward suppression. Battleworn Dummy and final bosses retain ordinary-reward suppression.

Parent-event reconstruction uses the game's saved `ParentEventId`, resume flag, encounter state and extra rewards. Stage 3 adds no reconstruction patch or save schema. Other gameplay alignment remains scheduled for steps 4–6.

## Automated coverage

The [validation command](../README.md#build-and-validate) includes engine checks for A0/A7 ranges, independent category consumption, route reordering, cross-act and Dread behavior, Bowler Hat acquisition, fixed/custom rewards, Gremlin Merc, final bosses, repeated reloads and next-room continuation. Event checks restore Dense Vegetation's gold reward and Battleworn Dummy's victory/timeout state through the real room-stack reconstruction and parent-resume paths.

Validated 2026-11-15: formatting and analyzer build, 32 unit tests, 139 standalone engine checks and 193 RitsuLib checks in each initialization order passed.

These checks use the game's test mode for reward interaction and construct completed battles at the checkpoint boundary. UI notifications retain normal-game behavior for RitsuLib's subscribers. The checks exercise production generation, selection, save/load and reconstruction code; the visible combat-to-reward transition requires gameplay verification.

## Gameplay checks

Deploy the validated package for a focused smoke test now; reserve broad full-run acceptance for step 6. Back up existing saves and start fresh with a fixed mod setup.

- Win a normal fight and an elite. Check displayed gold, collection and the balance; include Bowler Hat when available.
- Reload the victory checkpoint before and after collecting gold: the offer and pre-collection balance should return. Enter the next room and reload to confirm the collected balance persists and later gold continues correctly.
- Win an event combat that awards gold, then reload its victory checkpoint. Check the rewards and exit behavior. For Battleworn Dummy, check return to the parent event and the correct victory/timeout outcome.
- Repeat the essential save/reload checks with RitsuLib if that configuration will be used.

Deployment and manual gameplay remain pending. Live Steam synchronization retains the [stage 1 verification limits](stage-1.md#cloud-and-verification-limits).
