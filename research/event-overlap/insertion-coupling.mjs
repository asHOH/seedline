import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';

function near(actual, expected) {
  assert.ok(Math.abs(actual - expected) < 1e-10, `${actual} != ${expected}`);
}

function factorial(n) {
  let result = 1;
  for (let i = 2; i <= n; i++) result *= i;
  return result;
}

function patterns(oldCount, newCount, prefix = '') {
  if (!oldCount && !newCount) return [prefix];
  return [
    ...(oldCount ? patterns(oldCount - 1, newCount, prefix + 'O') : []),
    ...(newCount ? patterns(oldCount, newCount - 1, prefix + 'N') : []),
  ];
}

const spacingChecks = [];
for (let n = 1; n <= 4; n++) for (let m = 1; m <= 4; m++) {
  const orders = patterns(n, m);
  const rows = orders.map(order => {
    const gaps = order.split('O').map(gap => gap.length);
    const fixedSpacing = factorial(m) / gaps.reduce((product, count) => product * factorial(count), 1) / (n + 1) ** m;
    // All relative orders of independent continuous random priorities are equally likely.
    const randomOldPriorities = factorial(n) * factorial(m) / factorial(n + m);
    const vanilla = 1 / orders.length;
    near(randomOldPriorities, vanilla);
    return { order, fixedSpacing, randomOldPriorities, vanilla };
  });
  near(rows.reduce((sum, row) => sum + row.fixedSpacing, 0), 1);
  const oldFirst = rows.filter(row => row.order.startsWith('O')).reduce((sum, row) => sum + row.fixedSpacing, 0);
  near(oldFirst, (n / (n + 1)) ** m);
  if (m === 1) for (const row of rows) near(row.fixedSpacing, row.vanilla);
  else assert.ok(oldFirst < n / (n + m));
  spacingChecks.push({ n, m, oldFirst, vanillaOldFirst: n / (n + m), rows });
}

function merge(oldIds, newIds, draws) {
  const result = [];
  let oldIndex = 0, newIndex = 0;
  while (oldIndex < oldIds.length || newIndex < newIds.length) {
    const oldLeft = oldIds.length - oldIndex, newLeft = newIds.length - newIndex;
    if (draws[result.length] < newLeft / (oldLeft + newLeft)) result.push(newIds[newIndex++]);
    else result.push(oldIds[oldIndex++]);
  }
  return result;
}

function prioritySort(oldIds, oldDraws, newEntries) {
  const oldPositions = oldDraws.slice(0, oldIds.length).sort((a, b) => a - b);
  return oldIds.map((id, index) => ({ id, priority: oldPositions[index] }))
    .concat(newEntries)
    .sort((a, b) => a.priority - b.priority || a.id - b.id)
    .map(entry => entry.id);
}

assert.deepEqual(merge([0, 1], [4], [0.2, 0.8, 0.7]), [4, 0, 1]);
assert.deepEqual(merge([], [4, 5], [0.2, 0.8]), [4, 5]);
assert.deepEqual(prioritySort([0, 1], [0.8, 0.2], [{ id: 4, priority: 0.5 }]), [0, 4, 1]);
assert.deepEqual(prioritySort([], [], [{ id: 4, priority: 0.5 }]), [4]);

let rngState = 0x63726f73;
function uniform() {
  rngState = (rngState + 0x6d2b79f5) >>> 0;
  let z = Math.imul(rngState ^ (rngState >>> 15), 1 | rngState);
  z ^= z + Math.imul(z ^ (z >>> 7), 61 | z);
  return (((z ^ (z >>> 14)) >>> 0) + 0.5) / 4294967296;
}

const sequences = Array.from({ length: 16 }, (_, mask) => [0, 1, 2, 3].filter(id => mask & (1 << id)));
const pairs = { count: [], identity: [] };
for (let a = 0; a < sequences.length; a++) for (let b = a + 1; b < sequences.length; b++) {
  if (sequences[a].length === sequences[b].length) pairs.identity.push([a, b]);
  else if ((a & b) === a || (a & b) === b) pairs.count.push([a, b]);
}
assert.equal(pairs.count.length, 65);
assert.equal(pairs.identity.length, 27);

