import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

// shortcut: prescribed player-state histories isolate selection from event effects;
// use recorded routes before estimating gameplay-weighted gains or losses.
const trials = 50_000, rounds = 5;
const methods = ['refresh', 'fixed'];
const metrics = ['prefixLength', 'firstEvent', 'firstThree', 'firstFive', 'orderedThree', 'orderedFive', 'sameOrdinalFive', 'sharedFive', 'reversedSharedOrder'];
let rngState = 0x59dc3021;
function uniform() {
  rngState = (rngState + 0x6d2b79f5) >>> 0;
  let z = Math.imul(rngState ^ (rngState >>> 15), 1 | rngState);
  z ^= z + Math.imul(z ^ (z >>> 7), 61 | z);
  return (((z ^ (z >>> 14)) >>> 0) + 0.5) / 4294967296;
}
const range = n => Array.from({ length: n }, (_, i) => i);
function shuffle(ids) {
  const result = [...ids];
  for (let i = ids.length - 1; i > 0; i--) {
    const j = Math.floor(uniform() * (i + 1));
    [result[i], result[j]] = [result[j], result[i]];
  }
  return result;
}
const predicates = {
  N: () => true, g44: s => s.gold >= 44, g55: s => s.gold >= 55,
  g100: s => s.gold >= 100, g120: s => s.gold >= 120, g125: s => s.gold >= 125,
  g149: s => s.gold >= 149, g150: s => s.gold >= 150,
  h70: s => s.hp <= 56, h5: s => s.hp > 5, h10: s => s.hp >= 10,
  h12: s => s.hp >= 12, h19: s => s.hp >= 19,
  f6: s => s.floor >= 6, f7: s => s.floor > 6,
  p1: s => s.potions >= 1, p2: s => s.potions >= 2,
  r5: s => s.relics >= 5, basics: s => s.basics, soul: s => s.soul,
  merchant: s => s.gold >= 100 || s.foul,
  ranwid: s => s.gold >= 100 && s.potions >= 1 && s.relics >= 1,
};
const pools = [
  { name: 'Overgrowth', counts: { N: 13, g44: 1, g100: 1, g149: 1, g150: 1, h70: 1, h10: 1, f7: 1, p2: 1 }, floor: 3, rareN: true },
  { name: 'Underdocks', counts: { N: 10, g55: 1, g120: 1, g150: 1, h5: 1, h10: 1, f6: 1, f7: 1, p2: 1 }, floor: 3, rareN: true },
  { name: 'Hive', counts: { N: 14, g100: 2, g125: 1, g150: 1, h19: 1, basics: 1, p1: 1, merchant: 1, ranwid: 1, r5: 1, f7: 1, p2: 1 }, floor: 20, rareN: true, incoming: 10 },
  { name: 'Glory', counts: { N: 9, g100: 1, h12: 1, soul: 1, merchant: 1, ranwid: 1, r5: 1, f7: 1, p2: 1 }, floor: 36, incoming: 5 },
];
assert.deepEqual(pools.map(p => Object.values(p.counts).reduce((a, b) => a + b)), [21, 18, 26, 17]);
function history(name, category, values = {}) {
  return { name, category, values };
}
const histories = [
  history('rich-stable', 'stable'), history('poor-stable', 'stable', { gold: 30 }),
  history('mid-stable', 'stable', { gold: 110 }),
  history('gold-growing', 'gold', { gold: [30, 60, 110, 140, 180] }),
  history('purchase-and-recovery', 'gold', { gold: [180, 30, 60, 110, 180] }),
  history('one-gold-dip', 'gold', { gold: [180, 180, 30, 180, 180] }),
  history('injured-stable', 'stable', { hp: 40 }), history('critical-stable', 'stable', { hp: 8 }),
  history('damage-and-healing', 'hp', { hp: [65, 40, 8, 30, 65] }),
  history('empty-inventory', 'inventory', { potions: 0, relics: 0, basics: false, soul: false }),
  history('potions-used', 'inventory', { potions: [2, 1, 0, 0, 2] }),
  history('relic-threshold', 'inventory', { relics: [3, 4, 5, 5, 5] }),
  history('crossing-conditions', 'mixed', { gold: [180, 110, 30, 110, 180], hp: [8, 40, 65, 40, 8], potions: [0, 0, 1, 2, 0], relics: [3, 4, 5, 5, 5], basics: [true, true, false, false, false] }),
  history('foul-potion', 'inventory', { gold: 30, potions: 1, foul: true }),
  history('later-floors', 'floor', { floorOffset: 4 }),
  history('rare-N-rejection', 'rare', { rejectRareN: true }),
];
function states(h, pool) {
  return range(rounds).map(t => {
    const s = { gold: 180, hp: 65, potions: 2, relics: 5, basics: true, soul: true, foul: false, floor: pool.floor + [0, 2, 4, 7, 10][t] };
    for (const [key, value] of Object.entries(h.values)) s[key] = Array.isArray(value) ? value[t] : value;
    s.floor += s.floorOffset ?? 0;
    return s;
  });
}

