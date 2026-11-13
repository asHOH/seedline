# Card processing order

**Status:** preliminarily settled — ready for use, with fine-tuning remaining.

Detailed ranking for the [card processing order policy](DESIGN.md#card-processing-order).

## Property scope

**Lasting properties** are current card properties with modifiers that survive turn boundaries until this instance's next play within the combat. Include card-specific effects such as Bookmark and upgraded Enlightenment, and persistent shared effects such as Spiked Gauntlets' +1 Power cost and Corruption's free Skills. Exclude shared charges another card can consume, including Pounce's next-Skill discount. Burst and Echo Form contribute no replay; Ringing Triangle contributes no Retain.

**Turn effects** expire at a turn boundary, such as unupgraded Enlightenment. Stage 4 compares their presence, costs and temporary keywords separately from Bound and card-specific availability. Stage 5 also includes turn-limited state.

Other comparisons use lasting properties. Apply the [scope exclusions](#scope-exclusions) throughout.

## Identity-only order

Evaluate stages 1–3 using each identity's unupgraded, unenchanted base properties, excluding instance state and combat effects. Resolve any remaining tie between distinct identities with stage 3's identity row; copies of the same identity remain tied.

## Comparison order

Evaluate stages 1–6 in order. Within a stage, compare its rows from top to bottom. The first strict difference decides; cards equal under all six stages remain tied.

### 1. Category

Curse (including Quests beside Injury) < Status < Strike < Defend < Other Basic < Other Attack < Other Skill < Power.

Strike/Defend means starter identities. With replay >= 1, they promote to Other Attack/Skill.

### 2. Replay

Replay `r` counts extra plays. Compare `r` normally and `(r - 1) / 2` for promoted starter Strikes/Defends. Preserve fractions: `1, 2, 3` becomes `0, 0.5, 1`.

### 3. Within-category order

Curse/Status use the authored identity lists below; all other categories use these rows.

| Order | Property | Ascending rule |
|---|---|---|
| 1 | Retain | absent < present; includes qualifying inherent, enchanted and added Retain. |
| 2 | Star cost | `(absent = 0) < X < 1 < 2 < ...` |
| 3 | Energy cost | `0 < X < 1 < 2 < Slither < 3 < ...`; Slither always uses this fixed tier in this row. |
| 4 | Draw effect | absent < present; an effect with zero amount still qualifies. Compile Driver and active Swift qualify; expired Swift contributes no draw. |
| 5 | Rarity | Authored rarity order below. |
| 6 | Pool | character < colorless; Event and Token pools count as colorless. |
| 7 | Identity | Fixed English alphabetical order, then card ID. |

### 4. Copy state

| Order | Property | Ascending rule |
|---|---|---|
| 1 | Bound | `Bound < none`. |
| 2 | Carried card value | Higher carried damage, Block or Aim ranks later; includes accumulated in-combat gains. Enchantment-owned bonuses use the enchantment rows. |
| 3 | Enchantment rank | Authored enchantment order below. |
| 4 | Enchantment amount | Lower < higher. |
| 5 | Momentum's carried bonus | Within either Momentum rank, lower accumulated damage < higher. |
| 6 | Upgrade state | `unupgraded < temporarily downgraded < upgraded`; definitions below. |
| 7 | Guilty combat count | Guilty: fewer combats completed < more. Ignore Dowsing's room progress. |
| 8 | Ethereal | `none < Ethereal`. |
| 9 | Sly | `none < Sly`. |
| 10 | Fetch / pending-return availability | Fetch's first-play draw this turn: spent < available. Bolas / Thrumming Hatchet's return at the next hand draw: none pending < pending. |
| 11 | Dampen restoration level | Lower < higher; none = 0. |
| 12a | Turn-effect presence | `none < turn effect`. |
| 12b | Star cost with turn effects | If both have turn effects, compare resulting star costs: `(absent = 0) < X < 1 < 2 < ...`. |
| 12c | Energy cost with turn effects | If both have turn effects, compare resulting energy costs: `0 < X < 1 < 2 < 3 < ...`, including Slither's actual cost. |
| 13 | Temporary keywords | `none < temporary Retain < temporary Sly < both`. |
| 14 | Slither card's lasting energy cost | Lower < higher. |

Carried values include Genetic Algorithm's Block, Show Off's Aim, and damage accumulated by The Scythe, Sovereign Blade, The Shot, Rampage, Claw, Maul, Kingly Punch and Thrash.

**Upgrade state:** unupgraded = currently unupgraded with no restoration pending; temporarily downgraded = currently downgraded with restoration pending; upgraded = currently upgraded. Exact current upgrade level breaks ties within the upgraded tier.

Rows 12b–12c recalculate costs with turn-limited modifiers included, preserving modifier application order. Row 13 covers Expertise's temporary Retain and Hand Trick's temporary Sly.

Slither uses the resulting energy cost under [lasting properties](#property-scope), including before its first roll and after later overrides.

### 5. Tie-breaks

Resolve the `=` group in [enchantment rank](#enchantment-rank) using its written order as `<`. Within the expired-enchantment member, use the original active enchantment order.

If still tied, compare star effects, then energy effects:

1. Reduce equivalent histories to the same remaining effects, removing redundant repeats and permanent overrides. Preserve differences after play, turn end, upgrades and later cost changes.
2. Keep effect order where it changes behavior; otherwise sort by the fields below.
3. Compare lists entry by entry, shorter tied lists first.

**Fields:** expiration (`turn-or-play < turn-only < until-played < combat-long`), operation (`set < add`), value ascending, then reduce-only (`false < true`). Star effects use expiration and value.

### 6. Origin

Combat-only cards rank before permanent cards and their linked combat instances.

## Identity and rarity ranks

Usefulness motivated the initial Curse/Status ranking; comparisons follow the written rules.

**Curses:** Bad Luck < Enthralled < Normality < Regret < Debt < Shame < Doubt < Decay < Writhe < Curse of the Bell < Greed < Injury < Quests < Guilty < Spore Mind < Poor Sleep < Ascender's Bane < Folly < Clumsy.

**Quests:** Byrdonis Egg < Dowsing < Lantern Key < Spoils Map.

**Statuses:** Wither < Beckon < Void < Toxic < Infection < Burn < Wound < Soot < Debris < Slimed < Dazed < Frantic Escape.

**Rarity rank:** Basic < Trash Heap < Common < Other Token < Uncommon < Exterminate/Squash < Rare < Sovereign Blade < Other Event < Ancient. Other Token includes every Token-rarity card except Sovereign Blade; Shiv and Soul share this tier. Slash-separated cards share a tier and continue to later comparisons.

| Event tier | Identities |
|---|---|
| Trash Heap | Caltrops, Clash, Distraction, Dual Wield, Entrench, Hello World, Outmaneuver, Rebound, Rip and Tear, Stack |
| Exterminate/Squash | Exterminate, Squash |
| Other Event | Byrd Swoop, Enlightenment, Feeding Frenzy, Mad Science, Metamorphosis, Peck, Toric Toughness |

## Enchantment rank

Glam < (none = expired enchantment = Spiral = Clone = Imbued) < Momentum (carried bonus = 0) < Soul's Power < Sharp < Tezcatara's Ember < Vigorous < Momentum (carried bonus > 0) < Corrupted < Instinct < Inky < Nimble < Goopy < Adroit < Sown < Slither < Perfect Fit < Steady < Royally Approved < Swift.

Momentum uses the bonus currently carried, including inherited bonus on copies. The expiring enchantments are Glam, Vigorous, Sown and Swift.

## Scope exclusions

Exclude these encounter effects at every stage:

| Effect | Source and scope |
|---|---|
| Smog | Smoggy. |
| Ringing | Ringing. |
| Galvanized | Globe Head's Galvanic; Power cards. |
| Tainted | Infested Prism's Vital Spark; Skill cards. |
| Hex-derived Ethereal | Spectral Knight's Hex. |

Galvanic and Vital Spark also affect new combat entries; each has one source enemy.

**Scope limits:** Knowledge Demon's menu-only Status cards and multiplayer-only cards.
