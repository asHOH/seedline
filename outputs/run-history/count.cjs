// node outputs/run-history/count.cjs [.cache/run-history/runs-v0.111.0.jsonl.gz]
// Standard library only; references are read-only. Tests: node --test outputs/run-history/count.test.cjs
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const readline = require('node:readline');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const ROOT = path.resolve(__dirname, '../..');
const VERSION = 'v0.111.0';
const slug = s => s.replace(/(?<=[A-Za-z0-9])(?=[A-Z])/g, '_').toUpperCase();

function catalog(root = ROOT) {
  const cards = {};
  const dir = path.join(root, 'sts2-decompiled/MegaCrit.Sts2.Core.Models.Cards');
  for (const file of fs.readdirSync(dir).filter(f => f.endsWith('.cs'))) {
    const text = fs.readFileSync(path.join(dir, file), 'utf8');
    const m = text.match(/: base\([^\n]*CardType\.(\w+), CardRarity\.(\w+)/);
    if (m) cards['CARD.' + slug(file.slice(0, -3))] = {type: m[1], rarity: m[2], pools: [],
      multiplayerOnly: /MultiplayerConstraint\s*=>\s*CardMultiplayerConstraint\.MultiplayerOnly/.test(text)};
  }
  const pools = path.join(root, 'sts2-decompiled/MegaCrit.Sts2.Core.Models.CardPools');
  for (const file of fs.readdirSync(pools).filter(f => f.endsWith('CardPool.cs'))) {
    const text = fs.readFileSync(path.join(pools, file), 'utf8').split('protected override CardModel[] GenerateAllCards()')[1]?.split('public override')[0] || '';
    for (const m of text.matchAll(/ModelDb.Card<(\w+)>/g)) {
      const card = cards['CARD.' + slug(m[1])], pool = file.replace('CardPool.cs', '').toUpperCase();
      if (card && !card.pools.includes(pool)) card.pools.push(pool);
    }
  }
  assert(Object.keys(cards).length > 500, 'Incomplete local card catalog');
  return cards;
}

function cohortReason(r) {
  if (r.build_id !== VERSION) return 'version';
  if (r.players?.length !== 1) return 'multiplayer';
  if (!Number.isInteger(r.ascension) || r.ascension < 7 || r.ascension > 10) return 'ascension';
  if (r.game_mode !== 'standard' || r.modifiers?.length) return 'mode_or_modifiers';
  if (!['IRONCLAD', 'SILENT', 'DEFECT', 'NECROBINDER', 'REGENT'].includes(r.players[0].character?.split('.')[1])) return 'character';
  if (r.acts?.length !== 3 || r.acts[2] !== 'ACT.GLORY') return 'acts';
  if (!r.map_point_history?.[2]?.some(p => p.rooms?.some(x => x.room_type === 'boss'))) return 'before_act3_boss';
  return null;
}

function knownModels() {
  const ids = new Set();
  for (const [category, folder] of [['CARD', 'Cards'], ['RELIC', 'Relics'], ['ENCOUNTER', 'Encounters'],
    ['ACT', 'Acts'], ['EVENT', 'Events'], ['POTION', 'Potions'], ['MONSTER', 'Monsters'],
    ['ENCHANTMENT', 'Enchantments'], ['CHARACTER', 'Characters']]) {
    for (const f of fs.readdirSync(path.join(ROOT, 'sts2-decompiled/MegaCrit.Sts2.Core.Models.' + folder))) {
      if (f.endsWith('.cs')) ids.add(category + '.' + slug(f.slice(0, -3)));
    }
  }
  return ids;
}

function contentReason(line, known) {
  if (line.includes('BaseLibCardModifiers') || /"save_dict_[^" ]+"/.test(line)) return 'mod_serialization';
  for (const m of line.matchAll(/"((?:CARD|RELIC|ENCOUNTER|ACT|EVENT|POTION|MONSTER|ENCHANTMENT|CHARACTER)\.[^" ]+)"/g)) {
    if (!known.has(m[1])) return 'unknown_content:' + m[1];
  }
  return null;
}

const rare = (c, cards) => cards[c.id]?.rarity === 'Rare';
const rareCount = (entries, cards) => entries.filter(e => rare(e.card, cards)).length;
const unknown = reason => ({lower: false, upper: true, complete: false, reason});

