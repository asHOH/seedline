# Deterministic seed mod — design

## Scope

Version: StS2 v0.111.0 and Catfood Mod v1.1.2.

Compare runs with matching seeds, versions, rules and eligible pools. This context will not be restated hereafter.

Base findings on code; use run histories as supporting evidence.

## Goals

**Goal:** fair same-seed competition at equivalent ordinal opportunities. The first visited shop should be designed to be identical in every respect regardless of how the player reaches it, and likewise for later shop ordinals (Rare intentional exceptions exist). Activities inside it do not change later shops. Preserve meaningful choice consequences while removing accidental interference between unrelated systems.

**Fairness principle:** limit the difference one room creates compared with a run without that room. Even consuming the last sequence entry can matter greatly if it contains an exceptionally powerful relic.

## Terms and vanilla baseline

- **Ordinal:** an opportunity's position within its category. Act-local ordinals reset each act; other ordinals continue across acts ("cumulative" is omitted unless needed for clarity).
- **Card reward offer:** cards presented together for one choice, usually three. A **card slot** is one position within an offer.
- **Item identity:** which card, relic or potion an entry contains. A **sequence entry** is one occurrence, identified by its original position in its source sequence. Repeated occurrences of one identity are distinct entries; removals do not renumber remaining entries.
- **Ordinary relics:** Common / Uncommon / Rare / Shop, excluding Event and Ancient gifts. Event-exclusive relics are obtainable only through their associated events, outside generic ordinary-relic pools; scripted acquisition alone does not imply exclusivity.

RNG calls advance a repeatable seeded sequence. **Re-seeding** restarts a generator from a derived seed, removing dependence on its earlier calls. Outcomes can still depend on pools, rarity offsets, addressing counters and other generators.

Vanilla provides the baseline Catfood improves on:

| Mechanism | Baseline and interference |
|---|---|
| Shops and events | Shops use `Shops` RNG for identities/prices and shared `Rewards` RNG for card rarity, upgrades and relic rarity: entering a shop can change later combat rewards. Events have private RNG, but some choices also consume shared reward RNG or relic bags. Courier restocks share existing generators; prices follow call order, so restocks or shifted items can change later stock/prices. |
| Combat and cards | Encounter setup uses floor-based addressing. Ordinary monster HP uses `Niche` RNG. Shuffle RNG persists across combats, so extra reshuffles can change a later opening hand. Card-rarity offsets start at `-0.05`. |
| Relic pools | Ordinary rarity rolls are 50% Common / 33% Uncommon / 17% Rare. Shops pull two rolled-rarity relics and one fixed Shop relic from the back of the bag; rewards and Wongos pull from the front. Five ordinary relics are shop-ineligible. Fixed-rarity pulls still remove identities; depletion/fallback can change delivered rarity. `RelicCmd.Obtain` removes non-stackable ordinary identities even for scripted/special-pool acquisitions. |

## Settled design: shops, Wongos and Courier

Treat this section as a fixed contract for subsequent design work. Relic Trader event remains outside it; Wongos' hidden rewards belong to generic rewards below.

### Stock and prices

- **Goal:** without Wongos consumption, normal shop stock is determined only by the shop ordinal. No other relic, event, prior purchase or restock may change that stock.
- Give shops independent **card, relic-rarity, relic-identity and potion sequences**.
- The card slot marked on sale is determined only by the shop ordinal (e.g., derive it from run seed + shop ordinal), independently of item identities and other RNG calls.
- Give each **item occurrence** a deterministic base price derived from the run seed, source sequence and entry's **original position**, independently of other shop RNG. Repeated identities have distinct price addresses; an existing entry keeps its base price across slot/shop shifts. No advance generation is required.
- Give **Courier restocks separate sequences** for cards, relic rarities/identities and potions, analogous to Catfood card-reward rerolls. They advance neither normal shop-stock nor generic reward cursors, and do not advance the shop ordinal. Use the same base-price rule and preserve Courier's original mechanics in the [appendix](#appendix-existing-mechanics).
- Wongos' featured offer consumes only **one shop Rare identity**, but no shop rarity entry. Subsequent shops may effectively differ by at most one rare relic offer compared with a non-Wongos run (positions can shift). For example, if the subsequent shop originally have rare relics `[A, B]`, they become `[B, C]` after Wongos consumes A. When entering Wongos with less than 200 gold, the featured offer is unavailable and thus does not consume anything.

### Relic allocation and exhaustion

Deterministically allocate **disjoint identities within each rarity** to normal shops, Courier restocks and generic rewards, respecting source eligibility. Size allocations for expected demand rather than equal shares.

