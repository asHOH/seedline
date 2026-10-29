# Deterministic seed mod — working document

Unresolved work and supporting evidence, evaluated against [DESIGN's goals and fairness principle](DESIGN.md#goals-and-principles). [DESIGN.md](DESIGN.md) owns settled policies and reusable mechanics references.

**Reading convention:** **Known** = findings; **Constraints** = requirements beyond DESIGN; **Decisions needed** = open choices; **Candidates** = approaches still under consideration; **Reference alternative** = comparison for an open decision, not a leading candidate; **Validation** = checks informing open choices. [Implementation verification](#implementation-verification) checks conformance to settled policies. Adopted candidates become spec in DESIGN; settled exclusions worth retaining become its **Rejected alternatives**. Omit empty roles and obvious exhaustive alternatives.

---

Use the [Rewards / Encounter categories](DESIGN.md#concepts-and-terms) and [architecture dimensions](DESIGN.md#architecture-overview); distinguish contexts only where rules differ.

**Candidate defaults:** ordinals for Rewards; act-local ordinals for Encounter, subject to each unresolved source's sharing/addressing decision.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

**Out-of-combat status:** policies are settled. The [Deterministic Card Processing Order](DESIGN.md#deterministic-card-processing-order) is ready for use but may require fine-tuning.

**Remaining work roadmap:**

1. [Deterministic Card Processing Order](#deterministic-card-processing-order): fine-tune the preliminarily settled ranking.
2. [Combat randomness](#combat-randomness).

**Stakeholder input — 二硫键:** perceived slightly more combat Rares in Catfood, especially early in Act 1, against expected code behavior; considered this acceptable because both competitors were affected. Shop Rare rates were unobserved.

## Encounter

### Item-selection priorities

#### Deterministic Card Processing Order

**Known:** the [Deterministic Card Processing Order](DESIGN.md#deterministic-card-processing-order) is preliminarily settled and ready for use. Fine-tune the rankings later.

### Combat randomness

#### Opening shuffle

#### Later shuffles

#### Random pile insertion

#### Monster AI

#### Generated cards

#### Combat transformations

#### Random card selection

#### Random creature targets

#### Random cost values

#### Random orb types

## Implementation verification

**Deferred:** check each implemented policy against DESIGN. Use targeted state assertions and paired same-seed runs; log random operations where outcomes hide incorrect advancement.

**Monster HP:** grade SL impact; verify priority sampling, spawn ordering and Tough Egg's hatch.