// Enumerate only offer boundaries consistent with the log's picked-first ordering.
function partition(entries, cards, {candy = false, whiteStar = false, requireWhiteStar = whiteStar, minRares = 0, maxRares = 3} = {}) {
  const memo = new Map();
  function visit(i) {
    if (i === entries.length) return [{hit: false, star: false}];
    if (memo.has(i)) return memo.get(i);
    const out = [];
    for (const size of candy ? [3, 4] : [3]) {
      const block = entries.slice(i, i + size);
      if (block.length !== size || block.slice(1).some(e => e.was_picked)) continue;
      let baseOffers = [block];
      if (size === 4) {
        // Candy appends its Power; selection moves that card to the front if taken.
        const positions = [3, ...(block[0].was_picked ? [0] : [])].filter(j => cards[block[j].card.id]?.type === 'Power');
        baseOffers = positions.map(j => block.filter((_, k) => k !== j));
      }
      const outcomes = baseOffers.map(b => {
        const n = rareCount(b, cards);
        // White Star and Hunt lack IsFromCombat; Candy modifies only the ordinary reward.
        const star = whiteStar && size === 3 && n === 3;
        return {hit: !star && n >= 2, star, n};
      }).filter(o => o.n >= minRares && (o.star || o.n <= maxRares));
      if (!outcomes.length) continue;
      for (const o of outcomes) for (const tail of visit(i + size)) out.push({hit: o.hit || tail.hit, star: o.star || tail.star});
    }
    // Only four evidence states matter, even with many Hunt rewards.
    const result = [...new Map(out.map(o => [JSON.stringify(o), o])).values()];
    memo.set(i, result);
    return result;
  }
  const variants = visit(0).filter(v => !requireWhiteStar || v.star);
  if (!variants.length) return unknown('unrecognized_offer_boundaries');
  const lower = variants.every(v => v.hit), upper = variants.some(v => v.hit);
  return {lower, upper, complete: true, reason: lower === upper ? 'reconstructed' : 'candy_or_boundaries'};
}

function rewardEntries(p, cards, held) {
  // An Egg can clone a picked card, leaving its pre-upgrade instance in the skipped log.
  const entries = p.card_choices || [];
  return entries.filter(e => e.was_picked || !e.card.floor_added_to_deck || !entries.some(t =>
    t.was_picked && t.card.id === e.card.id && t.card.floor_added_to_deck === e.card.floor_added_to_deck &&
    (t.card.current_upgrade_level || 0) > (e.card.current_upgrade_level || 0) &&
    held.has('RELIC.' + ({Attack: 'MOLTEN_EGG', Skill: 'TOXIC_EGG', Power: 'FROZEN_EGG'}[cards[e.card.id]?.type]))));
}

