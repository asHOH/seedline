# Deterministic seed mod — design

Unfinished design questions and investigation notes are in the [working document](WORKING.md).

## Goals

**Goal:** fair same-seed competition at equivalent ordinal opportunities. Corresponding shops should match regardless of route, with explicit exceptions below; purchases/restocks should not change later shops. Preserve meaningful choices while removing interference between unrelated systems.

**Fairness principle:** limit the difference one room creates compared with a run without that room. Even consuming the last sequence entry can matter greatly if it contains an exceptionally powerful relic.

## Scope

Version: StS2 v0.111.0 and Catfood Mod v1.1.2.

Compare runs with matching seeds, versions, rules and eligible pools. This context will not be restated hereafter.

Single-player is the intended scope. Multiplayer behavior is excluded, except for easy changes that materially improve fairness.

Tutorial overrides, including scripted rewards and boss discovery order, and custom-run modifiers are excluded.

**Discussion boundary:** compare random outcomes before predictable modifiers such as eggs, Fresnel Lens and fixed discounts. Preserve these effects and exclude them from all later randomness-control discussions; effects on random selection or sequence consumption remain in scope.

**Deterministic identities:** predetermined card, relic and potion grants or replacements are excluded: their identities require no random selection and consume no controlled reward sequences. Fixed-rarity requests remain in scope when the identity is random.

**Exhaustion boundary:** finite relic allocations may borrow on exhaustion; depletion of an entire rarity is excluded. Rare tail effects and interactions between sequences through borrowing are outside ordinary isolation discussions throughout.

**Design status:** settled sections are fixed contracts, excluded from unfinished-design discussions.

Base findings on code; use run histories as supporting evidence.

## Design categories

The design has two top-level categories:

- **Rewards:** generated reward identities and amounts, including permanent transformation results.
- **Encounter:** random selection of affected cards/items, room content, named event options and combat randomness.

These categories classify random decisions, not whole effects; player choices and fixed selections belong to neither.

## Terms

### Game context

- **Scarcity:** the Ascension 7 modifier, active at A7 and above. It reduces base Rare card chances, rarity-offset growth and card-upgrade odds; "without/with Scarcity" means A0–6/A7+.
- **Map type** (`MapPointType`): the map marker; `Unknown` (`?`) may resolve into different room types.
- **Room type** (`RoomType`): the resolved room—normal fight, elite, boss, chest, event, shop or rest site. Ancients are an Event room subtype, counted separately in this design. An event may start combat without changing the map type.
- **Event ID / encounter ID:** identifies a specific event / combat encounter, not its room type.
- **Normal fight / combat:** **normal fight** means `RoomType.Monster`; **combat** means any battle, including normal fights, elites, bosses and event battles. Avoid bare **fight**. **Monster** in prose means an individual enemy.

### Rewards

- **Card reward offer:** cards presented together for one choice, usually three. A **card slot** is one position within an offer.
- **Direct card addition:** a card added without an identity choice or an opportunity to skip after seeing it; choosing the granting event option beforehand does not make it a card reward offer.
- **Item identity:** the card, relic or potion. A **sequence entry** is one occurrence identified by its original source position; repeats are distinct entries. Advancement changes its logical position, not its original source address; removals never renumber these addresses.
- **Filter / filter marker:** a filter defines eligibility for a draw. A **filter marker** occupies a sequence position and records one deferred advancement under that filter; shortened to **marker** below.
- **Ordinary relics:** Common / Uncommon / Rare / Shop. Event-exclusive relics sit outside these pools; scripting alone does not imply exclusivity.
- **Generation / acquisition:** generation selects an item; acquisition means actually receiving it. The potion acquisition policy also governs displayed offers, which consume on generation.
- **Reward sources:** **Courier** means restocks from The Courier relic, distinct from the Potion Courier event. **Generic rewards** groups combat rewards, events and other grants as specified per item type; relics and potions use separate sequences.

### Determinism

