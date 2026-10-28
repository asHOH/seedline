# Deterministic seed mod — working document

Unresolved work and supporting evidence, evaluated against [DESIGN's goals and fairness principle](DESIGN.md#goals-and-principles). [DESIGN.md](DESIGN.md) owns settled policies and reusable mechanics references.

**Reading convention:** **Known** = findings; **Constraints** = requirements beyond DESIGN; **Decisions needed** = open choices; **Candidates** = approaches still under consideration; **Reference alternative** = comparison for an open decision, not a leading candidate; **Validation** = checks informing open choices. [Implementation verification](#implementation-verification) checks conformance to settled policies. Adopted candidates become spec in DESIGN; settled exclusions worth retaining become its **Rejected alternatives**. Omit empty roles and obvious exhaustive alternatives.

---

Use the [Rewards / Encounter categories](DESIGN.md#concepts-and-terms) and [architecture dimensions](DESIGN.md#architecture-overview); distinguish contexts only where rules differ.

**Candidate defaults:** ordinals for Rewards; act-local ordinals for Encounter, subject to each unresolved source's sharing/addressing decision.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

**Remaining work roadmap:**

1. [Events and Ancients](#events-and-ancients): decompose remaining events/Ancients as needed.
2. [Item-selection priorities](#item-selection-priorities): complete canonical card ranking.
3. [Combat randomness](#combat-randomness): settle opening/later shuffles and remaining card generation; investigate monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations.

**Stakeholder input:** use answers to [Questions for 二硫键](Questions.md) about perceived Act 1/shop Rare rates and potentially fixable same-seed divergence to inform calibration and remaining investigation.

## Encounter

### Events and Ancients

#### Remaining events and Ancients

Decompose remaining Encounter random operations as investigated, evaluating them against the [event-specific reward policy](DESIGN.md#settled-policy-event-specific-rewards) and existing event-list rules.

### Item-selection priorities

#### Canonical card order

**Decisions needed:** complete the [canonical ranking](DESIGN.md#canonical-card-order) for identities, upgrades, enchantments and persistent progress. Starting point: Curse < Strike < Defend < other Basic < Common.

### Combat randomness

#### Shuffles

**Decision needed:** opening-shuffle addressing, sharing with later shuffles, and advancement rules.

**Candidate:** act-local opening-shuffle ordinals, matching encounter setup.

**Shared constraints:** [deck-order independence](DESIGN.md#canonical-card-order) and [run-seed derivation](DESIGN.md#rng-derivation-and-addressing), including boss-shuffle initialization.

#### Generated cards

**Decision needed:** sharing and addressing for remaining combat card-generation sources by pool/type: own-character Attack/Skill/Power, colorless and other characters; include special-relic interactions. [Potion-generated cards](DESIGN.md#settled-policy-potion-generated-cards) are settled.

#### Other combat randomness

**Backlog:** monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations. During investigation, decide source/effect sharing and special-relic interactions; split decision units only where useful.

## Implementation verification

**Deferred:** check each implemented policy against DESIGN. Use targeted state assertions and paired same-seed runs; log random operations where outcomes hide incorrect advancement.
