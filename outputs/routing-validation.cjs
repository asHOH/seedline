// Run: node outputs/routing-validation.cjs
// Executable design model, not a game implementation or an engine save/load test.
const assert = require('node:assert/strict');
const {createHash} = require('node:crypto');
const clone = value => JSON.parse(JSON.stringify(value));
const compare = (a, b) => a < b ? -1 : a > b ? 1 : 0;
const scopes = ['upgrade', 'enchant', 'remove', 'departure', 'randomUpgrade', 'randomDowngrade', 'bridge', 'sacrifice'];
const digest = (...address) => createHash('sha256').update(JSON.stringify(address)).digest();
const clock = (...address) => -Math.log((digest(...address).readUIntBE(0, 6) + 0.5) / 2 ** 48);
const progress = {GeneticAlgorithm: 'IncreasedBlock', TheScythe: 'IncreasedDamage', ShowOff: 'IncreasedAim', Dowsing: 'RoomsEntered', Guilty: 'CombatsSeen'};

function occurrence(key, cardKey = key) {
  return {key, provenance: ['offer', key], cardKey,
    clocks: Object.fromEntries(scopes.map(scope => [scope, clock(scope, cardKey, 0)])),
    counts: Object.fromEntries(scopes.map(scope => [scope, 0]))};
}
function card(position, key, id = 'Strike', upgrade = 0) {
  return {position, owner: 0, context: 'deck', kind: 'card', game: {id, upgrade, enchant: null, props: {}},
    acquired: position, logical: occurrence(key)};
}
function equivalence(c) {
  return JSON.stringify([c.owner, c.context, c.kind, c.game.id, ...(c.kind === 'potion' ? [] :
    [c.game.upgrade, c.game.enchant?.id ?? null, c.game.enchant?.amount ?? null,
      c.game.props[progress[c.game.id]] ?? null])]);
}
function priority(scope) {
  return (a, b) => a.clocks[scope] - b.clocks[scope] ||
    compare(a.cardKey, b.cardKey) ||
    compare(a.key, b.key);
}
function renew(logical, scope) {
  logical.clocks[scope] = clock(scope, logical.cardKey, ++logical.counts[scope]);
}
function logicalView(cards) {
  return cards.map(c => ({game: equivalence(c), logical: c.logical})).sort((a, b) => compare(a.logical.key, b.logical.key));
}
function physicalView(cards) {
  return [...cards].sort((a, b) => a.position - b.position);
}

// All participating classes and display positions are frozen before effects run.
function route(cards, clicked, scope, retires = false, eligible = () => true) {
  const selected = new Set(clicked);
  assert.equal(selected.size, clicked.length);
  assert(clicked.every(position => cards.some(c => c.position === position && eligible(c))));
  const classes = new Map();
  for (const c of physicalView(cards).filter(eligible)) {
    const key = equivalence(c);
    if (!classes.has(key)) classes.set(key, []);
    classes.get(key).push(c);
  }
  for (const members of classes.values()) {
    const targets = members.filter(c => selected.has(c.position));
    if (!targets.length) continue;
    const ordered = members.map(c => c.logical).sort(priority(scope));
    const winners = ordered.slice(0, targets.length);
    const remaining = ordered.slice(targets.length);
    const threshold = winners.at(-1).clocks[scope];
    for (const logical of remaining) logical.clocks[scope] -= threshold;
    if (!retires) for (const logical of winners) renew(logical, scope);
    remaining.sort((a, b) => compare(a.key, b.key));
    targets.forEach((c, i) => { c.logical = winners[i]; });
    members.filter(c => !selected.has(c.position)).forEach((c, i) => { c.logical = remaining[i]; });
  }
  return cards.filter(c => selected.has(c.position));
}
function act(cards, clicked, scope, effect, retires = false, eligible) {
  const targets = route(cards, clicked, scope, retires, eligible);
  targets.forEach(effect);
  return retires ? cards.filter(c => !targets.includes(c)) : cards;
}

