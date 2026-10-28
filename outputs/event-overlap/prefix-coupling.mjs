import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';

function choose(items, count) {
  if (count === 0) return [[]];
  if (count > items.length) return [];
  return items.flatMap((item, i) => choose(items.slice(i + 1), count - 1).map(tail => [item, ...tail]));
}
const range = n => Array.from({ length: n }, (_, i) => i);
const near = (a, b) => assert.ok(Math.abs(a - b) < 1e-9, `${a} != ${b}`);
function output(oldIds, m, slots) {
  const selected = new Set(slots);
  let old = 0, fresh = 0;
  return range(oldIds.length + m).map(i => selected.has(i) ? `N${fresh++}` : oldIds[old++]);
}

// A common priority for each output position; the m lowest positions receive new IDs.
function slotPairs(a, b, m, visit) {
  if (a.length > b.length) return slotPairs(b, a, m, (y, x, p) => visit(x, y, p));
  const smallLength = a.length + m, bigLength = b.length + m;
  const bigChoices = choose(range(bigLength), m);
  for (const big of bigChoices) {
    const common = big.filter(i => i < smallLength);
    const extras = choose(range(smallLength).filter(i => !common.includes(i)), m - common.length);
    for (const extra of extras) visit(output(a, m, common.concat(extra)), output(b, m, big), 1 / bigChoices.length / extras.length);
  }
}

function slotStats(a, b, m) {
  const survival = Array(Math.min(a.length, b.length) + m).fill(0);
  const marginalA = new Map(), marginalB = new Map();
  let total = 0;
  slotPairs(a, b, m, (x, y, p) => {
    total += p;
    marginalA.set(x.join(','), (marginalA.get(x.join(',')) || 0) + p);
    marginalB.set(y.join(','), (marginalB.get(y.join(',')) || 0) + p);
    for (let i = 0; i < survival.length && x[i] === y[i]; i++) survival[i] += p;
  });
  near(total, 1);
  for (const marginal of [marginalA, marginalB]) for (const p of marginal.values()) near(p, 1 / marginal.size);
  return survival;
}

function mergeStats(a, b, m) {
  const survival = Array(Math.min(a.length, b.length) + m).fill(0);
  function recurse(old, fresh, p) {
    const position = old + fresh;
    if (position >= survival.length) return;
    const left = m - fresh;
    const newA = left / (a.length - old + left), newB = left / (b.length - old + left);
    const sameNew = Math.min(newA, newB), sameOld = Math.min(1 - newA, 1 - newB);
    if (sameNew > 0) {
      survival[position] += p * sameNew;
      recurse(old, fresh + 1, p * sameNew);
    }
    if (sameOld > 0 && a[old] === b[old]) {
      survival[position] += p * sameOld;
      recurse(old + 1, fresh, p * sameOld);
    }
  }
  recurse(0, 0, 1);
  return survival;
}

function prefixBound(a, b, m) {
  const pathsA = choose(range(a.length + m), m).map(s => output(a, m, s));
  const pathsB = choose(range(b.length + m), m).map(s => output(b, m, s));
  return range(Math.min(a.length, b.length) + m).map(i => {
    const distributions = [pathsA, pathsB].map(paths => {
      const p = new Map();
      for (const path of paths) {
        const prefix = path.slice(0, i + 1).join(',');
        p.set(prefix, (p.get(prefix) || 0) + 1 / paths.length);
      }
      return p;
    });
    return [...distributions[0]].reduce((sum, [key, p]) => sum + Math.min(p, distributions[1].get(key) || 0), 0);
  });
}

const example = {
  weightedMerge: mergeStats(['A'], ['A', 'B'], 1),
  sharedSlotPriorities: slotStats(['A'], ['A', 'B'], 1),
  possibleUpperBound: prefixBound(['A'], ['A', 'B'], 1),
};
near(example.weightedMerge[0], 5 / 6);
near(example.weightedMerge[1], 7 / 12);
near(example.sharedSlotPriorities[0], 5 / 6);
near(example.sharedSlotPriorities[1], 2 / 3);

const multipleNewExample = {
  oldA: ['A'], oldB: ['A', 'B'], newCount: 2,
  weightedMerge: mergeStats(['A'], ['A', 'B'], 2),
  sharedSlotPriorities: slotStats(['A'], ['A', 'B'], 2),
  possibleUpperBound: prefixBound(['A'], ['A', 'B'], 2),
};

function permutations(items) {
  if (!items.length) return [[]];
  return items.flatMap((item, i) => permutations(items.filter((_, j) => i !== j)).map(tail => [item, ...tail]));
}
// A different incoming batch can change both the number of slots and how many win.
let varyingBatchFirst = 0;
for (const order of permutations(range(4))) {
  const small = order.filter(i => i < 2).slice(0, 1);
  const big = order.slice(0, 2);
  varyingBatchFirst += Number(small.includes(0) === big.includes(0)) / 24;
}
near(varyingBatchFirst, 5 / 6);

