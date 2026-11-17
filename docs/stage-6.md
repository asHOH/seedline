# Stage 6: combat policies

Implements the design's [combat policies](../DESIGN.md#source-policies--encounter--combat) and [card lifetimes](../DESIGN.md#combat-card-selection-state) in separately verified, reviewed commits.

## Slice 1: combat scopes and permanent-card history

| Operation | Interception, state and commitment |
|---|---|
| Combat entry | `CombatRoom.StartCombat` binds the combat before setup. `CombatManager.SetUpCombat` also supports direct engine entry. One binding consumes one ordinal: ordinary encounters use category and act; event-only encounters use the parent event. Construction and finished-room restoration consume none. |
| Local randomness | `CombatStateScope` owns a combat's address and transient random positions. Run-owned counters persist through checkpoints; replay from the preceding checkpoint regenerates the same combat address. |
| Permanent acquisition | Successful `CardPile.AddInternal` deck additions record the acquisition batch on the existing occurrence. Source contexts preserve batches across sequential copy grants, Astrolabe/Morphic Grove/Trial transformations and Sea Glass picks. Returns retain the original batch; permanent copies acquire new occurrences. |
| Permanent removal | Deck removal freezes the serialized card, including its occurrence and acquisition batch, in run-owned `CardHistory`. Pael's Tooth storage bypasses this history; returning a card clears its retained entry. Transformations retain the original's frozen entry and give the replacement a new acquisition. |
| Source assignment | Occurrence metadata records eligibility for later source-based shuffle assignment. Rewards and ordinary transformation entries qualify; copies and restricted Automaton/curse grants use acquisition order. |

Schema 6 saves acquisition metadata with cards and removed-card history with the checkpoint. Frozen JSON uses the game's card serializer; snapshot copies share only immutable strings. Earlier development saves require a fresh run.

Subsequent slices supply combat-card keys, ordering, shuffle assignment and gameplay selectors.

## Slice 2: combat card comparisons

`CardOrder.Combat` and its frozen snapshots implement the full [processing order](../CARD_ORDER.md). Combat equivalence uses the same state comparisons while omitting origin and occurrence tie-breaks. Permanent and identity-only order retain their existing scopes.

Comparisons read lasting and turn-limited costs separately, preserve modifier application order, include carried values and Dampen restoration, and exclude shared consumable discounts and the named encounter effects. Fetch and pending return availability use combat history. Snapshots freeze values before a batch changes cards.

These comparisons become the common ordering input for subsequent shuffle, routing and hand-entry slices. Bolas/Hatchet hand-draw hooks record when a pending return resolves; ordering inspection preserves gameplay state.

## Slice 3: encounter setup and enemy HP

`EncounterModel.GenerateMonstersWithSlots` prepares the combat address and seeds encounter setup. Event layouts can prepare before a fight is chosen; combat entry commits that address once. Declined layouts and finished-room reconstruction leave combat counters unchanged.

`CombatState.CreateCreature` supplies the scope while initial enemies, summons and replacements roll HP. HP selection retains current exclusions and full-range fallback, uses ascending eligible values, and advances the combat-local HP ordinal once per sampling call. Fixed ranges consume zero. Tough Egg's hatch uses the same ordinal while preserving its animation, scaling and HP hooks.

The review of slices 1–2 also found that Scroll Boxes split one acquisition batch across its sequential grants. Its source context now keeps the chosen bundle together. Ocarina's ordinary-cost melody pool is specified in the design for the generation slice.

## Slice 4: combat card generation and Entropy

Combat-local supplies reuse filtered draws and stable entry addresses. Source contexts classify the existing factory calls by cost treatment and pool; vanilla callers retain choice, upgrade, keyword and pile effects. Ocarina uses the combined melody pool, The Lovers and Splash use their combined other-character pools, and Mythic Form draws from Tarot while retaining its ordered upgrades.

Entropy reuses transformation-batch preparation: replacements are generated in identity order, paired within each identity in presentation order, then applied in the source's execution order. Generation consumes every offered identity and preserves distinct-versus-repeat batch rules. Potion factories remain reserved for the potion-effect slice.

## Slice 5: random pile insertion

`CardPileCmd.Add` replaces only its random gap roll and final insertion call, retaining the game's validation, movement and hooks. Each covered group and actual destination pile has its own combat-local sequence. The next roll chooses a relative gap in the current pile, including empty piles; rejected and fixed-position additions consume zero. Metamorphosis uses its existing generation-source context.

Checks exercise all insertion groups, draw/discard isolation, empty piles, rejected additions, inactive combat, fixed positions and Metamorphosis's actual play. Mighty Weapon and Mighty Shield join the documented Dazed source list.

