# Deterministic seed mod — design

This evolving specification records settled decisions, rationale and existing-mechanics references. [WORKING.md](WORKING.md) holds unresolved choices and supporting evidence. Unspecified systems retain existing behavior.

**Reading convention:** in the main body, unlabeled design statements are normative. Labeled baselines and references describe existing mechanics; deviation notes, rationale, tradeoffs, examples and rejected alternatives are explanatory. The Reference and Appendix sections are descriptive only.

## Goals and principles

**Goal:** make same-seed runs offer comparable choices and combat challenges across different routes and decisions. Compare corresponding ordinal opportunities—for example, each run's first shop or third normal-fight reward—even on different floors.

**Fairness principle — opportunity isolation:** preserve meaningful consequences while minimizing unrelated future gameplay changes. For example, room visits or reward choices should not shift unrelated later randomness.

**Behavior preservation:** all behavioral changes from vanilla, including probability changes, must be deliberate alignment tradeoffs, consistency fixes with no gameplay impact, or fixes to unintended vanilla behavior. Fixes to unintended behavior still receive impact grades; currently classified: [forced-event bypass](#settled-policy-forced-events).

**Same-seed divergence (歪种):** outcome differences from uncontrolled randomness that undermine same-seed consistency, often early or mid-game. Differences caused only by consuming different numbers of opportunities within intentionally shared sequences are benign, even when they produce late-game divergence.

**Batch-order neutrality:** selecting the same batch in a different click or enumeration order must not change which identities or statuses are affected or subsequent random state, unless the effect is explicitly order-dependent. This should preserve presentation order and gameplay effects.

### Vanilla deviations

Consistency fixes that only remove same-seed inconsistency, with no gameplay impact, are outside deviation grading.

**Unnoticeable:** imperceptible across ordinary runs, considering frequency and effect size. Roughly 500 runs is a soft calibration scale; the A7+ one-Rare cap is the reference example. Association with victory supplies supporting evidence.

**Neutral:** an informed player would make no meaningful change to strategy.

For gameplay changes, noticeability and strategic impact take precedence over expectation.

| Grade | Impact |
|---|---|
| **1** | Presentation only. |
| **2** | Preserves expectation but changes distribution or correlations; unnoticeable and neutral. |
| **3** | Changes expectation; unnoticeable and neutral. |
| **4A** | Noticeable and neutral. |
| **4B** | Unnoticeable and non-neutral. |
| **5** | Noticeable and non-neutral. |

**Strategy-change notation:** on 4B/5, **↓** means simpler meaningful decisions, **↑** more complex ones, and no direct arrow means altered tradeoffs. For SL, parenthesized arrows describe in-room exploration at any grade: `5(↓)` alters meaningful strategy while simplifying exploration.

**Play modes:** **NoSL** assumes no save/loading and covers in-room and upper-level strategy. **SL** explores every available action sequence from the current save before committing to the next saved state. Assess its best attainable room outcomes and upper-level decisions, or future-planning in-room decisions such as which potions to carry onward.

Grade each mode independently: write `NoSL x, SL y` when they differ, `NoSL x` when equal, and `SL x` when only SL is affected.

Rare deviations are appended with a **Frequency:** field.

## Scope

Version: StS2 v0.111.0 and Catfood Mod v1.1.2.

Compare single-player runs with matching seeds, versions and rules, with all content unlocked (including relic, card and event pools).

**Exclude:** multiplayer behavior, tutorial overrides (including scripted rewards and boss discovery order), and custom-run modifiers.

**Default context:** assume Scarcity (Ascension 7 and above) unless stated otherwise. Policies apply without Scarcity using the corresponding probabilities.

**Discussion boundary:** describe base random outcomes and preserve existing modifiers, including eggs, Fresnel Lens and fixed discounts. Discuss modifier interactions when they affect random operations.

**Fixed identities and selections:** predetermined card, relic and potion identities, player choices and fixed selections are outside scope, except for equivalent-copy routing. Fixed-rarity requests with random identities remain in scope.

## Concepts and terms

Two top-level categories classify random operations within effects:

- **Rewards:** generated reward identities and amounts, including permanent transformation results and otherwise-unspecified random rewards that are not event-exclusive.
- **Encounter:** random selection of affected cards/items, room content, named event options and combat randomness.

Relic Trader's outgoing selection and replacement targets are handled together under Encounter.

Category and RNG ownership are separate: a reward can use event RNG.

### Game context

- **Map type** (`MapPointType`): the map marker; `Unknown` (`?`) may resolve into different room types.
- **Room type** (`RoomType`): the resolved room—normal fight, elite, boss, chest, event, shop or rest site. Ancients are an Event room subtype counted separately in this design. An event may start combat without changing the map type.
- **Event ID / encounter ID:** identifies a specific event / combat encounter.
- **Normal fight / combat:** **normal fight** means `RoomType.Monster`; **combat** means any battle, including normal fights, elites, bosses and event battles.

### Reward terminology

- **Card reward offer:** cards presented together for one choice, usually three. **Card slot:** one position within an offer.
- **Card pool:** can be own character, colorless or a specified other character.
- **Direct card addition:** a card added without an identity choice or an opportunity to skip after seeing it.
- **Ordinary relics:** Common / Uncommon / Rare / Shop; Event-exclusive relics sit outside these pools.
- **Generation / acquisition:** generation determines an item; acquisition means receiving it.
- **Reward sources:** **Generic rewards** is an item-specific source class defined by each relevant policy. **Courier** means restocks from The Courier relic, distinct from the Potion Courier event.

### Randomness and addressing

- **RNG / random state:** an RNG (random number generator) supplies random values. Random state includes RNG state and relevant context, including sequence contents and positions, opportunity counters, and stored rolls or selection priorities.
- **Roll:** a random draw for a named purpose, such as rarity or upgrading.
- **Pool / sequence:** a pool defines candidate identities. A sequence is one ordered stream of entries; repeated identities are distinct entries. **Per-rarity identity sequences** provide one stream per rarity.
- **Allocation:** finite relic stock assigned to one source, ordered into one or more sequences.
- **Triple sequence:** a card identity sequence consumed only as complete consecutive groups of three entries; each group is a **triple**.
- **Filter:** the eligibility conditions for a draw.
- **Ordinal:** an opportunity's position within its category, counted across the run unless a scope is stated; scoped counts reset for each scope instance.
- **Ordinal-addressed generation:** generation derived from an opportunity's ordinal within its stated scope. **Shop-ordinal generation** names the shop case.
- **Event RNG:** an event's own generator, addressed by run seed + event ID in single-player, independently of event ordinals.
- **Logical occurrence / physical copy:** a logical occurrence is a persistent per-copy selection identity; a physical copy is the card or potion object currently associated with it.
- **Occurrence address:** the stable source identifier of a logical occurrence, distinct for each copy.
- **Selection key / selection-key alias:** a selection key identifies an occurrence for a selection purpose; an alias is a source-specified replacement key for that purpose.
- **Selection state:** an occurrence's address, selection keys and aliases, priorities or residual clocks, and renewal counts.

## Architecture overview

Each controlled random operation has five descriptive dimensions: **outcome** (what is randomized), **sharing scope** (which operations share randomness), **address** (identifying corresponding opportunities or entries), **form** (addressed draw, stream, finite allocation or another coupling mechanism), and **state** (what changes, resets and must survive save/load).

Different stages of one source—such as occurrence, rarity, identity and attributes—may use different sharing scopes, counters or sequences.

Consumption is a shared [invariant](#opportunity-consumption).

These tables summarize settled mechanisms. [Shared contracts](#shared-strategies) are omitted unless a mechanism differs.

### Rewards

| System / randomized outcome | Sharing and addressing | Form |
|---|---|---|
| [Relic rarity and identity](#allocation) | Generic rewards, shop and Courier are separate; rarity-roll and per-rarity identity sequences advance independently. | Rarity-roll streams and finite shuffled allocations. |
| [Chest gold](#rewards-and-consumption) | Non-empty chest ordinal. | Ordinal-addressed draw. |
| [Chest relic marker lifetime](#relic-advancement) | Rarity + non-empty chest ordinal. | Shared uniform fraction scaled by remaining generic and original shop/Courier shared stock. |
| [Potion generation and drops](#acquisition-and-sequences) | Generation uses four source groups; generic rarity modes share per-rarity identity sequences. Ordinary drop checks share a combat-wide check index and changing probability. | Rarity/identity streams and indexed drop checks. |
| [Shop stock, sale slot and base prices](#stock-and-prices) | Initial cards and sale use shop ordinal; restocks use source-specific sequences; prices use stable entry addresses. | Ordinal-addressed draws, item sequences and entry-derived price rolls. |
| [Combat gold](#settled-policy-combat-gold) | Separate normal-fight and elite gold-reward ordinals. | Independent ordinal-addressed draws. |
| [Own-character random-rarity offers: Rare occurrence](#luck-and-rarity-offset) | Luck and the threshold sequence are shared; the combat offset applies only to its source groups. | Exponential-threshold stream with pending delivery. |
| [Card-offer identities and rarity mixes](#settled-policy-card-reward-offers) | Separate sequences by pool/request/offer group, independent of shared Rare-occurrence state. | Triple sequences, other identity/rarity streams and event-RNG draws. |
| [Card upgrades](#inherent-card-rolls) | Each generated card occurrence has an independently addressed upgrade roll. | Entry-derived roll. |
| [Permanent transformation / direct-addition results](#selection-and-consumption) | Identity sequences are shared by pool; transformations and additions use separate rarity-roll sequences, with a source exception. | Identity and rarity-roll streams; event RNG for the exception. |
| [Event-specific rewards](#settled-policy-event-specific-rewards) | Event-local draws. | Event RNG outside shared reward sequences. |

### Encounter

| System / randomized outcome | Sharing and addressing | Form |
|---|---|---|
| [Wing Charm selection](#settled-policy-wing-charm) | Shared reward entries retain selection priorities; Candy uses its Power entry and supplementary event cards use event RNG. | Entry-derived priorities and weighted group selection. |
| [Persistent card selection](#corrected-persistent-selection-priorities) | Each logical card occurrence carries a clock within its selection scope. | Residual exponential clocks. |
| [Tooth and Bracelet selection](#fixed-priority-card-selection) | Separate purpose-specific selection keys with [reward aliases](#card-reward-selection-keys). | Fixed priority order. |
| [Slippery Bridge selection](#settled-policy-persistent-selection) | Bridge-only per-occurrence clocks and renewal counts with [reward aliases](#card-reward-selection-keys). | Residual clocks over vanilla candidates, including fallback. |
| [Owned-potion selection](#settled-policy-persistent-selection) | Ranwid and Stone of All Time share per-occurrence clocks and renewal counts. | Residual exponential clocks. |
| [The Future of Potions: card types](#settled-policy-the-future-of-potions-card-type-assignment) | Event-local type sequences by reward rarity; potion-keyed priorities assign their entries. | Independent type draws and fixed ordering priorities. |
| [Ranwid relic sacrifice](#settled-policy-ranwid-relic-sacrifice) | Event ID + relic identity, independent of Relic Trader | Opportunity-specific selection priorities |
| [Relic Trader replacement generation](#settled-policy-generation-routing) | Event ID + reward slot for rarity; Courier allocation for identities. | Independent rarity draws and Courier-tail pulls. |
| [Relic Trader outgoing assignment](#outgoing-relic-assignment) | Event ID + outgoing relic identity + fixed reward slot. | Pair-priority minima select the shortlist; the same priorities assign its trades. |
| [Endless Conveyor dishes](#settled-policy-endless-conveyor) | Event-local dish-selection sequence with a persistent position. | Weighted candidate stream with rejection of ineligible entries. |
| [Potion-generated cards](#settled-policy-potion-generated-cards) | Separate card-generation ordinals per potion type. | Ordinal-addressed batches. |
| [Unknown-room type](#addressing-and-room-type-chances) | Act-local visited-`?` ordinal, interpreted using the current stored room-type chances. | Ordinal-addressed draw through changing probability intervals. |
| [Ancient offerings](#settled-policy-ancient-offerings) | Ancient ID + option-pool ID + stable option identity for conditional pools; fixed pools retain vanilla. | Stable selection priorities, with vanilla weights. |
| [Ordinary-event identity](#settled-policy-ordinary-event-selection) | Act 1: act + condition key; Acts 2–3: act-local event ordinal + condition key. ID order carries across acts. | Fixed Act 1 schedule; refreshed Acts 2–3 priorities; sorted insertion. |
| [Encounter setup](#settled-policy-encounter-setup) | Ordinary combats use act-local combat-category ordinals plus encounter identity; event-created battles use event-local battle ordinals. | Purpose-separated addressed RNG. |
| [Monster HP](#settled-policy-monster-hp) | Per-monster HP opportunity; integer HP keys. | Opportunity-specific selection priorities. |

## Shared strategies

The [fairness principle](#goals-and-principles) and labeled invariants are shared constraints. All contracts below apply to covered policies; source policies add sharing scopes, local rules and exceptions.

### Addressing and state

#### RNG derivation and addressing

**Invariant — independent derivation:** new or re-addressed randomness derives from run seed + stable sharing-scope and purpose identifiers + the address components specified by its policy. The same full address reproduces the same draw or stream; independent purposes use separate derivations and state. Share randomness only where a policy specifies sharing or coupling. Explicitly retained baseline RNGs keep their stated addressing.

Run seed is implicit in local and candidate addresses for new or re-addressed randomness. In design address expressions, `+` means unambiguous composition of components, not arithmetic addition. Use stable identifiers rather than display labels; for streams, distinguish the stream's address from its current position. Concrete labels, encoding and RNG/hash algorithms can be chosen during implementation, subject to this contract.

In RNG addresses, a scoped ordinal denotes `(scope identity, count)`: act-local includes act number; event-local includes event ID.

**Selection keys:** derive initial keys from occurrence addresses, applying [source-specific aliases](#card-reward-selection-keys); never use current list position.

**Selection tie-breaks:** tie-break extremely rare equal random-selection priorities by comparing the candidate keys already used to generate them, in ascending order.

#### Stable entry addresses

**Invariant — stable addresses:** entries retain their original addresses through movement, filtered advancement, borrowing, removal and return.

**Invariant — presentation-only reordering:** change only display positions, keeping each entry's address, attached random state and group membership with it.

An entry's address is its original source sequence + original entry position, distinguishing occurrences of repeated identities.

#### Combat addresses

Ordinary combats use combat category + act-local combat ordinal + encounter identity, with separate normal-fight, elite and boss counters.

Event-created battles outside ordinary encounter lists use event-local battle ordinal + encounter identity, leaving ordinary combat counters unchanged.

Combat-local purposes derive from this address + purpose and reset each combat. Preserve settled scopes, including [potion-generated cards](#settled-policy-potion-generated-cards); use [global consumption](#opportunity-consumption).

#### Monster occurrence identity

Monster address = source-local spawn opportunity + creation index, stable for the monster's lifetime.

Initial enemies use this combat's encounter setup as their source; summons/replacements use the summoning/replaced occurrence.

One spawn opportunity covers the initial lineup or one summon/replacement trigger; number its enemies in vanilla creation order, regardless of animation timing.

#### Opportunity consumption

**Invariant — opportunity consumption:** an operation's random state, including selection clocks, stays unchanged until its random result becomes available for the player to reveal or receive. It advances at that point, regardless of actual viewing, acceptance or selection.

Deferred rewards consume at the payoff opportunity, not acquisition of the effect creating it.

**Exception:** [potion-generated cards](#settled-policy-potion-generated-cards) also consume opportunities on discard, trade and covered reward skips.

#### Saved state

Preserve [vanilla's run-save checkpoints](#run-save-checkpoints). A reload discards changes since the last checkpoint. Restore mod state before room regeneration so replay advances from the same saved state.

At each checkpoint, save sequence contents and positions, including original entry addresses, consumption, marker positions, conditions, remaining lifetimes and carried entries, plus opportunity counters and RNG state. Also preserve chest-offer history and pending acquisition/skip decisions, selection state, logical-to-physical occurrence associations and pending fixed-cohort selections wherever used.

### Generation

#### Rarity and identity sequences

**Main idea:** share rarity rolls and identity order across related rewards, with independent sharing scopes and sequence state.

**Sharing rationale:** generic rewards represent successive opportunities for the same kind of item, regardless of route. Shop isolation preserves corresponding inventories; isolating Courier and potion-generating effects prevents repeated use from displacing ordinary rewards. Transformations and direct additions share identity order because both generate permanent cards from a pool; separate rarity rolls preserve their different odds.

**Scope:** relics, potions, cards, permanent transformations and covered direct additions.

With per-rarity identity sequences, a random-rarity request consumes one value from its **rarity-roll sequence**, then draws an eligible item from that rarity's **identity sequence** using the [filtered draw strategy](#filtered-draws). Fixed-rarity requests consume identity only; pools with one identity sequence draw directly. [Generation routing](#settled-policy-generation-routing) specifies sharing and RNG exceptions.

**Generation and lifetime:** covered potion/card identity and rarity-roll sequences generate on demand indefinitely, using independent RNGs for the specified source and pool divisions. Each newly generated potion/card identity entry is an independent uniform draw with replacement from its sequence's pool unless the source specifies otherwise, such as weighted or grouped generation. Only finite relic allocations borrow.

#### Filtered draws

**Main idea:** take the next eligible item without pulling skipped items toward the front of later draws.

**Advancement:** generalize fixed-rarity draws. Viewing per-rarity identity sequences and rarity rolls together as a mixed sequence, later entries of that rarity fill earlier positions of the same rarity. For any filter, if eligible entries `A`, `B`, `C`, … occupy positions 2, 6, 8, …, return `A`, advance `B` to 2, `C` to 6, and so on. Ineligible positions remain unchanged, preserving the sequence's shape.

**Method — deferred advancement:** a filtered draw returns the first eligible entry, replacing it with a marker for its filter. Resolve a marker by moving the next eligible entry after it into the marker's position, leaving the same filter's marker at the supplying position. An ordinary draw resolves any front marker, then returns the entry and removes the drawn position. This performs the same advancement on demand for infinite sequences and finite relic allocations.

**Marker collisions:** when a draw using filter `Y` encounters a marker for filter `X`, resolve that marker first.

- If the resolved entry fails `Y`, retain it at the marker's former position and continue searching for `Y`.
- If it satisfies `Y`, return it and place a marker for `Y` there. The marker for `X` remains at the supplying position, representing an advancement of `X` followed by an advancement of `Y`.

Apply the same rule recursively to further collisions, including identical filters: two advancements require two markers.

Markers freeze eligibility conditions at the original draw, including batch exclusions and state-dependent restrictions; the current draw still enforces current eligibility.

Ordinary markers are permanent; chests use the [finite-lifetime extension](#finite-lifetime-chest-markers).

Apply the filtered draw strategy to all covered relic, potion, card, transformation and direct-addition identity sequences, including shop-batch duplicate filtering, without additional rarity rolls, except for [Wongos's identity removal](#stock-and-prices). Preserve existing source eligibility and batch uniqueness; skipped entries remain available to eligible draws and borrowing.

Triple sequences reject duplicate candidates while generating each triple. Other card sequences use filtered draws to exclude duplicates.

#### Inherent card rolls

**Main idea:** bind random attributes to each generated card occurrence so evaluating them cannot shift later identities or attributes.

Each card entry carries an **inherent upgrade roll** `u` uniformly distributed in `[0, 1)`, derived independently from its [stable entry address](#stable-entry-addresses) and the upgrade purpose.

When generating an offer, upgrade an eligible card exactly when `u < p`, using that source's rarity- and act-dependent upgrade chance. Preserve sources that suppress random upgrades or explicitly guarantee an upgrade. Neither changes the inherent roll or advances another reward's random state. This does not upgrade cards already in the deck when the act changes.

Baseline upgrade chances: [card rarity and upgrades](#card-reward-offers).

### Selection

#### Logical-occurrence lifetime

**Scope:** settled card and owned-potion selection policies. Each card or acquired potion copy has a distinct logical occurrence; new cards use [card occurrence initialization](#card-occurrence-initialization). Apply the action's [selection and renewal updates](#settled-policy-persistent-selection) alongside these lifetime rules.

| Operation | Occurrence and selection state |
|---|---|
| Acquire a generated card | Preserve its address and source-defined selection keys, including [reward aliases](#card-reward-selection-keys), regardless of which other offered cards are taken. |
| Move, temporarily store, or recreate the physical object for an existing occurrence | Retain the logical occurrence and its selection state; see [Tooth's return-record rule](#fixed-priority-card-selection). |
| Change status or eligibility | Retain the occurrence and unused clocks through equivalence-class splits and merges; eligibility governs selection participation. [Upgrade/downgrade refills](#permanent-upgrades-and-downgrades) remain source-specific. |
| Copy a card | Create a fresh occurrence from the granting source. Leave the original's selection state unchanged and consume no routing clocks. |
| Permanently remove or combine cards; player-chosen [potion use, discard or trade](#potion-departure-routing) | Retire the selected logical inputs. |
| Permanently transform a card | Retire the original. Apply [source-specific gameplay inheritance](#permanent-transformations-and-direct-additions), then initialize fresh replacement selection state. |
| [Route equivalent copies](#representative-routing) | Permute associations with physical copies while keeping each occurrence's selection state together. |

**Related reward-entry exception:** [Wing Charm's A0–6 replacement priorities](#settled-policy-wing-charm).

#### Equivalent-card selections

**Invariant — equivalent copies:** for every card identity, choosing any physical member of a gameplay-equivalent class has the same subsequent identity/status-level consequences. Hidden selection state remains equivalent up to association with physical copies. Equivalence requires the same identity and gameplay-relevant status; selection state and presentation position do not distinguish the class.

**Within-run equivalence key:** in the same owner and eligible deck context, explicitly compare model ID, exact upgrade level, enchantment ID/amount, and applicable persistent progress: Genetic Algorithm's `IncreasedBlock`, The Scythe's `IncreasedDamage`, Dowsing's `RoomsEntered`, and Guilty's `CombatsSeen`. Omit redundant current-value fields, change history, acquisition metadata, Spoils Map's additional fields and Mad Science's type/rider configuration within the supported scope. Baseline: [persistent card state](#card-state-and-selection).

All out-of-combat player-selected sources share [routing clocks](#representative-routing) by operation, separately for each card identity:

| Routing scope | Operations |
|---|---|
| Upgrade | Player-chosen upgrades, including Pael's Tooth's initial selection. |
| Enchant | All player-chosen enchantments, across enchantment types and amounts. |
| Permanent removal | Removal, transformation and Amalgamator's combining; sharing aligns surviving originals after [input retirement](#logical-occurrence-lifetime). |

#### Deterministic Card Processing Order

**Intent:** give unordered card batches a common usefulness-based processing order for [new occurrence addresses](#card-occurrence-initialization), permanent transformations and planned combat shuffling.

**Status:** preliminarily settled — ready for use, with fine-tuning remaining. The [detailed ranking and state comparisons](CARD_ORDER.md) form part of this specification.

Use a fixed, authored usefulness ranking of card identities and their gameplay-relevant statuses. Identify cards by stable model IDs.

Identical cards remain tied regardless of selection state, acquisition order or physical position; retain every copy. Tied-copy permutations must leave gameplay outcomes and subsequent random state equivalent under the [equivalent-copy contract](#equivalent-card-selections). This order does not replace per-copy randomness or its routing.

Source policies specify applicable parts of this order. It controls random-operation processing, not display order or intentionally sequential effects; reward offers retain their existing entry order as canonical generation order.

Shuffle outcomes must be independent of original deck sequence; the [shuffle addressing and advancement policy remains unresolved](WORKING.md#combat-randomness).

#### Card occurrence initialization

Each new logical card occurrence gets one stable address from its source and selection keys under the [shared addressing contract](#rng-derivation-and-addressing). Initialization preserves generated attributes and presentation order.

Cards with generation entries use their [stable entry addresses](#stable-entry-addresses).

For sources without generation entries, use source opportunity address + copy index. Assign indices in the [Deterministic Card Processing Order](#deterministic-card-processing-order) after establishing gameplay properties, with a distinct index for each equivalent copy. Treat the starting deck as one initial source opportunity.

#### Card reward selection keys

When a card reward offer allows at most one card, the acquired card uses the first card's selection key as an alias for later selection by Pael's Tooth, Beautiful Bracelet and Slippery Bridge. Freeze the anchor in canonical offer generation order, before presentation-only swaps.

Different offers, including [rerolls](#rerolls), have distinct anchors. Offers allowing multiple cards, including bundles, keep each acquired card's own selection keys even when only one is taken. Selecting existing deck cards creates no reward aliases.

#### Corrected persistent selection priorities

**Main idea:** for uniform occurrence selection, retain purpose-specific priorities across opportunities while correcting the survivors after each random selection. Use residual exponential clocks, not fresh priorities for every opportunity. [Source settings](#settled-policy-persistent-selection) specify strategy, sharing and local clock rules.

**Address and state:** each logical occurrence starts with a clock `e_i = -ln(U_i)`, with independent `U_i ∈ (0, 1)` derived from its selection key + selection scope/purpose.

Where renewal uses a per-occurrence count, derive each independent draw from selection key + selection scope/purpose + renewal count.

**Selection and update:** take the eligible occurrence with the smallest clock, `m`. On committing the selection, subtract `m` from every unselected eligible survivor. Ineligible occurrences retain their clocks without subtraction or renewal. A selected occurrence consumes its clock. Source policies define when and how to renew consumed clocks from the same distribution.

**Batches:** for an unordered uniform sample of `k` distinct occurrences from a fixed eligible set, select the `k` smallest pre-selection clocks and subtract the `k`-th smallest clock from every unselected eligible survivor once. Consume each selected clock once; a selected occurrence cannot re-enter the same batch. Commit from one eligibility snapshot, independently of UI click/enumeration order. Effects with intentionally sequential eligibility changes retain that distinction.

**Fixed-cohort shortcut:** if all members remain eligible until selected and each leaves permanently, sort their initial clocks once. Consume that order without subtraction or renewal.

A player-directed choice does not advance a gameplay selector's clocks merely because it changes membership; any hidden equivalent-copy routing has its own scope. Fixed-target and explicit affect-all effects also consume no selection clocks.

**Rationale and limit:** the exponential memoryless property preserves uniform selection probabilities while retaining the relative order of jointly participating survivors. This does not preserve one global ranking across different eligibility histories. For example, let `A`, `B`, `C` have clocks `0.5`, `0.6`, `0.7`: if only `A` and `C` participate and `A` is selected, the surviving `C` now outranks the inactive `B`.

#### Representative routing

**Scope:** out-of-combat [player-selected cards](#equivalent-card-selections) and [player-chosen potion departures](#potion-departure-routing), including combat departures. These policies define equivalence and operation sharing.

Choose representatives within each equivalence class using the [corrected persistent-clock strategy](#corrected-persistent-selection-priorities). An already randomly selected logical target bypasses representative routing.

**Presentation boundary:** apply effects to the clicked copies; preserve display order.

**Batch association:** use the shared clock strategy's eligibility snapshot and freeze presentation order with it. Within each equivalence class, pair selected occurrences in ascending pre-selection routing-clock order with clicked copies in frozen presentation order; pair remaining occurrences in stable occurrence-address order with unclicked copies in that order. Apply effects after all representatives are selected.

Commit the association permutation and shared clock update with the action. Renew each selected routing clock once if its logical occurrence survives, using its per-occurrence renewal count. Player-selected singleton and whole-class choices follow this same rule.

**Coupling limit:** shared independent priorities on matched nested occurrence cohorts give two-versus-three representative agreement of `2/3`, maximal for uniform selection. Different selection keys or eligibility histories can reduce agreement.

#### Opportunity-specific selection priorities

**Main idea:** for sources adopting uniform selection without replacement, give each eligible candidate an independent priority and select the `k` candidates with lowest priorities. Removing an unselected candidate cannot change the selected set.

Derive priorities from the source's selection-opportunity address + candidate's stable key + selection purpose, excluding current candidate collection and list positions. Each opportunity uses fresh priorities; sources define sharing scopes and opportunity counting. Unique owned relics use relic identity as candidate key.

This opt-in mechanism applies to sources such as The Future of Potions. It does not override the corrected persistent strategy for permanent-card selection or settle its source/effect sharing.

## Settled policy: generation routing

**Routing** specifies the random state supplying each generation stage. The table separates rarity and identity sharing for the covered requests.

Supplies are independent except where sharing is stated. Apply the [shared generation strategies](#generation); source policies retain eligibility, construction and consumption details.

For card reward offers, group identities by pool, rarity request, eligibility filter and draw grouping. Upgrade rules and the number of cards taken leave grouping unchanged. The expanded-pool row modifies the applicable offer route.

For own-character random-rarity offers, luck controls Rare delivery; triple sequences supply the base Common/Uncommon composition.

| Request / source | Rarity state or rule | Identity supply | Grouping / exceptions |
|---|---|---|---|
| Ordinary relics: generic rewards, shop and Courier | One rarity-roll sequence per source. | Disjoint finite [allocations](#allocation): independently advancing, shuffled Common/Uncommon/Rare sequences per source; shop also has all Shop-rarity identities in one shuffled sequence. | Individual draws; [source classification](#source-classification-and-consumption), [chest advancement](#relic-advancement) and [Wongos](#stock-and-prices) apply. |
| Relic Trader replacements | Ordinary relic rarity, addressed by event ID + reward slot. | Last eligible Courier entry of the rolled rarity. | Slots 1–3 in generation order; no added filters. |
| Generic potion rewards | Separate sequences for [standard weights](#potions) and uniform-identity selection; uniform mode weights rarities by eligible identity counts. | Both modes share generic Common/Uncommon/Rare identity sequences over the full eligible pool. | Individual potions. |
| Potions: shop / Courier / potion-generating effects | One standard-weight rarity sequence per source. | Separate Common/Uncommon/Rare identity sequences per source, each using the full eligible potion pool. | Individual potions; [source membership](#acquisition-and-sequences). |
| Initial shop cards | Shop-ordinal generation using [shop odds](#stock-and-prices). | Shop-ordinal generation. | — |
| Courier cards | Separate Attack/Skill/Power rarity sequences using shop odds; colorless requests fixed Uncommon or Rare. | Per-type, per-rarity sequences for Attack/Skill/Power; one sequence each for Uncommon and Rare colorless. | Individual restocks. |
| Own-character random-rarity offers: normal-fight rule | Shared [luck, thresholds and combat offset](#luck-and-rarity-offset); normal-fight Rare chance. | Shared [non-Rare and rare-bearing triples](#offer-sequences), plus [A0–6 extra Rares](#a06-multiple-rares), across all three random-rarity groups. | Normal fights, Dream Catcher, Prayer Wheel and The Hunt in normal fights: three-card offers. |
| Own-character random-rarity offers: elite rule | Same luck, thresholds and combat offset; elite Rare chance. | Same supplies. | Elites and The Hunt in elite combats: three-card offers. |
| Own-character random-rarity offers: fixed base odds | Same luck and thresholds; base Rare odds without the combat offset. | Same supplies. | Orrery: five three-card offers; Lost Coffer: one; Trial's Nondescript Guilty: two. Brain Leech's Share Knowledge: choose one of five, using [supplementary cards](#supplementary-cards). |
| Own-character fixed-Rare offers | Fixed Rare. | Separate Rare triple sequence. | One triple per offer: non-final bosses, White Star, The Hunt's delivered boss rewards, Hefty Tablet, Glass Eye's Rare offer and Crystal Sphere's Rare offer. |
| Own-character fixed-Common offers | Fixed Common. | Separate Common triple sequence. | Glass Eye: two triples; Crystal Sphere: one. [Room Full of Cheese](#supplementary-cards) and [Scroll Boxes](#scroll-boxes) have local rules. |
| Own-character fixed-Uncommon offers | Fixed Uncommon. | Separate Uncommon triple sequence. | Glass Eye: two triples; Crystal Sphere: one. |
| Own-character Power: Lasting Candy / The Future of Potions | Candy: separate normal-fight and elite rarity sequences using source base odds and unavailable-rarity fallback, one roll per triggered card; boss triggers request Rare. Future of Potions requests its specified rarity. | Shared Uncommon and Rare Power identity sequences. | Individual entries. |
| The Future of Potions: Attack / Skill offers | Specified rarity. | Event RNG addressed by rarity and type, independently of the other potion options. | — |
| Colorless offers: Lead Paperweight / Brain Leech's Rip | Existing Uncommon/Rare odds: **98.51% / 1.49%**. | One shared reward sequence. | Individual entries across batch sizes. |
| Other-character offers | Kaleidoscope: event RNG for character and rarity. Colorful Philosophers / Sea Glass: fixed rarities. | **12 shared identity sequences: four other characters × Common/Uncommon/Rare**. | Individual entries across batch sizes. |
| Prismatic Gem / Dingy Rug: eligible expanded-pool offers | Retain the source's rarity state and rules. | Independent sequences for the effective expanded pool; reuse the existing group when the complete request's eligible pool is unchanged. | Preserve source grouping; apply to whole requests. Candy uses the inherited pool's Power sequences. [Expansion rules](#prismatic-gem-and-dingy-rug). |
| Permanent transformations / [covered direct additions](#settled-policy-permanent-transformations-and-direct-card-additions): own-character and colorless pools | Separate transformation/addition rarity sequences per pool. Transformations weight by current eligible identity counts per rarity, excluding the original; additions use vanilla base odds and unavailable-rarity fallback. | Shared Common/Uncommon/Rare identity sequences per pool. | Individual entries; separate from combat transformations. |
| Infested Automaton: Study / Touch Core | Event RNG; leaves the addition rarity sequence unchanged. | Same permanent own-character identity sequences as the preceding row. | — |
| Permanent transformations / [covered direct additions](#settled-policy-permanent-transformations-and-direct-card-additions): other-character and curse pools | Direct identity draws. | One sequence per pool: four other characters and curse. | Individual entries; separate from combat transformations. |
| [Event-specific rewards](#settled-policy-event-specific-rewards) | Event RNG where rarity is random. | Event RNG. | — |
| [Potion-generated cards](#settled-policy-potion-generated-cards) | Uniform eligible identities, without a separate rarity roll. | Complete batch addressed by potion type + that type's card-generation ordinal. | Separate from other combat card generation. |

## Settled policy: relics

**Intent:** isolate normal-play relic supplies for shops, Courier and generic rewards; remove Catfood's fixed rarity-roll limit for random relic rewards.

### Allocation

Construct the three [routed allocations](#settled-policy-generation-routing) once. Split each Common/Uncommon/Rare pool **45% / 35% / 20%** between generic rewards, shop and Courier respectively.

Capacity is per rarity/source. Exact fully unlocked allocations (generic rewards / shop / Courier): **Common 12 / 9 / 5; Uncommon 14 / 11 / 7; Rare 17 / 13 / 8**.

Mix shop-ineligible relics into allocations normally.

**Generic opportunity coverage:** advancing ordinary generic reward opportunities must eventually expose every still-eligible entry, subject to the stated exhaustion/borrowing boundary. An outside multi-choice offer must not retire unchosen generic entries before those opportunities.

### Source classification and consumption

Hidden random relic rewards from combat, chests, events and rest sites, including relic-triggered grants in any room, use the generic rewards source, with source-specific eligibility, regardless of cost (including Ranwid and Mystery Box), subject to explicit source exceptions. Classify deferred payoffs separately from their granting relic.

### Borrowing

**Scope boundary:** borrowing covers source exhaustion. Entire-rarity exhaustion is outside discussion scope; fallback and chest refilling are implementation details. Ordinary isolation analysis excludes borrowing's rare tail effects and cross-sequence interactions.

On source exhaustion, borrow the **last eligible remaining identity** of the requested rarity from another sequence. Priority: Courier sequence borrows from generic rewards first; Others borrow from Courier first.

Marker resolution follows the same borrowing and marker placement rules as a filtered draw.

**Implementation details — chest refilling:** when same-rarity borrowing fails, retain the normal generic rarity result and select uniformly among eligible identities of that rarity in the original shared relic pool, using one additional generic relic rarity RNG draw. Reusing this RNG advances later generic rarity rolls, accepted only in this out-of-scope case. Without a refill candidate, use the final fallback.

**Implementation details — final fallback:** starting at the requested rarity, try local entries and same-rarity borrowing before advancing through Shop → Common → Uncommon → Rare, without wrapping or another rarity roll; otherwise return Circlet.

Baseline pool sizes and draw rules: [relic bags](#relic-bags); Catfood's roll limit: [Catfood addressing](#catfood-addressing-and-combat-rewards).

## Settled policy: chests

**Intent:** isolate chest gold from normal-fight counts and later chest rewards from Silver Crucible's empty chest.

**Rejected alternative:** a separate chest allocation adds unfairness between reward sources.

### Rewards and consumption

- Silver Crucible suppresses the first chest's rewards without advancing chest ordinal; the next chest is ordinal 1.
- Base gold uses ordinal-addressed generation with the non-empty chest ordinal, independently of combat gold.

Relic generation consumes the rarity roll, chest opportunity and lifetime draw, and exhausts the identity for future chests.

### Relic advancement

**Intent:** preserve skipped relics for ordinary rewards, align common future identities, and spread acquisition's character-specific probability increase across later positions.

Draw the next eligible shared identity from the generic rarity sequence and replace it with a [finite-lifetime marker](#finite-lifetime-chest-markers). Both choices advance along the same path:

- **Acquire:** retire the relic; the marker disappears at expiry, compacting survivors.
- **Skip or leave behind:** save the relic with the marker as its **carried entry**; at expiry it returns as an [ordinary reward entry](#opportunity-consumption).

Resolve the choice before expiry, including `x = 0`; permanent ineligibility or acquisition elsewhere cancels a skipped relic's return.

#### Finite-lifetime chest markers

Use the [shared filtered-draw advancement and collision rules](#filtered-draws), with expiry after `x` eligible-entry advances. Each transfer into the vacancy decrements `x` and moves the marker to the supplying position. Initial `x = 0` applies expiry at the drawn position. Collisions preserve each marker's lifetime and carried entry.

**Lifetime:** use `x = floor(u * S)`, with uniform `u` in `[0, 1)` addressed by rarity + non-empty chest ordinal + marker-lifetime purpose. Corresponding chests share `u`; successive chests draw independently.

Before recording the offer, `S` counts chest-eligible shared identities of that rarity, excluding previous chest offers, in:

- The remaining generic sequence, including the drawn entry.
- The **original shop and Courier allocations**, retaining identities after display or acquisition.

Apply current run-state eligibility throughout; shop eligibility adds no restriction. Character-specific identities and markers contribute zero. The original allocations act as a retained tail, reflecting vanilla's back-drawing shops and keeping shop-stock consumption outside lifetime generation. Advancement uses generic stock; borrowing during selection or resolution uses a permanent marker under the [borrowing boundary](#borrowing).

**Probability:** within sufficient supply and the [uniform-order assumptions](#relic-bags), acquisition models deletion of a uniform chest-eligible position in the combined generic order and retained tail. Skipping rotates eligible identities through their positions, preserving the character-specific position pattern.

**Alignment:** acquire and skip share the future sequence up to the return position; skip adds one ordinary opportunity while preserving common-entry order. Across runs, reducing `S` by one changes `x` by zero or one; starting one shared entry later in the same order and reducing `x` by one gives the same expiry relic. Relocation is a consistency fix with no gameplay impact under the [deviation convention](#vanilla-deviations).

Baseline: [chest mechanics](#shops-chests-and-relic-events) and [Catfood addressing](#catfood-addressing-and-combat-rewards).

## Settled policy: potions

**Intent:** isolate potions across rewards, shops, Courier and potion-generating effects; preserve comparable results across batch sizes. Failed direct grants leave later potions unchanged. White Beast Statue's forced drops leave the ordinary drop-check state unchanged for use after the relic's removal.

### Acquisition and sequences

Use [generation routing](#settled-policy-generation-routing) for four sources. **Generic rewards** combines random combat rewards, events and other grants without paid/free or per-event separation; **shop** and **Courier** are separate. **Potion-generating effects** covers Alchemize, Entropic Brew and Delicate Frond, including use outside combat.

Preserve existing failure feedback, including Alchemize's failure animation. Batch boundaries affect duplicate filtering only.

**Example — batch boundaries:** Catfood's call-based seeding can turn `A | BCD | E` for requests `1–3–1` into `AFG | B` for `3–1`. Per-potion consumption instead follows `ABC | D`, subject to batch duplicate filtering.

**Vanilla deviation — SL 5(↓):** failed grants no longer let players advance to a different later potion outcome.

### Drop occurrence

- Retain [Catfood addressing](#catfood-addressing-and-combat-rewards) and [vanilla's shared changing probability](#potions) across normal fights, elites and bosses. Extra ordinary checks advance the cumulative index; combat-category threshold differences are preserved. No act/Ancient reset.
- **White Beast Statue:** forced drops freeze the occurrence index and stored probability; the potion still consumes a reward identity.
- Save the occurrence index and probability.

## Settled policy: shops, Wongos and Courier

**Intent:** Extra rewards and Courier restocks no longer scramble later shops. Corresponding shops keep the same sale slot; relocated offers keep their base prices. Wongos affects only future Rare shop offers, and only when its featured relic is affordable.

### Stock and prices

- Initial shop stock depends only on shop ordinal, except for Wongos, relic floor cutoffs and [borrowing](#borrowing).
- **Initial cards:** follow [generation routing](#settled-policy-generation-routing); preserve slot order, duplicate exclusion and unavailable-rarity fallback.
- **Card rarity:** vanilla shop base chances plus an invariant offset of `0` at A7+ and `+0.018` at A0–6. The [three-act calibration](#rarity-calibration) approximates vanilla's mean Rare count on ordinary routes.
- Sale slot uses independent shop-ordinal generation.
- **Base prices:** use [stable entry address](#stable-entry-addresses) for initial stock and Courier restocks. Initial shop cards use shop ordinal + slot as their entry address.
- **Courier cards:** follow generation routing; preserve unavailable-rarity fallback, exclude cards still displayed, and retain [original mechanics](#shops-chests-and-relic-events).
- **Wongos:** featured offer consumes **one shop Rare identity**; below 200 gold, suppress the offer without consuming stock as a [consistency fix](#vanilla-deviations). Treat the featured offer as a shop display for [chest lifetime counts](#relic-advancement). Other options use [generic relic rewards](#source-classification-and-consumption).

**Wongos identity removal:** for the featured Rare and Common purchase, resolve encountered markers normally, then remove the first shop-eligible entry from the designated source without placing a new marker. This preserves vanilla's local promotion of skipped identities toward later unrestricted draws; developer intent is unknown. See the [filtered-pull example](#relic-bags).

**Vanilla deviation — NoSL 3:** Wongos's featured Rare uses shop stock, removing its vanilla advancement of shop-ineligible Rares toward later generic rewards.

**Vanilla deviation — NoSL 4B↓:** fixed shop rarity offsets remove recent combat rarities' influence on shop Rare odds and change Rare clustering.

**Vanilla deviation — SL 4A(↓):** independent restock streams remove restock manipulation through interleaved purchase categories and effects.

**Vanilla deviation — SL 4A(↓):** entry-bound prices remove price manipulation through unrelated purchase order.

**Boundary — relic floor cutoffs (see [appendix](#relic-bags)):** reaching corresponding shops on opposite sides of a cutoff can change their relics and shift later stock; these differences are inevitable and benign.

Baseline: [shop mechanics](#shop-and-courier-rng) and [Catfood addressing](#catfood-addressing-and-combat-rewards).

## Settled policy: combat gold

**Intent:** align corresponding normal-fight and elite gold rewards independently, preserving vanilla distributions and isolating them from other rewards.

**Generation and addressing:** derive each draw from gold purpose + combat category + that category's gold-reward ordinal. Normal fights and elites use separate ordinals; event battles use their encounter's `RoomType`. Draw uniformly from the existing inclusive gold range, preserving endpoint adjustments and payout modifiers.

Fixed or absent combat gold, fixed extra payouts and event-choice gold consume neither ordinal. Gremlin Merc's half-gold reward advances the normal-fight ordinal using its reduced range; zero gold advances neither.

**Rationale and tradeoff:** an extra elite leaves later normal-fight gold unchanged, and vice versa. Reordering unreduced rewards with matching category counts preserves individual base payouts and totals under unchanged payout modifiers, including Bowler Hat's per-payout truncation. Replacing one unreduced normal reward with an elite reward can change total base gold by 11–26 at A3+, rather than a nearly fixed premium.

### Rejected alternatives

- **Shared rolls with independent compensation:** share ordinal for normal-fight and elite rewards. A3+ normal gold preserves expectation but becomes nonuniform (**Vanilla deviation — NoSL 2**); Bowler Hat can change the received total by 1. [Derivation](#rejected-combat-gold-coupling).
- **Shared range-relative rolls:** preserves vanilla distributions, but at A3+ swapping normal → elite to elite → normal can change total gold by 1.

Baseline: [gold ranges and exceptions](#combat-gold) and [Catfood addressing](#catfood-addressing-and-combat-rewards).

## Settled policy: card reward offers

**Intent:** equivalent rewards share identity order across sources. Corresponding rare-bearing offers share the same choices across sources; Catfood separates normal fights and elites. Larger own-character drafts preserve later three-card offer boundaries. Random upgrades leave later card choices unchanged; matching Lasting Candy triggers retain their rarity.

**Scope:** initial generation and reachable rerolls for the offer groups in [generation routing](#settled-policy-generation-routing). Preserve source eligibility, offer sizes, selection rules and upgrade behavior.

**Crystal Sphere:** does not reset the combat rarity offset.

Colorless and other-character rewards share across batch sizes because their few sources have varying offer sizes.

### Rare occurrence

**Scope:** the three own-character random-rarity groups in [generation routing](#settled-policy-generation-routing).

**Intent:** separate when a Rare appears from the complete choice offered. Align corresponding rare-bearing and non-Rare opportunities while preserving source-dependent Rare chances; matching combat-room ordinals need not consume matching offer entries.

#### Luck and rarity offset

Maintain luck `L`, initially zero, and a threshold `T = -ln(U)`, where `U` is uniform on `(0, 1)`. Thresholds use an independent sequence addressed by purpose and threshold ordinal. Advance it only when a Rare is delivered into a generated offer.

Process card slots in order. Each slot adds `-ln(1 - p)` to luck, where `p` is its Rare probability. Normal-fight-rule and elite slots use `max(0, base Rare chance + o)` with one shared combat offset `o`; fixed-base-odds slots use their base Rare chance without reading or modifying `o`.

The combat offset starts at `-0.05`. Normal-fight-rule and elite non-Rare slots add `0.005` (`0.01` without Scarcity), capped at `0.40`; a delivered Rare in either group resets it to `-0.05`. Zero-probability slots add no luck but still grow the offset. Retain boss-reward offset resets, without advancing luck or these offer sequences.

Subject to the difficulty-specific cap below, when a slot reaches the threshold, resolve that slot as Rare, reset luck to zero and advance to the next threshold. Discard the overshoot. Common/Uncommon identities and their rarity mix come from the offer sequences, independently of this simulation.

Luck, the current threshold and sequence state carry across acts; the combat offset resets to `-0.05` between acts. Save this state under the [shared saved-state contract](#saved-state), including an already-reached threshold awaiting delivery.

**Rationale:** before deferral, `P(no Rare through n slots) = exp(-sum(-ln(1 - p_i))) = product(1 - p_i)`. Exponential thresholds preserve the supplied waiting probabilities without fitting a route-dependent luck distribution. This does not make the capped, deferred reward system identical to vanilla.

**Competition effect — luck coupling:** with matched luck, threshold, offset and three-card offers, an elite leaves more luck than a normal fight if neither delivers a Rare. The lead survives boss rewards and act transitions; with equal later increments, the elite branch reaches the shared threshold no later. Rare delivery resets luck and advances the threshold, ending this timing guarantee.

#### A7+: one Rare per offer and deferral

Cap each covered offer at one Rare. If a later slot reaches the next threshold, suppress its Rare and keep the reached threshold pending: do not reset luck or advance the threshold. Treat suppressed slots as non-Rare for applicable offset updates. Continue accumulating luck, but create no further pending occurrences until delivery.

A **Rare-eligible offer** has a positive chance of producing a Rare along its ordinary all-non-Rare progression, ignoring pending delivery. At the next such offer, deliver the pending Rare in its first simulated slot instead of performing that slot's ordinary check; reset luck, clear pending and advance the threshold once, applying the delivery source's offset rule. Process its remaining slots normally. Ineligible offers leave the pending threshold intact and apply normal non-Rare offset growth.

This naturally excludes the first two three-card normal-rule offers after an offset reset without elite intervention; use probability eligibility rather than room counts. Delivery follows offer generation order, including extra offers and rerolls, and can wait across acts.

**Vanilla deviation — NoSL 3:** extra Rares in elite and fixed-odds offers are deferred, changing acquisition opportunities; occurrences beyond the single pending threshold are omitted. **Frequency:** rare — approximately **0.3% of vanilla A7+ runs** contain a dual-Rare offer from these sources (project-owner estimate).

#### A0–6: Rare delivery

Deliver every threshold crossing immediately under the shared luck and source-offset rules, without a per-offer cap or deferral.

### Offer construction

#### Offer sequences

Own-character random-rarity offers use two deterministic triple sequences independent of the luck thresholds. The **non-Rare sequence** supplies three Common/Uncommon cards per triple; the **rare-bearing sequence** supplies one Rare and two non-Rares. Store the Rare first in each rare-bearing triple.

A generated offer consumes the next non-Rare triple or, if a Rare is delivered, the next rare-bearing triple. A suppressed occurrence reserves no additional triple.

**Uncommon probabilities:** use **38% ordinary / 39% rare-bearing companions** at A7+, and **40% / 40.5%** at A0–6, except for the opening entries below. These rounded rates approximate expected displayed Uncommon totals in the [three-act calibration](#rarity-calibration). Shop offsets do not apply to these probabilities.

**Vanilla deviation — NoSL 3:** shared Uncommon rates remove elites' advantage (about 0.14 extra Uncommons per three-card non-Rare offer near zero starting offset) and change source- and route-specific rates.

**Rationale:** sharing preserves both triples when a Rare moves between sources; separate normal/elite sequences would advance differently. Few elite rewards, some Rare-bearing, limit the impact; elites retain relic, gold and higher Rare-chance incentives.

The first two non-Rare triples use their own deterministic generation with vanilla first-two-normal-offer probabilities after an offset reset, conditional on no Rare. At A0–6 these are **35%, 36%, 37%**, then **37/99, 37/98, 37/97**: conditioning matters because the second normal offer can contain a Rare. They approximate two opening Act 1 normal fights without elite intervention and follow ordinary shared consumption, including by other covered sources. This exception occurs only once per run. Account for common normal-fight openings in Acts 2/3 and other post-Rare reductions through average calibration rather than special entries. Do not recalculate entries using the live offset or vanilla's shared RNG.

#### Supplementary cards

**Brain Leech — Share Knowledge:** simulate all five slots first under the applicable difficulty rules. Consume one triple under the ordinary delivery rule. Generate two additional Common/Uncommon cards with event RNG, excluding all identities already selected; use the ordinary non-Rare Uncommon probability if no Rare is delivered, otherwise the rare-bearing companion probability. Append them before any A0–6 replacements and presentation swaps.

**Room Full of Cheese:** use one Common triple and generate the remaining five Common cards with event RNG.

#### A0–6: multiple Rares

For own-character random-rarity offers, maintain one additional Rare-only identity sequence per character, shared by the three source groups and independent of fixed-Rare triples. For an offer with `k >= 1` Rares, draw `k - 1` entries from this sequence.

Before presentation swaps, replace the first Common in canonical generation order, or the first Uncommon if no Common remains, for each extra Rare. Apply this across the entire offer, including Brain Leech's two event-generated cards: `R,U,U | C,C` becomes `R,U,U | R,C` for two Rares. Repeat for three, four or five Rares as needed. Use [filtered draws](#filtered-draws) to exclude the original Rare and earlier extra Rares; replaced companions remain consumed.

**Accepted tradeoff:** extra-Rare sequence state depends on prior Rare counts; companion replacements depend on offer composition. These A0–6 alignment and distribution tradeoffs are accepted because the design primarily targets A7+.

**Vanilla deviation — NoSL 3:** Common-first replacement adds a preference for retaining Uncommon companions in multi-Rare offers, unlike vanilla's per-slot generation. **Frequency:** rare — at A0–6, approximately **0.265% of vanilla fixed-base-odds three-card offers** contain at least two Rares, calculated from the 3% per-slot Rare chance.

#### Presentation

For own-character random-rarity offers, after supplementary generation and any A0–6 replacements, swap the original Rare, then extra Rares in draw order, into the simulated Rare positions in ascending slot order.

#### Scroll Boxes

Resolve Defect's two independent 1% triple-Claw substitution checks with event RNG before requesting Common sequence entries. Non-Defect offers follow the no-substitution branch.

- **No substitution:** consume one Common triple. It supplies both Common cards in bundle one and the first Common card in bundle two; generate the remaining Common and both Uncommons with event RNG.
- **Triple-Claw exception:** if either bundle is replaced, bypass the shared Common sequence without inspecting its head. Each substituted bundle contains three fixed Claws; generate any remaining ordinary bundle entirely with event RNG. If both bundles are substituted, generate no unused ordinary-bundle cards.

### Rerolls

A reachable reroll consumes a fresh offer from its pool/request/filter group, including expanded pools, with ordinary offer structure and duplicate rules. Preserve source-specific rarity and offset effects. Shared-luck offers also apply luck and deferral rules; other sources remain outside that system. Fixed-rarity rerolls retain their group's sequence structure: triple-backed offers consume fresh triples; Power and other-character offers consume the required individual entries from their respective sequences. They do not consume the random-rarity group's rare-bearing sequence.

Event-generated supplementary cards use fresh local generation without restarting the original offer's address.

Kaleidoscope's Neow offers cannot be rerolled with Orobas's Driftwood in the normal-run scope.

### Prismatic Gem and Dingy Rug

**Intent:** align rewards from the same expanded pool.

Preserve the [existing pool-modification boundaries](#card-reward-offers) and apply the expanded-pool route. All results, including own-character cards, consume only that group; ordinary identity sequences and triple boundaries remain unchanged. Rug's fixed-Common or already-colorless offers reuse their existing group because their complete eligible pool is unchanged.

Retain source rarity rules, eligible-card sampling weights, offer sizes, duplicate exclusions, luck rules and upgrade rules. Neither relic resets luck state or restarts opening-only rarity exceptions.

Lasting Candy retains its source rarity-roll sequence position and fallback rules when its inherited pool changes.

**Accepted tradeoff:** separate identity groups sacrifice alignment with ordinary own-character identities to avoid acquisition-dependent reconstruction; prior shared rarity state still matters.

## Settled policy: permanent transformations and direct card additions

**Intent:** align same-pool transformations and covered direct additions, preserving source rarity odds and isolating them from card reward offers and combat transformations.

**Covered direct additions:** Infested Automaton's Study and Touch Core, Endless Conveyor's Fried Eel, Neow's Arcane Scroll and Neow's Bones curse, and Distinguished Cape.

### Selection and consumption

Use the permanent-card routes for seven pools: the five characters, colorless and curse.

- **[Transformations](#logical-occurrence-lifetime):** retain [vanilla target pools, eligibility and gameplay inheritance](#permanent-transformations-and-direct-additions).
- **Unordered transformation batches:** process the selected originals in the identity-only [Deterministic Card Processing Order](#deterministic-card-processing-order) first. Original upgrade/enchantment state does not affect this ordering; repeated requests for the same identity remain tied and retain their multiplicity. After generation, associate replacements in generation order with equivalent selected input positions in frozen presentation order.
- **Direct additions:** use the granting source's pool and restrictions; Infested Automaton's rarity exception is specified in [generation routing](#settled-policy-generation-routing).

**Distinguished Cape:** draw twice from the curse sequence; the second pull skips the first identity.

**Vanilla deviation — SL 5(↓):** canonicalizing unordered transformation batches removes order-dependent replacement alternatives.

Baseline: [generation mechanics](#permanent-transformations-and-direct-additions) and [Catfood addressing](#catfood-addressing-and-combat-rewards).

## Settled policy: event-specific rewards

The event-specific route covers Trash Heap, Doll Room, event choices granting otherwise unobtainable rewards and Fake Merchant stock. Preserve existing choices, costs and eligibility.

Neow's Bones' Ancient relic pair follows this policy; its curse follows [direct additions](#settled-policy-permanent-transformations-and-direct-card-additions).

Fake Merchant's price rolls also use event RNG, keeping its shop fixed for the same seed.

## Settled policy: Wing Charm

**Intent and scope:** align Swift targets across corresponding generated rewards, preserving existing generation-batch boundaries and modifier order. This includes eligible direct generation; Sea Glass retains one selection per five-card rarity batch, not one across its combined display.

Resolve [A0–6 Rare replacements](#a06-multiple-rares) before reward enchantments. Each replacement retains the replaced slot's Wing Charm priority or supplementary-card membership; its identity and inherent upgrade roll come from its own entry. Evaluate resulting cards' Swift eligibility when Wing Charm runs, after earlier modifiers. No eligible cards means no effect; select an eligible singleton directly.

**Triple-backed offers:** assign independent uniform `[0,1)` Wing Charm priorities to the three canonical entries through their [stable entry addresses](#stable-entry-addresses), and select the lowest-priority eligible card, subject to the exceptions below. The [canonical triple](#settled-policy-card-reward-offers) precedes presentation swaps.

- **Non-Rare larger offers:** use the canonical triple in Brain Leech and Room Full of Cheese; if none of its cards is eligible, select uniformly among eligible supplementary cards with event RNG. Restricting selection to interchangeable slots aligns the shared triple while preserving marginal gameplay outcomes, subject to the opening-triple deviation below.
- **Rare-bearing Brain Leech:** let `x` count eligible cards across all five and `y` eligible cards in the two supplementary positions. Select the supplementary group when an event-RNG uniform `[0,1)` value `u < y/x`; otherwise use the triple's priorities. If both supplementary cards are eligible, choose uniformly with an independent event-RNG draw under a separate purpose, so eligibility-dependent branches do not shift draws for corresponding selections. This gives each eligible card probability `1/x`, including replacement Rares; matching eligible extras retain the same target whenever both runs select that group.
- **Lasting Candy:** if its added Power is eligible, use an independent uniform `[0,1)` value derived from that Power sequence entry and the Wing Charm purpose; select it when the value is below `1/x`, where `x` counts all eligible cards including the Power. Otherwise use the triple's priorities. Qualifying combat offers have three base cards, so this rule excludes no supplementary candidates.

**Other batches:** use the same entry-derived priorities for every card and select the lowest eligible one. Other-character offers share per-entry priorities across Sea Glass and Colorful Philosophers despite different batch sizes. No separate advancing Wing Charm counter is required.

**Vanilla deviation — NoSL 1:** larger non-Rare offers favor enchanting positions in the canonical triple.

**Vanilla deviation — NoSL 3:** when Brain Leech consumes an opening non-Rare triple, its different rarity probabilities slightly change the enchanted card's rarity distribution.

## Settled policy: persistent selection

**Intent:** align affected cards across sources, decks and reward choices, and potion sacrifice offers independently of inventory order.

Use [corrected persistent clocks or their fixed-cohort shortcut](#corrected-persistent-selection-priorities). Preserve source access, triggers, counts, eligibility, costs, rewards and effect order, subject to local rules.

| Selection / candidates | Strategy and sharing | Local clock rules |
|---|---|---|
| Random permanent upgrades / downgrades of existing deck cards | Corrected clocks; one shared scope per operation. Source filters do not split clocks. | Mark consumed clocks zero; apply the refill rules below. |
| Pael's Tooth / Beautiful Bracelet | Fixed cohort; separate purpose-specific priorities with [reward aliases](#card-reward-selection-keys). | Shared fixed-cohort consumption; cohorts and effects below. |
| Slippery Bridge: [vanilla candidates](#slippery-bridge-selection), including fallback | Corrected clocks; Bridge-only scope with [reward aliases](#card-reward-selection-keys). | Update once when a newly selected card is shown. If declined, renew its clock once before the next selection, using its per-occurrence renewal count. |
| Ranwid the Elder / Stone of All Time: owned potions | Corrected clocks; one shared run-wide scope. | Singleton: leave its clock unchanged. Otherwise renew the offered occurrence's clock once when the offer becomes available, using its per-occurrence renewal count. Declined potions remain selectable. |

### Permanent upgrades and downgrades

**Upgrade sources:** Whetstone, War Paint, Fragrant Mushroom, Sand Castle, War Hammer, Fishing Rod, Doors of Light and Dark, Battleworn Dummy, Endless Conveyor, Tablet of Truth, and Reflections.
**Downgrade sources:** Wongos and Reflections.

- **Upgrade-clock refill:** after a random downgrade, retain a nonzero upgrade clock; otherwise take the next value from one dedicated run-wide refill sequence, shared only by Wongos and Reflections. This sequence is consumed at most three times per run.
- **Downgrade-clock refill:** Wongos refreshes the downgraded card's downgrade clock using event RNG. Reflections leaves consumed downgrade clocks at zero; no later permanent downgrade source can use them.

Reflections applies downgrades in ascending pre-selection downgrade-clock order: first downgraded, first refilled. It then determines upgrade candidates; downgraded cards may qualify.

Target selection stops advancing event or Niche RNG to isolate unrelated outcomes.

**Vanilla deviation — SL 4A(↓):** reordering random upgrades, such as Whetstone and War Paint, with candidate addition/removal or another disjoint random upgrade no longer rerolls their targets.

### Fixed-priority card selection

- **Pael's Tooth:** [temporarily store](#logical-occurrence-lifetime) five player-selected unupgraded cards outside the deck, preserving gameplay properties. Initial selection follows [equivalent-card routing](#equivalent-card-selections). At each subsequent combat trigger, return the next card upgraded and retire only its return-order record. Later removals never add or recycle cohort records.
- **Beautiful Bracelet:** enchant up to four eligible deck cards with Swift 2.

### Potion departure routing

For [player-chosen use, discard and trade](#logical-occurrence-lifetime), treat same-type owned potion copies as equivalent, including copies with different displayed trade payoffs. All such sources use [representative routing](#representative-routing) in one scope per potion type, separate from sacrifice-offer selection. Slot-dependent offers and trade payoffs stay with the clicked option.

Baseline selection: [potion event mechanics](#potions).

## Settled policy: The Future of Potions: card-type assignment

**Intent:** match rarity/type menus for equal offered rarity counts, and potion/type choices for identical offered potion multisets regardless of order. Retaining an individual potion's trade when other potions change is best effort.

Offer the first three owned potions in slot order, preserving display order. Use the existing [reward-rarity mapping and allowed card types](#card-reward-offers); generated cards follow [generation routing](#settled-policy-generation-routing).

- Use three independent event-local type sequences, addressed by event ID + type-assignment purpose + reward rarity (Common/Uncommon/Rare). Each entry is an independent uniform draw from that rarity's allowed types.
- Give offered potions [opportunity-specific priorities](#opportunity-specific-selection-priorities), using event ID as the opportunity address, a separate ordering purpose, and stable potion model ID + duplicate number as the candidate key. Number identical copies locally from 1 in offered slot order, independently of occurrence addresses. Priorities are independent of the type sequences.
- Within each reward rarity, assign successive sequence entries to potions in ascending priority order. Consume only for offered options, at most three entries total. Keep assignments fixed for the current event.

**Rationale and limit:** rarity sequences align menus while preserving vanilla's independent type probabilities. Shared priorities retain relative ordering across overlapping inventories, but adding or removing a same-rarity potion can shift a survivor's sequence position. Identical copies retain the same multiset of trades when reordered.

## Settled policy: Ranwid relic sacrifice

To align Ranwid's relic sacrifice, use [opportunity-specific selection priorities](#opportunity-specific-selection-priorities), with event ID as the opportunity address, independently of Relic Trader. Offer the lowest-priority tradable owned relic, preserving existing eligibility, costs and rewards.

## Settled policy: Relic Trader

**Intent:** align [replacement relics](#settled-policy-generation-routing); prioritize outgoing-relic overlap over trade matching.

### Outgoing relic assignment

1. For each tradable owned relic and reward slot, derive an independent uniform pair priority from event ID + relic identity + slot.
2. Freeze the three relics with the lowest minimum of its three pair priorities.
3. Among these relics, repeatedly assign the lowest-priority remaining pair, removing its relic and slot.

**Behavior preservation:** outgoing selection and pairing remain uniform. One eligible-owned-relic substitution retains at least two outgoing relics, with maximal symmetric expected outgoing overlap.

### Rejected alternatives

- **Independent candidate priorities:** lower trade agreement than the settled rule, with the same outgoing overlap.
- **Direct pairing over all eligible relics:** higher trade agreement, but one inventory substitution can change two outgoing relics.

Baseline: [shops, chests and relic events](#shops-chests-and-relic-events).

## Settled policy: Endless Conveyor

**Intent:** preserve shared dish order across runs.

Generate one persistent event-local sequence of independent draws over all randomized dishes using vanilla weights and a dedicated dish-selection purpose. For each random offer, continue from the current position, consuming ineligible entries—including the previous displayed dish—until the first eligible entry is offered; retain the resulting position for the next random offer. This preserves vanilla conditional probabilities while keeping the shared candidate order.

## Settled policy: potion-generated cards

**Intent and scope:** align random card generation from Attack, Skill, Power and Colorless Potions, Cosmic Concoction and Orobic Acid across corresponding card-generation opportunities.

A committed use, discard or trade consumes one card-generation ordinal of that potion type: use generates the [routed batch](#settled-policy-generation-routing); discard or trade skips it. Acquisition leaves this ordinal unchanged.

**Reward skips:** permanently skipping an unclaimed covered potion reward on the reward screen also consumes one ordinal. Count each skipped reward once.

**Cauldron exception:** leaving a Cauldron reward unclaimed or discarding an acquired Cauldron potion before entering the next room leaves the card-generation ordinal unchanged. After entering the next room, acquired Cauldron potions follow the ordinary use/discard/trade rule.

Preserve vanilla eligibility, identity sampling and batch rules; see [potion card-generation mechanics](#potion-card-generation).

**Vanilla deviation — SL 5(↓):** thieving hopper stealing and other potion types no longer change a potion's generated cards.

**Vanilla deviation — SL 5(↑):** discard, trade and covered reward skips advance the next same-type card batch.

## Settled policy: unknown-room resolution

**Intent:** align draws within each act and minimize movement of each room type's probability interval, limiting interference from earlier acts and other room types' changing chances.

### Addressing and room-type chances

- **Addressing:** room-type RNG uses the act-local ordinal of visited `?` nodes.
- **Shop chance:** when the map-adjacency policy excludes Shop, its stored chance remains unchanged, matching vanilla. Preserve existing Juzu Bracelet, Golden Compass and Lantern Key room-type restrictions.
- **Effective chances:** after exclusions, allocate a 100% probability budget in vanilla order (normal fight → chest → shop), capping each chance at the remaining budget. Use these effective chances only as interval widths; preserve stored chances and their vanilla updates.
- **Thresholds:** the chest interval begins at 0, the shop interval ends at 1, and the normal-fight interval stays centered at `7/15` unless shifted just enough to avoid overlap; `7/15` is the midpoint when chest/normal-fight/shop widths fill the range in their base `2:10:3` ratio. Unassigned space resolves to Event.

## Settled policy: Ancient offerings

**Intent and scope:** align Ancient offerings when eligibility changes. Preserve vanilla option pools, weights, prerequisites and menu structure. Granted effects follow their policies separately.

Use [opportunity-specific selection priorities](#opportunity-specific-selection-priorities) for Orobas's third pool, Pael's second and third pools, Tezcatara's first pool, and Tanx's and Nonupeipe's three-choice pools. The opportunity address is Ancient ID + option-pool ID; each candidate uses its stable option identity. Select the lowest-priority eligible option, or the three lowest for Tanx and Nonupeipe. Retain surviving entries' relative display order.

**Pael's second pool:** use weighted priorities `-ln(U) / weight`, with independent candidate-addressed uniform `U ∈ (0, 1)` and vanilla weights: Growth 1; each other option 2.

Retain vanilla generation and RNG positions for other menu components.

Baseline: [Ancient offerings](#ancient-offerings).

## Settled policy: ordinary-event selection

**Intent and scope:** improve same-seed event matching within and across acts, prioritizing agreement before the first mismatch. Ancients remain separate; [forced events](#settled-policy-forced-events) bypass ordinary selection.

**Conditions and state:** group IDs by equivalent eligibility predicates, excluding act availability and rare checks, with one stable key per condition. Act availability determines participating IDs; evaluate rare checks at ID selection to keep grouping compact. Follow the shared [addressing, consumption and saved-state contracts](#addressing-and-state).

### Act entry and carryover

At run start, independently shuffle each condition's IDs using condition key + ID-order purpose. This fixes each arriving group's internal order. At act entry, retain surviving unvisited IDs in their current order, remove expired IDs and insert newly act-available, unvisited IDs in their run-start order. Run-wide visited exclusions persist.

**Sorted random priorities:** for `n` surviving and `m` incoming IDs, take the first `n` and `m` independent uniform draws from separate old/new streams addressed by act + condition key + insertion purpose + old/new + draw index. Sort each set, assign it along its sequence in existing order, then merge by priority. This preserves uniform interleaving and both internal orders.

**Alignment tradeoff:** shared-event order is secondary; sufficiently rare prefix-agreement losses are acceptable.

Restore passed scheduling entries: each act-available, unvisited ID contributes one entry, including IDs whose condition currently fails. This removes earlier-act depletion while retaining ID order.

### Within-act selection

**Schedule:** fixed in Act 1 for shared-event order; refreshed in Acts 2–3 for prefix agreement with differing pools.

1. Initialize priority `-ln(U) / n_c` for conditions with `n_c > 0` unpassed entries: once at Act 1 entry, at each resolution in Acts 2–3. Independent uniform `U ∈ (0, 1)` draws use condition key + per-condition draw index, plus act in Act 1 or act-local event ordinal in Acts 2–3.
2. Spend one entry of the lowest-priority condition. If false or lacking unvisited IDs, retain its ID position. Otherwise, try its next unvisited ID; a rare-check failure passes that ID too.
3. After failure, add a fresh `-ln(U) / remaining_count` to that condition's priority, retiring it at zero. Keep other priorities fixed and repeat.
4. On success, advance the ID sequence and commit the visit and all passing. In Act 1, update the winning priority as in step 3 and retain priorities and draw indices; Acts 2–3 refresh at the next resolution.

**Wrapping:** on exhaustion, restore passed entries and restart priorities, continuing each condition's draw index. ID sequences wrap in the act's established order; visited IDs stay excluded.

**Vanilla deviation — NoSL 3:** refreshed condition priorities after wrapping can change expected event exposure. **Frequency:** rare (qualitative judgment).

**Implementation detail — empty eligible pool:** outside this policy's scope; relax act restrictions first, then all eligibility restrictions except visited-event exclusion, then allow repeats; stop at the first stage with a candidate.

**Behavior preservation:** before either structure wraps, preserve vanilla's distribution when eligibility cannot inspect hidden order. With passing rare checks, same starting state + same selected event leaves the same state. Scheduling changes cross-run and SL correlations; carrying ID order preserves earlier ordering across acts. Wrapping and the empty-pool fallback are separate probability and eligibility tradeoffs.

### Rejected alternatives

- **Fixed scheduling in every act:** improves shared order with matching pools, but loses prefix agreement when survivor pools differ.
- **Refreshed priorities in every act:** simpler and better same-ordinal matching, but more Act 1 shared-order reversals.
- **Cross-act insertion — shared position priorities:** reserve incoming positions using the lowest shared output-position priorities, then fill both sequences in order. Can improve prefix agreement, but preserved less shared-event order in simulated act transitions.
- **Cross-act insertion — weighted merging:** use shared output-position draws to choose old or incoming in proportion to remaining counts. Can improve prefix agreement over sorted priorities, but preserved less shared-event order in tests that consumed different numbers of leading events.

Baseline: [event selection and wrapping](#event-and-encounter-identities); [condition predicates and rare checks](#event-condition-classes).

## Settled policy: forced events

**Intent:** inserting a forced event must not affect later ordinary-event availability.

Resolve room type under the [unknown-room policy](#settled-policy-unknown-room-resolution). When an effect supplies a forced event, enter it without ordinary event-list eligibility scanning, selection, cursor advancement or ordinary event-opportunity advancement. Record the actual event visit and retain the normal visited-event semantics. The forced event uses its existing event RNG; it needs no special ordinary-event ordinal.

Lantern Key's forced War Historian Repy follows this bypass. A visited `?` still advances its act-local visited-node ordinal; preserve the existing forced room type and frozen non-event chances.

**Vanilla deviation — NoSL 3:** bypassing vanilla's forced-insertion opportunity consumption changes later ordinary-event availability relative to vanilla. Vanilla's consumption is probably unintended by developers.

## Settled policy: encounter setup

**Intent:** align corresponding combat setups within an act regardless of earlier-act combat counts.

Use the shared [combat address](#combat-addresses) + setup purpose.

Preserve encounter identity selection and setup sampling; reward ordinals and AI retain their own policies.

## Settled policy: monster HP

**Intent:** align HP rolls for initial enemies, summons, replacements and Tough Egg's hatch.

For every covered HP roll, use [opportunity-specific selection priorities](#opportunity-specific-selection-priorities) with `k = 1`: opportunity = [monster occurrence](#monster-occurrence-identity) + purpose-local roll ordinal; candidate key = integer HP value; purpose = initial HP or hatch. Hatch retains the egg's occurrence.

Preserve vanilla eligibility and full-range fallback, conditional probabilities, fixed values, range modifiers, subsequent HP changes, creature lifecycle order and summon rules. Punch-Off's starting HP reductions remain under [encounter setup](#settled-policy-encounter-setup).

Baseline: [monster HP mechanics](#monster-hp).

## Reference

[Event spreadsheet](references/StS2_Events.xlsx): **57 ordinary/forced events and 8 Ancients**. `is_deterministic` concerns offered choices. The workbook details criteria and state restrictions.

### Rarity calibration

[Rarity model](outputs/reward-calibration.cjs) and [route/event projection](outputs/reward-calibration-environment.cjs), 2026-10-11. Fit vanilla's expected displayed Uncommon totals separately in non-Rare and rare-bearing offers, including once-per-run opening triples and A0–6 Common-first replacement. Each three-act study uses 200,000 baseline and 200,000 independent validation runs. Act 3 has 0/1/2/3 elites with probabilities 40%/40%/15%/5%; its zero-elite branch removes the first elite and replaces the second with an event.

| Study | Validation fit: ordinary / companion | Settled rates | Uncommon difference per 100 displayed slots |
|---|---|---|---|
| [A7+](outputs/reward-calibration-act3-results.json) | 38.016% / 38.942% | 38% / 39% | −0.011 |
| [A0–6](outputs/reward-calibration-a6-results.json) | 39.863% / 40.469% | 40% / 40.5% | +0.105 |

Shop offsets approximate the mean Rare count across five character-card slots, excluding fixed colorless cards. Vanilla versus settled Rare chances per slot are **4.453% / 4.5%** at A7+ and **10.762% / 10.8%** at A0–6. Constant offsets change clustering: shops with any Rare character card occur in **19.55% / 20.56%** and **41.78% / 43.53%**, respectively.

These are route-average choices: elite frequency and extra-offer sources shift the fitted rates. The [earlier A7+ study](outputs/reward-calibration-results.json) and A0–6 results retain sensitivity cases, assumptions and checks. The model approximates player-state eligibility, event outcomes and non-rarity item effects.

### Rejected combat-gold coupling

The [compensated shared-roll alternative](#rejected-alternatives) used one normal-fight/elite gold ordinal to supply uniform `u ∈ [0,1)`:

| Difficulty | Shared draw | Normal-fight gold | Elite gold |
|---|---|---|---|
| A0–2 | `X = floor(11u)` | `10 + X` | `35 + X` |
| A3+ | `X = floor(8u)` | `7 + X + b` | `26 + X` |

At A3+, `b` was a fair bit addressed by the cumulative unreduced normal-gold index, independently of `u` and other bits. Normal gold's endpoints 7 and 15 each had probability 1/16; 8–14 each had 1/8.

## Appendix: existing mechanics

Existing mechanics for comparison: vanilla by default, [Catfood differences](#catfood-addressing-and-combat-rewards) explicitly labeled. Settled policies belong in the main design sections.

| Mechanism | Baseline and interference |
|---|---|
| [Unknown room types](#room-resolution) | Changing odds and RNG state make `?` resolution route-dependent. Catfood leaves this unchanged. |
| [Event / encounter identities](#event-and-encounter-identities) | Eligibility and room visits affect list cursors. |
| [Shops and events](#shop-and-courier-rng) | Visits and restocks can shift later stock, prices and rewards. |
| [Combat](#combat-setup-and-shuffles) | Earlier rooms and reshuffles affect later setup and opening hands. |
| [Combat gold](#combat-gold) | Other reward draws can shift later gold. |
| [Card rarity](#card-reward-offers) | Combat reward history affects later combat and shop rarities. |
| [Ancient options](#ancient-offerings) | Options are independent of event counts; current eligibility can change the menu. |
| [Chests](#shops-chests-and-relic-events) | Other rewards can shift chest contents. |
| [Relic pools](#relic-bags) | Finite stock couples sources; depletion can change delivered rarity. |
| [Potion drops](#potions) | Earlier checks affect later drop probability. |
| [Potion generation](#potions) | Failed grants can still shift later results. |
| [Transformations / direct additions](#permanent-transformations-and-direct-additions) | Results depend on the source and its other RNG consumption. |

### Rooms, events and combat

#### Room resolution

**Unknown-room resolution:** base chances are 10% normal fight / 2% chest / 3% shop; Event takes the remainder (initially 85%). Elites are disabled under ordinary rules. Each roll consumes one `UnknownMapPoint` draw. The selected non-event type resets to its base chance; other allowed non-event types gain their base amount. Excluded types retain their stored odds. Exclusions apply before interpreting the draw, without renormalizing the remaining probabilities. Odds reset between acts; RNG state persists across acts.

**Unknown-room overflow:** vanilla checks cumulative thresholds in normal fight → elite (if enabled) → chest → shop order. Totals above 100% are neither normalized nor capped in stored state; intervals beyond 1 are unreachable, so later types lose effective probability and Event has none. At 70% normal fight / 14% chest / 21% shop, effective chances are 70% / 14% / 16%.

| Source | Mechanics |
|---|---|
| `?` shop exclusion | Shop is excluded when the previous visited map point contained a shop, or every outgoing map node is a shop. Stored shop odds freeze during exclusion. |
| Juzu Bracelet / Golden Compass | Juzu excludes normal fights from `?` resolution. Golden Compass forces Event during its affected act. |
| Lantern Key | In Act 3, forces `?` to Event and replaces the selected event with War Historian Repy. The ordinary event-list cursor still advances. Like Golden Compass, forced resolution still consumes one `UnknownMapPoint` draw while non-event odds freeze. |

#### Event and encounter identities

**Event / encounter identity selection:** per-act lists are generated upfront with `UpFront` RNG; event lists shuffle act-specific plus shared identities without explicit weights. Room type is resolved first. An Event advances through its act's pre-shuffled list to the next `IsAllowed` identity not in the run-wide visited-event set; skipped entries are passed over, not retained for the next eligible pull. A normal fight uses the next entry in the act's normal encounter list, regardless of whether its map marker was `?` or a normal-fight marker. Forced-event replacement occurs after ordinary event eligibility scanning.

**Event-list wrapping (vanilla):** the cursor indexes `events[cursor % events.Count]`; the list wraps without reshuffling or clearing visits, and skipped entries remain in it. Skips advance the cursor too. A skipped event can qualify after wrapping; a full unsuccessful scan instead returns the cursor's event anyway, potentially repeated or ineligible.

**Event-pool capacity:** act lists contain 25–31 entries before eligibility/visited filtering, versus 13–15 ordinary floors per act.

**Event skipping and probability:** with a uniform hidden shuffle, other eligibility held fixed and no wrapping, encounters while condition K is false do not themselves change K-events' later selection probability when K becomes true. Skipping passes hidden K entries along with encountered non-K entries; exchangeability of their positions preserves the odds. For two K events and three others, zero, one or two non-K encounters while K is false leave the next K chance at `2/5`. Encounters while K is true have the usual without-replacement effect. This is a distributional property, not identical outcomes for each seed or invariance under other eligibility changes.

**Ancient identity:** selected during upfront room generation, using `UpFront` RNG. Shared Ancients are partitioned between later acts before each act selects its Ancient. Earlier room visits leave these selections unchanged. Evidence: `RunManager.GenerateRooms`, `ActModel.GenerateRooms`.

#### Ancient offerings

All eight Ancients select their menus with event RNG seeded by run seed + event identity hash + player-slot contribution (`EventModel.BeginEvent`). Matching seed, Ancient and ordered eligible pools reproduce the menu independently of earlier event visits. Changed eligibility can also change which still-eligible options appear.

| Ancient | Menu generation and relevant conditions |
|---|---|
| Neow | Two positive options and one curse option, with fixed exclusion pairs and unlock/player-count restrictions. Pools are fixed in the fully unlocked solo scope. |
| Darv | Shuffles one choice from each eligible relic group; offers three, or two plus Dusty Tome with 50% probability. Ectoplasm and Sozu are Act 2 only. |
| Orobas | One choice from each of three pools. Sea Glass's featured character uses event RNG. The third pool checks for the starter relic (Touch of Orobas) and supported starter card (Archaic Tooth), with a locked option if both are absent. |
| Pael | One choice from each of three pools. Claw needs ≥3 Goopy-eligible cards; Tooth needs ≥5 removable cards; Legion requires no event pet. Growth has half the weight of each other eligible second-pool option. |
| Tezcatara | One choice from each of three pools; Nutritious Soup joins the first while a Basic Strike remains. |
| Tanx | Three choices from a shuffled pool; Tri-Boomerang joins with ≥3 Instinct-eligible cards. |
| Vakuu | One choice from each of three fixed pools. |
| Nonupeipe | Three choices from a shuffled pool; Beautiful Bracelet joins with ≥4 Swift-eligible cards. |

**Scope of consistency:** the menu RNG does not isolate every granted effect. Neow's Bones shuffles its relic candidates with shared `Rewards` RNG. Dusty Tome also calls `Rewards`, but its eligible pool contains exactly one card per character in this version: Corruption, Wraith Form, The Sealed Throne, Forbidden Grimoire or Biased Cognition. Its payload is therefore fixed by character despite consuming a draw.

**Fur Coat:** selects marked normal-fight/elite map nodes with its own run-seed + player-slot + relic-ID RNG and saves their coordinates. Matching maps reproduce the marks independently of earlier RNG consumption.

#### Event condition classes

**Scope:** vanilla entry predicates for solo ordinary runs with permanent unlocks satisfied; Ancients are separate. These **31 reference classes cover 56 ordinary events**. Act availability combines pool membership with entry predicates; run-wide visited exclusions apply throughout. [Ordinary-event selection](#settled-policy-ordinary-event-selection) groups rows with equivalent non-act predicates into one scheduling condition.

**Reference labels:** `CX-Y` identifies one table row. `X` gives eligible acts: `1O` = Overgrowth, `1U` = Underdocks, `1` = either Act 1, `2` = Hive, `3` = Glory; `12`, `23`, `123` combine acts. `Y` gives a descriptive family and consecutive index starting at 1 within each act/family prefix: `N` = no additional condition, `G` = gold, `H` = HP, `D` = deck, `F` = floor, `P` = potions, `R` = relics, `M` = mixed predicates. Rare checks stay outside the grouping predicate.

| Class | Scheduling condition beyond act | Count | Events |
|---|---|---:|---|
| C1O-N | None | 8 | Aroma of Chaos; Byrdonis’ Nest; Dense Vegetation; Jungle Maze Adventure; Sapphire Seed; Tablet of Truth; Wellspring; Wood Carvings |
| C1O-G1 | Gold ≥ 44 | 1 | Whispering Hollow |
| C1O-G2 | Gold ≥ 100 | 1 | Morphic Grove |
| C1O-G3 | Gold ≥ 149 | 1 | Luminous Choir |
| C1O-H1 | HP ≤ 70% of maximum | 1 | Unrest Site |
| C1U-N | None | 5 | Abyssal Baths; Doors of Light and Dark; Drowning Beacon; Spiraling Whirlpool; Sunken Treasury |
| C1U-G1 | Gold ≥ 55 | 1 | Waterlogged Scriptorium |
| C1U-G2 | Gold ≥ 120 | 1 | Endless Conveyor |
| C1U-H1 | HP > 5 | 1 | Trash Heap |
| C1U-F1 | Total floor ≥ 6 | 1 | Punch-Off |
| C1-N | None | 1 | Sunken Statue |
| C1-H1 | HP ≥ 10 | 1 | The Legends Were True |
| C2-N | None | 8 | Bugslayer; Colorful Philosophers; Doll Room; Field of Man-Sized Holes; Infested Automaton; Lost Wisp; Spirit Grafter; The Lantern Key |
| C2-G1 | Gold ≥ 100 | 1 | Welcome to Wongos |
| C2-G2 | Gold ≥ 125 | 1 | Zen Weaver |
| C2-H1 | HP ≥ 19 | 1 | Colossal Flower |
| C2-D1 | ≥ 2 removable Basic Strikes and ≥ 2 removable Basic Defends | 1 | Amalgamator |
| C2-P1 | ≥ 1 potion | 1 | Stone of All Time |
| C3-N | None | 5 | Battleworn Dummy; Hungry for Mushrooms; Reflections; Tinker Time; Trial |
| C3-H1 | HP ≥ 12 | 1 | Round Tea Party |
| C3-D1 | A card eligible for Soul’s Power | 1 | Grave of the Forgotten |
| C12-N | None | 2 | Brain Leech; Room Full of Cheese |
| C12-G1 | Gold ≥ 150 | 1 | Tea Master |
| C23-N | None | 2 | Potion Courier; Symbiote |
| C23-G1 | Gold ≥ 100 | 1 | Crystal Sphere |
| C23-M1 | Gold ≥ 100 or holding Foul Potion | 1 | Fake Merchant |
| C23-M2 | Gold ≥ 100, ≥ 1 potion and ≥ 1 tradable relic | 1 | Ranwid the Elder |
| C23-R1 | ≥ 5 tradable relics | 1 | Relic Trader |
| C123-N | None | 2 | Self Help Book; This Or That |
| C123-F1 | Total floor > 6 | 1 | Slippery Bridge |
| C123-P1 | ≥ 2 potions | 1 | The Future of Potions |

**Rare checks on selected IDs:**

| Event | Rare check |
|---|---|
| Wood Carvings | a removable Basic card |
| Morphic Grove | ≥ 2 transformable cards |
| Luminous Choir | Available relic stock |
| Spiraling Whirlpool | a card eligible for Spiral |
| The Legends Were True | A nonempty deck |
| Field of Man-Sized Holes | a card eligible for Perfect Fit |
| Slippery Bridge | A removable card |

**Forced event:** War Historian Repy adds one event outside these classes: Lantern Key inserts it in Act 3; ordinary `IsAllowed` is always false.

#### Combat setup and shuffles

Encounter setup uses floor-based addressing. Shuffle RNG persists across combats; extra reshuffles advance the same generator used for later opening hands.

**Shuffle input order:** `Player.PopulateCombatState` clones cards in deck order; `CardPile.RandomizeOrderInternal` applies `UnstableShuffle` directly. Later `CardPileCmd.Shuffle` calls `StableShuffle`, which sorts before shuffling. `CardModel.CompareTo` compares model identity and upgrade level; enchantment and persistent progress remain tied.

#### Monster HP

Enemy creation samples uniformly with `Niche` from the inclusive initial HP range, excluding `MaxHp` values (current/max HP denominators) in the enemy list; an empty candidate set restores the full range (`Creature.SetUniqueMonsterHpValue`). Startup creates/adds enemies sequentially; summons share this path. Death-triggered spawns precede source removal, so dying enemies still count.

**Special cases:** Tough Egg's hatch samples its full hatchling range with `Niche`. Axebot's range rises by 10 per replacement. Decimillipede segments adjust HP to distinct even values. Punch-Off's starting reductions use encounter-setup RNG.

**Summons:** ordinary sampling covers Fabricator's bots, Two-Tailed Rats, Ovicopter's eggs and Phrog Parasite's four sequential Wrigglers. Eye with Teeth (6), Parafright (21) and Gas Bomb (7/8 by ascension) have fixed HP. Gremlin Merc creates Fat, creates/adds Sneaky, then adds Fat: each rolls before the other joins the enemy list (`SurprisePower.AfterDeath`).

### Relics, shops and chests

#### Relic bags

**Pools and order:** the fully unlocked player bag contains **122 ordinary relics**—26 Common, 32 Uncommon, 38 Rare, 26 Shop (115 shared + 7 character-specific). The chest bag (`SharedRelicGrabBag`) contains only shared relics. Each bag has separately shuffled per-rarity lists; the two bags overlap in identity.

**Draws:** rarity is 50% Common / 33% Uncommon / 17% Rare; fixed-rarity requests consume identity only, including in Catfood. Ordinary rewards and Wongos draw from the player bag's front; shops draw two random-rarity relics and one Shop relic from its back. Chests draw from the chest bag's front. Factory rarity rolls default to player `Rewards` RNG; chests use separate `TreasureRoomRelics` RNG.

**Consumption:** ordinary reward/shop generation removes the selected identity from both bags, even if declined or unbought. Chest generation removes it only from the chest bag. Acquiring a non-stackable relic removes its identity wherever present in both bags, preserving survivors' relative order. In single-player, skipping a chest relic leaves it available in the player bag.

One-rarity example, front at left; `K` is character-specific. Acquire and skip are alternative branches after generation:

| Stage | Chest bag | Player bag |
|---|---|---|
| Before chest | `B D A C` | `A K C B D` |
| Generate B | `D A C` | `A K C B D` |
| Acquire B | `D A C` | `A K C D` |
| Skip B | `D A C` | `A K C B D` |

**Eligibility:** pool membership, requested rarity and depletion constrain every draw. Run-state `IsAllowed` failures are purged from all rarity lists in the queried bag. Source filters instead retain skipped entries. Shops and Wongos require `IsAllowedInShops`, excluding Amethyst Aubergine (Common), Bowler Hat and Lucky Fysh (Uncommon), Old Coin and The Courier (Rare). Wongos is the only non-shop ordinary-bag caller with an additional identity filter; Relic Trader's tradability restriction applies to outgoing relics, not replacements.

**Filtered relic pulls:** remove the first matching entry from the requested end and compact the list. Within one rarity, for a uniform shuffle of two shop-eligible identities and one shop-ineligible identity, a filtered front pull followed by an unrestricted front pull gives the shop-ineligible identity with probability `2/3`, twice its initial `1/3`; a filtered back pull leaves that probability at `1/3`. The multiplier depends on bag composition, and the front bias can span multiple later positions. Both examples retain eligible supply after the filtered pull.

**Chest probability:** for one rarity with a uniform unseen player-bag order, `N` remaining identities and `C` character-specific identities, acquiring a shared identity selected independently of that order leaves probability `C/(N-1)` at every remaining position: e.g. `5/100` becomes `5/99`. Skipping leaves the player bag unchanged. This describes marginal probabilities before further observations/restrictions, not every source history; preceding filtered draws can invalidate the uniform-order assumption.

**Floor eligibility:** these 17 relics require `TotalFloor < 41` in single-player or `< 38` in multiplayer (`IsBeforeAct3TreasureChest`): Amethyst Aubergine, Book of Five Rings, Bowler Hat, Dragon Fruit, Frozen Egg, Girya, Juzu Bracelet, Lasting Candy, Lucky Fysh, Meal Ticket, Molten Egg, Old Coin, Planisphere, Shovel, Toxic Egg, White Beast Statue and White Star. The cutoff uses total floor, not whether a chest was visited.

**Identity depletion:** from the requested rarity, search forward along Shop → Common → Uncommon → Rare, then multiplayer fallback; otherwise Circlet. Refresh-enabled bags first refill empty requested rarities. Catfood inherits this shared-bag fallback.

#### Shops, chests and relic events

| Source | Mechanics |
|---|---|
| Chests | Relics are generated on room entry; base gold uses `Rewards` on opening. Pool and consumption rules: [relic bags](#relic-bags). |
| Neow — Silver Crucible | First treasure room is empty, with neither gold nor relics. |
| Wongos | Act 2; all players have ≥100 gold. Featured Rare: 200 gold; hidden Common: 100 gold. Leaving downgrades a random upgraded card, if any. Featured Rare is generated and consumed from stock on offer, even if declined. This also occurs below 200 gold and is **harmful to same-seed consistency**. Hidden Common is generated on purchase. Both require `IsAllowedInShops`. |
| Wongos — Mystery Ticket | Pays three random relics after five combats. |
| Relic Trader | Acts 2–3; all players own ≥5 tradable relics. Randomly selects three owned relics and three replacements to offer; consumes all three replacement identities/rarities before the player's selection. Normally no leave. Replacement rarity is independent and replacements need not be tradable. Tradable excludes Starter/Event/Ancient, upon-pickup, used-up, melted and pet-spawning relics. |
| Courier | 20% discount; removal never restocks; sale clears on restock. Restocked character cards retain type, colorless cards retain rarity. Relics roll ordinary rarity even in the Shop slot. Shop ordinal does not advance. Potion restocks do not guarantee Uncommon. |
| Trash Heap | Dive In costs 8 HP and grants a random relic from its special list; Grab grants 100 gold and directly adds a random card from its special ten-card list. |

#### Shop and Courier RNG

Initial stock and restocks share these player RNG streams. Each listed random operation consumes a separate draw; relic identity follows bag order. The counts and interactions below exclude additional item effects.

| Restock | `Rewards` draws | `Shops` draws |
|---|---|---|
| Character card | 2: rarity, upgrade check | 2: identity, price |
| Fixed-rarity colorless card | 1: upgrade check | 2: identity, price |
| Relic | 1: rarity | 1: price |
| Potion | 0 | 3: rarity, identity, price |

Ordinary shop cards suppress random upgrades but still consume the check draw.

**Harmful to same-seed consistency — later restock effects:**

- **Card → relic; relic → character card:** rarity, identity and price can change. Both also shift potion rarity, identity and price; relics shift fixed-rarity colorless card identity/price.
- **Potion → card:** identity and price can change. **Potion → relic:** price only; rarity and bag order stay unchanged.

Thus even one restock per category can interfere. Equivalent slots share generators; matching RNG state, request and candidates preserves outcomes. Events may consume shared rewards/bags despite having event RNG.

### Cards

**Card pools:** Common Power cards do not exist.

#### Card state and selection

**Out-of-combat player card batches:** batch selections are unordered. Deck selectors and reward grids collect the full selection before effects. Enumeration can affect acquisition order (e.g. Sea Glass).

**Player-selected deck operations:** the inspected deck-selection callers cover upgrading (Smith), enchanting (Gnarled Hammer), permanent removal (Empty Cage), transformation (Astrolabe, Claws, Wood Carvings), combining removals with a new addition (Amalgamator), temporary storage for return (Pael's Tooth), and copying (Dolly's Mirror). Wongos and Reflections downgrade randomly. Reward selections acquire new occurrences. Catfood's inspected patches add no operation category.

**Persistent card state:** `SerializableCard.Equals` compares identity, upgrade level and enchantment while omitting card-specific saved properties. Genetic Algorithm maintains `CurrentBlock = 1 + IncreasedBlock`; The Scythe maintains `CurrentDamage = 13 + IncreasedDamage`. Tinker Time grants one configured Mad Science; later copies inherit its type/rider configuration. Dolly's Mirror excludes Quest cards, including Spoils Map.

**Pael's Tooth:** The player selects and removes five unupgraded cards. Each subsequent combat returns one random remaining stored card, upgraded; all five return after five combats. Returned records leave the cohort. This is stored-card selection without replacement, not new identity generation. Mechanics clarified by the project owner.

#### Slippery Bridge selection

Each offer uses event RNG to select eligible card copies uniformly; duplicates count separately. Eternal cards are not removable.

- **First offer:** removable non-Basic cards.
- **Rerolls:** record the rejected copy. From removable cards, exclude previously rejected copies and all copies of the previous offer's identity, regardless of status. Basics are allowed.
- **Empty candidate set, including the first offer:** draw from all removable copies. Keep the rejected set; reapply normal filters next reroll. Fallback can repeat the previous copy or identity before every copy has been offered.

#### Card reward offers

**Card rarity:** shared offset starts at `-0.05` and resets there after a Rare combat reward roll, but not after direct additions or on Ancient entry. Normal/elite non-rare rolls add `0.005`, capped at `0.40`. Base Rare chances for normal/elite/shop rolls are 1.49%/5%/4.5%, plus the offset; base Uncommon chances are 37%/40%/37%. A negative Rare threshold also reduces the Uncommon chance: initial normal rewards have 0% Rare and 33.49% Uncommon. Boss rewards guarantee Rare and reset the shared offset. Shop rolls read the offset without updating it.

**Base-odds card offers:** Orrery, Lost Coffer, Brain Leech's Share Knowledge and Trial's Nondescript Guilty use Common/Uncommon/Rare odds of 61.51%/37%/1.49% per slot without reading or updating the combat offset. Share Knowledge and Trial suppress random upgrades. Each slot rolls base odds independently, so these offers can contain multiple Rares, including three in a three-card offer.

**Card upgrades:** ordinary Common/Uncommon chances in Acts 1/2/3 are **0% / 12.5% / 25%**; Rare cards have no ordinary act-based upgrade chance.

**Without Scarcity (A0–6):** normal/elite/shop base Rare chances are 3%/10%/9%, non-Rare offset growth is `0.01`, and ordinary Common/Uncommon upgrade chances in Acts 1/2/3 are 0%/25%/50%. Base normal/direct-addition Common/Uncommon/Rare odds are 60%/37%/3%.

**Normal versus elite card rewards:** both offer three distinct card identities from the same character pool under the same upgrade rules; elites have no inherent upgrade-chance bonus. Under Scarcity, ordinary normal offers cannot contain two Rares because the rare chance after offset is negative; elites can produce Rare → non-Rare → Rare because the last slot has a 0.5% Rare chance after the intervening non-Rare. Catfood preserves these rules.

| Source | Mechanics |
|---|---|
| Hefty Tablet | Choose one of three own-character Rares; Injury is added even if the Rare is skipped. |
| Glass Eye | Five three-card offers: two Common, two Uncommon and one Rare, from the character pool. No random upgrades. |
| Scroll Boxes | Choose one of two bundles, normally two Commons and one Uncommon each, with six distinct identities across ordinary bundles. Defect has an independent 1% triple-Claw substitution chance per bundle; substituted bundles contain three Claws. |
| Room Full of Cheese — Gorge | Choose two of eight distinct Common character cards; no random upgrades. |
| Crystal Sphere | The board has one card-reward item per ordinary rarity. Each recovered item gives a three-card offer of that rarity; vanilla identities and upgrade rolls use event RNG. |
| The Future of Potions | Offers the first three owned potions in slot order; independently assigns a random allowed card type to every owned potion using event RNG. Trade a potion for three explicitly upgraded character cards of the option's rarity/type. Eight combinations exist: Common Attack/Skill and Uncommon/Rare Attack/Skill/Power. Rare/Event potions map to Rare cards; Common/Token to Common. |
| Lead Paperweight / Brain Leech — Rip | Choose one of two / three colorless cards respectively. Rip costs 5 HP and suppresses random upgrades. |
| Kaleidoscope | Two three-card offers; each independently selects three distinct other characters and generates one card from each using base rarity odds. |
| Colorful Philosophers | Choose another character's color, then receive one three-card offer per ordinary rarity from that pool. Ordinary upgrade rolls apply. |
| Sea Glass | Five cards per ordinary rarity from one featured other character; take any number of the 15. No random upgrades. |
| Prismatic Gem / Dingy Rug | Gem (Orobas, Act 2) adds unlocked character pools unless the original pools are all colorless; Rug adds colorless cards, which are only Uncommon/Rare. Both require the card-reward flag and permitted pool modification, and preserve filters. After any rarity roll, sample uniformly among eligible identities, not equally among pools. Brain Leech's Share Knowledge and Room Full of Cheese lack the required flag and remain unchanged; The Future of Potions explicitly blocks pool modification. |
| Lasting Candy | Adds a Power on alternating qualifying combat rewards from the parent reward's modified pool, using the combat source's base rarity odds without its offset. Excludes existing offer identities unless no eligible alternative remains. |
| Wing Charm / earlier enchantments | Wing Charm's late reward-options hook selects one currently Swift-eligible card per generation batch with `Niche` RNG, including direct generation. Swift cannot overwrite or stack with an existing enchantment. Earlier Glitter excludes its Glam-enchanted cards; earlier Fresnel Lens excludes its Nimble-enchanted Block cards. Ordinary modifiers run before late modifiers; late modifiers follow relic-list order. |

#### Permanent transformations and direct additions

**Direct card additions:** sources use shared reward RNG, `Niche` or event RNG. Infested Automaton and Endless Conveyor's Fried Eel roll the base-odds card-offer rates above, with the stated A0–6 direct-addition rates, before identity and independently of combat rarity offsets. Unavailable rarities advance Common → Uncommon → Rare → Common. Arcane Scroll and random curse grants select uniformly from their eligible pools.

**Permanent transformation's target:** callers use event RNG, `Niche` or player `Transformations` RNG. Use the original card's pool, except Quest cards and Event/Ancient/Token-rarity cards use colorless. Token is the generated-card rarity of Shiv, Soul and similar cards. Ordinary results are Common/Uncommon/Rare; curses share one curse pool. Preserve unlock/player restrictions and exclude the original identity. `CardFactory` preserves pool enumeration order through filtering, then consumes one RNG draw for a uniform index into the eligible list; batches allow repeated results. Combat transformations additionally filter by `CanBeGeneratedInCombat` and may use the Status pool.

**Transformation attributes:** Claws copies the original upgrade state and compatible enchantment to Maul (`Claws.CreateMaulFromOriginal`). This gameplay inheritance is separate from initializing fresh hidden selection state.

**Random transformation batches:** Astrolabe, Morphic Grove and Trial iterate returned selection order; Pandora's Box uses deck order; Leafy Poultice uses Strike then Defend. `CardCmd.Transform` resolves replacements before sorting by recorded pile position. Reordering inputs with different eligible lists can change the result multiset, even if only one original identity can replace another. Identical ordered lists preserve the multiset, including ordinary own-character Strike/Defend batches such as Pandora's Box.

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

### Potions

**Potion drops:** one shared probability starts at 40%; a generated drop subtracts 10 percentage points, a miss adds 10. Elites add 12.5 percentage points to the check. No act/Ancient reset. Checks use shared `Rewards` RNG; forced drops skip both the random draw and probability update.

**Potion generation:** ordinary generation rolls 65% Common / 25% Uncommon / 10% Rare, then identity. Factory batches exclude duplicate identities within the batch; separate calls can repeat them. Generation precedes acquisition; failed acquisition does not restore consumed RNG state.

| Source | Mechanics |
|---|---|
| Ranwid / Stone of All Time | Select one owned potion uniformly with event RNG. |
| White Beast Statue | Forces combat potion rewards and is normally tradable at Relic Trader. Freezes the stored drop probability while owned; removal resumes it without a reset. The final boss generates no ordinary reward set and performs no drop check. |
| Alchemize | Generates one potion, excluding Fairy in a Bottle, Fruit Juice and Regen Potion. Attempts generation even with full slots. |
| Entropic Brew | Fills empty slots with unrestricted potion generation, including outside combat. Generates only while slots are open and stops on the first failed acquisition. |
| Delicate Frond | Fills empty potion slots before combat. Uses unrestricted potion generation, generates only while slots are open and stops on the first failed acquisition. |
| Fixed-rarity random potions | Potion Courier's Ransack gives Uncommon; Crystal Sphere specifies the rarity and selects identity with event RNG. |
| Uniform-identity event potions | Battleworn Dummy, Endless Conveyor, The Legends Were True and Wellspring select uniformly from eligible potion identities. |
| Potion factory batches | Initial shop stock: 3; Alchemical Coffer on acquisition: 4; Phial Holster on acquisition: 2. These are the three multi-potion factory callers. |
| Phial Holster | Adds one slot but generates two potions, so its second grant can fail. |
| Cauldron | Shop relic offering five separate potion rewards on acquisition. |
| Potion acquisition failures | Full slots or Sozu can reject acquisition after generation. |

#### Potion card generation

Attack/Skill/Power Potions offer three distinct eligible own-character cards of the specified type; Colorless Potion offers three distinct eligible colorless cards. The player may skip or choose one, which becomes free this turn. Cosmic Concoction adds three distinct upgraded colorless cards; Orobic Acid adds one own-character Attack, Skill and Power, each free this turn.

All six use shared `CombatCardGeneration` RNG and sample uniformly over eligible identities, without a separate rarity roll. Combat generation excludes Basic/Ancient/Event rarities and cards with `CanBeGeneratedInCombat == false`, retaining unlock and player-count restrictions. Dingy Rug and Prismatic Gem do not expand these potions' eligible card pools, matching vanilla.

### Combat gold

Combat gold uses shared `Rewards` RNG, uniformly over default inclusive ranges: normal fights 10–20, elites 35–45. Poverty (A3+) multiplies each endpoint by 0.75 and truncates before rolling, giving 7–15 and 26–33.

**Bowler Hat:** multiplies gold gained by 1.25; `PlayerCmd.GainGold` truncates the modified amount to an integer for each payout.

**Combat-gold exceptions:** Fake Merchant grants a fixed 300 gold, unaffected by Poverty. Battleworn Dummy's three event combats grant no normal combat rewards. Gremlin Merc grants normal gold unless Fat Gremlin escapes: the gold proportion becomes one-half if no gold was stolen, or zero otherwise; vanilla rounds the reduced range endpoints before rolling and generates no gold reward at zero. Non-final bosses grant fixed 100 gold (75 with Poverty); the final boss grants no ordinary reward set. Fixed extra payouts, including recovered stolen gold, Royalties and Amethyst Aubergine, are separate from the normal combat-gold roll.

### Run-save checkpoints

| Situation | Save timing |
|---|---|
| Map-node entry, all room types | After exiting the previous room, before resolving `?`, selecting the event/encounter or generating room contents. Loading regenerates the destination. |
| Ordinary event, shop, rest site, chest | No additional checkpoint for choices, purchases, rewards or completion. Results reach disk at the next checkpoint. |
| Normal, elite and boss combat | Additional checkpoint after victory hooks, before ordinary room-end reward generation and selection. Loading regenerates those rewards. |
| Ancient | Additional checkpoint when the Ancient finishes. Loading restores it as finished. |
| Event-created combat | Starting combat adds no checkpoint; victory creates one with information needed to reconstruct the parent event. |

`Save and Quit` returns to the menu without taking a new snapshot; loading uses the last checkpoint.

### Catfood addressing and combat rewards

**Room addressing:** cumulative normal-fight/elite/boss/shop/event/Ancient counters. Ancients increment both `eventEntered` and `ancientVisited`; options stay independently seeded, while Ancient card overrides use `ancientVisited`. Room reward RNG uses room-type counts, a normal-fight fallback and event-card overrides. Combat entry re-seeds shared `Rewards` using run seed and cumulative combat-category count, with different multipliers for normal fights and elites; gold ranges and rounding are unchanged. Chests have no ordinal: entry re-seeds `Rewards` with the normal-fight count for gold. Silver Crucible's suppression is unchanged.

**Ancient option pools:** with `MassAuditionMode` enabled, Catfood removes Winged Boots from Neow's positive pool and Fur Coat from Nonupeipe's pool. The menu RNG retains vanilla addressing.

**Shops:** track shop ordinal and use the separate card-rarity state below. Card identities/prices and relic bags/rarity rolls retain vanilla generators; potion batches use the factory addressing below.

**Relic draws:** retain shared bags but precompute 400 rarity rolls; roll 401 would fail (code finding, not reproduced).

**Potions:** drop checks use `player seed` + `cumulative check index`, starting at 0, with vanilla's changing probability. Forced drops also advance this index. Potion factories seed each batch with a shared call index.

**Combat card generation:** identities/upgrades use room type, count and offer position. Rarity is rolled before identity; a Rare consumes the current slot's identity draw. There are no independent per-rarity identity sequences. Later identities can still change through rarity-offset resets and offer duplicate exclusion. The initial offer uses the RNG intended for rerolls: a code-confirmed wrong-stream bug, not a wrong ordinal; gameplay impact has not been reproduced.

**Card rarity:** normal fights and elites have separate offsets with vanilla base probabilities, non-rare growth and post-Rare reset value. Shops have a separate offset that does not grow, leaving their Rare roll chance at 0% (4% without Scarcity). Ancient entry, including Neow, resets these offsets to `-0.05`. The separately stored boss offset has no effect; boss rewards no longer reset normal-fight or elite offsets. Combat rerolls update their room type's rarity state, potentially shifting later offers; non-Rare rerolls raise the offset, as in vanilla.

**Direct/event generation:** card factories seed by adding room/event count to a per-room options-call index. Seed collisions exist; harmful gameplay impact remains unverified. Transformations retain caller-supplied RNGs. Neither uses dedicated shared identity sequences by pool.

**Encounter setup:** inherits vanilla event/encounter identity selection; cumulative room counters do not select identities. Encounter ID + room-type ordinal controls variable composition/setup, not ordinary HP. No visible setup mismatch has been reproduced. Lantern Key's forced event is counted normally.

**Shuffles:** re-seeds shuffle RNG at combat start; later shuffles continue it.