// Independent countdown oracle: repeatedly elapse time and remove one winner.
function sequentialClockOracle(logicals, k, scope) {
  let remaining = clone(logicals);
  const chosen = [];
  for (let i = 0; i < k; i++) {
    const winner = remaining.reduce((best, c) => priority(scope)(c, best) < 0 ? c : best);
    const elapsed = winner.clocks[scope];
    remaining = remaining.filter(c => c !== winner);
    for (const c of remaining) c.clocks[scope] -= elapsed;
    chosen.push(winner);
  }
  chosen.forEach(c => renew(c, scope));
  return [...chosen, ...remaining].sort((a, b) => compare(a.key, b.key));
}
function permutations(items) {
  return items.length ? items.flatMap((item, i) => permutations(items.filter((_, j) => i !== j)).map(rest => [item, ...rest])) : [[]];
}
function choices(n) {
  const result = [];
  for (let mask = 0; mask < 2 ** n; mask++) result.push(...permutations(Array.from({length: n}, (_, i) => i).filter(i => mask & 1 << i)));
  return result;
}

const tiedKeys = [occurrence('a', 'z'), occurrence('b', 'b'), occurrence('c', 'c')];
tiedKeys.forEach(c => { c.clocks.upgrade = 1; });
for (const order of permutations(tiedKeys)) {
  assert.deepEqual(order.sort(priority('upgrade')).map(c => c.key), ['b', 'c', 'a']);
}

let exhaustiveCases = 0;
for (let n = 1; n <= 4; n++) {
  const indexes = Array.from({length: n}, (_, i) => i);
  const ranks = [...permutations(indexes.map(i => i + 1)), indexes.map(() => 1), indexes.map(i => 1 + i % 2)];
  for (const clocks of ranks) {
    const originals = indexes.map(i => card(i, `copy/${i}`));
    originals.forEach((c, i) => { c.logical.clocks.upgrade = clocks[i]; });
    const baselines = new Map();
    for (const placement of permutations(indexes)) for (const clicks of choices(n)) {
      const cards = clone(originals);
      cards.forEach((c, i) => { c.logical = clone(originals[placement[i]].logical); });
      const before = clone(cards);
      // Collection enumeration differs from the frozen display order.
      cards.reverse();
      act(cards, clicks, 'upgrade', c => { c.game.upgrade++; });
      assert.deepEqual(logicalView(cards).map(c => c.logical), sequentialClockOracle(before.map(c => c.logical), clicks.length, 'upgrade'));
      for (const c of cards) {
        assert.equal(c.game.upgrade, Number(clicks.includes(c.position)));
        assert.equal(c.acquired, c.position);
      }
      if (clicks.length === 0) assert.deepEqual(physicalView(cards), before);
      if (!baselines.has(clicks.length)) baselines.set(clicks.length, clone(logicalView(cards)));
      assert.deepEqual(logicalView(cards), baselines.get(clicks.length));
      exhaustiveCases++;
    }
  }
}

// Mixed classes, scope isolation, retirement, and eligibility boundaries.
for (const scope of ['enchant', 'remove', 'departure']) {
  const originals = [card(0, 'a'), card(1, 'b'), card(2, 'c', 'Defend'), card(3, 'd', 'Defend'), card(4, 'inactive')];
  const baseline = new Map();
  for (const clicks of [[0, 2], [2, 0], [1, 3], [3, 1]]) {
    const cards = clone(originals);
    const survivors = act(cards, clicks, scope, c => { c.game.enchant = {id: 'Swift', amount: 2}; }, scope !== 'enchant', c => c.position !== 4);
    assert.deepEqual(cards[4], originals[4]);
    for (const c of cards) {
      const original = originals.find(o => o.logical.key === c.logical.key).logical;
      assert.equal(c.logical.cardKey, original.cardKey);
      assert.deepEqual(c.logical.provenance, original.provenance);
      for (const other of scopes.filter(s => s !== scope)) {
        assert.equal(c.logical.clocks[other], original.clocks[other]);
        assert.equal(c.logical.counts[other], original.counts[other]);
      }
      assert.equal(c.logical.counts[scope], Number(scope === 'enchant' && clicks.includes(c.position)));
    }
    if (!baseline.has(scope)) baseline.set(scope, clone(logicalView(survivors)));
    assert.deepEqual(logicalView(survivors), baseline.get(scope));
  }
}

