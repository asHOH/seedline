# Deterministic seed mod — summary

## Context and prerequisites

- Findings apply to the inspected **StS2 v0.111.0 and Catfood v1.1.2**.
- Compare outcomes with matching seeds, versions, rules, and eligible pools.
- Save/load must preserve outcomes. **You confirm that Catfood already does this correctly:** SL does not double-count entry or introduce differences; no SL fix is needed.

## 1. Established

### Vanilla and RNG basics

RNG calls advance a repeatable seeded sequence. Different call histories can therefore produce different outcomes despite identical starting seeds. **Re-seeding** restarts a generator from a derived seed, making an opportunity's randomness independent of earlier calls.

- Shops use `Shops` RNG for identities/prices, but shared `Rewards` RNG for card rarity, upgrades, and relic rarity. Merely entering a shop can therefore change later combat rewards.
- Events have private RNG, but some choices also consume shared reward RNG or the relic grab bag. Taking another relic can shift later relics; whether this is unfair depends on the intended rules.
- Shuffle RNG persists across combats, so extra reshuffles can affect a later fight's opening shuffle.

### What Catfood does

| System | Mechanism and effect |
|---|---|
| Opportunity counters | Saves separate counts for normal fights, elites, bosses, shops, events, and ancients. These remain cumulative across acts. |
| Rewards and shops | Re-seeds room reward RNG by category/count, limiting cross-category interference. Keeps the existing shop-only sequence: first shop, second shop, etc. |
| Card choices | Gives combat rewards explicit RNG based on category, count, and reward position; rerolls use separate offsets. Direct/event card generation uses a room/event count plus a per-room call index. |
| Card rarity | Maintains separate rarity offsets for normal fights, elites, bosses, and shops, isolating their progression. |
| Encounter setup | Seeds private encounter RNG using encounter identity and cumulative category count, replacing vanilla's floor-based addressing. |
| Opening shuffle | Re-seeds at combat initialization, isolating it from prior combat reshuffles. Later shuffles continue within that combat's sequence. |
| Potions | Separates drop rolls from other reward RNG. Potion identities use a different, global factory-call counter shared by shop and free sources; one call can generate multiple potions. |
| Relics | Precomputes 400 relic **rarities**. Identities still come from the vanilla grab bag; fixed-rarity requests can bypass the rarity sequence. |

### Remaining issues and corrections

- **Rare cards:** offsets start at `0` rather than vanilla's `-0.05`, allowing an earlier rare. Ancient entry resets offsets while reward counts stay cumulative; different prior-act fight counts can then place rare results into different card-choice sets. Fixing initialization alone does not settle progression policy.
- **Your inference (not a confirmed design intention):** starting at `0` compensates for slower rare-card accumulation after separating categories.
- **Lantern Key:** the forced War Historian Repy event advances the ordinary event count; Catfood has no explicit exception.
- **Potions:** shop generation shifts later free-potion identities. Crystal Sphere's fixed-rarity potion bypasses Catfood's sequence and uses event RNG.
- **Fixed-rarity relics:** Wongos takes the next eligible relic of the requested rarity without consuming a random-rarity entry. Whether to retain this policy is undecided.
- **Encounter correction:** private RNG controls some monster compositions and initial configurations, **not ordinary HP**, which uses `Niche` RNG. Cumulative counts can alter setup seeds across acts, but no visible setup mismatch was demonstrated. Many encounters have fixed composition.
- **Other concerns:** ordinary boss-room shuffle initialization omits the run seed. Direct-card seed arithmetic can overlap; its static call index needs investigation, not classification as a reproduced gameplay bug. The earlier SL concern is withdrawn following your confirmation above.

## 2. Your goals and proposals

**Goal:** fair same-seed competition at equivalent ordinal opportunities. First shop matches first shop; skipping a shop may legitimately shift later shops.

Preserve meaningful consequences of choices while removing accidental interference between unrelated systems. Start by understanding vanilla and Catfood code; run-history comparisons are supporting evidence.

Organize invariants into:

1. **Encounters:** fights, elites, events, shops, ancients, bosses.
2. **Rewards:** cards (fight drop, elite drop, boss drop, various event drop, reroll), potions, relics, gold, transformations.
3. **In-encounter randomness:** upgrades/downgrades, shop prices/sales, monster HP/AI, random targets/costs/orbs, first and later shuffles, generated cards by pool/type (own-character Attack/Skill/Power, colorless, other characters).

Each subpart should eventually have an explicit invariant describing what must remain equal and what may legitimately differ.

Your proposed changes:

- Use **act-local ordinals** for encounter setup and opening shuffles; keep reward ordinals cumulative. After 5 versus 6 Act 1 fights, the first Act 2 fight should have equivalent setup/shuffle addressing, while rewards differ. Different decks can still produce different hands.
- Count Lantern Key's forced Act 3 event as **ordinal 0**, leaving the ordinary event sequence untouched. The key-holder's next ordinary event should align with the non-key-holder's first.
- Potions: Combine **free** potions from combat drops and events; keep separate any potion that requires something in exchange for each individual event; keep separate shop potions. Same idea for relics.

**Non-goal improvements:**

- Include the run seed in boss shuffle initialization
- Minor performance improvement: Use on-demand relic-rarity rolls instead of the finite 400-entry list

**Still undecided:** for fixed-rarity rewards, whether to take the next eligible item of that rarity (doing so will shift expected rarity chance of later drops); how to reconcile vanilla rare-card accumulation with category isolation and act-boundary alignment; whether/how to improve direct card generation after the requested explanation.

## 3. Agent suggestions — not yet adopted

- Explicitly cover **scripted/forced events** and **unknown-room resolution** in the encounter skeleton. A forced event is selected by a specific condition rather than ordinary random selection, such as Lantern Key's War Historian Repy. Unknown-room resolution means deciding what a `?` map node becomes; choosing its room type is distinct from choosing the particular event or fight inside it. These are separate concerns, not one “forced/unknown” room category.
- For direct card generation, consider saved/local indices or stable slots and separate meaningful card scopes. Reproduce a useful failure case before expanding this design.
- Consider consistent seed derivation to avoid accidental overlaps. These are engineering suggestions, not adopted fairness rules.
- Use targeted logging and repeated-run comparisons to validate changes. Histories show offerings and choices, but cannot reveal every RNG call or combat action.

**Decisions needed next:** rarity policy; what advances each sequence (generation, offering, or acquisition); which in-room systems should share randomness.
