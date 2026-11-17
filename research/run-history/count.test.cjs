const {test} = require('node:test');
const assert = require('node:assert/strict');
const {catalog, cohortReason, knownModels, contentReason, partition, pointResult, analyzeRun, screenRun, wilson, verifySnapshot} = require('./count.cjs');
const cards = {R: {rarity: 'Rare', type: 'Attack'}, P: {rarity: 'Rare', type: 'Power'},
  U: {rarity: 'Uncommon', type: 'Skill'}, C: {rarity: 'Common', type: 'Attack'}, Q: {rarity: 'Uncommon', type: 'Power'}};
const choices = (ids, picked = []) => [...ids].map((id, i) => ({card: {id}, was_picked: picked.includes(i)}));
test('uses the downloaded manifest and rejects stale or damaged data', async t => {
  const fs = require('node:fs'), os = require('node:os'), path = require('node:path'), crypto = require('node:crypto');
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'sts2-snapshot-'));
  t.after(() => fs.rmSync(dir, {recursive: true}));
  const file = path.join(dir, 'runs.jsonl.gz'), manifestFile = path.join(dir, 'source-manifest.json');
  fs.writeFileSync(file, 'new snapshot');
  const manifest = {versions: {'v0.111.0': {bytes: 12, sha256: crypto.createHash('sha256').update('new snapshot').digest('hex')}}};
  fs.writeFileSync(manifestFile, JSON.stringify(manifest));
  assert.deepEqual(await verifySnapshot(file), manifest);
  fs.writeFileSync(file, 'bad snapshot');
  await assert.rejects(verifySnapshot(file), /checksum/);
  fs.writeFileSync(file, 'truncated');
  await assert.rejects(verifySnapshot(file), /byte count/);
  fs.writeFileSync(manifestFile, JSON.stringify({versions: {}}));
  await assert.rejects(verifySnapshot(file), /no v0.111.0 snapshot/);
});
const point = (type, ids) => ({rooms: [{room_type: type}], player_stats: [{player_id: 1, card_choices: choices(ids)}]});
function run() { return {build_id: 'v0.111.0', ascension: 10, game_mode: 'standard', modifiers: [], acts: ['ACT.OVERGROWTH', 'ACT.HIVE', 'ACT.GLORY'],
  players: [{id: 1, character: 'CHARACTER.IRONCLAD', relics: []}], map_point_history: [[point('elite', 'RRC')], [], [point('boss', '')]], win: false}; }