// Equivalence must distinguish every settled gameplay field, while preserving metadata.
for (const [id, field] of Object.entries(progress)) {
  const a = card(0, 'a', id), b = card(1, 'b', id);
  a.game.props[field] = b.game.props[field] = 2;
  assert.equal(equivalence(a), equivalence(b));
  b.game.props[field]++;
  assert.notEqual(equivalence(a), equivalence(b));
}
for (const change of [c => c.owner++, c => { c.context = 'storage'; }, c => { c.game.id = 'Defend'; },
  c => c.game.upgrade++, c => { c.game.enchant = {id: 'Swift', amount: 1}; }]) {
  const a = card(0, 'a'), b = clone(a); change(b);
  assert.notEqual(equivalence(a), equivalence(b));
}
const enchanted = card(0, 'a'); enchanted.game.enchant = {id: 'Swift', amount: 1};
const changedAmount = clone(enchanted); changedAmount.game.enchant.amount++;
const changedId = clone(enchanted); changedId.game.enchant.id = 'Nimble';
assert.notEqual(equivalence(enchanted), equivalence(changedAmount));
assert.notEqual(equivalence(enchanted), equivalence(changedId));
const metadata = clone(enchanted); metadata.acquired = 99; metadata.game.props.history = [1, 7];
assert.equal(equivalence(enchanted), equivalence(metadata));
const preserved = [card(0, 'a', 'GeneticAlgorithm'), card(1, 'b', 'GeneticAlgorithm')];
preserved.forEach((c, i) => { c.game.props = {IncreasedBlock: 4, CurrentBlock: 5, history: [i]}; });
const originalProps = preserved.map(c => clone(c.game.props));
act(preserved, [0], 'upgrade', c => { c.game.upgrade++; });
assert.deepEqual(preserved.map(c => c.game.props), originalProps);

// Split, merge, copy, transform, random-target bypass, and replay with recreated objects.
function lifecycle(clickLast, reload) {
  let cards = Array.from({length: 4}, (_, i) => card(i, `life/${i}`));
  const choose = eligible => {
    const options = physicalView(cards).filter(eligible);
    return (clickLast ? options.at(-1) : options[0]).position;
  };
  const checkpoint = () => { if (reload) cards = clone(cards); };
  const logs = [];
  for (let round = 0; round < 8; round++) {
    checkpoint();
    act(cards, [choose(c => c.game.upgrade === 0)], 'upgrade', c => { c.game.upgrade = 1; });
    logs.push(clone(logicalView(cards)));
    // A fixed downgrade merges classes; it retains unused routing clocks.
    cards.forEach(c => { c.game.upgrade = 0; });
    checkpoint();
    act(cards, [choose(() => true)], 'enchant', c => { c.game.enchant = {id: 'Swift', amount: 2}; });
    logs.push(clone(logicalView(cards)));
    cards.forEach(c => { c.game.enchant = null; });
  }
  const beforeCopy = clone(logicalView(cards));
  const source = cards.find(c => c.position === choose(() => true));
  const added = card(4, 'mirror/0'); added.game = clone(source.game);
  cards.push(added);
  assert.deepEqual(logicalView(cards.filter(c => c !== added)), beforeCopy);
  checkpoint();
  const selected = [choose(() => true)];
  cards = act(cards, selected, 'remove', () => {}, true);
  const replacement = card(5, 'transform/0', 'Bash'); cards.push(replacement);
  assert.deepEqual(replacement.logical, occurrence('transform/0'));
  logs.push(clone(logicalView(cards)));
  // An already selected random target never passes through routing.
  const randomTarget = [...cards].sort((a, b) => priority('randomUpgrade')(a.logical, b.logical))[0];
  const beforeRandom = clone(cards.map(c => c.logical));
  randomTarget.game.upgrade++;
  assert.deepEqual(cards.map(c => c.logical), beforeRandom);
  logs.push(clone(logicalView(cards)));
  return logs;
}
const life = lifecycle(false, false);
for (const last of [false, true]) for (const reload of [false, true]) assert.deepEqual(lifecycle(last, reload), life);