function pointResult(source, p, cards, held, isShop, newWhiteStar = false) {
  let entries = p.card_choices || [];
  if (entries.some(e => !cards[e.card?.id] || typeof e.was_picked !== 'boolean')) {
    const result = unknown('unknown_card_or_choice_schema');
    if (source === 'elite' && entries.length === 3) result.upper = rareCount(entries, cards) + entries.filter(e => !cards[e.card?.id]).length >= 2;
    return result;
  }
  entries = rewardEntries(p, cards, held);
  if (source === 'brain_leech') {
    let gained = (p.cards_gained || []).filter(c => ['Common', 'Uncommon', 'Rare'].includes(cards[c.id]?.rarity));
    if (held.has('RELIC.BING_BONG') && gained.length === 2 && gained[0].id === gained[1].id) gained = gained.slice(0, 1);
    if (entries.length !== 4 || entries.some(e => e.was_picked) || gained.length !== 1) return unknown('brain_leech_selected_card');
    const hit = rareCount(entries, cards) + Number(rare(gained[0], cards)) >= 2;
    return {lower: hit, upper: hit, complete: true, reason: 'grid_plus_gained'};
  }
  if (!entries.length) return unknown('missing_choices');
  if (source === 'elite') {
    const options = {candy: held.has('RELIC.LASTING_CANDY'), whiteStar: held.has('RELIC.WHITE_STAR'),
      requireWhiteStar: held.has('RELIC.WHITE_STAR') && !newWhiteStar};
    const result = partition(entries, cards, {...options, maxRares: 2});
    if (!result.complete && partition(entries, cards, options).complete) return {...unknown('impossible_elite_rare_count'), incompatible: true};
    return result;
  }
  if (source === 'lost_coffer' && held.has('RELIC.NEOWS_BONES') && entries.length > 3) {
    const variants = [];
    for (let i = 0; i + 3 <= entries.length; i++) {
      const result = partition(entries.slice(i, i + 3), cards);
      if (result.complete) variants.push(result);
    }
    if (variants.length) return {lower: variants.every(v => v.lower), upper: variants.some(v => v.upper), complete: true, reason: 'neow_mixed_boundaries'};
  }
  const expected = source === 'orrery' ? 15 : source === 'trial' ? 6 : 3;
  const variants = [];
  for (let n = expected; n <= expected * (held.has('RELIC.DRIFTWOOD') ? 2 : 1); n += 3) {
    const stock = entries.slice(n);
    if (entries.length < n || (isShop ? stock.length > 7 || stock.some(e => e.was_picked) : stock.length !== 0)) continue;
    const result = partition(entries.slice(0, n), cards);
    if (result.complete) variants.push(result);
  }
  if (!variants.length) return unknown('fixed_offer_count_or_shop_suffix');
  const lower = variants.every(v => v.lower), upper = variants.some(v => v.upper);
  return {lower, upper, complete: true, reason: lower === upper ? 'reconstructed' : 'shop_suffix'};
}

function* runPoints(r) {
  const player = r.players[0], held = new Set();
  let floor = 0, toyCombats = 0, wax = [];
  for (const act of r.map_point_history) for (const point of act) {
    floor++;
    const p = point.player_stats?.find(x => x.player_id === player.id) || (point.player_stats?.length === 1 ? point.player_stats[0] : null);
    if (!p) { yield {floor, p}; continue; }
    const rooms = point.rooms || [];
    // Wax relics remain in inventory after melting; Toy Box melts one before rewards every third combat.
    if (held.has('RELIC.TOY_BOX')) for (const room of rooms.filter(x => ['monster', 'elite', 'boss'].includes(x.room_type))) {
      if (++toyCombats % 3 === 0) {
        while (wax.length && !held.has(wax[0])) wax.shift();
        held.delete(wax.shift());
      }
    }
    const acquired = new Set([...(p.relic_choices || []).filter(x => x.was_picked).map(x => x.choice), ...(p.bought_relics || []),
      ...(player.relics || []).filter(x => x.floor_added_to_deck === floor).map(x => x.id)]);
    for (const id of acquired) held.add(id);
    if (acquired.has('RELIC.TOY_BOX')) {
      const picked = (p.relic_choices || []).filter(x => x.was_picked).map(x => x.choice);
      wax = picked.slice(picked.indexOf('RELIC.TOY_BOX') + 1, picked.indexOf('RELIC.TOY_BOX') + 6);
      toyCombats = 0;
    }
    yield {floor, p, rooms, held, acquired};
    for (const id of p.relics_removed || []) held.delete(id);
  }
}

