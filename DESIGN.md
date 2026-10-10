# Deterministic seed mod — design

This file records settled decisions, rationale and existing-mechanics references; it forms the specification as the design is completed. [WORKING.md](WORKING.md) holds unresolved choices and supporting evidence. Systems not yet specified retain existing behavior.

**Reading convention:** in the main body, unlabeled design statements are normative. Labeled baselines and references describe existing mechanics; rationale, tradeoffs, examples and rejected alternatives are explanatory. The Reference and Appendix sections are descriptive only.

## Goals and principles

**Goal:** make same-seed runs offer comparable choices and combat challenges even when players take different routes or make different decisions. Compare corresponding ordinal opportunities—for example, each run's first shop or third normal-fight reward—even when reached on different floors.

**Fairness principle — opportunity isolation:** preserve meaningful consequences while changing as little unrelated future gameplay as possible. For example, visiting an extra room or choosing a reward should not shift unrelated later randomness.

**Behavior preservation:** all behavioral changes from vanilla, including probability changes, must be deliberate alignment tradeoffs or potential bug fixes. Currently, only the [forced-event policy](#settled-policy-forced-events)'s bypass of ordinary event-list advancement is classified as a potential bug fix.

**Same-seed divergence (歪种):** outcome differences caused by uncontrolled randomness that undermine fair same-seed comparison, often appearing early or mid-game. Differences caused only by consuming different numbers of opportunities within intentionally shared sequences are benign, even when they produce late-game divergence.

**Batch-order neutrality:** selecting the same batch in a different click or enumeration order must not change which identities or statuses are affected or subsequent RNG state, unless order dependence is explicitly intended by the effect. This does not change presentation order or erase meaningful sequential gameplay effects.

## Scope

Version: StS2 v0.111.0 and Catfood Mod v1.1.2.

Compare single-player runs with matching seeds, versions and rules, with all content unlocked (including relic, card and event pools).

**Exclude:** multiplayer behavior, tutorial overrides (including scripted rewards and boss discovery order), and custom-run modifiers.

**Default context:** assume Scarcity (Ascension 7 and above) unless stated otherwise. Policies also apply without Scarcity using the corresponding probabilities. The difficulty-specific exception is [own-character random-rarity offers](#own-character-random-rarity-offers), whose behavior without Scarcity remains unresolved; this does not defer shops, relics, potions or other settled policies.

**Discussion boundary:** compare random outcomes before predictable modifiers such as eggs, Fresnel Lens and fixed discounts. Preserve ordinary probability and amount adjustments without repeating them unless their treatment changes. Specify interactions when they affect candidate eligibility, which random decisions occur, addressing, opportunity consumption or persistent RNG state.

**Fixed identities:** predetermined card, relic and potion grants or replacements are outside randomness-control scope: they require no identity draw and consume no controlled reward sequences. Fixed-rarity requests remain in scope when the identity is random.

## Concepts and terms

The design has two top-level categories:

- **Rewards:** generated reward identities and amounts, including permanent transformation results and otherwise-unspecified random rewards that are not event-exclusive.
- **Encounter:** random selection of affected cards/items, room content, named event options and combat randomness.

These categories classify random decisions, not whole effects; player choices and fixed selections belong to neither.

Category and RNG ownership are separate: a reward can use event RNG.

### Game context

- **Map type** (`MapPointType`): the map marker; `Unknown` (`?`) may resolve into different room types.
- **Room type** (`RoomType`): the resolved room—normal fight, elite, boss, chest, event, shop or rest site. Ancients are an Event room subtype counted separately in this design. An event may start combat without changing the map type.
- **Event ID / encounter ID:** identifies a specific event / combat encounter, not its room type.
- **Normal fight / combat:** **normal fight** means `RoomType.Monster`; **combat** means any battle, including normal fights, elites, bosses and event battles. Avoid bare **fight**. **Monster** in prose means an individual enemy.

### Reward terminology

- **Card reward offer:** cards presented together for one choice, usually three. A **card slot** is one position within an offer.
- **Card pool:** can be own character, colorless or a specified other character.
- **Direct card addition:** a card added without an identity choice or an opportunity to skip after seeing it; choosing the granting event option beforehand does not make it a card reward offer.
- **Ordinary relics:** Common / Uncommon / Rare / Shop; Event-exclusive relics sit outside these pools.
- **Generation / acquisition:** generation determines an item; acquisition means actually receiving it.
- **Reward sources:** **Courier** means restocks from The Courier relic, distinct from the Potion Courier event. **Generic rewards** is an item-specific source class defined by each relevant policy.

### Randomness and addressing

- **Pool / sequence:** a pool defines candidate identities. A sequence is one ordered stream of entries; repeated identities are distinct entries even when they have the same identity. **Per-rarity identity sequences** provide one stream per rarity.
- **Allocation:** finite relic stock assigned to one source, ordered into one or more identity sequences.
- **Triple sequence:** a card identity sequence consumed only as complete consecutive groups of three entries; each group is a **triple**.
- **Filter / marker:** a filter defines eligibility for a draw. A marker occupies a sequence position and records one deferred advancement under the filter active when it was created.
- **Ordinal:** an opportunity's position within its category. Act-local ordinals reset each act; otherwise ordinals continue across acts.
- **Ordinal-addressed generation:** generation derived from the run seed and an opportunity's ordinal within its stated scope, including components such as act or category where applicable. **Shop-ordinal generation** names the shop case.
- **Event RNG:** an event's own generator, addressed by run seed + event ID in single-player, independently of event ordinals.

## Architecture overview

Each controlled random decision can be described along five dimensions: **outcome** (what is randomized), **sharing scope** (which requests share randomness), **address/progress** (how corresponding opportunities are identified, advanced and reset), **form** (addressed draw, stream, finite allocation or another coupling mechanism), and **state** (what must survive save/load).

Sharing scope and address/progress are separate design choices. Different stages of one source—such as occurrence, rarity, identity and attributes—may use different scopes and progress.

Randomness follows the shared [opportunity-consumption](#opportunity-consumption) invariant: progress commits when the corresponding random opportunity becomes available to the player. Consumption is therefore a shared rule, not a separate architectural dimension.

The tables below are a navigation map, not a second specification. They show settled mechanisms with distinct randomness architecture, not every policy or exception; linked policy sections own the details. Shared contracts for [opportunity consumption](#opportunity-consumption), [stable entry addresses](#stable-entry-addresses), [act-local resets](#act-local-resets) and [saved state](#saved-state) apply where relevant and are omitted unless a mechanism differs.

### Rewards

| System / randomized outcome | Sharing and address/progress | Form |
|---|---|---|
| [Shop stock, sale slot and base prices](#stock-and-prices) | Initial cards and sale use shop ordinal; restocks have source-specific progress; prices use stable entry addresses. | Ordinal-addressed draws, item sequences and entry-derived price rolls. |
| [Relic rarity and identity](#allocation) | Generic rewards, shop and Courier are separate; rarity progress is separate from per-rarity identity progress. | Rarity-roll streams and finite shuffled allocations. |
| [Potion generation and drops](#acquisition-and-sequences) | Generation uses four source groups; generic rarity modes share per-rarity identity progress. Ordinary drop checks share combat-wide progress and changing probability. | Rarity/identity streams and indexed drop checks. |
| [Chest gold](#rewards-and-consumption) | Non-empty chest ordinal. | Ordinal-addressed draw. |
| [Combat gold](#shared-sequence-and-compensation) | Normal fights and elites share a gold ordinal; normal fights also advance a separate compensation index. | Shared uniform rolls and paired compensation bits. |
| [Own-character random-rarity offers: Rare occurrence](#luck-and-rarity-offset) | Luck and threshold progress are shared; the combat offset applies only to its source groups. | Exponential-threshold stream with pending delivery. |
| [Card-offer identities and rarity mixes](#settled-policy-card-reward-offers) | Progress is separated by pool/request/offer group and is independent of shared Rare-occurrence progress. | Triple sequences, other identity/rarity streams and event-RNG draws. |
| [Card upgrades](#inherent-card-rolls) | Each generated card occurrence has independently addressed upgrade randomness. | Entry-derived roll. |
| [Permanent transformation / direct-addition results](#selection-and-consumption) | Identity progress is shared by pool; transformations and additions use separate rarity progress, with a source exception. | Identity and rarity-roll streams; event RNG for the exception. |
| [Event-specific rewards](#settled-policy-event-specific-rewards) | Event-local draws. | Event RNG outside shared reward sequences. |

### Encounter

| System / randomized outcome | Sharing and address/progress | Form |
|---|---|---|
| [Persistent card selection](#corrected-persistent-selection-priorities) | Each logical card occurrence carries progress within its selection scope. | Residual exponential clocks. |
| [Owned-potion selection](#settled-policy-owned-potion-selection) | Ranwid and Stone of All Time share per-occurrence progress. | Residual exponential clocks. |
| [Tooth, Bracelet and Bridge selection](#settled-policy-fixed-priority-card-selection) | Separate purpose-specific priorities; qualifying one-card reward choices share an anchor. | Fixed priority order; event RNG after Bridge exhaustion. |
| [Relic Trader's owned-relic selection](#owned-relic-selection) | Event-local selection opportunity; candidates are addressed by relic identity. | Fresh candidate priorities. |
| [Encounter setup](#settled-policy-encounter-setup) | Ordinary combats use act-local combat-category progress plus encounter identity; event-created battles use event-local progress. | Purpose-separated addressed RNG. |
| [Unknown-room type](#addressing-and-room-type-chances) | Act-local visited-`?` ordinal, interpreted using the current stored room-type chances. | Ordinal-addressed draw through changing probability intervals. |

## Shared strategies

These are shared contracts for covered policies. The [fairness principle](#goals-and-principles), [opportunity consumption](#opportunity-consumption), [stable addresses](#stable-entry-addresses) and [act-local reset safety](#act-local-resets) are design invariants. Source policies specify sharing scopes, additional rules and exceptions; otherwise these contracts apply without restatement.

### RNG derivation and addressing

**Invariant — independent derivation:** new or re-addressed randomness derives from run seed + stable sharing-scope and purpose identifiers + the address components specified by its policy. The same full address reproduces the same draw or stream; independent purposes use separate derivations and progress. Share randomness only where a policy specifies sharing or coupling. Explicitly retained baseline RNGs keep their stated addressing.

In design address expressions, `+` means unambiguous composition of components, not arithmetic addition. Use stable identifiers rather than display labels; for streams, distinguish the stream's address from its current position. Concrete labels, encoding and RNG/hash algorithms can be chosen during implementation, subject to this contract.

### Rarity and identity sequences

**Main idea:** share rarity rolls and identity order across related rewards, with independent sharing scopes and progress.

**Sharing rationale:** generic rewards represent successive opportunities to receive the same kind of item, regardless of route. Shops are isolated to preserve corresponding inventories; Courier and potion-generating effects are isolated so repeated use cannot displace ordinary rewards. Transformations and direct additions both generate permanent cards from a pool, so they share identity order while separate rarity rolls preserve their different odds.

**Scope:** relics, potions, cards, permanent transformations and covered direct additions.

With per-rarity identity sequences, a random-rarity request consumes one value from its **rarity-roll sequence**, then draws an eligible item from that rarity's **identity sequence** using the [filtered draw strategy](#filtered-draws). Fixed-rarity requests consume identity only; pools with one identity sequence draw directly. Source policies specify sharing and RNG exceptions.

**Generation and lifetime:** covered potion/card identity sequences and rarity-roll sequences are generated on demand and continue indefinitely, using independent RNGs for the source and pool divisions specified below. Each new potion/card identity entry is an independent uniform draw with replacement from its sequence's pool unless the source specifies otherwise, such as weighted or grouped generation. Only finite relic allocations borrow.

### Opportunity consumption

**Invariant — opportunity consumption:** randomness advances when the player is granted the corresponding random opportunity—that is, when its result becomes available to reveal or receive. Actual viewing, acceptance or selection is not required.

If an operation is prevented or bypassed before any random result becomes available, its randomness does not advance. Deferred rewards consume when their payoff opportunity occurs, not when the effect creating that future opportunity is acquired.

### Stable entry addresses

**Invariant — stable addresses:** filtered advancement, borrowing and removals never change an entry's original address.

An entry's address is its original source sequence + original entry position. Prices and saved state refer to this stable address, so repeated item identities remain distinct occurrences.

### Act-local resets

**Invariant — act-local reset safety:** act-local RNG seeds include the act number so resetting ordinals never restarts a previous act's RNG sequence.

### Inherent card rolls

**Main idea:** bind random attributes to each generated card occurrence so evaluating them cannot shift later identities or attributes.

Each card entry carries an **inherent upgrade roll** `u` uniformly distributed in `[0, 1)`, derived independently from the run seed, its [stable entry address](#stable-entry-addresses) and the upgrade purpose.

When generating an offer, upgrade an eligible card exactly when `u < p`, using that source's rarity- and act-dependent upgrade chance. Preserve sources that suppress random upgrades or explicitly guarantee an upgrade. Neither changes the inherent roll or advances another reward's randomness. This does not upgrade cards already in the deck when the act changes.

**Vanilla baseline:** ordinary Common/Uncommon chances in Acts 1/2/3 are **0% / 12.5% / 25%**; Rare cards have no ordinary act-based upgrade chance.

### Corrected persistent selection priorities

**Main idea:** for uniform occurrence selection, retain purpose-specific priorities across opportunities while correcting the survivors after each random selection. Use residual exponential clocks, not fresh priorities for every opportunity. Source policies define sharing for [upgrades/downgrades](#settled-policy-permanent-upgrades-and-downgrades), [fixed-priority card selection](#settled-policy-fixed-priority-card-selection) and [owned-potion selection](#settled-policy-owned-potion-selection).

**Address and state:** each logical occurrence starts with a clock `e_i = -ln(U_i)`, with independent `U_i ∈ (0, 1)` derived from run seed + its inherent selection key + selection scope/purpose. Initial keys use stable occurrence provenance, subject to explicit source alignment and, for cards, [equivalent-copy routing](#equivalent-card-selections); never use current list position.

**Selection and update:** take the eligible occurrence with the smallest clock, `m`. On committing the selection, subtract `m` from every unselected eligible survivor. Ineligible occurrences retain their clocks without subtraction or renewal. A new occurrence receives its own fresh inherent clock; a selected occurrence consumes its clock. Source policies define when and how to renew consumed clocks from the same distribution. Mere eligibility loss/re-entry does not consume an unused clock.

**Batches:** for an unordered uniform sample of `k` distinct occurrences from a fixed eligible set, select the `k` smallest pre-selection clocks and subtract the `k`-th smallest clock from every unselected eligible survivor once. Consume each selected clock once; a selected occurrence cannot re-enter the same batch. Commit from one eligibility snapshot, independently of UI click/enumeration order. Effects with intentionally sequential eligibility changes retain that distinction.

**Fixed-cohort shortcut:** if all members remain eligible until selected and each leaves permanently, sort their initial clocks once. Consume that order without subtraction or renewal.

Use independently addressed deterministic tie-breaks, never collection order. Previews or cancellations before an opportunity becomes available consume no clocks. A player-directed choice does not advance a gameplay selector's clocks merely because it changes membership; any hidden equivalent-copy routing has its own scope. Fixed-target and explicit affect-all effects also consume no selection clocks. An unrelated purpose's selection does not advance these clocks.

**Rationale and limit:** the exponential memoryless property preserves uniform selection probabilities while retaining the relative order of jointly participating survivors. This does not preserve one global ranking across different eligibility histories. For example, let `A`, `B`, `C` have clocks `0.5`, `0.6`, `0.7`: if only `A` and `C` participate and `A` is selected, the surviving `C` now outranks the inactive `B`.

### Equivalent-card selections

**Invariant — equivalent copies:** choosing any physical member of a gameplay-equivalent class has the same subsequent identity/status-level consequences, regardless of which copy was clicked. Hidden selection state must remain equivalent up to the association with physical copies. This applies to all card identities, not only Basic Strikes and Defends. Equivalence requires the same identity and all gameplay-relevant status; hidden RNG provenance and presentation position do not distinguish the class.

Retain one logical occurrence per copy, with its own inherent randomness: multiplicity must not collapse into one roll per identity. Representative-selection progress is separate for different card identities. Exact count coupling, operation-purpose scopes and logical-to-physical association are [unresolved](WORKING.md#equivalent-copy-routing).

**Presentation boundary:** operate visibly on the card the player selected. If the first displayed Strike is upgraded, that first Strike must visibly be upgraded; never substitute another displayed copy or reorder cards to implement equivalence. Preserve acquisition-order-based display and original reward entry addresses. Hidden selection provenance may be routed independently of the physical UI object, without rewriting past generated attributes.

### Opportunity-specific selection priorities

**Main idea:** for a source that adopts uniform selection without replacement, give each eligible candidate an independent priority and take the lowest `k` priorities. Removing an unselected candidate cannot change the selected set.

Derive priorities from run seed + the source's selection-opportunity address + candidate's stable key + selection purpose. Neither the current candidate collection nor its list positions enter the address. Use independent deterministic tie-break draws rather than collection order. A fresh opportunity uses fresh priorities; the source defines its sharing scope and progress. For unique owned relics, the candidate key is relic identity.

This opt-in mechanism remains available for effects such as Relic Trader. It does not override the corrected persistent strategy for permanent-card selection or settle its source/effect sharing.

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

Triple sequences reject duplicate candidates while generating each triple. Other card sequences use filtered draws to exclude duplicates.

### Saved state

Save sequence contents and progress, including original entry addresses, consumption, marker positions and conditions, and RNG progress. Also preserve residual clocks, source-specific priority aliases, logical-to-physical occurrence associations and pending fixed-cohort selections wherever used.

**Validation:** verify save/load preserves counters and outcomes. Use targeted RNG logs and paired runs; histories alone miss internal calls/actions. Use consistent seed derivation.

## Settled policy: shops, Wongos and Courier

**Intent:** Extra rewards and Courier restocks no longer scramble later shops. Corresponding shops keep the same sale slot; relocated offers keep their base prices. Wongos affects only future Rare shop offers, and only when its featured relic is affordable.

### Stock and prices

- Shop stock depends only on shop ordinal, except for Wongos and relic floor cutoffs.
- **Initial cards:** use shop-ordinal generation for rarity and identity. Preserve slot order, duplicate exclusion and unavailable-rarity fallback.
- **Relics and potions:** initial stock and Courier use their respective sources under [relic allocation](#allocation) and [potion sequences](#acquisition-and-sequences).
- **Card rarity:** vanilla shop base chances plus an invariant offset of `0` (`+0.008` without Scarcity). These approximations are informed by repeating `3 normal → 1 elite → 2 normal → 1 elite` and adjusted downward to account for boss resets.
- Sale slot uses independent shop-ordinal generation.
- **Base prices:** use run seed + [stable entry address](#stable-entry-addresses) for initial stock and Courier restocks. Initial shop cards use shop ordinal + slot as their entry address.
- **Courier cards:** use dedicated sequences, independent of initial stock and other card sources. Attack, Skill and Power each have a rarity-roll sequence using the shop odds above and per-rarity identity sequences; Uncommon and Rare colorless each have one fixed-rarity identity sequence. Preserve unavailable-rarity fallback, exclude cards still displayed, and retain [original mechanics](#shops-chests-and-relic-events).
- **Wongos:** featured offer consumes **one shop Rare identity**; below 200 gold, the featured offer is not shown and consumes nothing. Other options use [generic relic rewards](#source-classification-and-consumption).

**Tradeoff — relic floor cutoffs (see [appendix](#generation-and-selection-mechanics)):** reaching corresponding shops on opposite sides of a cutoff can change their relics and shift later stock; these differences are inevitable and benign.

### Context

**Catfood baseline:** tracks shop ordinal and isolates shop card-rarity state, while retaining `Shops` RNG for card identities and prices, shared relic bags/rarity rolls, and a shared potion-factory call index.

## Settled policy: relics

**Intent:** Shops, Courier and generic rewards no longer compete for relics during normal play. Random relic rewards no longer risk Catfood's fixed rarity-roll limit.

### Allocation

Construct three disjoint relic allocations once: generic rewards, shop and Courier. Each source has its own rarity-roll sequence and independently advancing, shuffled Common/Uncommon/Rare identity sequences. Split each Common/Uncommon/Rare pool **45% / 35% / 20%** between these sources respectively. The shop source also owns a shuffled Shop-rarity sequence containing all Shop-rarity identities.

Capacity is per rarity/source. Exact fully unlocked allocations (generic rewards / shop / Courier): **Common 12 / 9 / 5; Uncommon 14 / 11 / 7; Rare 17 / 13 / 8**.

Mix shop-ineligible relics into allocations normally.

**Generic opportunity coverage:** advancing ordinary generic reward opportunities must eventually expose every still-eligible entry, subject to the stated exhaustion/borrowing boundary. An outside multi-choice offer must not retire unchosen generic entries before those opportunities.

### Source classification and consumption

Hidden random relic rewards from combat/chests/events use the generic rewards source, with source-specific eligibility, regardless of cost (including Ranwid and Mystery Box). Classify deferred payoffs separately from their granting relic.

### Borrowing

**Scope boundary:** borrowing covers source exhaustion. Entire-rarity exhaustion is outside discussion scope; fallback and chest refilling are implementation details. Rare tail effects and cross-sequence interactions caused by borrowing are outside ordinary isolation analysis.

On exhaustion, borrow the **last eligible remaining identity** of the requested rarity. Other sequences borrow from Courier first; Courier borrows from generic rewards first.

Marker resolution follows the same borrowing and marker placement rules as a filtered draw.

**Implementation details — chest refilling:** when same-rarity borrowing fails, retain the normal generic rarity result and select uniformly among eligible identities of that rarity in the original shared relic pool, using one additional draw from the generic relic rarity RNG. This introduces no new RNG but advances later generic rarity rolls, accepted only in this out-of-scope case. If no refill candidate exists, use the final fallback.

**Implementation details — final fallback:** starting at the requested rarity, try local entries and same-rarity borrowing before advancing through Shop → Common → Uncommon → Rare, without wrapping or another rarity roll; otherwise return Circlet.

### Context

**Catfood baseline:** retains shared relic bags but precomputes 400 rarity rolls; roll 401 would fail (code finding, not reproduced).

**Reference — fully unlocked pools:** **122 ordinary relics per character**—26 Common, 32 Uncommon, 38 Rare, 26 Shop (115 shared + 7 character-specific).

## Settled policy: potions

**Intent:** Rewards, shops, Courier and potion-generating effects no longer scramble one another's potions. Receiving potions together or separately preserves comparable results. Failed direct grants no longer change later potions. White Beast Statue no longer shifts ordinary potion drops after its removal.

### Acquisition and sequences

- Keep independent sequences for four **sources: generic rewards, shop, Courier and potion-generating effects**. Generic rewards combines random combat rewards, events and other grants without paid/free or per-event separation. **Potion-generating effects** covers Alchemize, Entropic Brew and Delicate Frond, including use outside combat.
- Each source uses the full eligible potion pool with independent Common/Uncommon/Rare identity sequences.
- Within the generic rewards source, use separate rarity-roll sequences for **[standard weights](#generation-and-selection-mechanics)** and **uniform-identity selection**. Uniform mode weights each rarity by its share of eligible identities. Both modes consume the same per-rarity identity sequences. Other sources use one standard-weight rarity-roll sequence each.
- Preserve existing failure feedback, including Alchemize's failure animation. Batch boundaries affect duplicate filtering only.

### Drop occurrence

- Retain Catfood addressing and vanilla's shared changing probability across normal fights, elites and bosses. Extra ordinary checks advance the cumulative index; combat-category threshold differences are preserved. No act/Ancient reset.
- **White Beast Statue:** forced drops freeze the occurrence index and stored probability; the potion still consumes a reward identity.
- Save the occurrence index and probability.

### Context

**Catfood baseline:** addresses drop checks by `player seed` and `cumulative check index`, starting at index 0, while preserving vanilla's changing probability. Forced drops also advance this index. Separately, potion factories use a shared call index to seed each batch.

**Example — batch boundaries:** Catfood's call-based seeding can turn `A | BCD | E` for requests `1–3–1` into `AFG | B` for `3–1`. Per-potion consumption instead follows `ABC | D`, subject to batch duplicate filtering.

## Settled policy: chests

**Intent:** Chest gold no longer depends on how many normal fights you took. Silver Crucible's empty chest no longer shifts later chest rewards.

### Rewards and consumption

- Base gold uses ordinal-addressed generation with the non-empty chest ordinal. Combining it with the normal-fight/elite gold sequence is rejected because vanilla A3+ chest gold (31–39) is nonuniform.
- Silver Crucible suppresses the first chest's rewards before generation, advancing neither chest ordinal nor relic sequences; the next chest is ordinal 1.

### Context

**Catfood baseline:** has no chest ordinal; chest entry re-seeds `Rewards` with the normal-fight count for gold. Silver Crucible's suppression is unchanged.

## Settled policy: combat gold

**Intent:** share random variation between unreduced normal-fight and elite gold draws so reordering them preserves total base gold. Keep vanilla expected gold and isolate these draws from other rewards.

### Shared sequence and compensation

**Shared roll:** generate uniform `u ∈ [0,1)` from run seed + combined normal-fight/elite gold-reward ordinal, including event battles. Set `X = floor(11u)` at A0–2 or `floor(8u)` at A3+.

Fixed or absent combat gold, fixed extra payouts and event-choice gold do not consume this sequence; see the appendix for exceptions.

| Difficulty | Shared uniform integer `X` | Normal-fight gold | Elite gold |
|---|---|---|---|
| A0–2 | 0–10 inclusive | `10 + X` | `35 + X` |
| A3+ | 0–7 inclusive | `7 + X + b` | `26 + X` |

**Compensation:** for each pair `j`, derive an independent fair bit `h(j)` from run seed + compensation purpose + pair index, independently of `u`. Use `b(2j) = h(j)` and `b(2j + 1) = 1 - h(j)`, equivalently `b(n) = h(floor(n / 2)) XOR (n mod 2)`. Here `n` is the zero-based unreduced normal-fight gold-reward index; elites do not advance it. Both indices continue across acts. Pair bits are reproducible from their addresses and require no saved phase bit.

**Rationale:** each pair contributes exactly one compensation gold; odd-length prefixes differ from their expected compensation by only 0.5 gold. Independent pair orientations preserve this balance without imposing correlations between different pairs.

**Tradeoff:** expected gold is unchanged, but A3+ normal gold is nonuniform: endpoints 7 and 15 each have probability 1/16; 8–14 each have 1/8.

**Gremlin Merc:** half-gold rewards use `5 + floor(6u)` at A0–2 or `4 + floor(5u)` at A3+, consuming the shared gold ordinal without applying or advancing compensation.

### Rejected alternatives

Drawbacks below concern A3+:

- **Shared range-relative rolls:** preserves vanilla distributions, but swapping normal → elite to elite → normal can change total gold by 1.
- **Shared identical offsets without compensation:** using `X = 0–7`, normal `7 + X` and elite `26 + X` removes order differences but lowers expected normal-fight gold by 0.5.
- **Separate normal-fight and elite sequences:** preserves vanilla distributions, but replacing a normal fight with an elite can shift the premium roughly 8 gold above or below its expectation.

### Context

**Catfood baseline:** re-seeds shared `Rewards` on room entry using run seed and cumulative combat-category count, with different multipliers for normal fights and elites. Vanilla gold ranges and rounding are unchanged.

## Settled policy: card reward offers

**Intent:** Equivalent rewards share identity order across granting sources. Corresponding rare-bearing offers share the same choices even when different reward sources reach them; Catfood separates normal fights and elites. Larger own-character drafts preserve later three-card offer boundaries. Random upgrades cannot scramble later card choices; matching Lasting Candy triggers retain their rarity.

**Scope:** initial generation and reachable rerolls for the groups below. Preserve source eligibility, offer sizes, selection rules and upgrade behavior. The random-rarity group's [sequence choices, calibration and behavior without Scarcity](WORKING.md#random-rarity-offer-sequences) remain unresolved.

**Grouping:** card rewards are organized first by pool, then by rarity request, eligibility filter and draw grouping. Upgrade rules and how many offered cards the player takes do not split identity groups.

### Own-character random-rarity offers

**Intent:** separate when a Rare appears from the complete choice offered. Align corresponding rare-bearing and non-Rare opportunities while preserving source-dependent Rare chances; matching combat-room ordinals need not consume matching offer entries.

| Rarity rule | Sources / offer structure |
|---|---|
| Normal-fight rule | Normal fights, Dream Catcher, Prayer Wheel and The Hunt in normal fights; three-card offers. |
| Elite rule | Elites and The Hunt in elite combats; three-card offers. |
| Fixed base odds | Orrery: five three-card offers; Lost Coffer: one; Brain Leech's Share Knowledge: choose one of five; Trial's Nondescript Guilty: two three-card offers. |

All three source groups share luck, thresholds, the rare-bearing sequence and pending-Rare delivery. Shops, fixed-rarity offers, Lasting Candy's additional Power and other card groups retain their separate policies.

#### Luck and rarity offset

Maintain luck `L`, initially zero, and a threshold `T = -ln(U)`, where `U` is uniform on `(0, 1)`. Thresholds use an independent sequence addressed by run seed, purpose and threshold ordinal. Advance it only when a Rare is delivered into a generated offer.

Process card slots in order. Each slot adds `-ln(1 - p)` to luck, where `p` is its Rare probability. Normal-fight-rule and elite slots use `max(0, base Rare chance + o)` with one shared combat offset `o`; fixed-base-odds slots use their base Rare chance without reading or modifying `o`.

The combat offset starts at `-0.05`. Normal-fight-rule and elite non-Rare slots add `0.005`, capped at `0.40`; a delivered Rare in either group resets it to `-0.05`. Zero-probability slots add no luck but still grow the offset. Retain boss-reward offset resets, without advancing luck or these offer sequences.

When a slot reaches the threshold and its offer has no Rare yet, resolve that slot as Rare, reset luck to zero and advance to the next threshold. Discard the overshoot. Common/Uncommon identities and their rarity mix come from the offer sequences, independently of this simulation.

Luck, the current threshold and sequence progress carry across acts; the combat offset resets to `-0.05` between acts. Save this state under the [shared saved-state contract](#saved-state), including an already-reached threshold awaiting delivery.

**Rationale:** before deferral, `P(no Rare through n slots) = exp(-sum(-ln(1 - p_i))) = product(1 - p_i)`. Exponential thresholds preserve the supplied waiting probabilities without fitting a route-dependent luck distribution. This does not make the capped, deferred reward system identical to vanilla.

#### One Rare per offer and deferral

Cap each covered offer at one Rare. If a later slot reaches the next threshold, suppress its Rare and keep the reached threshold pending: do not reset luck or advance the threshold. Treat suppressed slots as non-Rare for applicable offset updates. Continue accumulating luck, but create no further pending occurrences until delivery.

A **Rare-eligible offer** has a positive chance of producing a Rare along its ordinary all-non-Rare progression, ignoring pending delivery. At the next such offer, deliver the pending Rare in its first simulated slot instead of performing that slot's ordinary check; reset luck, clear pending and advance the threshold once, applying the delivery source's offset rule. Process its remaining slots normally. Ineligible offers leave the pending threshold intact and apply normal non-Rare offset growth.

This naturally excludes the first two three-card normal-rule offers after an offset reset without elite intervention; use probability eligibility rather than room counts. Delivery follows offer generation order, including extra offers and rerolls, and can wait across acts.

**Accepted tradeoff:** separating the rare Scarcity double-Rare elite result changes acquisition opportunities. Fixed-odds offers can also produce multiple Rares in vanilla; additional occurrences beyond the single pending threshold are deliberately omitted rather than queued.

#### Offer sequences and rerolls

Use deterministic triple sequences independent of the luck thresholds. One shared **rare-bearing sequence** supplies one Rare and two non-Rares per triple; non-Rare sequences supply three Common/Uncommon cards. The non-Rare sequence split, fixed-odds routing and opening-entry placement remain [unresolved](WORKING.md#random-rarity-offer-sequences).

A generated offer consumes the next triple from its applicable non-Rare sequence or, if a Rare is delivered, from the shared rare-bearing sequence. A suppressed occurrence reserves no additional triple.

Opening non-Rare entries use vanilla first-two-normal-offer probabilities through their own deterministic generation. Do not recalculate entries using the live offset or vanilla's shared RNG.

Store the Rare first in each rare-bearing triple. After simulating the offer, swap it with the card at the simulated Rare position if needed. Move each card's stable entry address and inherent rolls with it; presentation does not change the simulated state.

**Rerolls:** generate a fresh offer under the same luck, offset, deferral and sequence rules.

**Brain Leech — Share Knowledge:** simulate all five slots first, including pending delivery and any later suppressed crossing. If no Rare is delivered, consume one triple from the sequence used by normal-fight-rule non-Rare rewards; otherwise consume one shared rare-bearing triple. Generate two additional Common/Uncommon cards with event RNG, excluding all identities already selected. Append them, then swap the triple's Rare into the simulated position, including positions four or five.

### Own-character fixed-rarity offers

Use separate Common, Uncommon and Rare triple sequences.

| Sequence | Sources and consumption |
|---|---|
| Rare | One triple per offer: non-final bosses, White Star, The Hunt's delivered boss rewards, Hefty Tablet, Glass Eye's Rare offer and Crystal Sphere's Rare offer. |
| Common | Glass Eye consumes two triples; Crystal Sphere's Common offer consumes one. Room Full of Cheese consumes one; Scroll Boxes follows its branch rules below. |
| Uncommon | Glass Eye consumes two triples; Crystal Sphere's Uncommon offer consumes one. |

- **Room Full of Cheese:** use one Common triple and generate the remaining five Common cards with event RNG.
- **Crystal Sphere:** does not reset the combat rarity offset.

#### Scroll Boxes

Resolve Defect's two independent 1% triple-Claw substitution checks with event RNG before requesting Common sequence entries. Non-Defect offers follow the no-substitution branch.

- **No substitution:** consume one Common triple. It supplies both Common cards in bundle one and the first Common card in bundle two; generate the remaining Common and both Uncommons with event RNG.
- **Scroll Boxes — triple-Claw exception:** if either bundle is replaced, bypass the shared Common sequence entirely, without inspecting its head. Each substituted bundle contains three fixed Claws; generate any remaining ordinary bundle entirely with event RNG. If both bundles are substituted, generate no unused ordinary-bundle cards.

### Own-character Power offers

Lasting Candy and The Future of Potions' Power choices share separate Uncommon and Rare Power identity sequences, independent of the triple sequences above.

Lasting Candy uses two independent rarity-roll sequences, one for normal fights and one for elites. Advance the relevant sequence once per triggered additional card, preserving the source's base rarity odds and unavailable-rarity fallback. Boss triggers request Rare directly. The Future of Potions requests its specified rarity.

### Other The Future of Potions choices

Attack and Skill offers use event RNG addressed by rarity and type, independently of the other potion options.

### Colorless offers

Lead Paperweight and Brain Leech's Rip share one reward sequence with the existing Uncommon/Rare odds: **98.51% / 1.49%**.

### Other-character offers

Kaleidoscope, Colorful Philosophers and Sea Glass share **12 identity sequences: four other characters × Common/Uncommon/Rare**. Kaleidoscope uses event RNG for character and rarity selection; the other sources request fixed rarities.

Colorless and other-character rewards explicitly share across different batch sizes because these sources are few and their offer sizes vary.

### Rerolls outside the shared luck system

A reachable reroll consumes a fresh replacement offer from the same applicable pool/request/filter group, using its ordinary offer structure and duplicate rules. Fixed-rarity offers consume fresh triples of that fixed rarity, not the random-rarity group's rare-bearing sequence. Other groups consume fresh entries under their existing grouping rules.

Event-generated supplementary cards use fresh local generation rather than restarting the original offer's address. Preserve separately specified offset effects; rerolling does not enroll these sources in the shared luck system.

Kaleidoscope's Neow offers cannot be rerolled with Pael's reroll relic in the intended normal-run scope. No Kaleidoscope reroll decision is required.

## Settled policy: permanent transformations and direct card additions

**Intent:** transformations and covered direct additions give comparable same-pool results, preserving source rarity odds and avoiding interference from card reward offers or combat transformations.

**Covered direct additions:** Infested Automaton's Study and Touch Core, Endless Conveyor's Fried Eel, Neow's Arcane Scroll and Neow's Bones curse, and Distinguished Cape.

### Selection and consumption

- Keep sequences for seven pools: the five characters, colorless and curse. The player's character and colorless each use separate Common/Uncommon/Rare identity sequences, shared by transformations and covered direct additions, plus independent transformation and addition rarity-roll sequences. Other-character pools and curses retain one identity sequence each. Isolate all of these from card reward offers and combat transformations.
- **Transformations:** retain [vanilla target pools and eligibility](#generation-and-selection-mechanics). For per-rarity identity sequences, interpret the transformation rarity roll using current eligible identity counts per rarity, excluding the original card.
- **Direct additions:** use the granting source's pool and restrictions. Interpret the addition rarity roll using vanilla base odds and unavailable-rarity fallback.
- Transformations and direct additions consume on generation. Previews and canceled selections consume nothing.

**Infested Automaton:** Study and Touch Core use its event RNG for rarity without advancing the addition rarity sequence. Identities still use the shared character sequences.

**Distinguished Cape:** draw twice from the curse sequence; the second pull skips the first identity.

### Context

**Catfood baseline:** retains caller-supplied transformation RNGs; card-factory additions use room/event and options-call addressing. Neither uses dedicated shared identity sequences by pool.

## Settled policy: event-specific rewards

Trash Heap, Doll Room and event choices granting otherwise unobtainable rewards use the event RNG, independently of shared reward sequences. Preserve existing choices, costs and eligibility.

## Settled policy: permanent upgrades and downgrades

**Intent:** align random permanent upgrade and downgrade targets across sources.

Use the [shared clock strategy](#corrected-persistent-selection-priorities). All random upgrades of existing deck cards share one scope; random downgrades share another. Source filters do not create separate clocks.
**Upgrade sources:** Whetstone, War Paint, Fragrant Mushroom, Sand Castle, War Hammer, Fishing Rod, Doors of Light and Dark, Battleworn Dummy, Endless Conveyor, Tablet of Truth, and Reflections.
**Downgrade sources:** Wongos and Reflections.

Consumed clocks are marked zero.

- **Upgrade-clock refill:** after a random downgrade, retain a nonzero upgrade clock; otherwise take the next value from one dedicated run-wide refill sequence, shared only by Wongos and Reflections. This sequence is consumed at most three times per run.
- **Downgrade-clock refill:** Wongos refreshes the downgraded card's downgrade clock using event RNG. Reflections leaves consumed downgrade clocks at zero; no later permanent downgrade source can use them.

Preserve triggers, counts, eligibility and effect order. Reflections applies downgrades in ascending pre-selection downgrade-clock order: first downgraded, first refilled. It then determines upgrade candidates; downgraded cards may qualify.
Shared progress aligns successive opportunities while preserving uniform selection odds. Target selection stops advancing event or Niche RNG to isolate unrelated outcomes.

## Settled policy: fixed-priority card selection

**Intent:** align affected cards across decks and single-card reward choices.

Use separate purpose-specific priorities and the [fixed-cohort shortcut](#corrected-persistent-selection-priorities): lowest first, without correction or renewal. Preserve existing costs and triggers.

- **Pael's Tooth:** store five player-selected unupgraded cards outside the deck. At each subsequent combat trigger, return the next card upgraded and remove its record. Later removals never add or recycle cohort records.
- **Beautiful Bracelet:** enchant up to four eligible deck cards with Swift 2.
- **Slippery Bridge:** offer each removable deck card once, then use event RNG over all removable cards for subsequent draws. This replaces vanilla's Basic and previous-identity exclusions to preserve one fixed order.

### Single-card reward-choice alignment

For each purpose above, a reward choice that permits taking at most one card uses the first card's priority key for whichever card is acquired. Choosing a later card inherits that priority, not the first card's identity, upgrade roll or unrelated inherent RNGs. Freeze the anchor in canonical offer generation order, before presentation-only swaps such as moving the Rare to its simulated slot. Do not derive it from a subsequently sorted UI list.

Different one-card offers have distinct anchors; a rerolled offer uses its fresh entries. Multi-card-capable selections, including bundles, do not apply this alignment, even when the player actually takes only one card. Each acquired occurrence there keeps its own priority keys. Selecting existing deck cards is not a reward-choice alignment operation.

## Settled policy: owned-potion selection

**Intent:** align potions offered for sacrifice across events, independently of inventory order.

Ranwid the Elder and Stone of All Time share one run-wide scope using the [shared clock strategy](#corrected-persistent-selection-priorities), offering the lowest-clock owned potion. Each acquired copy remains a distinct logical occurrence.

With only one owned potion, leave its clock unchanged. Otherwise, renew the offered occurrence's clock once at commitment, using its key and a per-occurrence renewal count, so a declined sacrifice remains selectable later. Save the offer target and clock state; restoring the offer does not advance clocks again. Preserve event access, costs and rewards.

Equivalent player-chosen potion use/discard/trades remove the same logical occurrence, with its clock and renewal state, regardless of the clicked slot. Visibly consume the clicked potion and preserve the same remaining hidden clocks. Preserve slot-dependent offers and distinct trade payoffs; an already random-selected target needs no representative reselection. The representative-selection mechanism remains part of [equivalent-copy routing](WORKING.md#equivalent-copy-routing).

**The Future of Potions:** retain vanilla potion selection without consuming this selection scope; generated rewards follow their existing policies. Card-type assignment remains [unresolved](WORKING.md#the-future-of-potions-card-type-assignment).

**Vanilla baseline:** Ranwid and Stone select one owned potion uniformly with event RNG. The Future of Potions offers the first three owned potions in slot order and independently assigns a random allowed card type to each owned potion using event RNG.

## Settled policy: Relic Trader

**Intent:** make offered owned relics insensitive to collection order and associate replacements with the outgoing relic, not its display slot.

### Owned-relic selection

Preserve event access and tradability restrictions. Select three eligible owned relics with [opportunity-specific priorities](#opportunity-specific-selection-priorities), using event identity + event-local selection-opportunity index as the selection address and relic identity as the candidate key.

### Replacement constraints and consumption

Replacement rarity is independent of the outgoing relic's rarity; replacements need not themselves be tradable. Consume all three replacement identities and rarity outcomes when the offer is generated, before the player chooses a trade.

The replacement source and collision/eligibility handling remain [unresolved](WORKING.md#relic-trader). The mapping must be anchored to outgoing relic identity so that common inputs can keep their counterparts when feasible. Do not assign these replacements to the generic rewards allocation: preserve its [opportunity coverage](#allocation). Do not create a separate disjoint Trader allocation. The unresolved source policy must define how consuming Trader offers affects finite stock; it must not silently retire unchosen generic entries.

## Settled policy: encounter setup

**Intent:** align corresponding combat setups within an act regardless of earlier-act combat counts.

### Combat addresses

For ordinary normal fights, elites and bosses, address setup by run seed + act + combat category + act-local combat ordinal + encounter identity + setup purpose. Keep separate ordinal progress for these combat categories.

Event-created battles that do not consume an ordinary encounter list use event identity + event-local battle ordinal instead of an ordinary combat-category ordinal, together with run seed, encounter identity and setup purpose. They do not advance ordinary encounter setup progress.

Preserve existing encounter identity selection and setup sampling rules. This changes setup addressing, not reward ordinals, ordinary monster HP or AI.

## Settled policy: forced events

**Intent:** inserting a forced event must not consume or skip an ordinary event opportunity and thereby alter later ordinary-event availability.

Resolve room type under the [unknown-room policy](#settled-policy-unknown-room-resolution). When an effect supplies a forced event, enter it without ordinary event-list eligibility scanning, selection, cursor advancement or ordinary event-opportunity advancement. Record the actual event visit and retain the normal visited-event semantics. The forced event uses its existing event RNG; it needs no special ordinary-event ordinal.

Lantern Key's forced War Historian Repy follows this bypass. A visited `?` still advances its visited-node ordinal; preserve the existing forced room type and frozen non-event chances. Ordinary event-list advancement is deliberately removed from forced insertion, not from ordinary event visits.

## Settled policy: unknown-room resolution

**Intent:** align random draws within each act and keep each room type's probability interval as stationary as possible, limiting interference from earlier acts and other room types' changing chances.

### Addressing and room-type chances

- **Addressing:** room-type RNG uses run seed + act + act-local ordinal of visited `?` nodes.
- **Shop chance:** when the map-adjacency policy excludes Shop, its stored chance remains unchanged, matching vanilla. Preserve existing Juzu Bracelet, Golden Compass and Lantern Key room-type restrictions.
- **Effective chances:** after exclusions, allocate a 100% probability budget in vanilla order (normal fight → chest → shop), capping each chance at the remaining budget. Use these effective chances only as interval widths; preserve stored chances and their vanilla updates.
- **Thresholds:** the chest interval begins at 0, the shop interval ends at 1, and the normal-fight interval stays centered at `7/15` unless shifted just enough to avoid overlap; `7/15` is the midpoint when chest/normal-fight/shop widths fill the range in their base `2:10:3` ratio. Unassigned space resolves to Event.

## Reference

[Event spreadsheet](references/StS2_Events.xlsx): **57 ordinary/forced events and 8 Ancients**. `is_deterministic` concerns offered choices, not hidden rewards: Punch-Off is true; Wongos is false. Detailed criteria and state restrictions are in the workbook.

## Appendix: existing mechanics

This appendix records existing mechanics for comparison: vanilla by default, with Catfood behavior explicitly labeled. Settled policies belong in the main design sections.

### Vanilla baseline

This overview summarizes interference and Catfood differences. Source-specific rules are in [relic and event rules](#relic-and-event-rules); exact RNG, probability and selection rules are in [generation and selection mechanics](#generation-and-selection-mechanics) and [Catfood addressing and combat rewards](#catfood-addressing-and-combat-rewards).

| Mechanism | Baseline and interference |
|---|---|
| Unknown room types | Changing odds and persistent RNG progress make `?` resolution route-dependent. Catfood leaves this unchanged. |
| Event / encounter identities | Event eligibility and room visits affect list progression. Catfood leaves identity selection unchanged. |
| Shops and events | Shops use `Shops` RNG for identities/prices and shared `Rewards` for card rarity/upgrades and relic rarity. Events may consume shared rewards/bags despite having an event RNG. Courier shares these generators; visits/restocks can shift later outcomes and prices. |
| Combat | Route length and earlier reshuffles can change later setup and opening hands. Catfood addresses setup by room count and re-seeds shuffles at combat start. |
| Combat gold | Shared `Rewards` RNG lets other reward draws shift gold. Catfood re-seeds it on room entry but retains vanilla gold ranges and rounding. |
| Card rarity | Combat rewards change a shared rarity offset also read by shops. Catfood separates these offsets and changes when they reset. |
| Ancient options | Event-local RNG keeps options independent of event counts. |
| Chests | Relics use shared rarity RNG and identity bags; gold uses `Rewards`. Other rewards can shift chest contents. |
| Relic pools | Sources share finite identity bags, with different draw directions and eligibility filters. Depletion can change delivered rarity. |
| Potion drops | Checks share `Rewards` RNG and a changing probability across combats. |
| Potion generation | Failed grants can still shift later results. |
| Transformations | Callers use event RNG, `Niche` or player `Transformations` RNG, so results depend on the source and its other consumption. |
| Direct card additions | Sources use shared reward RNG, `Niche` or event RNG, with source-specific selection rules independent of combat rarity offsets. |

### Catfood addressing and combat rewards

**Room addressing:** cumulative normal-fight/elite/boss/shop/event/Ancient counters. Ancients increment both `eventEntered` and `ancientVisited`; options stay independently seeded, while Ancient card overrides use `ancientVisited`. Room reward RNG uses room-type counts, a normal-fight fallback and event-card overrides.

**Combat card generation:** identities/upgrades use room type, count and offer position. Rarity is rolled before identity; a Rare consumes the current slot's identity draw. There are no independent per-rarity identity sequences. Later identities can still change through rarity-offset resets and offer duplicate exclusion. The initial offer uses the RNG intended for rerolls: a code-confirmed wrong-stream bug, not a wrong ordinal; gameplay impact has not been reproduced.

**Card rarity:** normal fights and elites have separate offsets with vanilla base probabilities, non-rare growth and post-Rare reset value. Shops have a separate offset that does not grow, leaving their Rare roll chance at 0% (4% without Scarcity). Ancient entry, including Neow, resets these offsets to `-0.05`. The separately stored boss offset has no effect; boss rewards no longer reset normal-fight or elite offsets. Combat rerolls update their room type's rarity state, potentially shifting later offers; non-Rare rerolls raise the offset, as in vanilla.

**Direct/event generation:** seeds add room/event count to a per-room options-call index. Seed collisions exist; harmful gameplay impact remains unverified.

**Encounter setup:** inherits vanilla event/encounter identity selection; cumulative room counters do not select identities. Encounter ID + cumulative room-type ordinal controls variable composition/setup, not ordinary HP. No visible setup mismatch has been reproduced. Lantern Key's forced event is counted normally.

**Shuffles:** re-seeds shuffle RNG at combat start; later shuffles continue it.

### Relic and event rules

#### Room resolution

| Source | Mechanics |
|---|---|
| `?` shop exclusion | Shop is excluded when the previous visited map point contained a shop, or every outgoing map node is a shop. Stored shop odds freeze during exclusion. |
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

#### Card reward offers

| Source | Mechanics |
|---|---|
| Hefty Tablet | Choose one of three own-character Rares; Injury is added even if the Rare is skipped. |
| Glass Eye | Five three-card offers: two Common, two Uncommon and one Rare, from the character pool. No random upgrades. |
| Scroll Boxes | Choose one of two bundles, normally two Commons and one Uncommon each with six distinct identities across the ordinary bundles. Defect has an independent 1% triple-Claw substitution chance per bundle; substituted bundles instead contain three copies of Claw. |
| Room Full of Cheese — Gorge | Choose two of eight distinct Common character cards; no random upgrades. |
| Crystal Sphere | The board has one card-reward item per ordinary rarity. Each recovered item gives a three-card offer of that rarity; vanilla identities and upgrade rolls use event RNG. |
| The Future of Potions | Trade a potion for three explicitly upgraded character cards of the option's rarity/type. Eight combinations exist: Common Attack/Skill and Uncommon/Rare Attack/Skill/Power. Rare/Event potions map to Rare cards; Common/Token to Common. |
| Lead Paperweight / Brain Leech — Rip | Choose one of two / three colorless cards respectively. Rip costs 5 HP and suppresses random upgrades. |
| Kaleidoscope | Two three-card offers; each independently selects three distinct other characters and generates one card from each using base rarity odds. |
| Colorful Philosophers | Choose another character's color, then receive one three-card offer per ordinary rarity from that pool. Ordinary upgrade rolls apply. |
| Sea Glass | Five cards per ordinary rarity from one featured other character; take any number of the 15. No random upgrades. |
| Lasting Candy | Adds a Power on alternating qualifying combat rewards, using the combat source's base rarity odds without its offset. Excludes existing offer identities unless no eligible alternative remains. |

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
| Pael's Tooth | The player selects five unupgraded cards and removes them. Each subsequent combat returns one random card from the remaining stored cohort, upgraded; after five combats all five have returned. Returned records leave the cohort. This is stored-card selection without replacement, not new identity generation. Mechanics clarified by the project owner. |

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

**Card pools:** there are no Common Power cards in the game.

**Unknown-room resolution:** base chances are 10% normal fight / 2% chest / 3% shop; Event takes the remainder (initially 85%). Elites are disabled under ordinary rules. Each roll consumes one `UnknownMapPoint` draw. The selected non-event type resets to its base chance; other allowed non-event types gain their base amount. Excluded types retain their stored odds. Exclusions apply before interpreting the draw, without renormalizing the remaining probabilities. Odds reset between acts; RNG progress continues.

**Unknown-room overflow:** vanilla checks cumulative thresholds in normal fight → elite (if enabled) → chest → shop order. Totals above 100% are neither normalized nor capped in stored state; intervals beyond 1 are unreachable, so later types lose effective probability and Event has none. At 70% normal fight / 14% chest / 21% shop, effective chances are 70% / 14% / 16%.

**Event / encounter identity selection:** per-act lists are generated upfront with `UpFront` RNG; event lists shuffle act-specific plus shared identities without explicit weights. Room type is resolved first. An Event advances through its act's pre-shuffled list to the next `IsAllowed` identity not in the run-wide visited-event set; skipped entries are passed over, not retained for the next eligible pull. A normal fight uses the next entry in the act's normal encounter list, regardless of whether its map marker was `?` or a normal-fight marker. Forced-event replacement occurs after ordinary event eligibility scanning.

**Event-list wrapping (vanilla):** the cursor indexes `events[cursor % events.Count]`; the list wraps without reshuffling or clearing visits, and skipped entries remain in it. Skips advance the cursor too. A skipped event can qualify after wrapping; a full unsuccessful scan instead returns the cursor's event anyway, potentially repeated or ineligible. See [RoomSet.NextEvent / EnsureNextEventIsValid](sts2-decompiled/MegaCrit.Sts2.Core.Rooms/RoomSet.cs).

**Event-pool capacity:** act lists contain 25–31 entries before eligibility/visited filtering, versus 13–15 ordinary floors per act. Ordinary event-policy discussion assumes sufficient eligible, unvisited supply and excludes wrapping, exhaustion and singleton sets.

**Event skipping and probability:** with a uniform hidden shuffle, other eligibility held fixed and no wrapping, encounters while condition K is false do not themselves change K-events' later selection probability when K becomes true. Skipping passes hidden K entries along with encountered non-K entries; exchangeability of their positions preserves the odds. For two K events and three others, zero, one or two non-K encounters while K is false leave the next K chance at `2/5`. Encounters while K is true have the usual without-replacement effect. This is a distributional property, not identical outcomes for each seed or invariance under other eligibility changes.

**Ancient options:** use event RNG seeded with run seed + event identity hash + player-slot contribution, independently of event counts.

**Combat setup and shuffles:** encounter setup uses floor-based addressing. Ordinary monster HP uses `Niche` RNG. Shuffle RNG persists across combats; extra reshuffles advance the same generator used for later opening hands.

**Card rarity:** shared offset starts at `-0.05` and resets there after a Rare combat reward roll, but not after direct additions or on Ancient entry. Normal/elite non-rare rolls add `0.005`, capped at `0.40`. Base Rare chances for normal/elite/shop rolls are 1.49%/5%/4.5%, plus the offset; base Uncommon chances are 37%/40%/37%. A negative Rare threshold also reduces the Uncommon chance: initial normal rewards have 0% Rare and 33.49% Uncommon. Boss rewards guarantee Rare and reset the shared offset. Shop rolls read the offset without updating it.

**Base-odds card offers:** Orrery, Lost Coffer, Brain Leech's Share Knowledge and Trial's Nondescript Guilty use Common/Uncommon/Rare odds of 61.51%/37%/1.49% per slot without reading or updating the combat offset. Share Knowledge and Trial suppress random upgrades. Each slot rolls base odds independently, so these offers can contain multiple Rares, including three in a three-card offer.

**Without Scarcity (A0–6):** normal/elite/shop base Rare chances are 3%/10%/9%, non-Rare offset growth is `0.01`, and ordinary Common/Uncommon upgrade chances in Acts 1/2/3 are 0%/25%/50%. Base normal/direct-addition Common/Uncommon/Rare odds are 60%/37%/3%.

**Normal versus elite card rewards:** both offer three cards from the same character pool, exclude duplicates within the offer and use the same upgrade rules; elites have no inherent upgrade-chance bonus. Under Scarcity, ordinary normal offers cannot contain two Rares because the rare chance after offset is negative; elites can produce Rare → non-Rare → Rare because the last slot has a 0.5% Rare chance after the intervening non-Rare. Catfood preserves these rules.

**Combat gold:** uses shared `Rewards` RNG, uniformly over default inclusive ranges: normal fights 10–20, elites 35–45. Poverty (A3+) multiplies each endpoint by 0.75 and truncates before rolling, giving 7–15 and 26–33.

**Combat-gold exceptions:** Fake Merchant grants a fixed 300 gold, unaffected by Poverty. Battleworn Dummy's three event combats grant no normal combat rewards. Gremlin Merc grants normal gold unless Fat Gremlin escapes: the gold proportion becomes one-half if no gold was stolen, or zero otherwise; vanilla rounds the reduced range endpoints before rolling and generates no gold reward at zero. Non-final bosses grant fixed 100 gold (75 with Poverty); the final boss grants no ordinary reward set. Fixed extra payouts, including recovered stolen gold, Royalties and Amethyst Aubergine, are separate from the normal combat-gold roll.

**Relic draws:** rarity is 50% Common / 33% Uncommon / 17% Rare; fixed-rarity requests consume identity only, including in Catfood. Shops pull two random-rarity relics and one Shop relic from the back of the bags; rewards/Wongos pull from the front. Run-state eligibility (`IsAllowed`) removes disallowed relics before draws; five relics are additionally shop-ineligible.

**Identity depletion:** from the requested rarity, search forward along Shop → Common → Uncommon → Rare, then multiplayer fallback; otherwise Circlet. Refresh-enabled bags first refill empty requested rarities. Catfood inherits this shared-bag fallback.

**Ordinary relic eligibility:** the following 17 relics require `TotalFloor < 41` in single-player or `< 38` in multiplayer (`IsBeforeAct3TreasureChest`): Amethyst Aubergine, Book of Five Rings, Bowler Hat, Dragon Fruit, Frozen Egg, Girya, Juzu Bracelet, Lasting Candy, Lucky Fysh, Meal Ticket, Molten Egg, Old Coin, Planisphere, Shovel, Toxic Egg, White Beast Statue and White Star. The cutoff uses total floor, not whether a chest was visited.

**Potion drops:** one shared probability starts at 40%; a generated drop subtracts 10 percentage points, a miss adds 10. Elites add 12.5 percentage points to the check. No act/Ancient reset. Checks use shared `Rewards` RNG; forced drops skip both the random draw and probability update.

**Potion generation:** ordinary generation rolls 65% Common / 25% Uncommon / 10% Rare, then identity. Factory batches exclude duplicate identities within the batch; separate calls can repeat them. Generation precedes acquisition; failed acquisition does not restore consumed RNG state.

**Direct card additions:** Infested Automaton and Endless Conveyor's Fried Eel roll base rarity odds before identity: Common/Uncommon/Rare 61.51%/37%/1.49%, independent of combat rarity offsets. Unavailable rarities advance Common → Uncommon → Rare → Common. Arcane Scroll and random curse grants select uniformly from their eligible pools.

**Permanent transformation's target:** use the original card's pool, except Quest cards and Event/Ancient/Token-rarity cards use colorless. Token is the generated-card rarity used by Shiv, Soul and similar cards. Ordinary results are Common/Uncommon/Rare; curses share one curse pool. Preserve unlock/player restrictions and exclude the original identity. Selection is uniform over eligible identities, with no batch uniqueness. Combat transformations additionally filter by `CanBeGeneratedInCombat` and may use the Status pool.
