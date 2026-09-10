import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const root = 'D:/P/StS2';
const out = path.dirname(new URL(import.meta.url).pathname.replace(/^\/(\w:)/, '$1'));
const sourceDir = `${root}/sts2-decompiled/MegaCrit.Sts2.Core.Models.Events`;
// Columns: source model, act override, entry conditions, content, deterministic, explanation.
const data = [
 ['AbyssalBaths','','None.','Immerse: +2 max HP, then 3 damage; repeat with damage increasing by 1 each time, or exit. Abstain: heal 10.',true,'Fixed choices and repeat-cost rule.'],
 ['Amalgamator','','Every player: at least 2 removable Basic Strikes and 2 removable Basic Defends.','Combine 2 selected Basic Strikes into Ultimate Strike, or 2 selected Basic Defends into Ultimate Defend.',true,'Fixed choices; selected deck cards depend on the player.'],
 ['AromaOfChaos','','None.','Transform one selected card into a random card, or upgrade one selected card.',true,'Transformation result is hidden until commitment.'],
 ['BattlewornDummy','','None.','Choose dummy setting 1 / 2 / 3: win within 3 turns for a potion / 2 random upgrades / a random relic. The dummy does not attack; timeout gives no reward.',true,'Fixed challenge menu; random rewards do not change the offers.'],
 ['BrainLeech','Acts 1–2','None beyond act.','Share knowledge: choose 1 of 5 generated character cards. Rip: take 5 damage for a colorless card reward.',true,'Fixed event options; generated reward-screen cards are excluded.'],
 ['Bugslayer','','None.','Receive Exterminate or Squash.',true,'Two fixed named cards.'],
 ['ByrdonisNest','','Every player: no event pet.','Eat: +7 max HP. Take: add Byrdonis Egg.',true,'Fixed options.'],
 ['ColorfulPhilosophers','','Every player: more than 1 character card pool unlocked.','Choose another character’s color; receive Common, Uncommon and Rare card rewards from that color (3 choices per reward).',false,'Up to 3 other colors offered; random subset when more than 3 qualify. Character/unlocks also affect choices.'],
 ['ColossalFlower','','Every player: HP ≥ 19.','Take 35 gold, or take 5 damage to reach 75 gold; take 6 more damage to reach 135 gold; take 7 more for Pollinous Core instead.',true,'Fixed multi-stage gold/relic ladder.'],
 ['CrystalSphere','Acts 2–3','Every player: gold ≥ 100.','Pay 51–99 gold for 3 reveals, or take Debt for 6 reveals in the crystal-sphere minigame. Board rewards include gold, cards, potions and a hidden random relic.',false,'Random quoted admission price; minigame board also varies. Relic identity is not shown before paying.'],
 ['DenseVegetation','','Solo: none. Multiplayer: every player HP > 8.','Trudge: take 8 damage for 61–99 gold. Rest: receive rest-site healing, then fight the event encounter.',false,'Quoted gold amount is rolled; healing depends on player state.'],
 ['DollRoom','Act 2','None beyond act.','Receive 1 random doll free; take 5 damage to choose from 2, or 15 damage to choose from 3. Pool: Daughter of the Wind, Mr Struggles, Bing Bong.',false,'Initial menu is fixed. Later named doll options come from a shuffled special pool; payment precedes those options.'],
 ['DoorsOfLightAndDark','','None.','Light: upgrade 2 random cards. Dark: remove 1 selected card.',true,'Only outcome/target selection varies.'],
 ['DrowningBeacon','','None.','Lose 13 max HP for Fresnel Lens, or receive Glowwater Potion.',true,'Fixed scripted relic/potion choices.'],
 ['EndlessConveyor','','Every player: gold ≥ 120.','Buy the displayed dish for 40 gold, repeatedly, or initially observe the chef for a random upgrade. Dishes grant healing, max HP, upgrades, transformation, cards or a potion. Golden Fysh instead grants 75 gold; every fifth dish is Feeding Frenzy.',false,'Displayed dish and subsequent dish sequence are rolled; health and potion slots filter dishes.'],
 ['FakeMerchant','Acts 2–3','Single-player only; gold ≥ 100 OR holding Foul Potion.','Buy from 6 displayed counterfeits drawn from 9 Event relics, or leave. Throw Foul Potion to fight: remaining stock plus Fake Merchant’s Rug are awarded.',false,'Displayed counterfeit inventory and shop prices vary. Counterfeits are Event relics, not ordinary-pool offers.'],
 ['FieldOfManSizedHoles','','Every player: a card eligible for Perfect Fit.','Resist: remove 2 selected cards and add Normality. Enter your hole: enchant 1 selected card with Perfect Fit.',true,'Fixed options.'],
 ['GraveOfTheForgotten','','Every player: a card eligible for Soul’s Power.','Confront: enchant 1 card with Soul’s Power and add Decay. Accept: receive Forgotten Soul.',true,'Fixed scripted choices; curse belongs only to Confront.'],
 ['HungryForMushrooms','','None.','Big Mushroom: +20 max HP, but draw 2 fewer on the opening turn. Fragrant Mushroom: take 15 damage and upgrade 2 random cards.',true,'Two fixed named relics; random upgrade targets are outcomes.'],
 ['InfestedAutomaton','','None.','Study: receive a random Power. Touch core: receive a random card with canonical cost 0 (not X).',true,'Fixed pool descriptions; card identity is a hidden reward.'],
 ['JungleMazeAdventure','','Solo: none. Multiplayer: every player HP > 18.','Solo quest: take 18 damage for about 150 ± 15 gold. Join forces: gain about 50 ± 15 gold.',false,'Both quoted gold amounts are randomized (float variance, then game conversion).'],
 ['LostWisp','','None.','Receive Lost Wisp plus Decay, or take about 60 ± 15 gold.',false,'Quoted gold offer varies; named relic option is fixed.'],
 ['LuminousChoir','','Every player: gold ≥ 149 and available relics in grab bag.','Pay 100–149 gold for a hidden random relic, or remove 2 selected cards and add Spore Mind.',false,'Quoted tribute cost is rolled. Entry uses canonical 149-gold threshold.'],
 ['MorphicGrove','','Every player: gold ≥ 100 and at least 2 transformable cards.','Group: lose all gold and transform 2 selected cards. Loner: +5 max HP.',true,'Fixed rule (“all gold”); current gold and transformed outcomes can differ.'],
 ['PotionCourier','Acts 2–3','None beyond act.','Grab potions: receive 3 Foul Potions. Ransack: receive 1 hidden random Uncommon potion.',true,'Fixed offered reward types.'],
 ['PunchOff','','Total floor ≥ 6.','Nab: add Injury and receive a random relic. Fight: combat for a random relic and potion.',true,'Fixed choices. The source’s randomized Gold variable is unused in this reward flow.'],
 ['RanwidTheElder','Acts 2–3','Every player: gold ≥ 100, at least 1 potion and at least 1 tradable relic.','Give a displayed owned potion / 100 gold / a displayed owned relic for 1 / 1 / 2 hidden random relics. No normal leave option.',false,'Randomly selected surrendered potion and relic are named before choice; new relics remain hidden.'],
 ['Reflections','','None.','Touch mirror: randomly downgrade up to 2 cards, then randomly upgrade up to 4. Shatter: duplicate the entire deck and add Bad Luck.',true,'Fixed choice rules; random targets are outcomes.'],
 ['RelicTrader','Acts 2–3','Every player: at least 5 tradable relics.','Choose 1 of 3 displayed exchanges: surrender the named owned relic for the named new ordinary relic. Normally no leave option.',false,'Both trade inputs and new relic identities vary. Three new relics are pulled on entry, before choosing.'],
 ['RoomFullOfCheese','Acts 1–2','None beyond act.','Take 14 damage for Chosen Cheese, or choose a Common character card from 8 generated cards.',true,'Initial event menu is fixed; later card reward-grid identities are excluded.'],
 ['RoundTeaParty','','Every player: HP ≥ 12.','Enjoy tea: receive Royal Poison (heal fully now; take 4 damage at the start of each combat). Pick fight: take 11 damage for a hidden random relic.',true,'Fixed named/hidden reward choices; healing amount follows current HP.'],
 ['SapphireSeed','','None.','Eat: heal 9 and upgrade 1 selected card. Plant: enchant 1 selected card with Sown.',true,'Fixed choices.'],
 ['SelfHelpBook','Acts 1–3','No event-entry restriction. Options require suitable enchantable Attack / Skill / Power cards; skip if none qualify.','Read back / passage / whole book: enchant an Attack with Sharp 2 / a Skill with Nimble 2 / a Power with Swift 2.',true,'Fixed options conditional on eligible deck cards; no offer RNG.'],
 ['SlipperyBridge','Acts 1–3','Total floor > 6; every player has a removable card.','Remove the displayed random card, or take 3 damage to reroll it. Repeated hold-ons increase damage by 1 and offer another card.',false,'Named removal target is randomly selected on entry and after each hold-on.'],
 ['SpiralingWhirlpool','','Every player: a card eligible for Spiral.','Enchant 1 selected card with Spiral, or heal 33% of max HP.',true,'Fixed choices; healing follows max HP.'],
 ['SpiritGrafter','','None.','Let it in: heal 25 and receive Metamorphosis. Rejection: upgrade 1 selected card and take 10 damage.',true,'Fixed choices.'],
 ['StoneOfAllTime','Act 2','Every player: at least 1 potion. Push requires a card eligible for Vigorous.','Discard the displayed potion for +10 max HP, or take 6 damage to enchant a card with Vigorous 8.',false,'The surrendered potion is randomly selected and named before choice.'],
 ['SunkenStatue','','None.','Receive Sword of Stone, or take 7 damage for about 111 ± 10 gold. Sword later becomes Sword of Jade after 5 elite victories.',false,'Quoted gold offer varies; Sword of Stone is always the same.'],
 ['SunkenTreasury','','None.','Take 52–67 gold, or take 303–363 gold and add Greed.',false,'Both quoted gold amounts are rolled.'],
 ['Symbiote','Acts 2–3','No additional entry restriction. Approach requires a card eligible for Corrupted.','Approach: enchant 1 card with Corrupted. Kill with fire: transform 1 selected card.',true,'Fixed menu; eligibility locks and transformation results are separate.'],
 ['TabletOfTruth','','None.','Smash: heal 20. Decipher repeatedly: lose 3 / 6 / 12 / 24 max HP for a random upgrade each; final step reduces max HP to 1 and upgrades all eligible cards. Can stop between steps.',true,'Fixed cost ladder; final amount depends on current max HP. Random upgrade targets are outcomes.'],
 ['TeaMaster','Acts 1–2','Every player: gold ≥ 150.','Pay 50 for Bone Tea, pay 150 for Ember Tea, or receive Tea of Discourtesy (adds 2 Dazed next combat).',true,'Fixed scripted relic choices and prices.'],
 ['TheFutureOfPotions','Acts 1–3','Every player: at least 2 potions.','Trade one of the first 3 owned potions for an upgraded card reward of the shown rarity and randomly assigned Attack/Skill/Power type. Common/Token potions cannot yield Power; Event maps to Rare.',false,'Card type assigned to each named potion is rolled and shown before commitment.'],
 ['TheLanternKey','','None.','Return key: gain 100 gold. Keep key: fight the Mysterious Knight encounter for Lantern Key, which enables forced War Historian Repy in Act 3.',true,'Fixed event choices.'],
 ['TheLegendsWereTrue','Act 1','Every player: a nonempty deck and HP ≥ 10.','Nab the map: receive Spoils Map. Find an exit: take 8 damage for a hidden random potion.',true,'Fixed offered reward types.'],
 ['ThisOrThat','Acts 1–3','None.','Plain: take 6 damage for 41–68 gold. Ornate: add Clumsy and receive a hidden random relic.',false,'Quoted gold offer varies.'],
 ['TinkerTime','','None.','Build Mad Science: choose from 2 of 3 card types, then 2 of 3 rider effects for the chosen type; receive the resulting card.',false,'Initial Continue is fixed; type and rider menus are random subsets.'],
 ['TrashHeap','','Every player: HP > 5 (entry threshold is lower than the 8-damage option).','Dive in: take 8 damage for a hidden relic from Darkstone Periapt / Dream Catcher / Hand Drill / Maw Bank / The Boot. Alternative: 100 gold and a random card from its scripted card list.',true,'Fixed initial menu; special-pool relic and card identities are hidden outcomes.'],
 ['Trial','','None.','Accept a random case. Merchant: Regret + 2 random relics OR Shame + 2 selected upgrades. Noble: heal 10 OR Regret + 300 gold. Nondescript: Doubt + 2 card rewards OR Doubt + 2 transforms. Repeated refusal offers run abandonment.',false,'Initial accept/reject is fixed; randomly selected case changes later verdict offers.'],
 ['UnrestSite','','Every player: HP ≤ 70% of max HP.','Rest: heal fully and add Poor Sleep. Kill: lose 8 max HP for a hidden random relic.',true,'Fixed options; healing amount follows current HP.'],
 ['WarHistorianRepy','Act 3 — forced','Lantern Key triggers this event; ordinary IsAllowed always returns false. Key count and multiplayer affect redemption.','Use Lantern Key for History Course or a chest containing 2 random relics and 2 potions. Redeemed keys are removed; extra keys can permit both rewards.',true,'Fixed cage/chest choices conditional on keys/player state. Hidden rewards are excluded.'],
 ['WaterloggedScriptorium','','Every player: gold ≥ 55. The 99-gold option requires sufficient current gold.','Bloody ink: +6 max HP. Pay 55 to enchant 1 card with Steady, or 99 to enchant 2.',true,'Fixed prices; affordability changes locked state.'],
 ['WelcomeToWongos','Act 2','Every player: gold ≥ 100. Featured item requires 200; Mystery Box requires 300.','100 gold: hidden Common relic. 200: displayed Rare relic. 300: ticket yielding 3 random relics after 5 combats. Leave is available; loyalty badge is a scripted bonus.',false,'Featured Rare identity is pulled and displayed on entry, even if declined.'],
 ['Wellspring','','None.','Bottle: receive a hidden random potion. Bathe: remove 1 selected card and add Guilty.',true,'Fixed offered reward types.'],
 ['WhisperingHollow','','Every player: gold ≥ 44.','Pay 26–44 gold for 2 hidden random potions, or take 9 damage to transform 1 selected card.',false,'Quoted potion price is rolled.'],
 ['WoodCarvings','','Every player: a removable Basic card. Snake also requires a card eligible for Slither.','Transform a selected Basic card into Peck or Toric Toughness, or enchant 1 card with Slither.',true,'Fixed transformations and enchantment; targets/locks depend on deck.'],
 ['ZenWeaver','','Every player: gold ≥ 125. Two-card removal requires 250 gold.','Pay 50 for 2 Enlightenment cards, 125 to remove 1 card, or 250 to remove 2.',true,'Fixed prices and services; affordability changes locked state.'],
];

