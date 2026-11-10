# Same-Seed Consistency Mod — design

**Reading guide:**

Find source policies in the [Architecture overview](#architecture-overview); compare rarity and identity sharing in [Generation sharing](#generation-sharing).

For descriptive background, see [Reference](#reference) for calibration and rejected alternatives, and [Appendix](#appendix-existing-mechanics) for existing mechanics.

## Goals and principles

**Goal:** keep choices and combat challenges comparable across same-seed runs with different routes and decisions. Compare corresponding opportunities — such as each run's first shop or third normal-fight reward — even on different floors.

**Fairness principle:** preserve meaningful consequences while minimizing unrelated future gameplay changes.

**Preservation:** retain vanilla gameplay except for deliberate alignment tradeoffs or corrections to unintended behavior.

**Same-seed divergence (歪种):** outcome differences from uncontrolled randomness that undermine comparable opportunities, often early or mid-game. Differences caused solely by unequal consumption of intentionally shared sequences are benign, including late-game divergence.

**Batch-order neutrality:** changing click or enumeration order for the same batch must preserve which identities or statuses are affected and the subsequent random state, unless the effect is explicitly order-dependent. This should preserve presentation order and gameplay effects.

### Vanilla deviations

Consistency fixes with no gameplay impact are ungraded.

**Unnoticeable:** imperceptible across ordinary runs, considering frequency and effect size; effects on win chance provide supporting evidence. Use 500 runs as a rough benchmark, with the [A7+ one-Rare cap](#a7-one-rare-per-offer-and-deferral) as the reference.

**Neutral:** an informed player would make no meaningful change to strategy.

| Grade | Impact |
|---|---|
| **1** | Presentation only. |
| **3** | Unnoticeable and neutral gameplay changes. |
| **4A** | Noticeable and neutral. |
| **4B** | Unnoticeable and non-neutral. |
| **5** | Noticeable and non-neutral. |

**Play modes:**

- **NoSL:** no save/loading; assess in-room and upper-level strategy.
- **SL:** explores every available action sequence from the current save before committing to the next saved state. Assess the best attainable room outcomes, upper-level decisions and in-room choices affecting later rooms, such as which potions to carry.

Grade modes independently: `NoSL x, SL y` when they differ, `NoSL x` when equal, and `SL x` when only SL is affected.

**Strategy-change notation:** on 4B/5, **↓** means simpler meaningful decisions, **↑** more complex ones; no direct arrow means altered tradeoffs. For SL, parenthesized arrows describe in-room exploration at any grade: `5(↓)` changes strategy while simplifying exploration.

Rare deviations include a **Frequency:** field.

#### Loss of future outcome steering

This deviation is shared across policies.

**Vanilla deviation — SL 5↓(↓):** applies when changes remove planning decisions based on steering later outcomes through earlier consumption of shared randomness. For example, [combat-local random orbs](#random-orb-types) prevent arranging a favored orb sequence for the next battle.

## Scope

Standalone StS2 v0.111.0 mod for everyday play, also suited to competition. Catfood comparisons use v1.1.2.

Compare single-player runs with matching seeds, versions and rules, and all content unlocked.

**Exclude:** tutorial overrides (including scripted rewards and boss discovery order), custom-run modifiers, repeat visits to the same event, and content removed by game updates.

**Numerical scope:** exclude negligible probability differences caused solely by rounding or exact sampling boundaries.

**Default difficulty:** Scarcity (Ascension 7+); below A7, use the corresponding probabilities.

**Discussion boundary:** describe base random outcomes and modifier interactions that affect random operations. Preserve existing modifiers, including eggs, Fresnel Lens and fixed discounts.

## Concepts and terms

Random operations are grouped into:

- **Rewards:** generated reward identities and amounts, including permanent transformation results.
- **Encounter:** selection of affected cards/items, room content, named event options and combat randomness; divided into **Non-combat** and **Combat**.

[Relic Trader](#relic-trader) groups outgoing selection and replacement relics under Encounter.

Policy category and RNG ownership are separate: a reward can use event RNG.

### Document hierarchy

| Section | Owns |
|---|---|
| **Global rules** | Requirements and defaults that constrain all covered systems. |
| **Reusable mechanisms** | Algorithms for implementing policies. |
| **Shared policies** | Rules shared by source policies within a defined scope. |
| **Source policies** | Rules for named sources or source families, including local exceptions. |

Source policies combine local decisions with applicable global and shared rules and may use mechanisms directly. Exceptions remain explicit in the rules that authorize them.

A source policy may cover:

| Component | Describes |
|---|---|
| Intent | Consistency goal. |
| Scope | Covered operations. |
| Purpose | What the randomness determines. |
| Generation | How outcomes are produced. |
| Selection | How eligible candidates are chosen. |
| Sharing scope | Which operations share randomness. |
| Addressing | How corresponding opportunities or entries are identified. |
| State | What information is retained, reset or saved. |
| Consumption | When opportunities or entries are consumed. |
| Deviations | Gameplay changes from the baseline. |
| Rationale | Why these choices were made. |
| Baseline | Existing mechanics. |

### Game context

- **Identity:** which card, item, enemy or event it is. For example, Bash and Bash+ share an identity.
- **ID:** an identifying label.
- **Map type:** the map marker; `Unknown` (`?`) may resolve into different room types.
- **Room type:** the resolved room category. Ancients are counted separately in this design. An event can start combat while retaining its map type.
- **Combat:** any battle, including event battles. **Normal fight:** a battle outside the elite and boss categories.

### Items and rewards

- **Generation / acquisition:** generation determines an item; acquisition means receiving it.
- **Card reward offer:** cards offered together for selection. **Card slot:** one position within an offer.
- **Reward skipping:** an acquisition opportunity ends with its reward unclaimed.
- **Direct card addition:** a card added without an identity choice or an opportunity to skip after seeing it.
- **Ordinary relics:** Common, Uncommon, Rare and Shop rarities.

### Randomness and addressing

- **RNG:** random number generator.
- **[Random state](#rng-derivation-and-addressing):** internal information tracking random processes across operations.
- **Purpose:** what the randomness determines, such as selection, ordering or upgrading.
- **Roll:** sampling a random number, or the resulting number.
- **Pool / sequence:** a pool defines candidate identities; a sequence is an ordered collection of entries, such as random values or item identities. Repeated values or identities are distinct entries.
- **Filter:** conditions determining which candidates or entries are eligible.
- **Draw:** retrieving an item or entry from a pool or sequence.
- **Selection:** choosing a candidate, whether by the game or the player.
- **Partitioning:** separation of random state or finite stock by category, such as source, pool or purpose. Random-state partitions may use overlapping pools.
- **Isolation:** separation of randomness between opportunities, such as combats or potion uses. Consumption within one leaves another's assigned rolls or sequences unchanged.
- **Allocation:** finite relic stock assigned to a source and ordered into sequences.
- **Triple sequence:** a card identity sequence consumed in complete consecutive groups of three entries (**triples**).
- **Ordinal:** an opportunity's position within its category. Counts are run-wide unless scoped; scoped counts reset for each scope instance.
- **Ordinal-addressed generation:** randomness derived from an opportunity's ordinal within its scope. For example, **Shop-ordinal generation** names the shop case.
- **Logical occurrence / physical copy:** a logical occurrence is a persistent record for one copy; a physical copy is the card currently associated with that record.
- **Occurrence address:** a logical occurrence's source ID, distinct for each copy.
- **Selection key / selection-key alias:** a selection key is a candidate's ID for a selection purpose; an alias is a source-specified replacement key for that purpose.
- **[Selection state](#addressing-and-state):** hidden information carried by a logical occurrence that governs its future selection.
- **Routing:** selecting logical occurrences among eligible cards with the same identity and gameplay-relevant status, then associating them with player-selected physical copies.

## Architecture overview

The mod aligns corresponding same-seed opportunities through addresses, shared state for related sources, and isolation of unrelated random operations. A source may use different sharing scopes and state for whether and when a result appears, and for its rarity, identity and attributes.

### Rewards

| Decision area | Policies |
|---|---|
| Relics and chests | [Relic allocation and borrowing](#relics); [chest rewards and advancement](#chests). |
| Potions | [Generation and drops](#potions). |
| Shops | [Shops, Wongos and Courier](#shops-wongos-and-courier). |
| Cards | [Reward offers and Rare timing](#card-reward-offers); [permanent transformations and direct additions](#permanent-transformations-and-direct-card-additions). |
| Other rewards | [Combat gold](#combat-gold); [event-specific rewards](#event-specific-rewards). |

### Encounter

- **Rooms and events:** align `?` rolls by visit ordinal within each act and improve event agreement as eligible pools change.
- **Selection:** selection keys and [persistent selection state](#corrected-persistent-clocks) retain alignment across choices. [Routing](#routing) makes equivalent card clicks select the same logical occurrence; eligibility histories and position-dependent effects still matter.
- **Combat:** scope setup, enemy behavior and card randomness to each battle. Potion-generated cards and Oil costs use potion-effect ordinals across battles.

#### Non-combat

| Decision area | Policies |
|---|---|
| Rooms and events | [Unknown rooms](#unknown-room-resolution); [ordinary events](#ordinary-event-selection); [forced events](#forced-events); [Ancient offerings](#ancient-offerings). |
| Event selections and trades | [The Future of Potions](#the-future-of-potions-card-type-assignment); [Ranwid relic sacrifice](#ranwid-relic-sacrifice); [Relic Trader](#relic-trader); [Endless Conveyor](#endless-conveyor). |
| Card and potion selection | [Wing Charm](#wing-charm); [persistent selection](#persistent-selection). |

#### Combat

| Decision area | Policies |
|---|---|
| Setup and enemies | [Encounter setup](#encounter-setup); [enemy HP](#enemy-hp); [enemy AI](#enemy-ai). |
| Card piles | [Card shuffling](#card-shuffling); [random pile insertion](#random-pile-insertion). |
| Random selection | [Combat cards](#random-combat-card-selection); [Thieving Hopper](#thieving-hopper); [enemy targets](#random-enemy-targets). |
| Generation and effects | [Card generation and transformations](#combat-card-generation-and-transformations); [random energy costs](#random-energy-costs); [random orb types](#random-orb-types); [potion-generated cards](#potion-generated-cards). |

### Generation sharing

Sources sharing a sequence advance a common progression. Separate supplies limit that interference; finite relic allocations can still interact through [borrowing](#borrowing). Ordinal addressing gives corresponding opportunities their own randomness, as with initial shop cards.

"Shared" spans each row's named sources or offer category, within the stated partitions. Source policies supply local exceptions.

Shared rules: [rarity and identity sequences](#rarity-and-identity-sequences).

#### Relics, potions and event-specific rewards

| Sources | Rarity | Identity supply |
|---|---|---|
| [Ordinary relics](#allocation): generic rewards / shop / Courier | Separate source sequences. | Disjoint finite allocations. |
| [Relic Trader](#replacement-relics) | By event ID and reward slot. | Courier allocation. |
| [Potions](#acquisition-and-sequences): generic rewards / shop / Courier / generating effects | Per-source sequences; generic rewards split standard-weight and uniform-identity modes. | Per-source, per-rarity sequences. |
| [Event-specific rewards](#event-specific-rewards) | Event RNG. | Event RNG. |

#### Card offers and shop cards

| Sources | Rarity | Identity supply |
|---|---|---|
| [Initial shop cards](#stock-and-prices) | Shop ordinal; fixed colorless rarities. | Shop ordinal. |
| [Courier cards](#stock-and-prices) | Per-type sequences; fixed colorless rarities. | Character: per-type, per-rarity sequences. Colorless: per-rarity sequences. |
| [Own-character random-rarity offers](#offer-groups), such as fight rewards and Orrery | Shared luck and thresholds; source Rare odds. | Shared triple sequences, split by offers with or without a Rare. |
| [Own-character fixed-rarity offers](#other-offer-groups) | Requested rarity. | Separate triples per rarity. |
| [Lasting Candy / The Future of Potions](#type-specific-offers) | Candy: source sequences or fixed Rare. Future of Potions: fixed rarity. | Shared own-character Power sequences per rarity (Uncommon/Rare); Future of Potions uses event RNG for Attack/Skill. |
| [Colorless offers: Lead Paperweight / Brain Leech's Rip](#other-offer-groups) | Shared sequence at source odds. | Shared Uncommon/Rare identity sequences. |
| [Other-character offers: Kaleidoscope / Colorful Philosophers / Sea Glass](#other-offer-groups) | Kaleidoscope: event RNG. Others: fixed rarity. | Shared sequences per character and rarity. |
| [Prismatic Gem / Dingy Rug](#prismatic-gem-and-dingy-rug) | Source state. | Separate expanded-pool sequences. |

#### Other card generation

| Sources | Rarity | Identity supply |
|---|---|---|
| [Permanent transformations / direct additions](#selection-and-consumption) | Separate rarity sequences where used; the Infested Automaton event uses event RNG. | Shared permanent-card sequences by pool. |
| [Potion-generated cards](#potion-generated-cards) | No rarity roll. | Uniform draws per potion-type effect batch. |
| [Combat generation / Entropy](#combat-card-generation-and-transformations) | No rarity roll. | Uniform combat-local sequences by group and pool. |

## Global rules

### Preservation defaults

Unless a policy specifies a change, preserve existing behavior, including:

- Source access, pools, eligibility, choices, costs and rewards.
- Offer sizes, selection counts, batch boundaries, duplicate rules, generated attributes and upgrade rules.
- Triggers, effect timing/order, modifiers and presentation.

### RNG derivation and addressing

Random state includes RNG state, sequence contents and positions, opportunity counters, stored rolls and selection priorities.

**Invariant — independent derivation:** new or re-addressed randomness derives from run seed + purpose and sharing-scope IDs + the address components specified by its policy. The same full address reproduces the same roll or sequence; independent purposes use separate derivations and state. Share randomness only where a policy specifies sharing or coupling. Explicitly retained baseline RNGs keep their stated addressing.

Run seed is implicit in local and candidate addresses for new or re-addressed randomness. In design address expressions, `+` means unambiguous composition of components, not arithmetic addition. Sharing scope may be implicit or expressed through the policy's address components, such as sharing group or pool. Use stable IDs rather than display labels; distinguish a sequence's address from its current position.

In RNG addresses, a scoped ordinal denotes `(scope ID, count)`: act-local includes act number; event-local includes event ID.

**Card selection keys:** derive initial keys from occurrence addresses, subject to [source-specific aliases](#card-reward-selection-key-aliases) and [combat-card selection state](#combat-card-selection-state); never use current list position.

**Selection tie-breaks:** tie-break extremely rare equal random-selection priorities by comparing the selection keys already used to generate them, in ascending order.

### Entry addresses

An entry's address is its original source sequence + original entry position, distinguishing occurrences of repeated identities.

**Invariant — stable addresses:** entries retain their original addresses through movement, filtered advancement, [borrowing](#borrowing), removal and return.

**Invariant — presentation-only reordering:** change only display positions, keeping each entry's address, attached random state and group membership with it.

### Opportunity consumption timing

**Invariant — consumption timing:** for operations that consume opportunities, generate results and advance random state only when those results become available to reveal or receive — even if left unrevealed, declined or skipped.

Deferred rewards consume at the payoff opportunity, not acquisition of the effect creating it.

**Exception:** [potion effect opportunities](#potion-effect-opportunities) also advance on discard, trade and covered reward skips.

### Saved state

Preserve [vanilla's run-save checkpoints](#run-save-checkpoints). At each checkpoint, save all mod state needed to reproduce subsequent outcomes; restore it before room regeneration.

## Reusable mechanisms

### Filtered draws

**Main idea:** take the next eligible item without pulling skipped items toward the front of later draws.

**Advancement:** return the first eligible entry, then move each later eligible entry to the preceding eligible position. Ineligible positions remain unchanged. For example, if eligible entries `A`, `B`, `C`, ... occupy positions 2, 6, 8, ..., return `A`, move `B` to 2, `C` to 6, and so on.

**Method — deferred advancement:** perform the advancement on demand for infinite sequences and finite relic allocations.

Markers record pending advancements and freeze eligibility conditions at the original draw, including batch exclusions and state-dependent restrictions; the current draw still enforces current eligibility.

- **Filtered draw:** return the first eligible entry and replace it with a marker for its filter.
- **Marker resolution:** move the next eligible entry after it into the marker's position. Leave the same filter's marker at the supplying position.
- **Unfiltered draw:** resolve any front marker, then return the entry and remove the drawn position.

**Marker collisions:** when a draw using filter `Y` encounters a marker for filter `X`, resolve that marker first.

- If the resolved entry fails `Y`, retain it at the marker's former position and continue searching for `Y`.
- If it satisfies `Y`, return it and place a marker for `Y` there. The marker for `X` remains at the supplying position, representing an advancement of `X` followed by an advancement of `Y`.

Apply the same rule recursively to further collisions, including identical filters: two advancements require two markers.

If a finite supply has no eligible entry to resolve a marker after [borrowing](#borrowing), remove the marker and continue the current draw. Chests also use [finite-lifetime expiry](#finite-lifetime-chest-markers).

### Fixed selection priorities

**Scope:** one choice or batch per run within each sharing scope.

Calculate each candidate's independent uniform priority `U_i ∈ (0, 1)` only at the selection opportunity, from purpose + sharing scope + selection key. Select the lowest eligible priorities.

**Weighted single choice:** among candidates with weight `w_i`, select the lowest `-ln(U_i) / w_i`. Selection probability is `w_i / Σw`. Fixed relative weights preserve survivors' ranking when candidates are removed.

### Corrected persistent clocks

**Main idea:** for uniform occurrence selection across opportunities, retain residual exponential clocks and correct eligible survivors after each random selection. The [persistent-selection](#persistent-selection), [combat-selection](#random-combat-card-selection) and [combat-card routing](#combat-card-routing) policies specify sharing and local clock rules.

**Address and state:** each logical occurrence starts with a clock `e_i = -ln(U_i)`, with independent `U_i ∈ (0, 1)` derived from purpose + sharing scope + selection key.

Where renewal uses a per-occurrence count, derive each independent roll from purpose + sharing scope + selection key + renewal count.

**Selection and update:**

1. Select the eligible occurrence with the smallest clock, `m`.
2. On commitment, consume its clock and subtract `m` from every unselected eligible survivor. Ineligible occurrences retain their clocks without subtraction or renewal.

Policies define when and how to renew consumed clocks from the same distribution.

**Batches:** for an unordered uniform sample of `k` distinct occurrences from a fixed eligible set, select the `k` smallest pre-selection clocks and subtract the `k`-th smallest clock from every unselected eligible survivor once. Consume each selected clock once. Commit from one eligibility snapshot, independently of UI click/enumeration order. Effects with intentionally sequential eligibility changes retain that distinction.

A player action does not advance a gameplay selector's clocks merely because it changes membership; routing has its own scope. Fixed-target and explicit affect-all effects also consume no selection clocks.

**Rationale and limit:** the exponential memoryless property preserves uniform selection probabilities while retaining the relative order of jointly participating survivors. This does not preserve one global ranking across different eligibility histories. For example, let `A`, `B`, `C` have clocks `0.5`, `0.6`, `0.7`: if only `A` and `C` participate and `A` is selected, the surviving `C` now outranks the inactive `B`.

### Routing

**Scope:** [out-of-combat](#out-of-combat-card-routing) and [combat](#combat-card-routing) operations on player-selected cards. These shared policies define equivalence and operation sharing.

Use [corrected persistent clocks](#corrected-persistent-clocks) for selection. An already randomly selected logical target bypasses routing.

**Routing boundary:** preserve display order. Apply effects at the clicked copies' actual pile positions and follow the source's movement and ordering rules. For example, Hologram removes the clicked discard entry, and All for One later reads the remaining entries in discard order.

**Batch association:** use the shared clock mechanism's eligibility snapshot and freeze presentation order with it. Only classes containing a clicked copy participate in reassignment and routing-clock updates.

1. In each participating class, pair selected occurrences in ascending pre-selection routing-clock order with clicked copies in frozen presentation order.
2. Assign each participating class's remaining logical occurrences in address order to its unclicked copies in frozen presentation order.
3. Apply effects after selection is complete.

Commit the association permutation and shared clock update with the action. Renew each selected routing clock once if its logical occurrence survives, using its per-occurrence renewal count. Singleton and whole-class player selections follow this same rule.

**Coupling limit:** for single selections from matched nested sets of two and three occurrences, shared independent priorities select matching logical occurrences with probability `2/3`, maximal for uniform selection. Different selection keys or eligibility histories can reduce agreement.

### Opportunity-specific selection priorities

Apply [fixed-priority sampling](#fixed-selection-priorities) afresh per opportunity. Derive priorities from purpose + opportunity address + selection key, excluding candidate sets and list positions.

## Shared policies

### Addressing and state

**Selection state:** an occurrence's address, selection keys and aliases, [residual clocks and renewal counts](#corrected-persistent-clocks).

#### Shared addresses

##### Event addresses

Event ID defines event-local scope. Retained [event RNG](#event-and-encounter-identities) follows baseline seeding.

##### Combat addresses

Ordinary combats use combat category + act-local combat ordinal + encounter ID, with separate normal-fight, elite and boss counters.

Event-created battles outside ordinary encounter lists use event-local battle ordinal + encounter ID, leaving ordinary combat counters unchanged.

Combat-local random state derives from purpose + combat address and resets each combat. The [shared deviation](#loss-of-future-outcome-steering) applies where this removes cross-combat outcome steering.

##### Enemy occurrence addresses

An enemy's address is source-local spawn ordinal + creation index, stable for its lifetime.

Initial enemies use the [combat address](#combat-addresses) as their source; summons/replacements use the summoning/replaced occurrence.

One spawn opportunity covers the initial lineup or one summon/replacement trigger; number its enemies in vanilla creation order.

Revivals retain their occurrence; summons and replacements start new occurrences.

##### Card occurrence addresses

Out-of-combat operations follow the [lifetime rules](#out-of-combat-card-lifetime); combat cards follow [combat-card selection state](#combat-card-selection-state).

Cards with generation entries use their [entry addresses](#entry-addresses). Other cards use source opportunity address + copy index. Assign indices in the [card processing order](#card-processing-order) after establishing gameplay properties, with a distinct index for each equivalent copy. Treat the starting deck as one initial source opportunity.

#### Card reward selection-key aliases

When a card reward offer allows at most one card, the acquired card uses the first card's selection key as an alias for later selection by Pael's Tooth, Beautiful Bracelet, Slippery Bridge and Thieving Hopper. Freeze the first offer entry's selection key as the anchor before presentation-only swaps.

Different offers, including [rerolls](#rerolls), have distinct anchors. Offers allowing multiple cards, including bundles, keep each acquired card's own selection keys even when only one is taken.

#### Out-of-combat card lifetime

**Scope:** out-of-combat card operations for [persistent selection](#persistent-selection) and [routing](#out-of-combat-card-routing), using [card occurrence addresses](#card-occurrence-addresses).

| Operation | Occurrence and selection state |
|---|---|
| Acquire a generated card | Preserve its address and source-defined selection keys, including [reward aliases](#card-reward-selection-key-aliases). |
| Move, temporarily store, or recreate the physical object for an existing occurrence | Retain the logical occurrence and its selection state; see [Tooth's return-record rule](#fixed-priority-card-selection). |
| Change status or eligibility | The change itself preserves the occurrence and its selection state through equivalence-class splits and merges, subject to [upgrade/downgrade refills](#permanent-upgrades-and-downgrades). |
| Copy a card | Create a fresh occurrence and selection state from the granting source. Consume no routing clocks. |
| Permanently remove or combine cards | Retire the selected logical inputs. |
| [Transform a card](#permanent-transformations-and-direct-additions) | Retire the original and initialize fresh replacement selection state. |

#### Combat-card selection state

For combat [selection](#random-combat-card-selection) and [routing](#combat-card-routing), use combat-local [corrected clocks](#corrected-persistent-clocks) with one frozen selection key per occurrence. Use a separate per-occurrence renewal count within each sharing scope.

**At combat start:** before selection effects, use each card's [shuffle key](#priority-assignment) under ordinary-card assignment, including opening removed-entry reservations. Innate separation applies only to shuffling.

**During combat:** take the first applicable key for a fresh occurrence:

| Card source | Selection key |
|---|---|
| Identity drawn from a generation sequence | Original [sequence entry address](#entry-addresses). |
| Fixed token creation | Token ID + its creation ordinal in this combat. Count each new token of that identity admitted to combat once across all granting sources. |
| Other grants | Granting-source ID + committed grant ordinal within that source for this combat + copy index. Copies of the source share its count. Assign batch indices in [processing order](#card-processing-order). |

Pile movement and status or eligibility changes themselves preserve keys, residual clocks and renewal counts. A copy starts fresh. A transformation retires the input's combat state and initializes the replacement under the rules above.

#### Potion effect opportunities

**Scope:** [potion-generated cards](#potion-generated-cards) and [Snecko Oil costs](#random-energy-costs).

Each covered potion type has its own run-wide effect ordinal:

| Action | Ordinal advancement |
|---|---|
| Committed use, even when no rolls occur | Advance once. |
| Discard or trade | Advance once. |
| Skip a covered potion reward on the reward screen | Advance once per skipped reward. |
| Acquire | None. |

**Cauldron exception:** Each covered potion offered by Cauldron grants one exemption for its type — any action that would advance that type's effect ordinal consumes one available exemption instead. Unused exemptions expire at next-room entry.

**Vanilla deviation — SL 5(↑):** discard, trade and covered reward skips advance the next same-type effect batch. Preserving favorable randomness for later combats therefore requires picking up and keeping other same-type potions.

### Rarity and identity sequences

**Main idea:** align successive opportunities for the same kind of item across routes, with independent sharing scopes and sequence state for rarity and identity.

**Scope:** relics, potions, cards, permanent transformations and covered direct additions.

With per-rarity identity sequences, a random-rarity request consumes one value from its **rarity-roll sequence**, then draws an eligible item from that rarity's **identity sequence** using the [filtered-draw mechanism](#filtered-draws). Fixed-rarity requests consume identity only; pools with one identity sequence draw directly. Source policies specify sharing and RNG exceptions; [generation sharing](#generation-sharing) summarizes them.

**Generation and lifetime:** covered potion/card identity and rarity-roll sequences generate on demand indefinitely, using independent RNGs for the specified source and pool partitions. Each newly generated potion/card identity entry is an independent uniform draw with replacement from its sequence's pool unless the source specifies otherwise, such as weighted or grouped generation. Only finite relic allocations [borrow](#borrowing).

Apply the filtered-draw mechanism to covered identity sequences, including shop-batch duplicate filtering, without additional rarity rolls, except for [Wongos's identity removal](#stock-and-prices). Skipped entries remain available to eligible draws and borrowing.

Triple sequences retain each slot's rarity while rejecting duplicate identity candidates within that triple during generation. Other card sequences apply source-required duplicate exclusions through filtered draws.

### Selection

#### Card equivalence

Equivalence requires the same identity and gameplay-relevant status. Hidden selection state and pile position do not split a class.

**Invariant — equivalent copies:** selecting different equivalent copies preserves identity/status-level outcomes, apart from position-dependent effects. [Routing](#routing) enforces this for covered player choices.

#### Out-of-combat card routing

**Out-of-combat equivalence key:** compare card identity, upgrade level, enchantment identity/amount, and applicable persistent progress: Genetic Algorithm's `IncreasedBlock`, The Scythe's `IncreasedDamage`, Dowsing's `RoomsEntered`, and Guilty's `CombatsSeen`. Omit redundant current-value fields, change history, acquisition metadata, Spoils Map's additional fields and Mad Science's type/rider configuration within the supported scope. Baseline: [persistent card state](#card-state-and-selection).

For the out-of-combat operations below, sources share [routing clocks](#routing) by operation and card identity:

| Routing scope | Operations |
|---|---|
| Upgrade | Upgrades to player-selected cards, including Pael's Tooth's initial selection. |
| Enchant | Enchantments on player-selected cards, across enchantment types and amounts. |
| Permanent removal | Removal, transformation and Amalgamator's combining. |

Sharing the removal scope aligns surviving originals after [input retirement](#out-of-combat-card-lifetime).

#### Combat-card routing

Use [routing](#routing) for each player selection of an existing combat card. Only copies available for that choice participate. Follow [combat-card selection state](#combat-card-selection-state).

Partition routing clocks by card identity. Play, discard, exhaust, retrieval into hand, upgrade, enchantment and transformation each share one routing scope across granting sources. Other choices use the choice purpose and granting source as their scope, including Thinking Ahead's return to the top of the draw pile.

Use [card processing order](#card-processing-order)'s state comparisons for equivalence, excluding origin rank; include consequential pending effects on particular copies.

**Permanent-deck correspondence:** group Genetic Algorithm, The Scythe and Goopy copies by their exact linked permanent-deck occurrence, with a separate group for combat-only copies. While Thieving Hopper's steal is pending, apply this grouping to every card identity.

Within each equivalence class, route the logical occurrence as a unit, including its selection state, shuffle assignment, [Oil hand-entry assignment](#random-energy-costs), copy relationships and any deck link outside the protected cases above. Keep live references with their logical targets.

**Vanilla deviation — SL 4A(↓):** within a routed class, trying different clicks on reload selects the same hidden state for later combat selection, shuffles and Oil costs.

#### Card processing order

**Intent:** improve same-seed consistency across different card sets and keep unordered batch outcomes independent of click or enumeration order.

Apply the [identity and gameplay-relevant status ranking](CARD_ORDER.md) to random-operation processing of unordered card batches. Source policies specify applicable comparisons.

Identical cards remain tied regardless of selection state, acquisition order or physical position; retain every copy. Tied-copy permutations must leave gameplay outcomes and subsequent random state equivalent under [card equivalence](#card-equivalence).

Preserve display order and intentionally sequential effects; reward offers use their existing entry order before presentation-only swaps.

#### Transformation batches

A transformation batch comprises the originals selected together.

Generate replacements for selected originals in identity-only [card processing order](#card-processing-order). Within each input identity, pair replacements in generation order with originals in frozen presentation order.

## Source policies

### Rewards

#### Relics

**Intent:** limit interference between generic, shop and Courier relic rewards.

##### Allocation

Construct disjoint generic-reward, shop and Courier allocations once, targeting **45% / 35% / 20%** of each Common/Uncommon/Rare pool. Include shop-ineligible relics. Use these fully unlocked counts:

| Rarity | Generic rewards | Shop | Courier |
|---|---|---|---|
| Common | 12 | 9 | 5 |
| Uncommon | 14 | 11 | 7 |
| Rare | 17 | 13 | 8 |

Each source has one rarity-roll sequence, generated on demand indefinitely, and independently advancing, shuffled identity sequences per rarity. The shop also has one shuffled sequence containing all Shop-rarity identities.

Ordinary generic reward opportunities must eventually expose every still-eligible entry, subject to the [borrowing boundary](#borrowing). Other sources' multi-choice offers must preserve unchosen generic entries.

##### Source classification

By default, hidden random relic rewards use the generic rewards source, including paid rewards and relic-triggered grants. Classify deferred payoffs separately from their granting relic.

##### Borrowing

**Consistency boundary:** source-partitioning guarantees exclude borrowing's effects. Chest refill and final fallback require no alignment.

When the requested rarity has no eligible local entry, borrow the **last eligible entry** from another source's sequence of that rarity. Courier borrows from generic rewards, then shop; generic rewards and shop borrow from Courier, then the remaining source.

Apply borrowing during [marker resolution](#filtered-draws), preserving filtered-draw marker placement.

**Chest refill:** when a chest reward has no eligible entry after same-rarity borrowing, retain its generic rarity result and select uniformly among eligible identities of that rarity in the original shared relic pool, advancing the generic relic rarity RNG once more. If none qualify, use the final fallback.

**Final fallback:** from the requested rarity onward in Shop → Common → Uncommon → Rare, try local entries then same-rarity borrowing at each rarity. Return Circlet if all fail; make no additional rarity roll.

Baseline: [relic bags](#relic-bags) and [Catfood's rarity-roll limit](#catfood-addressing-and-combat-rewards).

#### Chests

##### Reward generation

On entering a non-empty chest room, advance the chest ordinal once and generate both gold and relic rewards.

- **Gold:** generate base gold from the chest ordinal, independently of combat gold.
- **Relic:** consume one generic rarity-roll entry, derive the [marker-lifetime roll](#finite-lifetime-chest-markers), and exclude the drawn identity from later chests.

Silver Crucible's empty chest room generates neither reward and leaves the chest ordinal unchanged.

##### Relic advancement

**Intent:** preserve skipped relics for ordinary rewards, align common future identities, and spread acquisition's character-specific probability increase across later positions.

Draw the next eligible shared identity from the generic rarity sequence and replace it with a [finite-lifetime marker](#finite-lifetime-chest-markers). Both choices advance along the same path:

- **Acquire:** retire the relic; the marker disappears at expiry, compacting survivors.
- **Skip, including unopened chests:** save the relic with the marker as its **carried entry**; at expiry it returns as an [ordinary reward entry](#opportunity-consumption-timing).

Resolve the choice before expiry, including `x = 0`; permanent ineligibility or acquisition elsewhere cancels a skipped relic's return.

**Rejected alternative:** a separate chest allocation adds unfairness between reward sources.

###### Finite-lifetime chest markers

Use the [shared filtered-draw advancement and collision rules](#filtered-draws). A chest marker expires after `x` eligible-entry advances or when supply exhaustion removes it. Each transfer into the vacancy decrements `x` and moves the marker to the supplying position. Initial `x = 0` applies expiry at the drawn position. Collisions preserve each marker's lifetime and carried entry.

**Lifetime:** use `x = floor(u * S)`, with uniform `u` in `[0, 1)` addressed by `purpose = marker lifetime` + rarity + non-empty chest ordinal. Corresponding chests share `u`; successive chests roll independently.

Before recording the offer, `S` counts chest-eligible shared identities of that rarity, excluding previous chest offers, in:

- The remaining generic sequence, including the drawn entry.
- The **original shop and Courier allocations**, retaining identities after display or acquisition.

Apply current run-state eligibility throughout; shop eligibility adds no restriction. Character-specific identities and markers contribute zero. The original allocations act as a retained tail, reflecting vanilla's back-drawing shops and keeping shop-stock consumption outside lifetime generation.

**Probability:** within sufficient supply and the [uniform-order assumptions](#relic-bags), acquisition models deletion of a uniform chest-eligible position in the combined generic order and retained tail. Skipping rotates eligible identities through their positions, preserving the character-specific position pattern.

**Alignment:** acquire and skip share the future sequence up to the return position; skip adds one ordinary opportunity while preserving common-entry order. Across runs, reducing `S` by one changes `x` by zero or one; starting one shared entry later in the same order and reducing `x` by one gives the same expiry relic. Relocation is a consistency fix with no gameplay impact under the [deviation convention](#vanilla-deviations).

Baseline: [chest mechanics](#shops-chests-and-relic-events) and [Catfood addressing](#catfood-addressing-and-combat-rewards).

#### Potions

**Intent:** preserve comparable results across batch sizes while keeping repeatable generation from displacing ordinary rewards. Failed direct grants leave later potions unchanged. White Beast Statue's forced drops leave the ordinary drop-check state unchanged for use after the relic's removal.

Potion copies carry no persistent selection state. Using, discarding or trading a player-selected potion affects that copy, preserving slot-dependent offers and trade payoffs.

##### Acquisition and sequences

**Generic rewards** combines random combat rewards, events and other grants without paid/free or per-event separation; **shop** and **Courier** are separate. **Potion-generating effects** covers Alchemize, Entropic Brew and Delicate Frond, including use outside combat.

Each source draws individual potions from its own Common/Uncommon/Rare identity sequences over the full eligible pool. Generic rewards share identities across separate standard-weight and uniform-identity rarity sequences; uniform mode weights rarities by eligible identity counts. Other sources each use one standard-weight rarity sequence. [Standard weights](#potions-1) remain unchanged.

Preserve existing failure feedback, including Alchemize's failure animation. Batch boundaries affect duplicate filtering only.

**Example — batch boundaries:** Catfood's call-based seeding can turn `A | BCD | E` for requests `1–3–1` into `AFG | B` for `3–1`. Per-potion consumption instead follows `ABC | D`, subject to batch duplicate filtering.

**Vanilla deviation — SL 5(↓):** failed grants no longer let players advance to a different later potion outcome.

##### Drop checks

- Retain [Catfood addressing](#catfood-addressing-and-combat-rewards) and [vanilla's shared changing probability](#potions-1) across normal fights, elites and bosses. Extra ordinary checks advance the drop-check ordinal; combat-category threshold differences are preserved. No act/Ancient reset.
- **White Beast Statue:** forced drops freeze the drop-check ordinal and stored probability; the potion still consumes a reward identity.
- Save the drop-check ordinal and probability.

#### Shops, Wongos and Courier

**Intent:** Extra rewards and Courier restocks no longer scramble later shops. Corresponding shops keep the same sale slot; relocated offers keep their base prices. Wongos affects only future Rare shop offers, and only when its featured relic is affordable.

##### Stock and prices

- Initial shop stock depends only on shop ordinal, except for Wongos, relic floor cutoffs and [borrowing](#borrowing).
- **Initial cards:** generate rarity and identities by shop ordinal; preserve unavailable-rarity fallback.
- **Card rarity:** vanilla shop base chances plus an invariant offset of `0` at A7+ and `+0.018` at A0–6. The [three-act calibration](#rarity-calibration) approximates vanilla's mean Rare count on ordinary routes.
- Sale slot uses independent shop-ordinal generation.
- **Base prices:** use [entry address](#entry-addresses) for initial stock and Courier restocks. Initial shop cards use shop ordinal + slot as their entry address.
- **Courier cards:** use separate Attack/Skill/Power rarity sequences at shop odds and per-type, per-rarity identity sequences. Colorless restocks request fixed Uncommon or Rare, with one identity sequence each. Preserve unavailable-rarity fallback, exclude cards still displayed, and retain [original mechanics](#shops-chests-and-relic-events).
- **Wongos:** featured offer consumes **one shop Rare identity**; below 200 gold, suppress the offer without consuming stock as a [consistency fix](#vanilla-deviations). Treat the featured offer as a shop display for [chest lifetime counts](#relic-advancement). Other options use [generic relic rewards](#source-classification).

**Wongos identity removal:** for the featured Rare and Common purchase, resolve encountered markers normally, then remove the first shop-eligible entry from the designated source without placing a new marker. This preserves vanilla's local promotion of skipped identities toward later unrestricted draws; developer intent is unknown. See the [filtered-pull example](#relic-bags).

**Boundary — relic floor cutoffs (see [appendix](#relic-bags)):** reaching corresponding shops on opposite sides of a cutoff can change their relics and shift later stock; these differences are inevitable and benign.

**Vanilla deviation — NoSL 3:** Wongos's featured Rare uses shop stock, removing its vanilla advancement of shop-ineligible Rares toward later generic rewards.

**Vanilla deviation — NoSL 4B↓:** fixed shop rarity offsets remove recent combat rarities' influence on shop Rare odds and change Rare clustering.

**Vanilla deviation — SL 4A(↓):** partitioned restock sequences remove restock manipulation through interleaved purchase categories and effects.

**Vanilla deviation — SL 4A(↓):** entry-bound prices remove price manipulation through unrelated purchase order.

Baseline: [shop mechanics](#shop-and-courier-rng) and [Catfood addressing](#catfood-addressing-and-combat-rewards).

#### Card reward offers

**Intent:** equivalent rewards share identity order across sources. Corresponding rare-bearing offers share the same choices across sources. Larger own-character drafts preserve later three-card offer boundaries. Random upgrades leave later card choices unchanged; matching Lasting Candy triggers retain their rarity.

**Scope:** initial generation and reachable rerolls for the offer groups below.

**Crystal Sphere:** does not reset the combat rarity offset.

Colorless and other-character rewards share across batch sizes because their few sources have varying offer sizes.

##### Offer groups

Group identities by pool, rarity request, eligibility filter and draw grouping. Upgrade rules and the number taken leave grouping unchanged.

Own-character random-rarity offers share [luck and thresholds](#luck-and-rarity-offset), [non-Rare and rare-bearing triples](#offer-sequences), and [A0–6 extra Rares](#a06-multiple-rares):

| Group | Rare odds | Sources and offer size |
|---|---|---|
| Normal-fight rule | Normal-fight chance with the shared combat offset. | Normal fights, Dream Catcher, Prayer Wheel and The Hunt in normal fights: three-card offers. |
| Elite rule | Elite chance with the same combat offset. | Elites and The Hunt in elite combats: three-card offers. |
| Fixed base odds | Base Rare odds without the combat offset. | Orrery: five three-card offers; Lost Coffer: one; Trial's Nondescript Guilty: two. Brain Leech's Share Knowledge: choose one of five, using [supplementary cards](#supplementary-cards). |

###### Other offer groups

| Offers | Rarity and identity supply |
|---|---|
| Own-character fixed Rare | Separate Rare triple sequence; one triple per offer: non-final bosses, White Star, The Hunt's delivered boss rewards, Hefty Tablet, Glass Eye and Crystal Sphere. |
| Own-character fixed Common / Uncommon | Separate triple sequence per rarity. Glass Eye consumes two triples per rarity; Crystal Sphere one. Common exceptions: [Room Full of Cheese](#supplementary-cards), [Scroll Boxes](#scroll-boxes). |
| Colorless: Lead Paperweight / Brain Leech's Rip | One shared rarity-roll sequence at Uncommon/Rare odds **98.51% / 1.49%**, with shared Uncommon/Rare identity sequences; consume individual entries across batch sizes. |
| Other-character | Kaleidoscope uses event RNG for character and rarity; Colorful Philosophers / Sea Glass request fixed rarities. Share 12 identity sequences: four other characters × Common/Uncommon/Rare, consumed individually across batch sizes. |

[Expanded pools](#prismatic-gem-and-dingy-rug) retain source rarity rules and draw grouping.

###### Type-specific offers

Lasting Candy and The Future of Potions share own-character Uncommon and Rare Power identity sequences, consuming individual entries. Candy uses separate normal-fight and elite rarity sequences at source base odds, with unavailable-rarity fallback and one roll per triggered card; boss triggers request Rare. Future of Potions requests its specified rarity.

Future of Potions Attack/Skill identities use event RNG addressed by rarity and type, independently of other potion options, at the specified rarity.

##### Offer procedure

For own-character random-rarity offers, including reachable [rerolls](#rerolls):

1. Simulate three slots (all five for Brain Leech's Share Knowledge) under [Rare timing and delivery](#rare-timing-and-delivery).
2. Consume the next [rare-bearing triple](#offer-sequences) if a Rare was delivered, otherwise the next non-Rare triple.
3. Append any [supplementary cards](#supplementary-cards), then apply [A0–6 extra-Rare replacements](#a06-multiple-rares) where applicable.

Replacements precede reward enchantments; [presentation swaps](#presentation) follow step 3. Preserve source-specific upgrade timing and modifier order.

##### Upgrade rolls

Derive each independent upgrade roll `u`, uniform in `[0, 1)`, from `purpose = upgrade` + the card's [entry address](#entry-addresses).

At the source's existing random upgrade check, upgrade an eligible card when `u < p`, using its rarity- and act-dependent chance `p` at that check.

Baseline upgrade chances: [card rarity and upgrades](#card-reward-offers-1).

##### Rare timing and delivery

**Scope:** the three own-character random-rarity [offer groups](#offer-groups).

**Intent:** separate when a Rare appears from the complete choice offered. Align corresponding rare-bearing and non-Rare opportunities while preserving source-dependent Rare chances; matching combat-room ordinals need not consume matching offer entries.

###### Luck and rarity offset

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

###### A7+: one Rare per offer and deferral

Cap each covered offer at one Rare. If a later slot reaches the next threshold:

- Suppress that Rare and keep the reached threshold pending, retaining luck and the current threshold.
- Treat suppressed slots as non-Rare for applicable offset updates.
- Continue accumulating luck; only one Rare can remain pending until delivery.

A **Rare-eligible offer** has a positive chance of producing a Rare along its ordinary all-non-Rare progression, ignoring pending delivery.

- **Next eligible offer:** deliver the pending Rare in its first simulated slot instead of that slot's ordinary check. Reset luck, clear pending and advance the threshold once, applying the delivery source's offset rule. Process remaining slots normally.
- **Ineligible offer:** retain the pending threshold and apply normal non-Rare offset growth.

This naturally excludes the first two three-card normal-rule offers after an offset reset without elite intervention; use probability eligibility rather than room counts. Delivery follows offer generation order, including extra offers and rerolls, and can wait across acts.

**Vanilla deviation — NoSL 3:** extra Rares in elite and fixed-odds offers are deferred, changing acquisition opportunities; additional Rares beyond the single pending one are omitted. **Frequency:** rare — approximately **0.3% of vanilla A7+ runs** contain a dual-Rare offer from these sources (project-owner estimate).

###### A0–6: Rare delivery

Deliver every threshold crossing immediately under the shared luck and source-offset rules, without a per-offer cap or deferral.

##### Offer construction

###### Offer sequences

Own-character random-rarity offers use two deterministic triple sequences independent of the luck thresholds. The **non-Rare sequence** supplies three Common/Uncommon cards per triple; the **rare-bearing sequence** supplies one Rare and two non-Rares. Store the Rare first in each rare-bearing triple.

A suppressed Rare reserves no additional triple.

**Rationale:** shared sequences preserve triple alignment when a Rare moves between sources; separate normal/elite sequences advance differently.

###### Uncommon probabilities

Use these Uncommon probabilities, except for the opening entries below:

| Difficulty | Non-Rare offers | Rare-bearing companions |
|---|---|---|
| A7+ | 38% | 39% |
| A0–6 | 40% | 40.5% |

These rounded rates approximate expected displayed Uncommon totals in the [three-act calibration](#rarity-calibration). Shop offsets do not apply.

**Opening triples:** the first two non-Rare triples use their own deterministic generation with vanilla first-two-normal-offer probabilities after an offset reset, conditional on no Rare. At A0–6 these are **35%, 36%, 37%**, then **37/99, 37/98, 37/97**.

This exception occurs only once per run. The triples follow ordinary shared consumption, including by other covered sources.

**Rationale:** these entries approximate two opening Act 1 normal fights without elite intervention. Normal-fight openings in Acts 2/3 are accounted for through average calibration rather than special entries.

**Vanilla deviation — NoSL 3:** shared Uncommon rates remove elites' advantage (about 0.14 extra Uncommons per three-card non-Rare offer near zero starting offset) and change source- and route-specific rates. Few elite rewards, some Rare-bearing, limit the impact; elites retain relic, gold and higher Rare-chance incentives.

###### Supplementary cards

**Brain Leech — Share Knowledge:** generate two additional Common/Uncommon cards with event RNG, excluding all identities already selected; use the ordinary non-Rare Uncommon probability if no Rare is delivered, otherwise the rare-bearing companion probability.

**Room Full of Cheese:** use one Common triple and generate the remaining five Common cards with event RNG.

###### A0–6: multiple Rares

For own-character random-rarity offers, maintain one additional Rare-only identity sequence per character, shared by the three source groups and independent of fixed-Rare triples. For an offer with `k >= 1` Rares, draw `k - 1` entries from this sequence.

Replace the first Common in offer entry order, or the first Uncommon if no Common remains, for each extra Rare. Apply this across the entire offer, including Brain Leech's two event-generated cards: `R,U,U | C,C` becomes `R,U,U | R,C` for two Rares. Repeat for three, four or five Rares as needed. Exclude the original Rare and earlier extra Rares; replaced companions remain consumed.

**Replacement state:** identity and [upgrade roll](#upgrade-rolls) come from the new Rare entry. Retain the replaced slot's Wing Charm priority or supplementary-card membership, and its [Acts 1–2 shuffle membership](#source-based-assignment--acts-12).

**Accepted tradeoff:** extra-Rare sequence state depends on prior Rare counts; companion replacements depend on offer composition. These A0–6 alignment and distribution tradeoffs are accepted because the design primarily targets A7+.

**Vanilla deviation — NoSL 3:** Common-first replacement adds a preference for retaining Uncommon companions in multi-Rare offers, unlike vanilla's per-slot generation. **Frequency:** rare — at A0–6, approximately **0.265% of vanilla fixed-base-odds three-card offers** contain at least two Rares, calculated from the 3% per-slot Rare chance.

###### Presentation

For own-character random-rarity offers, swap the original Rare, then extra Rares in draw order, into the simulated Rare positions in ascending slot order.

For Brain Leech's non-Rare Share Knowledge offers and Room Full of Cheese's Gorge offers, uniformly shuffle [display order](#entry-addresses) after reward modifiers, with or without Wing Charm, using event RNG.

###### Scroll Boxes

Resolve Defect's two independent 1% triple-Claw substitution checks with event RNG before requesting Common sequence entries. Non-Defect offers follow the no-substitution branch.

- **No substitution:** consume one Common triple. It supplies both Common cards in bundle one and the first Common card in bundle two; generate the remaining Common and both Uncommons with event RNG.
- **Triple-Claw exception:** if either bundle is replaced, bypass the shared Common sequence without inspecting its head. Each substituted bundle contains three fixed Claws; generate any remaining ordinary bundle entirely with event RNG. If both bundles are substituted, generate no unused ordinary-bundle cards.

##### Rerolls

A reachable reroll follows its source's [offer group](#offer-groups) and, where applicable, the [offer procedure](#offer-procedure), consuming fresh entries from its pool/request/filter group, including expanded pools. Preserve source-specific rarity and offset effects, offer structure and duplicate rules.

Event-generated supplementary cards use fresh local generation without restarting the original offer's address.

Kaleidoscope's Neow offers cannot be rerolled with Orobas's Driftwood in the normal-run scope.

##### Prismatic Gem and Dingy Rug

**Intent:** align rewards from the same expanded pool.

Preserve the [existing pool-modification boundaries](#card-reward-offers-1). Select the identity group for the complete request:

- **Unchanged eligible pool:** reuse its existing group, including Rug's fixed-Common or already-colorless offers.
- **Expanded eligible pool:** use independent sequences for the effective expanded pool, retaining source draw grouping. All results, including own-character cards, consume this group; ordinary identity sequences and triple boundaries remain unchanged.

Retain source rarity, luck and upgrade rules, eligible-card sampling weights, offer sizes and duplicate exclusions. Neither relic resets luck state or restarts opening-only rarity exceptions.

Lasting Candy uses the inherited pool's Power sequences while retaining its source rarity-roll sequence position and fallback rules.

**Accepted tradeoff:** separate identity groups sacrifice alignment with ordinary own-character identities to avoid acquisition-dependent reconstruction; prior shared rarity state still matters.

Baseline: [card reward mechanics](#card-reward-offers-1) and [Catfood addressing](#catfood-addressing-and-combat-rewards).

#### Permanent transformations and direct card additions

**Intent:** align same-pool transformations and covered direct additions while preserving source rarity odds.

**Covered direct additions:** Infested Automaton's Study and Touch Core, Endless Conveyor's Fried Eel, Neow's Arcane Scroll and Neow's Bones curse, and Distinguished Cape.

##### Selection and consumption

Use seven pools: the five characters, colorless and curse, separate from card reward offers and combat transformations. Consume individual identity entries:

- **Own character / colorless:** for each pool, transformations and additions share Common/Uncommon/Rare identity sequences, with separate transformation/addition rarity sequences. Transformations weight rarities by current eligible identity counts, excluding the original; additions use vanilla base odds and unavailable-rarity fallback.
- **Other characters / curse:** one identity sequence per pool.

- **[Transformations](#out-of-combat-card-lifetime):** retain [vanilla target pools, eligibility and gameplay inheritance](#permanent-transformations-and-direct-additions).
- **Transformation batches:** use [shared batch rules](#transformation-batches).
- **Direct additions:** use the granting source's pool and restrictions. Infested Automaton's Study / Touch Core use event RNG for rarity, leaving the addition rarity sequence unchanged; identities use the shared permanent own-character sequences.

**Distinguished Cape:** draw twice from the curse sequence; the second pull skips the first identity.

**Vanilla deviation — SL 5(↓):** processing transformation batches in [identity order](#transformation-batches) removes order-dependent replacement alternatives.

Baseline: [generation mechanics](#permanent-transformations-and-direct-additions) and [Catfood addressing](#catfood-addressing-and-combat-rewards).

#### Combat gold

**Intent:** align corresponding normal-fight and elite gold rewards independently, preserving vanilla distributions; partition their rolls from other rewards.

**Generation and addressing:** derive each roll from `purpose = gold reward` + combat category + that category's gold-reward ordinal. Normal fights and elites use separate ordinals; event battles use their encounter's `RoomType`. Roll uniformly over the existing inclusive gold range, preserving endpoint adjustments and payout modifiers.

Fixed or absent combat gold, fixed extra payouts and event-choice gold consume neither ordinal. Gremlin Merc's half-gold reward advances the normal-fight ordinal using its reduced range; zero gold advances neither.

**Rationale and tradeoff:** an extra elite leaves later normal-fight gold unchanged, and vice versa. Reordering unreduced rewards with matching category counts preserves individual base payouts and totals under unchanged payout modifiers, including Bowler Hat's per-payout truncation. Replacing one unreduced normal reward with an elite reward changes total base gold by 11–26 at A3+. [Rejected alternatives](#combat-gold-alternatives).

Baseline: [gold ranges and exceptions](#combat-gold-1) and [Catfood addressing](#catfood-addressing-and-combat-rewards).

#### Event-specific rewards

**Scope:** Trash Heap, Doll Room, event choices granting otherwise unobtainable rewards and Fake Merchant stock.

Neow's Bones' Ancient relic pair follows this policy; its curse follows [direct additions](#permanent-transformations-and-direct-card-additions).

Use event RNG for reward identities, random rarities and Fake Merchant's price rolls, keeping its shop fixed for the same seed.

### Encounter

#### Non-combat

##### Unknown-room resolution

**Intent:** align rolls within each act and minimize movement of each room type's probability interval, limiting interference from earlier acts and other room types' changing chances.

###### Addressing and room-type chances

- **Addressing:** room-type RNG uses the act-local ordinal of visited `?` nodes.
- **Shop chance:** when the vanilla shop-exclusion rule excludes Shop, its stored chance remains unchanged, matching vanilla. Preserve existing Juzu Bracelet, Golden Compass and Lantern Key room-type restrictions.
- **Effective chances:** after exclusions, allocate a 100% probability budget in vanilla order (normal fight → chest → shop), capping each chance at the remaining budget. Use these effective chances only as interval widths; preserve stored chances and their vanilla updates.
- **Thresholds:** the chest interval begins at 0, the shop interval ends at 1, and the normal-fight interval stays centered at `7/15` unless shifted just enough to avoid overlap; `7/15` is the midpoint when chest/normal-fight/shop widths fill the range in their base `2:10:3` ratio. Unassigned space resolves to Event.

##### Ordinary-event selection

**Intent and scope:** improve same-seed event matching within and across acts, prioritizing agreement before the first mismatch. Ancients remain separate; [forced events](#forced-events) bypass ordinary selection.

**Conditions and state:** group IDs by equivalent eligibility predicates, excluding act availability and late eligibility checks, with one key per condition. Act availability determines participating IDs; evaluate late eligibility checks at ID selection to keep grouping compact. Follow the global [addressing](#rng-derivation-and-addressing), [consumption](#opportunity-consumption-timing) and [saved-state](#saved-state) rules.

###### Act entry and carryover

**Alignment:** shared-event order is secondary; sufficiently rare prefix-agreement losses are acceptable.

At run start, independently shuffle each condition's IDs using `purpose = ID order` + condition key. This fixes each arriving group's internal order. At act entry, retain surviving unvisited IDs in their current order, remove expired IDs and insert newly act-available, unvisited IDs in their run-start order. Run-wide visited exclusions persist.

**Sorted random priorities:** for `n` surviving and `m` incoming IDs, take the first `n` and `m` entries from separate old/new sequences of independent uniform rolls, addressed by `purpose = insertion` + act + condition key + old/new. Derive each roll from its sequence address + entry index. Sort each set, assign it along its corresponding ID sequence in existing order, then merge by priority. This preserves uniform interleaving and both internal orders.

[Rejected alternatives](#event-selection-alternatives).

Restore passed scheduling entries: each act-available, unvisited ID contributes one entry, including IDs whose condition currently fails. This removes earlier-act depletion while retaining ID order.

###### Within-act selection

**Schedule:** fixed in Act 1 for shared-event order; refreshed in Acts 2–3 for prefix agreement with differing pools.

1. Initialize priority `-ln(U) / n_c` for conditions with `n_c > 0` unpassed entries: once at Act 1 entry, at each resolution in Acts 2–3. Independent uniform `U ∈ (0, 1)` rolls use condition key + per-condition roll index, plus act in Act 1 or act-local event ordinal in Acts 2–3.
2. Spend one entry of the lowest-priority condition. If false or lacking unvisited IDs, retain its ID position. Otherwise, try its next unvisited ID; a failed late eligibility check passes that ID too.
3. After failure, add a fresh `-ln(U) / remaining_count` to that condition's priority, retiring it at zero. Keep other priorities fixed and repeat.
4. On success, advance the ID sequence and commit the visit and all passing. In Act 1, update the winning priority as in step 3 and retain priorities and roll indices; Acts 2–3 refresh at the next resolution.

**Wrapping:** on exhaustion, restore passed entries and restart priorities, continuing each condition's roll index. ID sequences wrap in the act's established order; visited IDs stay excluded.

**Fallback — empty eligible pool:** if no eligible unvisited event remains, select `list[(n - 1) % list.Count]` from vanilla's original shuffled act list, where `n` is the 1-based act-local ordinary-event ordinal. Bypass eligibility and visited exclusions for this draw only.

**Vanilla deviation — NoSL 3:** ordinal indexing changes fallback probabilities conditioned on prior visits and eligibility. **Frequency:** effectively impossible (qualitative judgment).

**Vanilla deviation — NoSL 3:** refreshed condition priorities after wrapping can change expected event exposure. **Frequency:** rare (qualitative judgment).

**Behavior preservation:** before either structure wraps, preserve vanilla's distribution when eligibility cannot inspect hidden order. With passing late eligibility checks, same starting state + same selected event leaves the same state. Scheduling changes cross-run and SL correlations; carrying ID order preserves earlier ordering across acts. Wrapping and the empty-pool fallback are separate probability and eligibility tradeoffs.

Baseline: [event selection and wrapping](#event-and-encounter-identities); [condition predicates and late eligibility checks](#event-eligibility).

##### Forced events

**Intent:** inserting a forced event must not affect later ordinary-event availability.

Resolve room type under the [unknown-room policy](#unknown-room-resolution). When an effect supplies a forced event, enter it without ordinary event-list eligibility scanning, selection, cursor advancement or ordinary event-opportunity advancement. Record the actual event visit and retain the normal visited-event semantics. The forced event uses its existing event RNG; it needs no special ordinary-event ordinal.

Lantern Key's forced War Historian Repy follows this bypass. A visited `?` still advances its act-local visited-node ordinal; preserve the existing forced room type and frozen non-event chances.

**Vanilla deviation — NoSL 3:** bypassing vanilla's forced-insertion opportunity consumption changes later ordinary-event availability relative to vanilla. Vanilla's consumption is probably unintended by developers.

##### Ancient offerings

**Intent and scope:** align Ancient offerings when eligibility changes. Preserve vanilla weights and menu structure. Granted effects follow their policies separately.

Use [fixed priorities](#fixed-selection-priorities) keyed by Ancient ID + option-pool ID + option ID: one from Orobas's third pool, Pael's second/third and Tezcatara's first; three from Tanx's and Nonupeipe's three-choice pools. Preserve survivors' relative display order.

Retain vanilla generation and RNG positions for other menu components.

Baseline: [Ancient offerings](#ancient-offerings-1).

##### The Future of Potions: card-type assignment

**Intent:** match rarity/type menus for equal offered rarity counts, and potion/type choices for identical offered potion multisets regardless of order. Retaining an individual potion's trade when other potions change is best effort.

Offer the first three owned potions in slot order, preserving display order. Use the existing [reward-rarity mapping and allowed card types](#card-reward-offers-1); generated cards follow [type-specific offers](#type-specific-offers).

- Use three independent event-local type sequences, addressed by event ID + `purpose = type assignment` + reward rarity (Common/Uncommon/Rare). Each entry is an independent uniform draw from that rarity's allowed types.
- Order offered potions with [fixed priorities](#fixed-selection-priorities), keyed by event ID + `purpose = ordering` + potion ID + duplicate number. Number identical offered copies from 1 in slot order.
- Within each reward rarity, pair potions in priority order with successive type entries. Consume one entry per offered option and keep those assignments for the event.

**Rationale and limit:** rarity sequences align menus while preserving vanilla's independent type probabilities. Shared priorities retain relative ordering across overlapping inventories, but adding or removing a same-rarity potion can shift a survivor's sequence position. Identical copies retain the same multiset of trades when reordered.

##### Ranwid relic sacrifice

To align sacrificed relics, offer one tradable owned relic using [fixed priorities](#fixed-selection-priorities) keyed by event ID + relic ID.

##### Relic Trader

**Intent:** align replacement relics; prioritize outgoing-relic overlap over trade matching.

###### Replacement relics

Generate slots 1–3 in order. For each, roll [ordinary relic rarity](#relic-bags) addressed by event ID + reward slot, then take the last eligible Courier entry of that rarity, with no added filters.

###### Outgoing relic assignment

1. For each tradable owned relic and reward slot, use [fixed-priority derivation](#fixed-selection-priorities) keyed by event ID + relic ID + slot.
2. Freeze the three relics with the lowest minimum of its three pair priorities.
3. Among these relics, repeatedly assign the lowest-priority remaining pair, removing its relic and slot.

**Behavior preservation:** outgoing selection and pairing remain uniform. One eligible-owned-relic substitution retains at least two outgoing relics, with maximal symmetric expected outgoing overlap.

**Rejected alternatives:** independent candidate priorities give lower trade agreement with the same outgoing overlap; direct pairing over all eligible relics gives higher trade agreement but can change two outgoing relics after one inventory substitution.

Baseline: [shops, chests and relic events](#shops-chests-and-relic-events).

##### Endless Conveyor

**Intent:** preserve shared dish order across runs.

Generate one persistent event-local sequence of independent draws over all randomized dishes using vanilla weights and a dedicated dish-selection purpose. For each random offer, continue from the current position, consuming ineligible entries — including the previous displayed dish — until the first eligible entry is offered; retain the resulting position for the next random offer. This preserves vanilla conditional probabilities while keeping the shared candidate order.

##### Wing Charm

**Intent and scope:** align Swift targets across corresponding generated rewards, preserving existing generation-batch boundaries and modifier order. This includes eligible direct generation; Sea Glass retains one selection per five-card rarity batch, not one across its combined display.

Apply [A0–6 replacement state](#a06-multiple-rares) and [replacement ordering](#offer-procedure). Evaluate resulting cards' Swift eligibility when Wing Charm runs, after earlier modifiers. No eligible cards means no effect; select an eligible singleton directly.

**Triple-backed offers:** assign independent uniform `[0,1)` Wing Charm priorities to the triple's entries through their [entry addresses](#entry-addresses), and select the lowest-priority eligible card, subject to the exceptions below. The [triple](#card-reward-offers) precedes presentation swaps.

**Non-Rare larger offers:** use the triple in Brain Leech and Room Full of Cheese; if none of its cards is eligible, select uniformly among eligible supplementary cards with event RNG. Restricting selection to interchangeable slots aligns the shared triple while preserving marginal gameplay outcomes, subject to the opening-triple deviation below.

**Weighted extra-group selection:** for rare-bearing Brain Leech and Lasting Candy, let `x` count all eligible cards and `y` eligible extra cards. Select the extra group when the source's independent uniform `[0,1)` roll is below `y/x`; otherwise use the triple's priorities. This gives each eligible card probability `1/x`, including replacement Rares.

| Source / extra group | Group-selection roll | Selection within the extra group |
|---|---|---|
| Rare-bearing Brain Leech: two supplementary positions | Event RNG. | If both cards are eligible, select uniformly with an independent event-RNG roll under a separate purpose. Matching eligible extras retain the same target whenever both runs select the group. |
| Lasting Candy: added Power | Power sequence entry + `purpose = Wing Charm`, evaluated only if the Power is eligible; otherwise use the triple directly. | Select the Power. Qualifying combat offers have three base cards, so there are no other supplementary candidates. |

**Other batches:** use the same entry-derived priorities for every card and select the lowest eligible one. Other-character offers share per-entry priorities across Sea Glass and Colorful Philosophers despite different batch sizes. No separate advancing Wing Charm counter is required.

The [presentation shuffle](#presentation) removes the shared triple's visible enchantment-position bias.

**Vanilla deviation — NoSL 3:** when Brain Leech consumes an opening non-Rare triple, its different rarity probabilities slightly change the enchanted card's rarity distribution.

##### Persistent selection

**Intent:** align affected cards across sources, decks and reward choices, and potion sacrifice offers independently of inventory order.

Use [fixed priorities](#fixed-selection-priorities) or [corrected persistent clocks](#corrected-persistent-clocks) as specified below.

| Selection / candidates | Mechanism and sharing | Local selection rules |
|---|---|---|
| Random permanent upgrades / downgrades of existing deck cards | Corrected clocks; one shared scope per operation. Source filters do not split clocks. | Mark consumed clocks zero; apply the refill rules below. |
| Pael's Tooth / Beautiful Bracelet | Fixed priorities; separate purposes with [reward aliases](#card-reward-selection-key-aliases). | [Source rules](#fixed-priority-card-selection) below. |
| Slippery Bridge: [vanilla candidates](#slippery-bridge-selection), including fallback | Corrected clocks; Bridge-only scope with [reward aliases](#card-reward-selection-key-aliases). | Update once when a newly selected card is shown. If declined, renew its clock once before the next selection, using its per-occurrence renewal count. |
| Ranwid the Elder / Stone of All Time: owned potions | [Weighted sacrifice clocks](#weighted-potion-sacrifice-clocks); one shared run-wide scope. | With one owned potion, leave its clock unchanged. Otherwise renew the selected clock once when the offer becomes available. Declined potions remain selectable. |

###### Weighted potion sacrifice clocks

Each potion type has one clock `E`, keyed by `purpose = sacrifice` + type; use [shared initialization and renewal](#corrected-persistent-clocks). Ordinary use, discard, grants and absence preserve clocks and renewal counts.

For owned count `w > 0`, select the lowest `E/w = m` and subtract `w × m` from other participating clocks. The table specifies renewal timing and the singleton exception.

Choose uniformly among winning-type copies in slot order, using `purpose = copy selection` + type + the type's pre-renewal count.

Uniform copy-selection odds match [vanilla](#potions-1); removing sacrifice rerolls through same-type replacement causes the [shared deviation](#loss-of-future-outcome-steering).

###### Permanent upgrades and downgrades

**Upgrade sources:** Whetstone, War Paint, Fragrant Mushroom, Sand Castle, War Hammer, Fishing Rod, Doors of Light and Dark, Battleworn Dummy, Endless Conveyor, Tablet of Truth, Reflections, and Mad Science's Improvement (shared clocks replace `CombatCardSelection`).
**Downgrade sources:** Wongos and Reflections.

- **Upgrade-clock refill:** after a random downgrade, retain a nonzero upgrade clock; otherwise take the next value from one dedicated run-wide refill sequence, shared only by Wongos and Reflections. This sequence is consumed at most three times per run.
- **Downgrade-clock refill:** Wongos refreshes the downgraded card's downgrade clock using event RNG. Reflections leaves consumed downgrade clocks at zero; no later permanent downgrade source can use them.

Reflections applies downgrades in ascending pre-selection downgrade-clock order: first downgraded, first refilled. It then determines upgrade candidates; downgraded cards may qualify.

**Vanilla deviation — SL 4A(↓):** random upgrades, such as Whetstone and War Paint, lose target rerolls caused by unrelated RNG consumption. Eligibility histories can change survivor rankings under the [shared clock contract](#corrected-persistent-clocks).

###### Fixed-priority card selection

- **Pael's Tooth:** store five player-selected unupgraded cards outside the deck using [routing](#out-of-combat-card-routing). Return one upgraded per subsequent combat trigger in priority order, retiring only its return-order record. Later removals never add or recycle these records.
- **Beautiful Bracelet:** enchant up to four eligible deck cards with Swift 2.

#### Combat

##### Encounter setup

**Intent:** align corresponding combat setups within an act regardless of earlier-act combat counts.

Use the shared [combat address](#combat-addresses) + `purpose = setup`.

Preserve encounter identity selection and setup sampling; reward ordinals and AI retain their own policies.

##### Enemy HP

**Intent:** isolate HP sequences by combat, minimize per-roll expected absolute HP differences across eligibility changes, and retain within-combat roll-order manipulation.

Use shared-percentile sampling for each covered HP roll: derive uniform `u ∈ [0,1)` from [combat address](#combat-addresses) + `purpose = enemy HP` + combat-local HP-roll ordinal; select zero-based index `floor(u × count)` from ascending eligible HP values.

Initial enemies, summons, replacements and Tough Egg's hatch share this ordinal. Advance once per vanilla HP sampling call in vanilla execution order, excluding fixed values.

Preserve vanilla full-range fallback, conditional probabilities, enemy lifecycle order and summon rules. Punch-Off's starting HP reductions remain under [encounter setup](#encounter-setup).

Baseline: [enemy HP mechanics](#enemy-hp-1).

##### Enemy AI

**Intent:** align intent decisions while preserving vanilla probabilities and reactions. Across histories, prioritize same-seed consistency; assess changes to vanilla SL alternatives as tradeoffs. Encounter-assigned starting moves remain under [encounter setup](#encounter-setup).

**Shared rules:** partition sequences by enemy identity, except for Decimillipede. Address sequences by [combat](#combat-addresses) + purpose + sharing scope; consume independent entries in vanilla order. Preserve vanilla effective weights and state-machine behavior. History and eligibility stay outside addresses. Temporary states and resuming an intent consume zero.

**Effective decision:** at least two moves have positive effective weight. A sequence using effective decisions advances only for these choices; singleton and all-zero branches leave it unchanged. All-zero branches retain vanilla's first-listed result.

###### Intent sequences

| Source | Local rules |
|---|---|
| Types limited to one occurrence in the combat, including Fabricator | Personal random-branch count, including singleton/all-zero branches; use [weighted opportunity-specific priorities](#opportunity-specific-selection-priorities) with move IDs as selection keys. Deterministic transitions consume zero. |
| Inklets | Two continuous effective-decision sequences: one for the middle, one shared by both outers. Sample in vanilla declaration order; retain these scopes after deaths. |
| Exoskeletons / Scrolls of Biting | One continuous effective-decision sequence per type, shared across copies and rounds; sample in vanilla declaration order. [Baseline choice points](#enemy-ai-1). |
| Small Leaf Slimes | Independent type-local opening entries in creation order; preserve subsequent forced alternation without advancing intent sequences. |
| Decimillipede | One continuous revival-selection sequence shared by front, middle and back; choose uniformly among the three attacks. |
| Deterministic intent graphs | Preserve existing transitions and setup-assigned starting phases. |

###### Rat sampling

**Intent:** realign ordinary choices at matching rounds, selection positions and eligibility; align summoned openings independently of birth timing.

**Summon:** all rats share one continuous combat-local percentile sequence. Advance exactly when Summon has positive effective weight; choose it with probability `effective Summon weight / total effective weight`, preserving vanilla sequential eligibility checks.

**Ordinary sampling:** when Summon is unavailable or loses, use [weighted opportunity-specific priorities](#opportunity-specific-selection-priorities) keyed by move ID, independently of Summon. Both scopes below use the shared combat address and separate purposes.

| Ordinary opportunity | Address and consumption |
|---|---|
| Summoned opening | Combat-local summoned-rat ordinal in vanilla creation order, independent of birth round or summoner. Consume once at its first random intent selection. |
| Later choice, any rat | Current combat round + round-local selection ordinal, shared by all rats in vanilla selection order, including after deaths. Advance only for ordinary choices with at least two positive-weight moves; openings stay separate. |

Use `CombatState.RoundNumber` at selection; extra player turns stay in the same round. Reset the later-choice ordinal each round.

**Limit:** different selection counts can still shift choices within a round.

**Rejected alternatives:** percentiles align poorly across eligibility changes; pair sequences lose cross-pair relationships and retain count differences; markers retain filter-history differences after eligibility converges.

###### Fabricator bot identities

Use Fabricator occurrence + `purpose = bot generation` + bot-selection ordinal. Sample uniformly from the requested pool after vanilla's immediate-repeat exclusion, in enemy ID order. Singleton choices consume an ordinal. Preserve spawn order.

###### Behavior preservation

Fresh independent rolls at vanilla conditional probabilities, with preserved sequential eligibility and transitions, preserve the intended joint NoSL distribution.

**Vanilla deviation — SL 4A(↓):** partitioning AI sequences within combat and effective-decision consumption remove rerolls through unrelated or forced rolls.

**Vanilla deviation — SL 4A:** partitioning by enemy identity and rat move rankings change survivor alternatives. For example, in the [reference examples](#enemy-ai-1), Flyconid/Twig M can lose vanilla's guaranteed zero-immediate-damage alternative.

Baseline: [enemy AI mechanics](#enemy-ai-1).

##### Card shuffling

**Intent:** align source-matched cards in Acts 1–2 and usefulness ranks in Act 3, accepting rank-based shifts.

**Scope:** opening and later shuffles use the assignment below and cards' current logical associations. Shuffle outcomes must be independent of original deck sequence.

Assign independent uniform priorities on a common scale, sort all participating cards by priority, then apply existing opening-hand and shuffle modifiers.

Address opening shuffles by [combat address](#combat-addresses) + `purpose = opening shuffle`; later shuffles by combat address + `purpose = later shuffle` + combat-local shuffle ordinal. All later-shuffle triggers share this ordinal. Every executed shuffle consumes one opportunity, including empty and single-card shuffles.

###### Priority assignment

The **shuffle key** identifies the assignment below: a source-based key, or priority-sequence ID + assigned position. It is distinct from the random priority value and the card's final pile position.

The opening Innate subgroup uses [processing order](#card-processing-order), least useful first, with its own priority sequence. For ordinary cards, the first applicable row wins:

| Cards | Acts 1–2 | Act 3 |
|---|---|---|
| Defend and earlier processing order categories | First sequence, least useful first. | Same. |
| Other starting Basics; eligible rewards/direct additions | Source-based priorities below. | Second sequence, most useful first. |
| Remaining cards | Second sequence: earliest acquisition floor first, then ascending processing order. | Both rows combined, most useful first. |

Freeze the card's ordering properties at permanent removal and retain them through run end across [checkpoints](#saved-state). Include the entry in opening priority assignment, then omit it. Additions and reordered states can still shift sequential assignments.

###### Source-based assignment — Acts 1–2

Use [offer groups](#offer-groups) and [permanent-card sequences](#selection-and-consumption) to identify shared supplies. Other starting Basics receive distinct [initial-source keys](#card-occurrence-addresses).

- **Reward offers:** one shuffle key per eligible own-character shared triple. Exclude colorless, other-character and type-specific rewards.
- **Direct additions:** one shuffle key per shared generation entry; allow only unfiltered or transformation-filter-only generation.
- **Triple key assignment:** if one acquired card belongs to the triple, assign it the key. If several do, select the lowest priority derived from event ID + `purpose = assignment` + card ID. Freeze that choice at acquisition; the others enter the remaining group.
- **Transformations:** treat as deletion plus direct addition.
- **Copies and event-local supplementary cards:** enter the remaining group.
- **A0–6 extra-Rare replacements:** follow the [replacement-state rule](#a06-multiple-rares).

These assignments also initialize [combat selection clocks](#combat-card-selection-state). Existing [card reward selection-key aliases](#card-reward-selection-key-aliases) retain their scopes.

###### Shuffle deviations

**Vanilla deviation — SL 5:** stabilizes shuffles across draw/discard choices, often lowering the best attainable fight outcome.

**Vanilla deviation — SL 4A(↓):** state-aware [processing order](#card-processing-order) removes discard-order SL alternatives even with the same participating cards, such as drawing cheaper Frantic Escapes earlier ([baseline](#combat-setup-and-shuffles)). Losing these alternatives can lower the best attainable fight outcome.

##### Random pile insertion

**Intent:** align relative insertion depth across changing pile sizes, with separate randomness for unrelated source groups and shuffles. Neighboring card identities may differ.

Address independent sequences by [combat address](#combat-addresses) + `purpose = insertion` + sharing group + target pile. Source copies share their group.

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

**Within-combat deviation — SL 4A(↓):** partitioning by insertion group and target pile, with a separate shuffle partition, removes cross-sequence rerolls.

Baseline: [pile insertion and discard order](#combat-setup-and-shuffles).

##### Random combat card selection

**Scope:** align random selection of existing combat cards, including combat-start effects and sampled choice offers. For player-selected cards, use [combat-card routing](#combat-card-routing). Mad Science's Improvement follows [permanent upgrades](#permanent-upgrades-and-downgrades); Thieving Hopper follows its [theft policy](#thieving-hopper).

Use combat-local [corrected clocks](#corrected-persistent-clocks) with [shared selection state](#combat-card-selection-state) and [combat addressing and resets](#combat-addresses). Preserve preference/fallback tiers, fixed batches and sequential eligibility changes. Select and process in ascending pre-selection clock order; renew consumed clocks immediately under [shared consumption timing](#opportunity-consumption-timing).

| Shared clock scope | Sources |
|---|---|
| Exhaust | Cinder, unupgraded True Grit |
| Upgrade | Drain Power, Stone Cracker |
| Return | Anointed, Jeweled Mask, Power Cell |
| Attack auto-play | Beat Down, Uproar, Stampede |

Each remaining source has independent clocks: Thrash, Hidden Gem, Bookmark, Mummified Hand, Aggression, Catastrophe and Seeker Strike. Copies of a source share its scope.

**Consumption exceptions:** empty and forced-singleton selections leave clocks and renewal counts unchanged. So do whole-set selections by Anointed, Drain Power, Stone Cracker, Seeker Strike, Aggression and Power Cell; hand returns require space for every selected card.

**Incidental ordering:** for the whole-set cases above except Seeker Strike, order the batch by logical occurrence address, then uniformly shuffle it if it contains at least two cards. Use one dedicated RNG shared by those five sources, addressed by combat + `purpose = incidental shuffle`. Advance only for these shuffles.

**Seeker Strike:** when sampling, use clocks only to choose the offered cards. Apply routing to the player-selected card within the offered subset. Use the vanilla draw-pile selector for display, sorting only the offered cards by ascending rarity, then name.

**Within-combat deviation — SL 4A(↓):** clock partitioning and forced-selection skips remove rerolls. Survivor clocks change which alternatives are reachable by varying discard order or the eligible set.

Baseline: [card selection](#card-state-and-selection) and [shuffle mechanics](#combat-setup-and-shuffles).

##### Thieving Hopper

Use [fixed priorities](#fixed-selection-priorities) with a separate purpose and [reward aliases](#card-reward-selection-key-aliases). Apply [vanilla theft eligibility](#card-state-and-selection) to combat copies, selecting by their `DeckVersion` occurrences.

##### Random enemy targets

**Intent:** align random enemy targets across changing eligible sets.

Use four independent sequences addressed by [combat address](#combat-addresses) + `purpose = targeting` + sharing group:

| Sharing group | Sources |
|---|---|
| Forgotten Soul | All Forgotten Soul selections. |
| Lightning | All Lightning copies, passive activations and evokes. |
| Direct damage | Remaining effects that inherently cause damage, poison, or immediate HP loss; includes power and relic damage and Knife Trap. |
| Other | Mainly includes automatically played debuffs. |

Zero damage preserves classification.

With fewer than two eligible targets, advance nothing.

Reuse [opportunity-specific selection priorities](#opportunity-specific-selection-priorities) with equal weights: opportunity = sequence address + combat-local selection ordinal; selection key = [enemy occurrence address](#enemy-occurrence-addresses). Repeats remain allowed.

**Within-combat deviation — SL 4A(↓):** group partitioning removes cross-group rerolls; singleton skipping removes forced-target rerolls; priorities change alternatives across eligible sets.

Baseline: [random enemy targeting](#random-enemy-targeting).

##### Combat card generation and transformations

**Intent and scope:** align random identities from combat card, power and relic generators and Entropy. [Potion-generated cards](#potion-generated-cards) retain their settled scope.

Use independent sequences addressed by [combat address](#combat-addresses) + `purpose = card generation` + group + pool below. All copies and triggers sharing a group and pool use the same sequence.

| Group | Identity supply and consumers |
|---|---|
| Ordinary costs | One sequence per pool: own character, colorless, each other character, curse and status. Share ordinary-cost generation and Entropy; Jackpot uses the own-character sequence. |
| Free this turn | One own-character sequence shared by generators applying this effect. |
| Free this combat | One own-character Attack sequence for Metamorphosis. |
| Splash | One dedicated sequence over the combined eligible other-character Attacks, uniform across identities. |

Classify cost treatment by the generating source's free effect; natural costs, upgrades and later modifiers leave classification unchanged. Entropy follows the original card's transformation pool regardless of its acquisition source.

Draw identities without a separate rarity roll. Consume one entry per generated identity, including every offered alternative, under [shared consumption timing](#opportunity-consumption-timing).

For each Entropy batch, use [shared transformation batch rules](#transformation-batches), then apply replacements in the existing gameplay execution order.

**Within-combat deviation — SL 4A(↓):** partitioning sequences by pool and cost treatment, while sharing identity supply between generation and Entropy, changes source interactions and removes cross-group rerolls. Within a pool, ordinary-cost generation can advance Entropy's identity supply and vice versa.

**Within-combat deviation — SL 4A(↓):** processing Entropy batches in [identity order](#transformation-batches) removes selection-order rerolls for the same selected batch.

Baseline: [combat generation and transformation mechanics](#combat-card-generation-and-transformations-1).

##### Random energy costs

**Intent:** align cost rolls within card categories while separating combat-local rolls from persistent potion opportunities. Cost rolls remain uniform over 0–3.

| Partition | Sequence address | Assignment |
|---|---|---|
| Confused — Snecko Eye and Fake Snecko Eye | [Combat address](#combat-addresses) + `purpose = Confused cost` + card category | Five sequences, each following draw order. |
| Slither | Combat address + `purpose = Slither cost` | One sequence shared by all enchanted copies, following draw order. |
| Snecko Oil | `purpose = Oil cost` + [potion effect ordinal](#potion-effect-opportunities) + card category | Five fresh sequences per opportunity, using hand-entry order below. |

**Card categories:** starter Strikes; starter Defends; Other Basic 1; Other Basic 2; Others. Other Basic slots follow each character's original starting-deck identity order and remain fixed through removals. Classify by current identity: upgrades and copies retain its category; transformations use the replacement's category. Other Strike-tagged attacks belong to Others.

**Consumption:** consume one entry per eligible cost roll. Confused's X cards and Slither draws that have left hand consume zero.

**Confused + Slither:** when both apply to the same draw, Slither reuses that draw's Confused value at Slither's usual timing, retaining its hand check and leaving its own sequence unchanged. Keep the enchantment; its sequence resumes when Confused is absent. Reapplication preserves late overwrites such as Kingly Kick's draw discount.

**Oil assignment:** after Oil finishes drawing, order eligible cards within each category by latest successful entry into hand, earliest first:

- Draws, retrievals and generation receive entry positions. Retained cards keep theirs; leaving then returning refreshes them.
- Unordered entry batches share one entry ordinal, with internal ascending [processing order](#card-processing-order) frozen at entry.
- Sequential draws and explicitly ordered returns, including All for One, preserve source order.

Apply [combat-card routing](#combat-card-routing) to Oil's hand-entry assignment.

Oil batches are isolated by effect opportunity; the ordinal persists across combats. The [shared deviation](#loss-of-future-outcome-steering) covers lost cross-combat steering of later Oil batches through earlier Confused/Slither rolls or earlier Oil hand sizes.

**Within-combat deviation — SL 4A(↓):** group partitioning, card-category partitions for Confused and Oil, X skipping and overlap reuse remove consumption-based rerolls; processing order within unordered entry batches removes click/enumeration-order Oil rerolls.

Baseline: [random energy costs](#random-energy-costs-1).

##### Random orb types

**Intent:** isolate random-orb generation by combat; preserve uniform sampling.

All sources share one sequence addressed by [combat address](#combat-addresses) + `purpose = orb type`.

Failed channeling consumes zero.

**Vanilla deviation — SL 4A(↓):** retaining entries after failed channeling removes SL rerolls through wasted draws. **Frequency:** rare — require losing all orb slots, regaining, then exploring later random-orb outcomes.

##### Potion-generated cards

**Intent and scope:** align random card generation from Attack, Skill, Power and Colorless Potions, Cosmic Concoction and Orobic Acid across corresponding card-generation opportunities.

Partition generation state by potion type, separately from other combat generation; isolate each complete batch by addressing it with potion type + its [effect ordinal](#potion-effect-opportunities).

Draw identities without a separate rarity roll.

**Vanilla deviation — SL 5(↓):** thieving hopper stealing and other potion types no longer change a potion's generated cards.

Baseline: [potion card-generation mechanics](#potion-card-generation).

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

### Rejected alternatives

#### Combat-gold alternatives

- **Shared rolls with independent compensation:** preserves A3+ normal-gold expectation but makes its distribution nonuniform (**Vanilla deviation — NoSL 3**); Bowler Hat can change the received total by 1.
- **Shared range-relative rolls:** preserves vanilla distributions, but at A3+ swapping normal → elite to elite → normal can change total gold by 1.

#### Event-selection alternatives

- **Fixed scheduling in every act:** improves shared order with matching pools, but loses prefix agreement when survivor pools differ.
- **Refreshed priorities in every act:** simpler and better same-ordinal matching, but more Act 1 shared-order reversals.
- **Cross-act insertion — shared position priorities:** reserve incoming positions by shared output-position priority. Can improve prefix agreement, but preserved less shared-event order in simulated act transitions.
- **Cross-act insertion — weighted merging:** use shared rolls to choose old or incoming entries proportionally to remaining counts. Can improve prefix agreement over sorted priorities, but preserved less shared-event order when tests varied prior event consumption.

## Appendix: existing mechanics

Existing mechanics for comparison: vanilla by default, [Catfood differences](#catfood-addressing-and-combat-rewards) explicitly labeled. Settled policies belong in the main design sections.

### Rooms, events and combat

#### Room resolution

**Unknown-room resolution:** base chances are 10% normal fight / 2% chest / 3% shop; Event takes the remainder (initially 85%). Elites are disabled under ordinary rules. Each roll advances `UnknownMapPoint` once. The selected non-event type resets to its base chance; other allowed non-event types gain their base amount. Excluded types retain their stored odds. Exclusions apply before interpreting the roll, without renormalizing the remaining probabilities. Odds reset between acts; RNG state persists across acts. Catfood leaves this unchanged.

**Unknown-room overflow:** vanilla checks cumulative thresholds in normal fight → elite (if enabled) → chest → shop order. Totals above 100% are neither normalized nor capped in stored state; intervals beyond 1 are unreachable, so later types lose effective probability and Event has none. At 70% normal fight / 14% chest / 21% shop, effective chances are 70% / 14% / 16%.

| Source | Mechanics |
|---|---|
| `?` shop exclusion | Shop is excluded when the previous visited map point contained a shop, or every outgoing map node is a shop. Stored shop odds freeze during exclusion. |
| Juzu Bracelet / Golden Compass | Juzu excludes normal fights from `?` resolution. Golden Compass forces Event during its affected act. |
| Lantern Key | In Act 3, forces `?` to Event and replaces the selected event with War Historian Repy. The ordinary event-list cursor still advances. Like Golden Compass, forced resolution still advances `UnknownMapPoint` once while non-event odds freeze. |

#### Event and encounter identities

**Event RNG:** in single-player, seeded by run seed + deterministic event ID hash (`EventModel.BeginEvent`).

**Event / encounter identity selection:** per-act lists are generated upfront with `UpFront` RNG; event lists shuffle act-specific plus shared identities without explicit weights. Room type is resolved first. An Event advances through its act's pre-shuffled list to the next `IsAllowed` identity not in the run-wide visited-event set; skipped entries are passed over, not retained for the next eligible pull. A normal fight uses the next entry in the act's normal encounter list, regardless of whether its map marker was `?` or a normal-fight marker. Forced-event replacement occurs after ordinary event eligibility scanning.

**Event-list wrapping (vanilla):** the cursor indexes `events[cursor % events.Count]`; the list wraps without reshuffling or clearing visits, and skipped entries remain in it. Skips advance the cursor too. A skipped event can qualify after wrapping; a full unsuccessful scan instead returns the cursor's event anyway, potentially repeated or ineligible.

**Event-pool capacity:** act lists contain 25–31 entries before eligibility/visited filtering, versus 13–15 ordinary floors per act.

**Event skipping and probability:** assume a uniform hidden shuffle, fixed other eligibility and no wrapping. While condition K is false, skipping passes hidden K entries alongside encountered non-K entries; their exchangeable positions preserve K's odds when it becomes true. With two K events and three others, zero, one or two non-K encounters leave the next K chance at `2/5`. Encounters while K is true have the usual without-replacement effect. Individual-seed outcomes can differ; changing other eligibility can change the odds.

**Ancient identity:** selected during upfront room generation, using `UpFront` RNG. Shared Ancients are divided between later acts before each act selects its Ancient. Earlier room visits leave these selections unchanged.

#### Ancient offerings

All eight Ancients select their menus with [event RNG](#event-and-encounter-identities). Matching seed, Ancient and ordered eligible pools reproduce the menu independently of earlier event visits. Changed eligibility can also change which still-eligible options appear.

| Ancient | Menu generation and relevant conditions |
|---|---|
| Neow | Two positive options and one curse option, with fixed exclusion pairs and unlock/player-count restrictions. Pools are fixed in the fully unlocked solo scope. |
| Darv | Shuffles one choice from each eligible relic group; offers three, or two plus Dusty Tome with 50% probability. Ectoplasm and Sozu are Act 2 only. |
| Orobas | One choice from each of three pools. Sea Glass's featured character uses event RNG. The third pool checks for the starter relic (Touch of Orobas) and supported starter card (Archaic Tooth), with a locked option if both are absent. |
| Pael | One choice from each of three pools. Claw needs ≥3 Goopy-eligible cards; Tooth needs ≥5 removable cards; Legion requires no event pet. Second-pool weights: Growth 1; each other eligible option 2. |
| Tezcatara | One choice from each of three pools; Nutritious Soup joins the first while a Basic Strike remains. |
| Tanx | Three choices from a shuffled pool; Tri-Boomerang joins with ≥3 Instinct-eligible cards. |
| Vakuu | One choice from each of three fixed pools. |
| Nonupeipe | Three choices from a shuffled pool; Beautiful Bracelet joins with ≥4 Swift-eligible cards. |

**Scope of consistency:** some granted effects advance shared RNGs. Neow's Bones shuffles its relic candidates with shared `Rewards` RNG. Dusty Tome also calls `Rewards`, but its eligible pool contains exactly one card per character in this version: Corruption, Wraith Form, The Sealed Throne, Forbidden Grimoire or Biased Cognition. Its payload is therefore fixed by character despite advancing `Rewards` once.

**Fur Coat:** selects marked normal-fight/elite map nodes with its own run-seed + player-slot + relic-ID RNG and saves their coordinates. Matching maps reproduce the marks independently of earlier RNG consumption.

#### Event eligibility

**Scope:** vanilla entry predicates for **56 ordinary events** in solo ordinary runs with permanent unlocks satisfied; Ancients are separate. Act availability combines pool membership with entry predicates; run-wide visited exclusions apply throughout.

Act 1 is Overgrowth or Underdocks; Act 2 is Hive; Act 3 is Glory.

| Acts | Condition beyond act | Count | Events |
|---|---|---:|---|
| 1 (Overgrowth) | None | 8 | Aroma of Chaos; Byrdonis' Nest; Dense Vegetation; Jungle Maze Adventure; Sapphire Seed; Tablet of Truth; Wellspring; Wood Carvings |
| 1 (Overgrowth) | Gold ≥ 44 | 1 | Whispering Hollow |
| 1 (Overgrowth) | Gold ≥ 100 | 1 | Morphic Grove |
| 1 (Overgrowth) | Gold ≥ 149 | 1 | Luminous Choir |
| 1 (Overgrowth) | HP ≤ 70% of maximum | 1 | Unrest Site |
| 1 (Underdocks) | None | 5 | Abyssal Baths; Doors of Light and Dark; Drowning Beacon; Spiraling Whirlpool; Sunken Treasury |
| 1 (Underdocks) | Gold ≥ 55 | 1 | Waterlogged Scriptorium |
| 1 (Underdocks) | Gold ≥ 120 | 1 | Endless Conveyor |
| 1 (Underdocks) | HP > 5 | 1 | Trash Heap |
| 1 (Underdocks) | Total floor ≥ 6 | 1 | Punch-Off |
| 1 | None | 1 | Sunken Statue |
| 1 | HP ≥ 10 | 1 | The Legends Were True |
| 2 | None | 8 | Bugslayer; Colorful Philosophers; Doll Room; Field of Man-Sized Holes; Infested Automaton; Lost Wisp; Spirit Grafter; The Lantern Key |
| 2 | Gold ≥ 100 | 1 | Welcome to Wongos |
| 2 | Gold ≥ 125 | 1 | Zen Weaver |
| 2 | HP ≥ 19 | 1 | Colossal Flower |
| 2 | ≥ 2 removable Basic Strikes and ≥ 2 removable Basic Defends | 1 | Amalgamator |
| 2 | ≥ 1 potion | 1 | Stone of All Time |
| 3 | None | 5 | Battleworn Dummy; Hungry for Mushrooms; Reflections; Tinker Time; Trial |
| 3 | HP ≥ 12 | 1 | Round Tea Party |
| 3 | A card eligible for Soul's Power | 1 | Grave of the Forgotten |
| 1–2 | None | 2 | Brain Leech; Room Full of Cheese |
| 1–2 | Gold ≥ 150 | 1 | Tea Master |
| 2–3 | None | 2 | Potion Courier; Symbiote |
| 2–3 | Gold ≥ 100 | 1 | Crystal Sphere |
| 2–3 | Gold ≥ 100 or holding Foul Potion | 1 | Fake Merchant |
| 2–3 | Gold ≥ 100, ≥ 1 potion and ≥ 1 tradable relic | 1 | Ranwid the Elder |
| 2–3 | ≥ 5 tradable relics | 1 | Relic Trader |
| 1–3 | None | 2 | Self Help Book; This Or That |
| 1–3 | Total floor > 6 | 1 | Slippery Bridge |
| 1–3 | ≥ 2 potions | 1 | The Future of Potions |

**Late eligibility checks on selected IDs:**

| Event | Late eligibility check |
|---|---|
| Wood Carvings | a removable Basic card |
| Morphic Grove | ≥ 2 transformable cards |
| Luminous Choir | Available relic stock |
| Spiraling Whirlpool | a card eligible for Spiral |
| The Legends Were True | A nonempty deck |
| Field of Man-Sized Holes | a card eligible for Perfect Fit |
| Slippery Bridge | A removable card |

**Forced event:** War Historian Repy adds one event outside this table: Lantern Key inserts it in Act 3; ordinary `IsAllowed` is always false.

#### Combat setup and shuffles

Encounter setup uses floor-based addressing. Shuffle RNG persists across combats; reshuffles and random pile insertions advance the same generator used for later opening hands.

**Shuffle baseline:** opening shuffles clone cards in deck order (`Player.PopulateCombatState`), then apply Fisher–Yates directly (`CardPile.RandomizeOrderInternal` → `UnstableShuffle`). Later `CardPileCmd.Shuffle` combines discard and remaining draw cards, then uses `StableShuffle`: `List.Sort` followed by Fisher–Yates. Both apply existing shuffle modifiers afterward. `CardModel.CompareTo` compares only card identity and upgrade level, leaving enchantments, persistent progress and combat state tied; tied-copy order can still affect same-seed results.

**Discard-pile order:** affects which cards fit when `AllForOne` returns cards to a nearly full hand; `AggressionPower` and `DrainPower` also sample through order-sensitive shuffles. `FranticEscape.OnPlay` adds 1 cost for the combat, so the shuffle ties above can include copies with different costs, allowing discard order to affect which cost is drawn earlier.

**Random insertion:** `CardPileCmd.Add` uses `Shuffle.NextInt(n + 1)`; `MegaRandom.NextInner` scales a uniform double, matching percentile gap sampling. Lifecycle guards skip insertions before rolling a position.

#### Enemy HP

Enemy creation samples uniformly with `Niche` from the inclusive initial HP range, excluding `MaxHp` values (current/max HP denominators) in the enemy list; an empty candidate set restores the full range (`Creature.SetUniqueMonsterHpValue`). Startup creates/adds enemies sequentially; summons share this path. Death-triggered spawns precede source removal, so dying enemies still count.

**Special cases:** Tough Egg's hatch samples its full hatchling range with `Niche`. Axebot's range rises by 10 per replacement. Decimillipede segments adjust HP to distinct even values. Punch-Off's starting reductions use encounter-setup RNG.

**Summons:** ordinary sampling covers Fabricator's bots, Two-Tailed Rats, Ovicopter's eggs and Phrog Parasite's four sequential Wrigglers. Eye with Teeth (6), Parafright (21) and Gas Bomb (7/8 by ascension) have fixed HP. Gremlin Merc creates Fat, creates/adds Sneaky, then adds Fat: each rolls before the other joins the enemy list (`SurprisePower.AfterDeath`).

#### Enemy AI

**Randomness and selection:** vanilla uses the run-wide `RunRngSet.MonsterAi` for `MonsterModel.RollMove` and Fabricator's bot identities. `RandomBranchState.GetNextState` totals effective weights, calls `NextFloat(total)`, then subtracts each branch's weight in declaration order until the remainder is `<= 0`. Effective weights incorporate repetition limits, cooldowns and dynamic conditions. Singleton and all-zero selections still advance the AI RNG once. Deterministic transitions leave it unchanged. Encounter-assigned starting moves, such as Corpse Slugs' coordinated starts, use setup RNG separately.

**All-zero fallback:** all-zero branches always choose the first listed move. Flyconid can reach `Smash → Vulnerable Spores → Frail Spores → Smash`, making all effective weights zero; it then selects Vulnerable Spores.

**History and interruption:** `MonsterMoveStateMachine` follows branches until reaching a move and retains its state log for weight checks. Ordinary stun returns to the stored move; Thieving Hopper's Flutter depletion uses a deterministic follow-up. Decimillipede normally cycles `Writhe → Constrict → Bulk`; death leads through `Dead → Reattach → random attack`. After Reattach, the three attacks have equal weights. Revival selections share vanilla's AI RNG across segments.

**Duplicate random selectors:** Inklets, Exoskeletons, Scrolls of Biting, Two-Tailed Rats, and possible pairs of small Leaf Slimes beside Slithering Strangler. Leaf Slimes randomize their opening, then alternate forcibly. Exoskeletons choose equally after Enrage; Skitter forces Mandibles, which leads to Enrage. The normal encounter's fourth copy also has a random opening. Scrolls choose equally between Chomp and a second consecutive Chew after their first Chew; the second Chew forces Chomp, followed by More Teeth and Chew. Forced random-branch selections still advance the vanilla AI RNG.

**Same-type death timing:** outer Inklets alternate Jab with a 50/50 Piercing Gaze/Whirlwind choice, starting with Jab; the middle starts with Whirlwind. Remove the middle before its first random selection. Compare killing outer A before its first random selection versus after exactly one. Once B is alone in both plays, its corresponding selections (second onward) use AI RNG positions differing by one advance even with matching eligible moves.

**Eligibility changes:** Two-Tailed Rats require timer, shared summon limit, space and teammate-intent checks to permit Summon. With Screech on cooldown and both attacks eligible, attack odds `1/2, 1/2` become attack/attack/Summon odds `1/11, 1/11, 9/11` when Summon is allowed, using ideal weight ratios. Changed intervals can select a different ordinary attack at the same AI RNG position. After `Screech → Scratch`, blocking Summon can force Disease Bite; this singleton still advances the AI RNG once.

**Rat selection order:** enemies are processed in encounter-slot order, `first` through `fifth`; originals occupy the last three slots. Earlier selected Summon intents block later rats. With equal otherwise-eligible Summon probability `p` and no existing teammate Summon intent, successive rats have probabilities `p`, `(1-p)p`, etc. Existing teammate intents can also block earlier selectors. Sources: `CombatState.SortEnemiesBySlotName`, `CombatManager` player-turn preparation, `TwoTailedRat.CanSummon`.

**Rat history alternatives:** originals open with Scratch, Disease Bite and Screech. Assume the Screech starter and either attacker can be killed before the first random selection. With Summon timer-blocked, the survivor offers Bite/Screech after Scratch or Scratch/Screech after Bite. Vanilla's shared percentile selects Screech in both alternatives or neither: 50% SL availability. Shared move rankings or marker-filtered iid move sequences give `2/3`, preserving each survivor's 50/50 odds. Across all three pairs of opening histories, vanilla interval agreement averages `1/3`, matching shared priorities under equal pair weighting.

**Cross-type RNG advancement:** in the lineup `Leaf Slime S → Twig Slime M → Twig Slime S`, killing either small slime removes different numbers of later AI RNG advances: Leaf S selects through random branches even during forced alternation; Twig S has a fixed attack. This can shift the AI RNG position used for Twig M's next choice.

**Survivor-choice alternatives:** Flyconid opens with `Smash → Frail Spores`; Twig Slime M with `Sticky Shot → attack`. Assume either can be killed before the next intent selection. The sole survivor uses the same next AI roll:

| Roll half | Flyconid survives | Twig M survives |
|---|---|---|
| Lower | Vulnerable Spores | Attack |
| Upper | Smash | Sticky Shot |

Exactly one kill yields a zero-immediate-damage next intent. Choosing before seeing that intent gives a 50% chance; SL reveals which kill achieves it.

**Same-type survivor alternatives:** clear other original Two-Tailed Rats, leaving one at `Scratch → Disease Bite → Summon → Scratch` and its summon opening with Scratch. Their random-selection counts are three and one. Kill either before Scratch executes. Either survivor chooses equally between Disease Bite (7 damage at A7+) and Screech (Frail, zero damage); Summon is spent for the original and timer-blocked for the copy. Vanilla uses the same next roll, yielding the same result for either kill.

**Fabricator:** Fabricate selects a defensive bot (Guardbot/Noisebot), then an offensive bot (Zapbot/Stabbot); Fabricating Strike selects only an offensive bot. Each selection excludes the immediately preceding bot identity across both pools. An intervening defensive bot therefore permits the previous offensive identity again. Full capacity forces deterministic Disintegrate; its bots use fixed moves. In the standard single-player encounter, kill/stun timing changes when selections occur while preserving their order and the sequence of summoned bot identities.

#### Random enemy targeting

Direct random effects and automatic enemy targeting share one run-wide RNG, sampling uniformly among eligible enemies. A singleton selection advances the RNG once; an empty set leaves it unchanged. Automatic play and Lightning retain supplied targets; intrinsically random-hit effects select separately per hit and allow repeats. Catfood preserves these behaviors.

### Relics, shops and chests

#### Relic bags

**Pools and order:** the fully unlocked player bag contains **122 ordinary relics** — 26 Common, 32 Uncommon, 38 Rare, 26 Shop (115 shared + 7 character-specific). The chest bag (`SharedRelicGrabBag`) contains only shared relics. Each bag has separately shuffled per-rarity lists; the two bags overlap in identity.

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

**Filtered relic pulls:** remove the first matching entry from the requested end and compact the list. For one uniformly shuffled rarity with two shop-eligible identities and one shop-ineligible identity:

| First pull | Chance the next unrestricted front pull is shop-ineligible |
|---|---|
| None | `1/3` |
| Filtered front | `2/3` |
| Filtered back | `1/3` |

Both filtered pulls leave eligible supply. Front bias can affect several later positions; its size depends on bag composition.

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

Initial stock and restocks share these player RNGs. Each listed random operation advances its RNG once; relic identity follows bag order. The counts and interactions below exclude additional item effects.

| Restock | `Rewards` advances | `Shops` advances |
|---|---|---|
| Character card | 2: rarity, upgrade check | 2: identity, price |
| Fixed-rarity colorless card | 1: upgrade check | 2: identity, price |
| Relic | 1: rarity | 1: price |
| Potion | 0 | 3: rarity, identity, price |

Ordinary shop cards suppress random upgrades but still advance the RNG once for the check.

**Harmful to same-seed consistency — later restock effects:**

- **Card → relic; relic → character card:** rarity, identity and price can change. Both also shift potion rarity, identity and price; relics shift fixed-rarity colorless card identity/price.
- **Potion → card:** identity and price can change. **Potion → relic:** price only; rarity and bag order stay unchanged.

Thus even one restock per category can interfere. Equivalent slots share generators; matching RNG state, request and candidates preserves outcomes. Events may consume shared rewards/bags despite having event RNG.

### Cards

**Card pools:** Common Power cards do not exist.

#### Card state and selection

**Combat card selection:** selection uses `CombatCardSelection`, except auto-play uses `Shuffle`. `NextItem` advances the RNG once for singletons; batch selectors shuffle the full input before taking their count. See [shuffle mechanics](#combat-setup-and-shuffles) for `StableShuffle` ordering.

**Out-of-combat player-selected card batches:** batch selections are unordered. Deck selectors and reward grids collect the full selection before effects. Enumeration can affect acquisition order (e.g. Sea Glass).

**Deck operations on player-selected cards:** the inspected deck-selection callers cover upgrading (Smith), enchanting (Gnarled Hammer), permanent removal (Empty Cage), transformation (Astrolabe, Claws, Wood Carvings), combining removals with a new addition (Amalgamator), temporary storage for return (Pael's Tooth), and copying (Dolly's Mirror). Wongos and Reflections downgrade randomly. Reward selections acquire new occurrences. Catfood's inspected patches add no operation category.

**Persistent card state:** `SerializableCard.Equals` compares identity, upgrade level and enchantment while omitting card-specific saved properties. Genetic Algorithm maintains `CurrentBlock = 1 + IncreasedBlock`; The Scythe maintains `CurrentDamage = 13 + IncreasedDamage`. Tinker Time grants one configured Mad Science; later copies inherit its type/rider configuration. Dolly's Mirror excludes Quest cards, including Spoils Map.

**Mad Science:** after combat, permanently upgrades random eligible deck cards without replacement, one per power stack, using `CombatCardSelection`.

**Thieving Hopper:** appears at most once in supported runs and steals once. Select uniformly from draw/discard copies with `DeckVersion`, using the first nonempty rarity/enchantment preference tier, or all candidates if every tier is empty (`ThievingHopper.ThieveryMove`). Theft removes the deck card; Hopper's death restores that same object (`SwipePower`).

**Pael's Tooth:** The player selects and removes five unupgraded cards. Each subsequent combat returns one random remaining stored card, upgraded; all five return after five combats. This is stored-card selection without replacement, not new identity generation. Mechanics clarified by the project owner.

#### Combat card generation and transformations

The following generators and the six card-generating potions use shared factories and run-wide `CombatCardGeneration` RNG, sampling uniformly over eligible identities without a separate rarity roll. These factories exclude Basic/Ancient/Event rarities and cards with `CanBeGeneratedInCombat == false`, retaining unlock and player-count restrictions.

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

Metamorphosis generates own-character Attacks, free this combat. Splash offers three distinct Attacks from the combined other-character pools; the player-selected card is free this turn. Jack of All Trades excludes itself.

Stoke, Jackpot, Metamorphosis and Calamity allow duplicate identities. Creative AI and Call of the Void make separate single-card draws, allowing repeats across them. Other multi-card factory batches require distinct identities. Thieving Hopper's theft and random affliction selection also advance `CombatCardGeneration`.

Entropy is the sole random combat transformation source. It transforms selected hand cards in selection order using `CombatCardSelection`, with the [default transformation pool and exclusions](#permanent-transformations-and-direct-additions). Different selected originals can have different eligible lists, making results depend on selection order. Fixed-result transformations and Largesse's multiplayer generation fall outside this policy's scope.

#### Random energy costs

Confused, Slither and Snecko Oil are the only gameplay consumers of the run-wide `CombatEnergyCosts` RNG; combat entry preserves its position. Without intervening Confused/Slither rolls, the next Oil uses the next unused rolls. Their on-draw rolls, including during Oil's own draw, shift its starting RNG position; shuffles, targeting and other RNGs leave it unchanged. Catfood adds no cost-specific patch.

| Source | Vanilla mechanics |
|---|---|
| Confused | Applied by Snecko Eye (Darv) and Fake Snecko Eye (Fake Merchant). Each owned card drawn with nonnegative `EnergyCost.Canonical` (the card's original energy cost) receives a combat-long cost modifier. X cards store zero in this field, so they consume a roll while remaining X. Snecko Eye also adds two cards to turn-start draws. |
| Slither | Wood Carvings' Snake enchantment. Rerolls its card on draw only while it remains in hand, with a combat-long modifier. Enchanting excludes X and Unplayable cards. |
| Snecko Oil | Draws seven, then iterates the resulting hand in hand order. Rerolls non-X cards with nonnegative unmodified energy cost until turn end or next play, whichever comes first. |

Draw hooks resolve powers before cards and their enchantments: Confused rolls first, Kingly Kick applies its −1 draw discount, then Slither overwrites it with a separate roll. Hellraiser's earlier draw hook can play a drawn Strike before Slither checks hand presence.

#### Slippery Bridge selection

Each offer uses event RNG to select eligible card copies uniformly; duplicates count separately. Eternal cards are not removable.

- **First offer:** removable non-Basic cards.
- **Rerolls:** record the rejected copy. From removable cards, exclude previously rejected copies and all copies of the previous offer's identity, regardless of status. Basics are allowed.
- **Empty candidate set, including the first offer:** select uniformly from all removable copies. Keep the rejected set; reapply normal filters next reroll. Fallback can repeat the previous copy or identity before every copy has been offered.

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

**Permanent transformation's target:** callers use event RNG, `Niche` or player `Transformations` RNG. Use the original card's pool, except Quest cards and Event/Ancient/Token-rarity cards use colorless. Token is the generated-card rarity of Shiv, Soul and similar cards. Ordinary results are Common/Uncommon/Rare; curses share one curse pool. Preserve unlock/player restrictions and exclude the original identity. `CardFactory` preserves pool enumeration order through filtering, then rolls a uniform index into the eligible list, advancing the RNG once; batches allow repeated results. Combat transformations additionally filter by `CanBeGeneratedInCombat` and may use the Status pool.

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

**Potion drops:** one shared probability starts at 40%; a generated drop subtracts 10 percentage points, a miss adds 10. Elites add 12.5 percentage points to the check. No act/Ancient reset. Checks use shared `Rewards` RNG; forced drops skip both RNG advancement for the check and the probability update.

**Potion generation:** ordinary generation rolls rarity at 65% Common / 25% Uncommon / 10% Rare, then draws an identity. Factory batches exclude duplicate identities within the batch; separate calls can repeat them. Generation precedes acquisition; failed acquisition does not restore consumed RNG state.

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

These six potions and Snecko Oil are combat-only.

All six use the [combat-card factory mechanics](#combat-card-generation-and-transformations-1). Dingy Rug and Prismatic Gem do not expand these potions' eligible card pools, matching vanilla.

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

**Combat card rewards:** identities/upgrades use room type, count and offer position, separating normal-fight and elite offers. Rarity is rolled before drawing an identity; a Rare still advances the current slot's identity RNG once. There are no independent per-rarity identity sequences. Later identities can still change through rarity-offset resets and offer duplicate exclusion. The initial offer uses the RNG intended for rerolls: a code-confirmed wrong-RNG bug; gameplay impact has not been reproduced.

**Card rarity:** normal fights and elites have separate offsets with vanilla base probabilities, non-rare growth and post-Rare reset value. Shops have a separate offset that does not grow, leaving their Rare roll chance at 0% (4% without Scarcity). Ancient entry, including Neow, resets these offsets to `-0.05`. The separately stored boss offset has no effect; boss rewards no longer reset normal-fight or elite offsets. Combat rerolls update their room type's rarity state, potentially shifting later offers; non-Rare rerolls raise the offset, as in vanilla.

**Direct/event generation:** card factories seed by adding room/event count to a per-room options-call index. Seed collisions exist; harmful gameplay impact remains unverified. Transformations retain caller-supplied RNGs. Neither uses dedicated shared identity sequences by pool.

**Encounter setup:** inherits vanilla event/encounter identity selection; cumulative room counters do not select identities. Encounter ID + room-type ordinal controls variable composition/setup, not ordinary HP. No visible setup mismatch has been reproduced. Lantern Key's forced event is counted normally.

**Shuffles:** re-seeds shuffle RNG at combat start; later shuffles continue it.
