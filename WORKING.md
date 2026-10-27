# Deterministic seed mod — working document

Unresolved work and supporting evidence, evaluated against [DESIGN's goals and fairness principle](DESIGN.md#goals-and-principles). [DESIGN.md](DESIGN.md) owns settled policies and reusable mechanics references.

**Reading convention:** **Known** = findings; **Constraints** = requirements beyond DESIGN; **Decisions needed** = open choices; **Candidates** = approaches still under consideration; **Reference alternative** = comparison for an open decision, not a leading candidate; **Validation** = checks informing open choices. [Implementation verification](#implementation-verification) checks conformance to settled policies. Adopted candidates become spec in DESIGN; settled exclusions worth retaining become its **Rejected alternatives**. Omit empty roles and obvious exhaustive alternatives.

---

Use the [Rewards / Encounter categories](DESIGN.md#concepts-and-terms) and [architecture dimensions](DESIGN.md#architecture-overview); distinguish contexts only where rules differ.

**Candidate defaults:** ordinals for Rewards; act-local ordinals for Encounter, subject to each unresolved source's sharing/addressing decision.

Baseline: [Catfood addressing and combat rewards](DESIGN.md#catfood-addressing-and-combat-rewards).

**Remaining work roadmap:**

1. [Events and Ancients](#events-and-ancients): finish family assignment and cross-act carryover from the one-act event baseline; then decompose remaining events/Ancients as needed.
2. [Item-selection priorities](#item-selection-priorities): complete canonical card ranking.
3. [Combat randomness](#combat-randomness): settle opening/later shuffles and remaining card generation; investigate monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations.

**Stakeholder input:** use answers to [Questions for 二硫键](Questions.md) about perceived Act 1/shop Rare rates and potentially fixable same-seed divergence to inform calibration and remaining investigation.

## Encounter

### Events and Ancients

#### Ordinary event identity

**Status:** agreed one-act baseline, held here at the user's request pending promotion to DESIGN.

**Intent and constraints:** align ordinary events across same-seed eligibility histories. Keep the act-specific plus shared pool, live eligibility, run-wide visited exclusions, [forced-event bypass](DESIGN.md#settled-policy-forced-events) and event-internal RNG. Different actual visits remain meaningful exclusions.

**Boundary:** a fixed family hierarchy within one act, with eligible, unvisited supply. Empty eligible pools and singleton sets remain outside this decision; Ancients remain separate.

**Conditions and families:** a condition is the eligibility predicate used for grouping and scheduling, including act availability and excluding the designated [rare conditions](#rare-conditions); equivalent predicates share one key. A family groups conditions or smaller families; it is available when a contained condition is true and has unvisited event IDs. Each event belongs to one condition and one path through the hierarchy, so overlapping predicates never duplicate its weight. Use stable event, condition and family keys, independently of current candidates and enumeration order.

| Structure | Key / contents |
|---|---|
| **Family schedule** | Act; entries name top-level families. |
| **Condition schedule** | Family; entries name its conditions or child families. |
| **Event ID sequence** | Condition; entries are its unvisited event IDs. |

**Passing:** advance past an entry while retaining it for wrapping.

**Initialization:** for a fresh one-act instance, independently and uniformly shuffle each structure. Each schedule contains one entry per initial unvisited event inside the named condition/family; repeated labels are distinct entries. Each event ID sequence contains each identity once. Use act + structure key + purpose under the shared [addressing and state contracts](DESIGN.md#addressing-and-state); those contracts also govern commitment and save/load.

**Selection:**

1. Examine the next family-schedule entry. Pass an unavailable family, retaining every sequence position inside it, and continue the family schedule.
2. For an available family, examine exactly one condition-schedule entry. An available child family repeats this step. A failed condition or child family passes the examined scheduling entries and resumes the top-level family schedule; retain all positions below the failed entry.
3. An available condition attempts its next event ID, checking that event's rare conditions. On failure, pass the ID and every examined scheduling entry, then resume the top-level family schedule. Rejected candidates add no visit or ordinary-event opportunity and consume no event RNG.
4. On successful selection, advance its event ID sequence and permanently remove the successful entry at each schedule level. Commit all passing with the selected event under the shared state contract.

**Wrapping:** each schedule and event ID sequence loops through its remaining entries in original order when its end is reached; visited IDs remain excluded. Probability changes after wrapping are accepted to keep event selection running with remaining eligible identities.

**Behavior preservation:** before any schedule or event ID sequence wraps, independent uniform hidden schedules preserve vanilla's single-run selection distribution, assuming eligibility never inspects their hidden order. A uniform shuffle decomposes into independent family interleaving and internal orders; unseen internal positions can be deferred. This preserves [vanilla's inactive-condition probability property](DESIGN.md#event-and-encounter-identities). Alignment changes cross-run and SL correlations; eligibility-history differences can persist.

**Evidence:** before wrapping, six-event enumeration matched vanilla across 81 threshold histories, 64 intersecting-condition histories with a fixed hierarchy, and adaptive examples.

##### Rare conditions

**Scope and intent:** solo ordinary runs with permanent unlocks satisfied. A rare failure may defer a recovered identity behind later IDs; this alignment tradeoff keeps classification compact.

| Event | Eligible acts | Rare check | Scheduling condition beyond act availability |
|---|---|---|---|
| Luminous Choir | Overgrowth | Available relic stock | Gold ≥ 149 |
| Morphic Grove | Overgrowth | ≥ 2 transformable cards | Gold ≥ 100 |
| Wood Carvings | Overgrowth | A removable Basic card | None |
| Spiraling Whirlpool | Underdocks | A card eligible for Spiral | None |
| The Legends Were True | Act 1 | A nonempty deck | HP ≥ 10 |
| Field of Man-Sized Holes | Hive | A card eligible for Perfect Fit | None |
| Slippery Bridge | Acts 1–3 | A removable card | Total floor > 6 |

**Validation:** before wrapping, exact enumeration matched vanilla across 196 two-opportunity histories and 720 initial orders, using three two-event conditions, a nested family and one event-specific rare check; unresolved ends were included. A recovery check retained a rejected ID for wrapping while excluding it from visits and opportunity counts. Retrying within the same condition instead changed one three-event example's selection chance from `1/2` to `2/3`.

##### Remaining decisions

- **Family assignment:** map actual conditions into the hierarchy, including which grouping receives protection when predicates intersect without nesting.
- **Cross-act carryover:** choose lifetimes separately for the family schedule, condition schedules and event ID sequences. One candidate retains shared-event ID order while resetting schedules; run-wide visited exclusions persist.

##### Selection refinements

**Candidate — markers:** apply the shared [filtered-draw strategy](DESIGN.md#filtered-draws) to a single mixed condition schedule, freezing the currently eligible condition keys in each marker; retain event ID sequences by condition. This may preserve future selected-condition order within wholly unavailable groups, including crossing groups. Judge future selections, including pending marker resolution. Schedule consumption changes; general guarantees, marker collisions and finite-capacity behavior need assessment before adoption.

**Preliminary evidence:** across three two-event conditions, all 343 histories of three nonempty eligible-condition sets matched vanilla over 720 permutations each, including each model's unresolved ends as terminal outcomes. Marker wrapping remains open.

**Reference alternatives:** [Paused clocks](DESIGN.md#corrected-persistent-selection-priorities) keep every eligible, unvisited identity competing uniformly; the two-K/three-other example changes from vanilla's `2/5` to `2/4` and `2/3` after one and two encounters with K unavailable. Fresh per-event priorities restore agreement for matching ordinals/current sets but lose condition identity order.

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
