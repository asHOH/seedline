// Exact coupling checks for three fixed, legal replacement relics.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

function gcd(a, b) { while (b) [a, b] = [b, a % b]; return a; }
function fraction(n, d = 1n) {
  if (n === 0n) return [0n, 1n];
  const g = gcd(n, d);
  return [n / g, d / g];
}
const add = (a, b) => fraction(a[0] * b[1] + b[0] * a[1], a[1] * b[1]);
const scale = (a, n, d = 1) => fraction(a[0] * BigInt(n), a[1] * BigInt(d));
const number = a => Number(a[0]) / Number(a[1]);
const exact = a => a.map(String).join('/');
const terminal = () => new Map([[0, [1n, 1n]]]);

function combine(branches, solve) {
  if (!branches.length) return terminal();
  const total = branches.reduce((n, b) => n + b.count, 0);
  const result = new Map();
  for (const b of branches) {
    for (const [key, probability] of solve(b.state)) {
      const next = key + 4 * b.pairs + b.rows;
      const p = scale(probability, b.count, total);
      result.set(next, add(result.get(next) || [0n, 1n], p));
    }
  }
  return result;
}

// State: common rows unused in both, selected only left, selected only right,
// unique-left availability, unique-right availability, free columns left/right.
function directDistribution(common, uniqueLeft, uniqueRight) {
  const memo = new Map();
  function solve(state) {
    const key = state.join(',');
    if (memo.has(key)) return memo.get(key);
    const [a, b, c, u, v, left, right] = state;
    const branches = [];
    for (let type = 0; type < 5; type++) {
      const count = [a, b, c, u, v][type];
      if (!count) continue;
      for (let column = 0; column < 3; column++) {
        const bit = 1 << column;
        const inLeft = !!(left & bit) && [0, 2, 3].includes(type);
        const inRight = !!(right & bit) && [0, 1, 4].includes(type);
        if (!inLeft && !inRight) continue;
        const next = state.slice();
        next[type]--;
        if (type === 0 && inLeft && !inRight) next[1]++;
        if (type === 0 && inRight && !inLeft) next[2]++;
        if (inLeft) next[5] ^= bit;
        if (inRight) next[6] ^= bit;
        branches.push({
          count, state: next,
          pairs: +(inLeft && inRight),
          rows: +(type === 1 || type === 2 || (inLeft && inRight))
        });
      }
    }
    const result = combine(branches, solve);
    memo.set(key, result);
    return result;
  }
  return { distribution: solve([common, 0, 0, uniqueLeft, uniqueRight, 7, 7]), states: memo.size };
}