// Tooth stores five chosen cards, then reconstructs each while retaining its occurrence.
function tooth(clicked, reload) {
  let cards = Array.from({length: 6}, (_, i) => card(i, `tooth/${i}`, i < 3 ? 'Strike' : 'Defend'));
  const targets = route(cards, clicked, 'upgrade');
  let stored = targets.map(c => ({game: clone(c.game), logical: c.logical, returnPriority: clock('tooth', c.logical.cardKey)}));
  const saved = clone(stored);
  cards = cards.filter(c => !targets.includes(c));
  const returns = [];
  while (stored.length) {
    if (reload) { stored = clone(stored); cards = clone(cards); }
    stored.sort((a, b) => a.returnPriority - b.returnPriority || compare(a.logical.cardKey, b.logical.cardKey));
    const next = stored.shift();
    const returned = card(100 + returns.length, 'unused');
    returned.game = clone(next.game); returned.game.upgrade++;
    returned.logical = next.logical;
    const record = saved.find(c => c.logical.key === returned.logical.key);
    assert.deepEqual(returned.logical, record.logical);
    assert.deepEqual(returned.game, {...record.game, upgrade: record.game.upgrade + 1});
    cards.push(returned); returns.push(clone(logicalView([returned])[0]));
  }
  assert.equal(new Set(cards.map(c => c.logical.key)).size, 6);
  return {returns, state: logicalView(cards)};
}
const toothBaseline = tooth([0, 1, 3, 4, 5], false);
for (const clicked of [[5, 4, 3, 1, 0], [1, 2, 3, 4, 5], [2, 1, 5, 4, 3]]) {
  assert.deepEqual(tooth(clicked, true), toothBaseline);
}

// Same-type departure state aligns even when the clicked trade pays a different type.
const potionBase = [card(0, 'p/a', 'SkillPotion'), card(1, 'p/b', 'SkillPotion'), card(2, 'p/c', 'FirePotion')];
potionBase.forEach((c, i) => { c.kind = 'potion'; c.payoff = ['Attack', 'Skill', 'Power'][i]; });
const departures = [0, 1].map(clicked => {
  const cards = clone(potionBase);
  const paid = cards[clicked].payoff;
  const survivors = act(cards, [clicked], 'departure', () => {}, true);
  assert(!survivors.some(c => c.position === clicked));
  return {paid, state: logicalView(survivors)};
});
assert.notEqual(departures[0].paid, departures[1].paid);
assert.deepEqual(departures[0].state, departures[1].state);

// Exact exchangeable-rank enumeration: no statistical tolerance or RNG-quality claim.
let nestedMatches = 0, duplicateWins = 0;
for (const rank of permutations(['a', 'b', 'c'])) {
  nestedMatches += Number(rank[0] === rank.find(key => key !== 'c'));
  duplicateWins += Number(rank[0] !== 'c');
}
assert.equal(nestedMatches, 4); assert.equal(duplicateWins, 4);
// Independent routing and downstream rankings preserve this one-step marginal law.
const fixedChoiceLaw = {}, routedChoiceLaw = {};
for (const downstream of permutations(['a', 'b', 'c'])) {
  const outcome = chosen => downstream[0] === 'c' ? 'Defend' : downstream[0] === chosen ? 'Strike+' : 'Strike';
  fixedChoiceLaw[outcome('a')] = (fixedChoiceLaw[outcome('a')] ?? 0) + 1;
  for (const selectedOccurrence of ['a', 'b']) {
    const label = outcome(selectedOccurrence);
    routedChoiceLaw[label] = (routedChoiceLaw[label] ?? 0) + 1;
  }
}
for (const label of Object.keys(fixedChoiceLaw)) assert.equal(routedChoiceLaw[label], 2 * fixedChoiceLaw[label]);
// Different eligibility histories invalidate the matched-clock premise.
const history = [card(0, 'a'), card(1, 'b'), card(2, 'c')];
history.forEach((c, i) => { c.logical.clocks.upgrade = [5, 6, 7][i]; });
route(history, [0], 'upgrade', true, c => c.position !== 1);
assert.equal(history[1].logical.clocks.upgrade, 6);
assert.equal(history[2].logical.clocks.upgrade, 2);

// Source-key shuffles follow routed logical associations, independently of physical order.
function sourceShuffle(cards) {
  return [...cards].sort((a, b) => clock('shuffle', 0, a.logical.cardKey) - clock('shuffle', 0, b.logical.cardKey) ||
    compare(a.logical.cardKey, b.logical.cardKey)).map(equivalence);
}

