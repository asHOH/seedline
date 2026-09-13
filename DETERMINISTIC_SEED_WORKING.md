# Deterministic seed mod — working document

Develop the remaining design against the [goals and fairness principle](DETERMINISTIC_SEED_DESIGN.md#goals).

## Remaining design: rewards and encounter

| Category | Determines | Primary progression |
|---|---|---|
| Rewards | Generated reward identities and amounts, including permanent transformation results | Cumulative ordinals |
| Encounter | Random selection of affected cards/items, room content, named event options and combat randomness | Act-local ordinals |

Classify random decisions, not whole effects. Player choices and fixed selections belong to neither category. Sequence scope and addressing are separate fairness decisions; the progression above is a default, not a definition.

Distinguish normal fights, elites, bosses, Ancients, chests, events and special relic effects only where their rules differ.

**Catfood addressing:** cumulative normal-fight/elite/boss/shop/event/Ancient counters. Ancients increment both `eventEntered` and `ancientVisited`; options stay independently seeded, while Ancient card overrides use `ancientVisited`. Room reward RNG uses room-type counts, a normal-fight fallback and event-card overrides.

**Settled:** Rewards - relics, chests, combat gold, potions, permanent transformations and covered direct card additions; Encounter - shops and unknown-room resolution.

### Rewards

#### Direct card additions: remaining special sources

See the [settled transformation and direct-addition policy](DETERMINISTIC_SEED_DESIGN.md#settled-policy-permanent-transformations-and-direct-card-additions). Remaining cases:

- **Pael's Tooth — deferred:** randomly returns a previously removed card, upgraded; this selects a stored card rather than generating a new identity.

#### Cards: offers, rarity and rerolls

**Open:** source sharing and addressing for normal-fight, elite, boss and event/Ancient card rewards, including identities, random upgrades and rerolls; determine rarity-reset rules and reroll effects on later offers.

- **Catfood:** combat identities/upgrades use room type, count and offer position. Combat rerolls still update that room type's rarity state, potentially shifting later offers.
- **Catfood identity consumption:** rarity is rolled before identity; a Rare consumes the current slot's identity draw. There are no independent per-rarity identity sequences. Later identities can still change through rarity-offset resets and offer duplicate exclusion.
- **Code-confirmed Catfood bug:** the initial offer uses the RNG intended for rerolls. The bug selects the wrong stream, not the wrong ordinal. Gameplay impact not reproduced in-game yet.
- **Catfood rarity:** normal fights and elites have separate offsets with vanilla base probabilities, non-rare growth and post-Rare reset value. Shops have a separate offset that does not grow, leaving their Rare roll chance at 4% (0% with Scarcity). Ancient entry, including Neow, resets these offsets to `-0.05`.
- **Catfood boss rewards:** the separately stored boss offset has no effect; boss rewards no longer reset normal-fight or elite offsets; they are instead reset on entering Ancient. So there's no perceivable changes against vanilla.
- **Elite initialization — proposed:** on Ancient entry, including Neow, set the elite offset to `0.01`/`-0.02` (without Scarcity/with Scarcity), approximating two normal fights. Retain vanilla non-rare growth.
- **Offset resets — open:** whether to preserve combat rarity resets on Ancient entry. The elite Ancient-entry reset value has the separate proposal above.
- **Act alignment — open:** prevent divergence across acts; assess alongside the offset-reset decision.
- **Catfood direct/event generation:** seeds add room/event count to a per-room options-call index. Seed collisions exist; harmful gameplay impact remains unverified.

**Reroll effect:** non-Rare reroll results raise the rarity offset for later normal-fight rewards (catfood and vanilla).

### Encounter

#### Shops, chests and events

- **Shops and chests:** see the settled [shop](DETERMINISTIC_SEED_DESIGN.md#settled-policy-shops-wongos-and-courier) and [chest](DETERMINISTIC_SEED_DESIGN.md#settled-policy-chests) policies.
- **Relic Trader — deferred:** named choices belong to Encounter. Decide which owned relics and replacements are offered, their source, and when offered/chosen/unchosen replacements are consumed. See [event rules](DETERMINISTIC_SEED_DESIGN.md#relic-and-event-rules) and [selection mechanics](DETERMINISTIC_SEED_DESIGN.md#generation-and-selection-mechanics).
- **Event-specific rewards — settled:** use [private event RNG](DETERMINISTIC_SEED_DESIGN.md#settled-policy-event-specific-rewards), including Trash Heap and event-exclusive relic choices such as Doll Room. Event selection itself follows the existing event-list rules.

#### Selection and setup

- **Catfood identity baseline:** inherits [vanilla event/encounter selection](DETERMINISTIC_SEED_DESIGN.md#generation-and-selection-mechanics); its cumulative room counters do not select the event/encounter identity.
- **Catfood:** encounter ID + cumulative room-type ordinal controls variable composition/setup, not ordinary HP. No visible setup mismatch reproduced.
- **Proposed:** act-local setup ordinals, distinguishing source/effect where necessary.
- **Lantern Key proposal:** forced Act 3 War Historian Repy gets **ordinal 0**, leaving ordinary events untouched; Catfood counts it normally.
- **Unadopted suggestion:** separate forced-event selection from `?` room-type resolution.

#### Permanent card changes

**Open:** random card selection for permanent upgrades/downgrades; define sharing by source/effect.

#### Combat randomness

- **Catfood:** re-seeds shuffle RNG at combat start; later shuffles continue it.
- **Proposed:** act-local opening-shuffle ordinals. First Act 2 normal fight then aligns setup/shuffle addresses despite different Act 1 normal-fight counts; cumulative reward addresses still differ.
- **Open:** monster HP/AI, random targets/costs/orbs, temporary upgrades/downgrades and combat transformations; decide sharing by source/effect and special-relic interactions.
- **Shuffles — open:** settle opening-shuffle addressing and how later shuffles share or advance randomness.
- **Generated cards — open:** decide sharing and addressing by pool/type: own-character Attack/Skill/Power, colorless and other characters; include special-relic interactions.

## Unadopted combat-gold strategies

Superseded by the [settled shared sequence with normal-fight compensation](DETERMINISTIC_SEED_DESIGN.md#settled-policy-combat-gold). Drawbacks below concern A3+.

- **Shared range-relative rolls:** preserves vanilla distributions, but swapping normal → elite to elite → normal can change total gold by 1.
- **Shared identical offsets without compensation:** using `X = 0–7`, normal `7 + X` and elite `26 + X` removes order differences but lowers expected normal-fight gold by 0.5.
- **Separate normal-fight and elite sequences:** preserves vanilla distributions, but replacing a normal fight with an elite can shift the premium roughly 8 gold above or below its expectation.

## Next steps and validation

**Next:** Address remaining Encounter design, including Relic Trader, other events/Ancients and combat setup.

**Validation suggestion:** targeted RNG logs and paired runs; histories alone miss internal calls/actions. Use consistent seed derivation.

**Separate improvement:** add run seed to boss shuffle initialization.

**Pending:** verify save/load preserves counters and outcomes.