// Fixed scheduling keeps the weighted condition clocks across event opportunities.
// A fresh exponential increment after each spent entry generates one uniform label shuffle.
function fixedSchedule(counts, exps) {
  const remaining = [...counts], indices = counts.map(() => 0);
  const clocks = counts.map((n, c) => n ? exps[c][0] / n : Infinity), result = [];
  while (remaining.some(Boolean)) {
    let c = 0;
    for (let j = 1; j < counts.length; j++) if (clocks[j] < clocks[c]) c = j;
    result.push(c); remaining[c]--; indices[c]++;
    clocks[c] = remaining[c] ? clocks[c] + exps[c][indices[c]] / remaining[c] : Infinity;
  }
  return result;
}
function simulate(method, ids, eligibility, rareAllowed, exps, rareId = -1) {
  const counts = ids.map(x => x.length), remaining = [...counts], used = counts.map(() => 0), result = [];
  const schedule = method === 'fixed' ? fixedSchedule(counts, exps[0]) : null;
  let cursor = 0;
  for (let t = 0; t < eligibility.length; t++) {
    const indices = counts.map(() => 0);
    const clocks = counts.map((_, c) => remaining[c] ? exps[t][c][0] / remaining[c] : Infinity);
    while (true) {
      let c;
      if (schedule) c = schedule[cursor++];
      else {
        c = 0;
        for (let j = 1; j < counts.length; j++) if (clocks[j] < clocks[c]) c = j;
      }
      assert.ok(c !== undefined && remaining[c] > 0, 'configured horizon stays before wrapping');
      remaining[c]--;
      let selected;
      if (eligibility[t][c]) {
        selected = ids[c][used[c]++];
        assert.ok(selected !== undefined);
        if (selected === rareId && !rareAllowed[t]) selected = undefined;
      }
      if (selected !== undefined) { result.push(selected); break; }
      indices[c]++;
      clocks[c] = remaining[c] ? clocks[c] + exps[t][c][indices[c]] / remaining[c] : Infinity;
    }
  }
  return result;
}
function lcs(a, b) {
  let d = Array(b.length + 1).fill(0);
  for (const x of a) {
    let prev = 0;
    for (let j = 1; j <= b.length; j++) {
      const old = d[j]; d[j] = x === b[j - 1] ? prev + 1 : Math.max(d[j], d[j - 1]); prev = old;
    }
  }
  return d[b.length];
}
function measure(a, b) {
  let prefix = 0, sameOrdinal = 0;
  while (prefix < rounds && a[prefix] === b[prefix]) prefix++;
  for (let t = 0; t < rounds; t++) sameOrdinal += a[t] === b[t];
  const ordered = lcs(a, b), shared = a.filter(x => b.includes(x)).length;
  return [prefix, Number(prefix >= 1), Number(prefix >= 3), Number(prefix >= 5), lcs(a.slice(0, 3), b.slice(0, 3)), ordered, sameOrdinal / rounds, shared, Number(ordered < shared)];
}
function formatResults(value) {
  const output = JSON.stringify(value, null, 2).replace(/\{\n\s*"(?:refresh|mean|fixedGains)": [^{}]+\n\s*\}/g, metric => JSON.stringify(JSON.parse(metric)));
  assert.deepEqual(JSON.parse(output), value, 'compact formatting preserves result values');
  return output + '\n';
}
assert.equal(lcs([0, 1], [1, 0]), 1);
assert.equal(lcs([0, 1], [3, 0, 1]), 2);

