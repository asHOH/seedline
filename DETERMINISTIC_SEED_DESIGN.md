# Deterministic seed mod — design

## Scope

Version: StS2 v0.111.0 and Catfood Mod v1.1.2.

Compare runs with matching seeds, versions, rules and eligible pools. This context will not be restated hereafter.

Single-player is the intended scope. Multiplayer behavior is excluded, except for easy changes that materially improve fairness.

Tutorial overrides, including scripted rewards and boss discovery order, are excluded.

Extreme conditions such as depleting a rarity of relics are also excluded.

**Discussion boundary:** compare random outcomes before predictable modifiers such as eggs, Fresnel Lens and fixed discounts. Preserve these effects and exclude them from all later randomness-control discussions; effects on random selection or sequence consumption remain in scope.

**Exhaustion boundary:** finite relic allocations may borrow on exhaustion. This rare exception applies throughout; exclude it from ordinary isolation discussions.

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

### Determinism

- **Ordinal:** an opportunity's position within its category. Act-local ordinals reset each act; other ordinals continue across acts ("cumulative" is omitted unless needed for clarity).
- **Re-seeding** restarts a repeatable RNG sequence. Outcomes still depend on pools, rarity offsets, counters and other generators.
- **Same-seed divergence (歪种):** huge, usually malicious difference happening early-run or mid-run for the same seed. (permit late-game difference due to players getting different total ordinals for each room type/subtype; that's benign) This is what we try to avoid, as stated by Fairness principle.

## Vanilla baseline

Vanilla provides the baseline Catfood improves on:

| Mechanism | Baseline and interference |
|---|---|
| Shops and events | Shops use `Shops` RNG for identities/prices and shared `Rewards` for card rarity/upgrades and relic rarity. Events may consume shared rewards/bags despite private RNG. Courier shares these generators; visits/restocks can shift later outcomes and prices. |
| Combat | Encounter setup uses floor-based addressing. Ordinary monster HP uses `Niche` RNG. Shuffle RNG persists across combats, so extra reshuffles can change a later opening hand. |
| Card rarity | Shared offset starts at `-0.05` and resets there after a rare, but not on Ancient entry. Normal/elite non-rare rolls add `0.01` (`0.005` with Scarcity), capped at `0.40`. Base Rare chances for normal/elite/shop rolls are 3%/10%/9% (Scarcity: 1.49%/5%/4.5%), plus the offset. Boss rewards guarantee Rare and reset the shared offset to `-0.05`. Shop rolls read the offset without updating it. |
| Ancient options | Private event RNG uses run seed + event identity hash + player-slot contribution, independently of event counts. Ancient options use this RNG. |
| Chests | Ordinary relic rarity roll + `SharedRelicGrabBag` identity; unlike elite rewards, chests exclude character-specific relics even in single-player. Gold uses `Rewards`. Silver Crucible suppresses the first chest's relic and gold before either is generated. |
| Relic pools | Rarity: 50% Common / 33% Uncommon / 17% Rare. Shops pull two random-rarity and one Shop relic from the back; rewards/Wongos pull from the front. Run-state eligibility (`IsAllowed`) removes disallowed relics before draws; five relics are additionally shop-ineligible. Depletion can change delivered rarity. |
| Potion drops | One shared probability starts at 40%; a generated drop subtracts 10 percentage points, a miss adds 10. Elites add 12.5 percentage points to the check. No act/Ancient reset. Checks use shared `Rewards` RNG; forced drops skip both the random draw and probability update. |
| Potion generation | Ordinary generation rolls 65% Common / 25% Uncommon / 10% Rare, then identity. Some events select uniformly across eligible identities or guarantee a rarity. Factory batches exclude duplicate identities within the batch; separate calls can repeat them. Generation precedes acquisition; failed acquisition does not restore consumed RNG state. |

## Settled policy: shops, Wongos and Courier

**Improvements over Catfood (design, not implementation):**

- Isolate shop stock from other rewards and Courier consumption, including relic identities and potion generation.
- Stabilize sale slots and occurrence prices instead of letting RNG call order change them.
- Limit Wongos to one shop Rare identity; consume nothing when its featured offer is unaffordable.
- Replace shared relic bags with disjoint source allocations.

### Stock and prices

- Shop stock depends only on shop ordinal, except for Wongos.
- Give shops independent **card, relic-rarity, relic-identity and potion sequences**.
- **Card rarity:** vanilla shop base chances plus invariant offsets `+0.008` / `0` (without/with Scarcity), empirically lowered from the expected offset of repeating `3 normal → 1 elite → 2 normal → 1 elite` to account for boss resets.
- Sale slot depends only on shop ordinal, independently of identities and other RNG calls.
- Base price is addressed by run seed + source sequence + **original entry position**. It survives slot/shop shifts; repeated identities have distinct addresses. No advance generation is needed.
- **Courier:** separate card, relic-rarity/identity and potion sequences; advance neither shop/generic cursors nor shop ordinal. Apply the same price rule and preserve [original mechanics](#appendix-existing-mechanics).
- **Wongos:** featured offer consumes **one shop Rare identity**, no rarity entry; below 200 gold, consume nothing. Later Rare entries shift, e.g. `[A, B]` → `[B, C]`, limiting the change to one removed offer plus its replacement. Other options use [generic relic rewards](#source-classification-and-consumption).

## Settled policy: relics

**Catfood:** shared bags and 400 precomputed rarity rolls couple shops, combat, chests and events. Fixed-rarity requests skip rarity consumption. Roll 401 would fail (code finding, not reproduced); identity depletion uses the appendix fallback.

### Allocation

Construct four disjoint relic-identity sequences: generic rewards, shop, Courier, and Shop-rarity. The first three each contain Common, Uncommon and Rare subsequences that advance independently. Shops own the shop and Shop-rarity sequences. Split each Common/Uncommon/Rare pool **45% / 35% / 20%** between generic rewards / shop / Courier; all Shop-rarity identities enter the dedicated sequence.

Fully unlocked: **122 ordinary relics per character**—26 Common, 32 Uncommon, 38 Rare, 26 Shop (115 shared + 7 character-specific). Capacity is per rarity/source.

Mix shop-ineligible relics into allocations normally. When shop-ineligible entries are reached in shop/Courier draws, skip them without another rarity roll, but retain them at their original positions for borrowing by generic rewards.

**Implementation:** construct the shuffled identity allocations once, retain original entry positions and mark removals. Generate rarity rolls on demand with independent source RNGs; save allocation state and RNG progress.

### Source classification and consumption

Hidden random relic rewards from combat/chests/events share the generic allocation, with source-specific eligibility, regardless of cost (including Ranwid, Wongos' Common and Mystery Box). Random rarity advances rarity + identity; fixed rarity advances only identity. Classify deferred payoffs separately from their granting relic.

**Consumption order:** preserve existing generation order and pull sequentially; deferred rewards consume entries when their payout is generated (e.g. elite relic before Mystery Ticket's three relics).

**Filtered pulls:** take the first eligible identity, preserving vanilla run-state eligibility (`IsAllowed`) for draws and borrowing. Source-specific filters are `IsAllowedInShops` where required and exclusion of character-specific relics for chests. Entries skipped by these filters remain available at their original positions for later eligible pulls, without another rarity roll. Shop/Courier skips follow the [allocation rule](#allocation); disjoint allocations already handle Courier's displayed-relic exclusion.

**Known issue — deferred:** filtered draws leave skipped entries near the front, doubling the chance that the next unfiltered draw of the same rarity yields a shop-ineligible relic after Wongos' Common offer, or a character-specific relic after a chest. Investigate mitigation later.

### Borrowing

On exhaustion, borrow the **last eligible remaining identity** of the requested rarity. Other sequences borrow from Courier first; Courier borrows from generic first.

Only finite relic-identity allocations borrow; card/potion sequences and rarity rolls continue indefinitely.

**Final fallback — settled:** once same-rarity borrowing is exhausted, follow [vanilla fallback](#appendix-existing-mechanics), or a simpler implementation that keeps the game running. This is outside the randomness-control discussion.

**Borrowed entries:** retain their original source sequence and entry position as the price/save address. Apply the receiving source's price rules; save transfers and consumption.

## Settled policy: potions

Separate **drop occurrence** and **acquisition rarity + identity**. All policies are settled.

**Catfood:** drop checks use `player seed` and `cumulative check index`, starting at index 0, while preserving vanilla's changing probability. Forced drops still advance this index.

**Catfood acquisition limitation:** factory RNG advances once per call regardless of batch size or acquisition success. Batching therefore changes outcomes: `A | BCD | E` for requests `1–3–1` can become `AFG | B` for `3–1`, rather than `ABC | D`.

### Acquisition and sequences

- Keep independent **generic, shop, Courier-restock and combat-generation** sequences. Generic combines random combat rewards, events and other grants without paid/free or per-event separation. Combat generation includes Alchemize, Entropic Brew (also outside combat) and Delicate Frond.
- Each source uses the full eligible potion pool with independent Common/Uncommon/Rare identity sequences. Identities may recur; each source draws from the full eligible pool; sequences continue indefinitely, so borrowing is unnecessary.
- Within generic sequence, use separate rarity-roll sequences for **standard weights** and **uniform-identity selection**. Uniform mode weights each rarity by its share of eligible identities. Both modes consume the same per-rarity identity sequences. Fixed-rarity requests consume identity only. Other sequences only need standard weights rarity roll.
- **Consumption:** displayed offers consume on generation, even if declined or unclaimable; direct inventory grants consume only on successful acquisition. Failed direct grants leave rarity/identity RNG and sequence state unchanged; preserve existing failure feedback, including Alchemize's failure animation. Advance per potion, not per factory call; batch boundaries affect duplicate filtering only. Shop/Courier prices follow the settled original-entry price rule.
- **Filters:** take the first eligible identity without another rarity roll. Retain entries skipped by combat-generation restrictions at their original positions for later eligible pulls. Preserve vanilla batch uniqueness; discard duplicate occurrences within shop batches permanently, while retaining skipped occurrences in other sources. Discarding an occurrence does not ban that identity from future entries.
- **Known issue — deferred:** retained skipping is used for relic shop eligibility, chest exclusion of character-specific relics, potion combat-generation eligibility and non-shop batch uniqueness. These uses share the earlier filtered-pull bias; shop batch duplicate removal is the exception.

### Drop occurrence

- Retain Catfood addressing and vanilla's shared changing probability across normal fights, elites and bosses. Extra ordinary checks advance the cumulative index; combat-category threshold differences are preserved. No act/Ancient reset.
- **White Beast Statue:** forced drops freeze both the occurrence index and stored probability; seed no new occurrence RNG and consume no occurrence random number. After removal, resume at the next unused ordinary-check index. Matching pre-Statue states therefore remain aligned despite different numbers of forced drops.
- Acquisition without a drop check does not advance occurrence state. Save the occurrence index and probability alongside acquisition-sequence progress and retained skips.

## Remaining design: rewards and encounter

| Category | Determines | Primary progression |
|---|---|---|
| Rewards | What the player gets | Cumulative ordinals |
| Encounter | Room content, named event choices and combat conditions | Act-local ordinals |

Distinguish normal fights, elites, bosses, Ancients, chests, events and special relic effects only where their rules differ.

**Catfood addressing:** cumulative normal-fight/elite/boss/shop/event/Ancient counters. Ancients increment both `eventEntered` and `ancientVisited`; options stay independently seeded, while Ancient card overrides use `ancientVisited`. Room reward RNG uses room-type counts, a normal-fight fallback and event-card overrides. Shop card identities still use `Shops`; relic bags/rarities and potion counts remain shared.

**Settled:** Rewards - relics, chests and potions; Encounter - shops.

### Rewards

#### Cards: offers, rarity and rerolls

- **Catfood:** combat identities/upgrades use room type, count and offer position. Combat rerolls still update that room type's rarity state, potentially shifting later offers.
- **Code-confirmed Catfood bug:** ordinary card rewards share initial and reroll options, so both consume one reroll-seeded identity/upgrade stream. Gameplay impact not reproduced in-game.
- **Catfood rarity:** separate normal-fight, elite, boss and shop offsets use vanilla base probabilities, growth and rare-reset value. Boss rewards reset only the boss offset; Ancient entry resets all four to `-0.05`, including Neow. Shop rolls do not grow their isolated offset, leaving their Rare roll chance at 4% (0% with Scarcity).
- **Elite initialization — proposed:** on Ancient entry, including Neow, set the elite offset to `0.01`/`-0.02` (without Scarcity/with Scarcity) when the other offsets reset, approximating two normal fights. Retain vanilla non-rare growth.
- **Offset resets — settled:** preserve combat rarity resets to `-0.05` after Rare rolls and resets on Ancient entry.
- **Act alignment — open:** find other ways to prevent divergence while preserving offset resets.
- **Catfood direct/event generation:** seeds add room/event count to a per-room options-call index. Seed collisions exist; harmful gameplay impact remains unverified.

**Unadopted suggestion:** stable direct-generation scopes/indices, if a reproduced issue warrants them.

**Reroll example** (Scarcity): three non-rares raise offset `0` → `0.015`. Next matching normal fight, draw `0.025` gives Uncommon versus Rare (about 1.5% versus 3%). A rare reroll instead resets the offset. Assess the impact against the 歪种 definition.

#### Other gains and changes

**Open:** gold, transformations and permanent upgrades/downgrades; define equality and sharing by source/effect.

### Encounter

#### Shops, chests and events

- **Shops:** settled above.
- **Relic Trader — deferred:** named choices belong to Encounter. Decide which owned relics and replacements are offered, their source, and when offered/chosen/unchosen replacements are consumed. Existing rules are in the [appendix](#appendix-existing-mechanics).
- **Chest policy — settled:** relics use the generic allocation with the [filtered-pull rule](#source-classification-and-consumption), excluding character-specific relics; base gold uses run seed + cumulative non-empty chest ordinal to achieve separation from other room types. Silver Crucible's empty first chest advances neither chest ordinal nor relic sequences; the next chest is ordinal 1. Declining a generated relic does not undo consumption or the chest ordinal.
- **Catfood chests:** inherits Silver Crucible's suppression, but has no chest ordinal. Gold still uses `Rewards`, re-seeded with the normal-fight count on chest entry. Relics use shared rarity rolls/bags.
- **Event-exclusive relics:** part of event encounters, already outside ordinary pools; no manual separation needed. Keep event identities/sequences route-independent and preserve choices/costs (Doll Room, Forgotten Soul / Sword of Stone, Trash Heap, Fake Merchant).

#### Selection and setup

- **Catfood:** encounter ID + cumulative room-type ordinal controls variable composition/setup, not ordinary HP. No visible setup mismatch reproduced.
- **Proposed:** act-local setup ordinals, distinguishing source/effect where necessary.
- **Lantern Key proposal:** forced Act 3 War Historian Repy gets **ordinal 0**, leaving ordinary events untouched; Catfood counts it normally.
- **Unadopted suggestion:** separate forced-event selection from `?` room-type resolution.

#### Combat randomness

- **Catfood:** re-seeds shuffle RNG at combat start; later shuffles continue it.
- **Proposed:** act-local opening-shuffle ordinals. First Act 2 normal fight then aligns setup/shuffle addresses despite different Act 1 normal-fight counts; cumulative reward addresses still differ.
- **Open:** HP/AI, random targets/costs/orbs, shuffles, temporary upgrades/downgrades and generated cards by pool/type; decide sharing and special-relic interactions.

## Next steps and validation

**Next:** Address remaining Encounter design, including Relic Trader, other events/Ancients and combat setup.

**Validation suggestion:** targeted RNG logs and paired runs; histories alone miss internal calls/actions. Use consistent seed derivation.

**Separate improvement:** add run seed to boss shuffle initialization.

**Pending:** verify save/load preserves counters and outcomes.

## Reference

[Event spreadsheet](references/StS2_Events.xlsx): **57 ordinary/forced events and 8 Ancients**. `is_deterministic` concerns offered choices, not hidden rewards: Punch-Off is true; Wongos is false. Detailed criteria and state restrictions are in the workbook.

## Appendix: existing mechanics

**Identity depletion:** from the requested rarity, search forward along Shop → Common → Uncommon → Rare, then multiplayer fallback; otherwise Circlet. Refresh-enabled bags first refill empty requested rarities. Catfood inherits this shared-bag fallback.

The following are game rules, unrelated to our randomness-control design:

**Ordinary relic eligibility:** the following 17 relics require `TotalFloor < 41` in single-player or `< 38` in multiplayer (`IsBeforeAct3TreasureChest`): Amethyst Aubergine, Book of Five Rings, Bowler Hat, Dragon Fruit, Frozen Egg, Girya, Juzu Bracelet, Lasting Candy, Lucky Fysh, Meal Ticket, Molten Egg, Old Coin, Planisphere, Shovel, Toxic Egg, White Beast Statue and White Star. The cutoff uses total floor, not whether a chest was visited. (Off-scope note: Lasting Candy is also disallowed if any Ironclad player has `UnlockState.NumberOfRuns == 0`.)

| Source | Rules |
|---|---|
| Chests | Draw from `SharedRelicGrabBag`, populated from `SharedRelicPool`; character-specific relics are excluded even in single-player. Relics are generated on room entry; base gold is generated on opening. |
| Wongos | Act 2; all players have ≥100 gold. Featured Rare: 200 gold, consumed when offered even if unaffordable/declined. Hidden Common: 100 gold, generated on purchase. Both require `IsAllowedInShops`; preserve that filter. Mystery Ticket pays three random relics after five combats. Leaving downgrades a random upgraded card, if any. |
| Relic Trader | Acts 2–3; all players own ≥5 tradable relics. Shows three randomly selected owned relics and three replacements; consumes all three replacement identities/rarities before selection. Normally no leave. Replacement rarity is independent and replacements need not be tradable. Tradable excludes Starter/Event/Ancient, upon-pickup, used-up, melted and pet-spawning relics. |
| Courier | 20% discount; removal never restocks. Restocked character cards retain type, colorless cards retain rarity; sale clears. Relics roll ordinary rarity even in the Shop slot. Shop ordinal does not advance. |
| Silver Crucible | Neow relic; first treasure room is empty. Suppresses both base gold and relic generation. |
| Fixed potion grants | Drowning Beacon gives Glowwater Potion; Potion Courier offers three Foul Potions. Both identities are Event-rarity and outside ordinary random pools. |
| Fixed-rarity random potions | Potion Courier's Ransack gives Uncommon; Crystal Sphere specifies the rarity and selects identity with event RNG. The Courier's shop restocks do not guarantee Uncommon. |
| Uniform-identity event potions | Battleworn Dummy, Endless Conveyor, The Legends Were True and Wellspring select uniformly from eligible potion identities. |
| Potion factory batches | Initial shop stock: 3; Alchemical Coffer on acquisition: 4; Phial Holster on acquisition: 2. These are the three multi-potion factory callers; each batch excludes duplicate identities. |
| Combat potion generation | Alchemize excludes Fairy in a Bottle, Fruit Juice and Regen Potion. Entropic Brew fills empty slots using unrestricted generation, even during combat. Delicate Frond fills empty slots before combat using unrestricted generation. |
| Potion acquisition failures | Full slots or Sozu can reject acquisition after generation. Alchemize attempts generation even with full slots. Entropic Brew and Delicate Frond generate only while slots are open and stop on the first failed acquisition. Phial Holster adds one slot but generates two potions, so its second grant can fail. |
| White Beast Statue | Forces combat potion rewards and is normally tradable at Relic Trader. Vanilla freezes the stored drop probability while owned; removal resumes it without a reset. The final boss generates no ordinary reward set and performs no drop check. |