// Synthetic ranks isolate tied-position assignment from the production usefulness ranking.
function processingPositions(cards, rank, direction = 1) {
  return [...cards].sort((a, b) => direction * (rank(a) - rank(b)) || compare(a.logical.key, b.logical.key));
}
const tiedStrikes = [card(0, 'a'), card(1, 'b')];
tiedStrikes.forEach((c, i) => {
  c.logical.cardKey = ['z', 'y'][i];
  c.logical.clocks.exhaust = [0.1, 0.9][i];
});
const higherRank = card(2, 'c', 'Defend', 1);
for (const order of permutations([...tiedStrikes, higherRank])) {
  for (const direction of [1, -1]) {
    const positions = processingPositions(order, c => c.game.upgrade, direction);
    assert.deepEqual(positions.map(c => c.logical.key), direction === 1 ? ['a', 'b', 'c'] : ['c', 'a', 'b']);
    positions.forEach(c => assert.strictEqual(c, order.find(x => x.logical.key === c.logical.key)));
  }
}
for (const order of permutations(tiedStrikes)) {
  // Shuffle priorities draw one Strike; Armaments upgrades it before the other is drawn.
  const cards = clone(order), before = logicalView(cards);
  const positions = processingPositions(cards, () => 0);
  assert.deepEqual(logicalView(cards), before);
  const drawn = positions.map((c, i) => ({c, priority: [0.8, 0.2][i]})).sort((a, b) => a.priority - b.priority);
  drawn[0].c.game.upgrade++;
  const exhausted = [...cards].sort((a, b) => priority('exhaust')(a.logical, b.logical))[0];
  assert.equal(exhausted.logical.key, 'a');
  assert.equal(exhausted.game.upgrade, 0);

  // Freeze unordered hand-entry positions, then assign distinct Oil costs to the same occurrences.
  const hand = clone(order), logicalBefore = logicalView(hand);
  const entries = processingPositions(hand, () => 0);
  assert.deepEqual(logicalView(hand), logicalBefore);
  entries.forEach((c, i) => { c.oilCost = [0, 3][i]; });
  assert.deepEqual(hand.sort((a, b) => compare(a.logical.key, b.logical.key)).map(c => c.oilCost), [0, 3]);
}

// Sources without generation entries allocate addresses after gameplay properties, including Claws inheritance.
const additions = Array.from({length: 6}, (_, i) => card(i, `old/${i}`, i < 4 ? 'Maul' : 'GeneticAlgorithm'));
additions[2].game.upgrade = 1;
additions[3].game.enchant = {id: 'Swift', amount: 2};
additions[4].game.props.IncreasedBlock = 4;
additions[5].game.props.IncreasedBlock = 5;
const additionClasses = [...new Set(additions.map(equivalence))];
let canonicalAdditionCases = 0;
// Test rankings exercise order independence without choosing the production usefulness ranking.
for (const ranked of [additionClasses, [...additionClasses].reverse(), [...additionClasses.slice(2), ...additionClasses.slice(0, 2)]]) {
  const rank = new Map(ranked.map((key, i) => [key, i]));
  function initializeBatch(inputs) {
    const result = clone(inputs);
    [...result].sort((a, b) => rank.get(equivalence(a)) - rank.get(equivalence(b)))
      .forEach((c, i) => { c.logical = occurrence(c.generationEntry ? JSON.stringify(c.generationEntry) : `addition/batch/0/${i}`); });
    return result;
  }
  const baseline = logicalView(initializeBatch(additions));
  for (const order of permutations(additions)) {
    const added = initializeBatch(order);
    assert.deepEqual(logicalView(added), baseline);
    assert.equal(new Set(added.map(c => c.logical.key)).size, additions.length);
    for (const c of added) {
      assert.deepEqual(c.game, additions[c.position].game);
      assert.equal(c.acquired, additions[c.position].acquired);
      assert.equal(c.logical.cardKey, c.logical.key);
      assert(Object.values(c.logical.counts).every(count => count === 0));
    }
    const chosen = [...added].sort((a, b) => priority('bridge')(a.logical, b.logical))[0];
    const expected = baseline.reduce((best, c) => priority('bridge')(c.logical, best.logical) < 0 ? c : best);
    assert.equal(equivalence(chosen), expected.game);
    assert.deepEqual(logicalView(clone(added)), baseline);
    canonicalAdditionCases++;
  }
}

