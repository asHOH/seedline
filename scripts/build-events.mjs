import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
const runtimeRequire=createRequire(path.resolve(path.dirname(process.execPath),'../package.json'));
const { FileBlob, SpreadsheetFile }=await import(pathToFileURL(runtimeRequire.resolve('@oai/artifact-tool')).href);

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const out = `${root}/research/event-workbook`;
const sourceRoot = `${root}/sts2-decompiled`;
const sourceDir = `${sourceRoot}/MegaCrit.Sts2.Core.Models.Events`;
const workbookPath = `${root}/docs/StS2_Events.xlsx`;
const sourceCitation = id => `StS2 v0.113.1: MegaCrit.Sts2.Core.Models.Events.${id}`;
// Columns: source model, act override, entry conditions, content, deterministic, explanation.
const data = [
 ['AbyssalBaths','Act 1 — Underdocks','None.','Immerse: +2 max HP, then 3 damage; repeat with damage increasing by 1 each time, or exit. Abstain: heal 10.',true,'Fixed choices and repeat-cost rule.'],
 ['Amalgamator','Act 2 — Hive','At least 2 removable Basic Strikes and 2 removable Basic Defends.','Combine 2 selected Basic Strikes into Ultimate Strike, or 2 selected Basic Defends into Ultimate Defend.',true,'Fixed choices; selected deck cards depend on the player.'],
 ['AromaOfChaos','Act 1 — Overgrowth','None.','Transform one selected card into a random card, or upgrade one selected card.',true,'Transformation result is hidden until commitment.'],
 ['BattlewornDummy','Act 3 — Glory','None.','Choose dummy setting 1 / 2 / 3: win within 3 turns for a potion / 2 random upgrades / a random relic. The dummy does not attack; timeout gives no reward.',true,'Fixed challenge menu; random rewards do not change the offers.'],
 ['BrainLeech','Acts 1–2','None.','Share knowledge: choose 1 of 5 generated character cards. Rip: take 5 damage for a colorless card reward.',true,'Fixed event options; generated reward-screen cards are excluded.'],
 ['Bugslayer','Act 2 — Hive','None.','Receive Exterminate or Squash.',true,'Two fixed named cards.'],
 ['ByrdonisNest','Act 1 — Overgrowth','None.','Eat: +7 max HP. Take: add Byrdonis Egg.',true,'Fixed options. Code requires no event pet, including Byrdonis’ Egg; treated as None under the ordinary-run assumption of no earlier pet.'],
 ['ColorfulPhilosophers','Act 2 — Hive','None.','Choose another character’s color; receive Common, Uncommon and Rare card rewards from that color (3 choices per reward).',false,'Offers up to 3 other colors; random subset when >3 qualify. Entry requires >1 unlocked non-Fool character pool and Event 3 epoch (assumed satisfied).'],
 ['ColossalFlower','Act 2 — Hive','HP ≥ 19.','Take 35 gold, or take 5 damage to reach 75 gold; take 6 more damage to reach 135 gold; take 7 more for Pollinous Core instead.',true,'Fixed multi-stage gold/relic ladder.'],
 ['CrystalSphere','Acts 2–3','Gold ≥ 100.','Pay 51–99 gold for 3 reveals, or take Debt for 6 reveals in the crystal-sphere minigame. Board rewards include gold, cards, potions and a hidden random relic.',false,'Random quoted admission price; minigame board also varies. Relic identity is not shown before paying.'],
 ['DenseVegetation','Act 1 — Overgrowth','None.','Trudge: take 8 damage for 61–99 gold. Rest: receive rest-site healing, then fight the event encounter.',false,'Quoted gold amount is rolled; healing depends on player state. Multiplayer entry: every player HP > 8.'],
 ['DollRoom','Act 2 — Hive','None.','Receive 1 random doll free; take 5 damage to choose from 2, or 15 damage to choose from 3. Pool: Daughter of the Wind, Mr Struggles, Bing Bong.',false,'Initial menu is fixed. Later named doll options come from a shuffled special pool; payment precedes those options.'],
 ['DoorsOfLightAndDark','Act 1 — Underdocks','None.','Light: upgrade 1 selected card. Dark: remove 1 selected card.',true,'Fixed choices; both targets are player-selected.'],
 ['DrowningBeacon','Act 1 — Underdocks','None.','Lose 13 max HP for Fresnel Lens, or receive Glowwater Potion.',true,'Fixed scripted relic/potion choices.'],
 ['EndlessConveyor','Act 1 — Underdocks','Gold ≥ 120.','Buy the displayed dish for 40 gold, repeatedly, or initially observe the chef for a random upgrade. Dishes grant healing, max HP, upgrades, transformation, cards or a potion. Golden Fysh instead grants 75 gold; every fifth dish is Seapunk Salad, granting Feeding Frenzy.',false,'Displayed dish and subsequent dish sequence are rolled; health and potion slots filter dishes.'],
 ['FakeMerchant','Acts 2–3','Gold ≥ 100 OR holding Foul Potion.','Buy from 6 displayed counterfeits drawn from 9 Event relics, or leave. Throw Foul Potion to fight: remaining stock plus Fake Merchant’s Rug are awarded.',false,'Displayed counterfeit inventory and shop prices vary. Counterfeits are Event relics, not ordinary-pool offers. Solo only.'],
 ['FieldOfManSizedHoles','Act 2 — Hive','A card eligible for Perfect Fit.','Resist: remove 2 selected cards and add Normality. Enter your hole: enchant 1 selected card with Perfect Fit.',true,'Fixed options.'],
 ['GraveOfTheForgotten','Act 3 — Glory','A card eligible for Soul’s Power.','Confront: enchant 1 card with Soul’s Power and add Decay. Accept: receive Forgotten Soul.',true,'Fixed scripted choices; curse belongs only to Confront.'],
 ['HungryForMushrooms','Act 3 — Glory','None.','Big Mushroom: +20 max HP, but draw 2 fewer on the opening turn. Fragrant Mushroom: take 15 damage and upgrade 2 random cards.',true,'Two fixed named relics; random upgrade targets are outcomes.'],
 ['InfestedAutomaton','Act 2 — Hive','None.','Study: receive a random Power. Touch core: receive a random card with canonical cost 0 (not X).',true,'Fixed pool descriptions; card identity is a hidden reward.'],
 ['JungleMazeAdventure','Act 1 — Overgrowth','None.','Solo quest: take 18 damage for about 150 ± 15 gold. Join forces: gain about 50 ± 15 gold.',false,'Both quoted gold amounts are randomized (float variance, then game conversion). Multiplayer entry: every player HP > 18.'],
 ['LostWisp','Act 2 — Hive','None.','Receive Lost Wisp plus Decay, or take about 60 ± 15 gold.',false,'Quoted gold offer varies; named relic option is fixed.'],
 ['LuminousChoir','Act 1 — Overgrowth','Gold ≥ 149 and available relics in grab bag.','Pay 100–149 gold for a hidden random relic, or remove 2 selected cards and add Spore Mind.',false,'Quoted tribute cost is rolled. Entry uses canonical 149-gold threshold.'],
 ['MorphicGrove','Act 1 — Overgrowth','Gold ≥ 100 and at least 2 transformable cards.','Group: lose all gold and transform 2 selected cards. Loner: +5 max HP.',true,'Fixed rule (“all gold”); current gold and transformed outcomes can differ.'],
 ['PotionCourier','Acts 2–3','None.','Grab potions: receive 3 Foul Potions. Ransack: receive 1 hidden random Uncommon potion.',true,'Fixed offered reward types.'],
 ['PunchOff','Act 1 — Underdocks','Total floor ≥ 6.','Nab: add Injury and receive a random relic. Fight: combat for a random relic and potion.',true,'Fixed choices. The source’s randomized Gold variable is unused in this reward flow.'],
 ['RanwidTheElder','Acts 2–3','Gold ≥ 100, at least 1 potion and at least 1 tradable relic.','Give a displayed owned potion / 100 gold / a displayed owned relic for 1 / 1 / 2 hidden random relics. No normal leave option.',false,'Randomly selected surrendered potion and relic are named before choice; new relics remain hidden.'],
 ['Reflections','Act 3 — Glory','None.','Touch mirror: randomly downgrade up to 2 cards, then randomly upgrade up to 4. Shatter: duplicate the entire deck and add Bad Luck.',true,'Fixed choice rules; random targets are outcomes. Requires Event 2 epoch, assumed unlocked.'],
 ['RelicTrader','Acts 2–3','At least 5 tradable relics.','Choose 1 of 3 displayed exchanges: surrender the named owned relic for the named new ordinary relic. Normally no leave option.',false,'Both trade inputs and new relic identities vary. Three new relics are pulled on entry, before choosing.'],
 ['RoomFullOfCheese','Acts 1–2','None.','Take 14 damage for Chosen Cheese, or choose 2 Common character cards from 8 generated cards.',true,'Initial event menu is fixed; later card reward-grid identities are excluded.'],
 ['RoundTeaParty','Act 3 — Glory','HP ≥ 12.','Enjoy tea: receive Royal Poison (heal fully now; take 4 damage at the start of each combat). Pick fight: take 11 damage for a hidden random relic.',true,'Fixed named/hidden reward choices; healing amount follows current HP.'],
 ['SapphireSeed','Act 1 — Overgrowth','None.','Eat: heal 9 and upgrade 1 selected card. Plant: enchant 1 selected card with Sown.',true,'Fixed choices.'],
 ['SelfHelpBook','Acts 1–3','None.','Read back / passage / whole book: enchant an Attack with Sharp 2 / a Skill with Nimble 2 / a Power with Swift 2.',true,'Fixed options conditional on eligible deck cards; no offer RNG. Options require enchantable Attack / Skill / Power cards; skip if none qualify.'],
 ['SlipperyBridge','Acts 1–3','Total floor > 6 and a removable card.','Remove the displayed random card, or take 3 damage to reroll it. Repeated hold-ons increase damage by 1 and offer another card.',false,'Named removal target is randomly selected on entry and after each hold-on.'],
 ['SpiralingWhirlpool','Act 1 — Underdocks','A card eligible for Spiral.','Enchant 1 selected card with Spiral, or heal 33% of max HP.',true,'Fixed choices; healing follows max HP.'],
 ['SpiritGrafter','Act 2 — Hive','None.','Let it in: heal 25 and receive Metamorphosis. Rejection: upgrade 1 selected card and take 10 damage.',true,'Fixed choices.'],
 ['StoneOfAllTime','Act 2 — Hive','At least 1 potion.','Discard the displayed potion for +10 max HP, or take 6 damage to enchant a card with Vigorous 8.',false,'The surrendered potion is randomly selected and named before choice. Push requires a card eligible for Vigorous.'],
 ['SunkenStatue','Act 1','None.','Receive Sword of Stone, or take 7 damage for about 111 ± 10 gold. Sword later becomes Sword of Jade after 5 elite victories.',false,'Quoted gold offer varies; Sword of Stone is always the same.'],
 ['SunkenTreasury','Act 1 — Underdocks','None.','Take 52–67 gold, or take 303–363 gold and add Greed.',false,'Both quoted gold amounts are rolled.'],
 ['Symbiote','Acts 2–3','None.','Approach: enchant 1 card with Corrupted. Kill with fire: transform 1 selected card.',true,'Fixed menu; eligibility locks and transformation results are separate. Approach requires a card eligible for Corrupted.'],
 ['TabletOfTruth','Act 1 — Overgrowth','None.','Smash: heal 20. Decipher repeatedly: lose 3 / 6 / 12 / 24 max HP for a random upgrade each; final step reduces max HP to 1 and upgrades all eligible cards. Can stop between steps.',true,'Fixed cost ladder; final amount depends on current max HP. Random upgrade targets are outcomes.'],
 ['TeaMaster','Acts 1–2','Gold ≥ 150.','Pay 50 for Bone Tea, pay 150 for Ember Tea, or receive Tea of Discourtesy (adds 2 Dazed next combat).',true,'Fixed scripted relic choices and prices.'],
 ['TheFutureOfPotions','Acts 1–3','At least 2 potions.','Trade one of the first 3 owned potions for an upgraded card reward of the shown rarity and randomly assigned Attack/Skill/Power type. Common/Token potions cannot yield Power; Event maps to Rare.',false,'Card type assigned to each named potion is rolled and shown before commitment.'],
 ['TheLanternKey','Act 2 — Hive','None.','Return key: gain 100 gold. Keep key: fight the Mysterious Knight encounter for Lantern Key, which enables forced War Historian Repy in Act 3.',true,'Fixed event choices.'],
 ['TheLegendsWereTrue','Act 1','A nonempty deck and HP ≥ 10.','Nab the map: receive Spoils Map. Find an exit: take 8 damage for a hidden random potion.',true,'Fixed offered reward types.'],
 ['ThisOrThat','Acts 1–3','None.','Plain: take 6 damage for 41–68 gold. Ornate: add Clumsy and receive a hidden random relic.',false,'Quoted gold offer varies.'],
 ['TinkerTime','Act 3 — Glory','None.','Build Mad Science: choose from 2 of 3 card types, then 2 of 3 rider effects for the chosen type; receive the resulting card.',false,'Initial Continue is fixed; type and rider menus are random subsets.'],
 ['TrashHeap','Act 1 — Underdocks','HP > 5.','Dive in: take 8 damage for a hidden relic from Darkstone Periapt / Dream Catcher / Hand Drill / Maw Bank / The Boot. Alternative: 100 gold and a random card from its scripted card list.',true,'Fixed initial menu; special-pool relic and card identities are hidden outcomes. Entry HP > 5 is below the 8-damage option. Requires Event 1 epoch, assumed unlocked.'],
 ['Trial','Act 3 — Glory','None.','Accept a random case. Merchant: Regret + 2 random relics OR Shame + 2 selected upgrades. Noble: heal 10 OR Regret + 300 gold. Nondescript: Doubt + 2 card rewards OR Doubt + 2 transforms. Repeated refusal offers run abandonment.',false,'Initial accept/reject is fixed; randomly selected case changes later verdict offers.'],
 ['UnrestSite','Act 1 — Overgrowth','HP ≤ 70% of max HP.','Rest: heal fully and add Poor Sleep. Kill: lose 8 max HP for a hidden random relic.',true,'Fixed options; healing amount follows current HP.'],
 ['WarHistorianRepy','Act 3 — forced','Forced by Lantern Key in Act 3; ordinary IsAllowed always returns false.','Use Lantern Key for History Course or a chest containing 2 random relics and 2 potions. Redeemed keys are removed; extra keys can permit both rewards.',true,'Fixed cage/chest choices conditional on keys/player state. Hidden rewards are excluded. Key count and multiplayer affect redemption.'],
 ['WaterloggedScriptorium','Act 1 — Underdocks','Gold ≥ 55.','Bloody ink: +6 max HP. Pay 55 to enchant 1 card with Steady, or 120 to enchant 2.',true,'Fixed prices; affordability changes locked state. The 120-gold option requires sufficient current gold.'],
 ['WelcomeToWongos','Act 2 — Hive','Gold ≥ 100.','100 gold: hidden Common relic. 200: displayed Rare relic. 300: ticket yielding 3 random relics after 5 combats. Leave: downgrade a random upgraded card. Loyalty badge is a scripted bonus.',false,'Featured Rare identity is pulled and displayed on entry, even if declined. Featured item requires 200 gold; Mystery Box requires 300.'],
 ['Wellspring','Act 1 — Overgrowth','None.','Bottle: receive a hidden random potion. Bathe: remove 1 selected card and add Guilty.',true,'Fixed offered reward types.'],
 ['WhisperingHollow','Act 1 — Overgrowth','Gold ≥ 44.','Pay 26–44 gold for 2 hidden random potions, or take 9 damage to transform 1 selected card.',false,'Quoted potion price is rolled.'],
 ['WoodCarvings','Act 1 — Overgrowth','A removable Basic card.','Transform a selected Basic card into Peck or Toric Toughness, or enchant 1 card with Slither.',true,'Fixed transformations and enchantment; targets/locks depend on deck. Snake also requires a card eligible for Slither.'],
 ['ZenWeaver','Act 2 — Hive','Gold ≥ 125.','Pay 25 for 1 Enlightenment card, 125 to remove 1 card, or 250 to remove 2.',true,'Fixed prices and services; affordability changes locked state. Two-card removal requires 250 gold.'],
 ['MuseumOfTorture','Act 2','None.','Take the displayed Obsidian Vice / Thumbscrew / Tormentous Collar and add a hidden random curse, or lose 10 max HP to break out.',false,'Initial Continue is fixed; the later displayed relic is randomly selected from a special Event-relic pool.'],
 ['ShrineOfTheWarlord','','Act 3 Ancient differs from Tanx.','Receive Mighty Weapon, Mighty Shield, or Mighty Wings plus Clumsy.',true,'Fixed named card/relic choices.'],
 ['ThermaeNonupeipe','','Gold > 175, HP ≤ 75% of max HP; Act 3 Ancient differs from Nonupeipe.','Pay 88 gold for Bath Towel; spend all gold for Bath Robes; or take 31 damage and add 3 Guilty for the displayed Nonupeipe gift.',false,'The displayed gift is randomly selected from Nonupeipe’s Ancient relics, excluding Looming Fruit.'],
 ['WaterfallOfObviousSecrets','','None.','Receive Dingy Dagger; receive a hidden random relic and add 2 Guilty; or pay 70 gold for a hidden random character Rare card.',true,'Fixed menu after entering; the 70-gold option requires sufficient gold. Random rewards remain hidden until commitment.'],
 ['WuppyInTrouble','','None.','Help immediately: receive a 60-HP Wuppy and Injury. Be cautious: take 5% of current HP as damage for a 30-HP Wuppy. Ignore: receive a hidden random potion.',true,'Fixed choices; damage follows current HP.'],
];

