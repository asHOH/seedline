# Deterministic seed mod — design

## Scope

Version: StS2 v0.111.0 and Catfood Mod v1.1.2.

Compare runs with matching seeds, versions, rules and eligible pools. This context will not be restated hereafter.

Single-player is the intended scope. Multiplayer behavior is excluded, except for easy changes that materially improve fairness.

Tutorial overrides, including scripted rewards and boss discovery order, are excluded.

**Discussion boundary:** compare random outcomes before predictable modifiers such as eggs, Fresnel Lens and fixed discounts. Preserve these effects and exclude them from all later randomness-control discussions; effects on random selection or sequence consumption remain in scope.

**Exhaustion boundary:** finite relic allocations may borrow on exhaustion; depletion of an entire rarity is excluded. These rare cases are outside ordinary isolation discussions throughout.

**Discussion boundary:** settled sections are fixed contracts, excluded from unfinished-design discussions.

Base findings on code; use run histories as supporting evidence.

## Goals

**Goal:** fair same-seed competition at equivalent ordinal opportunities. Corresponding shops should match regardless of route, with explicit exceptions below; purchases/restocks should not change later shops. Preserve meaningful choices while removing interference between unrelated systems.

**Fairness principle:** limit the difference one room creates compared with a run without that room. Even consuming the last sequence entry can matter greatly if it contains an exceptionally powerful relic.

## Terms

### Game context

- **Scarcity:** the Ascension 7 modifier, active at A7 and above. It reduces base Rare card chances, rarity-offset growth and card-upgrade odds; "without/with Scarcity" means A0–6/A7+.
- **Map type** (`MapPointType`): the map marker; `Unknown` (`?`) may resolve into different room types.
- **Room type** (`RoomType`): the resolved room—normal fight, elite, boss, chest, event, shop or rest site. Ancients are an Event room subtype, counted separately in this design. An event may start combat without changing the map type.
- **Event ID / encounter ID:** identifies a specific event / combat encounter, not its room type.
- **Normal fight:** `RoomType.Monster`; use **combat** for any battle, including elites, bosses and event battles. **Monster** in prose means an individual enemy.

### Rewards

- **Card reward offer:** cards presented together for one choice, usually three. A **card slot** is one position within an offer.
- **Item identity:** the card, relic or potion. A **sequence entry** is one occurrence at its original source position; repeats are distinct entries and removals never renumber survivors.
- **Ordinary relics:** Common / Uncommon / Rare / Shop. Event-exclusive relics sit outside these pools; scripting alone does not imply exclusivity.
- **Generation / acquisition:** generation selects an item; acquisition means actually receiving it. The potion acquisition policy also governs displayed offers, which consume on generation.
- **Reward sources:** **Courier** means restocks from The Courier relic, distinct from the Potion Courier event. **Generic rewards** groups combat rewards, events and other grants as specified per item type; relics and potions use separate sequences.

### Determinism

- **Allocation / sequence / RNG:** an allocation assigns relic identities to sources; a sequence orders outcomes; a random number generator (RNG) generates random choices.
- **Ordinal:** an opportunity's position within its category. Act-local ordinals reset each act; other ordinals continue across acts ("cumulative" is omitted unless needed for clarity).
- Act-local RNG seeds include the act number so resetting ordinals never restarts a previous act's RNG sequence.
- **Re-seeding** restarts a repeatable RNG sequence. Outcomes still depend on pools, rarity offsets, counters and other generators.
- **Same-seed divergence (歪种):** large early- or mid-run outcome differences that undermine fair same-seed comparison. Late-game differences from different total room-type/subtype ordinals are benign.

## Vanilla baseline

