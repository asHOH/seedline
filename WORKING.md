# Deterministic seed mod — working document

Unresolved work and supporting evidence, evaluated against [DESIGN's goals and fairness principle](DESIGN.md#goals-and-principles). [DESIGN.md](DESIGN.md) owns settled policies and reusable mechanics references.

**Reading convention:** **Known** = findings; **Constraints** = requirements beyond DESIGN; **Decisions needed** = open choices; **Candidates** = approaches still under consideration; **Reference alternative** = comparison for an open decision, not a leading candidate; **Validation** = pending checks. Adopted candidates become spec in DESIGN; settled exclusions worth retaining become its **Rejected alternatives**. Omit empty roles and obvious exhaustive alternatives.

---

Frame decisions with the [Rewards / Encounter categories](DESIGN.md#concepts-and-terms) and [architecture dimensions](DESIGN.md#architecture-overview), distinguishing contexts only where their rules differ.

**Candidate defaults:** cumulative ordinals for Rewards; act-local ordinals for Encounter, subject to each unresolved source's sharing/addressing decision.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

**Next decisions:** resolve ordinary event identity selection, equivalent-copy routing, and Relic Trader replacement sourcing/mapping; then decompose remaining events/Ancients as needed.

**Stakeholder input:** [Questions for 二硫键](Questions.md) covers perceived Act 1/shop Rare rates and potentially fixable same-seed divergence; use the answers to inform calibration and remaining investigation.

## Pending impact assessments

Assess forced-event bypass, selection priorities and reward-choice anchors, equivalent-copy routing, and potion-option mapping for gameplay impact under the [vanilla-deviation convention](DESIGN.md#vanilla-deviations). This is an assessment backlog; policy improvements require a separate decision. Grade unresolved policies when settled.

## Encounter

### Events and Ancients

#### Ordinary event identity

**Intent and constraints:** retain the same seeded identity order for events with equivalent complete eligibility conditions within the chosen scope. Unavailability must not consume that condition's next identity. Preserve the act-specific plus shared pool, live eligibility, run-wide visits, [forced-event bypass](DESIGN.md#settled-policy-forced-events) and event-internal RNG. Different actual visits remain meaningful exclusions; state dependence alone does not rule out the shared filtered-draw strategy, but that strategy does not automatically settle event scheduling.

**Boundary:** assume sufficient eligible, unvisited supply; exclude wrapping, exhaustion and singleton sets from this decision's scope. Use the vanilla skipping probability property in [existing mechanics](DESIGN.md#generation-and-selection-mechanics). Small examples isolate selection behavior, not ordinary pool capacity.

**Candidate class model:** partition identities by equivalent complete predicates, including act availability within the chosen scope. Predicate domains may overlap while identity classes remain disjoint: `gold >= 100` and `gold >= 125` can both be active, but each identity is counted once. Use stable event/class keys, not the current candidate set or enumeration order. Whether to preserve additional ordering across distinct nested/intersecting conditions remains open.

**Candidates — both remain under consideration:**

- **Paused clocks:** reuse [corrected persistent clocks](DESIGN.md#corrected-persistent-selection-priorities) per event. Equivalent conditions give survivors identical updates, preserving their relative order without a separate group draw. Visited events leave selection; no within-act renewal is needed. This retains eligibility-history effects and lets previously ineligible events compete immediately on re-entry, unlike vanilla passing. Matching current sets and ordinals need not reconverge.
- **Condition-level passing:** separate each condition's fixed identity order from its scheduling entries. Pass an inactive condition's scheduling entry without consuming its identity head. Define entry retirement/deferral and later scheduling weights; a finite schedule can lose all of a condition's entries while its identities remain. Simple identity-level passing fails the condition-order requirement. Nested/intersecting conditions must not duplicate identity weight.

**Known — probability tradeoff:** corrected paused clocks remain uniform over current eligible, unvisited identities, assuming eligibility does not inspect hidden clocks; memorylessness makes active survivors' residuals and frozen inactive clocks independent equal-rate exponentials. A class with `n` of `N` eligible survivors has chance `n/N`; an aggregated class clock needs rate `n`. This is not vanilla's invariance to encounters while K is false: in the appendix's two-K/three-other example, retaining all K identities gives `2/4` after one such encounter and `2/3` after two, versus vanilla's `2/5`. Neither is a clock-calculation bias; adopting paused clocks deliberately changes this baseline probability behavior.

**Reference alternative — fresh per-event priorities:** act + ordinary-event ordinal + event ID + purpose gives uniform selection and reconvergence for matching ordinals and current sets, without failed-check history. It does not retain a condition's identity order, so it is not a leading candidate. For candidate sets `S,T`, agreement is `|S ∩ T| / |S ∪ T|`, optimal for nested sets but not every pair; `{A,B}` versus `{A,C}` gives `1/3`, while a pair-specific coupling can reach `1/2`. This does not establish a universally better independent-run coupling.

**Decisions needed:** choose paused clocks or fully specified condition-level passing, with a stated rationale for any change to vanilla's inactive-condition probability property. Define class equivalence and overlap guarantees, scheduling weights/progress, and separate lifetimes for act-specific and cross-act identities/timing. Act-local clock initialization uses act + event ID + purpose; resetting removes prior-act timing history, not visited exclusions. Keeping a cross-act class's identity order while resetting its timing is a separate candidate. Ordinary-event ordinals, if used, count encounters rather than rejected candidates. Ancients remain separate.

**Implementation and validation:** a common offset replaces equal subtraction; pausing needs separate active-time offsets per class. Track identity and scheduling progress separately where the candidate requires them. Leaving raw clocks unchanged across different eligibility histories is not equivalent: with A,C active and B inactive, after A wins, B beats corrected `C-A` with probability `1/2`, but uncorrected C with `2/3`. Check same-condition order, overlapping thresholds, eligibility re-entry, the inactive-K probability example, different visit histories, forced insertion, act changes and save/load.

#### Relic Trader

Owned-relic selection, replacement constraints and offer consumption: [Relic Trader policy](DESIGN.md#settled-policy-relic-trader).

**Decisions needed:** choose replacement stock and input-linked assignment; define collision resolution, ownership/eligibility handling and any unavoidable changes to source isolation.

**Constraints:** only one trade is executed: resolving a proposed target for another outgoing relic does not remove that relic from the player's inventory. All three displayed replacements must be legal alternatives against the actual inventory, not a hypothetical simultaneous three-relic exchange.

**Candidates — graph and assignment:**

- **Successor ring:** construct a stable seeded ring of relic identities and trade each outgoing relic to a successor. If the successor is another selected outgoing relic, resolve that relic's target first; if already owned otherwise, continue around the ring. This is a sketch, not a complete algorithm: specify occupied-target skipping, cycle handling, termination and deterministic resolution independent of offer order. An unweighted identity ring does not implement the standard replacement-rarity weights by itself, and a ring does not solve stock ownership/isolation.
- **Rarity-first input-linked preferences:** key an independent rarity roll and per-rarity replacement preference order to each outgoing identity. Resolve distinct targets in a stable, independently addressed outgoing priority order, skipping claimed or unavailable targets without rerolling rarity merely because of a collision. Keep eligibility separate from preference addresses. Where each input can use every available target of its rolled rarity, feasibility reduces to available count per rarity, not a general graph-matching problem; genuinely input-specific restrictions may require matching.
- **Graph-aware outgoing selection:** jointly choose three outgoing relics and feasible replacement edges. This reopens the settled uniform opportunity-priority selection; it is not adopted. Feasibility or degree-based filtering can bias the outgoing choices, so compare its sampling law as well as counterpart alignment.

**Known — rarity-first sampling:** with fixed eligibility shared by all inputs requesting a rarity, sufficient capacity, independent uniform preference rankings and a processing order independent of those rankings, sequential assignment is uniform without replacement within that rarity. Each input's highest-ranked remaining target is uniform among the unclaimed eligible identities.

**Candidates — stock:** an overlapping Trader-local offer ledger could consume all three local offers while leaving unchosen ordinary entries available, reconciling only the acquired identity with ordinary stock. This changes global retirement semantics and cannot preserve a future ordinary offer of an identity already acquired from Trader. Another possibility is using eligible, unowned identities whose closed ordinary opportunities are already consumed, avoiding future-stock depletion where sufficient; its per-rarity availability and route-dependent replacement support prevent treating it as a complete source solution. No stock model is adopted.

**Validation:** compare overlapping selected sets with identical replacement availability, then force competing targets, adjacent outgoing ring nodes, owned-but-unselected successors, cycles, cutoff-ineligible counterparts and per-rarity shortages. Check outgoing selection probabilities, independent replacement rarity, batch uniqueness/order neutrality, generic coverage and which ordinary source changes after an actual trade versus an unchosen offer.

#### Remaining events and Ancients

Remaining Encounter decisions have not yet been decomposed. Evaluate them against the [event-specific reward policy](DESIGN.md#settled-policy-event-specific-rewards) and existing event-list rules as they are investigated.

### Item-selection priorities

#### Canonical card order

Shared contract and transformation use: [canonical card order](DESIGN.md#canonical-card-order) and [transformation batches](DESIGN.md#selection-and-consumption).

**Decisions needed:** define the usefulness ranking of card identities, upgrade states and enchantments. The rough direction is Curse < Strike < Defend < other Basic < Common; complete and refine this ranking, including how status changes affect placement. Identical cards remain tied under the shared contract.

**Validation:** permute batch enumeration and equivalent physical copies with different hidden RNG state; compare transformation results and subsequent sequence state. Include different original-identity exclusions. Assess combat-shuffle alignment separately under [Shuffles](#shuffles).

#### Equivalent-copy routing

Requirements, multiplicity and UI behavior: [equivalent-card selections](DESIGN.md#equivalent-card-selections). [Fixed-priority selection and reward-choice alignment](DESIGN.md#settled-policy-fixed-priority-card-selection) are settled.

**Decisions needed:**

- Define the gameplay-equivalence status key for each relevant card/effect, and the hidden representative chosen when interchangeable physical copies carry different inherent RNG provenance.
- Choose count coupling: compare equivalent-copy cohorts of two versus three, not only equal-sized decks. Decide how logical tokens enter, leave, split into different status classes and merge again without renumbering surviving tokens.
- Choose operation-purpose sharing and opportunity progress. Separate upgrade/removal/etc. representative streams are the leading direction; which sources of the same operation share progress remains open. For opportunity-indexed variants, decide advancement for singleton or whole-class selections so count differences do not inadvertently shift later addresses.
- Specify hidden-token association after routing an operation. For unordered batches, define snapshot-based assignment to the selected physical cards, including effects producing different results.

**Candidate — representative selection over logical tokens:** represent each logical occurrence with a token bundling its relevant future selection keys, aliases, residual clocks and renewal counters. Give tokens independent representative priorities within each card-identity/operation scope; choose the lowest priority, or the lowest `k` for `k` equivalent selected copies. Reusing [corrected persistent clocks](DESIGN.md#corrected-persistent-selection-priorities) in this separate routing scope would preserve surviving-token order; fresh [opportunity-specific priorities](DESIGN.md#opportunity-specific-selection-priorities) remain an alternative with less persistent state. Route the chosen hidden token(s) to the selected physical card(s), preserving the remaining token multiset. An already randomly selected logical winner needs no second representative draw. Do not choose representatives by the Tooth-return clock or another downstream outcome being aligned.

**Known — count coupling:** for nested logical-token cohorts with shared independent representative priorities, selections agree whenever the larger cohort selects a common token; two-versus-three agreement is `2/3`, the maximal uniform coupling. This is a matched-state result, not a guarantee across different eligibility histories; first establish meaningful common token keys across runs.

**Validation:** swap clicked copies of any identity while holding gameplay status fixed; compare subsequent identity/status outcomes and retained hidden-token multisets without changing which UI card visibly changed. Test multiplicity, two/three-copy cohorts, multi-selection click permutations, class splits/merges, reward-choice priority aliases and save/load.

**Potion routing:** choose the representative-selection mechanism for [equivalent potion actions](DESIGN.md#settled-policy-owned-potion-selection) alongside the card mechanism above.

### Combat randomness

#### Shuffles

**Decision needed:** opening-shuffle addressing and how later shuffles share or advance randomness.

**Planned input ordering:** use [canonical card order](DESIGN.md#canonical-card-order), including upgrade/enchantment distinctions, before shuffling. The shuffle mechanism and its alignment guarantees remain open; a common input order alone does not settle them.

**Candidates:**

- Act-local opening-shuffle ordinals. Together with act-local setup, this would align the first Act 2 normal fight's setup/shuffle addresses despite different Act 1 normal-fight counts; cumulative reward addresses would still differ.
- Add run seed to boss shuffle initialization.

#### Generated cards

**Decision needed:** sharing and addressing by pool/type: own-character Attack/Skill/Power, colorless and other characters; include special-relic interactions.

#### Other combat randomness

**Backlog:** monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations. As each area is investigated, decide sharing by source/effect and special-relic interactions, and split it into its own decision unit only where useful.