function permutations(left, p = [], result = []) {
  if (!left.some(Boolean)) result.push([...p]);
  for (let c = 0; c < left.length; c++) if (left[c]) { left[c]--; p.push(c); permutations(left, p, result); p.pop(); left[c]++; }
  return result;
}
// Independent exact shuffle reference for all two-draw histories of two optional conditions.
const validationCounts = [2, 1, 1], validationOrders = permutations([...validationCounts]);
const validationHistories = range(16).map(x => [x >> 2, x & 3].map(mask => [true, Boolean(mask & 1), Boolean(mask & 2)]));
const expected = validationHistories.map(h => {
  const frequencies = new Map();
  for (const order of validationOrders) {
    let cursor = 0; const used = [0, 0, 0], path = [];
    for (const eligible of h) { while (!eligible[order[cursor]]) cursor++; const c = order[cursor++]; path.push(c * 32 + used[c]++); }
    const key = path.join(','); frequencies.set(key, (frequencies.get(key) ?? 0) + 1 / validationOrders.length);
  }
  return frequencies;
});
const validationTrials = 50_000;
const observed = methods.map(() => validationHistories.map(() => new Map()));
for (let trial = 0; trial < validationTrials; trial++) {
  const exps = range(2).map(() => validationCounts.map(n => range(n).map(() => -Math.log(uniform()))));
  for (let h = 0; h < validationHistories.length; h++) for (let method = 0; method < 2; method++) {
    const result = simulate(methods[method], [[0, 1], [32], [64]], validationHistories[h], [true, true], exps).join(',');
    const f = observed[method][h]; f.set(result, (f.get(result) ?? 0) + 1);
  }
}
for (const model of observed) for (let h = 0; h < expected.length; h++) {
  assert.equal(model[h].size, expected[h].size);
  for (const [key, p] of expected[h]) assert.ok(Math.abs((model[h].get(key) ?? 0) / validationTrials - p) < 6 * Math.sqrt(p * (1 - p) / validationTrials) + 6 / validationTrials);
}