## Slice 6: random orb types

Chaos, Trash to Treasure and The Magician preview the next combat-local orb type. Successful channel history commits the roll before channel hooks run; failed channels preserve it. Fixed-type channels retain their existing behavior.

## Slice 7: enemy occurrences and AI

Initial enemies receive creation-order addresses; summon moves and death-triggered replacements assign children beneath their source occurrence. Revivals retain the existing occurrence. These addresses also identify Fabricator's bot supply and summoned-rat openings.

Random branches retain vanilla effective weights and fallback rules. Effective-decision sequences cover Inklets, Exoskeletons and Scrolls of Biting; Small Leaf Slimes consume only their opening roll. Decimillipede segments share revival choices. Other enemies use move priorities by identity and branch ordinal. Rats separate shared Summon decisions, summoned openings and round-local non-Summon choices.

## Slice 8: Confused and Slither costs

Confused rolls by original starting-deck category, skipping X cards. Slither has a separate combat-local sequence; when Confused already rolled for the same draw, Slither reapplies that value at its usual timing. Draw-hook context prevents reuse on later draws, and existing ownership/hand checks remain in place.

## Slice 9: random enemy targets

Random targets use enemy-occurrence priorities and the four design groups. Empty and singleton target sets preserve their group ordinal. Interceptions retain source eligibility, repeated-hit behavior and supplied targets; automatic card play uses the card's inherent effect category.

## Slice 10: combat-card occurrences and shuffling

Combat pile admission assigns each new logical occurrence its fixed selection key and creation batch. Randomly generated cards retain entry addresses; tokens share identity counters; other grants and copies use source opportunities. Sequential grants from one effect share their creation batch.

Opening and later shuffles replace the random ordering step while preserving game modifiers and test overrides. Opening assignment includes frozen removed cards and gives Innate its separate priority sequence; ordinary assignment supplies fixed combat selection keys. Later shuffles recalculate priorities from current properties and source/acquisition history, preserving those selection keys. Pending grants include shared combat costs and keywords before their batch indices are assigned.

## Slice 11: random combat-card selection

Named selectors use combat-local persistent clocks in their shared or source-specific groups. Selection commits from one eligibility snapshot and renews selected clocks immediately. Forced and whole-set exceptions preserve clocks; incidental whole-set ordering shares a separate combat-local sequence. Power Cell applies these rules per tier, while Seeker Strike retains the game's offer display path.

Thieving Hopper uses fixed priorities from linked permanent-card keys, preserving its eligibility and recovery behavior.

## Slice 12: potion opportunities and card generation

Covered potion types retain run-wide effect counters. Committed use, discard/trade and skipped rewards advance once; acquisition and canceled use preserve them. Cauldron offers grant type exemptions before reward interaction, with unused exemptions expiring at the next room entry. These counters use existing checkpoint state.

Each committed potion use supplies its own identity sequence. Orobic Acid and Bottled Song's ordered subdraws share that supply; the source retains choices, attributes and distinctness. Cosmic Concoction's sequential grants share one creation batch for subsequent shuffles.

## Slice 13: Snecko Oil hand-entry costs

Successful hand entries retain a frozen ordering snapshot on their logical occurrence. Unordered grants, transformations and selected return batches share entry ordinals; draws and explicitly sequential returns retain source order. Creation-batch metadata covers newly admitted cards, while Stratagem and Aggression group their sequential return calls. Overflow creates no hand entry; later returns refresh it.

After Oil draws, its eligible category rolls follow frozen hand-entry order and are supplied to the existing cost loop. Effect ordinals isolate uses across combats, and test overrides retain their original behavior.

## Slice 14: combat-card routing

Committed player choices and validated plays route equivalent copies through shared operation clocks while preserving clicked objects and pile positions. Reassociation carries card keys, shuffle metadata, hand-entry history, copy relationships and permitted deck links. Growth cards and pending Hopper theft retain their exact permanent correspondence.

Gameplay references and return history follow logical targets. Outstanding Sly and automatic-play batches split equivalence until their queued effects resolve, preserving suspended iterators. Scripted Vakuu choices and already-selected automatic plays retain their target associations. Choice hooks install before rewritten callers to prevent the original async entry points from being inlined.

## Validation

Engine regressions cover combat-counter isolation, event battle ordinals, act changes, replay, acquisition batches, copies, frozen removal, Tooth storage, returns and transformations. The existing checksum/replay suite continues to exercise opening combat with the extended card metadata.