Fully unlocked pool has **122 ordinary relics per character**: 26 Common, 32 Uncommon, 38 Rare and 26 Shop (115 shared + 7 character-specific). This supports the direction, but capacity must be considered per rarity and eligible source.

#### Borrowing: rare exception, deferred

Borrowing applies only to **finite relic-identity allocations**. Card and potion sequences may repeat identities and can continue indefinitely; they do not borrow. Relic-rarity rolls can likewise continue without borrowing identities.

When a source has no eligible identity left for the requested rarity, borrow deterministically from another source's allocation of that rarity. Take and remove the **last eligible identity from the donor's remaining sequence**. This is the accepted exception to isolation, preserving availability on exhaustion.

**Discussion boundary:** treat exhaustion as rare and exclude it from subsequent normal-case design discussions and comparisons unless explicitly discussing borrowing.

**Decide later:** allocation sizes, donor-source priority, behavior when no donor has an eligible identity (including rarity fallback versus a terminal fallback), and how borrowed entries retain their source/position for pricing and saved state. Sequence addressing also remains an implementation detail. The high-level isolation policy is settled.

## Remaining design: rewards and encounter

| Class | Determines | Primary progression |
|---|---|---|
| Rewards | What the player gets | Cumulative ordinals |
| Encounter | What challenge the player faces | Act-local ordinals |

Organize each class by mechanism, then distinguish normal fights, elites, bosses, events, shops, ancients and **special relic effects** where their rules differ. These are sources or modifiers, not competing top-level classes. Progression is the default direction, with explicit exceptions where needed. The settled shop contract above is not reopened here.

**Catfood addressing baseline:** saves separate counts across acts for normal fights, elites, bosses, shops, events and ancients; re-seeds room reward RNG by category/count, including shops. Its shop isolation is partial: merchant card identities still use `Shops`, while relic bags/rarities and the potion-generation counter remain shared across sources.

### Rewards

#### Cards: offers, rarity and rerolls

- **Catfood:** combat offers use explicit RNG based on category, count and reward position; rerolls use separate offsets. Keep special relic effects such as **card reward rerolls** in this scope, distinguishing sources where needed.
- **Rarity:** separate offsets for normal fights, elites, bosses and shops start at `0`, allowing an earlier rare than the baseline. **Your inference, not confirmed intent:** this compensates for slower accumulation after separating categories.
- **Act alignment — open:** ancient entry resets rarity offsets while reward counts continue. The same act-local rare slot can therefore fall in different cumulative offers. Example: A starts at offers 1, 2, 3; B at 2, 3, 4. A rare in the act's seventh card slot lands in each player's third offer—cumulative offer 3 for A, 4 for B. If another powerful card shares one of those offers, one player must choose between it and the rare while the other can obtain both. Resolve rare-card accumulation versus category/act alignment.
- **Direct/event generation:** Catfood uses a room/event count plus a per-room call index. Seed arithmetic can overlap; the static index needs investigation, **not classification as a reproduced gameplay bug**.

**Agent suggestion — not adopted:** consider saved/local indices or stable slots and meaningful card scopes for direct generation. Reproduce a useful failure case before expanding the design.

#### Relics: source classification and consumption

**Catfood:** precomputes 400 rarities shared by shops, combat drops and events; fixed-rarity requests bypass this sequence. Identities still use shared bags, so extra generation can shift other sources' rewards. It has no source-allocation borrowing. Its rarity patch reads and removes entry 0 without an empty-list guard; after 400 rolls, the next patched roll would fail rather than borrow or replenish (code finding, not a reproduced run). Identity depletion retains the baseline fallback described in the appendix.

**Proposed design:**

| Class | Sequence rule | Sources and boundaries |
|---|---|---|
| Event-exclusive relics | Same-seed identities/sequences are independent of how the event was reached; neither advance nor remove entries from generic rarity/identity sequences. | Doll Room's Act 2 sequence, fixed Forgotten Soul / Sword of Stone, Trash Heap's scripted shortlist, Fake Merchant's Event counterfeits. Exclusivity, not scripting, defines this class. |
| Hidden random relic rewards | Combat and event rewards share one generic system regardless of cost. Unspecified rarity advances rarity and the selected rarity's identity sequence; fixed rarity advances only identity. | Ranwid's hidden replacements, Wongos' hidden Common and deferred Mystery Box rewards. Classify special relics' deferred random payoffs separately from the granting relic. **Supersedes paid-event isolation.** |
| Shown paid ordinary relics | Wongos is settled above; Relic Trader's sequence and consumption timing remain open. | Only Wongos and Relic Trader qualify. Shops have their separate settled system. |

#### Potions

