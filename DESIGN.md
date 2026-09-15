# Deterministic seed mod — design

Coverage is partial: the [working document](WORKING.md) tracks unfinished systems, whose existing behavior remains a baseline pending design decisions.

## Goals

**Goal:** make same-seed runs offer comparable choices and combat challenges, even when players take different routes or make different decisions. Preserve meaningful consequences while keeping later opportunities fair. Compare equivalent ordinal opportunities—e.g., each run's first shop or third normal-fight reward, even if reached on different floors.

**Fairness principle:** keep the effects of a decision focused on what that decision concerns. Visiting an extra room or choosing a reward should change as little unrelated future gameplay as possible. Later opportunities may give players a chance to recover from earlier disadvantages.

## Scope

Version: StS2 v0.111.0 and Catfood Mod v1.1.2.

Compare runs with matching seeds, versions, rules and fully unlocked relic and card pools.

Single-player is the intended scope. Multiplayer behavior is excluded, except for easy changes that materially improve fairness.

Tutorial overrides, including scripted rewards and boss discovery order, and custom-run modifiers are excluded.

**Discussion boundary:** compare random outcomes before predictable modifiers such as eggs, Fresnel Lens and fixed discounts. Preserve these effects and exclude them from all later randomness-control discussions; effects on random selection or sequence consumption remain in scope.

**Deterministic identities:** predetermined card, relic and potion grants or replacements are excluded: their identities require no random selection and consume no controlled reward sequences. Fixed-rarity requests remain in scope when the identity is random.

**Exhaustion boundary:** finite relic allocations may borrow on exhaustion; depletion of an entire rarity is excluded. Rare tail effects and interactions between sequences through borrowing are outside ordinary isolation discussions throughout.

**Design status:** shared strategies and settled policies are fixed contracts, excluded from unfinished-design discussions.

Base findings on code; use run histories as supporting evidence.

## Design categories

The design has two top-level categories:

- **Rewards:** generated reward identities and amounts, including permanent transformation results and unspecified random non-event-exclusive rewards.
- **Encounter:** random selection of affected cards/items, room content, named event options and combat randomness.

These categories classify random decisions, not whole effects; player choices and fixed selections belong to neither.

Category and RNG ownership are separate: a reward can use event RNG.

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
- **Item identity:** the card, relic or potion. A **sequence entry** is one occurrence; repeated identities are distinct entries.
- **Filter / filter marker:** a filter defines eligibility for a draw. A **filter marker** occupies a sequence position and records one deferred advancement under that filter; shortened to **marker** below.
- **Ordinary relics:** Common / Uncommon / Rare / Shop. Event-exclusive relics sit outside these pools; scripting alone does not imply exclusivity.
- **Generation / acquisition:** generation selects an item; acquisition means actually receiving it.
- **Reward sources:** **Courier** means restocks from The Courier relic, distinct from the Potion Courier event. **Generic rewards** is defined per item type below; relics and potions use separate sequences.

### Determinism

- **Pool / allocation / sequence / RNG:** a pool defines candidate identities; an allocation is the finite relic stock assigned to a source, ordered into one or more identity sequences. A sequence is one ordered stream; **per-rarity identity sequences** provide one stream per rarity. A random number generator (RNG) generates random choices.
- **Ordinal:** an opportunity's position within its category. Act-local ordinals reset each act; other ordinals continue across acts ("cumulative" is omitted unless needed for clarity).
- **Ordinal-addressed generation:** generation determined solely by the run seed and the relevant category's ordinal; **shop-ordinal generation** names the shop case.
- Act-local RNG seeds include the act number so resetting ordinals never restarts a previous act's RNG sequence.
- **Re-seeding** restarts a repeatable RNG sequence. Outcomes still depend on pools, rarity offsets, counters and other generators.
- **Event RNG:** an event's own generator, addressed by run seed + event ID in single-player, independently of event ordinals. Decisions using it may share draws within that event; unrelated systems do not advance it. This does not imply that every random decision in the event uses it.
- **Same-seed divergence (歪种):** outcome differences caused by uncontrolled randomness that undermine fair same-seed comparison (often happens early- or mid-game). Differences caused by consuming different numbers of opportunities within shared sequences (often happens late-game) are benign.

