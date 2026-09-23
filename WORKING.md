# Deterministic seed mod — working document

Settled systems and reusable existing-mechanics references are maintained exclusively in [DESIGN.md](DESIGN.md). This file contains only unresolved work and evidence needed to resolve it, evaluated against the [goals and fairness principle](DESIGN.md#goals).

When a decision settles, update its canonical section in DESIGN.md and delete the resolved discussion here; retain at most a one-line pointer where needed by remaining work.

## Remaining design: rewards and encounter

Use the [Rewards / Encounter design categories](DESIGN.md#design-categories). Rewards defaults to cumulative ordinals; Encounter defaults to act-local ordinals. Sequence scope and addressing remain separate fairness decisions.

Distinguish normal fights, elites, bosses, Ancients, chests, events and special relic effects only where their rules differ.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

### Rewards

#### Direct card additions: remaining special sources

See the [settled transformation and direct-addition policy](DESIGN.md#settled-policy-permanent-transformations-and-direct-card-additions). Remaining cases:

- **Pael's Tooth — deferred:** randomly returns a previously removed card, upgraded; this selects a stored card rather than generating a new identity.

#### Cards: offers, rarity and rerolls

Existing contracts: [inherent card rolls](DESIGN.md#inherent-card-rolls) and [card reward offers, including grouping](DESIGN.md#settled-policy-card-reward-offers).

**Deferred own-character groups:**

| Rarity rule | Sources / offer structure |
|---|---|
| Normal-fight rarity | Normal fights, Dream Catcher, Prayer Wheel and The Hunt in normal fights; three-card offers. |
| Elite rarity | Elites and The Hunt in elite combats; three-card offers. |
| Base Common/Uncommon/Rare odds | Orrery: five three-card offers; Lost Coffer: one; Brain Leech's Share Knowledge: choose one of five; Trial's Nondescript Guilty: two three-card offers. |

**Candidate:** handle the base-odds group separately using a strategy similar to normal-fight rewards. Relevant evidence: its vanilla odds are 61.51%/37%/1.49%, without combat rarity offsets; Share Knowledge and Trial suppress random upgrades.

**Open:** sharing/addressing for these deferred groups, rarity offsets and resets, act alignment, and reroll addressing/consumption across card offers.

**Proposals and evidence needed:**

- **Pending validation:** determine gameplay impact of Catfood's initial-offer stream bug and direct/event seed collisions; see the baseline above.
- **Elite initialization — proposed:** on Ancient entry, including Neow, set the elite offset to `-0.02` (`0.01` without Scarcity), approximating two normal fights. Retain vanilla non-rare growth.
- **Offset resets — open:** whether to preserve combat rarity resets on Ancient entry. The elite Ancient-entry reset value has the separate proposal above.
- **Act alignment — open:** prevent divergence across acts; assess alongside the offset-reset decision.

### Encounter

#### Events and Ancients

- **Relic Trader — deferred:** named choices belong to Encounter. Decide which owned relics and replacements are offered, their source, and when offered/chosen/unchosen replacements are consumed. See [event rules](DESIGN.md#relic-and-event-rules) and [selection mechanics](DESIGN.md#generation-and-selection-mechanics).
- **Other events/Ancients — open:** resolve remaining Encounter decisions within the [event-specific reward policy](DESIGN.md#settled-policy-event-specific-rewards) and existing event-list rules.

#### Selection and setup

- **Pending validation:** check for visible Catfood setup mismatches; see the baseline above and [vanilla identity selection](DESIGN.md#generation-and-selection-mechanics).
- **Proposed:** act-local setup ordinals, distinguishing source/effect where necessary.
- **Lantern Key proposal:** forced Act 3 War Historian Repy gets **ordinal 0**, leaving ordinary events untouched.
- **Unadopted suggestion:** separate forced-event selection from `?` room-type resolution.

#### Permanent card changes

**Planned:** extend [inherent card rolls](DESIGN.md#inherent-card-rolls) with other purpose-specific rolls for Encounter selection priorities, such as random upgrades. **Open:** selection scope, priority reuse and source/effect sharing for permanent upgrades/downgrades.

#### Combat randomness

- **Proposed:** act-local opening-shuffle ordinals. First Act 2 normal fight then aligns setup/shuffle addresses despite different Act 1 normal-fight counts; cumulative reward addresses still differ.
- **Open:** monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations; decide sharing by source/effect and special-relic interactions.
- **Shuffles — open:** settle opening-shuffle addressing and how later shuffles share or advance randomness.
- **Generated cards — open:** decide sharing and addressing by pool/type: own-character Attack/Skill/Power, colorless and other characters; include special-relic interactions.

## Next steps and validation

**Next:** address card rerolls and the deferred reward groups, then Encounter design including Relic Trader, other events/Ancients and combat setup.

**Validation suggestion:** targeted RNG logs and paired runs; histories alone miss internal calls/actions. Use consistent seed derivation.

**Separate improvement:** add run seed to boss shuffle initialization.

**Pending:** verify save/load preserves counters and outcomes.
