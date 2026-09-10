# Deterministic seed mod — discussion summary

## Scope and goals

Findings apply to **StS2 v0.111.0 and Catfood v1.1.2**. Compare outcomes with matching seeds, versions, rules and eligible pools. Base findings on code; use run histories as supporting evidence.

**Goal:** fair same-seed competition at equivalent ordinal opportunities. First shop matches first shop; skipping a shop may legitimately shift later shops. Preserve meaningful choice consequences while removing accidental interference between unrelated systems.

**Fairness principle:** prevent one room from shifting subsequent gameplay too much. Consuming the last item of a sequence can matter greatly when that relic is exceptionally powerful.

**Confirmed by you:** Catfood handles save/load correctly, without double-counting or outcome differences. No SL fix is needed.

Status labels below distinguish inspected behavior, your adopted decisions, your proposals, and agent suggestions that have not been adopted. **Adopted policies are not yet implemented.**

## RNG and opportunity addressing

RNG calls advance a repeatable seeded sequence; different call histories can produce different outcomes from the same starting seed. **Re-seeding** restarts a generator from a derived seed, making an opportunity's randomness independent of earlier calls.

### Current behavior

- **Counters:** Catfood saves separate, act-cumulative counts for normal fights, elites, bosses, shops, events and ancients.
- **Room rewards:** Catfood re-seeds reward RNG by category/count, including shop-based addressing. This does not isolate every shop generator; details appear below.
- **Events:** vanilla events have private RNG, but some choices also consume shared reward RNG or the relic grab bag.
- **Lantern Key:** the forced War Historian Repy event advances Catfood's ordinary event count; there is no explicit exception.
- **Encounter setup:** Catfood seeds private encounter RNG from encounter identity and cumulative category count, replacing vanilla's floor-based addressing. This controls some monster compositions and initial configurations, **not ordinary HP**, which uses `Niche` RNG. Cumulative counts can change setup seeds across acts, but no visible setup mismatch was demonstrated; many encounters have fixed composition.
- **Shuffles:** vanilla shuffle RNG persists across combats, so extra reshuffles can change a later fight's opening shuffle. Catfood re-seeds at combat initialization; later shuffles continue within that combat's sequence.

### Your proposals

- Use **act-local ordinals** for encounter setup and opening shuffles; keep reward ordinals cumulative. After 5 versus 6 Act 1 fights, the first Act 2 fight should have equivalent setup/shuffle addressing while rewards differ. Different decks can still produce different hands.
- Count Lantern Key's forced Act 3 event as **ordinal 0**, leaving the ordinary event sequence untouched.

## Cards

### Current behavior and unresolved issues

- **Combat choices:** Catfood uses explicit RNG based on category, count and reward position, with separate offsets for rerolls.
- **Direct/event generation:** Catfood uses a room/event count plus a per-room call index. Seed arithmetic can overlap; the static call index needs investigation and is **not a reproduced gameplay bug**.
- **Rarity:** Catfood maintains separate offsets for normal fights, elites, bosses and shops. They start at `0` instead of vanilla's `-0.05`, allowing an earlier rare. **Your inference, not confirmed design intent:** this compensates for slower accumulation after separating categories.
- **Act alignment:** ancient entry resets rarity offsets while reward counts remain cumulative. Different prior-act fight counts can therefore place rare results into different card-choice sets. Rare-card accumulation versus category/act alignment remains open.

**Agent suggestion — not adopted:** consider saved/local indices or stable slots and separate meaningful card scopes for direct generation. Reproduce a useful failure case before expanding the design.

## Relics

### Current generation and pool mechanics

- Catfood precomputes **400 relic rarities** in one sequence shared by shops, combat drops and events. Fixed-rarity requests bypass it; identities still come from vanilla's shared grab bags. Extra generation can shift other sources' rewards.
- Ordinary rarity rolls are **50% Common / 33% Uncommon / 17% Rare**. Fixed-rarity pulls shift identities without consuming rarity entries; depletion/fallback can change the delivered rarity.
- Normal shops generate two rolled-rarity relics and one fixed Shop relic, pulling identities from the **back** of the bag. Rewards and Wongos pull from the **front**. Five ordinary relics are shop-ineligible.
- Even scripted ordinary-relic acquisitions remove identities from shared bags.
- The inspected fully unlocked pool contains **122 ordinary relics per character**: 26 Common, 32 Uncommon, 38 Rare and 26 Shop (115 shared + 7 character-specific). Capacity must be considered per rarity and eligible source, not only in total.

### Your current reward classification proposal

| Class | Proposed sequence/consumption rule | Examples and boundaries |
|---|---|---|
| Scripted relics / special pools | Same-seed identities/sequences are independent of how the event was reached; consume neither generic rarity nor identity sequences. | Doll Room's Act 2 sequence, fixed Forgotten Soul / Sword of Stone, Trash Heap's scripted shortlist, Fake Merchant's Event counterfeits. |
| Hidden random relic rewards | Combat and event rewards share one generic sequence system regardless of cost. Unspecified rarity advances rarity and the selected rarity's identity sequence; fixed rarity advances only its identity sequence. | Ranwid's hidden replacements, Wongos' hidden Common and deferred Mystery Box rewards. Classify deferred random payoffs separately from their scripted granting relic. **Supersedes the earlier paid-event isolation proposal.** |
| Shown paid ordinary relics | Wongos follows the adopted shop policy below; Relic Trader remains unresolved. | Only Welcome to Wongos and Relic Trader qualify. As you confirmed, “ordinary” means Common / Uncommon / Rare / Shop, excluding Event and Ancient gifts. Shops remain a separate system. |

### Event mechanics that constrain the policy

