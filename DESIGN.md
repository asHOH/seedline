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

Assess a seed's first playthrough.

Consistency fixes with no gameplay impact are ungraded.

**Unnoticeable:** imperceptible across runs, considering frequency and effect size; effects on win chance provide supporting evidence. Use 500 runs as a rough benchmark, with the [A7+ one-Rare cap](#a7-one-rare-per-offer-and-deferral) as the reference.

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

**Default difficulty:** Scarcity (Ascension 7+); below A7, use A0–6 probabilities.

**Discussion boundary:** describe base random outcomes and modifier interactions that affect random operations. Preserve modifiers, including eggs, Fresnel Lens and fixed discounts.

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
- **Ordinal-addressed generation:** randomness derived from an opportunity's ordinal within its scope.
- **Logical occurrence / physical copy:** a logical occurrence is a persistent record for one copy; a physical copy is the card currently associated with that record.
- **Occurrence address:** a logical occurrence's source ID, distinct for each copy.
- **Selection key / selection-key alias:** a selection key is a candidate's ID for a selection purpose; an alias is a source-specified replacement key for that purpose.
- **[Selection state](#addressing-and-state):** hidden information carried by a logical occurrence that governs its future selection.
- **Routing:** selecting logical occurrences among eligible cards with the same identity and gameplay-relevant status, then associating them with player-selected physical copies.

## Architecture overview

The mod aligns same-seed opportunities through addresses, shared state for related sources, and isolation of unrelated random operations. A source may use different sharing scopes and state for whether and when a result appears, and for its rarity, identity and attributes.

### Rewards

| Decision area | Policies |
|---|---|
| Relics and chests | [Relic allocation and borrowing](#relics); [chest rewards and advancement](#chests). |
| Potions | [Generation and drops](#potions). |
| Shops | [Shops, Wongos and Courier](#shops-wongos-and-courier). |
| Cards | [Reward offers and Rare timing](#card-reward-offers); [permanent transformations and direct additions](#permanent-transformations-and-direct-card-additions). |
| Other rewards | [Combat gold](#combat-gold); [event-specific rewards](#event-specific-rewards). |

### Encounter

- **Rooms and events:** align unforced `?` resolutions within each act and improve event agreement as eligible pools change.
- **Selection:** selection keys and [persistent selection state](#persistent-clocks) retain alignment across choices. [Routing](#routing) makes clicks on equivalent copies select the same logical occurrence; eligibility histories and position-dependent effects still matter.
- **Combat:** scope setup, enemy behavior and card randomness to each battle. Potion-generated cards and Oil costs use potion-effect ordinals across battles.

#### Non-combat

| Decision area | Policies |
|---|---|
| Rooms and events | [Unknown rooms](#unknown-room-resolution); [ordinary events](#ordinary-event-selection); [Lantern Key](#lantern-key); [Ancient offerings](#ancient-offerings). |
| Event selections and trades | [The Future of Potions](#the-future-of-potions-card-type-assignment); [Ranwid relic sacrifice](#ranwid-relic-sacrifice); [Relic Trader](#relic-trader); [Endless Conveyor](#endless-conveyor). |
| Card and potion selection | [Wing Charm](#wing-charm); [persistent selection](#persistent-selection). |

#### Combat

| Decision area | Policies |
|---|---|
| Setup and enemies | [Combat setup](#combat-setup); [enemy HP](#enemy-hp); [enemy AI](#enemy-ai). |
| Card piles | [Card shuffling](#card-shuffling); [random pile insertion](#random-pile-insertion). |
| Random selection | [Combat cards](#random-combat-card-selection); [Thieving Hopper](#thieving-hopper); [enemy targets](#random-enemy-targets). |
| Generation and effects | [Card generation and transformations](#combat-card-generation-and-transformations); [random energy costs](#random-energy-costs); [random orb types](#random-orb-types); [potion-generated cards](#potion-generated-cards). |

### Generation sharing

Sources sharing a sequence advance a common progression. Separate supplies limit that interference; finite relic allocations can still interact through [borrowing](#borrowing). Ordinal addressing gives opportunities their own randomness, as with initial shop cards.

"Shared" spans each row's named sources or offer category, within the stated partitions. Source policies supply local exceptions.

Rules: [rarity and identity sequences](#rarity-and-identity-sequences).

#### Relics, potions and event-specific rewards

| Sources | Rarity | Identity supply |
|---|---|---|
| [Ordinary relics](#allocation): generic rewards / shop / Courier | Separate source sequences. | Disjoint finite allocations. |
| [Relic Trader](#replacement-relics) | By event ID and reward slot. | Courier allocation. |
| [Potions](#generation): generic rewards / shop / Courier / generating effects | Per-source sequences; generic rewards split standard-weight and uniform-identity modes. | Per-source, per-rarity sequences. |
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
| [Permanent transformations / direct additions](#permanent-transformations-and-direct-card-additions) | Transformations: no rarity roll. Random-rarity additions: per-pool sequences; Infested Automaton uses event RNG. | One shared uniform identity sequence per pool. |
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

### Shared result supply

**Invariant:** when the next draw cannot satisfy a request and comparable requests are expected later in the same sharing scope, prefer resolving it from that supply, accepting shifts to later draws. Don't generate a result only for the current opportunity, which may never appear in other runs.

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
- **Marker resolution:** move the next eligible entry after it into the marker's position. Leave the same filter's marker at the supplying position. Resolve markers only when encountered by forward draws or recursive marker resolution.
- **Unfiltered draw:** resolve any front marker, then return the entry and remove the drawn position.

**Marker collisions:** when a draw using filter `Y` encounters a marker for filter `X`, resolve that marker first.

- If the resolved entry fails `Y`, retain it at the marker's former position and continue searching for `Y`.
- If it satisfies `Y`, return it and place a marker for `Y` there. The marker for `X` remains at the supplying position, representing an advancement of `X` followed by an advancement of `Y`.

Apply the rule recursively to further collisions, including identical filters: two advancements require two markers.

If an allocation has no eligible entry to resolve a marker after [borrowing](#borrowing), expire that marker, then continue the draw.

### Relic tail draws

Scan stored positions backward, skipping every marker and ineligible entry. Remove the first eligible relic.

### Fixed selection priorities

**Scope:** one choice or batch per run within each sharing scope.

Calculate each candidate's independent uniform priority `U_i ∈ (0, 1)` only at the selection opportunity, from purpose + sharing scope + selection key. Select the lowest eligible priorities.

**Weighted single choice:** among candidates with weight `w_i`, select the lowest `-ln(U_i) / w_i`. Selection probability is `w_i / Σw`. Fixed relative weights preserve survivors' ranking when candidates are removed.

### Persistent clocks

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

**Scope:** [out-of-combat](#out-of-combat-card-routing) and [combat](#combat-card-routing) operations on player-selected cards. These policies define equivalence and operation sharing.

Use [persistent clocks](#persistent-clocks) for selection. An already randomly selected logical target bypasses routing.

**Routing boundary:** preserve display order. Apply effects at the clicked copies' pile positions and follow the source's movement and ordering rules. For example, Hologram removes the clicked discard entry, and All for One later reads the remaining entries in discard order.

**Batch association:** use the clock mechanism's eligibility snapshot and freeze presentation order with it. Only classes containing a clicked copy participate in reassignment and routing-clock updates.

1. In each participating class, pair selected occurrences in ascending pre-selection routing-clock order with clicked copies in frozen presentation order.
2. Assign each participating class's remaining logical occurrences in address order to its unclicked copies in frozen presentation order.
3. Apply effects after selection is complete.

Commit the association permutation and clock update with the action. Renew each selected routing clock once if its logical occurrence survives, using its per-occurrence renewal count. Singleton and whole-class player selections follow this rule.

**Coupling limit:** for single selections from nested sets of two and three selection keys, shared independent priorities select the same key with probability `2/3`, maximal for uniform selection. Different selection keys or eligibility histories can reduce agreement.

### Opportunity-specific selection priorities

Apply [fixed-priority sampling](#fixed-selection-priorities) afresh per opportunity. Derive priorities from purpose + opportunity address + selection key, excluding candidate sets and list positions.

## Shared policies

### Addressing and state

**Selection state:** an occurrence's address, selection keys and aliases, [residual clocks and renewal counts](#persistent-clocks).

#### Shared addresses

##### Event addresses

Event ID defines event-local scope. Retained [event RNG](#event-and-encounter-identities) follows baseline seeding.

##### Combat addresses

Ordinary combats use combat category + act-local combat ordinal + encounter ID, with separate normal-fight, elite and boss counters.

Event-created battles outside ordinary encounter lists use event-local battle ordinal + encounter ID, leaving ordinary combat counters unchanged.

Combat-local random state derives from purpose + combat address and resets each combat. The [steering deviation](#loss-of-future-outcome-steering) applies where this removes cross-combat outcome steering.

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
| Move, temporarily store, or recreate the physical object for an existing occurrence | Retain the logical occurrence and its selection state. |
| Change status or eligibility | The change itself preserves the occurrence and its selection state through equivalence-class splits and merges, subject to [upgrade/downgrade refills](#permanent-upgrades-and-downgrades). |
| Copy a card | Create a new occurrence and selection state from the granting source. Consume no routing clocks. |
| Permanently remove or combine cards | Retire the selected logical inputs. |
| [Transform a card](#permanent-transformations-and-direct-additions) | Retire the original and initialize replacement selection state. |

#### Combat-card selection state

For combat [selection](#random-combat-card-selection) and [routing](#combat-card-routing), use combat-local [persistent clocks](#persistent-clocks) with one frozen selection key per occurrence. Use a separate per-occurrence renewal count within each sharing scope.

**At combat start:** before selection effects, use each card's [shuffle key](#priority-assignment) under ordinary-card assignment, including opening removed-entry reservations. Innate separation applies only to shuffling.

**During combat:** take the first applicable key for a new occurrence:

| Card source | Selection key |
|---|---|
| Identity drawn from a generation sequence | Original [sequence entry address](#entry-addresses). |
| Fixed token creation | Token ID + its creation ordinal in this combat. Count each new token of that identity admitted to combat once across all granting sources. |
| Other grants | Granting-source ID + committed grant ordinal within that source for this combat + copy index. Copies of the source share its count. Assign batch indices in [processing order](#card-processing-order). |

Pile movement and status or eligibility changes themselves preserve keys, residual clocks and renewal counts. A copy starts fresh. A transformation retires the input's combat state and initializes the replacement under the rules above.

#### Potion effect opportunities

**Scope:** [potion-generated cards](#potion-generated-cards) and [Snecko Oil costs](#random-energy-costs).

Each potion type has its own run-wide effect ordinal:

| Action | Ordinal advancement |
|---|---|
| Committed use, even when no rolls occur | Advance once. |
| Discard or trade | Advance once. |
| Skip a potion reward on the reward screen | Advance once per skipped reward. |
| Acquire | None. |

**Cauldron exception:** Each potion offered by Cauldron grants one exemption for its type — any action that would advance that type's effect ordinal consumes one available exemption instead. Unused exemptions expire at next-room entry.

**Vanilla deviation — SL 5(↑):** discard, trade and reward skips advance the next same-type effect batch. Preserving favorable randomness for later combats therefore requires picking up and keeping other same-type potions.

### Rarity and identity sequences

**Main idea:** align successive opportunities for the same kind of item across routes, with independent sharing scopes and sequence state for rarity and identity.

**Scope:** relics, potions, cards, permanent transformations and covered direct additions.

When a source rolls rarity, consume one value from its **rarity-roll sequence**; fixed-rarity requests consume identity only. Use [filtered draws](#filtered-draws) on the per-rarity **identity sequence** where present; otherwise filter the pool's sequence to the requested rarity, if any. Source policies specify sharing and RNG exceptions; [generation sharing](#generation-sharing) summarizes them.

**Generation and lifetime:** potion/card identity and rarity-roll sequences generate on demand indefinitely, using independent RNGs for the specified source and pool partitions. Each newly generated potion/card identity entry is an independent uniform draw with replacement from its sequence's pool unless the source specifies otherwise, such as weighted or grouped generation. Only finite relic allocations [borrow](#borrowing).

Apply the filtered-draw mechanism to identity sequences, including shop-batch duplicate filtering, without additional rarity rolls, except for [relic tail draws](#relic-tail-draws) and [Wongos's identity removal](#wongos). Skipped entries remain available to eligible draws and borrowing.

Triple sequences retain each slot's rarity while rejecting duplicate identity candidates within that triple during generation. Other card sequences apply source-required duplicate exclusions through filtered draws.

### Card selection and processing

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

Preserve ties regardless of selection state, acquisition order or physical position; retain every copy. Tied-copy permutations must leave gameplay outcomes and subsequent random state equivalent under [card equivalence](#card-equivalence).

Preserve display order and intentionally sequential effects; reward offers use their entry order before presentation-only swaps.

#### Transformation batches

A transformation batch comprises the originals selected together.

Generate replacements for selected originals in [identity-only order](CARD_ORDER.md#identity-only-order). Within each input identity, pair replacements in generation order with originals in frozen presentation order.

## Source policies — Rewards

### Relics

**Intent:** limit interference between generic, shop and Courier relic rewards.

#### Allocation

Construct disjoint generic-reward, shop and Courier allocations once, targeting **45% / 35% / 20%** of each Common/Uncommon/Rare pool. Include shop-ineligible relics. Use these fully unlocked counts:

| Rarity | Generic rewards | Shop | Courier |
|---|---|---|---|
| Common | 12 | 9 | 5 |
| Uncommon | 14 | 11 | 7 |
| Rare | 17 | 13 | 8 |

Each source has one rarity-roll sequence, generated on demand indefinitely, and independently advancing, shuffled identity sequences per rarity. The shop also has one shuffled sequence containing all Shop-rarity identities.

Without [borrowing](#borrowing), generic reward opportunities must eventually expose every still-eligible entry, and other sources' multi-choice offers must preserve unchosen generic entries.

#### Source classification

By default, hidden random relic rewards use the generic rewards source, including paid rewards and relic-triggered grants. Classify deferred payoffs separately from their granting relic.

#### Borrowing

**Consistency boundary:** source-partitioning guarantees exclude borrowing's effects. Chest refill and final fallback require no alignment.

When the requested rarity has no eligible local entry, borrow from another source's allocation of that rarity using a [tail draw](#relic-tail-draws). Courier borrows from generic rewards, then shop; generic rewards and shop borrow from Courier, then the remaining source.

Apply borrowing during [marker resolution](#filtered-draws), but skip filtered-draw marker placement in the donor allocation.

**Chest refill:** when a chest reward has no eligible entry after same-rarity borrowing, retain its generic rarity result and select uniformly among eligible identities of that rarity in the original shared relic pool, advancing the generic relic rarity RNG once more. If none qualify, use the final fallback.

**Final fallback:** from the requested rarity onward in Shop → Common → Uncommon → Rare, try local entries then same-rarity borrowing at each rarity. Return Circlet if all fail; make no additional rarity roll.

Baseline: [relic bags](#relic-bags).

### Chests

#### Reward generation

On entering a non-empty chest room, advance the chest ordinal once and generate both gold and relic rewards.

- **Gold:** generate base gold from the chest ordinal, independently of combat gold.
- **Relic:** consume one generic rarity-roll entry, derive the [marker-lifetime roll](#finite-lifetime-chest-markers), and exclude the drawn identity from later chests.

Silver Crucible's empty chest room generates neither reward and leaves the chest ordinal unchanged.

#### Relic advancement

**Intent:** preserve skipped relics for ordinary rewards, align common future identities, and spread acquisition's character-specific probability increase across later positions.

Draw the next eligible shared relic from the generic identity sequence for the rolled rarity. Replace a drawn sequence entry with a [finite-lifetime marker](#finite-lifetime-chest-markers).

- **Acquire:** retire the relic; the marker disappears at expiry, compacting survivors.
- **Skip, including unopened chests:** save the drawn entry with the marker as its **carried entry**; at expiry it replaces the marker as an [ordinary reward entry](#opportunity-consumption-timing).

Resolve the choice before expiry, including `x = 0`. Permanent ineligibility or acquisition elsewhere clears the carried entry, preserving the marker's lifetime.

Refill and Circlet rewards bypass markers: skipping leaves stock unchanged; acquisition retains [baseline identity removal](#relic-bags).

**Rejected alternative:** a separate chest allocation adds unfairness between reward sources.

##### Finite-lifetime chest markers

Use the [filtered-draw advancement and collision rules](#filtered-draws). A chest marker expires after `x` eligible-entry advances or when its resolution exhausts eligible supply after borrowing. Each transfer decrements `x` and moves the marker to the supplying position. Collisions preserve each marker's lifetime and carried entry.

**Lifetime:** use `x = floor(u * S)`, with uniform `u` in `[0, 1)` addressed by `purpose = marker lifetime` + rarity + non-empty chest ordinal.

Before recording the offer, `S` counts currently available, chest-eligible shared identities of that rarity across the generic, shop and Courier sequences, including the drawn entry. Exclude previous chest offers and consumed entries, including unbought shop displays.

Other-source consumption can shorten future marker lifetimes; existing lifetimes remain fixed.

**Probability:** a single acquisition matches vanilla's character-specific probabilities under the [reference assumptions](#chest-marker-probabilities).

**Alignment:** with identical subsequent draw requests and eligibility, and no borrowing, acquire and skip share the future sequence until expiry.

**Vanilla deviation — NoSL 3:** skipping a chest, visiting a shop, then acquiring another chest's relic can slightly shift later character-specific reward probabilities.

Baseline: [chest mechanics](#shops-chests-and-relic-events).

### Potions

**Intent:** align results across batch sizes; partition repeatable generation from ordinary rewards.

Potion copies carry no persistent selection state; use, discard and trade affect the selected copy, preserving slot-dependent outcomes.

#### Generation

Separate **generic rewards**, **shop**, **Courier** and **potion-generating effects**. Generic rewards combine combat, event and other grants regardless of cost; generating effects cover Alchemize, Entropic Brew and Delicate Frond, including outside combat.

Each source has Common/Uncommon/Rare identity sequences over the full eligible pool and a [standard-weight rarity sequence](#potions-1). Generic uniform-identity rewards use a separate rarity sequence weighted by eligible identity counts, sharing the identity sequences.

**Consumption:** direct inventory grants advance rarity and identity state only for acquired potions, including partial batches. Offers follow [consumption timing](#opportunity-consumption-timing), even if unclaimed. Preserve failure feedback.

**Batches:** consume per potion and retain [source duplicate rules](#potions-1); regrouping `A | BCD` yields `ABC | D`, subject to filtering.

**Vanilla deviation — SL 5(↓):** Alchemize with full slots can no longer bypass an unwanted potion.

#### Drop checks

Derive each drop-check roll from the drop-check ordinal.

**White Beast Statue:** freeze the drop-check ordinal and probability; forced rewards still advance generic potion generation.

### Shops, Wongos and Courier

**Intent:** align shops independently of rewards and Courier restocks.

#### Stock and prices

- **Initial cards:** generate card identities and random rarities by shop ordinal.
- **Card rarity:** initial and restocked character cards use [vanilla shop base chances](#card-reward-offers-1) plus a [calibrated fixed offset](#rarity-calibration) of `0` at A7+ and `+0.018` at A0–6.
- **Courier cards:** character cards use per-type rarity sequences and per-type, per-rarity identity sequences; colorless cards use per-rarity identity sequences.
- **Sale slot:** derive independently from shop ordinal.
- **Base prices:** derive from `purpose = base price` + [entry address](#entry-addresses) for initial stock and Courier restocks. Initial shop cards use shop ordinal + slot as their entry address.

**Alignment boundary:** eligibility changes, including [relic floor cutoffs](#relic-bags), can weaken stock alignment.

**Vanilla deviation — NoSL 4B↓:** fixed shop rarity offsets remove recent combat rarities' influence on shop Rare odds and change Rare clustering.

**Vanilla deviation — SL 4A(↓):** partitioned restock sequences remove restock manipulation through interleaved purchase categories and effects.

**Vanilla deviation — SL 4A(↓):** entry-bound prices remove price manipulation through unrelated purchase order.

Baseline: [shop mechanics](#shop-and-courier-rng).

#### Wongos

The featured Rare uses shop stock. Suppress generation when unaffordable as a [consistency fix](#vanilla-deviations).

**Identity removal:** for the featured Rare and Common purchase, resolve encountered markers normally, then remove the first shop-eligible entry from its source without placing a new marker. This retains [vanilla's promotion of skipped identities](#relic-bags).

**Vanilla deviation — NoSL 3:** sourcing the featured Rare from shop stock removes its vanilla advancement of shop-ineligible Rares toward later generic rewards.

Baseline: [Wongos offers and rewards](#shops-chests-and-relic-events).

### Card reward offers

**Intent:** align reward choices across related sources and preserve own-character triple boundaries across offer sizes.

**Scope:** initial generation and reachable rerolls for the offer groups below.

#### Offer groups

Upgrade rules and the number taken leave identity sharing unchanged.

Own-character random-rarity offers share [luck and thresholds](#luck-and-rarity-offset), [non-Rare and rare-bearing triples](#offer-sequences), and [A0–6 extra Rares](#a06-multiple-rares):

| Rare odds | Sources and offer size |
|---|---|
| Normal-fight chance with the shared combat offset. | Normal fights, Dream Catcher, Prayer Wheel and The Hunt in normal fights: three-card offers. |
| Elite chance with the same combat offset. | Elites and The Hunt in elite combats: three-card offers. |
| Base Rare odds without the combat offset. | Orrery: five three-card offers; Lost Coffer: one; Trial's Nondescript Guilty: two. Brain Leech's Share Knowledge: choose one of five, using [supplementary cards](#supplementary-cards). |

##### Other offer groups

| Offers | Rarity and identity supply |
|---|---|
| Own-character fixed Rare | Separate Rare triple sequence; one triple per offer: non-final bosses, White Star, The Hunt's delivered boss rewards, Hefty Tablet, Glass Eye and Crystal Sphere. |
| Own-character fixed Common / Uncommon | Separate triple sequence per rarity. Glass Eye consumes two triples per rarity; Crystal Sphere one. Common exceptions: [Room Full of Cheese](#supplementary-cards), [Scroll Boxes](#scroll-boxes). |
| Colorless: Lead Paperweight / Brain Leech's Rip | Shared rarity-roll sequence at Uncommon/Rare odds **98.51% / 1.49%**, with shared Uncommon/Rare identity sequences. |
| Other-character | Kaleidoscope uses event RNG for character and rarity; Colorful Philosophers / Sea Glass request fixed rarities. Share Common/Uncommon/Rare identity sequences per character. |

Colorless and other-character rewards consume individual entries across batch sizes.

**Crystal Sphere:** does not reset the combat rarity offset.

##### Type-specific offers

Lasting Candy and The Future of Potions share own-character Uncommon and Rare Power identity sequences, consuming individual entries.

- **Lasting Candy:** separate normal-fight and elite rarity sequences at source base odds, with unavailable-rarity fallback; boss triggers request Rare.
- **The Future of Potions:** request the option's rarity; Attack/Skill identities use event RNG addressed by rarity and type.

#### Upgrade rolls

At the source's random upgrade check, use a uniform `[0, 1)` roll derived from `purpose = upgrade` + the card's [entry address](#entry-addresses).

Baseline upgrade chances: [card rarity and upgrades](#card-reward-offers-1).

#### Offer procedure

For own-character random-rarity offers:

1. Simulate three slots (all five for Brain Leech's Share Knowledge) under [Rare timing and delivery](#rare-timing-and-delivery).
2. Consume the next [rare-bearing triple](#offer-sequences) if a Rare was delivered, otherwise the next non-Rare triple.
3. Append any [supplementary cards](#supplementary-cards), then apply [A0–6 extra-Rare replacements](#a06-multiple-rares) where applicable.

Replacements precede reward enchantments; [presentation swaps](#presentation) follow step 3.

#### Rare timing and delivery

**Scope:** the three own-character random-rarity [offer groups](#offer-groups).

**Intent:** separate Rare timing from offer identities while preserving source-dependent Rare chances.

##### Luck and rarity offset

**State:** luck `L` starts at zero; the combat offset `o` starts at `-0.05`. The threshold is `T = -ln(U)`, where `U` is uniform on `(0, 1)`. Thresholds use an independent sequence addressed by purpose and threshold ordinal.

**Per slot:** process card slots in order. Each adds `-ln(1 - p)` to luck, where `p` is its Rare probability:

- Normal-fight-rule and elite slots use `max(0, base Rare chance + o)` with the shared combat offset.
- Fixed-base-odds slots use their base Rare chance without reading or modifying `o`.

**Rare delivery:** subject to the difficulty-specific cap below, a slot reaching the threshold becomes Rare. Reset luck to zero, discard the overshoot and advance to the next threshold.

**Combat offset updates:**

- Normal-fight-rule and elite non-Rare slots add `0.005` (`0.01` without Scarcity), capped at `0.40`. Zero-probability slots still grow the offset while adding no luck.
- A delivered Rare in either group resets the offset to `-0.05`.
- Boss rewards reset the combat offset, preserving luck and the threshold.
- Between acts, reset the offset to `-0.05`; carry luck, the threshold and sequence state forward.

**Rationale:** before deferral, exponential thresholds preserve the supplied waiting probabilities: `P(no Rare through n slots) = exp(-sum(-ln(1 - p_i))) = product(1 - p_i)`.

**Competition effect — luck coupling:** for three-card offers with equal starting luck, threshold and offset, an elite leaves more luck than a normal fight if neither delivers a Rare. The lead survives boss rewards and act transitions; with equal later increments, the elite branch reaches the shared threshold no later. This guarantee ends when either branch delivers a Rare.

##### A7+: one Rare per offer and deferral

Cap each offer at one Rare. If a later slot reaches the next threshold:

- Keep that threshold pending and continue accumulating luck until delivery; only one Rare can remain pending.
- Treat suppressed slots as non-Rare for applicable offset updates.

An offer is **Rare-eligible** if at least one slot has positive Rare probability along its all-non-Rare progression, ignoring pending delivery.

- **Next eligible offer:** replace its first simulated slot's ordinary check with the pending Rare. Clear pending and apply the [delivery and offset rules](#luck-and-rarity-offset), then process remaining slots normally.
- **Ineligible offer:** retain the pending threshold and apply normal non-Rare offset growth.

This excludes the first two three-card normal-rule offers after an offset reset without elite intervention. Delivery follows offer generation order, including extra offers and rerolls, and can wait across acts.

**Vanilla deviation — NoSL 3:** extra Rares in elite and fixed-odds offers are deferred, changing acquisition opportunities; additional Rares beyond the single pending one are omitted. **Frequency:** rare — about **0.2%** of [sampled A7+ runs reaching Act 3's first boss](outputs/run-history/README.md#current-result).

##### A0–6: Rare delivery

Deliver every threshold crossing immediately.

#### Offer construction

##### Offer sequences

Own-character random-rarity offers use two triple sequences independent of the luck thresholds. The **non-Rare sequence** supplies three Common/Uncommon cards per triple; the **rare-bearing sequence** supplies a Rare followed by two Common/Uncommon cards before presentation swaps.

**Rationale:** shared sequences preserve triple alignment when a Rare moves between sources; separate normal/elite sequences advance differently.

##### Uncommon probabilities

Use these Uncommon probabilities for each Common/Uncommon entry, except for the opening triples:

| Difficulty | Non-Rare offers | Rare-bearing companions |
|---|---|---|
| A7+ | 38% | 39% |
| A0–6 | 40% | 40.5% |

These rounded rates approximate expected displayed Uncommon totals in the [three-act calibration](#rarity-calibration).

**Opening triples:** the first two non-Rare triples in the run use [vanilla's per-slot probabilities](#card-reward-offers-1) for two consecutive normal-fight offers after an offset reset, conditional on no Rare. At A0–6 these are **35%, 36%, 37%**, then **37/99, 37/98, 37/97**.

These triples follow shared consumption across sources.

**Rationale:** these entries approximate an Act 1 opening of two normal fights. Later-act openings are covered by the average calibration.

**Vanilla deviation — NoSL 3:** shared Uncommon rates remove elites' advantage (about 0.14 extra Uncommons per three-card non-Rare offer near zero starting offset) and change source- and route-specific rates. Few elite rewards, some Rare-bearing, limit the impact; elites retain relic, gold and higher Rare-chance incentives.

##### Supplementary cards

**Brain Leech — Share Knowledge:** generate two additional Common/Uncommon cards with event RNG, excluding identities already in the offer. Use the [Uncommon-probability table](#uncommon-probabilities) according to whether a Rare was delivered.

**Room Full of Cheese — Gorge:** use one Common triple and generate the remaining five Common cards with event RNG.

##### A0–6: multiple Rares

Own-character random-rarity offers share one extra-Rare identity sequence per character, independent of fixed-Rare triples. For an offer with `k >= 1` Rares, draw `k - 1` entries from this sequence, excluding the original Rare and earlier extra Rares.

Replace the first Common in offer entry order, or the first Uncommon if no Common remains, for each extra Rare. Apply this across the entire offer, including Brain Leech's two event-generated cards: `R,U,U | C,C` becomes `R,U,U | R,C` for two Rares. Replaced companions remain consumed.

**Replacement state:** identity and [upgrade roll](#upgrade-rolls) come from the new Rare entry. Retain the replaced slot's Wing Charm priority or supplementary-card membership, and its [Acts 1–2 shuffle membership](#source-based-assignment--acts-12).

**Accepted tradeoff:** prior Rare counts and companion composition can weaken alignment between offers built from the same triple.

**Vanilla deviation — NoSL 3:** Common-first replacement favors Uncommon companions in multi-Rare offers. **Frequency:** rare — about **2% of vanilla elite offers** in the [A0–6 calibration](#rarity-calibration).

##### Presentation

For own-character random-rarity offers, swap the original Rare, then extra Rares in draw order, into the simulated Rare positions in ascending slot order.

For Brain Leech's non-Rare Share Knowledge offers and Room Full of Cheese's Gorge offers, uniformly shuffle [display order](#entry-addresses) after reward modifiers using event RNG.

##### Scroll Boxes

Resolve Defect's two independent 1% [triple-Claw substitution checks](#card-reward-offers-1) with event RNG before drawing Common sequence entries. Non-Defect offers follow the no-substitution branch.

- **No substitution:** consume one Common triple. It supplies both Common cards in bundle one and the first Common card in bundle two; generate the remaining Common and both Uncommons with event RNG.
- **Triple-Claw exception:** if either bundle is replaced, generate any remaining ordinary bundle entirely with event RNG.

#### Rerolls

Generate reachable rerolls using the source's [offer group](#offer-groups) and, where applicable, [offer procedure](#offer-procedure), drawing unused entries from that group's sequences, including [expanded pools](#prismatic-gem-and-dingy-rug).

Rerolls of Kaleidoscope's Neow offers are unreachable.

#### Prismatic Gem and Dingy Rug

**Intent:** align rewards from the same expanded pool.

Preserve the [pool-modification boundaries](#card-reward-offers-1). Select the identity group for the complete request:

- **Unchanged eligible pool:** reuse its existing group, including Rug's fixed-Common or already-colorless offers.
- **Expanded eligible pool:** use independent sequences for the effective expanded pool, retaining source draw grouping. All results, including own-character cards, consume this group.

Changing pools preserves luck state and opening-triple progression.

Lasting Candy draws from the inherited pool's Power sequences while retaining its source rarity-roll sequence position.

**Accepted tradeoff:** separate identity groups sacrifice alignment with the unexpanded pool to avoid rebuilding sequences when the pool expands.

### Permanent transformations and direct card additions

**Intent:** align same-pool transformations and direct additions while preserving source rarity odds.

**Covered direct additions:** Infested Automaton's Study and Touch Core, Endless Conveyor's Fried Eel, Neow's Arcane Scroll and Neow's Bones curse, and Distinguished Cape.

For random results, transformations and additions share one uniform identity sequence per pool: the five characters, colorless and curse. Keep these separate from card reward offers and combat transformations.

- **Transformations:** draw directly, filtering out the original identity; follow [lifetime](#out-of-combat-card-lifetime) and [batch](#transformation-batches) rules.
- **Direct additions:** preserve fixed rarities; random rarities use per-pool rarity sequences at vanilla base odds, except Infested Automaton's Study and Touch Core use event RNG. Apply unavailable-rarity fallback within the source's eligible pool, then filter the identity sequence by rarity and source restrictions.

**Vanilla deviation — SL 5(↓):** processing batches in [identity order](#transformation-batches) removes order-dependent alternatives.

Baseline: [generation mechanics](#permanent-transformations-and-direct-additions).

### Combat gold

**Intent:** align normal-fight and elite gold rewards independently.

**Generation and addressing:** derive each roll from `purpose = gold reward` + combat category (normal fight or elite) + that category's gold-reward ordinal. Event battles use their encounter's room type. Roll uniformly over vanilla's adjusted inclusive gold range.

Advance ordinals only for generated random combat-gold rewards. Gremlin Merc's half-gold reward uses the reduced range and advances the normal-fight ordinal.

**Rationale and tradeoff:** reordering unreduced rewards preserves each category's payouts and totals under unchanged payout modifiers, including Bowler Hat's per-payout truncation. Replacing one unreduced normal reward with an elite reward adds 11–26 base gold at A3+. [Rejected alternatives](#combat-gold-alternatives).

Baseline: [gold ranges and exceptions](#combat-gold-1).

### Event-specific rewards

**Scope:** Trash Heap, Doll Room, Fake Merchant stock, Neow's Bones' Ancient relic pair, and other rewards exclusive to events.

Use [event RNG](#event-addresses) for reward identities, random rarities and Fake Merchant's price rolls.

Neow's Bones' curse follows [direct additions](#permanent-transformations-and-direct-card-additions).

## Source policies — Encounter — Non-combat

### Unknown-room resolution

**Intent:** align `?` room type resolution across runs, even when their chances differ.

**Addressing:** derive a uniform `[0, 1)` roll from `purpose = room type` + act-local room-type ordinal. Advance once per `?` visit, except Event rooms forced by Golden Compass or Lantern Key.

**Interval widths:** after exclusions, allocate widths from stored chances in normal fight → chest → shop order, capping each at the remaining share of 100%.

**Interval placement:** chest starts at 0 and shop ends at 1. Center normal fight at `7/15`, shifting only as needed to avoid overlap. Remaining space resolves to Event.

**Rationale:** `7/15` is the normal-fight midpoint when chest/normal-fight/shop widths fill the range in their base `2:10:3` ratio.

Baseline: [room restrictions and chance updates](#unknown-room-resolution-1).

### Ordinary-event selection

**Intent:** keep event sequences matching across same-seed runs for as long as possible, even as eligibility and acts change.

**Scope:** ordinary events, excluding [Ancients](#ancient-offerings) and [Lantern Key's forced event](#lantern-key).

**Conditions:** group event IDs by equivalent eligibility predicates, excluding act availability and [late eligibility checks](#event-eligibility), with one key per condition. Act availability determines participating IDs; evaluate late checks when selecting an ID.

#### Act entry and carryover

**Alignment:** accept rare earlier mismatches for better agreement in the relative order of events both runs encounter.

At run start, independently shuffle each condition's IDs using `purpose = ID order` + condition key.

At act entry, rebuild each condition's sequence using only unvisited IDs available in that act. Preserve survivors' order and insert newly available IDs in their run-start order.

**Sorted random priorities:** for `n` surviving and `m` incoming IDs, take the first `n` and `m` rolls from independent uniform sequences, addressed by `purpose = insertion` + act + condition key + surviving/incoming + roll index. Sort each set, assign it along its ID sequence in order, then merge by priority. The interleaving is uniform. [Rejected alternatives](#event-selection-alternatives).

**Remaining counts:** reset each condition's count to its number of act-available, unvisited IDs, even when the condition currently fails.

#### Within-act selection

**Priority persistence:**

| Act | Condition priorities and roll indices | Alignment aim |
|---|---|---|
| 1 | Initialize at act entry; retain between selections. | Match relative event order. |
| 2–3 | Refresh priorities from remaining counts and reset roll indices at each selection. | Delay the act's first mismatch when earlier visits leave different pools. |

Remaining counts and ID positions carry between selections in every act.

1. Reset per-condition roll indices to zero. For each remaining count `n_c > 0`, initialize priority `-ln(U) / n_c`. Independent uniform `U ∈ (0, 1)` rolls use `purpose = condition selection` + condition key + roll index. In Acts 2–3, also include the act-local ordinary-event ordinal.
2. Decrement the lowest-priority condition's count. If its predicate passes and an unvisited ID remains, try the next unvisited ID and advance the ID position, including on late eligibility failure.
3. After failure, retire that condition at zero; otherwise add a fresh `-ln(U) / n_c` to its priority. Keep other priorities fixed and repeat step 2.
4. On success, commit the visit and state changes. In Act 1, update the winning priority as in step 3.

**Wrapping:** when all counts reach zero, refill them from act-available, unvisited IDs and restart priorities, continuing roll indices. ID sequences wrap in their established order, excluding visited IDs.

**Vanilla deviation — NoSL 3:** refreshed condition priorities after wrapping can change expected event exposure. **Frequency:** rare (qualitative judgment).

**Fallback:** if the act has no eligible unvisited event, return `list[(n - 1) % list.Count]` from vanilla's original shuffled act list, where `n` is the 1-based act-local ordinary-event ordinal. Bypass eligibility and visited exclusions for this draw.

**Vanilla deviation — NoSL 3:** ordinal indexing changes fallback probabilities conditioned on prior visits and eligibility. **Frequency:** effectively impossible (qualitative judgment).

**Behavior preservation:** before count refill or ID wrapping, preserve vanilla's distribution when eligibility cannot inspect hidden order. With passing late eligibility checks, same starting random state + same selected event leaves the same random state. Cross-run and SL correlations change.

Baseline: [event selection and wrapping](#event-and-encounter-identities); [condition predicates and late eligibility checks](#event-eligibility).

### Lantern Key

**Intent:** keep Lantern Key from consuming an ordinary-event opportunity.

Enter War Historian Repy directly, bypassing [ordinary-event selection](#ordinary-event-selection). Record the visit and leave the act-local ordinary-event ordinal unchanged.

**Vanilla deviation — NoSL 3:** bypassing [vanilla's ordinary-list consumption](#event-and-encounter-identities) changes later ordinary-event availability.

### Ancient offerings

**Intent:** align Ancient offerings when eligibility changes.

Use [fixed priorities](#fixed-selection-priorities) with vanilla weights, keyed by Ancient ID + option-pool ID + option ID, for Orobas's third pool, Pael's second/third, Tezcatara's first, and Tanx's and Nonupeipe's pools. Display each pool's selections in priority order.

Retain vanilla generation and RNG positions for other menu components.

Baseline: [Ancient offerings](#ancient-offerings-1).

### The Future of Potions: card-type assignment

**Intent:** match rarity/type options for equal offered counts by reward rarity, and each potion ID's assigned types for identical offered potion multisets.

Assign types to the first three owned potions, retaining slot order for display.

- Use one independent type sequence per reward rarity, addressed by `purpose = type assignment` + event ID + rarity. Each entry is an independent uniform draw from that rarity's allowed card types.
- Rank offered potions with [fixed priorities](#fixed-selection-priorities), keyed by `purpose = ordering` + event ID + potion ID + copy number. Number each potion ID's offered copies from 1 in slot order.
- Within each rarity, assign successive type entries to offered potions in ascending priority order. Keep these assignments for the event.

Card identities follow [type-specific offers](#type-specific-offers).

**Limit:** a potion offered in both runs may receive different card types when the other offered potions with the same rarity differ.

Baseline: [reward-rarity mapping and allowed card types](#card-reward-offers-1).

### Ranwid relic sacrifice

**Intent:** align the offered relic across changing inventories.

Select one tradable owned relic using [fixed priorities](#fixed-selection-priorities), keyed by `purpose = relic sacrifice` + event ID + relic ID.

Baseline: [relic-event mechanics](#shops-chests-and-relic-events).

### Relic Trader

**Intent:** align replacement relics; prioritize outgoing-relic overlap over preserving each relic's replacement pairing.

#### Replacement relics

Generate reward slots 1–3 in order. For each, roll [vanilla relic rarity](#relic-bags) using `purpose = replacement rarity` + event ID + reward slot, then use a [tail draw](#relic-tail-draws) from the [Courier allocation](#allocation) of that rarity, with ordinary relic eligibility. Use [borrowing and fallback](#borrowing) on exhaustion.

Consume all three replacements when offered, including borrowed generic entries.

#### Outgoing relic assignment

1. Give each tradable owned relic / reward-slot pair an independent uniform **pair priority**, using [fixed-priority derivation](#fixed-selection-priorities) keyed by `purpose = outgoing assignment` + event ID + relic ID + reward slot.
2. Select the three relics with the lowest minima across their three pair priorities.
3. Among these relics, repeatedly assign the lowest-priority remaining pair, removing its relic and slot from further assignment.

**Rationale:** Replacing one eligible owned relic retains at least two outgoing relics, with maximal expected outgoing overlap among rules treating identities symmetrically. See the [matching analysis and alternatives](#relic-trader-alternatives).

Baseline: [shops, chests and relic events](#shops-chests-and-relic-events).

### Endless Conveyor

**Intent:** align dish order across changing eligibility.

Use one sequence addressed by `purpose = dish selection`, drawing independently over all [random dishes at vanilla weights](#endless-conveyor-dishes).

For each random offer, advance past ineligible entries, offer the first eligible dish, and resume after it next time.

Fixed Seapunk Salad displays consume zero sequence entries.

**Vanilla deviation — SL 4A(↓):** changing only potion-slot availability can switch the next random offer only to or from Suspicious Condiment.

Baseline: [dish selection](#endless-conveyor-dishes).

### Wing Charm

**Intent:** align Swift targets on shared reward entries.

**Scope:** batches processed by Wing Charm's reward-options hook.

Apply [A0–6 replacement state](#a06-multiple-rares) and evaluate Swift eligibility after earlier modifiers. An empty eligible set has no effect; select a singleton directly.

**Default selection:** use [fixed-priority derivation](#fixed-selection-priorities), keyed by `purpose = Wing Charm` + [card occurrence address](#card-occurrence-addresses). Select the lowest-priority eligible card in the batch.

**Non-Rare larger offers:** for Brain Leech's Share Knowledge and Room Full of Cheese's Gorge, select from the triple; if it has no eligible cards, select uniformly among eligible supplementary cards using event RNG.

**Rationale:** interchangeable positions preserve marginal targeting probabilities; [presentation shuffling](#presentation) removes visible position bias.

**Weighted group selection:** for rare-bearing Share Knowledge and Lasting Candy, select the extras when the independent uniform `[0,1)` roll below is less than `eligible extras / all eligible cards`; otherwise select from the triple.

| Source / extra group | Group-selection roll | Selection within the extra group |
|---|---|---|
| Share Knowledge: two supplementary positions | Event RNG. | Select uniformly among eligible supplementary positions, using `purpose = Wing Charm supplementary selection` + event ID when both qualify. |
| Lasting Candy: added Power | `purpose = Wing Charm` + Power entry address, evaluated only when the Power is eligible. | Select the Power. |

**Vanilla deviation — NoSL 3:** when Brain Leech consumes an opening non-Rare triple, its different rarity probabilities slightly change the enchanted card's rarity distribution.

Baseline: [Wing Charm and earlier reward modifiers](#card-reward-offers-1).

### Persistent selection

**Intent:** align card selections across sources, changing decks and reward choices, and potion sacrifice offers across changing inventories.

| Selection | Mechanism and sharing | Local rules |
|---|---|---|
| Random permanent upgrades / downgrades of deck cards | [Persistent clocks](#persistent-clocks); separate upgrade and downgrade scopes shared across sources and filters. | Set consumed clocks to zero; apply [refill rules](#permanent-upgrades-and-downgrades). |
| Pael's Tooth return order / Beautiful Bracelet targets | [Fixed priorities](#fixed-selection-priorities); separate purposes with [reward aliases](#card-reward-selection-key-aliases). | Use [routing](#out-of-combat-card-routing) for Tooth's initial selection. |
| Slippery Bridge: [vanilla candidates and fallback](#slippery-bridge-selection) | Persistent clocks; Bridge-only scope with [reward aliases](#card-reward-selection-key-aliases). | Commit each offer when available; renew a declined card's clock once before the next selection, using its per-occurrence renewal count. |
| Ranwid the Elder / Stone of All Time: owned potions | [Weighted sacrifice clocks](#weighted-potion-sacrifice-clocks); shared run-wide. | With one owned potion, preserve its clock. Otherwise renew the selected clock once when the offer becomes available. |

#### Weighted potion sacrifice clocks

Keep a [persistent clock](#persistent-clocks) and renewal count per potion type, keyed by `purpose = sacrifice` + type. Inventory changes preserve both.

Select the owned type with the smallest `clock / owned count`; call this minimum `m`. For each other owned type, subtract its `owned count × m` from its clock.

Choose uniformly among copies of the selected type in slot order, using `purpose = copy selection` + type + its pre-renewal count.

Baseline: [potion sacrifice selection](#potions-1).

#### Permanent upgrades and downgrades

**Upgrade sources:** Whetstone, War Paint, Fragrant Mushroom, Sand Castle, War Hammer, Fishing Rod, Doors of Light and Dark, Battleworn Dummy, Endless Conveyor, Tablet of Truth, Reflections, and Mad Science's Improvement.
**Downgrade sources:** Wongos and Reflections.

- **Upgrade-clock refill:** after a random downgrade, refill a zero upgrade clock with the next entry from the run-wide `purpose = upgrade refill` sequence; retain nonzero clocks.
- **Downgrade-clock refill:** renew the downgraded card's clock using event RNG.

Reflections processes downgrades and refills in ascending pre-selection downgrade-clock order, then determines upgrade candidates.

**Vanilla deviation — SL 4A(↓):** removes rerolls of upgrade targets through unrelated RNG consumption. In vanilla, buying Royal Stamp before Whetstone can change Whetstone's targets via shared `Niche` RNG.

Baseline: [card selection](#card-state-and-selection) and [Wongos](#shops-chests-and-relic-events).

## Source policies — Encounter — Combat

### Combat setup

**Intent:** align combat setups within each act.

Use `purpose = setup` + [combat address](#combat-addresses) for encounter RNG, covering variable enemy composition and assigned starting states.

Baseline: [combat setup](#combat-setup-and-shuffles).

### Enemy HP

**Intent:** isolate HP sequences by combat, minimize per-roll expected absolute HP differences across eligibility changes, and retain within-combat roll-order manipulation.

For each HP roll, derive uniform `u ∈ [0,1)` from `purpose = enemy HP` + [combat address](#combat-addresses) + combat-local HP-roll ordinal. Select zero-based index `floor(u × eligible count)` from ascending eligible HP values.

Initial enemies, summons, replacements and Tough Egg's hatch share this ordinal. Advance once per vanilla sampling call in execution order, including filtered singletons. Fixed HP ranges consume zero.

Preserve vanilla eligibility and full-range fallback. Punch-Off's starting HP reductions use [combat setup](#combat-setup).

Baseline: [enemy HP mechanics](#enemy-hp-1).

### Enemy AI

**Intent:** align intents across move histories.

Use [combat-local sequences](#combat-addresses), partitioned by enemy identity except for Decimillipede. Keep move history and eligibility outside addresses.

**Effective-decision sequences:** use vanilla percentile sampling; advance only when at least two moves have positive effective weight.

#### Intent sequences

| Source | Local rules |
|---|---|
| Inklets | Two effective-decision sequences: one for the middle, one shared by the outers. Retain scopes after deaths. |
| Exoskeletons / Scrolls of Biting | One effective-decision sequence per type. |
| Small Leaf Slimes | One opening sequence, consumed once per enemy in creation order. |
| Decimillipede | One revival-selection sequence shared by all segments. |
| Other enemies with random intents, excluding [rats](#rat-sampling) | Use [weighted opportunity-specific priorities](#opportunity-specific-selection-priorities), addressed by the enemy's random-branch ordinal, with move IDs as selection keys. Count singleton/all-zero branches. |

#### Rat sampling

**Intent:** realign later non-Summon choices each round when eligibility agrees; align summoned openings independently of birth timing.

**Summon:** all rats share one percentile sequence. Advance when Summon has positive effective weight; choose it with probability `effective Summon weight / total effective weight`, preserving vanilla sequential eligibility checks.

**Non-Summon:** otherwise, use [weighted opportunity-specific priorities](#opportunity-specific-selection-priorities) keyed by move ID. Summon, summoned openings and later choices use separate purposes.

| Non-Summon opportunity | Address and consumption |
|---|---|
| Summoned opening | Combat-local summoned-rat ordinal in creation order. Consume at its first random intent selection. |
| After opening, any rat | Combat round + round-local selection ordinal, shared by all rats in vanilla selection order. Advance only when at least two non-Summon moves have positive effective weight. |

Use `CombatState.RoundNumber` at selection: starts at 1 and advances after the enemy turn.

**Limit:** different selection counts can shift choices within a round. [Rejected alternatives](#rat-sampling-alternatives).

#### Fabricator bot identities

Use `purpose = bot generation` + [Fabricator occurrence](#enemy-occurrence-addresses) + its bot-selection ordinal, shared across offensive and defensive pools. Sample uniformly from the requested pool after immediate-repeat exclusion, in enemy ID order. Advance once per selection, including singletons.

#### Vanilla deviations

**Vanilla deviation — SL 4A(↓):** fewer reroll opportunities. In the [small-slime example](#enemy-ai-1), killing either small slime gives Twig M the same next intent.

**Vanilla deviation — SL 4A:** changed survivor alternatives. In the [reference examples](#enemy-ai-1), Flyconid/Twig M lose vanilla's guaranteed non-attacking option; trying both rat survivors finds Screech with probability `2/3`, up from `1/2`.

Baseline: [enemy AI mechanics](#enemy-ai-1).

### Card shuffling

**Intent:** align cards by source in Acts 1–2 and usefulness rank in Act 3.

**Scope:** opening and later shuffles, using cards' current [logical associations](#routing). Results are independent of input pile order.

Assign independent uniform priorities on a common scale, sort cards by priority, then apply opening-hand and shuffle modifiers.

Address each shuffle by `purpose = shuffle` + [combat address](#combat-addresses) + combat-local shuffle ordinal. All triggers share this ordinal. Advance once per shuffle, including empty and single-card shuffles.

#### Priority assignment

Derive each card's priority from the shuffle address + its **shuffle key**: a source-based key or priority-sequence ID + position reassigned at each shuffle.

The opening Innate subgroup uses [processing order](#card-processing-order), least useful first, with its own priority sequence. For ordinary cards, the first applicable row wins:

| Cards | Acts 1–2 | Act 3 |
|---|---|---|
| Defend and earlier processing order categories | First sequence, least useful first. | Same. |
| Other starting Basics; eligible rewards/direct additions | Source-based keys below. | Second sequence, most useful first. |
| Remaining cards | Second sequence: permanent cards in acquisition order, followed by new combat occurrences in creation order. | Both rows combined, most useful first. |

Number acquisition batches across the run and creation batches within each combat; order cards within each batch by [card processing order](#card-processing-order).

Freeze the card's ordering properties at permanent removal and retain its entry through run end. Include it in opening priority assignment, then omit it from the shuffle. Additions and rank changes can still shift sequence positions.

#### Source-based assignment — Acts 1–2

Other starting Basics receive distinct [initial-source keys](#card-occurrence-addresses).

- **Reward offers:** one shuffle key per shared triple from the [own-character offer groups](#offer-groups).
- **Triple key assignment:** give the key to the acquired card from the triple with the lowest [fixed priority](#fixed-selection-priorities), addressed by `purpose = assignment` + event ID + card ID. Freeze the assignment at acquisition; other acquired cards from that triple enter the remaining group.
- **Transformations and Direct additions:** one shuffle key per [shared generation entry](#permanent-transformations-and-direct-card-additions); allow only unfiltered, rarity-only or transformation-filter-only generation.
- **Copies and event-local supplementary cards:** enter the remaining group.
- **A0–6 extra-Rare replacements:** follow the [replacement-state rule](#a06-multiple-rares).

#### Vanilla deviations

**Vanilla deviation — SL 5(↓):** fewer shuffle rerolls through draw/discard choices. Reordering the same discarded cards gives the same shuffle, including which Frantic Escape cost is drawn first ([baseline](#combat-setup-and-shuffles)). This can weaken all cards with a draw effect.

### Random pile insertion

**Intent:** align relative insertion depth across changing pile sizes.

Address sequences by `purpose = insertion` + [combat address](#combat-addresses) + sharing group + target pile.

| Sharing group | Coverage |
|---|---|
| Soul | Capture Spirit, Dirge, Grave Warden, Reave, Severance and Funerary Mask. |
| Metamorphosis | Generated attacks. |
| Dazed | Blessed Antler, Tea of Discourtesy, Noisebot and Entomancer's Personal Hive. |
| Biiig Hug | Soot. |
| Soul Fysh | Beckon. |
| The Insatiable | Frantic Escape. |

For each insertion in generation order, take the next uniform `u ∈ [0,1)` and choose zero-based gap `floor(u × (n + 1))`, where `n` is the target pile's current size.

Advance once per inserted card, including into an empty pile.

**Vanilla deviation — SL 4A(↓):** fewer within-combat rerolls through other insertion groups, piles or shuffles.

Baseline: [pile insertion and discard order](#combat-setup-and-shuffles).

### Random combat card selection

**Intent:** align selections across sources and changing card sets.

**Scope:** existing combat cards, including sampled choice offers. Mad Science's Improvement follows [permanent upgrades](#permanent-upgrades-and-downgrades); Thieving Hopper follows its [theft policy](#thieving-hopper).

Use [combat-card selection state](#combat-card-selection-state). Renew consumed clocks immediately on commitment; process selected cards in ascending pre-selection clock order.

| Shared clock scope | Sources |
|---|---|
| Exhaust | Cinder, unupgraded True Grit |
| Upgrade | Drain Power, Stone Cracker |
| Return | Anointed, Jeweled Mask, Power Cell |
| Attack auto-play | Beat Down, Uproar, Stampede |

Thrash, Hidden Gem, Bookmark, Mummified Hand, Aggression, Catastrophe and Seeker Strike each use a separate scope.

**Consumption exceptions:** preserve clocks and renewal counts for empty or forced-singleton selections, and whole-set selections by Anointed, Drain Power, Stone Cracker, Seeker Strike, Aggression and Power Cell. Whole-set hand returns qualify only when every selected card fits.

**Whole-set ordering:** for the whole-set cases above except Seeker Strike, order the batch by logical occurrence address, then uniformly shuffle it if it contains at least two cards. Use one shared RNG addressed by `purpose = incidental shuffle` + [combat address](#combat-addresses).

**Seeker Strike:** clocks select the offer; [routing](#combat-card-routing) resolves the player's choice within it. Keep vanilla offer display order.

**Vanilla deviation — SL 4A(↓):** fewer within-combat rerolls through unrelated sources, forced selections or discard order.

**Vanilla deviation — SL 4A:** persistent clocks change the alternatives available across eligible sets.

Baseline: [card selection](#card-state-and-selection) and [shuffle mechanics](#combat-setup-and-shuffles).

### Thieving Hopper

**Intent:** align theft targets across decks.

Apply eligibility to combat copies. Select using [fixed priorities](#fixed-selection-priorities), keyed by `purpose = Thieving Hopper` + the linked permanent-deck card's selection key, including [reward aliases](#card-reward-selection-key-aliases).

**Vanilla deviation — SL 5(↓):** fewer within-combat theft rerolls through card generation or pile order. This can remove a reason to carry a card-generating potion into Act 2 or on Act 2 Ancient to change theft target.

Baseline: [theft eligibility and recovery](#card-state-and-selection).

### Random enemy targets

**Intent:** align random enemy targets across changing eligible sets.

Address sequences by `purpose = targeting` + [combat address](#combat-addresses) + sharing group:

| Sharing group | Sources |
|---|---|
| Forgotten Soul | All Forgotten Soul selections. |
| Lightning | All Lightning copies, passive activations and evokes. |
| Direct damage | Remaining effects that inherently cause damage, poison, or immediate HP loss, even at zero damage; includes power and relic damage, Knife Trap and auto-played damaging cards (including through Beat Down). |
| Other | Remaining effects, including Countdown and automatically played debuffs. |

Use [opportunity-specific selection priorities](#opportunity-specific-selection-priorities) with equal weights: opportunity = sequence address + group-local selection ordinal; selection key = [enemy occurrence address](#enemy-occurrence-addresses). Advance the ordinal only when at least two targets are eligible.

**Vanilla deviation — SL 4A(↓):** fewer within-combat rerolls through other groups or forced targets.

**Vanilla deviation — SL 4A:** priorities change the alternatives available across eligible sets.

Baseline: [random enemy targeting](#random-enemy-targeting).

### Combat card generation and transformations

**Intent:** align random card identities.

**Scope:** random card generation by cards, powers, relics, and Entropy. Potions follow [potion-generated cards](#potion-generated-cards).

Address sequences by `purpose = card generation` + [combat address](#combat-addresses) + group + pool:

| Group | Pools and sources |
|---|---|
| Ordinary costs | One sequence per character pool, plus colorless, curse and status; shared by ordinary-cost generation (including Jackpot) and Entropy. |
| Free this turn | Own-character pool; shared by generators making cards free this turn. |
| Free this combat | Own-character Attacks; Metamorphosis. |
| Splash | Combined other-character Attacks, uniform across identities. |

Classify cost treatment by the generating source's free effect; natural costs, upgrades and later modifiers leave classification unchanged. Entropy uses the original card's [transformation pool](#permanent-transformations-and-direct-additions).

Use [identity sequences](#rarity-and-identity-sequences) without rarity rolls. Consume one entry per generated identity, including every offered alternative.

For Entropy, follow [transformation batch rules](#transformation-batches); apply replacements in vanilla execution order.

**Vanilla deviation — SL 4A(↓):** fewer within-combat rerolls through other pools, cost groups or unrelated sources.

**Vanilla deviation — SL 4A:** ordinary-cost generation and Entropy now advance each other's identity supply within a pool.

**Vanilla deviation — SL 4A(↓):** processing Entropy batches in [identity order](#transformation-batches) removes selection-order rerolls.

Baseline: [combat generation and transformation mechanics](#combat-card-generation-and-transformations-1).

### Random energy costs

**Intent:** align cost rolls within card categories.

| Partition | Sequence address | Assignment |
|---|---|---|
| Confused — Snecko Eye and Fake Snecko Eye | `purpose = Confused cost` + [combat address](#combat-addresses) + card category | Draw order. |
| Slither | `purpose = Slither cost` + combat address | Draw order across all Slither copies. |
| Snecko Oil | `purpose = Oil cost` + [potion effect ordinal](#potion-effect-opportunities) + card category | Hand-entry order below. |

**Card categories:** starter Strikes; starter Defends; Other Basic 1; Other Basic 2; Others. Other Basic slots follow each character's original starting-deck identity order and remain fixed through removals. Classify by current identity; other Strike-tagged attacks belong to Others.

**Consumption:** roll uniformly over 0–3, consuming one entry per eligible roll. Confused's X cards and Slither draws that have left hand consume zero.

**Confused + Slither:** when both apply to the same draw, Slither reapplies that draw's Confused value at its usual timing, only while the card remains in hand. Consume no Slither entry. Reapplication preserves Slither's overwrite of Kingly Kick's draw discount.

**Oil assignment:** after Oil finishes drawing, order eligible cards within each category by latest successful entry into hand, earliest first:

- Draws, retrievals and generation receive entry positions. Retained cards keep theirs; leaving then returning refreshes them.
- Unordered entry batches share one entry ordinal, with internal ascending [processing order](#card-processing-order) frozen at entry.
- Sequential draws and explicitly ordered returns, including All for One, preserve source order.

Apply [combat-card routing](#combat-card-routing) to Oil's hand-entry assignment.

The [steering deviation](#loss-of-future-outcome-steering) covers lost cross-combat steering of later Oil batches through earlier Confused/Slither rolls or Oil hand sizes.

**Vanilla deviation — SL 4A(↓):** source and category partitions, X skipping and overlap reuse remove consumption-based rerolls.

**Vanilla deviation — SL 4A(↓):** processing order within unordered entry batches removes click/enumeration-order Oil rerolls.

Baseline: [random energy costs](#random-energy-costs-1).

### Random orb types

**Intent:** isolate random-orb generation by combat.

Chaos and Trash to Treasure share one sequence addressed by `purpose = orb type` + [combat address](#combat-addresses).

Consume one entry per successfully channeled random orb.

**Vanilla deviation — SL 4A(↓):** failed channeling no longer rerolls later orb types. **Frequency:** rare — Defect must lose all orb slots, attempt random channeling, then regain slots and channel random orbs in the same combat.

Baseline: [random orb generation](#random-orb-generation).

### Potion-generated cards

**Intent:** align random card generation.

**Scope:** Attack, Skill, Power and Colorless Potions, Cosmic Concoction and Orobic Acid.

Address each batch by `purpose = potion card generation` + potion type + [effect ordinal](#potion-effect-opportunities). Orobic Acid's Attack, Skill and Power share one batch.

Use one [identity sequence](#rarity-and-identity-sequences) per batch.

**Vanilla deviation — SL 5(↓):** thieving hopper stealing and other potion types no longer change a potion's generated cards.

Baseline: [potion card-generation mechanics](#potion-card-generation).

## Reference

[Event workbook](references/StS2_Events.xlsx): vanilla offers and eligibility for **56 ordinary events, forced War Historian Repy and 8 Ancients**.

### Rarity calibration

Calibration keeps [displayed Uncommon counts](#uncommon-probabilities) and [shop Rare counts](#stock-and-prices) close to vanilla averages, while spreading shop Rares across more visits.

Uncommon rates below are non-Rare offers / rare-bearing companions:

| Study | Design rates | Design − vanilla Uncommons per 100 slots |
|---|---|---|
| [A7+](outputs/reward-calibration-act3-results.json) | 38% / 39% | −0.01 |
| [A0–6](outputs/reward-calibration-a6-results.json) | 40% / 40.5% | +0.1 |

Initial shops with at least one Rare character card rise from **20% to 21%** at A7+ and **42% to 44%** at A0–6.

The [model](outputs/reward-calibration.cjs) uses 200,000 calibration and 200,000 validation runs per difficulty, with the same [projected three-act routes and economy](outputs/reward-calibration-environment.cjs) for vanilla and mod. Fits include opening triples and A0–6 Common-first replacement.

Main runs use A3+ gold; A0–2 gold is a sensitivity case. Eligibility and event outcomes are approximated. Route assumptions and [sensitivity studies](outputs/reward-calibration-results.json) accompany the results.

### Chest marker probabilities

With a uniform unseen order, [finite-lifetime markers](#finite-lifetime-chest-markers) match vanilla's character-specific relic chances after one chest acquisition. Skipped chests followed by shops can change later chances.

The comparison concerns generic rewards at one rarity, with fixed eligibility and before borrowing or supply exhaustion. Uniform lifetimes affect character-specific positions like deleting a random chest-eligible position; earlier draws can break uniformity.

Examples contain one character-specific relic; all others are shared, and all are shop-eligible. The mod assigns one random relic to shop stock and uniformly orders the rest as generic stock; vanilla uses [independent bags](#relic-bags). The table gives the character-specific chance in the next generic reward.

| Pool size | Sequence | Vanilla | Mod |
|---|---|---|---|
| 4 | Shop display → take chest relic | 37.5% | 37.5% |
| 6 | Skip chest → shop display → take chest relic | 20.8% | 21.1% |

The second example adds **0.3 percentage points** because shop consumption changes new lifetimes while preserving existing ones. Counting already displayed shop relics in `S` lowers the first example's mod chance to **33.3%**.

### Rejected alternatives

#### Combat-gold alternatives

- **Shared rolls with independent compensation:** nonuniform A3+ normal gold at unchanged expectation (**Vanilla deviation — NoSL 3**); normal/elite reordering can change Bowler Hat totals by 1.
- **Shared range-relative rolls:** vanilla distributions, but reordering can change A3+ base gold by 1.

#### Event-selection alternatives

For [condition priorities](#within-act-selection):

- **Retained in every act:** differing survivor pools can cause earlier mismatches, despite better relative event order with matching pools.
- **Refreshed in every act:** more Act 1 order reversals, despite better same-ordinal matching.

Both [cross-act insertion](#act-entry-and-carryover) alternatives preserved less relative event order in simulations, despite sometimes delaying the first mismatch:

- **Position priorities:** reserve incoming positions using shared output-position priorities.
- **Weighted merging:** use shared rolls to choose surviving or incoming IDs proportionally to remaining counts.

#### Relic Trader alternatives

For one eligible relic removed/substituted and three distinct replacements matching by slot across runs, [analysis](outputs/relic-trader-matching-results.json) compares [the selected assignment](#outgoing-relic-assignment) with:

- **Independent candidate priorities:** fewer matching outgoing/replacement pairs on average, equal expected outgoing overlap.
- **Direct pairing over all eligible relics:** one inventory substitution can change two outgoing relics, despite more matching pairs on average.

#### Rat-sampling alternatives

[Non-Summon choices](#rat-sampling):

- **Percentile sampling:** eligibility changes shift retained moves' roll intervals.
- **Sequences per eligible move pair:** lose cross-pair alignment; selection-count differences persist.
- **[Filtered draws](#filtered-draws):** retain filter-history differences after eligibility converges.

## Appendix: existing mechanics

Vanilla mechanics; [Catfood differences](#catfood-addressing-and-combat-rewards) are labeled.

### Rooms, events and combat

#### Unknown-room resolution

**Selection:** sample cumulative chances in normal fight → chest → shop order; Event takes the remainder. Base chances are 10% / 2% / 3%; elites are disabled. Excluded types contribute no interval.

**Overflow:** only the first 100% in selection order is reachable; stored chances remain uncapped. Thus 70% / 14% / 21% gives 70% / 14% / 16% and 0% Event.

**Chance updates:** reset the selected non-event type to its base chance; add each other allowed non-event type's base amount. Excluded types' stored chances freeze.

**Random state:** each `?` resolution advances `UnknownMapPoint` once, including forced Events, which freeze non-event chances. Chances reset between acts; RNG state continues.

| Source | Mechanics |
|---|---|
| Shop exclusion | Excluded when the previous visited map point contained a shop, or all next nodes are shops (at least one). |
| Juzu Bracelet | Excludes normal fights. |
| Golden Compass | Forces Event during its affected act. |
| Lantern Key | In Act 3, forces Event and replaces the selected event with War Historian Repy. The ordinary event-list cursor still advances. |

#### Event and encounter identities

**Upfront generation:** `UpFront` builds each act's event and encounter lists and selects its Ancient. Event lists uniformly shuffle act-specific plus shared identities. Later acts receive disjoint subsets of shared Ancients before selection.

**Events:** scan the shuffled list for the next eligible identity absent from the run-wide visited set, advancing past failed entries. Forced-event replacement follows this scan.

**Wrapping:** retain list order and visits. Skipped entries can qualify on later passes; a full unsuccessful scan returns the cursor's event, even if repeated or ineligible.

**Normal encounters:** take the next normal encounter whether the map marker was `?` or a normal fight.

**Capacity:** act event lists contain 25–31 entries before filtering, versus 13–15 ordinary floors per act.

**Event RNG:** in single-player, seeded by run seed + deterministic event ID hash (`EventModel.BeginEvent`).

**Skipping and probability:** with a uniform hidden shuffle, fixed other eligibility and no wrapping, skipping hidden ineligible entries preserves their later odds. With two K events and three others, encountering zero, one or two non-K events while K is ineligible leaves the next K chance at `2/5` once K becomes eligible. Encounters while K is eligible have the usual without-replacement effect.

#### Ancient offerings

Ancient menus use [event RNG](#event-and-encounter-identities). Matching seed, Ancient and ordered eligible pools reproduce the menu; changed eligibility can also change which still-eligible options appear.

| Ancient | Menu generation and relevant conditions |
|---|---|
| Neow | Two positive options and one curse option, with curse-dependent exclusions and random paired alternatives in the positive pool. |
| Darv | Shuffles one choice from each eligible relic group; offers three, or two plus Dusty Tome with 50% probability. Ectoplasm and Sozu are Act 2 only. |
| Orobas | One choice from each of three pools. Sea Glass's featured character uses event RNG. The third pool checks for the starter relic (Touch of Orobas) and supported starter card (Archaic Tooth), with a locked option if both are absent. |
| Pael | One choice from each of three pools. Claw needs ≥3 Goopy-eligible cards; Tooth needs ≥5 removable cards; Legion requires no event pet. Second-pool weights: Growth 1; each other eligible option 2. |
| Tezcatara | One choice from each of three pools; Nutritious Soup joins the first while a Basic Strike remains. |
| Tanx | Three choices from a shuffled pool; Tri-Boomerang joins with ≥3 Instinct-eligible cards. |
| Vakuu | One choice from each of three fixed pools. |
| Nonupeipe | Three choices from a shuffled pool; Beautiful Bracelet joins with ≥4 Swift-eligible cards. |

**Shared RNG use:** acquiring Neow's Bones shuffles its relic candidates with `Rewards`. Offering Dusty Tome advances `Rewards` once, even if declined; its single eligible card per character makes the result fixed.

**Fur Coat:** marks normal-fight/elite map nodes using its own run seed + player slot + relic ID RNG and saves their coordinates. Matching maps reproduce the marks.

#### Event eligibility

The tables cover **56 ordinary events**, combining act availability with entry checks. Checks guaranteed by scope are omitted; run-wide visited exclusions also apply.

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

**Additional entry checks:** vanilla evaluates these in `IsAllowed`; the [design](#ordinary-event-selection) defers them until ID selection.

| Event | Entry check |
|---|---|
| Wood Carvings | a removable Basic card |
| Morphic Grove | ≥ 2 transformable cards |
| Luminous Choir | Available relic stock |
| Spiraling Whirlpool | a card eligible for Spiral |
| The Legends Were True | A nonempty deck |
| Field of Man-Sized Holes | a card eligible for Perfect Fit |
| Slippery Bridge | A removable card |

**Forced events:** Lantern Key's War Historian Repy is the only forced replacement of an ordinary-event draw; `IsAllowed` is always false.

#### Endless Conveyor dishes

Dishes are generated on entry and after each purchase's effects, even when the next purchase is unaffordable. Every fifth display is Seapunk Salad; other displays use one weighted event-RNG roll, excluding the previous dish.

| Dish | Weight | Additional condition |
|---|---:|---|
| Caviar | 6 | None. |
| Spicy Snappy / Jelly Liver / Fried Eel | 3 each | None. |
| Suspicious Condiment | 3 | An open potion slot. |
| Clam Roll | 6 | Below full HP. |
| Golden Fysh | 1 | After the first display. |

Jelly Liver's transformation and Spicy Snappy's random upgrade also use event RNG.

#### Combat setup and shuffles

**Encounter setup:** RNG seeded from run seed + total floor + encounter ID samples variable composition and starting states.

**Shuffle RNG:** opening shuffles, reshuffles and random pile insertions share `Shuffle` across combats.

**Shuffling:** opening shuffles start from deck-order clones. Reshuffles first sort combined discard/draw cards by identity and upgrade level (`StableShuffle`). Both use Fisher–Yates, then apply modifiers. Sorting leaves copies tied despite different enchantments, persistent progress or combat state, so input order can affect results.

**Discard order:** affects All for One with limited hand space and the shuffles used by Aggression and Drain Power. Frantic Escape's +1 combat cost per play can leave tied copies with different costs, changing which cost is drawn earlier.

**Random insertion:** for a pile of `n` cards, `CardPileCmd.Add` uses `Shuffle.NextInt(n + 1)` to select a uniform gap. Lifecycle guards run before the roll.

#### Enemy HP

**Generation:** `Niche` samples uniformly from the inclusive initial HP range, excluding `MaxHp` values already in the enemy list. An empty candidate set restores the full range. Every creation advances RNG once, including fixed ranges and singletons (`Creature.SetUniqueMonsterHpValue`).

**Order:** enemies are normally created and added sequentially, including summons. Death-triggered spawns roll before the dying enemy is removed. Gremlin Merc instead creates Fat, creates/adds Sneaky, then adds Fat: neither excludes the other's HP.

**Special cases:** Tough Egg's hatch rerolls its full hatchling range with `Niche`. Axebot's range rises by 10 per replacement. Decimillipede segments adjust HP to distinct even values. Punch-Off's starting reductions use encounter RNG.

**Fixed-HP summons:** Eye with Teeth (6), Parafright (21), and Gas Bomb (7/8 by ascension).

#### Enemy AI

**Selection:** run-wide `MonsterAi` supplies move choices and Fabricator's bot identities. Random branches sample cumulative effective weights in declaration order, accounting for repetition limits, cooldowns and conditions. Singleton/all-zero branches advance RNG once; deterministic transitions leave it unchanged. Encounter-assigned openings use setup RNG.

**All-zero fallback:** all-zero branches always choose the first listed move. Flyconid can reach `Smash → Vulnerable Spores → Frail Spores → Smash`, making all effective weights zero; it then selects Vulnerable Spores.

**History and interruption:** `MonsterMoveStateMachine` retains its state log for weight checks. Stun returns to the stored move; Thieving Hopper's Flutter depletion uses a deterministic follow-up. Decimillipede cycles `Writhe → Constrict → Bulk`; after `Dead → Reattach`, it chooses equally among these attacks using shared AI RNG.

**Repeated enemy types:** Inklets and rats are covered below. Other cases:

| Enemy | Move pattern |
|---|---|
| Small Leaf Slimes beside Slithering Strangler | Random opening, then forced alternation through random branches. |
| Exoskeletons | After Enrage, choose Skitter/Mandibles equally; Skitter forces `Mandibles → Enrage`. The normal encounter's fourth copy has a random opening. |
| Scrolls of Biting | After the first Chew, choose Chomp/Chew equally; a second Chew forces `Chomp → More Teeth → Chew`. |

**Inklet death timing:** outers start with Jab, alternating it with a 50/50 Piercing Gaze/Whirlwind choice; the middle starts with Whirlwind. Kill the middle before its first random selection. Killing outer A before versus after one random selection leaves B's second and later selections one RNG advance apart, even with matching eligible moves.

**Rat eligibility:** Summon depends on its timer, shared limit, space and teammate intents. With Screech on cooldown and both attacks eligible, enabling Summon changes attack/attack odds `1/2, 1/2` to attack/attack/Summon odds `1/11, 1/11, 9/11`. The same roll can then select a different attack. After `Screech → Scratch`, blocking Summon forces Disease Bite, still advancing RNG.

**Rat selection order:** slots `first` through `fifth`; originals occupy the last three. Earlier Summon choices block later rats. With equal otherwise-eligible probability `p` and no existing Summon intent, successive rats have probabilities `p`, `(1-p)p`, etc. Existing teammate intents can also block earlier selectors.

**Rat history alternatives:** originals open with Scratch, Disease Bite and Screech. With Summon timer-blocked, kill the Screech starter and either attacker before the first random selection. The survivor offers Bite/Screech after Scratch or Scratch/Screech after Bite. Vanilla gives 50% SL availability of Screech; shared move rankings or marker-filtered independent uniform sequences give `2/3`, retaining each survivor's 50/50 odds. Across all three equally weighted opening-history pairs, both percentile sampling and shared priorities average `1/3` agreement.

**Cross-type advancement:** in `Leaf Slime S → Twig Slime M → Twig Slime S`, killing either small slime can shift Twig M's next roll. Leaf S's forced alternation advances AI RNG; Twig S's fixed attack leaves it unchanged.

**Survivor-choice alternatives:** Flyconid opens with `Smash → Frail Spores`; Twig Slime M with `Sticky Shot → attack`. Assume either can be killed before the next intent selection. The sole survivor uses the same next AI roll:

| Roll half | Flyconid survives | Twig M survives |
|---|---|---|
| Lower | Vulnerable Spores | Attack |
| Upper | Smash | Sticky Shot |

Exactly one kill yields a zero-damage next intent: 50% before seeing the intent; guaranteed with SL exploration.

**Same-type survivor alternatives:** leave an original rat at `Scratch → Disease Bite → Summon → Scratch` and its summoned copy opening with Scratch; remove other rats. Their random-selection counts are three and one. Kill either before Scratch executes. Both survivors offer Disease Bite (7 damage at A7+)/Screech (Frail, zero damage) at 50/50; Summon is spent for the original and timer-blocked for the copy. Vanilla's shared next roll gives the same result for either kill.

**Fabricator:** Fabricate selects Guardbot/Noisebot, then Zapbot/Stabbot; Fabricating Strike selects only Zapbot/Stabbot. Each draw excludes the last bot across both pools, so a defensive bot permits the previous offensive bot again. Full capacity forces Disintegrate; bots use fixed moves. Kill/stun timing changes selection timing, preserving bot order and identities.

#### Random enemy targeting

Direct random effects and automatic enemy targeting share run-wide `CombatTargets`, sampling uniformly among eligible enemies. Singletons advance RNG once; empty sets leave it unchanged. Automatic play and Lightning retain supplied targets; intrinsically random-hit effects roll per hit and allow repeats.

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

**Filtered relic pulls:** remove the first eligible entry from the requested end and compact the list. For one uniformly shuffled rarity with two shop-eligible identities and one shop-ineligible identity:

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

**Tradable relics:** Ranwid and Relic Trader share `RelicModel.IsTradable`, which excludes Starter/Event/Ancient, upon-pickup, used-up, melted and pet-spawning relics.

| Source | Mechanics |
|---|---|
| Chests | Relics are generated on room entry; base gold uses `Rewards` on opening. Pool and consumption rules: [relic bags](#relic-bags). |
| Neow — Silver Crucible | First treasure room is empty, with neither gold nor relics. |
| Wongos | Act 2; all players have ≥100 gold. Featured Rare: 200 gold; hidden Common: 100 gold. Leaving downgrades a random upgraded card, if any. Featured Rare is generated and consumed from stock on offer, even if declined. This also occurs below 200 gold and is **harmful to same-seed consistency**. Hidden Common is generated on purchase. Both require `IsAllowedInShops`. |
| Wongos — Mystery Ticket | Pays three random relics after five combats. |
| Ranwid — relic sacrifice | Offers one uniformly selected tradable owned relic using event RNG; sacrificing it grants two hidden random relics, generated after removal. |
| Relic Trader | Offers three trades, using an event-RNG shuffle of tradable owned relics. Generates all three displayed replacements from the player bag's front with player `Rewards` rarity rolls, independently of outgoing rarity; consumes them before choice. Choose one; normally no leave. |
| Courier | 20% discount; removal never restocks; sale clears on restock. Restocked character cards retain type, colorless cards retain rarity. Relics use the [vanilla relic rarity roll](#relic-bags) even in the Shop slot. Shop ordinal does not advance. Potion restocks do not guarantee Uncommon. |
| Trash Heap | Dive In costs 8 HP and grants a random relic from its special list; Grab grants 100 gold and directly adds a random card from its special ten-card list. |

#### Shop and Courier RNG

**Cards:** initial colorless slots request Uncommon/Rare. Character cards fall back to the next available rarity. Generation excludes displayed cards, including the just-purchased card during restocking.

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

Thus even one restock per category can interfere. Identical RNG state, requests and ordered candidate lists reproduce outcomes. Events may consume shared rewards/bags despite having event RNG.

### Cards

**Card pools:** Common Power cards do not exist.

#### Card state and selection

**Combat card selection:** selection uses `CombatCardSelection`, except auto-play uses `Shuffle`. `NextItem` advances the RNG once for singletons; batch selectors shuffle the full input before taking their count. See [shuffle mechanics](#combat-setup-and-shuffles) for `StableShuffle` ordering.

**Out-of-combat player-selected card batches:** batch selections are unordered. Deck selectors and reward grids collect the full selection before effects. Enumeration can affect acquisition order (e.g. Sea Glass).

**Deck operations on player-selected cards:** the inspected deck-selection callers cover upgrading (Smith), enchanting (Gnarled Hammer), permanent removal (Empty Cage), transformation (Astrolabe, Claws, Wood Carvings), combining removals with a new addition (Amalgamator), temporary storage for return (Pael's Tooth), and copying (Dolly's Mirror). Wongos and Reflections downgrade randomly. Reward selections acquire new occurrences. Catfood's inspected patches add no operation category.

**Persistent card state:** `SerializableCard.Equals` compares identity, upgrade level and enchantment while omitting card-specific saved properties. Genetic Algorithm maintains `CurrentBlock = 1 + IncreasedBlock`; The Scythe maintains `CurrentDamage = 13 + IncreasedDamage`. Tinker Time grants one configured Mad Science; later copies inherit its type/rider configuration. Dolly's Mirror excludes Quest cards, including Spoils Map.

**Mad Science:** after combat, permanently upgrades random eligible deck cards without replacement, one per power stack, using `CombatCardSelection`.

**Reflections:** Act 3; uses event RNG to downgrade up to two distinct upgraded cards, then upgrade up to four distinct upgradable cards, including those just downgraded.

**Thieving Hopper:** appears at most once in supported runs and steals once. Select uniformly from draw/discard copies with `DeckVersion`, using the first nonempty rarity/enchantment preference tier, or all candidates if every tier is empty (`ThievingHopper.ThieveryMove`). Theft removes the deck card; Hopper's death offers that same object as a skippable recovery reward (`SwipePower`, `SpecialCardReward`). Escape or skipping recovery leaves the card removed.

**Pael's Tooth:** stores five player-selected unupgraded cards. After each combat, returns one random remaining stored card upgraded, using `Rewards` RNG.

**Beautiful Bracelet:** enchants up to four random Swift-eligible deck cards with Swift 2, using `Niche` RNG.

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

#### Random orb generation

Chaos and Trash to Treasure sample uniformly from Lightning, Frost, Dark, Plasma and Glass using run-wide `CombatOrbGeneration`. They roll before channeling, so failed attempts advance the RNG. Defect cannot channel with zero slots; characters with no base orb slots automatically gain one (`OrbModel.GetRandomOrb`, `OrbCmd.Channel`).

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
| Crystal Sphere | The board has one card-reward item for each of Common, Uncommon and Rare. Each recovered item gives a three-card offer of that rarity; vanilla identities and upgrade rolls use event RNG. |
| The Future of Potions | Offers the first three owned potions in slot order; independently assigns a random allowed card type to every owned potion using event RNG. Trade a potion to choose one of three upgraded character cards of the option's rarity/type. Eight combinations exist: Common Attack/Skill and Uncommon/Rare Attack/Skill/Power. Rare/Event potions map to Rare cards; Common/Token to Common. |
| Lead Paperweight / Brain Leech — Rip | Choose one of two / three colorless cards respectively. Rip costs 5 HP and suppresses random upgrades. |
| Kaleidoscope | Two three-card offers; each independently selects three distinct other characters and generates one card from each using base rarity odds. |
| Colorful Philosophers | Choose another character's color, then receive one three-card offer per rarity (Common/Uncommon/Rare) from that pool. Ordinary upgrade rolls apply. |
| Sea Glass | Three five-card generation batches (Common, Uncommon, Rare) from one featured other character, each triggering Wing Charm separately. Take any number from the combined display. No random upgrades. |
| Prismatic Gem / Dingy Rug | Gem (Orobas, Act 2) adds unlocked character pools unless the original pools are all colorless; Rug adds colorless cards, which are only Uncommon/Rare. Both require the card-reward flag and permitted pool modification, and preserve filters. After any rarity roll, sample uniformly among eligible identities, not equally among pools. Brain Leech's Share Knowledge and Room Full of Cheese lack the required flag and remain unchanged; The Future of Potions blocks pool modification. |
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
| Ranwid / Stone of All Time | Select one owned potion uniformly in occupied-slot order using event RNG. |
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

All six use the [combat-card factory mechanics](#combat-card-generation-and-transformations-1). Dingy Rug and Prismatic Gem do not expand these potions' eligible card pools.

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

**Direct/event generation:** card factories seed by adding room/event count to a per-room options-call index. Seed collisions exist; harmful gameplay impact remains unverified. Transformations retain caller-supplied RNGs. Neither uses shared identity sequences by pool.

**Combat setup:** inherits vanilla event/encounter identity selection; cumulative room counters do not select identities. Encounter ID + room-type ordinal controls variable composition/setup, not ordinary HP. No visible setup mismatch has been reproduced. Lantern Key's forced event is counted normally.

**Shuffles:** re-seeds shuffle RNG at combat start; later shuffles continue it.