## Vanilla baseline

Vanilla provides the baseline Catfood improves on; exact probabilities and draw rules are in [generation and selection mechanics](#generation-and-selection-mechanics).

| Mechanism | Baseline and interference |
|---|---|
| Unknown room types | Dedicated `UnknownMapPoint` RNG and changing odds make `?` resolution route-dependent. Odds reset between acts; RNG progress continues. Catfood leaves this unchanged. |
| Event / encounter identities | Per-act lists are generated upfront with `UpFront` RNG; event eligibility and room visits affect progression. Catfood leaves identity selection unchanged. |
| Shops and events | Shops use `Shops` RNG for identities/prices and shared `Rewards` for card rarity/upgrades and relic rarity. Events may consume shared rewards/bags despite having an event RNG. Courier shares these generators; visits/restocks can shift later outcomes and prices. |
| Combat | Encounter setup uses floor-based addressing. Ordinary monster HP uses `Niche` RNG. Shuffle RNG persists across combats, so extra reshuffles can change a later opening hand. |
| Combat gold | Uses shared `Rewards` RNG, uniformly over default inclusive ranges: normal fights 10–20, elites 35–45. Poverty (A3+) multiplies each endpoint by 0.75 and truncates before rolling, giving 7–15 and 26–33. Source-specific exceptions are in the appendix. |
| Card rarity | Combat rewards change a shared rarity offset also read by shops. Vanilla resets it on Rare combat rolls; Catfood replaces boss resets with Ancient-entry resets. |
| Ancient options | Use the event RNG. Vanilla seeds it with run seed + event identity hash + player-slot contribution, independently of event counts. |
| Chests | Relics use shared rarity RNG and identity bags; gold uses `Rewards`. Other rewards can shift chest contents. |
| Relic pools | Sources share finite identity bags, with different draw directions and eligibility filters. Depletion can change delivered rarity. |
| Potion drops | Checks share `Rewards` RNG and a changing probability across combats. |
| Potion generation | Selection rules vary by source. Generation precedes acquisition, so failed grants can still shift later results. |
| Transformations | Callers use event RNG, `Niche` or player `Transformations` RNG, so results depend on the source and its other consumption. |
| Direct card additions | Sources use shared reward RNG, `Niche` or event RNG, with source-specific selection rules independent of combat rarity offsets. |

## Shared strategies

### Rarity and identity sequences

**Main idea:** share rarity rolls and identity order across related rewards, with independent sharing scopes and progress.

**Sharing rationale:** generic rewards represent successive opportunities to receive the same kind of item, regardless of route. Shops are isolated to preserve corresponding inventories; Courier and potion-generating effects are isolated so repeated use cannot displace ordinary rewards. Transformations and direct additions both generate permanent cards from a pool, so they share identity order while separate rarity rolls preserve their different odds.

**Scope:** relics, potions, cards, permanent transformations and covered direct additions.

With per-rarity identity sequences, a random-rarity request consumes one value from its **rarity-roll sequence**, then draws an eligible item from that rarity's **identity sequence** using the [filtered draw strategy](#filtered-draws). Fixed-rarity requests consume identity only; pools with one identity sequence draw directly. Source policies specify sharing and RNG exceptions.

**Generation and lifetime:** covered potion/card identity sequences and rarity-roll sequences are generated on demand and continue indefinitely, using independent RNGs for the source and pool divisions specified below. Each new potion/card identity entry is an independent uniform draw with replacement from its sequence's pool; draw-specific eligibility and duplicate restrictions are handled by filtering. Only finite relic allocations borrow.

### Consumption timing

**Main idea:** advance a sequence when its corresponding opportunity occurs.

- **Displayed offers:** consume on generation, even if declined.
- **Direct potion grants:** consume only on successful acquisition. Failed grants leave rarity/identity RNG and sequence state unchanged.
- **Deferred rewards:** consume when the payoff is generated, not when its granting relic is acquired; e.g., the elite relic precedes Mystery Ticket's three relics.
- **Bypassed operations:** leave their ordinary sequence paused. White Beast Statue bypasses the drop check, but its potion still consumes a reward identity; Silver Crucible suppresses chest rewards before generation. Source policies specify the affected counters.

### Stable entry addresses

**Main idea:** an entry keeps its original address when it moves.

An entry's address is its original source sequence + original entry position. Filtered advancement and borrowing preserve this address; removals never renumber it. Prices and saved state refer to this stable address, so repeated item identities remain distinct occurrences.

### Filtered draws

**Main idea:** take the next eligible item without pulling skipped items toward the front of later draws.

**Advancement:** generalize fixed-rarity draws. Viewing per-identity sequences and rarity rolls together as a mixed sequence, later entries of that rarity fill earlier positions of the same rarity. For any filter, if eligible entries `A`, `B`, `C`, … occupy positions 2, 6, 8, …, return `A`, advance `B` to 2, `C` to 6, and so on. Ineligible positions remain unchanged, preserving the sequence's shape.

**Method — deferred advancement:** a filtered draw returns the first eligible entry and replaces it with a marker for its filter. To resolve a marker, move the next eligible entry from the following sequence into the marker's position, leaving a marker for the same filter at the supplying position. An ordinary draw resolves any marker at the front, then returns the entry and removes the drawn position. This performs the same advancement on demand, for infinite sequences as well as finite relic allocations.

**Marker collisions:** when a draw using filter `Y` encounters a marker for filter `X`, resolve that marker first.

- If the resolved entry fails `Y`, retain it at the marker's former position and continue searching for `Y`.
- If it satisfies `Y`, return it and place a marker for `Y` there. The marker for `X` remains at the supplying position, representing an advancement of `X` followed by an advancement of `Y`.

Apply the same rule recursively to further collisions, including identical filters: two advancements require two markers.

Markers freeze eligibility conditions at the original draw, including batch exclusions and state-dependent restrictions; the current draw still enforces current eligibility.

Apply the filtered draw strategy to all covered relic, potion, card, transformation and direct-addition identity sequences, including shop-batch duplicate filtering, without additional rarity rolls. Preserve existing source eligibility and batch uniqueness; skipped entries remain available to eligible draws and borrowing.

### Saved state

Save sequence contents and progress, including original entry addresses, consumption, marker positions and conditions, and RNG progress.

## Settled policy: shops, Wongos and Courier

**Catfood baseline:** tracks shop ordinal and isolates shop card-rarity state, while retaining `Shops` RNG for card identities and prices, shared relic bags/rarity rolls, and a shared potion-factory call index.

**Our improvements:**

- Extra rewards and Courier restocks no longer scramble later shops.
- Corresponding shops keep the same sale slot; relocated offers keep their base prices.
- Wongos affects only future Rare shop offers, and only when its featured relic is affordable.

### Stock and prices

- Shop stock depends only on shop ordinal, except for Wongos and relic floor cutoffs.
- **Initial cards:** use shop-ordinal generation for rarity and identity. Preserve slot order, duplicate exclusion and unavailable-rarity fallback.
- **Relics and potions:** initial stock and Courier use their respective sources under [relic allocation](#allocation) and [potion sequences](#acquisition-and-sequences).
- **Card rarity:** vanilla shop base chances plus invariant offsets `+0.008` / `0` (without/with Scarcity), chosen approximations informed by repeating `3 normal → 1 elite → 2 normal → 1 elite` and adjusted downward to account for boss resets.
- Sale slot uses independent shop-ordinal generation.
- **Base prices:** use run seed + [stable entry address](#stable-entry-addresses) for initial stock and Courier restocks. Initial shop cards use shop ordinal + slot as their entry address. No advance generation is needed.
- **Courier cards:** use dedicated sequences, independent of initial stock and other card sources. Attack, Skill and Power each have a rarity-roll sequence using the shop odds above and per-rarity identity sequences; Uncommon and Rare colorless each have one fixed-rarity identity sequence. As in vanilla, roll rarity once, apply unavailable-rarity fallback, then select an identity excluding cards still displayed. Filtered draws advance identities only. Preserve [original mechanics](#shops-chests-and-relic-events).
- **Wongos:** featured offer consumes **one shop Rare identity**; below 200 gold, the featured offer is not shown and consumes nothing. Later Rare entries shift, e.g. `[A, B]` → `[B, C]`, limiting the change to one removed offer plus its replacement. Other options use [generic relic rewards](#source-classification-and-consumption).
- **Relic floor cutoffs (see [appendix](#generation-and-selection-mechanics)):** reaching corresponding shops on opposite sides of a cutoff can change their relics and shift later stock; these differences are inevitable and benign.

## Settled policy: relics

**Catfood baseline:** retains shared relic bags but precomputes 400 rarity rolls; roll 401 would fail (code finding, not reproduced).

**Our improvements:**

- Shops, Courier and generic rewards no longer compete for relics during normal play.
- Random relic rewards no longer risk Catfood's fixed rarity-roll limit.

### Allocation

Construct three disjoint relic allocations once: generic rewards, shop and Courier. Each source has its own rarity-roll sequence and independently advancing, shuffled Common/Uncommon/Rare identity sequences. Split each Common/Uncommon/Rare pool **45% / 35% / 20%** between these sources respectively. The shop source also owns a shuffled Shop-rarity sequence containing all Shop-rarity identities.

Fully unlocked: **122 ordinary relics per character**—26 Common, 32 Uncommon, 38 Rare, 26 Shop (115 shared + 7 character-specific). Capacity is per rarity/source.

Exact fully unlocked allocations (generic rewards / shop / Courier): **Common 12 / 9 / 5; Uncommon 14 / 11 / 7; Rare 17 / 13 / 8**.

Mix shop-ineligible relics into allocations normally.

### Source classification and consumption

Hidden random relic rewards from combat/chests/events use the generic rewards source, with source-specific eligibility, regardless of cost (including Ranwid and Mystery Box). Classify deferred payoffs separately from their granting relic.

### Borrowing

On exhaustion, borrow the **last eligible remaining identity** of the requested rarity. Other sequences borrow from Courier first; Courier borrows from generic rewards first.

Marker resolution follows the same borrowing and marker placement rules as a filtered draw.

**Final fallback:** once same-rarity borrowing is exhausted, follow [vanilla fallback](#generation-and-selection-mechanics), or a simpler implementation that keeps the game running.

## Settled policy: potions

**Catfood baseline:** addresses drop checks by `player seed` and `cumulative check index`, starting at index 0, while preserving vanilla's changing probability. Forced drops also advance this index. Separately, potion factories use a shared call index to seed each batch.

**Our improvements:**

- Rewards, shops, Courier and potion-generating effects no longer scramble one another's potions.
- Receiving potions together or separately preserves comparable results.
- Failed direct grants no longer change later potions.
- White Beast Statue no longer shifts ordinary potion drops after its removal.

### Acquisition and sequences

- Keep independent sequences for four **sources: generic rewards, shop, Courier and potion-generating effects**. Generic rewards combines random combat rewards, events and other grants without paid/free or per-event separation. **Potion-generating effects** covers Alchemize, Entropic Brew and Delicate Frond, including use outside combat.
- Each source uses the full eligible potion pool with independent Common/Uncommon/Rare identity sequences.
- Within the generic rewards source, use separate rarity-roll sequences for **[standard weights](#generation-and-selection-mechanics)** and **uniform-identity selection**. Uniform mode weights each rarity by its share of eligible identities. Both modes consume the same per-rarity identity sequences. Other sources use one standard-weight rarity-roll sequence each.
- **Consumption:** follow the [shared timing rules](#consumption-timing); preserve existing failure feedback, including Alchemize's failure animation. Batch boundaries affect duplicate filtering only.

**Batch example:** Catfood's call-based seeding can turn `A | BCD | E` for requests `1–3–1` into `AFG | B` for `3–1`. Per-potion consumption instead follows `ABC | D`, subject to batch duplicate filtering.

### Drop occurrence

- Retain Catfood addressing and vanilla's shared changing probability across normal fights, elites and bosses. Extra ordinary checks advance the cumulative index; combat-category threshold differences are preserved. No act/Ancient reset.
- **White Beast Statue:** forced drops freeze the occurrence index and stored probability. After removal, resume at the next unused ordinary-check index.
- Acquisition without a drop check does not advance occurrence state. Save the occurrence index and probability.

## Settled policy: chests

**Catfood baseline:** has no chest ordinal; chest entry re-seeds `Rewards` with the normal-fight count for gold. Silver Crucible's suppression is unchanged.

**Our improvements:**

- Chest gold no longer depends on how many normal fights you took.
- Silver Crucible's empty chest no longer shifts later chest rewards.

### Rewards and consumption

- Base gold uses ordinal-addressed generation with the non-empty chest ordinal.
- Silver Crucible's empty first chest advances neither chest ordinal nor relic sequences; the next chest is ordinal 1. Declining a generated relic does not undo consumption or the chest ordinal.

## Settled policy: combat gold

**Main idea:** share random variation between unreduced normal-fight and elite gold draws so reordering them preserves total base gold. Keep vanilla expected gold and isolate these draws from other rewards.

**Catfood baseline:** re-seeds shared `Rewards` on room entry using run seed and cumulative combat-category count, with different multipliers for normal fights and elites. Vanilla gold ranges and rounding are unchanged.

### Shared sequence and compensation

**Shared roll:** generate uniform `u ∈ [0,1)` from run seed + combined normal-fight/elite gold-reward ordinal, including event battles. Set `X = floor(11u)` at A0–2 or `floor(8u)` at A3+.

Fixed or absent combat gold, fixed extra payouts and event-choice gold do not consume this sequence; see the appendix for exceptions.

| Difficulty | Shared uniform integer `X` | Normal-fight gold | Elite gold |
|---|---|---|---|
| A0–2 | 0–10 inclusive | `10 + X` | `35 + X` |
| A3+ | 0–7 inclusive | `7 + X + b` | `26 + X` |

**Compensation:** use the [Thue–Morse sequence](https://oeis.org/A010060): `b(n) = s XOR (popcount(n) mod 2)`, where `n` is the zero-based unreduced normal-fight gold-reward index, `popcount` counts binary 1-bits, and `s` is a fair seed-derived bit independent of `u`. Elites do not advance `n`. Both sequences continue across acts; save their progress and `s`.

**Tradeoff:** expected gold is unchanged, but A3+ normal gold is nonuniform: endpoints 7 and 15 each have probability 1/16; 8–14 each have 1/8.

**Gremlin Merc:** half-gold rewards use `5 + floor(6u)` at A0–2 or `4 + floor(5u)` at A3+, consuming the shared gold ordinal without applying or advancing compensation.

## Settled policy: permanent transformations and direct card additions

**Catfood baseline:** retains caller-supplied transformation RNGs; card-factory additions use room/event and options-call addressing. Neither uses dedicated shared identity sequences by pool.

**Our improvements:** transformations and covered direct additions give comparable same-pool results, preserving source rarity odds and avoiding interference from card reward offers or combat transformations.

Covered direct additions: Infested Automaton's Study and Touch Core, Endless Conveyor's Fried Eel, Neow's Arcane Scroll and Neow's Bones curse, and Distinguished Cape.

### Selection and consumption

- Keep sequences for seven pools: the five characters, colorless and curse. The player's character and colorless each use separate Common/Uncommon/Rare identity sequences, shared by transformations and covered direct additions, plus independent transformation and addition rarity-roll sequences. Other-character pools and curses retain one identity sequence each. Isolate all of these from card reward offers and combat transformations.
- **Transformations:** use the original card's pool; Quest cards and Event/Ancient/Token-rarity cards use colorless. Preserve [vanilla eligibility](#generation-and-selection-mechanics). For per-rarity identity sequences, interpret the transformation rarity roll using current eligible identity counts per rarity, excluding the original card.
- **Direct additions:** use the granting source's pool and restrictions. Interpret the addition rarity roll using vanilla base odds and unavailable-rarity fallback.
- Transformations and direct additions consume on generation; canceled selections and previews consume nothing.

**Infested Automaton:** Study and Touch Core use its event RNG for rarity, preserving vanilla base odds and fallback without advancing the addition rarity sequence. Identities still use the shared character sequences.

**Distinguished Cape:** draw twice from the curse sequence; the second pull skips the first identity.

## Settled policy: event-specific rewards

Trash Heap, Doll Room and event choices granting otherwise unobtainable rewards use the event RNG, independently of shared reward sequences and unrelated game randomness. Corresponding events preserve the same rewards, or the same generation procedure where choices affect the result. Preserve existing choices, costs and eligibility.

## Settled policy: unknown-room resolution

**Main idea:** align random draws within each act and keep each room type's probability interval as stationary as possible, limiting interference from earlier acts and other room types' changing chances.

**Catfood baseline:** uses the [vanilla baseline above](#vanilla-baseline); cumulative room counters do not determine `?` room type.

### Addressing and room-type chances

- **Addressing:** room-type RNG uses run seed + act + act-local ordinal of visited `?` nodes, removing dependence on previous acts' `?` visit counts.
- **Shop chance:** when the map-adjacency policy excludes Shop, its stored chance still increases by 3 percentage points. Preserve existing Juzu Bracelet, Golden Compass and Lantern Key room-type restrictions.
- **Thresholds:** allowed room types' stored chances determine interval widths. The chest interval begins at 0, the shop interval ends at 1, and the normal-fight interval stays centered at `7/15` (midpoint when chest/normal-fight/shop widths fill the range in their base `2:10:3` ratio) unless shifted just enough to avoid overlap. Unassigned space resolves to Event.
- **Overflow:** when the total exceeds 100%, pack chest → normal fight → shop from 0 and truncate at 1. The shop interval shrinks first; in the extremely rare case that chest + normal-fight chances alone exceed 100%, the normal-fight interval is truncated and shop gets no space.

## Reference

[Event spreadsheet](references/StS2_Events.xlsx): **57 ordinary/forced events and 8 Ancients**. `is_deterministic` concerns offered choices, not hidden rewards: Punch-Off is true; Wongos is false. Detailed criteria and state restrictions are in the workbook.

## Appendix: existing mechanics

This appendix records existing mechanics for comparison: vanilla by default, with Catfood behavior explicitly labeled. Proposed policies belong in the main design sections.

### Relic and event rules

#### Room resolution

| Source | Mechanics |
|---|---|
| `?` shop exclusion | Shop is excluded when the previous visited map point contained a shop, or every outgoing map node is a shop. |
| Juzu Bracelet / Golden Compass | Juzu excludes normal fights from `?` resolution. Golden Compass forces Event during its affected act. |
| Lantern Key | In Act 3, forces `?` to Event and replaces the selected event with War Historian Repy. The ordinary event-list cursor still advances. Like Golden Compass, forced resolution still consumes one `UnknownMapPoint` draw while non-event odds freeze. |

#### Shops, chests and relic events

| Source | Mechanics |
|---|---|
| Chests | Draw from `SharedRelicGrabBag`, populated from `SharedRelicPool`; character-specific relics are excluded even in single-player. Relics are generated on room entry; base gold is generated on opening. |
| Neow — Silver Crucible | First treasure room is empty, with neither gold nor relics. |
| Wongos | Act 2; all players have ≥100 gold. Featured Rare: 200 gold; hidden Common: 100 gold. Leaving downgrades a random upgraded card, if any. Featured Rare is consumed when offered even if unaffordable/declined; hidden Common is generated on purchase. Both require `IsAllowedInShops`. |
| Wongos — Mystery Ticket | Pays three random relics after five combats. |
| Relic Trader | Acts 2–3; all players own ≥5 tradable relics. Randomly selects three owned relics and three replacements to offer; consumes all three replacement identities/rarities before the player's selection. Normally no leave. Replacement rarity is independent and replacements need not be tradable. Tradable excludes Starter/Event/Ancient, upon-pickup, used-up, melted and pet-spawning relics. |
| Courier | 20% discount; removal never restocks; sale clears on restock. Restocked character cards retain type, colorless cards retain rarity. Relics roll ordinary rarity even in the Shop slot. Shop ordinal does not advance. Potion restocks do not guarantee Uncommon. |
| Trash Heap | Dive In costs 8 HP and grants a random relic from its special list; Grab grants 100 gold and directly adds a random card from its special ten-card list. |

#### Permanent transformations and direct additions

| Source | Mechanics |
|---|---|
| Neow — Leafy Poultice | Transforms the first Basic Strike and Defend. |
| Pandora's Box | Transforms all removable Basic Strikes/Defends. |
| Darv — Astrolabe | Transforms three selected cards and upgrades the results. |
| Infested Automaton — Study / Touch Core | Directly adds one random own-character Power / zero-cost non-X card. |
| Endless Conveyor — Fried Eel | Directly adds one random colorless card. |
| Neow — Arcane Scroll | Directly adds one random own-character Rare card. |
| Neow — Neow's Bones | Grants two random eligible Neow relics and directly adds one random curse eligible for modifier generation. |
| Vakuu — Distinguished Cape | Directly adds two distinct random eligible curses. |

#### Potions

| Source | Mechanics |
|---|---|
| White Beast Statue | Forces combat potion rewards and is normally tradable at Relic Trader. Freezes the stored drop probability while owned; removal resumes it without a reset. The final boss generates no ordinary reward set and performs no drop check. |
| Alchemize | Generates one potion, excluding Fairy in a Bottle, Fruit Juice and Regen Potion. Attempts generation even with full slots. |
| Entropic Brew | Fills empty slots with unrestricted potion generation, including outside combat. Generates only while slots are open and stops on the first failed acquisition. |
| Delicate Frond | Fills empty potion slots before combat. Uses unrestricted potion generation, generates only while slots are open and stops on the first failed acquisition. |
| Fixed-rarity random potions | Potion Courier's Ransack gives Uncommon; Crystal Sphere specifies the rarity and selects identity with event RNG. |
| Uniform-identity event potions | Battleworn Dummy, Endless Conveyor, The Legends Were True and Wellspring select uniformly from eligible potion identities. |
| Potion factory batches | Initial shop stock: 3; Alchemical Coffer on acquisition: 4; Phial Holster on acquisition: 2. These are the three multi-potion factory callers; each batch excludes duplicate identities. |
| Phial Holster | Adds one slot but generates two potions, so its second grant can fail. |
| Potion acquisition failures | Full slots or Sozu can reject acquisition after generation. |

### Generation and selection mechanics

**Unknown-room resolution:** base chances are 10% normal fight / 2% chest / 3% shop; Event takes the remainder (initially 85%). Elites are disabled under ordinary rules. Each roll consumes one `UnknownMapPoint` draw. The selected non-event type resets to its base chance; other allowed non-event types gain their base amount. Excluded types retain their stored odds. Exclusions apply before interpreting the draw, without renormalizing the remaining probabilities.

**Unknown-room overflow:** vanilla checks cumulative thresholds in normal fight → elite (if enabled) → chest → shop order. Totals above 100% are neither normalized nor capped in stored state; intervals beyond 1 are unreachable, so later types lose effective probability and Event has none. At 70% normal fight / 14% chest / 21% shop, effective chances are 70% / 14% / 16%.

**Event / encounter identity selection:** room type is resolved first. An Event advances through its act's pre-shuffled list to the next `IsAllowed` identity not in the run-wide visited-event set; skipped entries are passed over, not retained for the next eligible pull. A normal fight uses the next entry in the act's normal encounter list, regardless of whether its map marker was `?` or a normal-fight marker. Forced-event replacement occurs after ordinary event eligibility scanning.

**Card rarity:** shared offset starts at `-0.05` and resets there after a Rare combat reward roll, but not after direct additions or on Ancient entry. Normal/elite non-rare rolls add `0.01` (`0.005` with Scarcity), capped at `0.40`. Base Rare chances for normal/elite/shop rolls are 3%/10%/9% (Scarcity: 1.49%/5%/4.5%), plus the offset; base Uncommon chances are 37%/40%/37%. A negative Rare threshold also reduces the Uncommon chance: initial normal rewards with Scarcity have 0% Rare and 33.49% Uncommon. Boss rewards guarantee Rare and reset the shared offset. Shop rolls read the offset without updating it.

**Normal versus elite card rewards:** both offer three cards from the same character pool, exclude duplicates within the offer and use the same upgrade rules; elites have no inherent upgrade-chance bonus. Catfood preserves these rules.

**Combat-gold exceptions:** Fake Merchant grants a fixed 300 gold, unaffected by Poverty. Battleworn Dummy's three event combats grant no normal combat rewards. Gremlin Merc grants normal gold unless Fat Gremlin escapes: the gold proportion becomes one-half if no gold was stolen, or zero otherwise; vanilla rounds the reduced range endpoints before rolling and generates no gold reward at zero. Non-final bosses grant fixed 100 gold (75 with Poverty); the final boss grants no ordinary reward set. Fixed extra payouts, including recovered stolen gold, Royalties and Amethyst Aubergine, are separate from the normal combat-gold roll.

**Relic draws:** rarity is 50% Common / 33% Uncommon / 17% Rare; fixed-rarity requests consume identity only, including in Catfood. Shops pull two random-rarity relics and one Shop relic from the back of the bags; rewards/Wongos pull from the front. Run-state eligibility (`IsAllowed`) removes disallowed relics before draws; five relics are additionally shop-ineligible.

**Identity depletion:** from the requested rarity, search forward along Shop → Common → Uncommon → Rare, then multiplayer fallback; otherwise Circlet. Refresh-enabled bags first refill empty requested rarities. Catfood inherits this shared-bag fallback.

**Ordinary relic eligibility:** the following 17 relics require `TotalFloor < 41` in single-player or `< 38` in multiplayer (`IsBeforeAct3TreasureChest`): Amethyst Aubergine, Book of Five Rings, Bowler Hat, Dragon Fruit, Frozen Egg, Girya, Juzu Bracelet, Lasting Candy, Lucky Fysh, Meal Ticket, Molten Egg, Old Coin, Planisphere, Shovel, Toxic Egg, White Beast Statue and White Star. The cutoff uses total floor, not whether a chest was visited.

**Potion drops:** one shared probability starts at 40%; a generated drop subtracts 10 percentage points, a miss adds 10. Elites add 12.5 percentage points to the check. No act/Ancient reset. Checks use shared `Rewards` RNG; forced drops skip both the random draw and probability update.

**Potion generation:** ordinary generation rolls 65% Common / 25% Uncommon / 10% Rare, then identity. Factory batches exclude duplicate identities within the batch; separate calls can repeat them. Generation precedes acquisition; failed acquisition does not restore consumed RNG state.

**Direct card additions:** Infested Automaton and Endless Conveyor's Fried Eel roll base rarity odds before identity: Common/Uncommon/Rare 60%/37%/3%, or 61.51%/37%/1.49% with Scarcity, independent of combat rarity offsets. Unavailable rarities advance Common → Uncommon → Rare → Common. Arcane Scroll and random curse grants select uniformly from their eligible pools.

**Permanent transformation's target:** use the original card's pool, except Quest cards and Event/Ancient/Token-rarity cards use colorless. Token is the generated-card rarity used by Shiv, Soul and similar cards. Ordinary results are Common/Uncommon/Rare; curses share one curse pool. Preserve unlock/player restrictions and exclude the original identity. Selection is uniform over eligible identities, with no batch uniqueness. Combat transformations additionally filter by `CanBeGeneratedInCombat` and may use the Status pool.