// First three distinct rows encountered in edge order are the three lowest
// row minima. Processing their edges immediately equals selecting them first.
// Row status: 0 excluded, 1 selected/unmatched, 2 matched. Only active types stay.
const rowTypes = [[1, 1], [1, 2], [2, 1], [1, 0], [0, 1]];
function hybridDistribution(common, uniqueLeft, uniqueRight) {
  const memo = new Map();
  // Unseen common, unseen unique left/right, selected counts left/right,
  // free columns left/right, and counts of the five active row types.
  function solve(state) {
    const key = state.join(',');
    if (memo.has(key)) return memo.get(key);
    const [a, u, v, selectedLeft, selectedRight, left, right] = state;
    const branches = [];
    function retain(next, l, r) {
      const index = rowTypes.findIndex(([x, y]) => x === l && y === r);
      if (index >= 0) next[7 + index]++;
    }
    for (let type = 0; type < 3; type++) {
      const count = [a, u, v][type];
      const chooseLeft = type !== 2 && selectedLeft < 3;
      const chooseRight = type !== 1 && selectedRight < 3;
      if (!count || (!chooseLeft && !chooseRight)) continue;
      for (let column = 0; column < 3; column++) {
        const bit = 1 << column;
        const matchLeft = chooseLeft && !!(left & bit);
        const matchRight = chooseRight && !!(right & bit);
        const next = state.slice();
        next[type]--;
        next[3] += +chooseLeft;
        next[4] += +chooseRight;
        if (matchLeft) next[5] ^= bit;
        if (matchRight) next[6] ^= bit;
        retain(next, chooseLeft ? matchLeft ? 2 : 1 : 0, chooseRight ? matchRight ? 2 : 1 : 0);
        branches.push({ count, state: next, pairs: +(matchLeft && matchRight), rows: +(chooseLeft && chooseRight) });
      }
    }
    for (let type = 0; type < rowTypes.length; type++) {
      const count = state[7 + type];
      if (!count) continue;
      const [l, r] = rowTypes[type];
      for (let column = 0; column < 3; column++) {
        const bit = 1 << column;
        const matchLeft = l === 1 && !!(left & bit);
        const matchRight = r === 1 && !!(right & bit);
        if (!matchLeft && !matchRight) continue;
        const next = state.slice();
        next[7 + type]--;
        if (matchLeft) next[5] ^= bit;
        if (matchRight) next[6] ^= bit;
        retain(next, matchLeft ? 2 : l, matchRight ? 2 : r);
        branches.push({ count, state: next, pairs: +(matchLeft && matchRight), rows: 0 });
      }
    }
    const result = combine(branches, solve);
    memo.set(key, result);
    return result;
  }
  return { distribution: solve([common, uniqueLeft, uniqueRight, 0, 0, 7, 7, 0, 0, 0, 0, 0]), states: memo.size };
}

function separateDistribution(common, uniqueLeft, uniqueRight) {
  const union = common + uniqueLeft + uniqueRight;
  const same = fraction(BigInt(common * (common - 1) * (common - 2)), BigInt(union * (union - 1) * (union - 2)));
  const different = fraction(same[1] - same[0], same[1]);
  return {
    distribution: new Map([
      [15, same], [2, scale(different, 2, 27)],
      [6, scale(different, 23, 54)], [10, scale(different, 1, 2)]
    ]), states: 0
  };
}

function summarize({ distribution, states }) {
  let mass = [0n, 1n], pairs = [0n, 1n], rows = [0n, 1n];
  let noPairs = [0n, 1n], wholeMenu = [0n, 1n], collateral = [0n, 1n];
  const joint = [];
  for (const [key, probability] of [...distribution].sort((a, b) => a[0] - b[0])) {
    if (!probability[0]) continue;
    const p = Math.floor(key / 4), r = key % 4;
    assert(p >= 0 && p <= r && r <= 3);
    mass = add(mass, probability);
    pairs = add(pairs, scale(probability, p));
    rows = add(rows, scale(probability, r));
    if (p === 0) noPairs = add(noPairs, probability);
    if (p === 3) wholeMenu = add(wholeMenu, probability);
    if (r < 2) collateral = add(collateral, probability);
    joint.push({ pairs: p, outgoing: r, probability: number(probability), exact: exact(probability) });
  }
  assert.deepEqual(mass, [1n, 1n]);
  return {
    meanPairs: number(pairs), meanOutgoing: number(rows),
    noPairs: number(noPairs), wholeMenu: number(wholeMenu),
    outgoingOverlapUnderTwo: number(collateral),
    exact: { meanPairs: exact(pairs), meanOutgoing: exact(rows), noPairs: exact(noPairs), wholeMenu: exact(wholeMenu), outgoingOverlapUnderTwo: exact(collateral) },
    states, joint
  };
}

function compare(common, left, right) {
  const separate = summarize(separateDistribution(common, left, right));
  const direct = summarize(directDistribution(common, left, right));
  const hybrid = summarize(hybridDistribution(common, left, right));
  assert.equal(hybrid.exact.meanOutgoing, separate.exact.meanOutgoing);
  assert.equal(hybrid.outgoingOverlapUnderTwo, 0);
  assert.equal(direct.exact.wholeMenu, separate.exact.wholeMenu);
  assert.equal(hybrid.exact.wholeMenu, separate.exact.wholeMenu);
  return { eligibleLeft: common + left, eligibleRight: common + right, commonEligible: common, separate, direct, hybrid };
}