// Independent oracle: enumerate every strict ranking of actual position priorities.
let rankCases = 0, poolConfigurations = 0;
for (let total = 0; total <= 7; total++) {
  const orders = permutations(range(total));
  for (let m = 0; m <= total; m++) for (let smallTotal = m; smallTotal <= total; smallTotal++) {
    const a = range(smallTotal - m).map(i => `O${i}`), b = range(total - m).map(i => `O${i}`);
    const observed = new Map(), predicted = new Map();
    for (const order of orders) {
      const x = output(a, m, order.filter(i => i < smallTotal).slice(0, m));
      const y = output(b, m, order.slice(0, m));
      const key = JSON.stringify([x, y]);
      observed.set(key, (observed.get(key) || 0) + 1 / orders.length);
      assert.deepEqual(x.filter(id => id.startsWith('O')), a);
      assert.deepEqual(y.filter(id => id.startsWith('O')), b);
      assert.deepEqual(x.filter(id => id.startsWith('N')), range(m).map(i => `N${i}`));
      assert.deepEqual(y.filter(id => id.startsWith('N')), range(m).map(i => `N${i}`));
      rankCases++;
    }
    slotPairs(a, b, m, (x, y, p) => {
      const key = JSON.stringify([x, y]);
      predicted.set(key, (predicted.get(key) || 0) + p);
    });
    assert.equal(observed.size, predicted.size);
    for (const [key, p] of observed) near(p, predicted.get(key));
    slotStats(a, b, m);
    poolConfigurations++;
  }
}

// Each row has probability 1/6. Independent randomization of the common C,D
// order extends the example to uniformly shuffled new IDs as well.
const betterPairing = [
  { short: 'CDA', long: 'CDAB' },
  { short: 'CAD', long: 'CADB' },
  { short: 'CAD', long: 'CABD' },
  { short: 'ACD', long: 'ACDB' },
  { short: 'ACD', long: 'ACBD' },
  { short: 'CDA', long: 'ABCD' },
];
const betterSurvival = [0, 0, 0], shortCounts = new Map(), longCounts = new Map();
for (const row of betterPairing) {
  shortCounts.set(row.short, (shortCounts.get(row.short) || 0) + 1);
  longCounts.set(row.long, (longCounts.get(row.long) || 0) + 1);
  for (let i = 0; i < 3 && row.short[i] === row.long[i]; i++) betterSurvival[i] += 1 / 6;
  assert.equal([...row.short].filter(id => id === 'A').join(''), 'A');
  assert.equal([...row.long].filter(id => id === 'A' || id === 'B').join(''), 'AB');
  for (const path of [row.short, row.long]) assert.equal([...path].filter(id => id === 'C' || id === 'D').join(''), 'CD');
}
assert.deepEqual([...shortCounts.values()], [2, 2, 2]);
assert.equal(longCounts.size, 6);
for (const count of longCounts.values()) assert.equal(count, 1);
for (let k = 0; k < 3; k++) near(betterSurvival[k], multipleNewExample.possibleUpperBound[k]);
assert.ok(betterSurvival[1] > multipleNewExample.sharedSlotPriorities[1]);

const sequences = range(16).map(mask => range(4).filter(i => mask & (1 << i)).map(i => `O${i}`));
const comparisons = [], reversals = [];
for (const m of [1, 2, 4]) {
  for (const kind of ['count', 'identity']) {
    let pairs = 0, merge = 0, slots = 0, bound = 0;
    const improved = [0, 0, 0], equal = [0, 0, 0], worse = [0, 0, 0];
    for (let ai = 0; ai < 16; ai++) for (let bi = ai + 1; bi < 16; bi++) {
      const a = sequences[ai], b = sequences[bi];
      if (kind === 'identity' ? a.length !== b.length : a.length === b.length || ((ai & bi) !== ai && (ai & bi) !== bi)) continue;
      const x = mergeStats(a, b, m), y = slotStats(a, b, m), z = prefixBound(a, b, m);
      const length = Math.min(3, x.length);
      pairs++;
      for (let k = 0; k < length; k++) {
        merge += x[k] / length; slots += y[k] / length; bound += z[k] / length;
        assert.ok(y[k] <= z[k] + 1e-9);
        if (y[k] > x[k] + 1e-9) improved[k]++;
        else if (y[k] < x[k] - 1e-9) { worse[k]++; reversals.push({ a, b, m, k: k + 1, merge: x[k], slots: y[k] }); }
        else equal[k]++;
      }
    }
    comparisons.push({ m, kind, pairs, averageFirstThreePrefixSurvival: { merge: merge / pairs, slots: slots / pairs, pairwiseUpperBound: bound / pairs }, improved, equal, worse });
  }
}

const result = {
  example, multipleNewExample,
  independentRankOracle: { maxPositions: 7, poolConfigurations, rankCases, outcome: 'Exact joint probabilities, uniform marginals, and both internal orders passed, including empty pools.' },
  optimalityCounterexample: { rows: betterPairing, probabilityPerRow: '1/6', prefixSurvival: betterSurvival, preservesBothMarginals: true, reachesEveryPrefixUpperBoundForThisPair: true },
  varyingBatchCounterexample: { oldCounts: [1, 2], newCounts: [1, 2], sharedOldHead: true, sharedNewHead: true, firstAgreement: { weightedMerge: 1, sharedSlotPriorities: varyingBatchFirst } },
  method: 'Exact enumeration of shared output-slot priority order effects; analytical shared-threshold merging; uniform marginal assertions for every pair.',
  scope: 'All subsets of four old IDs in their common order, with one, two, or four identical ordered new IDs. Count pairs are nested subsets; identity pairs have equal lengths. Prefix survival through at most three outputs.',
  comparisons, reversals,
};
writeFileSync(new URL('./prefix-coupling-results.json', import.meta.url), JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify(result, null, 2));
