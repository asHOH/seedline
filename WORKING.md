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

### Combat-card equivalence and state

**Decisions needed — deferred:** extend equivalent-copy handling to combat, including state changes and their effects on [shuffle assignment](DESIGN.md#settled-policy-card-shuffling). Consider alongside random card selection.

### Shuffle behavior assessment

**Validation:** assess the [settled shuffle policy](DESIGN.md#settled-policy-card-shuffling)'s changed correlations and NoSL/SL consequences against [behavior preservation](DESIGN.md#goals-and-principles); grade distinct deviations separately.

### Other combat randomness

Open topics; develop each into a decision unit as choices and evidence emerge:

- [ ] Random pile insertion
- [ ] Generated cards
- [ ] Combat transformations
- [ ] Random card selection
- [ ] Random creature targets
- [ ] Random cost values

## Implementation verification

**Deferred:** check each implemented policy against DESIGN. Use targeted state assertions and paired same-seed runs; log random operations where outcomes hide incorrect advancement.