function simulationCheck(entry, samples) {
  let seed = 72019 + entry.eligibleLeft;
  function random() {
    seed = seed + 0x6D2B79F5 | 0;
    let t = Math.imul(seed ^ seed >>> 15, seed | 1);
    t ^= t + Math.imul(t ^ t >>> 7, t | 61);
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  }
  const common = entry.commonEligible;
  const leftRows = Array.from({ length: entry.eligibleLeft }, (_, i) => i);
  const rightRows = Array.from({ length: entry.eligibleRight }, (_, i) => i < common ? i : entry.eligibleLeft);
  const union = entry.eligibleLeft + entry.eligibleRight - common;
  const totals = Object.fromEntries(['separate', 'direct', 'hybrid'].map(k => [k, [0, 0, 0, 0, 0]]));
  const assignments = Object.fromEntries(Object.keys(totals).map(k => [k, new Map()]));
  function greedy(rows, edges) {
    const usedRows = new Set(), usedColumns = new Set(), pairs = [];
    for (const [score, row, column] of edges) {
      if (rows.includes(row) && !usedRows.has(row) && !usedColumns.has(column)) {
        pairs.push([row, column]);
        usedRows.add(row);
        usedColumns.add(column);
        if (pairs.length === 3) return pairs;
      }
    }
    throw new Error('Incomplete matching');
  }
  for (let trial = 0; trial < samples; trial++) {
    const weights = Array.from({ length: union }, () => [random(), random(), random()]);
    const priorities = weights.map(() => random());
    const minima = weights.map(w => Math.min(...w));
    const edges = weights.flatMap((w, r) => w.map((v, c) => [v, r, c])).sort((a, b) => a[0] - b[0]);
    for (const kind of Object.keys(totals)) {
      const ranking = kind === 'hybrid' ? minima : priorities;
      const selected = rows => kind === 'direct' ? rows : rows.slice().sort((a, b) => ranking[a] - ranking[b]).slice(0, 3);
      const a = greedy(selected(leftRows), edges), b = greedy(selected(rightRows), edges);
      const pairs = a.filter(([r, c]) => b.some(([s, d]) => r === s && c === d)).length;
      const rows = a.filter(([r]) => b.some(([s]) => r === s)).length;
      const values = [pairs, rows, +(pairs === 0), +(pairs === 3), +(rows < 2)];
      values.forEach((value, i) => totals[kind][i] += value);
      if (entry.eligibleLeft === 5) {
        const key = a.slice().sort((x, y) => x[1] - y[1]).map(p => p[0]).join(',');
        assignments[kind].set(key, (assignments[kind].get(key) || 0) + 1);
      }
    }
  }
  const metrics = [x => x.pairs, x => x.outgoing, x => +(x.pairs === 0), x => +(x.pairs === 3), x => +(x.outgoing < 2)];
  for (const kind of Object.keys(totals)) {
    metrics.forEach((metric, i) => {
      const model = entry[kind].joint;
      const mean = model.reduce((s, x) => s + x.probability * metric(x), 0);
      const variance = model.reduce((s, x) => s + x.probability * (metric(x) - mean) ** 2, 0);
      assert(Math.abs(totals[kind][i] / samples - mean) <= 6 * Math.sqrt(variance / samples) + 1e-12, `${kind}: Monte Carlo mismatch for metric ${i}`);
    });
    if (entry.eligibleLeft === 5) {
      assert.equal(assignments[kind].size, 60);
      for (const count of assignments[kind].values()) {
        assert(Math.abs(count / samples - 1 / 60) <= 6 * Math.sqrt((1 / 60) * (59 / 60) / samples));
      }
    }
  }
  return { eligibleLeft: entry.eligibleLeft, eligibleRight: entry.eligibleRight, samples, passed: true };
}