- **Pool / allocation / sequence / RNG:** a pool defines candidate identities; an allocation is the finite relic stock assigned to a source, ordered into one or more identity sequences. A sequence defines draw order; a random number generator (RNG) generates random choices.
- **Ordinal:** an opportunity's position within its category. Act-local ordinals reset each act; other ordinals continue across acts ("cumulative" is omitted unless needed for clarity).
- Act-local RNG seeds include the act number so resetting ordinals never restarts a previous act's RNG sequence.
- **Re-seeding** restarts a repeatable RNG sequence. Outcomes still depend on pools, rarity offsets, counters and other generators.
- **Event RNG:** an event's own generator, addressed by run seed + event ID in single-player, independently of event ordinals. Decisions using it may share draws within that event; unrelated systems do not advance it. This does not imply that every random decision in the event uses it.
- **Same-seed divergence (歪种):** large early- or mid-run outcome differences that undermine fair same-seed comparison. Late-game differences from different total room-type/subtype ordinals are benign.

## Vanilla baseline

Vanilla provides the baseline Catfood improves on; exact probabilities and draw rules are in [generation and selection mechanics](#generation-and-selection-mechanics).

| Mechanism | Baseline and interference |
|---|---|
| Unknown room types | `?` resolution uses dedicated `UnknownMapPoint` RNG and changing odds. Odds reset between acts; RNG progress continues. Route exclusions and forced outcomes affect resolution. Catfood leaves this unchanged. |
| Event / encounter identities | Per-act lists are generated upfront with `UpFront` RNG. Events advance past ineligible or previously visited identities; normal fights reached through `?` nodes or visible normal-fight nodes share the same encounter list. Catfood leaves identity selection unchanged. |
| Shops and events | Shops use `Shops` RNG for identities/prices and shared `Rewards` for card rarity/upgrades and relic rarity. Events may consume shared rewards/bags despite having an event RNG. Courier shares these generators; visits/restocks can shift later outcomes and prices. |
| Combat | Encounter setup uses floor-based addressing. Ordinary monster HP uses `Niche` RNG. Shuffle RNG persists across combats, so extra reshuffles can change a later opening hand. |
| Combat gold | Uses shared `Rewards` RNG, uniformly over inclusive ranges: normal fights 10–20, elites 35–45. Poverty (A3+) multiplies each endpoint by 0.75 and truncates before rolling, giving 7–15 and 26–33. |
| Card rarity | Combat rewards update a shared rarity offset; shops read it without updating it. Rare combat reward rolls, including guaranteed boss Rares, reset it; direct additions and Ancient entry do not. Catfood resets on Ancient entry instead of boss rewards, which is effectively the same timing. |
| Ancient options | Use the event RNG. Vanilla seeds it with run seed + event identity hash + player-slot contribution, independently of event counts. |
| Chests | Relics use shared rarity RNG and identity bags; gold uses `Rewards`. Unlike elite rewards, chests exclude character-specific relics. Silver Crucible suppresses the first chest's rewards before generation. |
| Relic pools | Sources share finite identity bags, with different draw directions and eligibility filters. Depletion can change delivered rarity. |
| Potion drops | Checks use shared `Rewards` RNG and a shared changing probability, with no act/Ancient reset. Forced drops skip both the random draw and probability update. |
| Potion generation | Ordinary generation rolls rarity, then identity; some events use uniform-identity or fixed-rarity selection. Batches exclude duplicates. Failed acquisition does not restore consumed RNG state. |
| Transformations | Random replacements are uniform over eligible identities, excluding the original. Callers use event RNG, `Niche` or player `Transformations` RNG, so results depend on the source and its other consumption. |
| Direct card additions | Sources use shared reward RNG, `Niche` or event RNG. Infested Automaton and Endless Conveyor's Fried Eel roll base rarity odds before identity: Common/Uncommon/Rare 60%/37%/3%, or 61.51%/37%/1.49% with Scarcity，independent of combat rarity offsets. Unavailable rarities advance Common → Uncommon → Rare → Common. Arcane Scroll and random curse grants select uniformly from their eligible pools. |

## Settled policy: filtered draw strategy

**Intent:** return an eligible entry while preserving the sequence's distribution. Advance the eligible subsequence within its existing positions, so skipped entries do not accumulate at the front of later draws.

**Principle:** generalize fixed-rarity draws, which consume an identity while preserving the rarity rolls. Viewed as a mixed sequence, later entries of that rarity fill earlier positions of the same rarity. For any filter, if eligible entries `A`, `B`, `C`, … occupy positions 2, 6, 8, …, return `A`, advance `B` to 2, `C` to 6, and so on. Ineligible positions remain unchanged, preserving the sequence's shape.

**Method — deferred advancement:** a filtered draw returns the first eligible entry and replaces it with a marker for its filter. To resolve a marker, move the next eligible entry from the following sequence into the marker's position, leaving a marker for the same filter at the supplying position. An ordinary draw resolves any marker at the front, then returns the entry and removes the drawn position. This performs the same advancement on demand, for infinite sequences as well as finite relic allocations.

**Marker collisions:** when a draw using filter `Y` encounters a marker for filter `X`, resolve that marker first.

- If the resolved entry fails `Y`, retain it at the marker's former position and continue searching for `Y`.
- If it satisfies `Y`, return it and place a marker for `Y` there. The marker for `X` remains at the supplying position, representing an advancement of `X` followed by an advancement of `Y`.

Apply the same rule recursively to further collisions, including identical filters: two advancements require two markers.

Markers freeze eligibility conditions at the original draw, including batch exclusions and state-dependent restrictions; the current draw still enforces current eligibility.

Apply the filtered draw strategy to all covered relic, potion, transformation and direct-addition identity sequences, including shop-batch duplicate filtering, without additional rarity rolls. Preserve existing source eligibility and batch uniqueness; skipped entries remain available to eligible draws and borrowing. The main cost is understanding and implementing marker resolution.

**Sequence implementation:** covered potion/card identity sequences and rarity-roll sequences are generated on demand and continue indefinitely, using independent RNGs for the source and pool divisions specified below. Potion/card identities may recur; only finite relic allocations borrow. Save sequence contents and progress, including original entry addresses, consumption, marker positions and conditions, borrowing transfers, and RNG progress.

## Settled policy: shops, Wongos and Courier

**Catfood baseline:** tracks shop ordinal and isolates shop card-rarity state, while retaining `Shops` RNG for card identities and prices, shared relic bags/rarity rolls, and a shared potion-factory call index.

**Our improvements:**

- Extra rewards and Courier restocks no longer scramble later shops.
- Corresponding shops keep the same sale slot, and an offer keeps its base price if it moves to a later slot or shop.
- Wongos' featured relic advances only the shop's Rare relic offers—and leaves them untouched when you cannot afford it.

### Stock and prices

- Shop stock depends only on shop ordinal, except for Wongos and relic floor cutoffs.
- **Initial cards:** generate rarity and identity from run seed + shop ordinal, independently of other systems. Preserve slot order, duplicate exclusion and unavailable-rarity fallback. Relic-rarity, relic-identity and potion sequences remain independent.
- **Card rarity:** vanilla shop base chances plus invariant offsets `+0.008` / `0` (without/with Scarcity), chosen approximations informed by repeating `3 normal → 1 elite → 2 normal → 1 elite` and adjusted downward to account for boss resets.
- Sale slot uses run seed + shop ordinal, independently of card generation and other RNG calls.
- Base price is addressed by run seed + source sequence + **original entry position**. Initial shop cards use shop ordinal + slot as their entry address. It survives slot/shop shifts; repeated identities have distinct addresses. No advance generation is needed.
- **Courier:** five independent card sequences—Attack, Skill, Power, Uncommon colorless and Rare colorless—plus separate relic-rarity/identity and potion sequences. Character cards use the shop rarity odds above; colorless cards retain their fixed rarity. Apply filtered draws to exclude cards still displayed. Advance neither shop nor generic rewards sequences, nor shop ordinal. Apply the same price rule and preserve [original mechanics](#generation-and-selection-mechanics).
- **Wongos:** featured offer consumes **one shop Rare identity**, no rarity entry; below 200 gold, the featured offer is not shown and consumes nothing. Later Rare entries shift, e.g. `[A, B]` → `[B, C]`, limiting the change to one removed offer plus its replacement. Other options use [generic relic rewards](#source-classification-and-consumption).
- **Relic floor cutoffs (see [appendix](#generation-and-selection-mechanics)):** reaching corresponding shops on opposite sides of a cutoff can change their relics and shift later stock; these differences are inevitable and benign.

## Settled policy: relics

**Catfood baseline:** draws identities from shared relic bags and rarities from 400 precomputed rolls; roll 401 would fail (code finding, not reproduced). Fixed-rarity requests consume identity only; identity depletion uses the [appendix fallback](#generation-and-selection-mechanics).

**Our improvements:**

- Relics offered through shops, Courier and generic rewards no longer compete for the same stock during normal play.
- Remove Catfood's fixed limit on random relic-rarity draws.

### Allocation

Construct three disjoint relic allocations once: generic rewards, shop and Courier. Each contains independently advancing, shuffled Common/Uncommon/Rare identity sequences. Split each Common/Uncommon/Rare pool **45% / 35% / 20%** between generic rewards / shop / Courier. Shops own the shop allocation and a separate shuffled Shop-rarity sequence containing all Shop-rarity identities.

Fully unlocked: **122 ordinary relics per character**—26 Common, 32 Uncommon, 38 Rare, 26 Shop (115 shared + 7 character-specific). Capacity is per rarity/source.

Exact fully unlocked allocations (generic rewards / shop / Courier): **Common 12 / 9 / 5; Uncommon 14 / 11 / 7; Rare 17 / 13 / 8**.

Mix shop-ineligible relics into allocations normally.

### Source classification and consumption

Hidden random relic rewards from combat/chests/events share the generic rewards allocation, with source-specific eligibility, regardless of cost (including Ranwid, Wongos' Common and Mystery Box). Random rarity advances rarity + identity; fixed rarity advances only identity. Classify deferred payoffs separately from their granting relic.

**Consumption order:** preserve existing generation order and pull sequentially; deferred rewards consume entries when their payout is generated (e.g. elite relic before Mystery Ticket's three relics).

**Filtered draws:** use the [filtered draw strategy](#settled-policy-filtered-draw-strategy).

### Borrowing

On exhaustion, borrow the **last eligible remaining identity** of the requested rarity. Other sequences borrow from Courier first; Courier borrows from generic rewards first.

Marker resolution follows the same borrowing and marker placement rules as a filtered draw.

**Final fallback — settled:** once same-rarity borrowing is exhausted, follow [vanilla fallback](#generation-and-selection-mechanics), or a simpler implementation that keeps the game running. This is outside the randomness-control discussion.

**Borrowed entries:** retain their original source sequence and entry position as the price/save address. Apply the receiving source's price rules.

## Settled policy: potions

**Catfood baseline:** addresses drop checks by `player seed` and `cumulative check index`, starting at index 0, while preserving vanilla's changing probability. Forced drops also advance this index. Separately, potion factories use a shared call index to seed each batch.

**Our improvements:**

- Potion rewards, shop stock, Courier restocks and potion-generating effects no longer scramble one another's potion results.
- Receiving several potions together no longer starts a different random sequence from receiving them separately.
- A failed direct potion grant no longer changes your later potion results.
- White Beast Statue pauses ordinary drop checks, so extra forced drops do not disturb the checks that resume after it is removed.

### Acquisition and sequences

- Keep independent **generic rewards, shop, Courier and combat-generation** sequences. Generic rewards combines random combat rewards, events and other grants without paid/free or per-event separation. **Combat-generation** names a source category, not a combat-only restriction: Alchemize, Entropic Brew (also outside combat) and Delicate Frond.
- Each source uses the full eligible potion pool with independent Common/Uncommon/Rare identity sequences.
- Within the generic rewards source, use separate rarity-roll sequences for **[standard weights](#generation-and-selection-mechanics)** and **uniform-identity selection**. Uniform mode weights each rarity by its share of eligible identities. Both modes consume the same per-rarity identity sequences. Fixed-rarity requests consume identity only. Other sequences only need standard weights rarity roll.
- **Consumption:** displayed offers consume on generation, even if declined or unclaimable; direct inventory grants consume only on successful acquisition. Failed direct grants leave rarity/identity RNG and sequence state unchanged; preserve existing failure feedback, including Alchemize's failure animation. Advance per potion, not per factory call; batch boundaries affect duplicate filtering only. Shop/Courier prices follow the settled original-entry price rule.
- **Filters:** use the [filtered draw strategy](#settled-policy-filtered-draw-strategy).

**Batch example:** Catfood's call-based seeding can turn `A | BCD | E` for requests `1–3–1` into `AFG | B` for `3–1`. Per-potion consumption instead follows `ABC | D`, subject to batch duplicate filtering.

### Drop occurrence

- Retain Catfood addressing and vanilla's shared changing probability across normal fights, elites and bosses. Extra ordinary checks advance the cumulative index; combat-category threshold differences are preserved. No act/Ancient reset.
- **White Beast Statue:** forced drops freeze both the occurrence index and stored probability; seed no new occurrence RNG and consume no occurrence random number. After removal, resume at the next unused ordinary-check index. Matching pre-Statue states therefore remain aligned despite different numbers of forced drops.
- Acquisition without a drop check does not advance occurrence state. Save the occurrence index and probability.

## Settled policy: chests

**Catfood baseline:** inherits Silver Crucible's suppression, but has no chest ordinal. Gold uses `Rewards`, re-seeded with the normal-fight count on chest entry. Relics use shared rarity rolls/bags.

**Our improvements:**

- Chest gold no longer depends on how many normal fights you took.
- Silver Crucible's empty chest does not advance later chest gold or relic rewards.

### Rewards and consumption

- Relics use the generic rewards allocation with the [filtered draw strategy](#settled-policy-filtered-draw-strategy).
- Base gold uses run seed + cumulative non-empty chest ordinal to separate it from other room types.
- Silver Crucible's empty first chest advances neither chest ordinal nor relic sequences; the next chest is ordinal 1. Declining a generated relic does not undo consumption or the chest ordinal.

## Settled policy: combat gold

**Catfood baseline:** preserves vanilla gold ranges and rounding. On room entry, re-seeds shared `Rewards` RNG by run seed and cumulative combat-category count, with different seed multipliers for normal fights and elites. Gold has no dedicated RNG.

**Our improvements:**

- Taking the same number of normal fights and elites in a different order gives the same total base gold.
- Preserve vanilla expected gold while isolating combat gold from other rewards.

### Shared sequence and compensation

Address a dedicated gold sequence by run seed + cumulative combined normal-fight/elite ordinal. Event gold belongs to Encounter and does not advance reward sequences.

| Difficulty | Shared uniform integer `X` | Normal-fight gold | Elite gold |
|---|---|---|---|
| A0–2 | 0–10 inclusive | `10 + X` | `35 + X` |
| A3+ | 0–7 inclusive | `7 + X + b` | `26 + X` |

At A3+, choose a fair starting bit from the run seed, independently of the gold sequence. Alternate compensation `b` between 0 and 1, advancing **once per normal fight**, using its cumulative ordinal; elites never advance it. Both sequences continue across acts. Preserve their progress and the starting bit across save/load.

**Accepted tradeoff:** at A3+, normal gold keeps its mean of 11 but is no longer uniform, with 7 and 15 each having a lower probability of 1/16; 8 to 14 remain uniform. Despite this, vanilla expected total gold is preserved at every fixed normal-fight count.

## Settled policy: permanent transformations and direct card additions

**Catfood baseline:** retains caller-supplied transformation RNGs; card-factory additions use room/event and options-call addressing. Neither uses dedicated shared identity sequences by pool.

**Our improvements:** permanent transformations and covered direct additions share a consistent identity order for each pool while retaining their different rarity chances, unaffected by card reward offers or combat transformations.

### Selection and consumption

- Keep seven pool groups with cumulative sequences: the five characters, colorless and curse. The player's character and colorless each use separate Common/Uncommon/Rare identity sequences, shared by transformations and covered direct additions, plus independent transformation and addition rarity-roll sequences. Other-character pools and curses retain one identity sequence each. Isolate all of these from card reward offers and combat transformations.
- **Transformations:** use the original card's pool; Quest cards and Event/Ancient/Token-rarity cards use colorless. Preserve [vanilla eligibility](#generation-and-selection-mechanics). For split pools, interpret the transformation rarity roll using current eligible identity counts per rarity, excluding the original card.
- **Direct additions:** use the granting source's pool and restrictions. Interpret the addition rarity roll using vanilla base odds and unavailable-rarity fallback. Fixed-rarity requests consume identity only.
- Draw one eligible entry from the resulting rarity's sequence, or directly from an unsplit pool. Identity entries are uniform. Apply the [filtered draw strategy](#settled-policy-filtered-draw-strategy).
- Consume per generated card in existing generation order. Canceled selections and previews consume nothing.

Covered direct additions: Infested Automaton's Study and Touch Core, Endless Conveyor's Fried Eel, Neow's Arcane Scroll and Neow's Bones curse, and Distinguished Cape.

**Infested Automaton:** Study and Touch Core use its event RNG for rarity, preserving vanilla base odds and fallback without advancing the addition rarity sequence. Identities still use the shared character sequences and filtered draw strategy.

**Distinguished Cape:** draw twice from the curse sequence; the second pull skips the first identity.

## Settled policy: event-specific rewards

Trash Heap, Doll Room and event choices granting otherwise unobtainable rewards use the event RNG, independently of shared reward sequences and unrelated game randomness. Corresponding events preserve the same rewards, or the same generation procedure where choices affect the result. Preserve existing choices, costs and eligibility.

## Settled policy: unknown-room resolution

**Catfood baseline:** uses the [vanilla baseline above](#vanilla-baseline); cumulative room counters do not determine `?` room type.

**Our improvements:**

- Earlier acts' `?` visits no longer shift the next act's random draws.
- When the map temporarily blocks a `?` shop, its chance still builds for later visits.
- Changes to one room type's chance are less likely to disturb other room types' results.

### Addressing and room-type chances

- **Addressing:** room-type RNG uses run seed + act + act-local ordinal of visited `?` nodes, removing dependence on previous acts' `?` visit counts.
- **Shop chance:** when the map-adjacency policy excludes Shop, its stored chance still increases by 3 percentage points. Preserve existing Juzu Bracelet, Golden Compass and Lantern Key room-type restrictions.
- **Thresholds:** allowed room types' stored chances determine interval widths. The chest interval begins at 0, the shop interval ends at 1, and the normal-fight interval stays centered at `7/15` unless shifted just enough to avoid overlap. When the total exceeds 100%, pack chest → normal fight → shop from 0 and truncate at 1, reducing effective interval widths without capping stored chances. The shop interval shrinks first; in the extremely rare case that chest + normal-fight chances alone exceed 100%, the normal-fight interval is also truncated and shop gets no space. Unassigned space resolves to Event.

## Reference

[Event spreadsheet](references/StS2_Events.xlsx): **57 ordinary/forced events and 8 Ancients**. `is_deterministic` concerns offered choices, not hidden rewards: Punch-Off is true; Wongos is false. Detailed criteria and state restrictions are in the workbook.

## Appendix: existing mechanics

### Relic and event rules

| Event / option | Relic | Effect |
|---|---|---|
| Wongos | — | Act 2; all players have ≥100 gold. Featured Rare: 200 gold; hidden Common: 100 gold. Leaving downgrades a random upgraded card, if any. |
| Wongos | Mystery Ticket | Pays three random relics after five combats. |
| Relic Trader | — | Acts 2–3; all players own ≥5 tradable relics. Offers a choice among three owned relics and three replacements. Normally no leave. |
| — | Courier | 20% discount; removal never restocks; sale clears on restock. |
| Neow | Silver Crucible | First treasure room is empty, with neither gold nor relics. |
| — | Delicate Frond | Fills empty potion slots before combat. |
| — | White Beast Statue | Forces combat potion rewards and is normally tradable at Relic Trader. |
| Neow | Leafy Poultice | Transforms the first Basic Strike and Defend. |
| — | Pandora's Box | Transforms all removable Basic Strikes/Defends. |
| Darv | Astrolabe | Transforms three selected cards and upgrades the results. |
| Infested Automaton — Study / Touch Core | — | Directly adds one random own-character Power / zero-cost non-X card. |
| Endless Conveyor — Fried Eel | — | Directly adds one random colorless card. |
| Neow | Arcane Scroll | Directly adds one random own-character Rare card. |
| Neow | Neow's Bones | Grants two random eligible Neow relics and directly adds one random curse eligible for modifier generation. |
| Trash Heap | — | Dive In costs 8 HP and grants a random relic from its special list; Grab grants 100 gold and directly adds a random card from its special ten-card list. |
| Vakuu | Distinguished Cape | Directly adds two distinct random eligible curses. |

**Other potion-generation effects:** Alchemize generates one potion. Entropic Brew fills empty slots, including outside combat.

### Generation and selection mechanics

Vanilla mechanics unless explicitly attributed to Catfood.

**Unknown-room resolution:** base chances are 10% normal fight / 2% chest / 3% shop; Event takes the remainder (initially 85%). Elites are disabled under ordinary rules. Each roll consumes one `UnknownMapPoint` draw. The selected non-event type resets to its base chance; other allowed non-event types gain their base amount. Excluded types retain their stored odds. Exclusions apply before interpreting the draw, without renormalizing the remaining probabilities.

**Unknown-room overflow:** vanilla checks cumulative thresholds in normal fight → elite (if enabled) → chest → shop order. Totals above 100% are neither normalized nor capped in stored state; intervals beyond 1 are unreachable, so later types lose effective probability and Event has none. At 70% normal fight / 14% chest / 21% shop, effective chances are 70% / 14% / 16%.

**Event / encounter identity selection:** room type is resolved first. An Event advances through its act's pre-shuffled list to the next `IsAllowed` identity not in the run-wide visited-event set; skipped entries are passed over, not retained for the next eligible pull. A normal fight uses the next entry in the act's normal encounter list, regardless of whether its map marker was `?` or a normal-fight marker. Forced-event replacement occurs after ordinary event eligibility scanning.

**Card rarity:** shared offset starts at `-0.05` and resets there after a Rare combat reward roll, but not after direct additions or on Ancient entry. Normal/elite non-rare rolls add `0.01` (`0.005` with Scarcity), capped at `0.40`. Base Rare chances for normal/elite/shop rolls are 3%/10%/9% (Scarcity: 1.49%/5%/4.5%), plus the offset; base Uncommon chances are 37%/40%/37%. A negative Rare threshold also reduces the Uncommon chance: initial normal rewards with Scarcity have 0% Rare and 33.49% Uncommon. Boss rewards guarantee Rare and reset the shared offset. Shop rolls read the offset without updating it.

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
