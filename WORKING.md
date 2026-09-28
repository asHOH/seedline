# Deterministic seed mod — working document

Settled systems and reusable existing-mechanics references are maintained exclusively in [DESIGN.md](DESIGN.md). This file contains only unresolved work and evidence needed to resolve it, evaluated against the [goals and fairness principle](DESIGN.md#goals).

When a decision settles, update its canonical section in DESIGN.md and delete the resolved discussion here; retain at most a one-line pointer where needed by remaining work.

**Reading convention:** use **Known** for findings, **Constraints** for local requirements beyond DESIGN, **Decisions needed** for unresolved choices, **Candidates** for unadopted approaches, and **Validation** for checks still needed. Omit unused roles. Candidates become specification only when adopted in DESIGN.

## Remaining design: rewards and encounter

Use the [Rewards / Encounter categories](DESIGN.md#design-categories) and [architecture dimensions](DESIGN.md#architecture-overview) to frame each remaining decision.

Distinguish normal fights, elites, bosses, Ancients, chests, events and special relic effects only where their rules differ.

**Candidate defaults:** cumulative ordinals for Rewards; act-local ordinals for Encounter, subject to each unresolved source's sharing/addressing decision.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

**Next decisions:** refine persistent card-selection priorities and Relic Trader replacement sourcing/mapping; choose and calibrate the non-Rare offer sequences; then decompose remaining events/Ancients as needed.

### Rewards

#### Cards: offers and rarity

Settled source sharing, luck, deferral, rerolls and Brain Leech grouping: [own-character random-rarity offers](DESIGN.md#own-character-random-rarity-offers).

##### Random-rarity offer sequences

**Decisions needed:**

- Choose two or three offer sequences below, including non-Rare routing for fixed-base-odds sources other than Brain Leech. Balance the elite Uncommon advantage against matching choices when different sources reach the Rare threshold.
- Place the special opening entries under the chosen sharing scheme. Fixed-odds rewards can consume normal non-Rare entries before the first two normal-rule rewards; a fully shared non-Rare sequence can also be advanced by elites. Decide how the opening exception should be addressed without making stored entries depend on the live offset.
- Calibrate Common/Uncommon distributions for each non-Rare sequence, the shared rare-bearing companions and Brain Leech's two event-generated cards. Exact modeling and probabilities are deferred; include varied routes, events, extra rewards and shops, especially Orrery. Do not adopt the illustrative `3 normal → 1 elite → 2 normal → 1 elite` route as the full model or transfer the shop's `0.008` offset adjustment.
- Behavior without Scarcity, including multiple Rares and deferral, remains deferred.

**Candidates — both remain strong:**

| Offer sequences | Tradeoff |
|---|---|
| Three: normal non-Rare, elite non-Rare, shared rare-bearing | Retains a richer elite non-Rare distribution, but which source produces a Rare changes both non-Rare consumption and the resulting Uncommon advantage. |
| Two: shared non-Rare, shared rare-bearing | Aligns non-Rare choices across sources, at the cost of the distinct elite non-Rare Uncommon advantage. |

**Known — tradeoff:** with one normal and one elite reward, a Rare in the normal reward consumes a shared Rare offer plus an elite non-Rare offer; a Rare in the elite consumes a normal non-Rare offer plus the same shared Rare offer. Changing the shared Rare companions alone cannot remove this difference.

**Validation:** use automated comparison with vanilla across normal-heavy, elite-heavy and mixed routes, including fixed-odds and extra offers. Compare Rare-bearing offer counts, waiting distributions and conditional Uncommon rates; paired same-seed checks should verify offer alignment, rerolls, pending delivery across acts and Brain Leech crossings in slots four/five. Force rare multi-crossing cases rather than relying on manual runs to encounter them.

**Pending baseline validation:** determine gameplay impact of Catfood's initial-offer stream bug and direct/event seed collisions; see the baseline above.

### Encounter

#### Events and Ancients

##### Relic Trader

Owned-relic selection, replacement constraints and offer consumption: [settled policy](DESIGN.md#settled-policy-relic-trader).

**Decisions needed:** choose a non-generic replacement source and resolve input-linked identity assignment, collisions and changing eligibility without consuming unchosen generic opportunities. Determine which existing allocation/isolation guarantees must change if replacement stock is reserved or taken from shop/Courier.

**Constraint:** for selected inputs `A,B,C` versus `B,C,D`, preserve `B → B'` and `C → C'` whenever compatible with replacement eligibility and distinctness; do not relabel one sequential three-relic offer by display position.

**Candidates:** key an independent rarity roll and per-rarity candidate order to each outgoing relic identity; resolve collisions deterministically without rerolling rarity merely because an identity collides. An isolated Trader allocation protects ordinary source progress but reopens the three-allocation design. Using another existing allocation avoids a fourth stock but couples Trader to that source.

A separate, overlapping Trader offer stock could instead consume its own offer entries while leaving unchosen identities available in ordinary allocations. It would require explicit acquisition-time reconciliation and permit later ordinary reoffers of declined replacements, changing the disjoint-stock model. No replacement source is adopted.

**Validation:** compare overlapping selected sets with identical replacement availability, then force same-target collisions, already-owned/ineligible counterparts and source depletion. Check rarity independence, batch uniqueness and unchanged generic coverage.

##### Remaining events and Ancients

Remaining Encounter decisions have not yet been decomposed. Evaluate them against the [event-specific reward policy](DESIGN.md#settled-policy-event-specific-rewards) and existing event-list rules as they are investigated.

#### Card-selection priorities

##### Permanent upgrades and downgrades

**Decisions needed:** selection scope, priority normalization/reuse, source/effect sharing, new entrants and re-entry after eligibility changes. Establish priority lifetimes before extending them to other Encounter systems.

**Candidates:**

- **Corrected persistent priorities:** retain inherent purpose-specific randomness. After selecting the minimum eligible priority `m`, map each unselected eligible survivor from `u` to `(u - m) / (1 - m)`. Equivalently, subtract the elapsed minimum from eligible exponential clocks. New candidates receive fresh inherent draws; a selected candidate needs a renewed draw before participating again. Decide inactive-candidate treatment and shared scopes explicitly.
- **Fresh opportunity-specific priorities:** the [adopted opt-in mechanism](DESIGN.md#opportunity-specific-selection-priorities) remains a strong alternative for effects where persistent ordering provides little benefit. Its use by Relic Trader does not settle card selection.

**Known:** uncorrected priorities bias fresh entrants: after selecting the lower of two independent uniform priorities, a new candidate beats the survivor with probability `2/3`, not `1/2`. Conditional survivor normalization corrects this and preserves order within a jointly participating survivor cohort. Normalizing one eligibility subset can change its order relative to inactive cards, so it does not guarantee a fixed global order across arbitrary histories.

**Validation:** compare conditional selection odds and shared-card order across nested candidate sets, new additions, differing eligibility subsets, upgrades followed by downgrades, repeated selection and save/load. Treat exact uniform selection and cross-run sequence similarity as separate checks.

##### Pael's Tooth: return-priority lifecycle

Direction and equivalent-copy requirement: [settled policy](DESIGN.md#settled-policy-paels-tooth).

**Decisions needed:** address indistinguishable Basic Strikes/Defends without making the player's choice of physical copy observable in later returns; specify priority treatment when new removed cards join an already-used stored pool or a record becomes eligible again.

**Candidates:** use canonical virtual occurrence tokens within a gameplay-equivalent Basic-card class, retaining one independent priority per occurrence rather than one per card identity. Corrected persistent priorities above are a candidate for late entrants, not an adopted Tooth lifecycle.

**Validation:** establish the stored-record lifecycle from code, including what happens after return and re-removal; the existing reference does not establish selection without replacement. Check equal-status copy substitution, multiplicity-weighted selection and common-card return-order alignment.

#### Combat randomness

##### Shuffles

**Decision needed:** opening-shuffle addressing and how later shuffles share or advance randomness.

**Candidates:**

- Act-local opening-shuffle ordinals. Together with act-local setup, this would align the first Act 2 normal fight's setup/shuffle addresses despite different Act 1 normal-fight counts; cumulative reward addresses would still differ.
- Add run seed to boss shuffle initialization.

##### Generated cards

**Decision needed:** sharing and addressing by pool/type: own-character Attack/Skill/Power, colorless and other characters; include special-relic interactions.

##### Other combat randomness

**Backlog:** monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations. As each area is investigated, decide sharing by source/effect and special-relic interactions, and split it into its own decision unit only where useful.
