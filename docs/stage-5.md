# Stage 5: persistent selection and deck routing

This increment implements [persistent selection](../DESIGN.md#persistent-selection) and [out-of-combat routing](../DESIGN.md#out-of-combat-card-routing). Transformation/direct-addition supplies, room and event generation, Ancient offerings and remaining event policies follow separately.

## Integration boundaries

| Operation | Interception and commitment |
|---|---|
| Random permanent upgrades | Source-local selection calls for Whetstone, War Paint, Fragrant Mushroom, Sand Castle, War Hammer, Fishing Rod, Battleworn Dummy, Endless Conveyor and Tablet of Truth; Improvement's combat-end hook. Commit the selected batch before effects. Fixed-target and explicit affect-all paths bypass clocks. |
| Downgrades | Wongos's selection and Reflections' two batches. Reflections commits downgrades from one snapshot, processes refills in pre-selection clock order, then determines upgrade candidates. Upgrade refills use the shared run sequence; downgrade refills use event RNG. |
| Player choices | Deck upgrade, transformation, removal and enchantment selectors capture eligible copies before waiting. Committed results reassociate logical occurrences in presentation order while retaining clicked objects and effect order. Empty/cancelled results consume nothing. |
| Special player choices | Pael's Tooth's initial selection uses upgrade routing; Wood Carvings' two generic transformation selectors use removal routing. Copy selections retain their existing path. |
| Fixed priorities | Beautiful Bracelet's eligible targets and Pael's Tooth's stored return selection use separate card-key priorities. |
| Slippery Bridge | Retain vanilla candidate filtering and fallback; commit each offer and renew the preceding declined occurrence before reselection. |
| Potion sacrifices | Ranwid and Stone of All Time share type clocks weighted by owned counts. Commit and renew when offered; single-potion inventories preserve clocks. Copy choice retains slot order. |

Run-owned `SelectionState` stores clocks and renewal counts by scope and selection key. Card reassociation moves the existing occurrence/key metadata; clocks follow those keys through status changes and Tooth storage. New copies and transformation results use new occurrence keys. Checkpoint capture deep-copies selection state; restoration precedes room regeneration. Schema 4 rejects earlier development saves without changing their files.

## Validation

Automated checks cover batch correction, inactive candidates, renewal, malformed saved clocks, equivalent-copy upgrade/removal choices, click-order-independent enchantment batches, cancellation, checkpoint isolation/replay, Whetstone, downgrade refills, Dingy Dagger, Reflections, Tooth storage/return, Bracelet, Bridge and weighted potion offers. Patch installation checks each source interception count.

2026-11-16, v0.113.1: formatting and analyzer build passed, with 48 unit tests, 282 standalone engine checks and 336 checks with RitsuLib v0.6.14 in each initialization order. Two independent agent reviews found no actionable issues. This increment's v0.113.0 execution remains pending.

## Gameplay checks

Use a fresh v0.5.0 run; repeat essential reload cases with RitsuLib when used.

- Smith or enchant different equivalent copies after reloading. Compare later random upgrades and surviving cards; confirm the clicked position receives the effect.
- Cancel a selector, then repeat the choice. Check multi-card removal/transformation and Amalgamator's selected inputs.
- Store cards with Pael's Tooth, save/reload, and follow all five returns; verify upgrades and stored-copy removal.
- Exercise Reflections after random upgrades, Bridge rerolls through fallback, and Ranwid/Stone with duplicate potion types.

Interactive acceptance remains pending; broader stage 5 policies retain their existing behavior.
