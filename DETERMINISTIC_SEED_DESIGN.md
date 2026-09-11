# Deterministic seed mod — design

## Scope

Version: StS2 v0.111.0 and Catfood Mod v1.1.2.

Compare runs with matching seeds, versions, rules and eligible pools. This context will not be restated hereafter.

**Discussion boundary:** compare random outcomes before predictable modifiers such as eggs, Fresnel Lens and fixed discounts. Preserve these effects and exclude them from all later randomness-control discussions; effects on random selection or sequence consumption remain in scope.

**Exhaustion boundary:** finite relic allocations may borrow on exhaustion. This rare exception applies throughout; exclude it from ordinary isolation discussions.

Base findings on code; use run histories as supporting evidence.

## Goals

**Goal:** fair same-seed competition at equivalent ordinal opportunities. Corresponding shops should match regardless of route, with explicit exceptions below; purchases/restocks should not change later shops. Preserve meaningful choices while removing interference between unrelated systems.

**Fairness principle:** limit the difference one room creates compared with a run without that room. Even consuming the last sequence entry can matter greatly if it contains an exceptionally powerful relic.

## Terms and vanilla baseline

- **Ordinal:** an opportunity's position within its category. Act-local ordinals reset each act; other ordinals continue across acts ("cumulative" is omitted unless needed for clarity).
- **Card reward offer:** cards presented together for one choice, usually three. A **card slot** is one position within an offer.
- **Same-seed divergence (歪种):** huge, usually malicious difference happening early-run or mid-run for the same seed. (permit late-game difference due to players getting different total ordinals for each encounter category; that's benign) This is what we try to avoid, as stated by Fairness principle.
- **Item identity:** the card, relic or potion. A **sequence entry** is one occurrence at its original source position; repeats are distinct entries and removals never renumber survivors.
- **Ordinary relics:** Common / Uncommon / Rare / Shop. Event-exclusive relics sit outside these pools; scripting alone does not imply exclusivity.

**Re-seeding** restarts a repeatable RNG sequence. Outcomes still depend on pools, rarity offsets, counters and other generators.

Vanilla provides the baseline Catfood improves on:

| Mechanism | Baseline and interference |
|---|---|
| Shops and events | Shops use `Shops` RNG for identities/prices and shared `Rewards` for card rarity/upgrades and relic rarity. Events may consume shared rewards/bags despite private RNG. Courier shares these generators; visits/restocks can shift later outcomes and prices. |
| Combat | Encounter setup uses floor-based addressing. Ordinary monster HP uses `Niche` RNG. Shuffle RNG persists across combats, so extra reshuffles can change a later opening hand. |
| Card rarity | Shared offset starts at `-0.05` and resets there after a rare, but not on Ancient entry. Normal/elite non-rare rolls both add `0.01` (`0.005` with Scarcity), capped at `0.40`. Base rare chances are 3%/10% (Scarcity: 1.49%/5%); add the offset. Boss rewards are always Rare; shop rolls read the offset without updating it. |
| Ancient options | Private event RNG uses run seed + event identity hash + player-slot contribution, independently of event counts. Ancient options use this RNG. |
| Chests | Ordinary relic rarity roll + shared-bag identity; gold uses `Rewards`. Silver Crucible suppresses the first chest's relic and gold before either is generated. |
| Relic pools | Rarity: 50% Common / 33% Uncommon / 17% Rare. Shops pull two random-rarity and one Shop relic from the back; rewards/Wongos pull from the front. Five relics are shop-ineligible. Fixed-rarity pulls and scripted non-stackable acquisitions still remove identities; depletion can change delivered rarity. |

## Settled policy: shops, Wongos and Courier

**Discussion boundary:** settled sections are fixed contracts, excluded from unfinished-design discussions.

**Improvements over Catfood (design, not implementation):**

- Isolate normal shop stock from other rewards and Courier consumption, including relic identities and potion generation.
- Stabilize sale slots and occurrence prices instead of letting RNG call order change them.
- Limit Wongos to one shop Rare identity; consume nothing when its featured offer is unaffordable.
- Replace shared relic bags with disjoint source allocations.

### Stock and prices

- Normal stock depends only on shop ordinal, except for Wongos.
- Give shops independent **card, relic-rarity, relic-identity and potion sequences**.
- Sale slot depends only on shop ordinal, independently of identities and other RNG calls.
- Base price is addressed by run seed + source sequence + **original entry position**. It survives slot/shop shifts; repeated identities have distinct addresses. No advance generation is needed.
- **Courier:** separate card, relic-rarity/identity and potion sequences; advance neither normal-stock/generic cursors nor shop ordinal. Apply the same price rule and preserve [original mechanics](#appendix-existing-mechanics).
- **Wongos:** featured offer consumes **one shop Rare identity**, no rarity entry; below 200 gold, consume nothing. Later Rare entries shift, e.g. `[A, B]` → `[B, C]`, limiting the change to one removed offer plus its replacement. Other options use generic rewards below.

## Settled strategy: Relic allocation and exhaustion

Deterministically allocate **disjoint identities within each rarity** to normal shops, Courier restocks and generic rewards, respecting source eligibility. Size allocations for expected demand rather than equal shares.

Fully unlocked: **122 ordinary relics per character**—26 Common, 32 Uncommon, 38 Rare, 26 Shop (115 shared + 7 character-specific). Capacity is per rarity/source.

**Open:** account for increased generic-drop odds of the five shop-ineligible relics.

### Borrowing: rare exception, deferred

On exhaustion, borrow the **last eligible remaining identity** of the requested rarity from a deterministic donor source.

Only finite relic-identity allocations borrow; card/potion sequences and rarity rolls continue indefinitely.

**Deferred:** allocation sizes, donor priority, final fallback, borrowed-entry price/save addresses and sequence implementation.

## Remaining design: rewards and encounter

| Category | Determines | Primary progression |
|---|---|---|
| Rewards | What the player gets | Cumulative ordinals |
| Encounter | What challenge the player faces | Act-local ordinals |

Distinguish fights, elites, bosses, Ancients, chests, events and special relic effects only where their rules differ.

**Catfood addressing:** cumulative fight/elite/boss/shop/event/Ancient counters. Ancients increment both `eventEntered` and `ancientVisited`; options stay independently seeded, while Ancient card overrides use `ancientVisited`. Room reward RNG uses category counts, a normal-fight fallback and event-card overrides. Shop card identities still use `Shops`; relic bags/rarities and potion counts remain shared.

### Rewards

#### Cards: offers, rarity and rerolls

- **Catfood:** combat identities/upgrades use category, count and offer position; rerolls have separate seed offsets but still update the category's rarity state, potentially shifting later offers.
- **Rarity:** separate normal-fight, elite, boss and shop offsets follow baseline growth/reset rules. Ancient entry additionally resets all four to `-0.05`, including Neow.
- **Compensation — proposed:** slightly raise the starting/reset offset to compensate for separation. Amount remains open; separation removes shared growth and shared resets, so assess its net effect.
- **Act alignment — open:** Ancient resets coexist with cumulative rarity RNG addresses. Rare slots and valuable-card groupings can differ; assess whether this causes 歪种 under the fairness principle.
- **Direct/event generation:** room/event count + per-room call index. Seed overlap/static-index concerns remain unverified.

**Unadopted suggestion:** stable direct-generation scopes/indices, if a reproduced issue warrants them.

**Reroll example** (Scarcity): three non-rares raise offset `0` → `0.015`. Next matching fight, draw `0.025` gives Uncommon versus Rare (about 1.5% versus 3%). A rare reroll instead resets the offset. Assess the impact against the 歪种 definition.

#### Relics: source classification and consumption

**Catfood:** shared bags and 400 precomputed rarity rolls couple shops, combat, chests and events. Fixed-rarity requests skip rarity consumption. Roll 401 would fail (code finding, not reproduced); identity depletion uses the appendix fallback.

**Proposed design:**

| Source | Sequence rule | Sources and boundaries |
|---|---|---|
| Hidden random relic rewards | Combat/chests/events share generic rewards regardless of cost. Random rarity advances rarity + identity; fixed rarity advances only identity. | Ranwid, Wongos' Common and Mystery Box. Classify deferred payoffs separately from their granting relic. **Supersedes paid-event isolation.** |
| Shown paid ordinary relics | Wongos settled; Relic Trader sequence/consumption open. | Wongos and Relic Trader only; shops are separate. |

**To settle:** Relic Trader's source and consumption timing; whether declined offers remain consumed; ordering of simultaneous/deferred drops; handling filtered or scripted ordinary identities. Allocation balance remains open; exhaustion stays deferred.

#### Potions

**Catfood:** isolated drop rolls, but shop/free identities share a factory-call counter (possibly several potions per call). Crystal Sphere's fixed-rarity potion uses event RNG.

**Tentative:** combine free combat/event potions; separate paid sources by event. Pending relic policy.

#### Other gains and changes

**Open:** gold, transformations and permanent upgrades/downgrades; define equality and sharing by source/effect.

### Encounter

#### Shops, chests and events

- **Shops:** settled above.
- **Chest policy:** relics use generic rarity + identity; base gold uses run seed + cumulative non-empty chest ordinal to achieve separation from other encounter identities. Silver Crucible's empty first chest advances neither chest ordinal nor relic sequences; the next chest is ordinal 1.
- **Catfood chests:** inherits Silver Crucible's suppression, but has no chest ordinal. Gold still uses `Rewards`, re-seeded with the normal-fight count on chest entry. Relics use shared rarity rolls/bags.
- **Event-exclusive relics:** part of event encounters, already outside ordinary pools; no manual separation needed. Keep event identities/sequences route-independent and preserve choices/costs (Doll Room, Forgotten Soul / Sword of Stone, Trash Heap, Fake Merchant).

#### Selection and setup

- **Catfood:** encounter identity + cumulative category ordinal controls variable composition/setup, not ordinary HP. No visible setup mismatch reproduced.
- **Proposed:** act-local setup ordinals, distinguishing source/effect where necessary.
- **Lantern Key proposal:** forced Act 3 War Historian Repy gets **ordinal 0**, leaving ordinary events untouched; Catfood counts it normally.
- **Unadopted suggestion:** separate forced-event selection from `?` room-type resolution.

#### Combat randomness

- **Catfood:** re-seeds shuffle RNG at combat start; later shuffles continue it.
- **Proposed:** act-local opening-shuffle ordinals. First Act 2 fight then aligns setup/shuffle addresses despite different Act 1 fight counts; cumulative reward addresses still differ.
- **Open:** HP/AI, random targets/costs/orbs, shuffles, temporary upgrades/downgrades and generated cards by pool/type; decide sharing and special-relic interactions.

## Next steps and validation

**Next:** finish ordinary relic reward policy, starting with Relic Trader. Then address the Encounter category: shops are settled, chest policy is above, and event/Ancient and combat setup remain. Potion policy can follow the relic decisions separately.

**Validation suggestion:** targeted RNG logs and paired runs; histories alone miss internal calls/actions. Use consistent seed derivation.

**Separate improvements:** add run seed to boss shuffle initialization; generate relic rarities on demand.

**Pending:** verify save/load preserves counters and outcomes.

## Reference

[Event spreadsheet](references/StS2_Events.xlsx): **57 ordinary/forced events and 8 Ancients**. `is_deterministic` concerns offered choices, not hidden rewards: Punch-Off is true; Wongos is false. Detailed criteria and state restrictions are in the workbook.

## Appendix: existing mechanics

**Identity depletion:** from the requested rarity, search forward along Shop → Common → Uncommon → Rare, then multiplayer fallback; otherwise Circlet. Refresh-enabled bags first refill empty requested rarities. Catfood inherits this shared-bag fallback.

The following are game rules, unrelated to our randomness-control design:

| Source | Rules |
|---|---|
| Wongos | Act 2; all players have ≥100 gold. Featured Rare: 200 gold, consumed when offered even if unaffordable/declined. Hidden Common: 100 gold, generated on purchase. Both require `IsAllowedInShops`; preserve that filter. Mystery Ticket pays three random relics after five combats. Leaving downgrades a random upgraded card, if any. |
| Relic Trader | Acts 2–3; all players own ≥5 tradable relics. Shows three randomly selected owned relics and three replacements; consumes all three replacement identities/rarities before selection. Normally no leave. Replacement rarity is independent and replacements need not be tradable. Tradable excludes Starter/Event/Ancient, upon-pickup, used-up, melted and pet-spawning relics. |
| Courier | 20% discount; removal never restocks. Restocked character cards retain type, colorless cards retain rarity; sale clears. Relics roll ordinary rarity even in the Shop slot. Shop ordinal does not advance. |
| Silver Crucible | Neow relic; first treasure room is empty. Suppresses both base gold and relic generation. |
