# Deterministic seed mod — working document

Settled systems and reusable existing-mechanics references are maintained exclusively in [DESIGN.md](DESIGN.md). This file contains only unresolved work and evidence needed to resolve it, evaluated against the [goals and fairness principle](DESIGN.md#goals-and-principles).

When a decision settles, update its canonical section in DESIGN.md and delete the resolved discussion here; retain at most a one-line pointer where needed by remaining work.

**Reading convention:** use **Known** for findings, **Constraints** for local requirements beyond DESIGN, **Decisions needed** for unresolved choices, **Candidates** for unadopted approaches, and **Validation** for checks still needed. Omit unused roles and obvious exhaustive candidate lists; weaker alternatives may remain when they are not the only other candidate. Candidates become specification only when adopted in DESIGN.

## Remaining design: rewards and encounter

Use the [Rewards / Encounter categories](DESIGN.md#concepts-and-terms) and [architecture dimensions](DESIGN.md#architecture-overview) to frame each remaining decision.

Distinguish normal fights, elites, bosses, Ancients, chests, events and special relic effects only where their rules differ.

**Candidate defaults:** cumulative ordinals for Rewards; act-local ordinals for Encounter, subject to each unresolved source's sharing/addressing decision.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

**Next decisions:** resolve equivalent-copy routing and persistent-selection sharing scopes, and Relic Trader replacement sourcing/mapping; choose and calibrate the non-Rare offer sequences; then decompose remaining events/Ancients as needed.

**Stakeholder input:** [Questions for 二硫键](Questions.md) covers perceived Act 1/shop Rare rates and potentially fixable same-seed divergence; use the answers to inform calibration and remaining investigation.

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

Owned-relic selection, replacement constraints and offer consumption: [Relic Trader policy](DESIGN.md#settled-policy-relic-trader).

**Decisions needed:** choose replacement stock and input-linked assignment without retiring unchosen generic opportunities; define collision resolution, ownership/eligibility handling and any unavoidable changes to source isolation. A fourth disjoint Trader allocation is rejected, not a pending candidate.

**Constraints:** for inputs `A,B,C` versus `B,C,D`, preserve `B → B'` and `C → C'` whenever compatible with replacement eligibility and distinctness. Only one trade is executed: resolving a proposed target for another outgoing relic does not remove that relic from the player's inventory. All three displayed replacements must be legal alternatives against the actual inventory, not a hypothetical simultaneous three-relic exchange.

**Candidates — graph and assignment:**

- **Successor ring:** construct a stable seeded ring of relic identities and trade each outgoing relic to a successor. If the successor is another selected outgoing relic, resolve that relic's target first; if already owned otherwise, continue around the ring. This is a sketch, not a complete algorithm: specify occupied-target skipping, cycle handling, termination and deterministic resolution independent of offer order. An unweighted identity ring does not implement the standard replacement-rarity weights by itself, and a ring does not solve stock ownership/isolation.
- **Rarity-first input-linked preferences:** key an independent rarity roll and per-rarity replacement preference order to each outgoing identity. Resolve distinct targets in a stable, independently addressed outgoing priority order, skipping claimed or unavailable targets without rerolling rarity merely because of a collision. Keep eligibility separate from preference addresses. Where each input can use every available target of its rolled rarity, feasibility reduces to available count per rarity, not a general graph-matching problem; genuinely input-specific restrictions may require matching.
- **Graph-aware outgoing selection:** jointly choose three outgoing relics and feasible replacement edges. This reopens the settled uniform opportunity-priority selection; it is not adopted. Feasibility or degree-based filtering can bias the outgoing choices, so compare its sampling law as well as counterpart alignment.

**Known — rarity-first sampling:** with fixed eligibility shared by all inputs requesting a rarity, sufficient capacity, independent uniform preference rankings and a processing order independent of those rankings, sequential assignment is uniform without replacement within that rarity. Each input's highest-ranked remaining target is uniform among the unclaimed eligible identities.

**Candidates — stock:** an overlapping Trader-local offer ledger could consume all three local offers while leaving unchosen ordinary entries available, reconciling only the acquired identity with ordinary stock. This changes global retirement semantics and cannot preserve a future ordinary offer of an identity already acquired from Trader. Another possibility is using eligible, unowned identities whose closed ordinary opportunities are already consumed, avoiding future-stock depletion where sufficient; its per-rarity availability and route-dependent replacement support prevent treating it as a complete source solution. No stock model is adopted.

**Validation:** compare overlapping selected sets with identical replacement availability, then force competing targets, adjacent outgoing ring nodes, owned-but-unselected successors, cycles, cutoff-ineligible counterparts and per-rarity shortages. Check outgoing selection probabilities, independent replacement rarity, batch uniqueness/order neutrality, generic coverage and which ordinary source changes after an actual trade versus an unchosen offer.

##### Remaining events and Ancients

Remaining Encounter decisions have not yet been decomposed. Evaluate them against the [event-specific reward policy](DESIGN.md#settled-policy-event-specific-rewards) and existing event-list rules as they are investigated.

#### Card-selection priorities

##### Permanent upgrades and downgrades

The [corrected persistent exponential-clock strategy](DESIGN.md#corrected-persistent-card-selection-priorities) is settled.

**Decisions needed:** assign remaining permanent-selection effects to sharing scopes, including whether upgrades and downgrades use separate scopes and which different granting sources share one scope. Weighted or intentionally sequential effects need source-specific treatment rather than an automatic uniform batch rule.

**Validation:** test shared-card order and conditional selection odds across additions, inactive subsets, eligibility re-entry, selected-occurrence renewal, batch selection and save/load. Confirm player-directed choices and previews do not advance random-selection clocks.

##### Equivalent-copy routing

Requirements, multiplicity and UI behavior: [equivalent-card selections](DESIGN.md#equivalent-card-selections). Tooth's fixed cohort and reward-choice alignment are settled; its former return-lifecycle question is closed.

**Decisions needed:**

- Define the gameplay-equivalence status key for each relevant card/effect, and the hidden representative chosen when interchangeable physical copies carry different inherent RNG provenance. Keep progress separate by card identity without deriving identity from UI/acquisition order.
- Choose count coupling: compare equivalent-copy cohorts of two versus three, not only equal-sized decks. Decide how logical tokens enter, leave, split into different status classes and merge again without renumbering surviving tokens or pooling their multiplicity.
- Choose operation-purpose sharing and opportunity progress. Separate upgrade/removal/etc. representative streams are the leading direction; which sources of the same operation share progress remains open. For opportunity-indexed variants, decide advancement for singleton or whole-class selections so count differences do not inadvertently shift later addresses.
- Specify hidden-token association after routing an operation to the clicked physical card, preserving the selected UI object and acquisition-order display. For unordered batches, resolve the selected multiplicity of each class against a snapshot, not click order; effects producing different results also need a deterministic hidden assignment to inputs.

**Candidate — representative selection over logical tokens:** use one stable logical RNG token per copy, distinct from the physical UI object; keep its relevant future selection keys, aliases, residual clocks and renewal counters together. Give tokens independent representative priorities within each card-identity/operation scope; choose the lowest priority, or the lowest `k` for `k` equivalent selected copies. Reusing corrected persistent clocks in this separate routing scope would preserve surviving-token order; fresh opportunity-specific priorities remain an alternative with less persistent state. Candidate count and the full candidate list affect eligibility, not the randomness address. Route the chosen hidden token(s) to the selected physical card(s), preserving the remaining token multiset. An already randomly selected logical winner needs no second representative draw. Do not choose representatives by the Tooth-return clock or another downstream outcome being aligned.

**Known — count coupling:** for nested logical-token cohorts with shared independent representative priorities, selections agree whenever the larger cohort selects a common token; two-versus-three agreement is `2/3`, the maximal uniform coupling. This is a matched-state result, not a guarantee across different eligibility histories; first establish meaningful common token keys across runs.

**Validation:** swap clicked copies of any identity while holding gameplay status fixed; compare subsequent identity/status outcomes and retained hidden-token multisets without changing which UI card visibly changed. Test multiplicity, two/three-copy cohorts, multi-selection click permutations, class splits/merges, reward-choice priority aliases and save/load.

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