**Catfood:** separates drop rolls from other reward RNG. Identities use a global factory-call counter shared by shops and free sources; one call can generate multiple potions, shifting other sources' rewards. Crystal Sphere's fixed-rarity potion bypasses Catfood's sequence and uses event RNG.

**Your tentative proposal, pending relic handling:** combine free combat/event potions and separate paid sources by event. Shop/Courier isolation is already settled.

#### Other gains and changes

Cover gold, transformations and permanent upgrades/downgrades, distinguishing room sources and special relic effects where necessary. Their detailed equality and sequence-sharing rules remain to be specified; shop prices/sales are covered by the settled contract.

### Encounter

#### Selection and setup

- **Catfood:** private encounter RNG uses encounter identity and category ordinal. It controls some monster compositions and initial configurations, not ordinary HP. Counts across acts can change setup seeds, but no visible mismatch was demonstrated; many encounters have fixed composition.
- **Your proposal:** use act-local ordinals for setup. Cover normal fights, elites, bosses, events, shops and ancients where their selection/setup rules differ, plus special relic effects that modify the challenge.
- **Lantern Key:** Catfood counts forced War Historian Repy as an ordinary event. Your proposal assigns this forced Act 3 event **ordinal 0**, leaving the ordinary event sequence untouched.
- **Agent suggestion — not adopted:** treat forced-event selection and unknown-room resolution (what room type a `?` becomes before selecting its event/fight) as separate concerns.

#### Combat randomness

- **Catfood:** re-seeds opening shuffle RNG at combat initialization; later shuffles continue within that combat's sequence.
- **Your proposal:** use act-local ordinals for opening shuffles as well as setup. After different Act 1 fight counts, the first Act 2 fight has equivalent setup/shuffle addressing but different reward addresses, overwhelmingly likely to yield different rewards. Different decks can still produce different hands.
- **Scope still open:** monster HP/AI, random targets/costs/orbs, first/later shuffles, temporary upgrades/downgrades and generated combat cards by pool/type (own-character Attack/Skill/Power, colorless, other characters). Include special relic effects where applicable; determine which mechanisms share randomness.

## Next steps and validation

**Next decision:** settle Relic Trader's sequence and consumption timing—generation, offering or acquisition—then adapt relic handling to potions. Other open decisions are recorded under their mechanisms above.

**Agent suggestions — not adopted:** use consistent seed derivation to avoid overlaps; validate with targeted logging and repeated-run comparisons. Histories show offerings and choices but cannot reveal every RNG call or combat action.

**Non-goal improvements:** include the currently omitted run seed in ordinary boss-room shuffle initialization; replace the finite 400-entry relic-rarity list with on-demand rolls for a minor performance improvement.

**Verification pending:** catfood should already handle save/load correctly, without double-counting or outcome differences. (This needs further check to see whether SL fix is needed.)

## Reference

[Event spreadsheet](references/StS2_Events.xlsx): **57 ordinary/forced events and 8 Ancients**. `is_deterministic` concerns offered choices, not hidden rewards: Punch-Off is true; Wongos is false. Detailed criteria and state restrictions are in the workbook.

## Appendix: existing mechanics

**Identity depletion inherited by Catfood:** `RelicGrabBag.GetAvailableDeque` searches from the requested rarity along Shop → Common → Uncommon → Rare, then tries the multiplayer fallback bag; factory pulls return Circlet if no eligible identity remains. Bags explicitly configured to refresh can first refill an empty requested rarity. This is vanilla fallback within shared bags, not our planned borrowing between source allocations.

The following existing mechanics constrain the design; preserve them unless a policy explicitly changes their sequence use:

| Source | Rules |
|---|---|
| Wongos | Act 2; every player has at least 100 gold. Featured Rare costs 200 gold and is removed on option generation, even if unaffordable or declined; leaving/other purchases are available. Hidden Common costs 100 gold and is selected only on purchase. Mystery Ticket generates three random relics after five combats (the deferred Mystery Box rewards). Leaving downgrades a random upgraded card, if one exists. |
| Relic Trader | Acts 2–3; every player owns at least five tradable relics. Randomly selects three owned relics and shows three exchanges; generates/removes all three replacement identities and consumes three rarity entries before one trade is chosen. Normally no leave option. Replacement rarity is independent of surrendered rarity; replacements need not be tradable. Tradable excludes Starter/Event/Ancient rarities, upon-pickup effects, used-up, melted and pet-spawning relics. |
| Courier | 20% discount; card removal does not restock; character cards retain slot type; colorless cards retain designated rarity; sale status is cleared. Relic restocks roll ordinary rarity even in the initially Shop-rarity slot. Restocks do not advance the shop ordinal. |
