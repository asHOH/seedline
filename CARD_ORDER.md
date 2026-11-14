# Card processing order

Ranking for the [card processing order policy](DESIGN.md#card-processing-order).

## Property scope

**Lasting properties** are current card properties with modifiers that survive turn boundaries until this instance's next play within the combat. Include card-specific effects such as Bookmark and upgraded Enlightenment, and persistent shared effects such as Spiked Gauntlets' +1 Power cost and Corruption's free Skills. Exclude shared charges another card can consume, including Pounce's next-Skill discount. Burst and Echo Form contribute no replay; Ringing Triangle contributes no Retain.

**Turn effects** expire at a turn boundary, such as unupgraded Enlightenment. Stage 4 compares their presence, costs and temporary keywords separately from Bound and card-specific availability. Stage 5 also includes turn-limited state.

Other comparisons use lasting properties. Apply the [scope exclusions](#scope-exclusions) throughout.

## Identity-only order

Evaluate stages 1–3 using each identity's unupgraded, unenchanted base properties, excluding instance state and combat effects. Resolve any remaining tie between distinct identities with stage 3's identity row; copies of the same identity remain tied.

## Comparison order

Evaluate stages 1–6 in order. Within a stage, compare its rows from top to bottom. The first strict difference decides; cards equal under all six stages remain tied.

### 1. Category

Curses and Quests < Status < Strike < Mighty Weapon < Defend < Mighty Shield < Other Basic < Other Attack < Other Skill < Conditionals < Power.

Strike/Defend means starter identities. With replay >= 1, they promote to Other Attack/Skill.

Rank these Skills as **Other Attack**: Blade Dance, Bouncing Flask, Darkness, Deadly Poison, Death, End of Days, God Byrd, Knife Trap, No Escape, Oblivion, Outbreak, Snakebite, Tempest, The Bomb, The Moon, Zap.

**Conditionals:** an authored category for setup-dependent payoffs, combo enablers and selected conditional discounts. The list below defines membership, which stays fixed as combat state changes.

| Pool | Conditional identities |
|---|---|
| Ironclad | Body Slam, Feed, Fiend Fire, Forgotten Ritual, One-Two Punch, Pact's End, Primal Force, Rage, Second Wind, Stoke, Stomp, Tear Asunder |
| Silent | Anticipate, Bubble Bubble, Bullet Time, Calculated Gamble, Corrosive Wave, Finisher, Grand Finale, Mirage, Murder, Nightmare, Pinpoint, Shadow Step, Shadowmeld, The Hunt |
| Regent | "Decisions, Decisions", Lunar Blast, Monologue, Radiate, Supermassive |
| Necrobinder | Eidolon, Pull from Below, Sacrifice, Transfigure |
| Defect | Barrage, Flak Cannon, Helix Drill, Multi-Cast, Quadcast, Signal Boost, Synchronize, Voltaic |
| Fool | Bounce, Cho Limbs, Coda, Florescence, Growth, Harmonic Shield, Ki Verse, Nail, Quietus, Uproot, Wrest |
| Colorless | Anointed, Beat Down, Gold Axe, Hand of Greed, Hidden Gem, Impatience, Mind Blast, Prolong, Restlessness |
| Event | Clash, Dual Wield, Enlightenment, Entrench, Feeding Frenzy, Stack |
| Token | Judgement, The Devil |

### 2. Replay

Replay `r` counts extra plays. Compare `r` normally and `(r - 1) / 2` for promoted starter Strikes/Defends. Preserve fractions: `1, 2, 3` becomes `0, 0.5, 1`.

### 3. Within-category order

Curses, Quests and Statuses use the authored identity lists below; all other categories use these rows.

| Order | Property | Ascending rule |
|---|---|---|
| 1 | Retain | absent < present; includes qualifying inherent, enchanted and added Retain. |
| 2 | Star cost | `(absent = 0) < X < 1 < 2 < ...` |
| 3 | Rarity band | Below Rare < Rare or higher in the authored rarity rank. |
| 4 | Energy cost | `0 < X < 1 < 2 < Slither < 3 < ...`; Slither always uses this fixed tier in this row. |
| 5 | Draw effect | absent < present; an effect with zero amount still qualifies. Compile Driver and active Swift qualify; expired Swift contributes no draw. |
| 6 | Rarity | Authored rarity order below. |
| 7 | Pool | character < colorless; Event and Token pools count as colorless. |
| 8 | Identity | Fixed English alphabetical order, then card ID. |

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

If still tied, compare stored star-modifier lists, then energy-modifier lists, in application order. Retain stored duplicates and overridden entries.

Compare records lexicographically: stars by `(expiration, value)`; energy by `(expiration, operation, value, reduce-only)`. Shorter tied lists rank first.

Use `turn-or-play < turn-only < until-played < combat-long`, `set < add`, ascending numeric values, and `false < true`.

**Tradeoff:** ordering and [combat-routing equivalence](DESIGN.md#combat-card-routing) can depend on modifier history even when future cost behavior is identical, such as two `−1` modifiers versus one `−2` with the same expiration.

### 6. Origin

Combat-only cards rank before permanent cards and their linked combat instances.

## Identity and rarity ranks

Usefulness motivated the initial Curse/Quest/Status ranking; comparisons follow the written rules.

**Curses and Quests:** Bad Luck < Enthralled < Normality < Regret < Debt < Shame < Doubt < Decay < Writhe < Curse of the Bell < Greed < Injury < Quests < Guilty < Spore Mind < Poor Sleep < Ascender's Bane < Folly < Clumsy.

**Quests (alphabetical):** Byrdonis Egg < Dowsing < Lantern Key < Spoils Map.

**Statuses:** Wither < Beckon < Void < Toxic < Infection < Burn < Wound < Soot < Debris < Slimed < Sadness < Dazed < Frantic Escape.

**Rarity rank:** Basic < Peck < Trash Heap < Toric Toughness < Common < Other Token < Dingy Dagger (+0/+1) < Uncommon < Other Event A < Rare < Sovereign Blade < Other Event B < Ancient. Other Token includes every Token-rarity card except Sovereign Blade; Shiv and Soul share this tier. Slash-separated cards share a tier and continue to later comparisons.

| Event tier | Identities |
|---|---|
| Trash Heap | Caltrops, Clash, Distraction, Dual Wield, Entrench, Hello World, Outmaneuver, Rebound, Rip and Tear, Stack |
| Other Event A | Exterminate, Squash, Enlightenment, Feeding Frenzy |
| Other Event B | Byrd Swoop, Dingy Dagger (+2 or higher), God Byrd, Mad Science, Metamorphosis |

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
