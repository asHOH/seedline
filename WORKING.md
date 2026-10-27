# Deterministic seed mod — working document

Unresolved work and supporting evidence, evaluated against [DESIGN's goals and fairness principle](DESIGN.md#goals-and-principles). [DESIGN.md](DESIGN.md) owns settled policies and reusable mechanics references.

**Reading convention:** **Known** = findings; **Constraints** = requirements beyond DESIGN; **Decisions needed** = open choices; **Candidates** = approaches still under consideration; **Reference alternative** = comparison for an open decision, not a leading candidate; **Validation** = checks informing open choices. [Implementation verification](#implementation-verification) checks conformance to settled policies. Adopted candidates become spec in DESIGN; settled exclusions worth retaining become its **Rejected alternatives**. Omit empty roles and obvious exhaustive alternatives.

---

Use the [Rewards / Encounter categories](DESIGN.md#concepts-and-terms) and [architecture dimensions](DESIGN.md#architecture-overview); distinguish contexts only where rules differ.

**Candidate defaults:** ordinals for Rewards; act-local ordinals for Encounter, subject to each unresolved source's sharing/addressing decision.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

**Remaining work roadmap:**

1. [Events and Ancients](#events-and-ancients): settle cross-act event carryover; then decompose remaining events/Ancients as needed.
2. [Item-selection priorities](#item-selection-priorities): complete canonical card ranking.
3. [Combat randomness](#combat-randomness): settle opening/later shuffles and remaining card generation; investigate monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations.

**Stakeholder input:** use answers to [Questions for 二硫键](Questions.md) about perceived Act 1/shop Rare rates and potentially fixable same-seed divergence to inform calibration and remaining investigation.

## Encounter

### Events and Ancients

#### Act-local event selection

**Status:** settled policy, held here temporarily.

**Intent and scope:** improve same-seed event matching within one act. Ancients remain separate.

**State:** a condition is an eligibility predicate, including act availability and excluding [rare checks](#rare-conditions). Equivalent predicates share a stable key; each event belongs to one condition. Initially shuffle unvisited IDs independently per condition using act + condition key + purpose, with one scheduling entry per ID. Follow the shared [addressing, consumption and saved-state contracts](DESIGN.md#addressing-and-state).

**Selection:**

1. At each **act-local event ordinal**, initialize priority `-ln(U) / n_c` for conditions with `n_c > 0` unpassed entries. Independent uniform `U ∈ (0, 1)` draws use act + act-local event ordinal + condition key + per-condition draw index, starting at zero for each resolution.
2. Spend one entry of the lowest-priority condition. If false or lacking unvisited IDs, retain its ID position. Otherwise, try its next unvisited ID; a rare-check failure passes that ID too.
3. After failure, add a fresh `-ln(U) / remaining_count` to that condition's priority, retiring it at zero. Keep other priorities fixed and repeat.
4. On success, advance the ID sequence and commit the visit and all passing. Refresh priorities at the next resolution.

**Wrapping:** when the schedule exhausts, restore passed entries and restart priorities within the same ordinal, continuing each condition's draw index. ID sequences wrap in original order; visited IDs stay excluded. Probability changes after wrapping are accepted.

**Implementation detail — empty eligible pool:** outside this policy's scope; relax act restrictions first, then all eligibility restrictions except visited-event exclusion, then allow repeats; stop at the first stage with a candidate.

**Guarantees:** before either structure wraps, preserve vanilla's distribution when eligibility cannot inspect hidden order. With passing rare checks, same starting state + same selected event leaves the same state. Refreshing order changes cross-run and SL correlations to improve recovery after mismatch; shared priorities handle eligible-set overlap.

##### Rare conditions

**Intent:** keep classification compact. Evaluate rare checks at ID selection, allowing otherwise equivalent events to share one scheduling condition.

| Event | Eligible acts | Rare check | Scheduling condition beyond act availability |
|---|---|---|---|
| Luminous Choir | Overgrowth | Available relic stock | Gold ≥ 149 |
| Morphic Grove | Overgrowth | ≥ 2 transformable cards | Gold ≥ 100 |
| Wood Carvings | Overgrowth | A removable Basic card | None |
| Spiraling Whirlpool | Underdocks | A card eligible for Spiral | None |
| The Legends Were True | Act 1 | A nonempty deck | HP ≥ 10 |
| Field of Man-Sized Holes | Hive | A card eligible for Perfect Fit | None |
| Slippery Bridge | Acts 1–3 | A removable card | Total floor > 6 |

##### Evidence and eliminated candidate

**Validation:** Vanilla-distribution and same-state checks pass. Rare failures and wrapping remain untested; some history pairs worsen.

**Eliminated candidate — flat condition schedule:** fix one shuffled schedule, one entry per initial unvisited ID; pass failures, remove successful entries and wrap in original order. Shares the ID sequences and preserves pre-wrap probabilities. Eliminated for lower average matching in the tested histories.

Baseline: [event selection and wrapping](DESIGN.md#event-and-encounter-identities).

#### Cross-act event carryover

**Decision needed:** determine whether shared-event ID sequences carry across acts; run-wide visited exclusions persist.

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