function run(pool, carriedDifference = 0) {
  const keys = Object.keys(pool.counts), counts = Object.values(pool.counts);
  const candidates = histories.map(h => {
    const stateHistory = states(h, pool);
    return { name: h.name, eligibility: stateHistory.map(s => keys.map(key => predicates[key](s))), rareAllowed: stateHistory.map(s => !pool.rareN || !s.rejectRareN) };
  });
  const distinct = new Map();
  for (const s of candidates) {
    const key = JSON.stringify([s.eligibility, s.rareAllowed]);
    if (!distinct.has(key)) distinct.set(key, s);
  }
  const specs = [...distinct.values()];
  // Unequal pools give the two sides different roles, so test both assignments.
  const pairs = carriedDifference
    ? specs.flatMap((_, a) => specs.map((_, b) => [a, b]))
    : specs.flatMap((_, a) => specs.slice(a + 1).map((_, i) => [a, a + i + 1]));
  const sums = pairs.map(() => ({ refresh: Array(metrics.length).fill(0), fixed: Array(metrics.length).fill(0), squares: Array(metrics.length).fill(0) }));
  const aggregateSquares = Array(metrics.length).fill(0);
  for (let trial = 0; trial < trials; trial++) {
    const idsA = counts.map((n, c) => shuffle(range(n).map(i => c * 32 + i)));
    let idsB = idsA;
    if (carriedDifference) {
      const old = shuffle([0, 1, 2, 3]), fresh = shuffle(range(pool.incoming).map(i => i + 4));
      const oldDraws = range(4).map(uniform), newDraws = range(pool.incoming).map(uniform).sort((a, b) => a - b);
      function insert(survivors) {
        return oldDraws.slice(0, survivors.length).sort((a, b) => a - b).map((v, i) => [v, survivors[i]])
          .concat(newDraws.map((v, i) => [v, fresh[i]])).sort((a, b) => a[0] - b[0]).map(x => x[1]);
      }
      idsA[0] = insert(old); idsB = [...idsA]; idsB[0] = insert(old.slice(carriedDifference));
    }
    const rareId = pool.rareN ? (carriedDifference ? 4 : 0) : -1;
    const exps = range(rounds).map(() => counts.map(n => range(n).map(() => -Math.log(uniform()))));
    const paths = methods.map(method => [idsA, idsB].map(ids => specs.map(s => simulate(method, ids, s.eligibility, s.rareAllowed, exps, rareId))));
    const deltas = Array(metrics.length).fill(0);
    for (let i = 0; i < pairs.length; i++) {
      const [a, b] = pairs[i], values = paths.map(p => measure(p[0][a], p[1][b]));
      for (let k = 0; k < metrics.length; k++) {
        sums[i].refresh[k] += values[0][k]; sums[i].fixed[k] += values[1][k];
        const delta = values[1][k] - values[0][k]; sums[i].squares[k] += delta * delta; deltas[k] += delta / pairs.length;
      }
    }
    for (let k = 0; k < metrics.length; k++) aggregateSquares[k] += deltas[k] ** 2;
  }
  function finish(row) {
    return Object.fromEntries(metrics.map((metric, k) => {
      const refresh = row.refresh[k] / trials, fixed = row.fixed[k] / trials, delta = fixed - refresh;
      const se = Math.sqrt(Math.max(0, row.squares[k] / trials - delta * delta) / (trials - 1));
      return [metric, { refresh, fixed, delta, confidence95: [delta - 1.96 * se, delta + 1.96 * se], sixSE: 6 * se }];
    }));
  }
  const rows = pairs.map(([a, b], i) => ({ a: specs[a].name, b: specs[b].name, metrics: finish(sums[i]) }));
  const total = { refresh: Array(metrics.length).fill(0), fixed: Array(metrics.length).fill(0), squares: aggregateSquares };
  for (const row of sums) for (let k = 0; k < metrics.length; k++) for (const method of methods) total[method][k] += row[method][k] / pairs.length;
  const classifications = Object.fromEntries(metrics.map(metric => [metric, {
    fixedGains: rows.filter(r => r.metrics[metric].delta > r.metrics[metric].sixSE + 1e-10).length,
    unresolved: rows.filter(r => Math.abs(r.metrics[metric].delta) <= r.metrics[metric].sixSE + 1e-10).length,
    refreshGains: rows.filter(r => r.metrics[metric].delta < -r.metrics[metric].sixSE - 1e-10).length,
  }]));
  const strongest = Object.fromEntries(['prefixLength', 'orderedFive'].map(metric => {
    const brief = r => ({ a: r.a, b: r.b, metrics: { [metric]: r.metrics[metric] } });
    return [metric, {
      fixed: [...rows].sort((a, b) => b.metrics[metric].delta - a.metrics[metric].delta).slice(0, 2).map(brief),
      refresh: [...rows].sort((a, b) => a.metrics[metric].delta - b.metrics[metric].delta).slice(0, 2).map(brief),
    }];
  }));
  const result = { pool: pool.name, counts: pool.counts, carriedDifference, histories: specs.map(s => s.name), trials, pairs: pairs.length, aggregates: finish(total), classifications, strongest };
  console.log(JSON.stringify({ pool: pool.name, carriedDifference, aggregates: result.aggregates, classifications }));
  return result;
}
export { pools, histories, states, predicates, simulate, shuffle, uniform, range, lcs, measure, formatResults };

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const results = pools.map(p => run(p));
  for (const pool of pools.filter(p => p.incoming)) for (const difference of [1, 3]) results.push(run(pool, difference));
  const output = {
  scope: 'Actual condition-group counts and predicates. Five event opportunities before wrapping. Base runs share ID orders; additional Hive/Glory cases generate different N survivor counts/orders using settled cross-act sorting. Fixed schedule uses weighted clocks retained for the whole act.',
  validation: 'Both samplers matched all 16 small-pool two-opportunity path distributions from exact shuffle enumeration within six standard errors; LCS checks and pre-wrap assertions passed.',
  limitations: 'Histories prescribe player state independently of selected event effects. The rare-N sensitivity rejects one N-group ID; other rare checks pass. Histories receive equal coverage weights, without route-frequency claims. Other previously visited condition groups remain at full availability.',
  metrics, histories, results,
  };
  output.retainedResults = 'All aggregates and classifications; two strongest gains and losses for prefix length and ordered overlap. Routine examples and unrelated metrics in extreme cases omitted.';
  writeFileSync(new URL('./within-act-sequences-results.json', import.meta.url), formatResults(output));
}