const actLabels = {Overgrowth:'Act 1 — Overgrowth',Underdocks:'Act 1 — Underdocks',Hive:'Act 2 — Hive',Glory:'Act 3 — Glory'};
const pools = new Map();
for (const [act,label] of Object.entries(actLabels)) {
 const text = await fs.readFile(`${sourceRoot}/MegaCrit.Sts2.Core.Models.Acts/${act}.cs`,'utf8');
 for (const match of text.matchAll(/ModelDb\.Event<(\w+)>/g)) {
  pools.set(match[1], [...(pools.get(match[1]) ?? []),label]);
 }
}
const db = await fs.readFile(`${sourceRoot}/MegaCrit.Sts2.Core.Models/ModelDb.cs`,'utf8');
const shared = [...db.slice(db.indexOf('AllSharedEvents'),db.indexOf('AllSharedAncients')).matchAll(/\bEvent<(\w+)>/g)].map(x=>x[1]);
const registered = new Set([...pools.keys(),...shared]);
assert.deepEqual(new Set(data.map(x=>x[0])), registered, 'All registered ordinary/forced events must be covered');
assert.equal(data.length,62);
data.sort((a,b)=>a[0].localeCompare(b[0]));
const pretty = id => ({PunchOff:'Punch-Off',ByrdonisNest:'Byrdonis’ Nest',WelcomeToWongos:'Welcome to Wongos',FieldOfManSizedHoles:'Field of Man-Sized Holes'}[id] ?? id.replace(/([a-z])([A-Z])/g,'$1 $2').replace(/\b(Of|The|For|To|And|In)\b/g,s=>s.toLowerCase()).replace(/^the /,'The '));
const rows = [];
for(const [id,act,condition,content,det,note] of data) {
 await fs.access(`${sourceDir}/${id}.cs`);
 const acts = act || [...new Set(pools.get(id))].join(' / ');
 rows.push([pretty(id),acts,condition,content,det,note,['RelicTrader','WelcomeToWongos'].includes(id),id,sourceCitation(id)]);
}
assert.equal(rows.filter(x=>x[6]).length,2);
assert.equal(rows.find(x=>x[7]==='PunchOff')[4],true);
assert.equal(rows.find(x=>x[7]==='WelcomeToWongos')[4],false);

