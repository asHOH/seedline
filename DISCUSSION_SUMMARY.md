# Deterministic seed mod — summary

## Context and prerequisites

- Findings apply to the inspected **StS2 v0.111.0 and Catfood v1.1.2**.
- Compare outcomes with matching seeds, versions, rules, and eligible pools.
- **You confirm that Catfood handles save/load correctly:** no double-counting or outcome differences; no SL fix is needed.

## 1. Established

### Vanilla and RNG basics

RNG calls advance a repeatable seeded sequence. Different call histories can therefore produce different outcomes despite identical starting seeds. **Re-seeding** restarts a generator from a derived seed, making an opportunity's randomness independent of earlier calls.

- Shops use `Shops` RNG for identities/prices, but shared `Rewards` RNG for card rarity, upgrades, and relic rarity. Merely entering a shop can therefore change later combat rewards.
- Events have private RNG, but some choices also consume shared reward RNG or the relic grab bag.
- Shuffle RNG persists across combats, so extra reshuffles can affect a later fight's opening shuffle.

### What Catfood does

| System | Mechanism and effect |
|---|---|
| Opportunity counters | Saves separate counts for normal fights, elites, bosses, shops, events, and ancients. These remain cumulative across acts. |
| Rewards and shops | Re-seeds room reward RNG by category/count, including a separate shop sequence. |
| Card choices | Gives combat rewards explicit RNG based on category, count, and reward position; rerolls use separate offsets. Direct/event card generation uses a room/event count plus a per-room call index. |
| Card rarity | Maintains separate rarity offsets for normal fights, elites, bosses, and shops, isolating their progression. |
| Encounter setup | Seeds private encounter RNG using encounter identity and cumulative category count, replacing vanilla's floor-based addressing. |
| Opening shuffle | Re-seeds at combat initialization, isolating it from prior combat reshuffles. Later shuffles continue within that combat's sequence. |
| Potions | Separates drop rolls from other reward RNG. Potion identities use a different, global factory-call counter shared by shop and free sources; one call can generate multiple potions. |
| Relics | Precomputes 400 relic **rarities**. Identities still come from the vanilla grab bag; fixed-rarity requests can bypass the rarity sequence. |

### Remaining issues and corrections

- **Rare cards:** offsets start at `0` rather than vanilla's `-0.05`, allowing an earlier rare. **Your inference, not confirmed design intent:** this compensates for slower accumulation after separating categories. Ancient entry resets offsets while reward counts stay cumulative, so different prior-act fight counts can place rare results into different card-choice sets.
- **Lantern Key:** the forced War Historian Repy event advances the ordinary event count; Catfood has no explicit exception.
- **Potions:** shop generation shifts later free-potion identities. Crystal Sphere's fixed-rarity potion bypasses Catfood's sequence and uses event RNG.
- **Fixed-rarity relics:** vanilla and Catfood pull the next eligible identity from the requested rarity's bucket without consuming a rarity roll. This shifts later identities, not normal rarity probabilities; bucket exhaustion/fallback is the exception.
- **Encounter correction:** private RNG controls some monster compositions and initial configurations, **not ordinary HP**, which uses `Niche` RNG. Cumulative counts can alter setup seeds across acts, but no visible setup mismatch was demonstrated. Many encounters have fixed composition.
- **Direct cards:** seed arithmetic can overlap; the static call index needs investigation, not classification as a reproduced gameplay bug.

## 2. Your goals and proposals

**Goal:** fair same-seed competition at equivalent ordinal opportunities. First shop matches first shop; skipping a shop may legitimately shift later shops.

Preserve meaningful choice consequences while removing accidental interference between unrelated systems. Base findings on code, with run histories as supporting evidence.

Define what must remain equal and what may differ within:

