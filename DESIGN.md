# Same-Seed Consistency Mod — design

This specification records settled policies, their rationale and existing-mechanics references. [WORKING.md](WORKING.md) holds unresolved choices and supporting evidence. Unspecified systems retain existing behavior.

**Reading guide:**

- Start with [goals](#goals-and-principles), [scope](#scope) and [terms](#concepts-and-terms).
- Find a source in the [policy guide](#architecture-overview), or compare generation sources in [generation routing](#settled-policy-generation-routing). Policies link to the [shared strategies](#shared-strategies) they use.
- Use [Reference](#reference) for calibration and [Appendix](#appendix-existing-mechanics) for existing mechanics.

**Reading convention:** unlabeled design statements in the main body define required behavior. Labeled baselines and references describe existing mechanics. Deviation notes, rationale, tradeoffs, examples and rejected alternatives explain the design. Reference and Appendix are descriptive only.

## Goals and principles

**Goal:** keep choices and combat challenges comparable across same-seed runs with different routes and decisions. Compare corresponding opportunities—such as each run's first shop or third normal-fight reward—even on different floors.

**Fairness principle — opportunity isolation:** preserve meaningful consequences while minimizing unrelated future gameplay changes. For example, room visits and reward choices should leave unrelated later randomness unchanged.

**Behavior preservation:** all behavioral changes from vanilla, including probability changes, must be deliberate alignment tradeoffs, consistency fixes with no gameplay impact, or fixes to unintended vanilla behavior. Fixes to unintended behavior still receive impact grades; currently classified: [forced-event bypass](#settled-policy-forced-events).

**Same-seed divergence (歪种):** outcome differences from uncontrolled randomness that undermine comparable opportunities, often early or mid-game. Differences caused solely by unequal consumption of intentionally shared sequences are benign, including late-game divergence.

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

**Play modes:**

- **NoSL:** assumes no save/loading; covers in-room and upper-level strategy.
- **SL:** explores every available action sequence from the current save before committing to the next saved state. Assess the best attainable room outcomes, upper-level decisions and in-room future planning, such as which potions to carry onward.

Grade each mode independently: write `NoSL x, SL y` when they differ, `NoSL x` when equal, and `SL x` when only SL is affected.

Rare deviations are appended with a **Frequency:** field.

#### Shared isolation impact

**Vanilla deviation — SL 5(↓):** where isolation removes the ability to strategically steer future outcomes by changing earlier consumption of shared randomness, particularly between combats. It simplifies SL exploration by removing future-planning decisions. This is an opportunity-alignment tradeoff. For example, [combat-local random orbs](#settled-policy-random-orb-types) remove the ability to engineer a favored orb sequence for the next battle.

## Scope

Version: StS2 v0.111.0 and Catfood Mod v1.1.2.

Compare single-player runs with matching seeds, versions and rules, with all content unlocked (including relic, card and event pools).

**Exclude:** multiplayer behavior, tutorial overrides (including scripted rewards and boss discovery order), custom-run modifiers, and content removed by game updates.

**Numerical scope:** exclude negligible probability differences caused solely by rounding or exact boundary hits in random sampling, game-wide.

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
- **Event RNG:** an event's own generator.
- **Logical occurrence / physical copy:** a logical occurrence is a persistent per-copy selection identity; a physical copy is the card or potion object currently associated with it.
- **Occurrence address:** the stable source identifier of a logical occurrence, distinct for each copy.
- **Selection key / selection-key alias:** a selection key identifies an occurrence for a selection purpose; an alias is a source-specified replacement key for that purpose.
- **Selection state:** an occurrence's address, selection keys and aliases, priorities or residual clocks, and renewal counts.

## Architecture overview

Each controlled random operation has five descriptive dimensions:

| Dimension | Describes |
|---|---|
| Outcome | What is randomized. |
| Sharing scope | Which operations share randomness. |
| Address | How corresponding opportunities or entries are identified. |
| Form | Addressed draw, stream, finite allocation or another coupling mechanism. |
| State | What changes, resets and must survive save/load. |

Different stages of one source—such as occurrence, rarity, identity and attributes—may use different sharing scopes, counters or sequences.

Use [shared strategies](#shared-strategies) for common contracts and [generation routing](#settled-policy-generation-routing) to compare rarity and identity sharing. The guide below links to source policies.

### Rewards

| Decision area | Policies |
|---|---|
| Relics and shops | [Relic allocation and borrowing](#settled-policy-relics); [chest rewards and advancement](#settled-policy-chests); [shops, Wongos and Courier](#settled-policy-shops-wongos-and-courier). |
| Potions | [Generation and drops](#settled-policy-potions). |
| Cards | [Reward offers and Rare occurrence](#settled-policy-card-reward-offers); [generated upgrades](#inherent-card-rolls); [permanent transformations and direct additions](#settled-policy-permanent-transformations-and-direct-card-additions). |
| Other rewards | [Combat gold](#settled-policy-combat-gold); [event-specific rewards](#settled-policy-event-specific-rewards). |

### Encounter

| Decision area | Policies |
|---|---|
| Card and potion selection | [Wing Charm](#settled-policy-wing-charm); [persistent selection](#settled-policy-persistent-selection). |
| Event selections and trades | [The Future of Potions](#settled-policy-the-future-of-potions-card-type-assignment); [Ranwid relic sacrifice](#settled-policy-ranwid-relic-sacrifice); [Relic Trader](#settled-policy-relic-trader); [Endless Conveyor](#settled-policy-endless-conveyor). |
| Potion effects | [Potion-generated cards](#settled-policy-potion-generated-cards); [Snecko Oil costs](#settled-policy-random-cost-values). |
| Rooms and events | [Unknown rooms](#settled-policy-unknown-room-resolution); [Ancient offerings](#settled-policy-ancient-offerings); [ordinary events](#settled-policy-ordinary-event-selection); [forced events](#settled-policy-forced-events). |
| Combat | [Encounter setup](#settled-policy-encounter-setup); [card shuffling](#settled-policy-card-shuffling); [random pile insertion](#settled-policy-random-pile-insertion); [monster HP](#settled-policy-monster-hp); [monster AI](#settled-policy-monster-ai); [random creature targets](#settled-policy-random-creature-targets); [random orb types](#settled-policy-random-orb-types); [card generation and transformations](#settled-policy-combat-card-generation-and-transformations); [random cost values](#settled-policy-random-cost-values). |

## Shared strategies

The [fairness principle](#goals-and-principles) and labeled invariants are shared constraints. All contracts below apply to covered policies; source policies add sharing scopes, local rules and exceptions.

### Addressing and state

#### RNG derivation and addressing

**Invariant — independent derivation:** new or re-addressed randomness derives from run seed + stable sharing-scope and purpose identifiers + the address components specified by its policy. The same full address reproduces the same draw or stream; independent purposes use separate derivations and state. Share randomness only where a policy specifies sharing or coupling. Explicitly retained baseline RNGs keep their stated addressing.

Run seed is implicit in local and candidate addresses for new or re-addressed randomness. In design address expressions, `+` means unambiguous composition of components, not arithmetic addition. Use stable identifiers rather than display labels; for streams, distinguish the stream's address from its current position. Concrete labels, encoding and RNG/hash algorithms can be chosen during implementation, subject to this contract.

In RNG addresses, a scoped ordinal denotes `(scope identity, count)`: act-local includes act number; event-local includes event ID.

**Selection keys:** derive initial keys from occurrence addresses, applying [source-specific aliases](#card-reward-selection-keys); never use current list position.

**Selection tie-breaks:** tie-break extremely rare equal random-selection priorities by comparing the candidate keys already used to generate them, in ascending order.

#### Occurrence identities

Authoritative address reference. Policies choose which identities and purposes supply their randomness.

##### Event addresses

Events use event ID. Event RNG uses run seed + event ID in single-player, independently of event ordinals.

##### Stable entry addresses

**Invariant — stable addresses:** entries retain their original addresses through movement, filtered advancement, borrowing, removal and return.

**Invariant — presentation-only reordering:** change only display positions, keeping each entry's address, attached random state and group membership with it.

An entry's address is its original source sequence + original entry position, distinguishing occurrences of repeated identities.

##### Combat addresses

Ordinary combats use combat category + act-local combat ordinal + encounter identity, with separate normal-fight, elite and boss counters.

Event-created battles outside ordinary encounter lists use event-local battle ordinal + encounter identity, leaving ordinary combat counters unchanged.

Combat-local purposes derive from this address + purpose and reset each combat. Apply [shared isolation impact](#shared-isolation-impact) where this removes cross-combat outcome steering; source policies retain their within-combat deviation assessments.

Preserve settled scopes, including [potion-generated cards](#settled-policy-potion-generated-cards); use [shared consumption timing](#opportunity-consumption-timing).

##### Monster occurrence identity

For combat policies addressing individual monsters, monster address = source-local spawn opportunity + creation index, stable for the monster's lifetime.

Initial enemies use the [combat address](#combat-addresses) as their source; summons/replacements use the summoning/replaced occurrence.

One spawn opportunity covers the initial lineup or one summon/replacement trigger; number its enemies in vanilla creation order, regardless of animation timing.

##### Card and potion occurrence addresses

Each card or acquired potion copy has a distinct logical occurrence with a stable source address; apply the [lifetime rules](#logical-occurrence-lifetime).

Cards with generation entries use their [stable entry addresses](#stable-entry-addresses). Other cards use source opportunity address + copy index. Assign indices in the [Deterministic Card Processing Order](#deterministic-card-processing-order) after establishing gameplay properties, with a distinct index for each equivalent copy. Treat the starting deck as one initial source opportunity.

Card initialization preserves generated attributes and presentation order; derive selection keys under the [shared addressing contract](#rng-derivation-and-addressing).

#### Card reward selection keys

When a card reward offer allows at most one card, the acquired card uses the first card's selection key as an alias for later selection by Pael's Tooth, Beautiful Bracelet, Slippery Bridge and Thieving Hopper. Freeze the anchor in canonical offer generation order, before presentation-only swaps.

Different offers, including [rerolls](#rerolls), have distinct anchors. Offers allowing multiple cards, including bundles, keep each acquired card's own selection keys even when only one is taken. Selecting existing deck cards creates no reward aliases.

#### Logical-occurrence lifetime

**Scope:** [persistent selection](#settled-policy-persistent-selection) and [representative routing](#representative-routing), using the [occurrence address reference](#card-and-potion-occurrence-addresses). Apply the action's [selection and renewal updates](#settled-policy-persistent-selection) alongside these lifetime rules. Extension to combat-card state remains open.

| Operation | Occurrence and selection state |
|---|---|
| Acquire a generated card | Preserve its address and source-defined selection keys, including [reward aliases](#card-reward-selection-keys), regardless of which other offered cards are taken. |
| Move, temporarily store, or recreate the physical object for an existing occurrence | Retain the logical occurrence and its selection state; see [Tooth's return-record rule](#fixed-priority-card-selection). |
| Change status or eligibility | Retain the occurrence and its priorities or residual clocks through equivalence-class splits and merges; eligibility governs selection participation. [Upgrade/downgrade refills](#permanent-upgrades-and-downgrades) remain source-specific. |
| Copy a card | Create a fresh occurrence from the granting source. Leave the original's selection state unchanged and consume no routing clocks. |
| Permanently remove or combine cards; player-chosen [potion use, discard or trade](#potion-departure-routing) | Retire the selected logical inputs. |
| Permanently transform a card | Retire the original. Apply [source-specific gameplay inheritance](#permanent-transformations-and-direct-additions), then initialize fresh replacement selection state. |
| [Route equivalent copies](#representative-routing) | Permute associations with physical copies while keeping each occurrence's selection state together. |

**Related reward-entry exception:** [A0–6 replacement state](#a06-multiple-rares).

#### Opportunity consumption timing

**Invariant — consumption timing:** for an operation that consumes an opportunity, its random state, including selection clocks, stays unchanged until its result becomes available for the player to reveal or receive. It advances at that point, regardless of actual viewing, acceptance or selection.

Deferred rewards consume at the payoff opportunity, not acquisition of the effect creating it.

**Exception:** [potion effect opportunities](#potion-effect-opportunities) also advance on discard, trade and covered reward skips.

#### Potion effect opportunities

**Scope:** [potion-generated cards](#settled-policy-potion-generated-cards) and [Snecko Oil costs](#settled-policy-random-cost-values).

Each covered potion type has its own run-wide effect ordinal:

| Action | Effect ordinal |
|---|---|
| Committed use, including an empty effect batch | Consume one ordinal for the batch. |
| Discard or trade | Skip one batch. |
| Permanently skip an unclaimed covered potion reward on the reward screen | Consume one ordinal, once per skipped reward. |
| Acquire | Leave unchanged. |

**Cauldron exception:** leaving a Cauldron reward unclaimed or discarding an acquired Cauldron potion before entering the next room leaves the ordinal unchanged. After entering the next room, acquired Cauldron potions follow the ordinary rule.

**Vanilla deviation — SL 5(↑):** discard, trade and covered reward skips advance the next same-type effect batch.

#### Saved state

Preserve [vanilla's run-save checkpoints](#run-save-checkpoints). A reload discards changes since the last checkpoint. Restore mod state before room regeneration so replay advances from the same saved state.

At each checkpoint, save the following wherever used:

- **Sequences:** contents, positions, original entry addresses and consumption.
- **Markers:** positions, conditions, remaining lifetimes and carried entries.
- **Opportunities:** counters and RNG state; chest-offer history and pending acquisition/skip decisions.
- **Selection:** selection state, logical-to-physical occurrence associations and pending fixed-cohort selections.

### Generation

#### Rarity and identity sequences

**Main idea:** share rarity rolls and identity order across related rewards, with independent sharing scopes and sequence state.

**Sharing rationale:** generic rewards represent successive opportunities for the same kind of item, regardless of route. Shop isolation preserves corresponding inventories; isolating Courier and potion-generating effects prevents repeated use from displacing ordinary rewards. Transformations and direct additions share identity order because both generate permanent cards from a pool; separate rarity rolls preserve their different odds.

**Scope:** relics, potions, cards, permanent transformations and covered direct additions.

With per-rarity identity sequences, a random-rarity request consumes one value from its **rarity-roll sequence**, then draws an eligible item from that rarity's **identity sequence** using the [filtered draw strategy](#filtered-draws). Fixed-rarity requests consume identity only; pools with one identity sequence draw directly. [Generation routing](#settled-policy-generation-routing) specifies sharing and RNG exceptions.

**Generation and lifetime:** covered potion/card identity and rarity-roll sequences generate on demand indefinitely, using independent RNGs for the specified source and pool divisions. Each newly generated potion/card identity entry is an independent uniform draw with replacement from its sequence's pool unless the source specifies otherwise, such as weighted or grouped generation. Only finite relic allocations borrow.

#### Filtered draws

**Main idea:** take the next eligible item without pulling skipped items toward the front of later draws.

**Advancement:** return the first eligible entry, then move each later eligible entry to the preceding eligible position. Ineligible positions remain unchanged. For example, if eligible entries `A`, `B`, `C`, … occupy positions 2, 6, 8, …, return `A`, move `B` to 2, `C` to 6, and so on.

This generalizes fixed-rarity draws: viewing per-rarity identity sequences and rarity rolls as one mixed sequence, later entries of a rarity fill earlier positions of that rarity.

**Method — deferred advancement:** perform the advancement on demand for infinite sequences and finite relic allocations:

- **Filtered draw:** return the first eligible entry and replace it with a marker for its filter.
- **Marker resolution:** move the next eligible entry after it into the marker's position. Leave the same filter's marker at the supplying position.
- **Ordinary draw:** resolve any front marker, then return the entry and remove the drawn position.

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

**Intent:** give unordered card batches a common usefulness-based processing order for [new occurrence addresses](#card-and-potion-occurrence-addresses), permanent and combat transformations and [combat shuffling](#settled-policy-card-shuffling).

**Status:** preliminarily settled — ready for use, with fine-tuning remaining. The [detailed ranking and state comparisons](CARD_ORDER.md) form part of this specification.

Use a fixed, authored usefulness ranking of card identities and their gameplay-relevant statuses. Identify cards by stable model IDs.

Identical cards remain tied regardless of selection state, acquisition order or physical position; retain every copy. Tied-copy permutations must leave gameplay outcomes and subsequent random state equivalent under the [equivalent-copy contract](#equivalent-card-selections). This order does not replace per-copy randomness or its routing.

Source policies specify applicable parts of this order. It controls random-operation processing, not display order or intentionally sequential effects; reward offers retain their existing entry order as canonical generation order.

Shuffle outcomes must be independent of original deck sequence; use the [card-shuffling policy](#settled-policy-card-shuffling).

#### Unordered transformation batches

Process the selected originals in the identity-only [Deterministic Card Processing Order](#deterministic-card-processing-order) first. Original upgrade/enchantment state does not affect this ordering; repeated requests for the same identity remain tied and retain their multiplicity. After generation, associate replacements in generation order with equivalent selected input positions in frozen presentation order.

#### Fixed selection priorities

**Scope:** a single choice or effective batch, at most once per run.

Each candidate is given an independent uniform priority `U_i ∈ (0, 1)` derived from its selection key + selection scope/purpose. Select the lowest eligible priorities.

**Weighted single choice:** use priority `-ln(U_i) / w_i` for weight `w_i`; select the lowest. Selection probability is `w_i / Σw`. Fixed relative weights preserve survivors' ranking when candidates are removed.

#### Corrected persistent clocks

**Main idea:** for uniform occurrence selection across opportunities, retain residual exponential clocks and correct eligible survivors after each random selection. [Source settings](#settled-policy-persistent-selection) specify sharing and local clock rules.

**Address and state:** each logical occurrence starts with a clock `e_i = -ln(U_i)`, with independent `U_i ∈ (0, 1)` derived from its selection key + selection scope/purpose.

Where renewal uses a per-occurrence count, derive each independent draw from selection key + selection scope/purpose + renewal count.

**Selection and update:**

1. Select the eligible occurrence with the smallest clock, `m`.
2. On commitment, consume its clock and subtract `m` from every unselected eligible survivor. Ineligible occurrences retain their clocks without subtraction or renewal.

Source policies define when and how to renew consumed clocks from the same distribution.

**Batches:** for an unordered uniform sample of `k` distinct occurrences from a fixed eligible set, select the `k` smallest pre-selection clocks and subtract the `k`-th smallest clock from every unselected eligible survivor once. Consume each selected clock once; a selected occurrence cannot re-enter the same batch. Commit from one eligibility snapshot, independently of UI click/enumeration order. Effects with intentionally sequential eligibility changes retain that distinction.

A player-directed choice does not advance a gameplay selector's clocks merely because it changes membership; any hidden equivalent-copy routing has its own scope. Fixed-target and explicit affect-all effects also consume no selection clocks.

**Rationale and limit:** the exponential memoryless property preserves uniform selection probabilities while retaining the relative order of jointly participating survivors. This does not preserve one global ranking across different eligibility histories. For example, let `A`, `B`, `C` have clocks `0.5`, `0.6`, `0.7`: if only `A` and `C` participate and `A` is selected, the surviving `C` now outranks the inactive `B`.

#### Representative routing

**Scope:** out-of-combat [player-selected cards](#equivalent-card-selections) and [player-chosen potion departures](#potion-departure-routing), including combat departures. These policies define equivalence and operation sharing.

Choose representatives within each equivalence class using [corrected persistent clocks](#corrected-persistent-clocks). An already randomly selected logical target bypasses representative routing.

**Presentation boundary:** apply effects to the clicked copies; preserve display order.

**Batch association:** use the shared clock strategy's eligibility snapshot and freeze presentation order with it. Within each equivalence class, pair selected occurrences in ascending pre-selection routing-clock order with clicked copies in frozen presentation order; pair remaining occurrences in stable occurrence-address order with unclicked copies in that order. Apply effects after all representatives are selected.

Commit the association permutation and shared clock update with the action. Renew each selected routing clock once if its logical occurrence survives, using its per-occurrence renewal count. Player-selected singleton and whole-class choices follow this same rule.

**Coupling limit:** shared independent priorities on matched nested occurrence cohorts give two-versus-three representative agreement of `2/3`, maximal for uniform selection. Different selection keys or eligibility histories can reduce agreement.

#### Opportunity-specific selection priorities

Apply [fixed-priority sampling](#fixed-selection-priorities) afresh per opportunity. Derive priorities from opportunity address + candidate key + purpose, excluding candidate sets and list positions. Sources define keys, sharing and opportunity counting.

## Settled policy: generation routing

**Routing** specifies the random state supplying each generation stage.

Apply the [shared generation strategies](#generation); source policies retain eligibility, construction and consumption details.

### Relics, potions and event-specific rewards

| Request / source | Rarity state or rule | Identity supply | Grouping / exceptions |
|---|---|---|---|
| Ordinary relics: generic rewards, shop and Courier | One rarity-roll sequence per source. | Disjoint finite [allocations](#allocation): independently advancing, shuffled Common/Uncommon/Rare sequences per source; shop also has all Shop-rarity identities in one shuffled sequence. | Individual draws; [source classification](#source-classification-and-consumption), [chest advancement](#relic-advancement) and [Wongos](#stock-and-prices) apply. |
| Relic Trader replacements | Ordinary relic rarity, addressed by event ID + reward slot. | Last eligible Courier entry of the rolled rarity. | Slots 1–3 in generation order; no added filters. |
| Generic potion rewards | Separate sequences for [standard weights](#potions) and uniform-identity selection; uniform mode weights rarities by eligible identity counts. | Both modes share generic Common/Uncommon/Rare identity sequences over the full eligible pool. | Individual potions. |
| Potions: shop / Courier / potion-generating effects | One standard-weight rarity sequence per source. | Separate Common/Uncommon/Rare identity sequences per source, each using the full eligible potion pool. | Individual potions; [source membership](#acquisition-and-sequences). |
| [Event-specific rewards](#settled-policy-event-specific-rewards) | Event RNG where rarity is random. | Event RNG. | — |

### Card offers and shop cards

For card reward offers, group identities by pool, rarity request, eligibility filter and draw grouping. Upgrade rules and the number of cards taken leave grouping unchanged.

**Own-character random-rarity offers** share [luck and thresholds](#luck-and-rarity-offset), [non-Rare and rare-bearing triples](#offer-sequences), and [A0–6 extra Rares](#a06-multiple-rares). Luck controls Rare delivery; triples supply the base Common/Uncommon composition. The three groups differ in Rare odds and offer size:

| Group | Rare odds | Sources and offer size |
|---|---|---|
| Normal-fight rule | Normal-fight chance with the shared combat offset. | Normal fights, Dream Catcher, Prayer Wheel and The Hunt in normal fights: three-card offers. |
| Elite rule | Elite chance with the same combat offset. | Elites and The Hunt in elite combats: three-card offers. |
| Fixed base odds | Base Rare odds without the combat offset. | Orrery: five three-card offers; Lost Coffer: one; Trial's Nondescript Guilty: two. Brain Leech's Share Knowledge: choose one of five, using [supplementary cards](#supplementary-cards). |

**Other offers and shop cards:** the expanded-pool row also applies to the random-rarity groups above.

| Request / source | Rarity state or rule | Identity supply | Grouping / exceptions |
|---|---|---|---|
| Initial shop cards | Shop-ordinal generation using [shop odds](#stock-and-prices). | Shop-ordinal generation. | — |
| Courier cards | Separate Attack/Skill/Power rarity sequences using shop odds; colorless requests fixed Uncommon or Rare. | Per-type, per-rarity sequences for Attack/Skill/Power; one sequence each for Uncommon and Rare colorless. | Individual restocks. |
| Own-character fixed-Rare offers | Fixed Rare. | Separate Rare triple sequence. | One triple per offer: non-final bosses, White Star, The Hunt's delivered boss rewards, Hefty Tablet, Glass Eye's Rare offer and Crystal Sphere's Rare offer. |
| Own-character fixed-Common offers | Fixed Common. | Separate Common triple sequence. | Glass Eye: two triples; Crystal Sphere: one. [Room Full of Cheese](#supplementary-cards) and [Scroll Boxes](#scroll-boxes) have local rules. |
| Own-character fixed-Uncommon offers | Fixed Uncommon. | Separate Uncommon triple sequence. | Glass Eye: two triples; Crystal Sphere: one. |
| Own-character Power: Lasting Candy / The Future of Potions | Candy: separate normal-fight and elite rarity sequences using source base odds and unavailable-rarity fallback, one roll per triggered card; boss triggers request Rare. Future of Potions requests its specified rarity. | Shared Uncommon and Rare Power identity sequences. | Individual entries. |
| The Future of Potions: Attack / Skill offers | Specified rarity. | Event RNG addressed by rarity and type, independently of the other potion options. | — |
| Colorless offers: Lead Paperweight / Brain Leech's Rip | Existing Uncommon/Rare odds: **98.51% / 1.49%**. | One shared reward sequence. | Individual entries across batch sizes. |
| Other-character offers | Kaleidoscope: event RNG for character and rarity. Colorful Philosophers / Sea Glass: fixed rarities. | **12 shared identity sequences: four other characters × Common/Uncommon/Rare**. | Individual entries across batch sizes. |
| Prismatic Gem / Dingy Rug: eligible expanded-pool offers | Source rarity state and rules. | [Expanded-pool identity sequences](#prismatic-gem-and-dingy-rug). | Source draw grouping. |

### Other card generation

| Request / source | Rarity state or rule | Identity supply | Grouping / exceptions |
|---|---|---|---|
| Permanent transformations / [covered direct additions](#settled-policy-permanent-transformations-and-direct-card-additions): own-character and colorless pools | Separate transformation/addition rarity sequences per pool. Transformations weight by current eligible identity counts per rarity, excluding the original; additions use vanilla base odds and unavailable-rarity fallback. | Shared Common/Uncommon/Rare identity sequences per pool. | Individual entries; separate from combat transformations. |
| Infested Automaton: Study / Touch Core | Event RNG; leaves the addition rarity sequence unchanged. | Same permanent own-character identity sequences as the preceding row. | — |
| Permanent transformations / [covered direct additions](#settled-policy-permanent-transformations-and-direct-card-additions): other-character and curse pools | Direct identity draws. | One sequence per pool: four other characters and curse. | Individual entries; separate from combat transformations. |
| [Potion-generated cards](#settled-policy-potion-generated-cards) | Uniform eligible identities, without a separate rarity roll. | Complete batch addressed by potion type + its [effect ordinal](#potion-effect-opportunities). | Separate from other combat card generation. |
| [Combat card generation and Entropy](#settled-policy-combat-card-generation-and-transformations) | Uniform eligible identities, without a separate rarity roll. | Combat-local identity sequences by pool and source cost treatment; dedicated Metamorphosis and Splash sequences. | Individual entries; preserve source batch uniqueness. |

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
- **Skip or leave behind:** save the relic with the marker as its **carried entry**; at expiry it returns as an [ordinary reward entry](#opportunity-consumption-timing).

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

### Offer procedure

Follow [generation routing](#settled-policy-generation-routing). For own-character random-rarity offers, including reachable [rerolls](#rerolls):

1. Simulate three slots (all five for Brain Leech's Share Knowledge) under [Rare occurrence](#rare-occurrence).
2. Consume the next [rare-bearing triple](#offer-sequences) if a Rare was delivered, otherwise the next non-Rare triple.
3. Append any [supplementary cards](#supplementary-cards), then apply [A0–6 extra-Rare replacements](#a06-multiple-rares) where applicable.

Replacements precede reward enchantments; [presentation swaps](#presentation) follow step 3. Preserve source-specific upgrade timing and modifier order.

### Rare occurrence

**Scope:** the three own-character random-rarity groups in [generation routing](#settled-policy-generation-routing).

**Intent:** separate when a Rare appears from the complete choice offered. Align corresponding rare-bearing and non-Rare opportunities while preserving source-dependent Rare chances; matching combat-room ordinals need not consume matching offer entries.

#### Luck and rarity offset

**State:** luck `L` starts at zero; the combat offset `o` starts at `-0.05`. The threshold is `T = -ln(U)`, where `U` is uniform on `(0, 1)`. Thresholds use an independent sequence addressed by purpose and threshold ordinal, advancing only when a Rare is delivered into a generated offer.

**Per slot:** process card slots in order. Each adds `-ln(1 - p)` to luck, where `p` is its Rare probability:

- Normal-fight-rule and elite slots use `max(0, base Rare chance + o)` with the shared combat offset.
- Fixed-base-odds slots use their base Rare chance without reading or modifying `o`.

**Rare delivery:** subject to the difficulty-specific cap below, a slot reaching the threshold becomes Rare. Reset luck to zero, discard the overshoot and advance to the next threshold. Common/Uncommon identities and their rarity mix come from the offer sequences, independently of this simulation.

**Combat offset updates:**

- Normal-fight-rule and elite non-Rare slots add `0.005` (`0.01` without Scarcity), capped at `0.40`. Zero-probability slots still grow the offset while adding no luck.
- A delivered Rare in either group resets the offset to `-0.05`.
- Retain boss-reward offset resets, without advancing luck or these offer sequences.
- Between acts, reset the offset to `-0.05`; carry luck, the current threshold and sequence state forward.

Save this state under the [shared saved-state contract](#saved-state), including an already-reached threshold awaiting delivery.

**Rationale:** before deferral, `P(no Rare through n slots) = exp(-sum(-ln(1 - p_i))) = product(1 - p_i)`. Exponential thresholds preserve the supplied waiting probabilities without fitting a route-dependent luck distribution. This does not make the capped, deferred reward system identical to vanilla.

**Competition effect — luck coupling:** with matched luck, threshold, offset and three-card offers, an elite leaves more luck than a normal fight if neither delivers a Rare. The lead survives boss rewards and act transitions; with equal later increments, the elite branch reaches the shared threshold no later. Rare delivery resets luck and advances the threshold, ending this timing guarantee.

#### A7+: one Rare per offer and deferral

Cap each covered offer at one Rare. If a later slot reaches the next threshold:

- Suppress that Rare and keep the reached threshold pending, retaining luck and the current threshold.
- Treat suppressed slots as non-Rare for applicable offset updates.
- Continue accumulating luck; only one occurrence can be pending until delivery.

A **Rare-eligible offer** has a positive chance of producing a Rare along its ordinary all-non-Rare progression, ignoring pending delivery.

- **Next eligible offer:** deliver the pending Rare in its first simulated slot instead of that slot's ordinary check. Reset luck, clear pending and advance the threshold once, applying the delivery source's offset rule. Process remaining slots normally.
- **Ineligible offer:** retain the pending threshold and apply normal non-Rare offset growth.

This naturally excludes the first two three-card normal-rule offers after an offset reset without elite intervention; use probability eligibility rather than room counts. Delivery follows offer generation order, including extra offers and rerolls, and can wait across acts.

**Vanilla deviation — NoSL 3:** extra Rares in elite and fixed-odds offers are deferred, changing acquisition opportunities; occurrences beyond the single pending threshold are omitted. **Frequency:** rare — approximately **0.3% of vanilla A7+ runs** contain a dual-Rare offer from these sources (project-owner estimate).

#### A0–6: Rare delivery

Deliver every threshold crossing immediately under the shared luck and source-offset rules, without a per-offer cap or deferral.

### Offer construction

#### Offer sequences

Own-character random-rarity offers use two deterministic triple sequences independent of the luck thresholds. The **non-Rare sequence** supplies three Common/Uncommon cards per triple; the **rare-bearing sequence** supplies one Rare and two non-Rares. Store the Rare first in each rare-bearing triple.

A suppressed occurrence reserves no additional triple.

**Rationale:** sharing preserves both triples when a Rare moves between sources; separate normal/elite sequences would advance differently.

#### Uncommon probabilities

Use these Uncommon probabilities, except for the opening entries below:

| Difficulty | Non-Rare offers | Rare-bearing companions |
|---|---|---|
| A7+ | 38% | 39% |
| A0–6 | 40% | 40.5% |

These rounded rates approximate expected displayed Uncommon totals in the [three-act calibration](#rarity-calibration). Shop offsets do not apply.

**Vanilla deviation — NoSL 3:** shared Uncommon rates remove elites' advantage (about 0.14 extra Uncommons per three-card non-Rare offer near zero starting offset) and change source- and route-specific rates. Few elite rewards, some Rare-bearing, limit the impact; elites retain relic, gold and higher Rare-chance incentives.

**Opening triples:** the first two non-Rare triples use their own deterministic generation with vanilla first-two-normal-offer probabilities after an offset reset, conditional on no Rare. At A0–6 these are **35%, 36%, 37%**, then **37/99, 37/98, 37/97**.

This exception occurs only once per run. The triples follow ordinary shared consumption, including by other covered sources.

**Rationale:** these entries approximate two opening Act 1 normal fights without elite intervention. Normal-fight openings in Acts 2/3 are accounted for through average calibration rather than special entries.

#### Supplementary cards

**Brain Leech — Share Knowledge:** generate two additional Common/Uncommon cards with event RNG, excluding all identities already selected; use the ordinary non-Rare Uncommon probability if no Rare is delivered, otherwise the rare-bearing companion probability.

**Room Full of Cheese:** use one Common triple and generate the remaining five Common cards with event RNG.

#### A0–6: multiple Rares

For own-character random-rarity offers, maintain one additional Rare-only identity sequence per character, shared by the three source groups and independent of fixed-Rare triples. For an offer with `k >= 1` Rares, draw `k - 1` entries from this sequence.

Replace the first Common in canonical generation order, or the first Uncommon if no Common remains, for each extra Rare. Apply this across the entire offer, including Brain Leech's two event-generated cards: `R,U,U | C,C` becomes `R,U,U | R,C` for two Rares. Repeat for three, four or five Rares as needed. Use [filtered draws](#filtered-draws) to exclude the original Rare and earlier extra Rares; replaced companions remain consumed.

**Replacement state:** identity and inherent upgrade roll come from the new Rare entry. Retain the replaced slot's Wing Charm priority or supplementary-card membership, and its [Acts 1–2 shuffle membership](#source-based-assignment--acts-12).

**Accepted tradeoff:** extra-Rare sequence state depends on prior Rare counts; companion replacements depend on offer composition. These A0–6 alignment and distribution tradeoffs are accepted because the design primarily targets A7+.

**Vanilla deviation — NoSL 3:** Common-first replacement adds a preference for retaining Uncommon companions in multi-Rare offers, unlike vanilla's per-slot generation. **Frequency:** rare — at A0–6, approximately **0.265% of vanilla fixed-base-odds three-card offers** contain at least two Rares, calculated from the 3% per-slot Rare chance.

#### Presentation

For own-character random-rarity offers, swap the original Rare, then extra Rares in draw order, into the simulated Rare positions in ascending slot order.

#### Scroll Boxes

Resolve Defect's two independent 1% triple-Claw substitution checks with event RNG before requesting Common sequence entries. Non-Defect offers follow the no-substitution branch.

- **No substitution:** consume one Common triple. It supplies both Common cards in bundle one and the first Common card in bundle two; generate the remaining Common and both Uncommons with event RNG.
- **Triple-Claw exception:** if either bundle is replaced, bypass the shared Common sequence without inspecting its head. Each substituted bundle contains three fixed Claws; generate any remaining ordinary bundle entirely with event RNG. If both bundles are substituted, generate no unused ordinary-bundle cards.

### Rerolls

A reachable reroll follows its source's [generation route](#settled-policy-generation-routing) and, where applicable, the [offer procedure](#offer-procedure), consuming fresh entries from its pool/request/filter group, including expanded pools. Preserve source-specific rarity and offset effects, offer structure and duplicate rules.

Event-generated supplementary cards use fresh local generation without restarting the original offer's address.

Kaleidoscope's Neow offers cannot be rerolled with Orobas's Driftwood in the normal-run scope.

### Prismatic Gem and Dingy Rug

**Intent:** align rewards from the same expanded pool.

Preserve the [existing pool-modification boundaries](#card-reward-offers). Select the identity group for the complete request:

- **Unchanged eligible pool:** reuse its existing group, including Rug's fixed-Common or already-colorless offers.
- **Expanded eligible pool:** use independent sequences for the effective expanded pool, retaining source draw grouping. All results, including own-character cards, consume this group; ordinary identity sequences and triple boundaries remain unchanged.

Retain source rarity, luck and upgrade rules, eligible-card sampling weights, offer sizes and duplicate exclusions. Neither relic resets luck state or restarts opening-only rarity exceptions.

Lasting Candy uses the inherited pool's Power sequences while retaining its source rarity-roll sequence position and fallback rules.

**Accepted tradeoff:** separate identity groups sacrifice alignment with ordinary own-character identities to avoid acquisition-dependent reconstruction; prior shared rarity state still matters.

## Settled policy: permanent transformations and direct card additions

**Intent:** align same-pool transformations and covered direct additions, preserving source rarity odds and isolating them from card reward offers and combat transformations.

**Covered direct additions:** Infested Automaton's Study and Touch Core, Endless Conveyor's Fried Eel, Neow's Arcane Scroll and Neow's Bones curse, and Distinguished Cape.

### Selection and consumption

Use the permanent-card routes for seven pools: the five characters, colorless and curse.

- **[Transformations](#logical-occurrence-lifetime):** retain [vanilla target pools, eligibility and gameplay inheritance](#permanent-transformations-and-direct-additions).
- **Unordered transformation batches:** use [shared allocation](#unordered-transformation-batches).
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

Apply [A0–6 replacement state](#a06-multiple-rares) and [replacement ordering](#offer-procedure). Evaluate resulting cards' Swift eligibility when Wing Charm runs, after earlier modifiers. No eligible cards means no effect; select an eligible singleton directly.

**Triple-backed offers:** assign independent uniform `[0,1)` Wing Charm priorities to the three canonical entries through their [stable entry addresses](#stable-entry-addresses), and select the lowest-priority eligible card, subject to the exceptions below. The [canonical triple](#settled-policy-card-reward-offers) precedes presentation swaps.

**Non-Rare larger offers:** use the canonical triple in Brain Leech and Room Full of Cheese; if none of its cards is eligible, select uniformly among eligible supplementary cards with event RNG. Restricting selection to interchangeable slots aligns the shared triple while preserving marginal gameplay outcomes, subject to the opening-triple deviation below.

**Weighted extra-group selection:** for rare-bearing Brain Leech and Lasting Candy, let `x` count all eligible cards and `y` eligible extra cards. Select the extra group when the source's independent uniform `[0,1)` roll is below `y/x`; otherwise use the triple's priorities. This gives each eligible card probability `1/x`, including replacement Rares.

| Source / extra group | Group-selection roll | Selection within the extra group |
|---|---|---|
| Rare-bearing Brain Leech: two supplementary positions | Event RNG. | If both cards are eligible, choose uniformly with an independent event-RNG draw under a separate purpose. Matching eligible extras retain the same target whenever both runs select the group. |
| Lasting Candy: added Power | Power sequence entry + Wing Charm purpose, evaluated only if the Power is eligible; otherwise use the triple directly. | Select the Power. Qualifying combat offers have three base cards, so there are no other supplementary candidates. |

**Other batches:** use the same entry-derived priorities for every card and select the lowest eligible one. Other-character offers share per-entry priorities across Sea Glass and Colorful Philosophers despite different batch sizes. No separate advancing Wing Charm counter is required.

**Vanilla deviation — NoSL 1:** larger non-Rare offers favor enchanting positions in the canonical triple.

**Vanilla deviation — NoSL 3:** when Brain Leech consumes an opening non-Rare triple, its different rarity probabilities slightly change the enchanted card's rarity distribution.

## Settled policy: persistent selection

**Intent:** align affected cards across sources, decks and reward choices, and potion sacrifice offers independently of inventory order.

Use [fixed priorities](#fixed-selection-priorities) or [corrected persistent clocks](#corrected-persistent-clocks) as specified below. Preserve source access, triggers, counts, eligibility, costs, rewards and effect order, subject to local rules.

| Selection / candidates | Strategy and sharing | Local selection rules |
|---|---|---|
| Random permanent upgrades / downgrades of existing deck cards | Corrected clocks; one shared scope per operation. Source filters do not split clocks. | Mark consumed clocks zero; apply the refill rules below. |
| Pael's Tooth / Beautiful Bracelet / Thieving Hopper | Fixed priorities; separate purposes with [reward aliases](#card-reward-selection-keys). | [Source rules](#fixed-priority-card-selection) below. |
| Slippery Bridge: [vanilla candidates](#slippery-bridge-selection), including fallback | Corrected clocks; Bridge-only scope with [reward aliases](#card-reward-selection-keys). | Update once when a newly selected card is shown. If declined, renew its clock once before the next selection, using its per-occurrence renewal count. |
| Ranwid the Elder / Stone of All Time: owned potions | Corrected clocks; one shared run-wide scope. | Singleton: leave its clock unchanged. Otherwise renew the offered occurrence's clock once when the offer becomes available, using its per-occurrence renewal count. Declined potions remain selectable. |

### Permanent upgrades and downgrades

**Upgrade sources:** Whetstone, War Paint, Fragrant Mushroom, Sand Castle, War Hammer, Fishing Rod, Doors of Light and Dark, Battleworn Dummy, Endless Conveyor, Tablet of Truth, Reflections, and Mad Science's Improvement (shared clocks replace `CombatCardSelection`).
**Downgrade sources:** Wongos and Reflections.

- **Upgrade-clock refill:** after a random downgrade, retain a nonzero upgrade clock; otherwise take the next value from one dedicated run-wide refill sequence, shared only by Wongos and Reflections. This sequence is consumed at most three times per run.
- **Downgrade-clock refill:** Wongos refreshes the downgraded card's downgrade clock using event RNG. Reflections leaves consumed downgrade clocks at zero; no later permanent downgrade source can use them.

Reflections applies downgrades in ascending pre-selection downgrade-clock order: first downgraded, first refilled. It then determines upgrade candidates; downgraded cards may qualify.

**Vanilla deviation — SL 4A(↓):** reordering random upgrades, such as Whetstone and War Paint, with candidate addition/removal or another disjoint random upgrade no longer rerolls their targets.

### Fixed-priority card selection

- **Pael's Tooth:** store five player-selected unupgraded cards outside the deck using [equivalent-card routing](#equivalent-card-selections). Return one upgraded per subsequent combat trigger in priority order, retiring only its return-order record. Later removals never add or recycle cohort records.
- **Beautiful Bracelet:** enchant up to four eligible deck cards with Swift 2.
- **Thieving Hopper:** assign priorities at acquisition. Apply [vanilla theft eligibility](#card-state-and-selection) to combat copies, selecting by their `DeckVersion` occurrences.

### Potion departure routing

For [player-chosen use, discard and trade](#logical-occurrence-lifetime), treat same-type owned potion copies as equivalent, including copies with different displayed trade payoffs. All such sources use [representative routing](#representative-routing) in one scope per potion type, separate from sacrifice-offer selection. Slot-dependent offers and trade payoffs stay with the clicked option.

Baseline selection: [potion event mechanics](#potions).

## Settled policy: The Future of Potions: card-type assignment

**Intent:** match rarity/type menus for equal offered rarity counts, and potion/type choices for identical offered potion multisets regardless of order. Retaining an individual potion's trade when other potions change is best effort.

Offer the first three owned potions in slot order, preserving display order. Use the existing [reward-rarity mapping and allowed card types](#card-reward-offers); generated cards follow [generation routing](#settled-policy-generation-routing).

- Use three independent event-local type sequences, addressed by event ID + type-assignment purpose + reward rarity (Common/Uncommon/Rare). Each entry is an independent uniform draw from that rarity's allowed types.
- Order offered potions with [fixed priorities](#fixed-selection-priorities), keyed by event ID + ordering purpose + potion model ID + duplicate number. Number identical offered copies from 1 in slot order.
- Within each reward rarity, pair potions in priority order with successive type entries. Consume one entry per offered option and keep those assignments for the event.

**Rationale and limit:** rarity sequences align menus while preserving vanilla's independent type probabilities. Shared priorities retain relative ordering across overlapping inventories, but adding or removing a same-rarity potion can shift a survivor's sequence position. Identical copies retain the same multiset of trades when reordered.

## Settled policy: Ranwid relic sacrifice

To align sacrificed relics, offer one tradable owned relic using [fixed priorities](#fixed-selection-priorities) keyed by event ID + relic identity. Preserve existing eligibility, costs and rewards.

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

Use [potion effect opportunities](#potion-effect-opportunities); each use generates the [routed batch](#settled-policy-generation-routing).

Preserve vanilla eligibility, identity sampling and batch rules; see [potion card-generation mechanics](#potion-card-generation).

**Vanilla deviation — SL 5(↓):** thieving hopper stealing and other potion types no longer change a potion's generated cards.

## Settled policy: unknown-room resolution

**Intent:** align draws within each act and minimize movement of each room type's probability interval, limiting interference from earlier acts and other room types' changing chances.

### Addressing and room-type chances

- **Addressing:** room-type RNG uses the act-local ordinal of visited `?` nodes.
- **Shop chance:** when the map-adjacency policy excludes Shop, its stored chance remains unchanged, matching vanilla. Preserve existing Juzu Bracelet, Golden Compass and Lantern Key room-type restrictions.
- **Effective chances:** after exclusions, allocate a 100% probability budget in vanilla order (normal fight → chest → shop), capping each chance at the remaining budget. Use these effective chances only as interval widths; preserve stored chances and their vanilla updates.
- **Thresholds:** the chest interval begins at 0, the shop interval ends at 1, and the normal-fight interval stays centered at `7/15` unless shifted just enough to avoid overlap; `7/15` is the midpoint when chest/normal-fight/shop widths fill the range in their base `2:10:3` ratio. Unassigned space resolves to Event.

## Settled policy: Ancient offerings

**Intent and scope:** align Ancient offerings when eligibility changes. Preserve vanilla option pools, weights, prerequisites and menu structure. Granted effects follow their policies separately.

Use [fixed priorities](#fixed-selection-priorities) keyed by Ancient ID + option-pool ID + option identity: one from Orobas's third pool, Pael's second/third and Tezcatara's first; three from Tanx's and Nonupeipe's three-choice pools. Preserve survivors' relative display order.

**Pael's second pool:** use vanilla weights: Growth 1; each other option 2.

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

## Settled policy: card shuffling

**Intent:** align source-matched cards in Acts 1–2 and usefulness ranks in Act 3, accepting rank-based shifts.

**Scope:** opening and later shuffles share the assignment strategy below. Combat equivalence and state handling remain deferred.

Assign independent uniform priorities on a common scale, sort all participating cards by priority, then apply existing opening-hand and shuffle modifiers.

Address opening shuffles by [combat address](#combat-addresses) + opening purpose; later shuffles by combat address + later purpose + combat-local shuffle ordinal. All later-shuffle triggers share this ordinal. Every executed shuffle consumes one opportunity, including empty and single-card shuffles.

**Vanilla deviation — SL 5:** stabilizes shuffles across draw/discard choices, often lowering the best attainable fight outcome.

**Vanilla deviation — SL 4A(↓):** state-aware [Processing Order](#deterministic-card-processing-order) removes discard-order SL alternatives even with the same participating cards, such as drawing cheaper Frantic Escapes earlier ([baseline](#combat-setup-and-shuffles)). Losing these alternatives can lower the best attainable fight outcome.

### Priority assignment

The opening Innate subgroup uses [Processing Order](#deterministic-card-processing-order), least useful first, with its own priority sequence. For ordinary cards, the first applicable row wins:

| Cards | Acts 1–2 | Act 3 |
|---|---|---|
| Defend and earlier Processing Order categories | First sequence, least useful first. | Same. |
| Other starting Basics; eligible rewards/direct additions | Source-based priorities below. | Second sequence, most useful first. |
| Remaining cards | Second sequence: earliest acquisition floor first, then ascending Processing Order. | Both rows combined, most useful first. |

Opening shuffles retain removed entries only during priority assignment, then omit them. This protects deletion alignment; additions and reordered states can still shift sequential assignments.

### Source-based assignment — Acts 1–2

Use [generation routing](#settled-policy-generation-routing) to identify shared supplies. Other starting Basics receive distinct [initial-source keys](#card-and-potion-occurrence-addresses).

- **Reward offers:** one shuffle key per eligible own-character shared triple. Exclude colorless, other-character and type-specific rewards.
- **Direct additions:** one shuffle key per shared generation entry; allow only unfiltered or transformation-filter-only generation.
- **Triple representative:** if one acquired card belongs to the triple, assign it the key. If several do, select the lowest priority derived from event ID + assignment purpose + card identity. Freeze the representative at acquisition; the others enter the remaining group.
- **Transformations:** treat as deletion plus direct addition.
- **Copies and event-local supplementary cards:** enter the remaining group.
- **A0–6 extra-Rare replacements:** follow the [replacement-state rule](#a06-multiple-rares).

These are shuffle-specific assignments; existing [card reward selection keys](#card-reward-selection-keys) retain their scopes.

## Settled policy: random pile insertion

**Intent:** align relative insertion depth across changing pile sizes while isolating unrelated sources and shuffles. Neighboring card identities may differ.

Address independent sequences by [combat address](#combat-addresses) + sharing group + target pile + insertion purpose. Source copies share their group.

| Sharing group | Coverage |
|---|---|
| Soul | Capture Spirit, Dirge, Grave Warden, Reave, Severance and Funerary Mask. |
| Metamorphosis | Generated attacks. |
| Dazed | Blessed Antler, Tea of Discourtesy, Noisebot and Entomancer's Personal Hive. |
| Biiig Hug | Soot. |
| Soul Fysh | Beckon. |
| The Insatiable | Frantic Escape. |

For each insertion, take the next uniform `u ∈ [0,1)` and choose zero-based gap `floor(u × (n + 1))`, where `n` is the target pile's current size. This preserves vanilla's conditional gap probabilities.

Insert cards one at a time into the evolving pile, preserving vanilla generation order, intervening hooks and existing-card order.

Advance once per inserted card, including into an empty pile; failed insertions consume zero.

**Within-combat deviation — SL 4A(↓):** group, target-pile and shuffle isolation remove cross-sequence rerolls.

Baseline: [pile insertion and discard order](#combat-setup-and-shuffles).

## Settled policy: monster HP

**Intent:** align combat HP sequences independently of earlier rooms, minimize per-roll expected absolute HP differences across eligibility changes, and retain within-combat draw-order manipulation.

Use shared-percentile sampling for each covered HP roll: derive uniform `u ∈ [0,1)` from [combat address](#combat-addresses) + combat-local HP-roll ordinal + monster HP purpose; select zero-based index `floor(u × count)` from ascending eligible HP values.

Initial enemies, summons, replacements and Tough Egg's hatch share this ordinal. Advance once per vanilla HP sampling call in vanilla execution order, excluding fixed values.

Preserve vanilla eligibility and full-range fallback, conditional probabilities, fixed values, range modifiers, subsequent HP changes, creature lifecycle order and summon rules. Punch-Off's starting HP reductions remain under [encounter setup](#settled-policy-encounter-setup).

Baseline: [monster HP mechanics](#monster-hp).

## Settled policy: monster AI

**Intent:** align intent decisions while preserving vanilla probabilities and reactions. Across histories, prioritize same-seed consistency; assess changes to vanilla SL alternatives as tradeoffs. Encounter-assigned starting moves remain under [encounter setup](#settled-policy-encounter-setup).

**Shared rules:** isolate monster model identities except for Decimillipede. Address sequences by [combat](#combat-addresses) + sharing scope + purpose; consume independent entries in vanilla order. Preserve vanilla effective weights and state-machine behavior. History and eligibility stay outside addresses. Temporary states and resuming an intent consume zero.

**Effective decision:** at least two moves have positive effective weight. A sequence using effective decisions advances only for these choices; singleton and all-zero branches leave it unchanged. All-zero branches retain vanilla's first-listed result.

### Intent sequences

| Source | Settled strategy |
|---|---|
| Types limited to one occurrence in the combat, including Fabricator | Personal random-branch count, including singleton/all-zero branches; use [weighted opportunity-specific priorities](#opportunity-specific-selection-priorities) with move identity keys. Deterministic transitions consume zero. |
| Inklets | Two continuous effective-decision sequences: one for the middle, one shared by both outers. Sample in vanilla declaration order; retain these scopes after deaths. |
| Exoskeletons / Scrolls of Biting | One continuous effective-decision sequence per type, shared across copies and rounds; sample in vanilla declaration order. [Baseline choice points](#monster-ai). |
| Small Leaf Slimes | Independent type-local opening entries in creation order; preserve subsequent forced alternation without advancing intent sequences. |
| Decimillipede | One continuous revival-selection sequence shared by front, middle and back; choose uniformly among the three attacks. |
| Deterministic intent graphs | Preserve existing transitions and setup-assigned starting phases. |

### Rat sampling

**Intent:** realign ordinary choices at matching rounds, selection positions and eligibility; align summoned openings independently of birth timing.

**Summon:** all rats share one continuous combat-local percentile sequence. Advance exactly when Summon has positive effective weight; choose it with probability `effective Summon weight / total effective weight`, preserving vanilla sequential eligibility checks.

**Ordinary sampling:** when Summon is unavailable or loses, use [weighted opportunity-specific priorities](#opportunity-specific-selection-priorities) keyed by move identity, independently of Summon. Both scopes below use the shared combat address and separate purposes.

| Ordinary opportunity | Address and consumption |
|---|---|
| Summoned opening | Combat-local summoned-rat ordinal in vanilla creation order, independent of birth round or summoner. Consume once at its first random intent selection. |
| Later choice, any rat | Current combat round + round-local selection ordinal, shared by all rats in vanilla selection order, including after deaths. Advance only for ordinary choices with at least two positive-weight moves; openings stay separate. |

Use `CombatState.RoundNumber` at selection; extra player turns stay in the same round. Reset the later-choice ordinal each round.

**Limit:** different selection counts can still shift choices within a round.

**Rejected alternatives:** percentiles align poorly across eligibility changes; pair sequences lose cross-pair relationships and retain count differences; markers retain filter-history differences after eligibility converges.

### Fabricator bot identities

Use Fabricator occurrence + bot-selection ordinal + a separate bot-generation purpose. Sample uniformly from the requested pool after vanilla's immediate-repeat exclusion, in stable model-identity order. Singleton choices consume an ordinal. Preserve spawn order.

### Behavior preservation

Fresh independent draws at vanilla conditional probabilities, with preserved sequential eligibility and transitions, preserve the intended joint NoSL distribution.

**Vanilla deviation — SL 4A(↓):** within-combat scope isolation and effective-decision consumption remove rerolls through unrelated or forced draws.

**Vanilla deviation — SL 4A:** type isolation and rat move rankings change survivor alternatives. For example, in the [reference examples](#monster-ai), Flyconid/Twig M can lose vanilla's guaranteed zero-immediate-damage alternative.

Baseline: [monster AI mechanics](#monster-ai).

## Settled policy: random creature targets

**Intent:** align random enemy targets across changing eligible sets.

Use four independent sequences addressed by [combat address](#combat-addresses) + sharing group + targeting purpose:

| Sharing group | Sources |
|---|---|
| Forgotten Soul | All Forgotten Soul selections. |
| Lightning | All Lightning copies, passive activations and evokes. |
| Direct damage | Remaining plays that inherently cause damage, poison, or immediate HP loss; includes Knife Trap. |
| Other | Mainly includes automatically played debuffs. |

Zero damage preserves classification.

With fewer than two eligible targets, advance nothing.

Reuse [opportunity-specific selection priorities](#opportunity-specific-selection-priorities) with equal weights: opportunity = sequence address + combat-local selection ordinal; candidate key = [monster occurrence identity](#monster-occurrence-identity). Repeats remain allowed.

Revivals retain occurrence identity; summons and replacements receive fresh identities.

**Within-combat deviation — SL 4A(↓):** group isolation removes cross-group rerolls; singleton skipping removes forced-target rerolls; priorities change alternatives across eligible sets.

Baseline: [random creature targeting](#random-creature-targeting).

## Settled policy: random orb types

Use one sequence shared by all random-orb sources, derived from [combat address](#combat-addresses) + orb-type purpose, preserving vanilla's uniform sampling.

Failed channeling consumes zero.

**Vanilla deviation — SL 4A(↓):** retaining entries after failed channeling removes SL rerolls through wasted draws. **Frequency:** rare — require losing all orb slots, regaining, then exploring later random-orb outcomes.

## Settled policy: combat card generation and transformations

**Intent and scope:** align random identities from combat card, power and relic generators and Entropy. [Potion-generated cards](#settled-policy-potion-generated-cards) retain their settled scope.

Use independent sequences addressed by [combat address](#combat-addresses) + card-generation purpose + group + pool below. All copies and triggers sharing a group and pool use the same sequence.

| Group | Identity supply and consumers |
|---|---|
| Ordinary costs | One sequence per pool: own character, colorless, each other character, curse and status. Share ordinary-cost generation and Entropy; Jackpot uses the own-character sequence. |
| Free this turn | One own-character sequence shared by generators applying this effect. |
| Free this combat | One own-character Attack sequence for Metamorphosis. |
| Splash | One dedicated sequence over the combined eligible other-character Attacks, uniform across identities. |

Classify cost treatment by the generating source's free effect; natural costs, upgrades and later modifiers leave classification unchanged. Entropy follows the original card's transformation pool regardless of its acquisition source.

Use [filtered draws](#filtered-draws), preserving source eligibility, batch duplicate rules, counts and generated attributes. Consume one entry per generated identity, including every offered alternative, under [shared consumption timing](#opportunity-consumption-timing).

For Entropy's unordered selections, use [shared transformation allocation](#unordered-transformation-batches), then apply replacements in the existing gameplay execution order.

**Within-combat deviation — SL 4A(↓):** pool/cost-treatment isolation and shared generation–Entropy identity supply change source interactions and remove cross-group rerolls. Within a pool, ordinary-cost generation can advance Entropy's identity supply and vice versa.

**Within-combat deviation — SL 4A(↓):** canonical Entropy allocation removes selection-order rerolls for the same selected batch.

Baseline: [combat generation and transformation mechanics](#combat-card-generation-and-transformations).

## Settled policy: random cost values

**Intent:** align cost rolls within card categories while separating combat draws from persistent potion opportunities. Cost rolls remain uniform over 0–3.

| Independent group | Sequence address | Assignment |
|---|---|---|
| Confused — Snecko Eye and Fake Snecko Eye | [Combat address](#combat-addresses) + Confused cost purpose + card category | Five sequences, each following draw order. |
| Slither | Combat address + Slither cost purpose | One sequence shared by all enchanted copies, following draw order. |
| Snecko Oil | Oil cost purpose + [potion effect ordinal](#potion-effect-opportunities) + card category | Five fresh sequences per opportunity, using hand-entry order below. |

**Card categories:** starter Strikes; starter Defends; Other Basic 1; Other Basic 2; Others. Other Basic slots follow each character's original starting-deck identity order and remain fixed through removals. Classify by current identity: upgrades and copies retain its category; transformations use the replacement's category. Other Strike-tagged attacks belong to Others.

**Consumption:** consume one entry per eligible cost roll. Confused's X cards and Slither draws that have left hand consume zero.

**Confused + Slither:** when both apply to the same draw, Slither reuses that draw's Confused value at Slither's usual timing, retaining its hand check and leaving its own sequence unchanged. Keep the enchantment; its sequence resumes when Confused is absent. Reapplication preserves late overwrites such as Kingly Kick's draw discount.

**Oil assignment:** after Oil finishes drawing, order eligible cards within each category by latest successful entry into hand, earliest first:

- Draws, retrievals and generation receive entry positions. Retained cards keep theirs; leaving then returning refreshes them.
- Unordered entry batches share one entry ordinal, with internal ascending [Processing Order](#deterministic-card-processing-order) frozen at entry.
- Sequential draws and explicitly ordered returns, including All for One, preserve source order.

Combat-equivalent-copy handling remains deferred alongside shuffling.

Oil's opportunity ordinal persists across combats. [Shared isolation impact](#shared-isolation-impact) applies to removing cross-combat steering of later Oil batches through earlier Confused/Slither rolls or earlier Oil hand sizes.

**Within-combat deviation — SL 4A(↓):** group/category isolation, X skipping and overlap reuse remove consumption-based rerolls; Processing Order within unordered entry batches removes click/enumeration-order Oil rerolls.

Baseline: [random energy costs](#random-energy-costs).

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

Encounter setup uses floor-based addressing. Shuffle RNG persists across combats; reshuffles and random pile insertions advance the same generator used for later opening hands.

**Shuffle baseline:** opening shuffles clone cards in deck order (`Player.PopulateCombatState`), then apply Fisher–Yates directly (`CardPile.RandomizeOrderInternal` → `UnstableShuffle`). Later `CardPileCmd.Shuffle` combines discard and remaining draw cards, then uses `StableShuffle`: `List.Sort` followed by Fisher–Yates. Both apply existing shuffle modifiers afterward. `CardModel.CompareTo` compares only model identity and upgrade level, leaving enchantments, persistent progress and combat state tied; tied-copy order can still affect same-seed results.

**Discard-pile order:** affects which cards fit when `AllForOne` returns cards to a nearly full hand; `AggressionPower` and `DrainPower` also sample through order-sensitive shuffles. `FranticEscape.OnPlay` adds 1 cost for the combat, so the shuffle ties above can include copies with different costs, allowing discard order to affect which cost is drawn earlier.

**Random insertion:** `CardPileCmd.Add` uses `Shuffle.NextInt(n + 1)`; `MegaRandom.NextInner` scales a uniform double, matching percentile gap sampling. Lifecycle guards skip insertions before rolling a position.

#### Monster HP

Enemy creation samples uniformly with `Niche` from the inclusive initial HP range, excluding `MaxHp` values (current/max HP denominators) in the enemy list; an empty candidate set restores the full range (`Creature.SetUniqueMonsterHpValue`). Startup creates/adds enemies sequentially; summons share this path. Death-triggered spawns precede source removal, so dying enemies still count.

**Special cases:** Tough Egg's hatch samples its full hatchling range with `Niche`. Axebot's range rises by 10 per replacement. Decimillipede segments adjust HP to distinct even values. Punch-Off's starting reductions use encounter-setup RNG.

**Summons:** ordinary sampling covers Fabricator's bots, Two-Tailed Rats, Ovicopter's eggs and Phrog Parasite's four sequential Wrigglers. Eye with Teeth (6), Parafright (21) and Gas Bomb (7/8 by ascension) have fixed HP. Gremlin Merc creates Fat, creates/adds Sneaky, then adds Fat: each rolls before the other joins the enemy list (`SurprisePower.AfterDeath`).

#### Monster AI

**Randomness and selection:** vanilla uses the run-wide `RunRngSet.MonsterAi` for `MonsterModel.RollMove` and Fabricator's bot identities. `RandomBranchState.GetNextState` totals effective weights, draws `NextFloat(total)`, then subtracts each branch's weight in declaration order until the remainder is `<= 0`. Effective weights incorporate repetition limits, cooldowns and dynamic conditions. Singleton and all-zero selections still consume a draw. Deterministic transitions consume none. Encounter-assigned starting moves, such as Corpse Slugs' coordinated starts, use setup RNG separately.

**All-zero fallback:** all-zero branches always choose the first listed move. Flyconid can reach `Smash → Vulnerable Spores → Frail Spores → Smash`, making all effective weights zero; it then selects Vulnerable Spores.

**History and interruption:** `MonsterMoveStateMachine` follows branches until reaching a move and retains its state log for weight checks. Ordinary stun returns to the stored move; Thieving Hopper's Flutter depletion uses a deterministic follow-up. Decimillipede normally cycles `Writhe → Constrict → Bulk`; death leads through `Dead → Reattach → random attack`. After Reattach, the three attacks have equal weights. Revival draws share vanilla's AI stream across segments.

**Duplicate random selectors:** Inklets, Exoskeletons, Scrolls of Biting, Two-Tailed Rats, and possible pairs of small Leaf Slimes beside Slithering Strangler. Leaf Slimes randomize their opening, then alternate forcibly. Exoskeletons choose equally after Enrage; Skitter forces Mandibles, which leads to Enrage. The normal encounter's fourth copy also has a random opening. Scrolls choose equally between Chomp and a second consecutive Chew after their first Chew; the second Chew forces Chomp, followed by More Teeth and Chew. Forced random-branch selections still consume vanilla draws.

**Same-type death timing:** outer Inklets start with Jab, then choose equally between Piercing Gaze and Whirlwind; either attack returns through Jab. The middle starts with Whirlwind. Remove the middle before it makes a random selection. Compare killing outer A before its first random selection with killing it after exactly one. Once outer B is alone in both plays, its corresponding random selections (second, third, etc.) use AI-stream positions one draw apart, even when its eligible moves match.

**Eligibility changes:** Two-Tailed Rats permit Summon only after their timer, shared summon limit, space and teammate-intent checks pass. With Screech on cooldown and both ordinary attacks eligible, attack probabilities are `1/2, 1/2` without Summon; with Summon, attack/attack/Summon probabilities are `1/11, 1/11, 9/11`, using ideal weight ratios. At the same AI-stream position, enabling Summon can switch the selected ordinary attack because the weighted intervals change. After `Screech → Scratch`, blocking Summon can leave Disease Bite as the sole eligible move; selecting it still consumes an AI draw.

**Rat selection order:** enemies are processed in encounter-slot order, `first` through `fifth`; originals occupy the last three slots. Earlier selected Summon intents block later rats. With equal otherwise-eligible Summon probability `p` and no existing teammate Summon intent, successive rats have probabilities `p`, `(1-p)p`, etc. Existing teammate intents can also block earlier selectors. Sources: `CombatState.SortEnemiesBySlotName`, `CombatManager` player-turn preparation, `TwoTailedRat.CanSummon`.

**Rat history alternatives:** the three originals start with Scratch, Disease Bite and Screech. Suppose the Screech starter and either attacker can be killed before the first random selection. The survivor then offers Bite/Screech after Scratch, or Scratch/Screech after Bite; Summon is timer-blocked. Vanilla's shared percentile selects Screech in both alternatives or neither, giving 50% SL availability. A shared move ranking or marker-filtered iid move sequence gives 2/3. Both preserve each individual survivor's 50/50 odds; across the three pairs of opening histories, interval agreement averages 1/3, equal to shared priorities under equal pair weighting.

**Cross-type draw consumption:** in the lineup `Leaf Slime S → Twig Slime M → Twig Slime S`, killing either small slime removes different numbers of later AI draws: Leaf S selects through random branches even during forced alternation; Twig S has a fixed attack. This can change Twig M's next draw.

**Survivor-choice alternatives:** in a Flyconid/Twig Slime M encounter after opening with `Smash → Frail Spores` and `Sticky Shot → attack`, respectively, suppose either kill is feasible before the next intent selection. Either sole survivor uses the same next AI draw: the lower half of its range selects Flyconid's Vulnerable Spores or Twig's attack; the upper half selects Flyconid's Smash or Twig's Sticky Shot. Exactly one kill choice therefore leaves a zero-immediate-damage next intent. Choosing a survivor before seeing its next intent gives a 50% zero-immediate-damage chance; reloading to try both kills reveals which choice achieves it.

**Same-type survivor alternatives:** clear the other original Two-Tailed Rats while one follows `Scratch → Disease Bite → Summon → Scratch`; its summoned copy opens with Scratch. At this point, the original has made three random selections and the copy one. Kill either before Scratch executes. Afterwards, either survivor chooses equally between Disease Bite (7 damage at A7+) and Screech (Frail, zero damage): the original has used its summon, while the copy's summon timer remains positive. Vanilla gives both kill alternatives the same next draw and result.

**Fabricator:** Fabricate selects a defensive bot (Guardbot/Noisebot), then an offensive bot (Zapbot/Stabbot); Fabricating Strike selects only an offensive bot. Each draw excludes the immediately preceding bot identity across both pools. An intervening defensive bot therefore permits the previous offensive identity again. Full capacity forces deterministic Disintegrate; its bots use fixed moves. In the standard single-player encounter, kill/stun timing changes when selections occur while preserving their order and the sequence of summoned bot identities.

#### Random creature targeting

Direct random effects and automatic enemy targeting share one run-wide random stream, sampling uniformly among eligible enemies. A singleton selection consumes a draw; an empty set consumes none. Automatic play and Lightning retain supplied targets; intrinsically random-hit effects select separately per hit and allow repeats. Catfood preserves these behaviors.

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

**Mad Science:** after combat, permanently upgrades random eligible deck cards without replacement, one per power stack, using `CombatCardSelection`.

**Thieving Hopper:** appears at most once in supported runs and steals once. Select uniformly from draw/discard copies with `DeckVersion`, using the first nonempty rarity/enchantment preference tier, or all candidates if every tier is empty (`ThievingHopper.ThieveryMove`). Theft removes the deck card; Hopper's death restores that same object (`SwipePower`).

**Pael's Tooth:** The player selects and removes five unupgraded cards. Each subsequent combat returns one random remaining stored card, upgraded; all five return after five combats. Returned records leave the cohort. This is stored-card selection without replacement, not new identity generation. Mechanics clarified by the project owner.

#### Combat card generation and transformations

The following generators use the same factories and run-wide `CombatCardGeneration` RNG as [potion card generation](#potion-card-generation), with its eligibility and uniform identity sampling.

| Pool / filter | Ordinary costs | Free this turn |
|---|---|---|
| Own character | Stoke, Call of the Void, Choices Paradox | Discovery, Mad Science's Chaos effect, Vexing Puzzlebox |
| Own-character Attack | Calamity | Infernal Blade, Crossbow |
| Own-character Skill | — | Distraction |
| Own-character Power | Creative AI | Abundance, White Noise |
| Own-character Common | Hello World | — |
| Own-character zero-cost non-X | Jackpot | — |
| Own-character Ethereal | Big Hat | — |
| Colorless | Bundle of Joy, Jack of All Trades, Manifest Authority, Quasar, Spectrum Shift, Orange Dough, Toolbox | — |

Metamorphosis generates own-character Attacks, free this combat. Splash offers three distinct Attacks from the combined other-character pools; its chosen card is free this turn. Jack of All Trades excludes itself.

Stoke, Jackpot, Metamorphosis and Calamity allow duplicate identities. Creative AI and Call of the Void make separate single-card draws, allowing repeats across them. Other multi-card factory batches require distinct identities. Thieving Hopper's theft and random affliction selection also consume `CombatCardGeneration`.

Entropy is the sole random combat transformation source. It transforms selected hand cards in selection order using `CombatCardSelection`, with the [default transformation pool and exclusions](#permanent-transformations-and-direct-additions). Different selected originals can have different eligible lists, making results depend on selection order. Fixed-result transformations and Largesse's multiplayer generation fall outside this policy's scope.

#### Random energy costs

Confused, Slither and Snecko Oil are the only gameplay consumers of the run-wide `CombatEnergyCosts` RNG; combat entry preserves its position. Without intervening Confused/Slither rolls, the next Oil continues the unused stream. Their on-draw rolls, including during Oil's own draw, shift its starting position; shuffles, targeting and other RNG streams leave it unchanged. Catfood adds no cost-specific patch.

| Source | Vanilla mechanics |
|---|---|
| Confused | Applied by Snecko Eye (Darv) and Fake Snecko Eye (Fake Merchant). Each owned card drawn with nonnegative canonical energy cost receives a combat-long cost modifier. X cards have canonical cost zero, so they consume a roll while remaining X. Snecko Eye also adds two cards to turn-start draws. |
| Slither | Wood Carvings' Snake enchantment. Rerolls its card on draw only while it remains in hand, with a combat-long modifier. Enchanting excludes X and Unplayable cards. |
| Snecko Oil | Draws seven, then iterates the resulting hand in hand order. Rerolls non-X cards with nonnegative unmodified energy cost until turn end or next play, whichever comes first. |

Draw hooks resolve powers before cards and their enchantments: Confused rolls first, Kingly Kick applies its −1 draw discount, then Slither overwrites it with a separate roll. Hellraiser's earlier draw hook can play a drawn Strike before Slither checks hand presence.

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