Slice 1, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 370 standalone engine checks and 424 checks in each RitsuLib initialization order passed. Two independent reviews identified source batches split by sequential additions; the shared acquisition context and regression check resolved the finding, with both follow-up reviews clear.

Slice 2, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 394 standalone engine checks and 448 checks in each RitsuLib initialization order passed. Two independent reviews verified the Tangled cost-scope and pending-return lifecycle fixes; follow-up reviews were clear.

Slice 3, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 436 standalone engine checks and 490 checks in each RitsuLib initialization order passed. Two independent reviews were clear, including the Scroll Boxes correction and Ocarina policy addition.

Slice 4, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 458 standalone engine checks and 512 checks in each RitsuLib initialization order passed. Two independent reviews were clear after adding an actual Entropy selection-hook regression alongside factory, Ocarina and Mythic Form checks.

Slice 5, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 474 standalone engine checks and 528 checks in each RitsuLib initialization order passed. Two independent reviews were clear.

Slice 6, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 482 standalone engine checks and 536 checks in each RitsuLib initialization order passed. Checks cover the three orb generators, failed and fixed channels, and automatic slot creation. Two independent reviews were clear.

Slice 7, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 505 standalone engine checks and 559 checks in each RitsuLib initialization order passed. Checks exercise branch isolation, forced/all-zero decisions, slime and Inklet scopes, summon/replacement addresses, rat rounds and Fabricator's actual move. Two independent reviews were clear.

Slice 8, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 511 standalone engine checks and 565 checks in each RitsuLib initialization order passed. Draw-hook checks cover X cards, starter categories, Kingly Kick/Slither reuse, later independent draws and cards outside hand. Two independent reviews were clear.

Slice 9, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 535 standalone engine checks and 589 checks in each RitsuLib initialization order passed. Tests exercise all four target groups, zero-damage hits, supplied targets, automatic play, and eligibility/order changes. Two independent reviews were clear.

Slice 10, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 595 standalone engine checks and 649 checks in each RitsuLib initialization order passed. Checks cover opening/removal alignment, Act 3 ordering, fixed selection keys, tokens, copies, rejected admissions, unordered grants, Corruption, and sequential Nightmare/Mythic Form/Entropy batches. Two independent reviews were clear after fixing pending-grant comparisons and the two missing power batch scopes.

Slice 11, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 619 standalone engine checks and 673 checks in each RitsuLib initialization order passed. Checks exercise shared exhaust progression, filtered batches, forced and whole-set exceptions, incidental order, Power Cell tiers/overflow, Seeker offers and Hopper theft. Two independent reviews were clear.

Slice 12, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 686 standalone engine checks and 740 checks in each RitsuLib initialization order passed. Checks exercise all seven potion generators, choices/skips, type counters, Cauldron exemptions and restoration, Cosmic creation batches, and replay across different combat opportunities. Two independent reviews were clear after grouping Cosmic Concoction's sequential grants.

Slice 13, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 740 standalone engine checks and 794 checks in each RitsuLib initialization order passed. Checks cover category rolls, frozen batches, ordered draws/All for One, overflow/re-entry, logical reassociation, Cosmic/Ecstasy/Stratagem batches and different combat opportunities. Two independent reviews were clear.

Slice 14, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 815 standalone engine checks and 869 checks in each RitsuLib initialization order passed. Checks cover real plays and cancellations, equivalent-copy choices, protected deck links, reference/return-history rebinding, Oil metadata, repeated Decisions plays, and nested Sly/Hologram and Beat Down/Graveblast batches. Two independent reviews were clear after fixing pending-reference coverage, choice-hook installation order and coexistence with RitsuLib's payment wrapper.

Aggression correction, 2026-11-17: the new Oil regression failed with the original batching and passed after grouping Aggression's returns. Checks cover sampled and whole-set returns, frozen ranks before upgrades, distinct Oil costs across reversed return order, overflow and later separate retrievals. Formatting, analyzer build and 61 unit tests passed; 917 standalone engine checks and 971 checks in each RitsuLib initialization order passed on v0.113.1. Two independent reviews were clear.

## Gameplay checks

Interactive acceptance remains pending:

- Compare corresponding combats across same-seed routes: setup, enemy decisions, generated cards and orb sequences.
- Play, discard and retrieve different equivalent copies; verify card behavior and pile presentation, including nested Sly/Hologram and Beat Down/Graveblast choices.
- Compare Oil after retention, generation and retrieval, with hand rearrangement and matching potion opportunities in different combats.
- Reload before ordinary/event battles and after victory; confirm regenerated outcomes and counters, with and without RitsuLib.