// These checks apply to every eligible run, including runs without a multi-Rare offer.
function screenRun(r, cards, known) {
  const character = r.players[0].character.split('.')[1];
  for (const {floor, p, rooms, held, acquired} of runPoints(r)) {
    if (!p) return {reason: 'missing_player_stats', floor};
    const reject = (reason, evidence) => ({reason, floor, evidence});
    for (const e of p.event_choices || []) {
      const title = e.title;
      if (title?.table !== 'events' || known.has('EVENT.' + title.key.split('.')[0])) continue;
      // Spire Delight documents this choice as opting out of its gameplay; custom content is still screened.
      if (floor === 1 && title.key === 'SPIRE_DELIGHT_EVENT_SPIRE_DELIGHT_EVENT.pages.INITIAL.options.NORMAL.title') continue;
      return reject('unknown_event_choice', title.key);
    }
    if (floor === 1) {
      if (rooms[0]?.model_id !== 'EVENT.NEOW') return reject('starting_ancient', rooms);
      const cardGold = held.has('RELIC.LUCKY_FYSH') ? 15 * ((p.cards_gained || []).length + (p.cards_transformed || []).length) : 0;
      const goldLimit = ([...acquired].reduce((sum, id) => sum + ({'RELIC.GOLDEN_PEARL': 150,
        'RELIC.CURSED_PEARL': 333, 'RELIC.OLD_COIN': 300}[id] || 0), 0) + cardGold) * (held.has('RELIC.BOWLER_HAT') ? 1.25 : 1);
      if ((p.gold_gained || 0) > goldLimit || p.current_gold !== 99 + (p.gold_gained || 0) - (p.gold_lost || 0) - (p.gold_spent || 0) - (p.gold_stolen || 0)) {
        return reject('starting_gold', {gold: p.current_gold, gained: p.gold_gained, maximumGain: goldLimit});
      }
      const removalLimit = [...acquired].reduce((sum, id) => sum + ({'RELIC.PRECISE_SCISSORS': 1,
        'RELIC.PRECARIOUS_SHEARS': 2, 'RELIC.NEW_LEAF': 1, 'RELIC.LEAFY_POULTICE': 2}[id] || 0), 0);
      if ((p.cards_removed || []).length > removalLimit) return reject('starting_deck_removals', {removed: p.cards_removed, acquired: [...acquired]});
      const transformLimit = Number(acquired.has('RELIC.NEW_LEAF')) + 2 * Number(acquired.has('RELIC.LEAFY_POULTICE'));
      if ((p.cards_transformed || []).length > transformLimit) return reject('starting_deck_transformations', {transformed: p.cards_transformed, acquired: [...acquired]});
      const gainLimit = [...acquired].reduce((sum, id) => sum + ({'RELIC.ARCANE_SCROLL': 1, 'RELIC.LARGE_CAPSULE': 2,
        'RELIC.LEAD_PAPERWEIGHT': 1, 'RELIC.LOST_COFFER': 1, 'RELIC.KALEIDOSCOPE': 2, 'RELIC.HEFTY_TABLET': 2,
        'RELIC.NEOWS_BONES': 1, 'RELIC.NEOWS_SACRIFICE': 1, 'RELIC.NEOWS_TORMENT': 1, 'RELIC.DOWSING_ROD': 1,
        'RELIC.CURSED_PEARL': 1, 'RELIC.SCROLL_BOXES': 3, 'RELIC.ORRERY': 5, 'RELIC.NEW_LEAF': 1,
        'RELIC.LEAFY_POULTICE': 2}[id] || 0), 0) * (held.has('RELIC.BING_BONG') ? 2 : 1);
      if ((p.cards_gained || []).length > gainLimit) return reject('starting_deck_additions', {gained: p.cards_gained, acquired: [...acquired]});
    }
    if (rooms.length !== 1 || !['monster', 'elite', 'boss'].includes(rooms[0].room_type)) continue;
    if ((p.card_choices || []).some(e => !e.card?.id || typeof e.was_picked !== 'boolean')) return reject('choice_schema', p.card_choices);
    let entries = rewardEntries(p, cards, held);
    // SwipePower returns a removed deck card as a separate SpecialCardReward.
    if (rooms[0].model_id?.includes('THIEVING_HOPPER')) {
      for (const stolen of p.cards_removed || []) {
        const i = entries.findLastIndex(e => e.card.id === stolen.id);
        if (i >= 0) entries = entries.filter((_, j) => j !== i);
      }
    }
    const pools = new Set([character]);
    if (held.has('RELIC.PRISMATIC_GEM') || acquired.has('RELIC.SEA_GLASS') || acquired.has('RELIC.KALEIDOSCOPE')) {
      for (const c of ['IRONCLAD', 'SILENT', 'DEFECT', 'NECROBINDER', 'REGENT']) pools.add(c);
    }
    if (held.has('RELIC.DINGY_RUG') || acquired.has('RELIC.LEAD_PAPERWEIGHT')) pools.add('COLORLESS');
    for (const e of entries) {
      const card = cards[e.card.id];
      if (!card || card.multiplayerOnly || !['Common', 'Uncommon', 'Rare'].includes(card.rarity) || !card.pools.some(pool => pools.has(pool))) {
        return reject('combat_card_pool', {room: rooms[0], card: e.card.id, held: [...held]});
      }
    }
    // Pickup offers share the floor log with combat rewards, so rarity checks need an unmixed source.
    if (!entries.length || [...acquired].some(id => ['RELIC.ORRERY', 'RELIC.GLASS_EYE', 'RELIC.LOST_COFFER',
      'RELIC.LEAD_PAPERWEIGHT', 'RELIC.KALEIDOSCOPE', 'RELIC.HEFTY_TABLET', 'RELIC.SEA_GLASS'].includes(id))) continue;
    const room = rooms[0].room_type, candy = held.has('RELIC.LASTING_CANDY');
    const whiteStar = room === 'elite' && held.has('RELIC.WHITE_STAR');
    const valid = partition(entries, cards, {candy, whiteStar, requireWhiteStar: whiteStar && !acquired.has('RELIC.WHITE_STAR'),
      minRares: room === 'boss' ? 3 : 0, maxRares: room === 'monster' ? 1 : room === 'elite' ? 2 : 3});
    if (!valid.complete && partition(entries, cards, {candy}).complete) {
      return reject('combat_reward_rarity', {room: rooms[0], choices: entries, held: [...held]});
    }
  }
  return null;
}

