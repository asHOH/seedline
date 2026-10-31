# Deterministic seed mod — working document

Unresolved work and supporting evidence, evaluated against [DESIGN's goals and fairness principle](DESIGN.md#goals-and-principles). [DESIGN.md](DESIGN.md) owns settled policies and reusable mechanics references.

**Reading convention:** **Known** = findings; **Constraints** = requirements beyond DESIGN; **Decisions needed** = open choices; **Candidates** = approaches still under consideration; **Reference alternative** = comparison for an open decision, not a leading candidate; **Validation** = checks informing open choices. [Implementation verification](#implementation-verification) checks conformance to settled policies. Adopted candidates become spec in DESIGN; settled exclusions worth retaining become its **Rejected alternatives**. Omit empty roles and obvious exhaustive alternatives.

---

Use the [Rewards / Encounter categories](DESIGN.md#concepts-and-terms) and [architecture dimensions](DESIGN.md#architecture-overview); distinguish contexts only where rules differ.

**Candidate defaults:** ordinals for Rewards; act-local ordinals for Encounter, subject to each unresolved source's sharing/addressing decision.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

**Out-of-combat status:** policies are settled.

**Stakeholder input — 二硫键:** perceived slightly more combat Rares in Catfood, especially early in Act 1, against expected code behavior; considered this acceptable because both competitors were affected. Shop Rare rates were unobserved.

## Deterministic Card Processing Order

The [card-processing ranking](CARD_ORDER.md) is preliminarily settled. Fine-tuning remains.

## Combat randomness

### Monster AI: rat non-Summon strategy

**Settled boundary:** [DESIGN's rat sampling contract](DESIGN.md#rat-sampling) owns Summon and the ordinary sampler's probability/SL constraints. Only ordinary-move sharing and advancement remain open.

**Candidate:** one continuous combat-local percentile sequence shared by all rats, including summons. Advance when an ordinary move is selected from at least two eligible moves; Summon and forced ordinary outcomes retain its position.

**Alternative:** round-local ordinary sequences limit earlier-round consumption interference, but a different birth round can change a summoned rat's opening intent.

**Decision needed:** choose the ordinary sequence scope and advancement. Continuous sharing preserves consumption-based SL alternatives; round sharing prioritizes later realignment.

### Other combat randomness

Open topics; develop each into a decision unit as choices and evidence emerge:

- [ ] Opening shuffle
- [ ] Later shuffles
- [ ] Random pile insertion
- [ ] Generated cards
- [ ] Combat transformations
- [ ] Random card selection
- [ ] Random creature targets
- [ ] Random cost values
- [ ] Random orb types

## Implementation verification

**Deferred:** check each implemented policy against DESIGN. Use targeted state assertions and paired same-seed runs; log random operations where outcomes hide incorrect advancement.