test('cohort includes first A10 Act 3 boss losses and abandons, excludes early runs and custom modes', () => {
  const r = run(); assert.equal(cohortReason(r), null);
  r.was_abandoned = true; assert.equal(cohortReason(r), null);
  r.map_point_history[2] = [point('monster', '')]; assert.equal(cohortReason(r), 'before_act3_boss');
  r.game_mode = 'daily'; assert.equal(cohortReason(r), 'mode_or_modifiers');
});
test('counts two rares in one offer, keeps adjacent offers and rerolls separate', () => {
  assert.equal(partition(choices('RRC'), cards).lower, true);
  assert.equal(partition(choices('RCURCU', [0, 3]), cards).upper, false);
  assert.equal(partition(choices('RRCUCC', [3]), cards).lower, true);
  assert.equal(partition(choices('RCU', [2]), cards).complete, false);
});
test('White Star guaranteed triples are outside the random-rarity claim', () => {
  assert.equal(partition(choices('RRRRCU', [0, 3]), cards, {whiteStar: true}).upper, false);
  assert.equal(partition(choices('RRRRRC', [0, 3]), cards, {whiteStar: true}).lower, true);
  assert.equal(partition(choices('PRRCCUQ', [0]), cards, {whiteStar: true, candy: true}).upper, false);
});
test('Egg cloning does not create a second offered Rare', () => {
  const entries = choices('RRCU', [0]);
  entries[0].card = {id: 'R', floor_added_to_deck: 8, current_upgrade_level: 1};
  entries[1].card.floor_added_to_deck = 8;
  const result = pointResult('elite', {card_choices: entries}, cards, new Set(['RELIC.MOLTEN_EGG']), false);
  assert.equal(result.complete, true); assert.equal(result.upper, false);
});
test('Candy supplemental Power is excluded; ambiguous selection stays uncertain', () => {
  assert.equal(partition(choices('RUCP'), cards, {candy: true}).upper, false);
  assert.equal(partition(choices('RRCQ'), cards, {candy: true}).lower, true);
  const uncertain = partition(choices('PRCQ', [0]), cards, {candy: true});
  assert.equal(uncertain.lower, false); assert.equal(uncertain.upper, true);
});
test('Brain Leech adds the chosen card once from cards_gained', () => {
  const p = {card_choices: choices('RCUU'), cards_gained: [{id: 'R'}]};
  assert.equal(pointResult('brain_leech', p, cards, new Set(), false).lower, true);
  p.cards_gained.push({id: 'R'});
  assert.equal(pointResult('brain_leech', p, cards, new Set(['RELIC.BING_BONG']), false).lower, true);
  p.cards_gained = []; assert.equal(pointResult('brain_leech', p, cards, new Set(), false).complete, false);
});
test('Orrery excludes trailing shop stock, including two rare shop cards', () => {
  const p = {card_choices: choices('CUCCUCCUCCUCCUCRR')};
  assert.equal(pointResult('orrery', p, cards, new Set(), true).upper, false);
  assert.equal(pointResult('orrery', {card_choices: choices('RRC')}, cards, new Set(), true).complete, false);
});
test('missing and unknown cards are uncertain; source identification uses actual rooms', () => {
  assert.equal(pointResult('elite', {}, cards, new Set(), false).upper, true);
  assert.equal(pointResult('elite', {card_choices: choices('RXC')}, cards, new Set(), false).complete, false);
  const r = run(); r.map_point_history[0][0].map_point_type = 'unknown';
  assert.equal(analyzeRun(r, cards).lower, true);
});
test('run incidence counts a run once and trial is selected by the actual option key', () => {
  const r = run(); r.map_point_history[0].push(point('elite', 'RRC'));
  const t = point('event', 'RCURCU');
  t.player_stats[0].event_choices = [{title: {key: 'TRIAL.pages.NONDESCRIPT.options.GUILTY.title'}}];
  r.map_point_history[0].push(t);
  const result = analyzeRun(r, cards); assert.equal(result.lower, true);
  assert.equal(result.points.filter(p => p.lower).length, 2);
  assert.equal(result.points.at(-1).source, 'trial'); assert.equal(result.points.at(-1).upper, false);
});
test('local card metadata and Wilson interval sanity checks', () => {
  const c = catalog(); assert.equal(c['CARD.ACROBATICS'].rarity, 'Uncommon');
  assert.equal(c['CARD.ADRENALINE'].rarity, 'Rare');
  assert.equal(c['CARD.EXPECT_A_FIGHT'].rarity, 'Uncommon');
  const [lo, hi] = wilson(30, 10000); assert(lo < 0.003 && hi > 0.003);
  assert(wilson(0, 100)[1] > 0); assert.equal(wilson(0, 0), null);
});
test('vanilla-compatible screen catches explicit mod fields and foreign content', () => {
  const known = knownModels();
  assert.equal(contentReason('{"id":"CARD.EXPECT_A_FIGHT"}', known), null);
  assert.equal(contentReason('{"id":"CARD.MOD_CARD"}', known), 'unknown_content:CARD.MOD_CARD');
  assert.equal(contentReason('{"BaseLibCardModifiers":[]}', known), 'mod_serialization');
});
test('three natural elite Rares violate A7+ reset odds; fixed-odds triples can contain three', () => {
  assert.equal(pointResult('elite', {card_choices: choices('RRR')}, cards, new Set(), false).incompatible, true);
  assert.equal(pointResult('lost_coffer', {card_choices: choices('RRR')}, cards, new Set(), false).lower, true);
});
test('mixed Neow history separates a picked Coffer triple from Tablet skips', () => {
  const p = {card_choices: choices('RRUCC', [2])};
  assert.equal(pointResult('lost_coffer', p, cards, new Set(['RELIC.NEOWS_BONES', 'RELIC.HEFTY_TABLET']), false).upper, false);
});
test('Toy Box White Star melts before the third combat reward', () => {
  const r = run(), toy = point('event', '');
  toy.player_stats[0].relic_choices = ['TOY_BOX', 'WHITE_STAR', 'ANCHOR', 'VAJRA', 'PEN_NIB', 'KUNAI'].map(id => ({choice: 'RELIC.' + id, was_picked: true}));
  r.map_point_history[0] = [toy, point('monster', 'CCU'), point('monster', 'CCU'), point('elite', 'CCU')];
  assert.equal(analyzeRun(r, cards).upper, false);
  r.map_point_history[0][3] = point('elite', 'RRC');
  assert.equal(analyzeRun(r, cards).lower, true);
});