const actLabels = {Overgrowth:'Act 1 — Overgrowth',Underdocks:'Act 1 — Underdocks',Hive:'Act 2 — Hive',Glory:'Act 3 — Glory'};
const pools = new Map();
for (const [act,label] of Object.entries(actLabels)) {
 const text = await fs.readFile(`${root}/sts2-decompiled/MegaCrit.Sts2.Core.Models.Acts/${act}.cs`,'utf8');
 for (const match of text.matchAll(/ModelDb\.Event<(\w+)>/g)) {
  pools.set(match[1], [...(pools.get(match[1]) ?? []),label]);
 }
}
const db = await fs.readFile(`${root}/sts2-decompiled/MegaCrit.Sts2.Core.Models/ModelDb.cs`,'utf8');
const shared = [...db.slice(db.indexOf('AllSharedEvents'),db.indexOf('AllSharedAncients')).matchAll(/\bEvent<(\w+)>/g)].map(x=>x[1]);
const registered = new Set([...pools.keys(),...shared]);
assert.deepEqual(new Set(data.map(x=>x[0])), registered, 'All registered ordinary/forced events must be covered');
assert.equal(data.length,57);
const epochs = new Map();
for(let i=1;i<=3;i++) {
 const text = await fs.readFile(`${root}/sts2-decompiled/MegaCrit.Sts2.Core.Timeline.Epochs/Event${i}Epoch.cs`,'utf8');
 for(const m of text.matchAll(/ModelDb\.Event<(\w+)>/g)) epochs.set(m[1],` Requires Event ${i} epoch unlocked.`);
}
const pretty = id => ({PunchOff:'Punch-Off',ByrdonisNest:'Byrdonis’ Nest',WelcomeToWongos:'Welcome to Wongos',FieldOfManSizedHoles:'Field of Man-Sized Holes'}[id] ?? id.replace(/([a-z])([A-Z])/g,'$1 $2').replace(/\b(Of|The|For|To|And)\b/g,s=>s.toLowerCase()).replace(/^the /,'The '));
const rows = [];
for(const [id,act,condition,content,det,note] of data) {
 const src = await fs.readFile(`${sourceDir}/${id}.cs`,'utf8');
 const line = src.slice(0,src.indexOf('GenerateInitialOptions')).split('\n').length;
 const acts = act || [...new Set(pools.get(id))].join(' / ');
 rows.push([pretty(id),acts,condition+(epochs.get(id)??''),content,det,note,['RelicTrader','WelcomeToWongos'].includes(id),id,`${sourceDir}/${id}.cs:${line}`]);
}
assert.equal(rows.filter(x=>x[6]).length,2);
assert.equal(rows.find(x=>x[7]==='PunchOff')[4],true);
assert.equal(rows.find(x=>x[7]==='WelcomeToWongos')[4],false);

