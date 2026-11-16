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

Combat-card keys, full combat ordering, shuffle assignment and gameplay selectors follow in subsequent slices. This slice preserves current combat outcomes.

## Slice 2: combat card comparisons

`CardOrder.Combat` and its frozen snapshots implement the full [processing order](../CARD_ORDER.md). Combat equivalence uses the same state comparisons while omitting origin and occurrence tie-breaks. Permanent and identity-only order retain their existing scopes.

Comparisons read lasting and turn-limited costs separately, preserve modifier application order, include carried values and Dampen restoration, and exclude shared consumable discounts and the named encounter effects. Fetch and pending return availability use combat history. Snapshots freeze values before a batch changes cards.

These comparisons become the common ordering input for subsequent shuffle, routing and hand-entry slices. Bolas/Hatchet hand-draw hooks record when a pending return resolves; ordering inspection preserves gameplay state.

## Slice 3: encounter setup and enemy HP

`EncounterModel.GenerateMonstersWithSlots` prepares the combat address and seeds encounter setup. Event layouts can prepare before a fight is chosen; combat entry commits that address once. Declined layouts and finished-room reconstruction leave combat counters unchanged.

`CombatState.CreateCreature` supplies the scope while initial enemies, summons and replacements roll HP. HP selection retains current exclusions and full-range fallback, uses ascending eligible values, and advances the combat-local HP ordinal once per sampling call. Fixed ranges consume zero. Tough Egg's hatch uses the same ordinal while preserving its animation, scaling and HP hooks.

The review of slices 1–2 also found that Scroll Boxes split one acquisition batch across its sequential grants. Its source context now keeps the chosen bundle together. Ocarina's ordinary-cost melody pool is specified in the design for the generation slice.

## Validation

Engine regressions cover combat-counter isolation, event battle ordinals, act changes, replay, acquisition batches, copies, frozen removal, Tooth storage, returns and transformations. The existing checksum/replay suite continues to exercise opening combat with the extended card metadata.

Slice 1, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 370 standalone engine checks and 424 checks in each RitsuLib initialization order passed. The v0.113.0 API build passed. Two independent reviews identified source batches split by sequential additions; the shared acquisition context and regression check resolved the finding, with both follow-up reviews clear.

Slice 2, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 394 standalone engine checks and 448 checks in each RitsuLib initialization order passed. The v0.113.0 API build passed. Two independent reviews verified the Tangled cost-scope and pending-return lifecycle fixes; follow-up reviews were clear.

Slice 3, 2026-11-16: formatting, analyzer build and 61 unit tests passed; 436 standalone engine checks and 490 checks in each RitsuLib initialization order passed. The v0.113.0 API build passed. Two independent reviews were clear, including the Scroll Boxes correction and Ocarina policy addition.

Interactive acceptance and v0.113.0 runtime checks remain pending.