function analyzeRun(r, cards) {
  const points = [];
  for (const {floor, p, rooms, held, acquired} of runPoints(r)) {
    if (!p) { points.push({source: 'unknown', floor, ...unknown('missing_player_stats')}); continue; }
    const choices = (p.event_choices || []).map(x => x.title?.key || '');
    const sources = [];
    if (rooms.some(x => x.room_type === 'elite')) sources.push('elite');
    if (acquired.has('RELIC.ORRERY')) sources.push('orrery');
    if (acquired.has('RELIC.LOST_COFFER')) sources.push('lost_coffer');
    if (choices.some(k => k.includes('BRAIN_LEECH.pages.INITIAL.options.SHARE_KNOWLEDGE'))) sources.push('brain_leech');
    if (choices.some(k => k.includes('TRIAL.pages.NONDESCRIPT.options.GUILTY'))) sources.push('trial');
    if (sources.length) {
      const result = sources.length > 1 ? unknown('multiple_target_sources') : pointResult(sources[0], p, cards, held, rooms.some(x => x.room_type === 'shop'), acquired.has('RELIC.WHITE_STAR'));
      points.push({source: sources.join('+'), floor, ...result,
        ...(result.upper ? {rooms, choices: p.card_choices || [], gained: p.cards_gained || [], held: [...held]} : {})});
    }
  }
  return {lower: points.some(p => p.lower), upper: points.some(p => p.upper), complete: points.every(p => p.complete),
    incompatible: points.some(p => p.incompatible), points};
}

function wilson(k, n) {
  if (!n) return null;
  const z = 1.959963984540054, p = k / n, d = 1 + z * z / n;
  const mid = (p + z * z / (2 * n)) / d, half = z * Math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d;
  return [mid - half, mid + half];
}

async function verifySnapshot(file) {
  const manifest = JSON.parse(fs.readFileSync(path.join(path.dirname(file), 'source-manifest.json'), 'utf8').replace(/^\uFEFF/, ''));
  const expected = manifest.versions[VERSION];
  assert(expected, `Downloaded manifest has no ${VERSION} snapshot`);
  assert.equal(fs.statSync(file).size, expected.bytes, 'Dataset byte count differs from downloaded manifest');
  const hash = crypto.createHash('sha256');
  for await (const chunk of fs.createReadStream(file)) hash.update(chunk);
  assert.equal(hash.digest('hex'), expected.sha256, 'Dataset checksum differs from downloaded manifest');
  return manifest;
}

