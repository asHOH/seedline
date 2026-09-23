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

Existing contracts: [inherent card rolls](DESIGN.md#inherent-card-rolls) and [card reward offers, including grouping](DESIGN.md#settled-policy-card-reward-offers).

**Known — deferred own-character groups:**

| Rarity rule | Sources / offer structure |
|---|---|
| Normal-fight rarity | Normal fights, Dream Catcher, Prayer Wheel and The Hunt in normal fights; three-card offers. |
| Elite rarity | Elites and The Hunt in elite combats; three-card offers. |
| Base Common/Uncommon/Rare odds | Orrery: five three-card offers; Lost Coffer: one; Brain Leech's Share Knowledge: choose one of five; Trial's Nondescript Guilty: two three-card offers. |

The base-odds group's vanilla odds are 61.51%/37%/1.49%, without combat rarity offsets; Share Knowledge and Trial suppress random upgrades.

**Decisions needed:**

- Sharing/addressing for these deferred groups, rarity offsets, and reroll addressing/consumption across card offers.
- Whether to retain Catfood's combat rarity resets on Ancient entry, and which reset values to use.
- Act alignment to prevent divergence across acts; assess alongside the offset-reset decision.

**Candidates:**

- **Base-odds group:** handle it separately using a strategy similar to normal-fight rewards.
- **Elite initialization:** on Ancient entry, including Neow, set the elite offset to `-0.02` (`0.01` without Scarcity), approximating two normal fights. Retain vanilla non-rare growth.

**Validation:** determine gameplay impact of Catfood's initial-offer stream bug and direct/event seed collisions; see the baseline above.

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

**Next:** address card rerolls and the deferred reward groups, then Encounter design including Relic Trader, other events/Ancients and combat setup.

**Candidate — separate improvement:** add run seed to boss shuffle initialization.

**Validation:** verify save/load preserves counters and outcomes.

**Validation approach:** targeted RNG logs and paired runs; histories alone miss internal calls/actions. Use consistent seed derivation.
