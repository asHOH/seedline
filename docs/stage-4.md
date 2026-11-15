# Stage 4: reward supplies and card occurrences

Implements the design's relic/chest, potion-generation, shop and card-offer policies on the stage 2 random-state foundation. Combat gold retains the stage 3 path.

## Integration boundaries

| Operation | Interception, state and commitment |
|---|---|
| Shared supplies | Run-owned `RewardState` stores finite allocations and infinite-sequence positions. Filter markers freeze eligible identity sets; entries keep original addresses through advancement and borrowing. Unfiltered draws remove positions. |
| Relics | `RelicFactory` front/back calls select generic, shop or Courier allocations. Generation consumes displayed stock; acquisition retires non-stackable identities across allocations. Wongos removes its first eligible entry directly. Relic Trader consumes Courier tail entries; its outgoing selection remains stage 5 work. |
| Chests | `TreasureRoomRelicSynchronizer.BeginRelicPicking` generates the non-empty chest's relic and gold before opening. The generic relic draw installs its finite-lifetime marker. Acquisition clears its carried entry; skipping retains it. `OneOffSynchronizer` pays the addressed gold through vanilla modifiers. Silver Crucible's empty chest bypasses generation. |
| Potions | Potion factories and named event draws use source-specific rarity and identity supplies. Alchemize, Brew, Frond, Coffer and Holster preview direct grants; `Player.AddPotionInternal` commits successful acquisitions. Reward displays commit during generation. `PotionRewardOdds.Roll` isolates drop checks and preserves White Beast Statue's freeze. |
| Shops | `MerchantInventory.CreateForNormalMerchant` allocates the shop ordinal. Card generation, sale selection and entry prices derive independently; restocks use Courier supplies. Price and reward modifiers retain their game hooks. Fake Merchant stock and prices use event RNG. |
| Card offers | `CardFactory.CreateForReward` builds shared triples or individual-entry offers, simulates Rare timing, applies upgrades and existing reward hooks, then performs presentation swaps. Lasting Candy uses Power supplies and joins its parent offer. Scroll Boxes handles substitution before triple consumption. Kaleidoscope and Neow's Bones use event RNG for their event-specific choices. |
| Card acquisition | Generated cards carry entry occurrences. Grid/bundle selections assign offer keys to committed picks; ordinary single-pick rewards assign their key at deck addition, before deck-add hooks. Starting cards receive occurrences after ascension setup. |
| Transformations | `Hook.ModifyCardBeingAddedToDeck` initializes missing replacement occurrences before modifiers can clone them, using the existing fallback grant addresses. Random and fixed transformations remain saveable; supply alignment and batch ordering belong to stage 5. |
| Copies and storage | Physical recreation retains occurrence metadata. Permanent copies receive new source addresses; Reflections and Clone rest-site batches assign copy indices in permanent-card processing order while preserving effect order. Pael's Tooth saves and restores the original occurrence and removes the returned serialized object by reference. |

## Checkpoint contract

Schema 3 stores reward supplies, frozen filters, chest exclusions and Rare timing in `same_seed.rewards`. Card addresses and keys travel with each `SerializableCard` in a reserved `SavedProperties` string, including cards stored by Pael's Tooth. The game ignores that property during ordinary field restoration; SameSeed rebinds it when reconstructing the physical card.

SameSeed registers `SameSeedOccurrence` during the game's property-cache initialization, including its numeric ID, reverse mapping and compatibility hash before the bit width is finalized. Single-player combat checksums and replay saving use this binary format; existing schema 3 JSON checkpoints retain their format.

Capture freezes reward state and card metadata with the vanilla snapshot. Restore completes before reward regeneration. Validation rejects missing/duplicate live card occurrences and malformed supply state while preserving checkpoint files. Schema 1/2 saves require a fresh run because their prior reward consumption cannot be reconstructed. RitsuLib remains optional with the same SameSeed format and RNG behavior.

## Coverage and remaining stages

Automated checks cover filtered advancement against an eager model, marker collisions and expiry, borrowing through exhaustion, shop isolation and stable prices, potion commitment/failure, chest entry/skips, rarity deferral, reward modifiers, all six characters at A0/A7, expanded-pool rerolls, Scroll Boxes, card copies, stored-card returns, random/fixed transformation metadata through modifier cloning and reload, and checkpoint replay.

Combat serialization checks explicitly enable checksums and replay recording, which test mode normally disables. They exercise the opening player turn, binary combat-state round-trip, stable checksums and replay saving through run cleanup, then decode the saved replay and validate deck and Pael's Tooth occurrences. The same checks run standalone and with RitsuLib in both initialization orders.

Validation on v0.113.1 passed formatting, the analyzer build (zero warnings/errors), 41 unit tests, 211 standalone engine checks and 265 RitsuLib checks in each initialization order. Run the [validation command](../README.md#build-and-validate) to check the current suite.

Permanent-card comparisons establish the applicable portion of `CARD_ORDER.md`; combat-only comparisons arrive with stage 6. Permanent transformation/direct-addition supplies, selection clocks, player-choice routing and Pael's Tooth's aligned return selection remain stage 5 work. Potion effect ordinals and combat-card lifetimes accompany their stage 6 consumers.

Independent correctness and over-engineering reviews led to preserving Claw eligibility in Scroll Boxes' ordinary bundle after substitution and removing a redundant identity lookup. Permanent ordering also covers draw effects applied through powers. Focused regression checks cover both gameplay fixes; review follow-up found no remaining actionable issues.

## Gameplay checks

Deploy v0.4.0 for a focused smoke test on a fresh run. Keep the fixed mod setup throughout; repeat essential reload checks with RitsuLib when used. Broad full-run acceptance belongs after stage 6.

- Complete the pending stage 3 combat-gold and event-combat checks.
- Compare ordinary rewards and the first shop across two same-seed routes. Reload before reward selection and after entering the following room; confirm offers, prices, selected cards and later progression.
- Acquire one chest relic, skip another and leave one chest unopened. Check later relic rewards and reloads; include Silver Crucible's empty chest.
- Exercise Courier purchases in different item-category orders and retry failed potion pickups with full slots/Sozu. Check that successful pickups advance the intended supply.
- Check larger/event card offers, Wing Charm with earlier enchantments, Lasting Candy, Scroll Boxes and Pael's Tooth storage/return. Confirm presentation, selection counts and visible upgrades.

Deployment and manual gameplay remain pending. Automated checks exercise production logic inside the engine with test-mode interaction; visible transitions and live Steam synchronization retain the stage 1/3 verification limits.