const screenCards = Object.fromEntries(Object.entries({...cards, F: {rarity: 'Common', type: 'Attack', pools: ['DEFECT']},
  L: {rarity: 'Uncommon', type: 'Skill', pools: ['COLORLESS']}, S: {rarity: 'Status', type: 'Status'},
  M: {rarity: 'Rare', type: 'Attack', multiplayerOnly: true}}).map(([id, c]) => [id, {pools: ['IRONCLAD'], ...c}]));
const known = knownModels();
function screenedRun() {
  const r = run(), neow = point('event', '');
  neow.rooms[0].model_id = 'EVENT.NEOW';
  Object.assign(neow.player_stats[0], {current_gold: 99, gold_gained: 0});
  r.map_point_history[0] = [neow, point('monster', 'RCU')];
  return r;
}
const obtain = (p, ...ids) => p.player_stats[0].relic_choices = ids.map(id => ({choice: 'RELIC.' + id, was_picked: true}));

test('content screening covers enchantments, events, monsters and potions', () => {
  for (const id of ['ENCHANTMENT.MOCK_FREE_ENCHANTMENT', 'EVENT.MOD_EVENT', 'MONSTER.MOD_MONSTER', 'POTION.MOD_POTION']) {
    assert.equal(contentReason(JSON.stringify({id}), known), 'unknown_content:' + id);
  }
  assert.equal(contentReason('{"id":"ENCHANTMENT.SWIFT","ui_theme":"custom"}', known), null);
  const c = catalog();
  assert.deepEqual(c['CARD.DOMINATE'].pools, ['IRONCLAD']);
  assert.equal(c['CARD.CRUELTY'].rarity, 'Uncommon');
  assert.equal(c['CARD.MIDNIGHT'].multiplayerOnly, true);
});

test('starting economy and removals are screened equally for positive and negative runs', () => {
  for (const hit of [false, true]) {
    const r = screenedRun(), start = r.map_point_history[0][0], p = start.player_stats[0];
    if (hit) r.map_point_history[0].push(point('elite', 'RRC'));
    assert.equal(screenRun(r, screenCards, known), null);
    obtain(start, 'PRECISE_SCISSORS');
    p.gold_gained = 9999; p.current_gold = 10098;
    assert.equal(screenRun(r, screenCards, known).reason, 'starting_gold');
    p.gold_gained = 0; p.current_gold = 99;
    p.cards_removed = [{id: 'C'}];
    assert.equal(screenRun(r, screenCards, known), null);
    obtain(start, 'LOST_COFFER');
    p.cards_removed.push({id: 'C'});
    assert.equal(screenRun(r, screenCards, known).reason, 'starting_deck_removals');
    p.cards_removed = [];
    obtain(start, 'NEOWS_BONES', 'GOLDEN_PEARL', 'CURSED_PEARL');
    p.gold_gained = 483; p.current_gold = 582;
    assert.equal(screenRun(r, screenCards, known), null);
    obtain(start, 'LARGE_CAPSULE', 'LUCKY_FYSH');
    p.cards_gained = [{id: 'C'}, {id: 'C'}]; p.gold_gained = 30; p.current_gold = 129;
    assert.equal(screenRun(r, screenCards, known), null);
    obtain(start, 'LARGE_CAPSULE', 'LUCKY_FYSH', 'BOWLER_HAT');
    p.gold_gained = 36; p.current_gold = 135;
    assert.equal(screenRun(r, screenCards, known), null);
    start.rooms[0].model_id = 'EVENT.OROBAS';
    assert.equal(screenRun(r, screenCards, known).reason, 'starting_ancient');
  }
});