Vanilla provides the baseline Catfood improves on; exact probabilities and draw rules are in [generation and selection mechanics](#generation-and-selection-mechanics).

| Mechanism | Baseline and interference |
|---|---|
| Unknown room types | `?` resolution uses dedicated `UnknownMapPoint` RNG and changing odds. Odds reset between acts; RNG progress continues. Route exclusions and forced outcomes affect resolution. Catfood leaves this unchanged. |
| Event / encounter identities | Per-act lists are generated upfront with `UpFront` RNG. Events advance past ineligible or previously visited identities; `?` fights share the normal encounter list with visible fights. Catfood leaves identity selection unchanged. |
| Shops and events | Shops use `Shops` RNG for identities/prices and shared `Rewards` for card rarity/upgrades and relic rarity. Events may consume shared rewards/bags despite private RNG. Courier shares these generators; visits/restocks can shift later outcomes and prices. |
| Combat | Encounter setup uses floor-based addressing. Ordinary monster HP uses `Niche` RNG. Shuffle RNG persists across combats, so extra reshuffles can change a later opening hand. |
| Card rarity | Combat rewards update a shared rarity offset; shops read it without updating it. Rare rewards, including guaranteed boss Rares, reset it; Ancient entry does not. |
| Ancient options | Private event RNG uses run seed + event identity hash + player-slot contribution, independently of event counts. Ancient options use this RNG. |
| Chests | Relics use shared rarity RNG and identity bags; gold uses `Rewards`. Unlike elite rewards, chests exclude character-specific relics. Silver Crucible suppresses the first chest's rewards before generation. |
| Relic pools | Sources share finite identity bags, with different draw directions and eligibility filters. Depletion can change delivered rarity. |
| Potion drops | Checks use shared `Rewards` RNG and a shared changing probability, with no act/Ancient reset. Forced drops skip both the random draw and probability update. |
| Potion generation | Ordinary generation rolls rarity, then identity; some events use uniform-identity or fixed-rarity selection. Batches exclude duplicates. Failed acquisition does not restore consumed RNG state. |
| Transformations | Random replacements are uniform over eligible identities, excluding the original. Callers use event RNG, `Niche` or player `Transformations` RNG, so results depend on the source and its other consumption. |

## Proposed strategy: skipping strategy

Take the first eligible entry from the selected identity sequence, without another rarity roll where applicable. Retain skipped entries at their original positions for later eligible pulls. Borrowing preserves its specified selection order.

- **Relics:** preserve `IsAllowed` for draws/borrowing, `IsAllowedInShops` where required, and chest exclusion of character-specific relics. Shop/Courier skips remain available for generic rewards borrowing.
- **Potions:** preserve combat-generation restrictions and batch uniqueness. Permanently discard duplicate occurrences in shop batches only; identities may still recur.
- **Transformations:** exclude the original card identity.

**Known bias — mitigation deferred:** retained skips bias later eligible draws toward those entries. Shop-batch duplicate removal avoids this bias.

## Settled policy: shops, Wongos and Courier

**Catfood baseline:** tracks shop ordinal and isolates shop card-rarity state, while retaining `Shops` RNG for card identities and prices, shared relic bags/rarity rolls, and a shared potion-factory call index.

**Our improvements:**

- Isolate stock by shop ordinal, using independent source sequences and disjoint relic allocations to remove Catfood's interference from other rewards and Courier.
- Stabilize sale slots and original-entry prices instead of letting RNG call order change them.
- Limit Wongos to one shop Rare identity; consume nothing when its featured offer is unaffordable.

### Stock and prices

- Shop stock depends only on shop ordinal, except for Wongos and relic floor cutoffs.
- Give shops independent **card, relic-rarity, relic-identity and potion sequences**.
- **Card rarity:** vanilla shop base chances plus invariant offsets `+0.008` / `0` (without/with Scarcity), empirically lowered from the expected offset of repeating `3 normal → 1 elite → 2 normal → 1 elite` to account for boss resets.
- Sale slot depends only on shop ordinal, independently of identities and other RNG calls.
- Base price is addressed by run seed + source sequence + **original entry position**. It survives slot/shop shifts; repeated identities have distinct addresses. No advance generation is needed.
- **Courier:** separate card, relic-rarity/identity and potion sequences; advance neither shop nor generic rewards sequences, nor shop ordinal. Apply the same price rule and preserve [original mechanics](#generation-and-selection-mechanics).
- **Wongos:** featured offer consumes **one shop Rare identity**, no rarity entry; below 200 gold, consume nothing. Later Rare entries shift, e.g. `[A, B]` → `[B, C]`, limiting the change to one removed offer plus its replacement. Other options use [generic relic rewards](#source-classification-and-consumption).
- **Relic floor cutoffs (see [appendix](#generation-and-selection-mechanics)):** reaching corresponding shops on opposite sides of a cutoff can change their relics and shift later stock; these differences are inevitable and benign.

## Settled policy: relics

**Catfood baseline:** draws identities from shared relic bags and rarities from 400 precomputed rolls. Fixed-rarity requests consume identity only; identity depletion uses the [appendix fallback](#generation-and-selection-mechanics).

**Our improvements:**

- Use disjoint identity allocations and independent rarity rolls to isolate shops, Courier and generic rewards, which Catfood couples through shared state.
- Generate rarity rolls on demand, removing Catfood's 400-roll limit; roll 401 would fail (code finding, not reproduced).

### Allocation

Construct four disjoint relic-identity sequences: generic rewards, shop, Courier, and Shop-rarity. The first three each contain Common, Uncommon and Rare subsequences that advance independently. Shops own the shop and Shop-rarity sequences. Split each Common/Uncommon/Rare pool **45% / 35% / 20%** between generic rewards / shop / Courier; all Shop-rarity identities enter the dedicated sequence.

Fully unlocked: **122 ordinary relics per character**—26 Common, 32 Uncommon, 38 Rare, 26 Shop (115 shared + 7 character-specific). Capacity is per rarity/source.

Exact fully unlocked allocations (generic rewards / shop / Courier): **Common 12 / 9 / 5; Uncommon 14 / 11 / 7; Rare 17 / 13 / 8**.

Mix shop-ineligible relics into allocations normally.

**Implementation:** construct the shuffled identity allocations once, retain original entry positions and mark removals. Generate rarity rolls on demand with independent source RNGs; save allocation state and RNG progress.

### Source classification and consumption

Hidden random relic rewards from combat/chests/events share the generic rewards allocation, with source-specific eligibility, regardless of cost (including Ranwid, Wongos' Common and Mystery Box). Random rarity advances rarity + identity; fixed rarity advances only identity. Classify deferred payoffs separately from their granting relic.

**Consumption order:** preserve existing generation order and pull sequentially; deferred rewards consume entries when their payout is generated (e.g. elite relic before Mystery Ticket's three relics).

**Filtered pulls:** use the [skipping strategy](#proposed-strategy-skipping-strategy).

### Borrowing

On exhaustion, borrow the **last eligible remaining identity** of the requested rarity. Other sequences borrow from Courier first; Courier borrows from generic rewards first.

Only finite relic-identity allocations borrow; card/potion sequences and rarity rolls continue indefinitely.

**Final fallback — settled:** once same-rarity borrowing is exhausted, follow [vanilla fallback](#generation-and-selection-mechanics), or a simpler implementation that keeps the game running. This is outside the randomness-control discussion.

**Borrowed entries:** retain their original source sequence and entry position as the price/save address. Apply the receiving source's price rules; save transfers and consumption.

## Settled policy: potions

**Catfood baseline:** addresses drop checks by `player seed` and `cumulative check index`, starting at index 0, while preserving vanilla's changing probability. Forced drops also advance this index. Potion factories use a shared call index to seed each batch.

**Our improvements:**

- Separate **drop occurrence** and **acquisition rarity + identity**; isolate acquisition sources instead of sharing Catfood's factory-call index.
- Consume per potion. Catfood's call-based seeding can turn `A | BCD | E` for requests `1–3–1` into `AFG | B` for `3–1`, rather than `ABC | D`.
- Consume displayed offers on generation and direct grants only on successful acquisition, preventing Catfood's advancement on failed direct grants.
- Freeze occurrence state during forced drops, which advance Catfood's check index.

### Acquisition and sequences

- Keep independent **generic rewards, shop, Courier and combat-generation** sequences. Generic rewards combines random combat rewards, events and other grants without paid/free or per-event separation. **Combat-generation** names a source category, not a combat-only restriction: Alchemize, Entropic Brew (also outside combat) and Delicate Frond.
- Each source uses the full eligible potion pool with independent Common/Uncommon/Rare identity sequences. Identities may recur; sequences continue indefinitely, so borrowing is unnecessary.
- Within the generic rewards source, use separate rarity-roll sequences for **[standard weights](#generation-and-selection-mechanics)** and **uniform-identity selection**. Uniform mode weights each rarity by its share of eligible identities. Both modes consume the same per-rarity identity sequences. Fixed-rarity requests consume identity only. Other sequences only need standard weights rarity roll.
- **Consumption:** displayed offers consume on generation, even if declined or unclaimable; direct inventory grants consume only on successful acquisition. Failed direct grants leave rarity/identity RNG and sequence state unchanged; preserve existing failure feedback, including Alchemize's failure animation. Advance per potion, not per factory call; batch boundaries affect duplicate filtering only. Shop/Courier prices follow the settled original-entry price rule.
- **Filters:** use the [skipping strategy](#proposed-strategy-skipping-strategy).

### Drop occurrence

- Retain Catfood addressing and vanilla's shared changing probability across normal fights, elites and bosses. Extra ordinary checks advance the cumulative index; combat-category threshold differences are preserved. No act/Ancient reset.
- **White Beast Statue:** forced drops freeze both the occurrence index and stored probability; seed no new occurrence RNG and consume no occurrence random number. After removal, resume at the next unused ordinary-check index. Matching pre-Statue states therefore remain aligned despite different numbers of forced drops.
- Acquisition without a drop check does not advance occurrence state. Save the occurrence index and probability alongside acquisition-sequence progress and retained skips.

## Settled policy: chests

**Catfood baseline:** inherits Silver Crucible's suppression, but has no chest ordinal. Gold uses `Rewards`, re-seeded with the normal-fight count on chest entry. Relics use shared rarity rolls/bags.

**Our improvements:**

- Relics use the generic rewards allocation with the [skipping strategy](#proposed-strategy-skipping-strategy).
- Base gold uses run seed + cumulative non-empty chest ordinal to separate it from other room types.
- Silver Crucible's empty first chest advances neither chest ordinal nor relic sequences; the next chest is ordinal 1. Declining a generated relic does not undo consumption or the chest ordinal.

## Settled policy: permanent transformations

**Catfood baseline:** retains caller-supplied transformation RNGs rather than dedicated sequences by replacement pool.

**Our improvements:**

- Share seven cumulative identity sequences across permanent random transformation sources: the five characters, colorless and curse. Isolate them from other rewards and combat transformations.

### Selection and consumption

- Use the original card's pool; Quest cards and Event/Ancient/Token-rarity cards use colorless. Preserve [vanilla eligibility](#generation-and-selection-mechanics).
- Generate uniform identity entries without rarity rolls. Sequences continue indefinitely; repeats are allowed, including within batches. Apply the [skipping strategy](#proposed-strategy-skipping-strategy).
- Consume one eligible entry per generated replacement in existing generation order. Fixed replacements, canceled selections and previews consume nothing. Save sequence progress and retained skips.

## Settled policy: unknown-room resolution

**Catfood baseline:** uses the [vanilla baseline above](#vanilla-baseline); cumulative room counters do not determine `?` room type.

**Our improvements:**

- **Addressing:** room-type RNG uses run seed + act + act-local ordinal of visited `?` nodes, removing dependence on previous acts' `?` visit counts.
- **Shop chance:** when the map-adjacency policy excludes Shop, its stored chance still increases by 3 percentage points. Preserve existing Juzu Bracelet, Golden Compass and Lantern Key room-type restrictions.
- **Thresholds:** allowed room types' stored chances determine interval widths. The chest interval begins at 0, the shop interval ends at 1, and the normal-fight interval stays centered at `7/15` unless shifted just enough to avoid overlap. When the total exceeds 100%, pack chest → normal fight → shop from 0 and truncate at 1, reducing effective interval widths without capping stored chances. The shop interval shrinks first; in the extremely rare case that chest + normal-fight chances alone exceed 100%, the normal-fight interval is also truncated and shop gets no space. Unassigned space resolves to Event.

## Remaining design: rewards and encounter

| Category | Determines | Primary progression |
|---|---|---|
| Rewards | Generated reward identities and amounts, including permanent transformation results | Cumulative ordinals |
| Encounter | Random selection of affected cards/items, room content, named event options and combat randomness | Act-local ordinals |

Classify random decisions, not whole effects. Player choices and fixed selections belong to neither category. Sequence scope and addressing are separate fairness decisions; the progression above is a default, not a definition.

Distinguish normal fights, elites, bosses, Ancients, chests, events and special relic effects only where their rules differ.

**Catfood addressing:** cumulative normal-fight/elite/boss/shop/event/Ancient counters. Ancients increment both `eventEntered` and `ancientVisited`; options stay independently seeded, while Ancient card overrides use `ancientVisited`. Room reward RNG uses room-type counts, a normal-fight fallback and event-card overrides.

**Settled:** Rewards - relics, chests, potions and permanent transformations; Encounter - shops and unknown-room resolution.

### Rewards

#### Cards: offers, rarity and rerolls

- **Catfood:** combat identities/upgrades use room type, count and offer position. Combat rerolls still update that room type's rarity state, potentially shifting later offers.
- **Catfood identity consumption:** rarity is rolled before identity; a Rare consumes the current slot's identity draw. There are no independent per-rarity identity sequences. Later identities can still change through rarity-offset resets and offer duplicate exclusion.
- **Code-confirmed Catfood bug:** ordinary card rewards share initial and reroll options, so both consume one reroll-seeded identity/upgrade stream. Gameplay impact not reproduced in-game.
- **Catfood rarity:** separate normal-fight, elite, boss and shop offsets use vanilla base probabilities, growth and rare-reset value. Boss rewards reset only the boss offset; Ancient entry resets all four to `-0.05`, including Neow. Shop rolls do not grow their isolated offset, leaving their Rare roll chance at 4% (0% with Scarcity).
- **Elite initialization — proposed:** on Ancient entry, including Neow, set the elite offset to `0.01`/`-0.02` (without Scarcity/with Scarcity) when the other offsets reset, approximating two normal fights. Retain vanilla non-rare growth.
- **Offset resets — settled:** preserve combat rarity resets to `-0.05` after Rare rolls and resets on Ancient entry.
- **Act alignment — open:** find other ways to prevent divergence while preserving offset resets.
- **Catfood direct/event generation:** seeds add room/event count to a per-room options-call index. Seed collisions exist; harmful gameplay impact remains unverified.

**Unadopted suggestion:** stable direct-generation scopes/indices, if a reproduced issue warrants them.

**Reroll example** (Scarcity): three non-rares raise offset `0` → `0.015`. Next matching normal fight, draw `0.025` gives Uncommon versus Rare (about 1.5% versus 3%). A rare reroll instead resets the offset. Assess the impact against the 歪种 definition.

#### Other gains and changes

**Open:** gold outside chests; define equality and sharing by source/effect.

### Encounter

#### Shops, chests and events

- **Shops and chests:** settled above.
- **Relic Trader — deferred:** named choices belong to Encounter. Decide which owned relics and replacements are offered, their source, and when offered/chosen/unchosen replacements are consumed. See [event rules](#relic-and-event-rules) and [selection mechanics](#generation-and-selection-mechanics).
- **Event-exclusive relics:** part of event encounters, already outside ordinary pools; no manual separation needed. Keep event identities/sequences route-independent and preserve choices/costs (Doll Room, Forgotten Soul / Sword of Stone, Trash Heap, Fake Merchant).

#### Selection and setup

- **Catfood identity baseline:** inherits [vanilla event/encounter selection](#generation-and-selection-mechanics); its cumulative room counters do not select the event/encounter identity.
- **Catfood:** encounter ID + cumulative room-type ordinal controls variable composition/setup, not ordinary HP. No visible setup mismatch reproduced.
- **Proposed:** act-local setup ordinals, distinguishing source/effect where necessary.
- **Lantern Key proposal:** forced Act 3 War Historian Repy gets **ordinal 0**, leaving ordinary events untouched; Catfood counts it normally.
- **Unadopted suggestion:** separate forced-event selection from `?` room-type resolution.

#### Permanent card changes

**Open:** random card selection for permanent upgrades/downgrades; define sharing by source/effect.

#### Combat randomness

- **Catfood:** re-seeds shuffle RNG at combat start; later shuffles continue it.
- **Proposed:** act-local opening-shuffle ordinals. First Act 2 normal fight then aligns setup/shuffle addresses despite different Act 1 normal-fight counts; cumulative reward addresses still differ.
- **Open:** HP/AI, random targets/costs/orbs, shuffles, temporary upgrades/downgrades, combat transformations and generated cards by pool/type; decide sharing and special-relic interactions.

## Next steps and validation

**Next:** Address remaining Encounter design, including Relic Trader, other events/Ancients and combat setup.

**Validation suggestion:** targeted RNG logs and paired runs; histories alone miss internal calls/actions. Use consistent seed derivation.

**Separate improvement:** add run seed to boss shuffle initialization.

**Pending:** verify save/load preserves counters and outcomes.

## Reference

[Event spreadsheet](references/StS2_Events.xlsx): **57 ordinary/forced events and 8 Ancients**. `is_deterministic` concerns offered choices, not hidden rewards: Punch-Off is true; Wongos is false. Detailed criteria and state restrictions are in the workbook.

## Appendix: existing mechanics

### Relic and event rules

| Source | Rules |
|---|---|
| Wongos | Act 2; all players have ≥100 gold. Featured Rare: 200 gold; hidden Common: 100 gold. Mystery Ticket pays three random relics after five combats. Leaving downgrades a random upgraded card, if any. |
| Relic Trader | Acts 2–3; all players own ≥5 tradable relics. Offers a choice among three owned relics and three replacements. Normally no leave. |
| Courier | 20% discount; removal never restocks; sale clears on restock. |
| Silver Crucible | Neow relic; first treasure room is empty. Suppresses both base gold and relic generation. |
| Fixed potion grants | Drowning Beacon gives Glowwater Potion; Potion Courier offers three Foul Potions. Both identities are Event-rarity and outside ordinary random pools. |
| Combat-generation effects | Alchemize generates one potion. Entropic Brew fills empty slots, including outside combat. Delicate Frond fills empty slots before combat. |
| White Beast Statue | Forces combat potion rewards and is normally tradable at Relic Trader. |
| Permanent transformations | Inputs are player-selected or fixed by the effect. Leafy Poultice takes the first Basic Strike and Defend; Pandora's Box takes all removable Basic Strikes/Defends. Astrolabe transforms three selected cards and upgrades the results. Claws replaces selected cards with Maul. |

### Generation and selection mechanics

Vanilla mechanics unless explicitly attributed to Catfood.

**Unknown-room resolution:** base chances are 10% normal fight / 2% chest / 3% shop; Event takes the remainder (initially 85%). Elites are disabled under ordinary rules. Each roll consumes one `UnknownMapPoint` draw. The selected non-event type resets to its base chance; other allowed non-event types gain their base amount. Excluded types retain their stored odds. Exclusions apply before interpreting the draw, without renormalizing the remaining probabilities.

**Unknown-room overflow:** vanilla checks cumulative thresholds in normal fight → elite (if enabled) → chest → shop order. Totals above 100% are neither normalized nor capped in stored state; intervals beyond 1 are unreachable, so later types lose effective probability and Event has none. At 70% normal fight / 14% chest / 21% shop, effective chances are 70% / 14% / 16%.

**Event / encounter identity selection:** room type is resolved first. An Event advances through its act's pre-shuffled list to the next `IsAllowed` identity not in the run-wide visited-event set; skipped entries are passed over, not retained for the next eligible pull. A normal fight uses the next entry in the act's normal encounter list, regardless of whether its map marker was `?` or a visible fight. Forced-event replacement occurs after ordinary event eligibility scanning.

**Card rarity:** shared offset starts at `-0.05` and resets there after a Rare, but not on Ancient entry. Normal/elite non-rare rolls add `0.01` (`0.005` with Scarcity), capped at `0.40`. Base Rare chances for normal/elite/shop rolls are 3%/10%/9% (Scarcity: 1.49%/5%/4.5%), plus the offset; base Uncommon chances are 37%/40%/37%. Boss rewards guarantee Rare and reset the shared offset. Shop rolls read the offset without updating it.

**Normal versus elite card rewards:** both offer three cards from the same character pool, exclude duplicates within the offer and use the same upgrade rules; elites have no inherent upgrade-chance bonus. Catfood preserves these rules.

**Relic draws:** rarity is 50% Common / 33% Uncommon / 17% Rare. Shops pull two random-rarity relics and one Shop relic from the back of the bags; rewards/Wongos pull from the front. Run-state eligibility (`IsAllowed`) removes disallowed relics before draws; five relics are additionally shop-ineligible.

**Identity depletion:** from the requested rarity, search forward along Shop → Common → Uncommon → Rare, then multiplayer fallback; otherwise Circlet. Refresh-enabled bags first refill empty requested rarities. Catfood inherits this shared-bag fallback.

**Ordinary relic eligibility:** the following 17 relics require `TotalFloor < 41` in single-player or `< 38` in multiplayer (`IsBeforeAct3TreasureChest`): Amethyst Aubergine, Book of Five Rings, Bowler Hat, Dragon Fruit, Frozen Egg, Girya, Juzu Bracelet, Lasting Candy, Lucky Fysh, Meal Ticket, Molten Egg, Old Coin, Planisphere, Shovel, Toxic Egg, White Beast Statue and White Star. The cutoff uses total floor, not whether a chest was visited. (Off-scope note: Lasting Candy is also disallowed if any Ironclad player has `UnlockState.NumberOfRuns == 0`.)

**Potion drops:** one shared probability starts at 40%; a generated drop subtracts 10 percentage points, a miss adds 10. Elites add 12.5 percentage points to the check. No act/Ancient reset. Checks use shared `Rewards` RNG; forced drops skip both the random draw and probability update.

**Potion generation:** ordinary generation rolls 65% Common / 25% Uncommon / 10% Rare, then identity. Factory batches exclude duplicate identities within the batch; separate calls can repeat them. Generation precedes acquisition; failed acquisition does not restore consumed RNG state.

**Permanent transformation's target:** use the original card's pool, except Quest cards and Event/Ancient/Token-rarity cards use colorless. Token is the generated-card rarity used by Shiv, Soul and similar cards. Ordinary results are Common/Uncommon/Rare; curses share one curse pool. Preserve unlock/player restrictions and exclude the original identity. Selection is uniform over eligible identities, with no batch uniqueness. Combat transformations additionally filter by `CanBeGeneratedInCombat` and may use the Status pool.

| Source | Mechanics |
|---|---|
| `?` shop exclusion | Shop is excluded when the previous visited map point contained a shop, or every outgoing map node is a shop. |
| Juzu Bracelet / Golden Compass | Juzu excludes normal fights from `?` resolution. Golden Compass forces Event during its affected act. |
| Lantern Key | In Act 3, forces `?` to Event and replaces the selected event with War Historian Repy. The ordinary event-list cursor still advances. Like Golden Compass, forced resolution still consumes one `UnknownMapPoint` draw while non-event odds freeze. |
| Chests | Draw from `SharedRelicGrabBag`, populated from `SharedRelicPool`; character-specific relics are excluded even in single-player. Relics are generated on room entry; base gold is generated on opening. |
| Wongos | Featured Rare is consumed when offered even if unaffordable/declined; hidden Common is generated on purchase. Both require `IsAllowedInShops`. |
| Relic Trader | Randomly selects three owned relics and three replacements; consumes all three replacement identities/rarities before selection. Replacement rarity is independent and replacements need not be tradable. Tradable excludes Starter/Event/Ancient, upon-pickup, used-up, melted and pet-spawning relics. |
| Courier | Restocked character cards retain type, colorless cards retain rarity. Relics roll ordinary rarity even in the Shop slot. Shop ordinal does not advance. |
| Fixed-rarity random potions | Potion Courier's Ransack gives Uncommon; Crystal Sphere specifies the rarity and selects identity with event RNG. The Courier's shop restocks do not guarantee Uncommon. |
| Uniform-identity event potions | Battleworn Dummy, Endless Conveyor, The Legends Were True and Wellspring select uniformly from eligible potion identities. |
| Potion factory batches | Initial shop stock: 3; Alchemical Coffer on acquisition: 4; Phial Holster on acquisition: 2. These are the three multi-potion factory callers; each batch excludes duplicate identities. |
| Combat-generation filters | Alchemize excludes Fairy in a Bottle, Fruit Juice and Regen Potion. Entropic Brew and Delicate Frond use unrestricted generation. |
| Potion acquisition failures | Full slots or Sozu can reject acquisition after generation. Alchemize attempts generation even with full slots. Entropic Brew and Delicate Frond generate only while slots are open and stop on the first failed acquisition. Phial Holster adds one slot but generates two potions, so its second grant can fail. |
| White Beast Statue | Freezes the stored drop probability while owned; removal resumes it without a reset. The final boss generates no ordinary reward set and performs no drop check. |
