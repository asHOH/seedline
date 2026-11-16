# Stage 5: non-combat policies

Implements the [non-combat policies](../DESIGN.md#source-policies--encounter--non-combat) and [permanent transformations/direct additions](../DESIGN.md#permanent-transformations-and-direct-card-additions). Stage 4 supplies Wing Charm, event-specific rewards and Relic Trader's replacement stock.

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
| Transformations | `CardCmd.Transform` resolves random replacements in identity order before vanilla removal and effects. Astrolabe, Morphic Grove and Trial pre-resolve selected batches before sequential effects; Pandora's Box defers generation to the shared batch. Fixed results retain their source attributes. |
| Direct additions | Source-local factory calls cover Automaton, Fried Eel, Arcane Scroll and Waterfall; curse calls cover Neow's Bones, Cape and Museum. Draw from shared permanent-card supplies, attach entry addresses before modifiers, and preserve source rarity filters and duplicate rules. |
| Unknown rooms | `UnknownMapPointOdds.Roll` uses act-local room ordinals and the specified interval placement, preserving exclusions and chance updates. Compass and Lantern Key force Event without consumption. |
| Ordinary events | `ActModel.PullNextEvent` uses condition counts, ID queues and priorities. Act queues rebuild lazily before selection; skipped acts still receive their insertion step. Commit visits when selected. Lantern Key records Repy directly. Vanilla's original shuffled list remains the fallback. |
| Ancient menus | Filter targeted pools by fixed priorities; Pael's second pool retains its 1:2 weights. The three-pool helper retains vanilla RNG calls for unaffected components. Tanx and Nonupeipe display three choices in priority order. |
| Other event choices | Future of Potions caches type assignments for the first three potions. Ranwid ranks tradable relics; Relic Trader selects outgoing candidates by minimum pair priority, then assigns reward slots. Conveyor replaces only its random dish roll, advancing past ineligible dishes; fixed salads bypass it. |

Run-owned `SelectionState` stores clocks and renewal counts by scope and selection key. Card reassociation moves the existing occurrence/key metadata; clocks follow those keys through status changes and Tooth storage. New copies and transformation results use new occurrence keys.

Permanent-card supplies reuse `RewardState`; room and dish positions reuse `RandomState`. `EventState` holds act, ordinary-event ordinal, per-condition ID order/position, remaining count, priority and roll-sequence position. Checkpoint capture deep-copies all state; restoration precedes room regeneration. Schema 5 rejects earlier development saves without changing their files.

## Validation

Automated checks cover clock correction/renewal, equivalent-copy routing, cancellation, persistent-selection sources and checkpoint isolation/replay. Non-combat checks add transformation batch reversal and source hooks, direct-addition filters and addresses, room intervals/forced events, event carryover/wrapping/fallback, condition-weighted sampling, Ancient menus, potion types, Trader overlap and Conveyor consumption. Patch installation checks source interception counts.

2026-11-16, v0.113.1: formatting and analyzer build passed, with 58 unit tests, 351 standalone engine checks and 405 checks with RitsuLib v0.6.14 in each initialization order. Two independent agent reviews identified sequential transformation batches in Morphic Grove/Trial and Ranwid's empty-relic fallback; both fixes passed source-level regressions and reviewer follow-up. The v0.113.0 API build passed; runtime acceptance remains pending.

## Gameplay checks

Use a fresh v0.5.1 run; repeat essential reload cases with RitsuLib when used.

- Smith or enchant different equivalent copies after reloading. Compare later random upgrades and surviving cards; confirm the clicked position receives the effect.
- Cancel a selector, then repeat the choice. Check multi-card removal/transformation and Amalgamator's selected inputs.
- Store cards with Pael's Tooth, save/reload, and follow all five returns; verify upgrades and stored-copy removal.
- Exercise Reflections after random upgrades, Bridge rerolls through fallback, and Ranwid/Stone with duplicate potion types.
- Reverse Astrolabe/Morphic Grove selections after reload; compare results and later transformations/direct additions, including Wing Charm and egg modifiers.
- Compare corresponding `?` visits across routes and acts, then reload ordinary events. Check Compass and Lantern Key consumption separately.
- Change Ancient eligibility, rearrange Future of Potions' offered potions, trade at Relic Trader, and buy Conveyor dishes through a fixed salad display.

Interactive acceptance remains pending. Combat policies and potion-effect ordinals remain Stage 6 work.