// Freeze duplicate numbers over the whole offer before presentation swaps or picks.
function rewardOffer(ids) {
  const counts = new Map();
  return ids.map((id, entry) => {
    const duplicate = (counts.get(id) ?? 0) + 1;
    counts.set(id, duplicate);
    return {...card(entry, `entry/${entry}`, id), entry, duplicate};
  });
}
const pairPriority = (offerAddress, c, number) => clock('card-key assignment', offerAddress, c.game.id, c.duplicate, number);
function acquireReward(offerAddress, picked, score = pairPriority) {
  const acquired = clone(picked);
  assert.equal(new Set(acquired.map(c => c.logical.key)).size, acquired.length);
  const pairs = acquired.flatMap(c => Array.from({length: acquired.length}, (_, i) =>
    ({c, number: i + 1, priority: score(offerAddress, c, i + 1)})));
  pairs.sort((a, b) => a.priority - b.priority || compare(a.c.game.id, b.c.game.id) ||
    a.c.duplicate - b.c.duplicate || a.number - b.number);
  const usedCards = new Set(), usedNumbers = new Set();
  for (const {c, number} of pairs) {
    if (usedCards.has(c) || usedNumbers.has(number)) continue;
    c.logical = {...occurrence(c.logical.key, `${offerAddress}/${number}`), provenance: c.logical.provenance};
    usedCards.add(c); usedNumbers.add(number);
  }
  assert.equal(usedCards.size, acquired.length);
  return acquired;
}
const keySet = cards => cards.map(c => c.logical.cardKey).sort(compare);
let rewardAcquisitionCases = 0;
// Entries may come from triples, supplements, bundles, or multiple generation batches.
for (const ids of [['A', 'B', 'C', 'Power'], ['Claw', 'Claw', 'Claw', 'B']]) {
  const offered = rewardOffer(ids);
  const before = clone(offered);
  for (const picks of choices(offered.length)) {
    const selected = picks.map(i => offered[i]);
    const baseline = acquireReward('reward/0', selected);
    assert.deepEqual(keySet(baseline), picks.map((_, i) => `reward/0/${i + 1}`));
    for (const order of permutations(offered)) {
      // Vary both collection enumeration and displayed positions, retaining entry identity.
      const displayed = order.map((c, position) => ({...c, position}));
      const acquired = acquireReward('reward/0', displayed.filter(c => picks.includes(c.entry)));
      assert.deepEqual(logicalView(acquired), logicalView(baseline));
      assert.deepEqual(logicalView(clone(acquired)), logicalView(baseline));
      for (const c of acquired) {
        const original = offered[c.entry];
        assert.equal(c.logical.key, original.logical.key);
        assert.deepEqual(c.logical.provenance, original.logical.provenance);
        assert.deepEqual(c.game, original.game);
        assert.equal(c.duplicate, original.duplicate);
        for (const scope of scopes) {
          assert.equal(c.logical.clocks[scope], clock(scope, c.logical.cardKey, 0));
          renew(c.logical, scope);
          assert.equal(c.logical.clocks[scope], clock(scope, c.logical.cardKey, 1));
          assert.equal(c.logical.counts[scope], 1);
        }
      }
      rewardAcquisitionCases++;
    }
  }
  assert.deepEqual(offered, before);
}

// All post-acquisition purposes share correspondence, while their randomness stays separate.
const singleOffer = rewardOffer(['A', 'B', 'C']);
const singleStates = singleOffer.map(c => acquireReward('single', [c])[0].logical);
for (const logical of singleStates) {
  assert.deepEqual(logical.clocks, singleStates[0].clocks);
  for (const purpose of ['tooth', 'bracelet', 'hopper', 'shuffle']) {
    assert.equal(clock(purpose, logical.cardKey), clock(purpose, singleStates[0].cardKey));
  }
}
assert.equal(new Set(['tooth', 'bracelet', 'hopper', 'shuffle'].map(p => clock(p, singleStates[0].cardKey))).size, 4);
const rerolled = acquireReward('single/reroll/1', [singleOffer[0]])[0];
assert.notEqual(rerolled.logical.cardKey, singleStates[0].cardKey);
assert.notDeepEqual(rerolled.logical.clocks, singleStates[0].clocks);
assert.deepEqual(acquireReward('skipped', [], () => assert.fail('Skipped rewards consume no pair priorities')), []);
const seaGlass = acquireReward('sea-glass', rewardOffer(Array.from({length: 15}, (_, i) => `Card${i}`)));
assert.deepEqual(keySet(seaGlass), Array.from({length: 15}, (_, i) => `sea-glass/${i + 1}`).sort(compare));