| Event | Eligibility and offers | Current consumption and other consequences |
|---|---|---|
| Welcome to Wongos | Act 2; every player has at least 100 gold. Featured Rare identity is shown before purchase for 200 gold; leaving and other purchases are available. Hidden Common costs 100 gold. | Featured identity is removed on option generation, even if unaffordable or declined. Hidden Common is selected only on purchase. Mystery Ticket's three random relics are generated after five combats. Leaving downgrades a random upgraded card, if one exists. |
| Relic Trader | Acts 2–3; every player owns at least five tradable relics. Randomly selects three owned relics and shows three named exchanges; only one is exchanged. Normally no leave option, though another trade avoids a particular exchange. | Generates and removes all three replacement identities and consumes three rarity entries before selection. Replacement rarity is independent of the surrendered relic's rarity; replacements need not be tradable. |

Relic Trader's tradable filter excludes Starter/Event/Ancient rarities, upon-pickup effects, used-up relics, melted relics and pet-spawning relics.

### Adopted allocation direction

Deterministically allocate **disjoint identities within each rarity** to normal shops, Courier restocks and generic drops/rewards, respecting source eligibility. Size allocations for expected demand; equal shares are not required. The inspected pool size supports this direction.

On eligible-allocation exhaustion, allow deterministic borrowing from another source, removing the borrowed identity from the donor sequence to prevent duplicates. Prefer isolation in normal play; accept limited interference on exhaustion to preserve availability.

**Open:** allocation sizes, borrowing order, sequence addressing and interactions with scripted acquisitions.

**Agent suggestion — not adopted:** resolve ownership/duplicates across pools. Vanilla `RelicCmd.Obtain` removes non-stackable ordinary identities from grab bags even when supplied by a special pool. Decide how to preserve that ownership rule without advancing the generic sequence.

## Shops, Wongos and Courier

### Current behavior

- Vanilla uses `Shops` RNG for identities/prices, but shared `Rewards` RNG for card rarity, upgrades and relic rarity. Merely entering a shop can change later combat rewards.
- Catfood's room reward re-seeding and separate card-rarity offsets provide only partial isolation. Merchant card identities still use vanilla's `Shops` RNG; relic rarities/identity bags and the potion-generation counter remain shared across sources.
- Courier restocks have no separate sequences. They advance existing shop RNG and shared reward generators, without incrementing the shop ordinal.
- Prices follow RNG call order rather than item identity. Extra restocks or shifted items can change later prices; matching shop ordinals alone does not guarantee matching stock/prices.

### Adopted policy

- Give shops independent **card, relic-rarity, relic-identity and potion sequences**.
- Wongos' featured offer consumes **one shop Rare identity on entry**, but no shop rarity entry or shop ordinal. Its hidden rewards use the generic hidden-reward policy above. This leaves a one-position offset in the Rare sequence: overlapping offers retain identities/prices, but replacements may recur across later shops.
- Give each item a **deterministic base price** derived from the run seed and stable item-sequence entry, independently of other shop RNG. Preserve it across slot/shop shifts under equivalent pricing modifiers; apply current discounts afterward. No advance generation is required.
- Use **separate Courier restock sequences**, analogous to card-reward rerolls, for cards, relic rarities/identities and potions. Restocks advance neither normal shop-stock nor generic reward sequences, and do not advance the shop ordinal. Apply the same deterministic-base-price rule.

Preserve Courier's normal behavior: **20% discount**; card removal does not restock; character cards retain slot type; colorless cards retain designated rarity; sale status is cleared. Relic restocks roll ordinary rarity even in the initially Shop-rarity slot.

The high-level policy is settled; implementation still needs sequence addressing and the allocation/borrowing and scripted-acquisition details listed above. Relic Trader's handling remains unresolved.

## Potions

**Current behavior:** Catfood separates drop rolls from other reward RNG. Identities use a different, global factory-call counter shared by shops and free sources; one call can generate multiple potions. Extra generation can shift another source's rewards. Crystal Sphere's fixed-rarity potion bypasses Catfood's sequence and uses event RNG.

**Your tentative proposal, pending relic handling:** combine **free combat/event potions**, separate paid sources by event, and keep shop potions separate. Shop/Courier isolation is already part of the adopted policy above.

## Remaining work and scope

**Next decision:** settle Relic Trader's sequence and consumption timing—generation, offering or acquisition—then adapt relic handling to potions.

Beyond the specific open issues above, define what must remain equal and what may differ across:

| Area | Systems to cover |
|---|---|
| Encounters | Fights, elites, events, shops, ancients, bosses. |
| Rewards | Cards (fight/elite/boss drops, various event drops, rerolls), potions, relics, gold, transformations. |
| In-encounter randomness | Upgrades/downgrades, shop prices/sales, monster HP/AI, random targets/costs/orbs, first/later shuffles, generated cards by pool/type (own-character Attack/Skill/Power, colorless, other characters). Which systems share randomness remains open. |

### Agent suggestions — not adopted

- Treat **forced events** (condition-selected, such as Lantern Key's Repy) and **unknown-room resolution** (what room type a `?` becomes before selecting its event/fight) as separate encounter concerns.
- Use consistent seed derivation to avoid accidental overlaps.
- Validate with targeted logging and repeated-run comparisons. Histories show offerings and choices but cannot reveal every RNG call or combat action.

### Non-goal improvements

- Include the currently omitted run seed in ordinary boss-room shuffle initialization.
- Replace the finite 400-entry relic-rarity list with on-demand rolls for a minor performance improvement.

## Reference

[Event spreadsheet](outputs/01a086ff-8c1c-7ff0-bda5-09f2bacf39c2/StS2_Events.xlsx): **57 ordinary/forced events and 8 Ancients**. `is_deterministic` concerns offered choices, not hidden rewards: Punch-Off is true; Wongos is false. Detailed criteria and state restrictions are in the workbook.
