# Deterministic Card Processing Order

**Status:** preliminarily settled — ready for use, with fine-tuning remaining.

Detailed ranking for the [Deterministic Card Processing Order contract](DESIGN.md#deterministic-card-processing-order). Source-specific projections, including identity-only transformations, retain that contract.

## Property scope

**Lasting properties** are current card properties with modifiers that survive turn boundaries until this instance's next play within the combat. Include qualifying temporary and shared effects, such as Spiked Gauntlets' +1 Power cost, Corruption's free Skills, Bookmark and upgraded Enlightenment. Burst and Echo Form contribute no replay; Ringing Triangle contributes no Retain.

**Turn effects** expire at a turn boundary, such as unupgraded Enlightenment. Stage 4 gives Bound, card-specific availability and temporary keywords their own rows; its turn-effect block compares presence and temporary costs. Until-played effects that survive turn boundaries, including Pounce's next-Skill discount, leave that block unchanged. Stage 6's state tie-breakers also include turn-limited state. Other property comparisons use lasting properties. Apply the [scope exclusions](#scope-exclusions) throughout.

## Comparison sequence

Evaluate stages 1–7 in order. Within a stage, compare its rows from top to bottom. The first strict difference decides; later stages cannot override it. Identical copies remain tied.

### 1. Category

Curse (including Quests beside Injury) < Status < Strike < Defend < Other Basic < Other Attack < Other Skill < Power.

Strike/Defend means starter identities. With replay >= 1, they move to Other Attack/Skill.

### 2. Replay

Replay counts extra plays. Compare `r` normally and `(r - 1) / 2` for promoted starter Strikes/Defends. Preserve fractions: `1, 2, 3` becomes `0, 0.5, 1`.

### 3. Within-category order

| Category | Compare before proceeding to stage 4 |
|---|---|
| Curse / Status | Position in the authored identity lists below. |
| Starter Strike / Defend | Proceed directly to stage 4. |
| Other Basic / Other Attack / Other Skill / Power | Apply all seven rows below. |

| Order | Property | Ascending rule |
|---|---|---|
| 1 | Retain | absent < present; includes qualifying inherent, enchanted and added Retain. |
| 2 | Star cost | `(absent = 0) < X < 1 < 2 < ...` |
| 3 | Energy cost | `0 < X < 1 < 2 < Slither < 3 < ...`; Slither always uses this fixed tier in this row. |
| 4 | Draw effect | absent < present; an effect with zero amount still qualifies. Compile Driver and active Swift qualify; expired Swift contributes no draw. |
| 5 | Rarity | Authored rarity order below. |
| 6 | Pool | character < colorless; Event and Token pools count as colorless. |
| 7 | Identity | Fixed English alphabetical order, then stable model ID. |

### 4. Copy state

| Order | Property | Ascending rule |
|---|---|---|
| 1 | Bound | `Bound < none`. |
| 2 | Carried card value | Higher carried damage or Block ranks later; includes accumulated in-combat gains. Enchantment-owned bonuses use the enchantment rows. |
| 3 | Enchantment rank | Authored enchantment order below. |
| 4 | Enchantment amount | Lower < higher. |
| 5 | Momentum's carried bonus | Within either Momentum rank, lower accumulated damage < higher, including inherited bonus on copies. |
| 6 | Upgrade state | `originally unupgraded < originally upgraded < upgraded`; definitions below. |
| 7 | Late counters | Guilty: fewer combats completed < more. Ignore Dowsing's room progress. |
| 8 | Ethereal | `none < Ethereal`. |
| 9 | Sly | `none < Sly`. |
| 10 | Fetch / pending-return availability | Fetch: draw spent < draw available. Bolas / Thrumming Hatchet: no pending return < pending return. These card-specific comparisons share one row. |
| 11a | Turn-effect presence | `none < turn effect`. |
| 11b | Temporary star cost | If both have turn effects, compare resulting star costs: `(absent = 0) < X < 1 < 2 < ...`. |
| 11c | Temporary energy cost | If both have turn effects, compare resulting energy costs: `0 < X < 1 < 2 < 3 < ...`. This row uses Slither's actual temporary cost. |
| 12 | Temporary keywords | `none < temporary Retain / temporary Sly`. Either or both satisfy the present tier; stage 6 resolves their internal order. |

Carried values include Genetic Algorithm's Block and damage accumulated by The Scythe, Sovereign Blade, Rampage, Claw, Maul, Kingly Punch and Thrash.

**Upgrade state:** originally unupgraded = currently unupgraded with no restoration pending; originally upgraded = currently downgraded with restoration pending; upgraded = currently upgraded. This includes Dampen's stored restoration. Exact current upgrade level breaks ties within the upgraded tier.

**Availability:** Fetch tracks its first-play draw for this turn. Bolas and Thrumming Hatchet track their return at the next hand draw. Their identities make these comparisons independent.

Rows 11b–11c add turn-limited cost modifiers to the lasting cost calculation and compare the resulting costs. Shared next-play discounts such as Pounce remain excluded from these rows. Row 12 covers Expertise's temporary Retain and Hand Trick's temporary Sly.

Example: with all earlier rows tied, two cards that cost 0 and 1 this turn sort `0 < 1`. A difference in upgrade level has already decided their order before this comparison is reached.

### 5. Deferred equality groups

`=` defers a distinction until all comparisons in stages 1–4 tie. Then revisit declared equality groups from the latest layer backward, treating their written order as `<`. Within the expired-enchantment member, use the original active enchantment order. Slash-separated entries simply share a tier and continue to later layers.

For example, `none = Spiral` lets upgrade level and later copy-state rows decide first; only a remaining tie uses the written `none < Spiral` fallback.

### 6. Remaining state

After deferred equalities, use these deterministic tie-breakers in order:

| Order | Property | Ascending rule |
|---|---|---|
| 1 | Temporary-keyword combination | temporary Retain < temporary Sly < both. Absence was resolved in stage 4. |
| 2 | Slither's stored cost | Lower < higher; stage 3 retains its fixed Slither tier. |
| 3 | Active cost modifiers | Compare star modifiers, then energy modifiers, using the rule below. |

Compare each modifier list in application order. Within each modifier, compare expiration (turn-or-play < turn-only < until-played < combat-long), operation (set < add), value ascending, then reduce-only flag (false < true); star modifiers use expiration and value. If one list is an exact prefix of the other, the shorter list comes first. Include active modifiers whose effects are currently masked; cleared modifiers and acquisition history stay outside this comparison.

Equal current costs can diverge as their modifiers expire. Slither stores its roll for the combat; direct retrieval such as Secret Weapon can preserve it.

### 7. Origin

`combat copy < material`. A combat copy exists only for the combat; material is a permanent card or its linked combat instance. This is the final comparison, after remaining state.

## Identity and rarity ranks

Usefulness motivated the initial Curse/Status ranking; comparisons follow the written rules.

**Curses:** Bad Luck < Enthralled < Normality < Regret < Debt < Shame < Doubt < Decay < Writhe < Curse of the Bell < Greed < Injury < Quests < Guilty < Spore Mind < Poor Sleep < Ascender's Bane < Folly < Clumsy.

**Quests:** Byrdonis Egg < Dowsing < Lantern Key < Spoils Map; their exact positions beside Injury are immaterial.

**Statuses:** Wither < Beckon < Void < Toxic < Infection < Burn < Wound < Soot < Debris < Slimed < Dazed < Frantic Escape.

**Rarity layer:** Basic < Trash Heap < Common < Other Token < Uncommon < Exterminate/Squash < Rare < Sovereign Blade < Other Event < Ancient. Other Token includes every Token-rarity card except Sovereign Blade; Shiv and Soul share this tier. Slash-separated cards share a tier and continue to later layers.

| Event tier | Identities |
|---|---|
| Trash Heap | Caltrops, Clash, Distraction, Dual Wield, Entrench, Hello World, Outmaneuver, Rebound, Rip and Tear, Stack |
| Exterminate/Squash | Exterminate, Squash |
| Other Event | Byrd Swoop, Enlightenment, Feeding Frenzy, Mad Science, Metamorphosis, Peck, Toric Toughness |

## Enchantment rank

Glam < (none = expired enchantment = Spiral = Clone = Imbued) < Momentum (carried bonus = 0) < Soul's Power < Sharp < Tezcatara's Ember < Vigorous < Momentum (carried bonus > 0) < Corrupted < Instinct < Inky < Nimble < Goopy < Adroit < Sown < Slither < Perfect Fit < Steady < Royally Approved < Swift.

Momentum uses the bonus currently carried; playing and copying are equivalent ways to acquire it. Expired enchantments share the equality member above. Their internal stage-5 fallback follows their active ranks: Glam < Vigorous < Sown < Swift for the currently expiring types.

## Scope exclusions

- **Smog and Ringing:** excluded as shared encounter effects in the current solo scope.
- **Galvanized and Tainted:** excluded because the current solo encounters use a single Globe Head or Infested Prism. Galvanic and Vital Spark apply their shared penalty to Power or Skill cards respectively, including new combat entries.
- **Hex-derived Ethereal:** excluded at every stage because Spectral Knight in the three-knight elite encounter applies Hex across the player's cards. Inherent and locally added Ethereal, such as Sculpting Strike's keyword, still count. A Hex-only card uses the `none` tier; a card with local Ethereal plus Hex uses the `Ethereal` tier. Hex supplies no later source tie-break.

**Scope limits:** Knowledge Demon's menu-only Status cards and multiplayer-only cards lie outside the covered batches.