1. **Encounters:** fights, elites, events, shops, ancients, bosses.
2. **Rewards:** cards (fight drop, elite drop, boss drop, various event drop, reroll), potions, relics, gold, transformations.
3. **In-encounter randomness:** upgrades/downgrades, shop prices/sales, monster HP/AI, random targets/costs/orbs, first and later shuffles, generated cards by pool/type (own-character Attack/Skill/Power, colorless, other characters).

Your proposed changes:

- Use **act-local ordinals** for encounter setup and opening shuffles; keep reward ordinals cumulative. After 5 versus 6 Act 1 fights, the first Act 2 fight should have equivalent setup/shuffle addressing, while rewards differ. Different decks can still produce different hands.
- Count Lantern Key's forced Act 3 event as **ordinal 0**, leaving the ordinary event sequence untouched.
- Potions — tentative until relic handling is settled: combine **free** combat/event potions; separate paid sources by event and keep shop potions separate.

### Relic policy — your current proposal

1. **Scripted relics / special pools:** same-seed identities/sequences must be independent of how the event was reached, consuming neither generic rarity nor identity sequences. Examples: Doll Room's Act 2 sequence, fixed Forgotten Soul / Sword of Stone, and Trash Heap's shortlist of ordinary relics.
2. **Hidden random relic rewards:** combat and event rewards share one generic sequence system regardless of cost. Unspecified rarity advances the rarity sequence and the selected rarity's identity sequence; fixed rarity advances only its identity sequence. This supersedes the earlier paid-event isolation proposal. Classify deferred random payoffs separately from their scripted granting relic.
3. **Shown paid ordinary relics:** unresolved. Ordinary-pool means Common / Uncommon / Rare / Shop, excluding Event and Ancient gifts, as you confirmed.

Only two events match policy 3:

| Event | Restriction | Shown offer and cost | Current generation timing |
|---|---|---|---|
| Welcome to Wongos | Act 2; every player has at least 100 gold | Featured Rare relic, named before purchase, for 200 gold; leave/other purchases available | Pulls and removes the featured identity on option generation, even if declined. The 100-gold hidden Common and deferred Mystery Box rewards instead belong to policy 2. |
| Relic Trader | Acts 2–3; every player has at least 5 tradable relics | Three named exchanges; sacrifice the displayed owned relic for the displayed new ordinary relic | Generates and removes all three new identities before choosing. Normally no leave option; other trades avoid that particular exchange. |

Fake Merchant's Event counterfeits belong to policy 1; Ranwid's hidden replacement relics belong to policy 2. Shops remain a separate system.

**Non-goal improvements:**

- Include the currently omitted run seed in ordinary boss-room shuffle initialization
- Minor performance improvement: Use on-demand relic-rarity rolls instead of the finite 400-entry list

**Current step:** settle policy 3's sequence and consumption timing (generation, offering, or acquisition), then adapt relic handling to potions. Other open decisions: rare-card accumulation versus category/act alignment, direct-card generation, and which in-room systems share randomness.

**Reference:** [Event spreadsheet](outputs/01a086ff-8c1c-7ff0-bda5-09f2bacf39c2/StS2_Events.xlsx) — 57 ordinary/forced events and 8 Ancients. `is_deterministic` concerns offered choices, not hidden rewards: Punch-Off is true; Wongos is false. Detailed criteria and state restrictions are in the workbook.

## 3. Agent suggestions — not yet adopted

- Cover **forced events** (condition-selected, e.g. Lantern Key's Repy) and **unknown-room resolution** (what room type a `?` becomes, before selecting its event/fight) as separate encounter concerns.
- For direct card generation, consider saved/local indices or stable slots and separate meaningful card scopes. Reproduce a useful failure case before expanding this design.
- Use consistent seed derivation to avoid accidental overlaps.
- Use targeted logging and repeated-run comparisons to validate changes. Histories show offerings and choices, but cannot reveal every RNG call or combat action.
- Resolve ownership/duplicates across relic pools: vanilla `RelicCmd.Obtain` removes non-stackable ordinary identities from grab bags even when supplied by a special pool. Decide how to preserve that ownership rule without advancing the generic sequence.