const ancientData = [
 ['Neow','Act 1 start','Normal run; modifiers can replace normal gifts. Gift eligibility uses IsAllowedAtNeow.','Choose 1 of 3 gifts: 2 positive and 1 drawback-bearing. Capsules, Lava Rock and Neow’s Bones can produce later relic rewards.','Random curated gifts; Catfood Mass Audition settings may alter this pool.'],
 ['Darv','Acts 2–3 start','Shared Ancient: must be unlocked and assigned to this act’s candidate subset; not repeated across both acts.','Choose 1 of 3 curated gifts, sometimes including Dusty Tome. Calling Bell produces fixed Common + Uncommon + Rare relics; Black Star grants extra elite relics.','Random gift selection. Ectoplasm/Sozu only Act 2; Pandora’s Box excluded by deck-clearing modifiers.'],
 ['Orobas','Act 2 start — Hive','Must be unlocked; act tutorial/discovery rules can affect Ancient availability.','Choose 1 of 3 curated gifts. Includes scripted starter-relic upgrades through Touch of Orobas and cross-character gifts.','One random eligible gift per pool; empty pools refill from combined remaining options. Sea Glass character also varies.'],
 ['Pael','Act 2 start — Hive','Must be unlocked. Claw requires ≥3 Goopy-eligible cards; Tooth ≥5 removable cards; Legion requires no event pet.','Choose 1 of 3 curated body-part gifts. Pael’s Wing later exchanges every 2 sacrificed card rewards for a random relic.','One random eligible gift per pool, filtered by deck and pet state; empty pools refill from combined remaining options.'],
 ['Tezcatara','Act 2 start — Hive','Must be unlocked. Nutritious Soup is eligible only with a Basic Strike.','Choose 1 of 3 curated gifts. Toy Box grants 5 temporary random wax relics; one melts every 3 combats.','One random eligible gift per pool; empty pools refill from combined remaining options.'],
 ['Nonupeipe','Act 3 start — Glory','Must be unlocked. Beautiful Bracelet requires ≥4 Swift-eligible cards.','Choose 1 of 3 curated gifts, e.g. Blessed Antler.','Shuffled eligible pool; Catfood Mass Audition settings may alter offers.'],
 ['Tanx','Act 3 start — Glory','Must be unlocked. Tri-Boomerang requires ≥3 Instinct-eligible cards.','Choose 1 of 3 curated gifts; Claws provides optional scripted transformations.','Random 3-gift subset of eligible pool.'],
 ['Vakuu','Act 3 start — Glory','Must be unlocked.','Choose 1 of 3 curated gifts, including drawback-bearing options such as Sere Talon (max-HP cost).','One random eligible gift per pool; empty pools refill from combined remaining options.'],
];
const ancientRows = ancientData.map(([id,act,cond,content,why])=>[id,act,cond,content,false,why,false,id,sourceCitation(id)]);
const wb = await SpreadsheetFile.importXlsx(await FileBlob.load(workbookPath));
function updateSheet(name, items) {
 const s=wb.worksheets.getItem(name);
 const previous=s.getUsedRange().values.slice(8);
 const byId=new Map(previous.map(row=>[row[7],row]));
 for(const row of items) {
  if(byId.has(row[7])) row[0]=byId.get(row[7])[0];
  assert.equal(typeof row[4],'boolean');
  assert.equal(typeof row[6],'boolean');
 }
 assert(items.length>=previous.length);
 const added=items.length-previous.length;
 if(added) s.tables.items[0].rows.add(null,Array.from({length:added},()=>Array(9).fill(null)));
 const start=8,end=start+items.length;
 const widths=[180,180,320,530,128,370,145,205,520];
 for(let i=0;i<items.length;i++) {
  const row=start+i+1;
  if(i>=previous.length) s.getRange(`A${row}:I${row}`).copyFrom(s.getRange('A9:I9'),'all');
  // Conservative wrapped line estimate keeps long research notes readable in Excel.
  const lines=Math.max(...items[i].map((v,j)=>Math.ceil(String(v).length/Math.max(12,Math.floor(widths[j]/6.7)))));
  s.getRange(`A${row}:I${row}`).format.rowHeight=Math.max(64,(lines+1)*15);
  s.getRange(`A${row}:I${row}`).format.fill=i%2===1?'#F3F6F8':'#FFFFFF';
  s.getRange(`E${row}`).format.font={bold:true,color:items[i][4]?'#256548':'#9A5A18'};
  s.getRange(`G${row}`).format.font={bold:items[i][6],color:items[i][6]?'#256548':'#263442'};
  if(items[i][6]) s.getRange(`G${row}`).format={fill:'#E1EEE8',font:{bold:true,color:'#256548'}};
 }
 s.getRange(`A9:I${end}`).values=items;
 s.getRange('I8').values=[['source citation']];
 // Recreate the table so Excel's column metadata matches the renamed header.
 const table=s.tables.items[0], tableName=table.name, tableStyle=table.style;
 table.delete();
 s.tables.add(`A8:I${end}`,true,tableName).style=tableStyle;
 return s;
}
const events=updateSheet('Events',rows);
events.getRange('A1').values=[['StS2 event reference • v0.113.1']];
events.getRange('A2').values=[['61 ordinary events and forced War Historian Repy. is_deterministic = TRUE means fixed event offers across seeds at equivalent player state and decision stage.']];
events.getRange('A4').values=[['Entry conditions: solo, ordinary runs, permanent unlocks satisfied. Byrdonis’ Nest and Wuppy in Trouble assume no earlier event pet.']];
events.getRange('A6').values=[['Sources: StS2 v0.113.1 event models, ModelDb and act pools. Offer classification is independent of EventModel.IsDeterministic.']];
const ancients=updateSheet('Ancients',ancientRows);
ancients.getRange('A5').values=[['Source context: StS2 v0.113.1. Catfood v1.1.2 can change Neow / Nonupeipe pools when Mass Audition is enabled.']];
ancients.getRange('A6').values=[['Content is an event-level summary. The cited event model classes define the complete gift pools.']];
assert.equal(ancientRows.length,8);
assert.equal(rows.filter(row=>row[7]!=='WarHistorianRepy').length,61);
for(const sheet of [events,ancients]) {
 const values=JSON.stringify(sheet.getUsedRange().values);
 assert(!values.includes('v0.111.0'));
 assert(!/[A-Za-z]:[\\/]|\.(?:cache|tools)[\\/]|file:\/\//.test(values),'Published cells must use portable source citations');
}
wb.recalculate();
console.log((await wb.inspect({kind:'table',range:'Events!A8:G12',include:'values',tableMaxRows:5,tableMaxCols:7,maxChars:3000})).ndjson);
console.log((await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:20},summary:'Final error scan'})).ndjson);
await fs.mkdir(out,{recursive:true});
for(const [sheetName,range,file] of [['Events','A15:G25','event-early-preview.png'],['Events','A28:G34','event-new-preview.png'],['Events','A63:G70','event-late-preview.png']]) {
 const blob=await wb.render({sheetName,range,scale:1});
 await fs.writeFile(`${out}/${file}`,new Uint8Array(await blob.arrayBuffer()));
}
await (await SpreadsheetFile.exportXlsx(wb)).save(`${out}/StS2_Events.xlsx`);
await fs.writeFile(`${out}/event-data.json`,JSON.stringify({events:rows,ancients:ancientRows},null,2));
console.log(JSON.stringify({events:rows.length,ancients:ancientRows.length,paidShown:rows.filter(r=>r[6]).map(r=>r[0]),output:`${out}/StS2_Events.xlsx`}));
