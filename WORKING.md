# Deterministic seed mod — working document

Unresolved work and supporting evidence, evaluated against [DESIGN's goals and fairness principle](DESIGN.md#goals-and-principles). [DESIGN.md](DESIGN.md) owns settled policies and reusable mechanics references.

**Reading convention:** **Known** = findings; **Constraints** = requirements beyond DESIGN; **Decisions needed** = open choices; **Candidates** = approaches still under consideration; **Reference alternative** = comparison for an open decision, not a leading candidate; **Validation** = pending checks. Adopted candidates become spec in DESIGN; settled exclusions worth retaining become its **Rejected alternatives**. Omit empty roles and obvious exhaustive alternatives.

---

Use the [Rewards / Encounter categories](DESIGN.md#concepts-and-terms) and [architecture dimensions](DESIGN.md#architecture-overview); distinguish contexts only where rules differ.

**Candidate defaults:** cumulative ordinals for Rewards; act-local ordinals for Encounter, subject to each unresolved source's sharing/addressing decision.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

**Remaining work roadmap:**

1. [Events and Ancients](#events-and-ancients): settle ordinary event identity selection and Relic Trader replacement sourcing/mapping; then decompose remaining events/Ancients as needed.
2. [Item-selection priorities](#item-selection-priorities): complete canonical card ranking; validate settled equivalent-copy routing for cards and potions.
3. [Combat randomness](#combat-randomness): settle opening/later shuffles and remaining card generation; investigate monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations.

**Stakeholder input:** use answers to [Questions for 二硫键](Questions.md) about perceived Act 1/shop Rare rates and potentially fixable same-seed divergence to inform calibration and remaining investigation.

## Encounter

### Events and Ancients

#### Ordinary event identity

**Intent and constraints:** retain the same seeded identity order for events with equivalent complete eligibility conditions within the chosen scope. Unavailability must not consume that condition's next identity. Preserve the act-specific plus shared pool, live eligibility, run-wide visits, [forced-event bypass](DESIGN.md#settled-policy-forced-events) and event-internal RNG. Different actual visits remain meaningful exclusions; state dependence alone does not rule out the shared filtered-draw strategy, but that strategy does not automatically settle event scheduling.

**Boundary:** assume sufficient eligible, unvisited supply; exclude wrapping, exhaustion and singleton sets from this decision's scope. Use the vanilla skipping probability property in [existing mechanics](DESIGN.md#generation-and-selection-mechanics). Small examples isolate selection behavior, not ordinary pool capacity.

**Candidate class model:** partition identities by equivalent complete predicates, including act availability within the chosen scope. Predicate domains may overlap while identity classes remain disjoint: `gold >= 100` and `gold >= 125` can both be active, but each identity is counted once. Use stable event/class keys, not the current candidate set or enumeration order. Whether to preserve additional ordering across distinct nested/intersecting conditions remains open.

**Candidates:**

- **Paused clocks:** reuse [corrected persistent clocks](DESIGN.md#corrected-persistent-selection-priorities) per event. Equivalent conditions give survivors identical updates, preserving their relative order without a separate group draw. Visited events leave selection; no within-act renewal is needed. This retains eligibility-history effects and lets previously ineligible events compete immediately on re-entry, unlike vanilla passing. Matching current sets and ordinals need not reconverge.
- **Condition-level passing:** separate each condition's fixed identity order from its scheduling entries. Pass an inactive condition's scheduling entry without consuming its identity head. Define entry retirement/deferral and later scheduling weights; a finite schedule can lose all of a condition's entries while its identities remain. Simple identity-level passing fails the condition-order requirement. Nested/intersecting conditions must not duplicate identity weight.

**Known — probability tradeoff:** corrected paused clocks remain uniform over current eligible, unvisited identities, assuming eligibility does not inspect hidden clocks; memorylessness makes active survivors' residuals and frozen inactive clocks independent equal-rate exponentials. A class with `n` of `N` eligible survivors has chance `n/N`; an aggregated class clock needs rate `n`. This is not vanilla's invariance to encounters while K is false: in the appendix's two-K/three-other example, retaining all K identities gives `2/4` after one such encounter and `2/3` after two, versus vanilla's `2/5`. Neither is a clock-calculation bias; adopting paused clocks deliberately changes this baseline probability behavior.

**Reference alternative — fresh per-event priorities:** act + ordinary-event ordinal + event ID + purpose gives uniform selection and reconvergence for matching ordinals and current sets, without failed-check history. It does not retain a condition's identity order, so it is not a leading candidate. For candidate sets `S,T`, agreement is `|S ∩ T| / |S ∪ T|`, optimal for nested sets but not every pair; `{A,B}` versus `{A,C}` gives `1/3`, while a pair-specific coupling can reach `1/2`. This does not establish a universally better independent-run coupling.

**Decisions needed:** choose paused clocks or fully specified condition-level passing, with a stated rationale for any change to vanilla's inactive-condition probability property. Define class equivalence and overlap guarantees, scheduling weights and state, and separate lifetimes for act-specific and cross-act identities/timing. Act-local clock initialization uses act + event ID + purpose; resetting removes prior-act timing history, not visited exclusions. Keeping a cross-act class's identity order while resetting its timing is a separate candidate. Ordinary-event ordinals, if used, count encounters rather than rejected candidates. Ancients remain separate.

**Implementation and validation:** a common offset replaces equal subtraction; pausing needs separate active-time offsets per class. Track identity-sequence positions and scheduling state separately where the candidate requires them. Leaving raw clocks unchanged across different eligibility histories is not equivalent: with A,C active and B inactive, after A wins, B beats corrected `C-A` with probability `1/2`, but uncorrected C with `2/3`. Check same-condition order, overlapping thresholds, eligibility re-entry, the inactive-K probability example, different visit histories, forced insertion, act changes and save/load.

#### Relic Trader

Owned-relic selection, replacement constraints and offer consumption: [Relic Trader policy](DESIGN.md#settled-policy-relic-trader).

**Decisions needed:** choose replacement stock and input-linked assignment; define collision resolution, ownership/eligibility handling and any unavoidable changes to source isolation.

**Constraints:** only one trade executes; resolving another outgoing relic's proposed target leaves that relic owned. All three displayed replacements must be legal against the actual inventory, not a hypothetical simultaneous three-relic exchange.

**Candidates — graph and assignment:**

- **Successor ring:** construct a stable seeded ring of relic identities and trade each outgoing relic to a successor. If the successor is another selected outgoing relic, resolve that relic's target first; if already owned otherwise, continue around the ring. This is a sketch, not a complete algorithm: specify occupied-target skipping, cycle handling, termination and deterministic resolution independent of offer order. An unweighted identity ring does not implement the standard replacement-rarity weights by itself, and a ring does not solve stock ownership/isolation.
- **Rarity-first input-linked preferences:** key an independent rarity roll and per-rarity replacement preference order to each outgoing identity. Resolve distinct targets in a stable, independently addressed outgoing priority order, skipping claimed or unavailable targets without rerolling rarity merely because of a collision. Keep eligibility separate from preference addresses. Where each input can use every available target of its rolled rarity, feasibility reduces to available count per rarity, not a general graph-matching problem; genuinely input-specific restrictions may require matching.
- **Graph-aware outgoing selection:** jointly choose three outgoing relics and feasible replacement edges. This reopens the settled uniform opportunity-priority selection; it is not adopted. Feasibility or degree-based filtering can bias the outgoing choices, so compare its sampling law as well as counterpart alignment.

**Known — rarity-first sampling:** with fixed eligibility shared by all inputs requesting a rarity, sufficient capacity, independent uniform preference rankings and a processing order independent of those rankings, sequential assignment is uniform without replacement within that rarity. Each input's highest-ranked remaining target is uniform among the unclaimed eligible identities.

**Candidates — stock:** an overlapping Trader-local offer ledger could consume all three local offers while leaving unchosen ordinary entries available, reconciling only the acquired identity with ordinary stock. This changes global retirement semantics and cannot preserve a future ordinary offer of an identity already acquired from Trader. Another possibility is using eligible, unowned identities whose closed ordinary opportunities are already consumed, avoiding future-stock depletion where sufficient; its per-rarity availability and route-dependent replacement support prevent treating it as a complete source solution. No stock model is adopted.

**Validation:** compare overlapping selected sets with identical replacement availability, then force competing targets, adjacent outgoing ring nodes, owned-but-unselected successors, cycles, cutoff-ineligible counterparts and per-rarity shortages. Check outgoing selection probabilities, independent replacement rarity, batch uniqueness/order neutrality, generic coverage and which ordinary source changes after an actual trade versus an unchosen offer.

#### Remaining events and Ancients

Decompose remaining Encounter random operations as investigated, evaluating them against the [event-specific reward policy](DESIGN.md#settled-policy-event-specific-rewards) and existing event-list rules.

### Item-selection priorities

#### Canonical card order

**Decisions needed:** complete the [canonical ranking](DESIGN.md#canonical-card-order) for identities, upgrades, enchantments and persistent progress. Starting point: Curse < Strike < Defend < other Basic < Common.

#### Equivalent-copy routing

[Settled policy](DESIGN.md#representative-routing) · [Model checks](outputs/routing-validation.cjs).

**Validation:** in-game commit/cancel and snapshot timing; occurrence state through Tooth storage/return and save/load; clock/tie handling. End-to-end checks await [canonical ranking](#canonical-card-order) and [shuffles](#shuffles).

### Combat randomness

#### Potion-generated cards

**Validation:** verify [card-generation ordinal consumption](DESIGN.md#settled-policy-potion-generated-cards) through use, discard, trade, reward skips and save/load. Include skipped card offers, the Cauldron reward-skip exception, failed acquisitions and cancellations.

#### Shuffles

**Decision needed:** opening-shuffle addressing, sharing with later shuffles, and advancement rules, subject to [deck-order independence](DESIGN.md#canonical-card-order).

**Candidates:**

- Act-local opening-shuffle ordinals, matching encounter setup.
- Include run seed in boss-shuffle initialization.

#### Generated cards

**Decision needed:** sharing and addressing for remaining combat card-generation sources by pool/type: own-character Attack/Skill/Power, colorless and other characters; include special-relic interactions. [Potion-generated cards](DESIGN.md#settled-policy-potion-generated-cards) are settled.

#### Other combat randomness

**Backlog:** monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations. During investigation, decide source/effect sharing and special-relic interactions; split decision units only where useful.