const ancientData = [
 ['Neow','Act 1 start','Normal run; modifiers can replace normal gifts. Gift eligibility uses IsAllowedAtNeow.','Choose 1 of 3 gifts: 2 positive and 1 drawback-bearing. Capsules, Lava Rock and Neow’s Bones can produce later relic rewards.','Random curated gifts; Catfood Mass Audition settings may alter this pool.'],
 ['Darv','Acts 2–3 start','Shared Ancient: must be unlocked and assigned to this act’s candidate subset; not repeated across both acts.','Choose 1 of 3 curated gifts, sometimes including Dusty Tome. Calling Bell produces fixed Common + Uncommon + Rare relics; Black Star grants extra elite relics.','Random gift selection. Ectoplasm/Sozu only Act 2; Pandora’s Box excluded by deck-clearing modifiers.'],
 ['Orobas','Act 2 start — Hive','Must be unlocked; act tutorial/discovery rules can affect Ancient availability.','Choose 1 of 3 offered gifts, drawn one per curated pool. Includes scripted starter-relic upgrades through Touch of Orobas and cross-character gifts.','One random gift per pool; Sea Glass character also varies.'],
 ['Pael','Act 2 start — Hive','Must be unlocked. Claw requires ≥3 Goopy-eligible cards; Tooth ≥5 removable cards; Legion requires no event pet.','Choose 1 of 3 curated body-part gifts. Pael’s Wing later exchanges every 2 sacrificed card rewards for a random relic.','One random gift from each pool, filtered by deck and pet state.'],
 ['Tezcatara','Act 2 start — Hive','Must be unlocked. Nutritious Soup is eligible only with a Basic Strike.','Choose 1 of 3 curated gifts. Toy Box grants 5 temporary random wax relics; one melts every 3 combats.','One random gift per curated pool.'],
 ['Nonupeipe','Act 3 start — Glory','Must be unlocked. Beautiful Bracelet requires ≥4 Swift-eligible cards.','Choose 1 of 3 curated gifts, e.g. Blessed Antler.','Shuffled eligible pool; Catfood Mass Audition settings may alter offers.'],
 ['Tanx','Act 3 start — Glory','Must be unlocked. Tri-Boomerang requires ≥3 Instinct-eligible cards.','Choose 1 of 3 curated gifts; Claws provides optional scripted transformations.','Random 3-gift subset of eligible pool.'],
 ['Vakuu','Act 3 start — Glory','Must be unlocked.','Choose 1 of 3 curated gifts, including drawback-bearing options such as Sere Talon (max-HP cost).','One random gift from each of 3 pools.'],
];
const ancientRows = ancientData.map(([id,act,cond,content,why])=>[id,act,cond,content,false,why,false,id,`${sourceDir}/${id}.cs`]);
const wb = Workbook.create();
const headers=['name','act restriction','other conditions','content','is_deterministic','determinism / state notes','shown ordinary relic at cost','source model','source file'];
function makeSheet(name, title, notes, items) {
 const s=wb.worksheets.add(name);
 s.showGridLines=false;
 s.getRange(`A1:I${items.length+8}`).format.font={name:'Arial',size:11,color:'#263442'};
 s.getRange('A1:F1').merge(); s.getRange('A1').values=[[title]];
 s.getRange('A1').format.font={size:19,bold:true,color:'#17384D'};
 s.getRange('A1:F1').format.rowHeight=32;
 notes.forEach((text,i)=>{ s.getRange(`A${i+2}:F${i+2}`).merge(); s.getRange(`A${i+2}`).values=[[text]]; s.getRange(`A${i+2}:F${i+2}`).format={wrapText:true,rowHeight:30}; });
 const start=8,end=start+items.length;
 s.getRange(`A${start}:I${end}`).values=[headers,...items];
 const table=s.tables.add(`A${start}:I${end}`,true,`${name}Table`); table.showFilterButton=true;
 s.getRange(`A${start}:I${end}`).format={wrapText:true,verticalAlignment:'top'};
 s.getRange(`A${start}:I${start}`).format={fill:'#24495D',font:{bold:true,color:'#FFFFFF'},rowHeight:42};
 const widths=[180,180,320,530,128,370,145,205,520];
 widths.forEach((w,i)=>s.getRangeByIndexes(0,i,end,1).format.columnWidthPx=w);
 for(let i=0;i<items.length;i++) {
  const row=start+i+1;
  // Conservative wrapped line estimate keeps long research notes readable in Excel.
  const lines=Math.max(...items[i].map((v,j)=>Math.ceil(String(v).length/Math.max(12,Math.floor(widths[j]/6.7)))));
  s.getRange(`A${row}:I${row}`).format.rowHeight=Math.max(64,(lines+1)*15);
  s.getRange(`A${row}:I${row}`).format.fill=i%2===1?'#F3F6F8':'#FFFFFF';
  s.getRange(`E${row}`).format.font={bold:true,color:items[i][4]?'#256548':'#9A5A18'};
  if(items[i][6]) s.getRange(`G${row}`).format={fill:'#E1EEE8',font:{bold:true,color:'#256548'}};
 }
 s.freezePanes.freezeRows(start);
 s.freezePanes.freezeColumns(1);
 return s;
}
makeSheet('Events','StS2 event reference • v0.111.0',[
 '57 registered ordinary / forced events. TRUE means fixed event offers across seeds at equivalent player state and the same decision stage—not fixed reward outcomes.',
 'Random item identities or numeric terms shown before commitment count as FALSE. Hidden rewards and reward-screen card grids do not. Later event menus count; initial-only exceptions are noted.',
 'Act numbers are 1-based. “Every player” applies to multiplayer entry checks. Deck selection, affordability locks and percentage-based amounts follow player state.',
 'Filter “shown ordinary relic at cost” = TRUE for Wongos’ featured Rare and Relic Trader. Event/Ancient gifts and scripted special pools are excluded from that question.',
 'Sources: local decompiled event models, ModelDb, act pools and Event1/2/3Epoch. Native EventModel.IsDeterministic is not used. Deprecated/unregistered models are excluded.',
],rows);
makeSheet('Ancients','Ancient encounters • separate from ordinary events',[
 '8 Ancient sources, retained separately for completeness. These named curated gifts are outside the ordinary-pool paid-offer question.',
 'FALSE describes the offered gift identities across runs. A gift that later generates hidden ordinary relics is a separate reward source under the proposed policy.',
 'Act restrictions refer to normal registered act pools, not custom modifiers. Unlocks and initial discovery progression can restrict availability.',
 'Source context: StS2 v0.111.0. Catfood v1.1.2 can change Neow / Nonupeipe pools when Mass Audition is enabled.',
 'Content is an event-level summary, not an exhaustive list of every Ancient gift and its balance effects. Source files contain complete pools.',
],ancientRows);
wb.recalculate();
console.log((await wb.inspect({kind:'table',range:'Events!A8:G12',include:'values',tableMaxRows:5,tableMaxCols:7,maxChars:3000})).ndjson);
console.log((await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:20},summary:'Final error scan'})).ndjson);
await fs.mkdir(out,{recursive:true});
for(const [sheetName,range,file] of [['Events','A1:F12','events-preview.png'],['Ancients','A1:F11','ancients-preview.png'],['Events','A36:G39','trade-preview.png']]) {
 const blob=await wb.render({sheetName,range,scale:1});
 await fs.writeFile(`${out}/${file}`,new Uint8Array(await blob.arrayBuffer()));
}
await (await SpreadsheetFile.exportXlsx(wb)).save(`${out}/StS2_Events.xlsx`);
console.log(JSON.stringify({events:rows.length,ancients:ancientRows.length,paidShown:rows.filter(r=>r[6]).map(r=>r[0]),output:`${out}/StS2_Events.xlsx`}));