test('combat pools allow vanilla expansion and stolen-card returns, reject impossible cards', () => {
  const r = screenedRun(), combat = r.map_point_history[0][1];
  combat.player_stats[0].card_choices = choices('FCU');
  assert.equal(screenRun(r, screenCards, known).reason, 'combat_card_pool');
  obtain(r.map_point_history[0][0], 'PRISMATIC_GEM');
  assert.equal(screenRun(r, screenCards, known), null);
  combat.player_stats[0].card_choices = choices('SCU');
  assert.equal(screenRun(r, screenCards, known).reason, 'combat_card_pool');
  combat.player_stats[0].card_choices = choices('MCU');
  assert.equal(screenRun(r, screenCards, known).reason, 'combat_card_pool');
  obtain(r.map_point_history[0][0], 'DINGY_RUG');
  combat.player_stats[0].card_choices = choices('LCU');
  assert.equal(screenRun(r, screenCards, known), null);
  obtain(r.map_point_history[0][0]);
  combat.rooms[0].model_id = 'ENCOUNTER.THIEVING_HOPPER_WEAK';
  combat.player_stats[0].card_choices = choices('RCUF', [3]);
  combat.player_stats[0].cards_removed = [{id: 'F'}];
  assert.equal(screenRun(r, screenCards, known), null);
});

test('starting card additions and transformations require a matching vanilla source', () => {
  const r = screenedRun(), start = r.map_point_history[0][0], p = start.player_stats[0];
  p.cards_gained = [{id: 'C'}];
  assert.equal(screenRun(r, screenCards, known).reason, 'starting_deck_additions');
  obtain(start, 'ARCANE_SCROLL');
  assert.equal(screenRun(r, screenCards, known), null);
  p.cards_transformed = [{original: {id: 'C'}, replacement: {id: 'R'}}];
  assert.equal(screenRun(r, screenCards, known).reason, 'starting_deck_transformations');
  obtain(start, 'NEOWS_BONES', 'ARCANE_SCROLL', 'NEW_LEAF');
  assert.equal(screenRun(r, screenCards, known), null);
  p.cards_gained = [{id: 'C'}, {id: 'U'}, {id: 'C'}]; p.cards_transformed = [];
  obtain(start, 'SCROLL_BOXES');
  assert.equal(screenRun(r, screenCards, known), null);
});

test('boss and White Star rarity checks preserve Candy and separate Hunt rewards', () => {
  const r = screenedRun();
  r.map_point_history[2] = [point('boss', 'RRU')];
  assert.equal(screenRun(r, screenCards, known).reason, 'combat_reward_rarity');
  r.map_point_history[2] = [point('boss', 'RRRRCU')];
  assert.equal(screenRun(r, screenCards, known).reason, 'combat_reward_rarity');
  r.map_point_history[2] = [point('boss', 'RRRRRR')];
  assert.equal(screenRun(r, screenCards, known), null);
  obtain(r.map_point_history[0][0], 'LASTING_CANDY');
  r.map_point_history[2] = [point('boss', 'RRRQ')];
  assert.equal(screenRun(r, screenCards, known), null);
  r.map_point_history[0][1] = point('monster', 'RUCP');
  assert.equal(screenRun(r, screenCards, known), null);
  obtain(r.map_point_history[0][0], 'WHITE_STAR');
  r.map_point_history[2] = [point('boss', 'RRR')];
  r.map_point_history[0][1] = point('elite', 'RRUCCU');
  assert.equal(screenRun(r, screenCards, known).reason, 'combat_reward_rarity');
  r.map_point_history[0][1] = point('elite', 'RRRRRC');
  assert.equal(screenRun(r, screenCards, known), null);
});

test('the documented Spire Delight opt-out is allowed while active or unknown mod choices are excluded', () => {
  const r = screenedRun(), p = r.map_point_history[0][0].player_stats[0];
  p.event_choices = [{title: {table: 'events', key: 'SPIRE_DELIGHT_EVENT_SPIRE_DELIGHT_EVENT.pages.INITIAL.options.NORMAL.title'}}];
  assert.equal(screenRun(r, screenCards, known), null);
  p.event_choices[0].title.key = 'SPIRE_DELIGHT_EVENT_SPIRE_DELIGHT_EVENT.pages.INITIAL.options.COOK.title';
  assert.equal(screenRun(r, screenCards, known).reason, 'unknown_event_choice');
  assert.equal(contentReason('{"id":"RELIC.SPIRE_DELIGHT_RELIC_CHEF_BAG"}', known), 'unknown_content:RELIC.SPIRE_DELIGHT_RELIC_CHEF_BAG');
});
