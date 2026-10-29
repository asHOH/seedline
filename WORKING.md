# Deterministic seed mod — working document

Unresolved work and supporting evidence, evaluated against [DESIGN's goals and fairness principle](DESIGN.md#goals-and-principles). [DESIGN.md](DESIGN.md) owns settled policies and reusable mechanics references.

**Reading convention:** **Known** = findings; **Constraints** = requirements beyond DESIGN; **Decisions needed** = open choices; **Candidates** = approaches still under consideration; **Reference alternative** = comparison for an open decision, not a leading candidate; **Validation** = checks informing open choices. [Implementation verification](#implementation-verification) checks conformance to settled policies. Adopted candidates become spec in DESIGN; settled exclusions worth retaining become its **Rejected alternatives**. Omit empty roles and obvious exhaustive alternatives.

---

Use the [Rewards / Encounter categories](DESIGN.md#concepts-and-terms) and [architecture dimensions](DESIGN.md#architecture-overview); distinguish contexts only where rules differ.

**Candidate defaults:** ordinals for Rewards; act-local ordinals for Encounter, subject to each unresolved source's sharing/addressing decision.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

**Out-of-combat status:** all settled. The [Deterministic Card Processing Order](DESIGN.md#deterministic-card-processing-order) is ready for use but may require fine-tuning. Event selection may need a reconsideration.

**Remaining work roadmap:**

1. [Deterministic Card Processing Order](#deterministic-card-processing-order): fine-tune the preliminarily settled ranking.
2. [Combat randomness](#combat-randomness): settle opening/later shuffles and remaining card generation; investigate monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations.

**Stakeholder input:** use answers to [Questions for 二硫键](Questions.md) about perceived Act 1/shop Rare rates and potentially fixable same-seed divergence to inform calibration and remaining investigation.

## Encounter

Ancient combat effects remain in their shared decision units below.

### Item-selection priorities

#### Deterministic Card Processing Order

**Known:** the [Deterministic Card Processing Order](DESIGN.md#deterministic-card-processing-order) is preliminarily settled and ready for use. Fine-tune the rankings later.

### Combat randomness

#### Shuffles

**Decision needed:** opening-shuffle addressing, sharing with later shuffles, and advancement rules.

**Candidate:** act-local opening-shuffle ordinals, matching encounter setup.

**Shared constraints:** [deck-order independence](DESIGN.md#deterministic-card-processing-order) and [run-seed derivation](DESIGN.md#rng-derivation-and-addressing), including boss-shuffle initialization.

**Ancient sources:** Blessed Antler inserts Dazed cards at random draw-pile positions before the opening draw; Biiig Hug inserts Soot after shuffles. Both use the shared `Shuffle` RNG through `CardPileCmd`.

#### Generated cards

**Decision needed:** sharing and addressing for remaining combat card-generation sources by pool/type: own-character Attack/Skill/Power, colorless and other characters; include special-relic interactions. [Potion-generated cards](DESIGN.md#settled-policy-potion-generated-cards) are settled.

**Ancient sources:** Crossbow generates an own-character Attack each turn; Choices Paradox generates five own-character choices on turn one. Both use `CombatCardGeneration`.

#### Other combat randomness

**Backlog:** monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations. During investigation, decide source/effect sharing and special-relic interactions; split decision units only where useful.

**Ancient sources:** Jeweled Mask selects a Power from the opening draw pile with `CombatCardSelection`, preferring non-Innate candidates; Snecko Eye applies Confused's `CombatEnergyCosts` rolls. Whispering Earring's card selection and enemy targeting follow fixed order in solo play; its random ally targeting is multiplayer-only.

## Implementation verification

**Deferred:** check each implemented policy against DESIGN. Use targeted state assertions and paired same-seed runs; log random operations where outcomes hide incorrect advancement.
