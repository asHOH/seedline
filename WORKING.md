# Deterministic seed mod — working document

Settled systems and reusable existing-mechanics references are maintained exclusively in [DESIGN.md](DESIGN.md). This file contains only unresolved work and evidence needed to resolve it, evaluated against the [goals and fairness principle](DESIGN.md#goals).

When a decision settles, update its canonical section in DESIGN.md and delete the resolved discussion here; retain at most a one-line pointer where needed by remaining work.

**Reading convention:** use **Known** for findings, **Constraints** for local requirements beyond DESIGN, **Decisions needed** for unresolved choices, **Candidates** for unadopted approaches, and **Validation** for checks still needed. Omit unused roles. Candidates become specification only when adopted in DESIGN.

## Remaining design: rewards and encounter

Use the [Rewards / Encounter design categories](DESIGN.md#design-categories). Sequence scope and addressing remain separate fairness decisions.

Distinguish normal fights, elites, bosses, Ancients, chests, events and special relic effects only where their rules differ.

**Candidate defaults:** cumulative ordinals for Rewards; act-local ordinals for Encounter, subject to each unresolved source's sharing/addressing decision.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

### Rewards

#### Direct card additions: remaining special sources

See the [settled transformation and direct-addition policy](DESIGN.md#settled-policy-permanent-transformations-and-direct-card-additions).

**Known:** Pael's Tooth randomly returns a previously removed card, upgraded; this selects a stored card rather than generating a new identity.

**Decision needed:** how to control Pael's Tooth's stored-card selection.

#### Cards: offers, rarity and rerolls

Settled source sharing, luck, deferral, rerolls and Brain Leech grouping: [own-character random-rarity offers](DESIGN.md#own-character-random-rarity-offers).

**Decisions needed:**

- Choose two or three offer sequences below, including non-Rare routing for fixed-base-odds sources other than Brain Leech. Balance the elite Uncommon advantage against matching choices when different sources reach the Rare threshold.
- Place the special opening entries under the chosen sharing scheme. Fixed-odds rewards can consume normal non-Rare entries before the first two normal-rule rewards; a fully shared non-Rare sequence can also be advanced by elites. Decide how the opening exception should be addressed without making stored entries depend on the live offset.
- Calibrate Common/Uncommon distributions for each non-Rare sequence, the shared rare-bearing companions and Brain Leech's two event-generated cards. Exact modeling and probabilities are deferred; include varied routes, events, extra rewards and shops, especially Orrery. Do not adopt the illustrative `3 normal → 1 elite → 2 normal → 1 elite` route as the full model or transfer the shop's `0.008` offset adjustment.
- Behavior without Scarcity, including multiple Rares and deferral, remains deferred.
- Reroll addressing and consumption for card-offer groups outside the shared luck system remain unresolved.

**Candidates — both remain strong:**

| Offer sequences | Tradeoff |
|---|---|
| Three: normal non-Rare, elite non-Rare, shared rare-bearing | Retains a richer elite non-Rare distribution, but which source produces a Rare changes both non-Rare consumption and the resulting Uncommon advantage. |
| Two: shared non-Rare, shared rare-bearing | Aligns non-Rare choices across sources, at the cost of the distinct elite non-Rare Uncommon advantage. |

**Known — tradeoff:** with one normal and one elite reward, a Rare in the normal reward consumes a shared Rare offer plus an elite non-Rare offer; a Rare in the elite consumes a normal non-Rare offer plus the same shared Rare offer. Changing the shared Rare companions alone cannot remove this difference.

**Validation:** use automated comparison with vanilla across normal-heavy, elite-heavy and mixed routes, including fixed-odds and extra offers. Compare Rare-bearing offer counts, waiting distributions and conditional Uncommon rates; paired same-seed checks should verify offer alignment, rerolls, pending delivery across acts and Brain Leech crossings in slots four/five. Force rare multi-crossing cases rather than relying on manual runs to encounter them. Determine gameplay impact of Catfood's initial-offer stream bug and direct/event seed collisions; see the baseline above.

### Encounter

#### Events and Ancients

**Decisions needed:**

- **Relic Trader's named Encounter choices:** which owned relics and replacements are offered, their source, and when offered/chosen/unchosen replacements are consumed. See [event rules](DESIGN.md#relic-and-event-rules) and [selection mechanics](DESIGN.md#generation-and-selection-mechanics).
- **Other events/Ancients:** remaining Encounter decisions within the [event-specific reward policy](DESIGN.md#settled-policy-event-specific-rewards) and existing event-list rules.

#### Selection and setup

**Decisions needed:** setup addressing and forced-event handling.

**Candidates:**

- Act-local setup ordinals, distinguishing source/effect where necessary.
- **Lantern Key:** forced Act 3 War Historian Repy gets **ordinal 0**, leaving ordinary events untouched.
- Separate forced-event selection from `?` room-type resolution.

**Validation:** check for visible Catfood setup mismatches; see the baseline above and [vanilla identity selection](DESIGN.md#generation-and-selection-mechanics).

#### Permanent card changes

**Decisions needed:** selection scope, priority reuse and source/effect sharing for permanent upgrades/downgrades.

**Candidate:** extend [inherent card rolls](DESIGN.md#inherent-card-rolls) with other purpose-specific rolls for Encounter selection priorities, such as random upgrades.

#### Combat randomness

**Decisions needed:**

- **Monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations:** sharing by source/effect and special-relic interactions.
- **Shuffles:** opening-shuffle addressing and how later shuffles share or advance randomness.
- **Generated cards:** sharing and addressing by pool/type: own-character Attack/Skill/Power, colorless and other characters; include special-relic interactions.

**Candidate:** act-local opening-shuffle ordinals. Together with act-local setup, this would align the first Act 2 normal fight's setup/shuffle addresses despite different Act 1 normal-fight counts; cumulative reward addresses would still differ.

## Next steps and validation

**Next:** choose and calibrate the non-Rare offer sequences, resolve remaining card-offer exceptions and rerolls, then Encounter design including Relic Trader, other events/Ancients and combat setup.

**Candidate — separate improvement:** add run seed to boss shuffle initialization.

**Validation:** verify save/load preserves counters and outcomes.

**Validation approach:** targeted RNG logs and paired runs; histories alone miss internal calls/actions. Use consistent seed derivation.