function matches(paths, pairList) {
  let first = 0, firstThree = 0;
  for (const [a, b] of pairList) {
    const rounds = Math.min(3, paths[a].length, paths[b].length);
    first += Number(paths[a][0] === paths[b][0]);
    let count = 0;
    for (let index = 0; index < rounds; index++) count += Number(paths[a][index] === paths[b][index]);
    firstThree += count / rounds;
  }
  return [first / pairList.length, firstThree / pairList.length];
}

// shortcut: four ordered old IDs and equal-weight subset pairs;
// add route frequencies before interpreting these rates as gameplay averages.
const trials = 100_000;
const comparisons = [];
for (const newCount of [1, 2, 4]) {
  const sums = { count: [0, 0, 0, 0], identity: [0, 0, 0, 0] };
  const differenceSquares = { count: [0, 0], identity: [0, 0] };
  for (let trial = 0; trial < trials; trial++) {
    const oldDraws = Array.from({ length: 4 }, uniform);
    const newEntries = Array.from({ length: newCount }, (_, index) => ({ id: 4 + index, priority: uniform() }));
    const newIds = [...newEntries].sort((a, b) => a.priority - b.priority || a.id - b.id).map(entry => entry.id);
    const mergeDraws = Array.from({ length: 4 + newCount }, uniform);
    const merged = sequences.map(oldIds => merge(oldIds, newIds, mergeDraws));
    const sorted = sequences.map(oldIds => prioritySort(oldIds, oldDraws, newEntries));
    if (trial < 100) for (let index = 0; index < sequences.length; index++) {
      for (const output of [merged[index], sorted[index]]) {
        assert.deepEqual(output.filter(id => id < 4), sequences[index]);
        assert.deepEqual(output.filter(id => id >= 4), newIds);
        assert.equal(new Set(output).size, output.length);
      }
    }
    for (const kind of ['count', 'identity']) {
      const a = matches(merged, pairs[kind]), b = matches(sorted, pairs[kind]);
      for (let metric = 0; metric < 2; metric++) {
        sums[kind][metric] += a[metric];
        sums[kind][metric + 2] += b[metric];
        differenceSquares[kind][metric] += (b[metric] - a[metric]) ** 2;
      }
    }
  }
  for (const kind of ['count', 'identity']) {
    const means = sums[kind].map(sum => sum / trials);
    const deltas = [0, 1].map(metric => {
      const difference = means[metric + 2] - means[metric];
      const standardError = Math.sqrt(Math.max(0, differenceSquares[kind][metric] / trials - difference ** 2) / (trials - 1));
      return { difference, confidence95: [difference - 1.96 * standardError, difference + 1.96 * standardError] };
    });
    comparisons.push({ newCount, kind, pairs: pairs[kind].length,
      interleaving: { first: means[0], firstThree: means[1] },
      sortedPriorities: { first: means[2], firstThree: means[3] },
      sortedMinusInterleaving: { first: deltas[0], firstThree: deltas[1] },
    });
  }
}

const average = method => comparisons.reduce((sum, row) => sum + row[method].firstThree, 0) / comparisons.length;
const result = {
  exactChecks: 'Gap occupancy probabilities for 16 pool sizes; random sorted old priorities agree with all uniform interleaving pattern probabilities.',
  fixedSpacingExample: spacingChecks.find(row => row.n === 1 && row.m === 2),
  spacingChecks,
  comparison: {
    method: 'Seeded Monte Carlo with shared independent draws by purpose; old-position draws use a shared prefix across counts; new IDs share priorities across runs.',
    scope: 'All subsequences of four old IDs in one shared order. Count cases are proper nested subsets; identity cases have equal lengths. One, two or four new IDs. Pair classes and new-pool sizes receive equal aggregate weight.',
    metric: 'Same event ID at the first output and at corresponding positions among the first min(3, both lengths) outputs; each pair receives equal weight.',
    trialsPerNewCount: trials, comparisons,
    equalWeightFirstThree: { interleaving: average('interleaving'), sortedPriorities: average('sortedPriorities') },
  },
};
writeFileSync(new URL('./insertion-coupling-results.json', import.meta.url), JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify({ fixedSpacingExample: result.fixedSpacingExample, comparison: result.comparison }, null, 2));