// Exact tie and greedy-collision fixtures; every completed choice owns the full key prefix.
const pairOffer = rewardOffer(['A', 'B']);
for (const order of permutations(pairOffer)) {
  assert.deepEqual(acquireReward('tie', order, () => 0).sort((a, b) => compare(a.game.id, b.game.id))
    .map(c => c.logical.cardKey), ['tie/1', 'tie/2']);
}
const scores = {A: [4, 1], B: [2, 3]};
const fixtureScore = (offerAddress, c, number) => scores[c.game.id][number - 1];
const alone = acquireReward('fixture', [pairOffer[0]], fixtureScore);
const together = acquireReward('fixture', pairOffer, fixtureScore);
assert.equal(alone[0].logical.cardKey, 'fixture/1');
assert.equal(together[0].logical.cardKey, 'fixture/2');
assert.equal(together[1].logical.cardKey, 'fixture/1');
// Later removal leaves the survivor's key intact; returning an existing occurrence bypasses assignment.
const survivor = act(clone(together), [1], 'remove', () => {}, true)[0];
renew(survivor.logical, 'bridge');
const returned = card(7, 'unused-return');
returned.game = clone(survivor.game);
returned.logical = clone(survivor.logical);
assert.deepEqual(returned.logical, survivor.logical);
assert.equal(returned.logical.cardKey, 'fixture/2');
const copied = card(8, 'copy/source'); copied.game = clone(survivor.game);
assert.equal(copied.logical.cardKey, 'copy/source');
assert.notEqual(copied.logical.cardKey, survivor.logical.cardKey);

// Complete assignment precedes sequential deck-add hooks, which see final keys and clocks.
const hookRecords = together.map(c => ({key: c.logical.cardKey, clock: c.logical.clocks.randomUpgrade}));
assert.deepEqual(hookRecords, [{key: 'fixture/2', clock: clock('randomUpgrade', 'fixture/2', 0)},
  {key: 'fixture/1', clock: clock('randomUpgrade', 'fixture/1', 0)}]);
// A bundle of identical cards keeps all three keys; later equivalent-copy clicks route whole state.
const claws = rewardOffer(['Claw', 'Claw', 'Claw']);
const routedRewards = [0, 1, 2].map(clicked => {
  const acquired = acquireReward('scroll', claws);
  act(acquired, [clicked], 'upgrade', c => { c.game.upgrade++; });
  return {state: logicalView(acquired), shuffle: sourceShuffle(acquired)};
});
assert.deepEqual(routedRewards[0], routedRewards[1]);
assert.deepEqual(routedRewards[0], routedRewards[2]);

console.log(JSON.stringify({
  exhaustiveCases, canonicalAdditionCases, rewardAcquisitionCases,
  checks: ['batch/countdown oracle', 'click and enumeration invariance', 'visible selected positions',
    'ties, cancellation, singleton and whole-class consumption', 'scope isolation and retirement',
    'progress distinctions', 'split/merge lifecycle and model replay', 'copy/transform occurrence lifetime',
    'random-target bypass', 'Tooth reconstruction', 'different-payoff potion departures', 'canonical batch additions including Claws',
    'reward key prefixes, duplicates and acquisition-order invariance', 'all card selection scopes and renewal',
    'pair-priority ties and greedy collisions', 'offer and reroll separation', 'frozen keys after removal and return',
    'assignment before deck-add hooks', 'routed source-key shuffle invariance',
    'tied shuffle positions through upgrade and exhaust', 'tied Oil hand-entry positions'],
  matchedNestedAgreement: `${nestedMatches}/6`, duplicateIdentitySelection: `${duplicateWins}/6`,
  independentOneStepMarginals: {fixedChoiceLaw, routedChoiceLaw},
  limits: ['abstract model only', 'production canonical ranking and full rank-based shuffles are unmodeled',
    'reward offer addresses are supplied by fixtures', 'no game hooks or engine serialization',
    'no production RNG or floating-point portability validation']
}, null, 2));