// Known four-identity result, equal inputs, and left/right exchange symmetry.
for (const method of [directDistribution, hybridDistribution]) {
  const three = summarize(method(2, 1, 1));
  assert.equal(three.exact.meanPairs, '77/54');
  assert.equal(three.exact.meanOutgoing, '2/1');
  const equal = summarize(method(5, 0, 0));
  assert.equal(equal.exact.meanPairs, '3/1');
  assert.equal(equal.exact.meanOutgoing, '3/1');
  assert.deepEqual(summarize(method(4, 1, 0)).joint, summarize(method(4, 0, 1)).joint);
}

const report = {
  assumptions: [
    'Three identical distinct replacements in corresponding fixed reward slots, legal for every outgoing relic in both runs.',
    'Independent continuous pair scores, shared by stable outgoing identity + fixed reward slot and independent of replacement identities.',
    'Separate: independent row priorities select three, then greedy pair assignment.',
    'Direct: greedy pair assignment over every eligible outgoing relic.',
    'Hybrid: each row priority is its minimum pair score; select three, then greedy pair assignment.',
    'Substitution exchanges one eligible identity. Removal compares a set with its one-item subset.'
  ],
  method: 'Exact rational recurrence over the next lowest active edge; irrelevant edges are discarded permanently. Multiplicities aggregate exchangeable rows. Each remaining active edge is equally likely to be next.',
  proofs: {
    uniformMarginals: 'All three methods are equivariant under permutations of outgoing identities and reward columns. With independent continuous scores, these permutations act transitively on all ordered three-outgoing assignments. Every three-item subset is therefore uniform, and its six reward assignments are uniform.',
    optimalOutgoingOverlap: 'For substitution with N eligible relics, consider all N+1 inventories obtained by omitting one identity from a common N+1-identity universe. There are 3(N+1) selected-identity appearances, with at most N appearances per identity. Total outgoing overlap across inventory pairs is the sum of choose(d_i, 2). Convexity bounds this by 3*choose(N, 2)+choose(3, 2), attained by shared top-three priorities with counts N,N,N,3,0,... . Thus the maximum symmetric average outgoing overlap is 3-6(N-1)/(N(N+1)); both candidate-first rules attain it.',
    scope: 'The outgoing-overlap bound assumes an identity-symmetric rule applied consistently across possible inventories. It does not establish optimality for unchanged trade pairs.'
  },
  substitution: [], removal: []
};
for (const n of [5, 8, 12, 20]) {
  const result = compare(n - 1, 1, 1);
  report.substitution.push(result);
  console.log(JSON.stringify({ scenario: 'substitution', n, separate: result.separate.meanPairs, direct: result.direct.meanPairs, hybrid: result.hybrid.meanPairs, directOutgoing: result.direct.meanOutgoing, directCollateral: result.direct.outgoingOverlapUnderTwo }));
}
for (const n of [6, 9, 13, 21]) {
  const result = compare(n - 1, 1, 0);
  report.removal.push(result);
  console.log(JSON.stringify({ scenario: 'removal', n, separate: result.separate.meanPairs, direct: result.direct.meanPairs, hybrid: result.hybrid.meanPairs, directOutgoing: result.direct.meanOutgoing, directCollateral: result.direct.outgoingOverlapUnderTwo }));
}
report.simulationChecks = [report.substitution[0], report.substitution[2], report.removal[0], report.removal[2]].map(entry => simulationCheck(entry, 75000));
const output = path.join(__dirname, 'relic-trader-matching-results.json');
fs.writeFileSync(output, JSON.stringify(report, null, 2) + '\n');
console.log('All exact self-checks passed; results written to ' + output);
