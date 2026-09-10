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
- **Fixed-rarity relics:** vanilla and Catfood both let Wongos take the next eligible relic from the requested rarity's bucket without consuming a random-rarity entry. This shifts later identities, not normal rarity probabilities; bucket exhaustion/fallback is the exception. The proposed policy below adopts this rarity/identity separation for hidden random rewards; shown paid offers remain undecided.
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
- Potions — earlier proposal, pending adaptation after relics: combine **free** potions from combat drops and events; separate paid sources by event and keep shop potions separate. The newer relic policy below supersedes applying that earlier paid/free split to relics.

### Relic policy — your current proposal

1. **Scripted relics / special pools:** make each event's identities or special-pool sequence deterministic for the same seed, independently of how the event was reached. For example, Doll Room in Act 2 must retain the same doll sequence across paths; Forgotten Soul and Sword of Stone already have fixed identities. Selecting these options consumes neither the generic relic-rarity sequence nor generic relic-identity sequences. This includes scripted shortlists such as Trash Heap even though their members have ordinary rarities.
2. **Hidden random relic rewards:** use one shared generic reward domain, including combat rewards and event rewards, regardless of cost or explicit rarity. With unspecified rarity, advance the shared rarity sequence and that rarity's identity sequence. With fixed rarity, advance only that rarity's identity sequence. This replaces the earlier proposal to isolate every paid event source. Classify deferred random payoffs separately from the scripted relic that grants them.
3. **Ordinary-pool relics shown before commitment and obtained at a cost:** strategy still undecided. You confirmed that ordinary-pool means Common / Uncommon / Rare / Shop, excluding Event and Ancient gifts.

The complete matching event list in the inspected build is:

| Event | Restriction | Shown offer and cost | Current generation timing |
|---|---|---|---|
| Welcome to Wongos | Act 2; every player has at least 100 gold | Featured Rare relic, named before purchase, for 200 gold; leave/other purchases available | Pulls and removes the featured identity on option generation, even if declined. The 100-gold hidden Common and deferred Mystery Box rewards instead belong to policy 2. |
| Relic Trader | Acts 2–3; every player has at least 5 tradable relics | Three named exchanges; sacrifice the displayed owned relic for the displayed new ordinary relic | Generates and removes all three new identities before choosing. Normally no leave option; other trades avoid that particular exchange. |

Fake Merchant offers named paid counterfeits, but those are scripted Event relics and belong to policy 1. Ranwid displays the surrendered item, not the new relic, so belongs to policy 2. Ordinary shop inventory remains a separate shop system, not an additional event in this list.

**Non-goal improvements:**

- Include the run seed in boss shuffle initialization
- Minor performance improvement: Use on-demand relic-rarity rolls instead of the finite 400-entry list

**Still undecided:** handling shown paid ordinary relics; how to reconcile vanilla rare-card accumulation with category isolation and act-boundary alignment; whether/how to improve direct card generation after the requested explanation.

**Current step:** settle policy 3 before adapting relic handling to potions. [Event spreadsheet](outputs/01a086ff-8c1c-7ff0-bda5-09f2bacf39c2/StS2_Events.xlsx) replaces the earlier eight-category inventory and covers 57 ordinary/forced events plus 8 Ancients. `is_deterministic` describes offered event choices across runs, not hidden reward outcomes: Punch-Off is true; Wongos is false. The sheet counts randomly quoted amounts and later event menus as offer variation, while recording player-state locks separately.

## 3. Agent suggestions — not yet adopted

- Explicitly cover **scripted/forced events** and **unknown-room resolution** in the encounter skeleton. A forced event is selected by a specific condition rather than ordinary random selection, such as Lantern Key's War Historian Repy. Unknown-room resolution means deciding what a `?` map node becomes; choosing its room type is distinct from choosing the particular event or fight inside it. These are separate concerns, not one “forced/unknown” room category.
- For direct card generation, consider saved/local indices or stable slots and separate meaningful card scopes. Reproduce a useful failure case before expanding this design.
- Consider consistent seed derivation to avoid accidental overlaps. These are engineering suggestions, not adopted fairness rules.
- Use targeted logging and repeated-run comparisons to validate changes. Histories show offerings and choices, but cannot reveal every RNG call or combat action.
- Implementation detail to resolve under the new scripted-pool policy: vanilla `RelicCmd.Obtain` removes an acquired non-stackable ordinary identity from grab bags even when a special pool supplied it. Preserve the intended ownership/duplicate rule without silently advancing the generic sequence; these are distinct questions.

**Decisions needed next:** the domain and reservation/consumption rule for shown paid ordinary relics (generation, offering, or acquisition); ownership/duplicate handling across scripted and generic pools; which in-room systems should share randomness.