async function main() {
  const file = path.resolve(process.argv[2] || path.join(ROOT, '.cache/run-history/runs-v0.111.0.jsonl.gz'));
  const manifest = await verifySnapshot(file);
  const expected = manifest.versions[VERSION];
  console.log('Dataset checksum verified');
  const cards = catalog(), known = knownModels(), seen = new Set(), attempts = new Set(), excluded = {}, groups = {}, findings = [], rejected = [], sourceCounts = {}, reasons = {};
  let total = 0, n = 0, lower = 0, upper = 0, incomplete = 0, duplicateAttempts = 0;
  const lines = readline.createInterface({input: fs.createReadStream(file).pipe(zlib.createGunzip()), crlfDelay: Infinity});
  for await (const line of lines) {
    const r = JSON.parse(line); total++;
    if (total % 50000 === 0) console.log(JSON.stringify({scanned: total, qualifying: n, lower, upper}));
    assert(r.run_hash, `Missing run hash at row ${total}`);
    if (seen.has(r.run_hash)) { excluded.duplicate_hash = (excluded.duplicate_hash || 0) + 1; continue; }
    seen.add(r.run_hash);
    const reason = cohortReason(r);
    if (reason) { excluded[reason] = (excluded[reason] || 0) + 1; continue; }
    const content = contentReason(line, known);
    if (content) {
      excluded[content] = (excluded[content] || 0) + 1;
      rejected.push({run_hash: r.run_hash, reason: content});
      continue;
    }
    const attempt = JSON.stringify([r.seed, r.start_time, r.players[0].character]);
    if (attempts.has(attempt)) { duplicateAttempts++; continue; }
    const screening = screenRun(r, cards, known);
    if (screening) {
      excluded[screening.reason] = (excluded[screening.reason] || 0) + 1;
      rejected.push({run_hash: r.run_hash, ...screening});
      continue;
    }
    const result = analyzeRun(r, cards);
    if (result.incompatible) {
      excluded.impossible_elite_rare_count = (excluded.impossible_elite_rare_count || 0) + 1;
      rejected.push({run_hash: r.run_hash, points: result.points.filter(p => p.incompatible)});
      continue;
    }
    attempts.add(attempt);
    n++; lower += +result.lower; upper += +result.upper; incomplete += +!result.complete;
    for (const key of [`A${r.ascension}`, r.players[0].character, r.win ? 'win' : r.was_abandoned ? 'abandoned_at_boss' : 'loss_at_boss']) {
      const g = groups[key] ||= {runs: 0, lower: 0, upper: 0}; g.runs++; g.lower += +result.lower; g.upper += +result.upper;
    }
    for (const p of result.points) {
      const s = sourceCounts[p.source] ||= {locations: 0, lower: 0, upper: 0}; s.locations++; s.lower += +p.lower; s.upper += +p.upper;
      if (p.upper && !p.lower) reasons[p.reason] = (reasons[p.reason] || 0) + 1;
    }
    if (result.upper) findings.push({run_hash: r.run_hash, seed: r.seed, start_time: r.start_time, character: r.players[0].character,
      ascension: r.ascension, win: r.win, lower: result.lower, upper: result.upper, points: result.points.filter(p => p.upper)});
  }
  assert.equal(total, expected.runs, 'Dataset record count differs from downloaded manifest');
  const summary = {version: VERSION, source: 'https://spire-codex.com' + expected.url, generated_at: manifest.generated_at,
    sha256: expected.sha256, cardCatalogSha256: crypto.createHash('sha256').update(JSON.stringify(cards)).digest('hex'),
    total, excluded, duplicateAttempts, cohort: 'solo standard A7-10; entered first Act 3 boss; wins, losses and abandons; screened for vanilla compatibility',
    runs: n, confirmedRuns: lower, possibleRuns: upper, incompleteRuns: incomplete, frequencyBounds: [lower / n, upper / n],
    wilson95Confirmed: wilson(lower, n), groups, sourceCounts, ambiguousReasons: reasons};
  fs.writeFileSync(path.join(__dirname, 'results.json'), JSON.stringify(summary, null, 2) + '\n');
  fs.writeFileSync(path.join(__dirname, 'findings.json'), JSON.stringify(findings, null, 2) + '\n');
  fs.writeFileSync(path.join(__dirname, 'rejected.json'), JSON.stringify(rejected, null, 2) + '\n');
  fs.writeFileSync(path.join(__dirname, 'source-manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
  console.log(JSON.stringify(summary, null, 2));
}
module.exports = {catalog, cohortReason, knownModels, contentReason, partition, pointResult, analyzeRun, screenRun, wilson, verifySnapshot};
if (require.main === module) main().catch(e => { console.error(e); process.exitCode = 1; });
