import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';

const range = n => Array.from({ length: n }, (_, i) => i);
const near = (a, b) => assert.ok(Math.abs(a - b) < 1e-8, `${a} != ${b}`);
const choose = (xs, k) => k === 0 ? [[]] : xs.flatMap((x, i) => choose(xs.slice(i + 1), k - 1).map(t => [x, ...t]));
const factorial = n => n < 2 ? 1 : n * factorial(n - 1);
const permutations = xs => xs.length ? xs.flatMap((x, i) => permutations(xs.filter((_, j) => j !== i)).map(t => [x, ...t])) : [[]];
const pattern = (n, m, slots) => range(n + m).map(i => slots.includes(i) ? 'N' : 'O').join('');
const cache = new Map();

// Joint O/N patterns avoid enumerating equivalent assignments of random ranks.
function joint(method, a, b, m) {
  assert.ok(a <= b);
  const key = [method, a, b, m].join(':');
  if (cache.has(key)) return cache.get(key);
  const mass = new Map();
  const add = (x, y, p) => { const k = x + '/' + y; mass.set(k, (mass.get(k) || 0) + p); };
  const bigSlots = choose(range(b + m), m);
  if (method === 'slots') {
    for (const slots of bigSlots) {
      const common = slots.filter(i => i < a + m);
      const extras = choose(range(a + m).filter(i => !common.includes(i)), m - common.length);
      for (const extra of extras) add(pattern(a, m, common.concat(extra)), pattern(b, m, slots), 1 / bigSlots.length / extras.length);
    }
  } else if (method === 'corrected') {
    const survivingRanks = choose(range(b), a);
    for (const slots of bigSlots) {
      const y = pattern(b, m, slots);
      for (const keep of survivingRanks) {
        let rank = 0;
        const x = [...y].filter(c => c === 'N' || keep.includes(rank++)).join('');
        add(x, y, 1 / bigSlots.length / survivingRanks.length);
      }
    }
  } else if (method === 'merge') {
    function visit(x, y, ax, ay, nx, ny, p) {
      if (ax + nx === a + m && ay + ny === b + m) return add(x, y, p);
      const px = ax + nx === a + m ? 0 : (m - nx) / (a + m - ax - nx);
      const py = ay + ny === b + m ? 0 : (m - ny) / (b + m - ay - ny);
      const edges = [...new Set([0, px, py, 1])].sort((u, v) => u - v);
      for (let i = 1; i < edges.length; i++) {
        const u = (edges[i - 1] + edges[i]) / 2;
        const cx = ax + nx === a + m ? '' : u < px ? 'N' : 'O';
        const cy = ay + ny === b + m ? '' : u < py ? 'N' : 'O';
        visit(x + cx, y + cy, ax + (cx === 'O'), ay + (cy === 'O'), nx + (cx === 'N'), ny + (cy === 'N'), p * (edges[i] - edges[i - 1]));
      }
    }
    visit('', '', 0, 0, 0, 0, 1);
  } else if (method === 'fixed') {
    const edges = [...new Set([0, 1, ...range(a).map(i => (i + 1) / (a + 1)), ...range(b).map(i => (i + 1) / (b + 1))])].sort((u, v) => u - v);
    function visit(i, left, values, p, denominator) {
      if (i === edges.length - 1) {
        if (left) return;
        const make = n => range(n).map(j => ({ v: (j + 1) / (n + 1), type: 'O' }))
          .concat(values.map(v => ({ v, type: 'N' }))).sort((x, y) => x.v - y.v).map(x => x.type).join('');
        return add(make(a), make(b), p * factorial(m) / denominator);
      }
      for (let count = 0; count <= left; count++) visit(i + 1, left - count, values.concat(Array(count).fill((edges[i] + edges[i + 1]) / 2)), p * (edges[i + 1] - edges[i]) ** count, denominator * factorial(count));
    }
    visit(0, m, [], 1, 1);
  } else throw Error(method);
  near([...mass.values()].reduce((x, y) => x + y, 0), 1);
  if (method !== 'fixed' || m <= 1) for (const side of [0, 1]) {
    const marginal = new Map();
    for (const [k, p] of mass) { const s = k.split('/')[side]; marginal.set(s, (marginal.get(s) || 0) + p); }
    assert.equal(marginal.size, choose(range((side ? b : a) + m), m).length);
    for (const p of marginal.values()) near(p, 1 / marginal.size);
  }
  const rows = [...mass].map(([k, p]) => [...k.split('/'), p]);
  cache.set(key, rows);
  return rows;
}

function ids(pat, old) {
  let o = 0, n = 0;
  return [...pat].map(c => c === 'O' ? old[o++] : 1000 + n++);
}
function observation(x, y, k) {
  const xx = x.slice(0, k), yy = y.slice(0, k);
  const overlap = xx.filter(id => yy.includes(id)).length;
  const exact = xx.every((id, i) => id === yy[i]);
  return [Number(exact), overlap, Number(overlap === 0), Number(overlap < k / 2), Number(!exact), exact ? 0 : overlap];
}
function finish(sums, total = 1) {
  const [prefix, overlap, zero, belowHalf, mismatch, mismatchOverlap] = sums.map(x => x / total);
  return { prefix, overlap, zero, belowHalf, mismatch, overlapAfterMismatch: mismatch > 1e-10 ? mismatchOverlap / mismatch : null, zeroAfterMismatch: mismatch > 1e-10 ? zero / mismatch : null };
}
function exact(method, a, b, m, k) {
  if (a.length > b.length) return exact(method, b, a, m, k);
  const sums = Array(6).fill(0);
  for (const [x, y, p] of joint(method, a.length, b.length, m)) {
    const v = observation(ids(x, a), ids(y, b), k);
    for (let i = 0; i < v.length; i++) sums[i] += p * v[i];
  }
  return finish(sums);
}

function binomial(n, k) {
  if (n < 0 || k < 0 || k > n) return 0;
  let v = 1;
  for (let i = 1; i <= Math.min(k, n - k); i++) v *= (n - i + 1) / i;
  return v;
}
// Prefix overlap depends only on the two incoming-event counts, allowing larger exact checks.
function countJoint(method, a, b, m, k) {
  if (a > b) return countJoint(method, b, a, m, k).map(([x, y, p]) => [y, x, p]);
  const mass = new Map();
  const add = (x, y, p) => { if (p > 0) { const key = `${x}/${y}`; mass.set(key, (mass.get(key) || 0) + p); } };
  if (method === 'slots') {
    for (let y = 0; y <= Math.min(k, m); y++) for (let e = 0; e <= Math.min(b - a, m - y); e++) {
      const base = binomial(k, y) * binomial(a + m - k, m - y - e) * binomial(b - a, e) / binomial(b + m, m);
      if (!base) continue;
      for (let t = 0; t <= e; t++) add(y + t, y, base * binomial(k - y, t) * binomial(a + e - k + y, e - t) / binomial(a + e, e));
    }
  } else if (method === 'corrected') {
    for (let y = 0; y <= Math.min(k, m); y++) for (let u = 0; u <= Math.min(a, k - y); u++) {
      const v = k - y - u;
      const base = binomial(m, y) * binomial(a, u) * binomial(b - a, v) / binomial(b + m, k);
      if (!base) continue;
      for (let t = 0; t <= Math.min(v, m - y); t++) add(y + t, y, base * binomial(m - y, t) * binomial(a - u, v - t) / binomial(a + m - y - u, v));
    }
  } else if (method === 'merge') {
    let states = [[0, 0, 1]];
    for (let i = 0; i < k; i++) {
      mass.clear();
      for (const [x, y, p] of states) {
        const px = (m - x) / (a + m - i), py = (m - y) / (b + m - i);
        add(x + 1, y + 1, p * Math.min(px, py));
        add(x, y, p * (1 - Math.max(px, py)));
        add(x + 1, y, p * Math.max(0, px - py));
        add(x, y + 1, p * Math.max(0, py - px));
      }
      states = [...mass].map(([key, p]) => [...key.split('/').map(Number), p]);
    }
  } else if (method === 'fixed') {
    function tail(x, y) {
      if (x > m || y > m || x > k || y > k) return 0;
      let tx = x === 0 ? 1 : Math.min(1, Math.max(0, (k - x + 1) / (a + 1)));
      let ty = y === 0 ? 1 : Math.min(1, Math.max(0, (k - y + 1) / (b + 1)));
      if (tx > ty) { [tx, ty] = [ty, tx]; [x, y] = [y, x]; }
      let p = 0;
      for (let l = x; l <= m; l++) for (let h = Math.max(0, y - l); h <= m - l; h++) p += binomial(m, l) * binomial(m - l, h) * tx ** l * (ty - tx) ** h * (1 - ty) ** (m - l - h);
      return p;
    }
    for (let x = 0; x <= Math.min(k, m); x++) for (let y = 0; y <= Math.min(k, m); y++) {
      const p = tail(x, y) - tail(x + 1, y) - tail(x, y + 1) + tail(x + 1, y + 1);
      assert.ok(p > -1e-8);
      add(x, y, Math.max(0, p));
    }
  }
  const rows = [...mass].map(([key, p]) => [...key.split('/').map(Number), p]);
  near(rows.reduce((s, r) => s + r[2], 0), 1);
  return rows;
}
function exactOverlap(method, a, b, m, k) {
  const histogram = Array(k + 1).fill(0);
  for (const [x, y, p] of countJoint(method, a.length, b.length, m, k)) {
    const commonOld = a.slice(0, k - x).filter(id => b.slice(0, k - y).includes(id)).length;
    histogram[Math.min(x, y) + commonOld] += p;
  }
  return { histogram, overlap: histogram.reduce((s, p, i) => s + p * i, 0), zero: histogram[0] };
}

// Independent rank enumeration validates both probability-preserving constructions.
let oracleCases = 0;
for (let b = 0; b <= 4; b++) for (let a = 0; a <= b; a++) for (let m = 0; m <= 3; m++) {
  if (b + m > 7) continue;
  const orders = permutations(range(b + m));
  for (const method of ['slots', 'corrected']) {
    const mass = new Map();
    for (const rank of orders) {
      let x, y;
      if (method === 'slots') {
        x = pattern(a, m, rank.filter(i => i < a + m).slice(0, m));
        y = pattern(b, m, rank.slice(0, m));
      } else {
        x = rank.filter(i => i < a || i >= b).map(i => i < b ? 'O' : 'N').join('');
        y = rank.map(i => i < b ? 'O' : 'N').join('');
      }
      const key = x + '/' + y;
      mass.set(key, (mass.get(key) || 0) + 1 / orders.length);
      oracleCases++;
    }
    const expected = joint(method, a, b, m);
    assert.equal(expected.length, mass.size);
    for (const [x, y, p] of expected) near(p, mass.get(x + '/' + y));
  }
}
near(exact('slots', [0], [0, 1], 2, 2).zero, 0);
near(exact('slots', [0], [0, 1], 2, 2).overlap, 5 / 3);
near(exact('fixed', [0], [0], 2, 1).prefix, 1);
for (const [x, y, p] of joint('fixed', 1, 1, 2)) near(p, x === 'NON' ? 1 / 2 : 1 / 4);
const methods = ['slots', 'corrected', 'merge', 'fixed'];
for (let b = 0; b <= 5; b++) for (let a = 0; a <= b; a++) for (let m = 1; m <= 4; m++) for (let k = 1; k <= Math.min(5, a + m); k++) for (const method of methods) {
  const expected = new Map();
  for (const [x, y, p] of joint(method, a, b, m)) {
    const key = [x.slice(0, k).replaceAll('O', '').length, y.slice(0, k).replaceAll('O', '').length].join('/');
    expected.set(key, (expected.get(key) || 0) + p);
  }
  const actual = new Map(countJoint(method, a, b, m, k).map(([x, y, p]) => [`${x}/${y}`, p]));
  for (const key of new Set([...expected.keys(), ...actual.keys()])) near(expected.get(key) || 0, actual.get(key) || 0);
}
const small = [], summaries = {};
const sequences = range(32).map(mask => range(5).filter(i => mask & (1 << i)));
for (let ai = 0; ai < 32; ai++) for (let bi = ai + 1; bi < 32; bi++) {
  const a = sequences[ai], b = sequences[bi];
  const kind = a.length === b.length ? 'equal-count identities' : ((ai & bi) === ai || (ai & bi) === bi) ? 'nested counts' : 'different counts and identities';
  for (const m of [1, 2, 3, 4]) for (const k of [2, 3, 5]) {
    if (Math.min(a.length, b.length) + m < k) continue;
    const stats = Object.fromEntries(methods.map(method => [method, exact(method, a, b, m, k)]));
    small.push({ a, b, m, k, kind, stats });
    for (const method of methods.slice(1)) {
      const key = kind + ':' + method;
      const s = summaries[key] ??= { kind, method, cases: 0, overlap: [0, 0, 0], zero: [0, 0, 0], prefix: [0, 0, 0], mismatchOverlap: [0, 0, 0] };
      s.cases++;
      for (const [label, metric, direction] of [['overlap', 'overlap', 1], ['zero', 'zero', -1], ['prefix', 'prefix', 1], ['mismatchOverlap', 'overlapAfterMismatch', 1]]) {
        if (stats[method][metric] === null || stats.slots[metric] === null) continue;
        const d = direction * (stats[method][metric] - stats.slots[metric]);
        s[label][d > 1e-8 ? 0 : d < -1e-8 ? 2 : 1]++;
      }
    }
  }
}
const strongest = {};
for (const method of methods.slice(1)) for (const metric of ['overlap', 'zero', 'overlapAfterMismatch', 'prefix']) {
  const rows = small.filter(r => r.stats.slots[metric] !== null && r.stats[method][metric] !== null);
  const direction = metric === 'zero' ? -1 : 1;
  rows.sort((x, y) => direction * ((y.stats[method][metric] - y.stats.slots[metric]) - (x.stats[method][metric] - x.stats.slots[metric])));
  strongest[method + ':' + metric] = { best: rows[0], worst: rows.at(-1) };
}

let seed = 0x65a31b29;
function uniform() {
  seed = (seed + 0x6d2b79f5) >>> 0;
  let z = Math.imul(seed ^ (seed >>> 15), 1 | seed);
  z ^= z + Math.imul(z ^ (z >>> 7), 61 | z);
  return (((z ^ (z >>> 14)) >>> 0) + 0.5) / 4294967296;
}
function sampledPatterns(n, m, draws) {
  const slots = draws.slots.slice(0, n + m).map((v, i) => [v, i]).sort((x, y) => x[0] - y[0]).slice(0, m).map(x => x[1]);
  const newPriorities = [...draws.fresh].sort((a, b) => a - b);
  const sorted = old => old.map(v => ({ v, c: 'O' })).concat(newPriorities.map(v => ({ v, c: 'N' }))).sort((a, b) => a.v - b.v).map(x => x.c).join('');
  let old = 0, fresh = 0, merged = '';
  for (let i = 0; i < n + m; i++) {
    if (draws.merge[i] < (m - fresh) / (n + m - i)) { merged += 'N'; fresh++; } else { merged += 'O'; old++; }
  }
  return { slots: pattern(n, m, slots), corrected: sorted(draws.old.slice(0, n)), fixed: sorted(range(n).map(i => (i + 1) / (n + 1))), merge: merged };
}
const large = [];
const trials = 50_000;
for (const size of [8, 16, 32]) {
  const universe = range(size);
  const pairs = [
    ['nested-prefix', universe.slice(0, size / 2), universe],
    ['nested-tail', universe.slice(size / 2), universe],
    ['nested-alternating', universe.filter(i => i % 2 === 0), universe],
    ['equal-count-shift', universe.slice(0, size / 2), universe.slice(1, size / 2 + 1)],
    ['equal-count-disjoint', universe.slice(0, size / 2), universe.slice(size / 2)],
    ['mixed-count-identities', universe.filter(i => i % 3 === 0), universe.filter(i => i % 3 !== 0 || i % 2 === 0)],
  ];
  for (const m of [1, 2, 4, 8]) {
    const sums = pairs.map(() => Object.fromEntries([2, 3, 5].map(k => [k, Object.fromEntries(methods.map(method => [method, Array(6).fill(0)]))])));
    const deltaSquares = pairs.map(() => Object.fromEntries([2, 3, 5].map(k => [k, Object.fromEntries(methods.slice(1).map(method => [method, [0, 0]]))])));
    for (let t = 0; t < trials; t++) {
      const draws = { slots: range(size + m).map(uniform), old: range(size).map(uniform), fresh: range(m).map(uniform), merge: range(size + m).map(uniform) };
      const pats = new Map();
      for (const [, a, b] of pairs) for (const n of [a.length, b.length]) if (!pats.has(n)) pats.set(n, sampledPatterns(n, m, draws));
      for (let pair = 0; pair < pairs.length; pair++) {
        const [, a, b] = pairs[pair];
        const paths = Object.fromEntries(methods.map(method => [method, [ids(pats.get(a.length)[method], a), ids(pats.get(b.length)[method], b)]]));
        for (const k of [2, 3, 5]) {
          if (Math.min(a.length, b.length) + m < k) continue;
          const obs = {};
          for (const method of methods) {
            obs[method] = observation(...paths[method], k);
            for (let i = 0; i < 6; i++) sums[pair][k][method][i] += obs[method][i];
          }
          for (const method of methods.slice(1)) for (const [i, metric] of [1, 2].entries()) deltaSquares[pair][k][method][i] += (obs[method][metric] - obs.slots[metric]) ** 2;
        }
      }
    }
    for (let pair = 0; pair < pairs.length; pair++) for (const k of [2, 3, 5]) {
      const [kind, a, b] = pairs[pair];
      if (Math.min(a.length, b.length) + m < k) continue;
      const stats = Object.fromEntries(methods.map(method => [method, finish(sums[pair][k][method], trials)]));
      const differences = {};
      for (const method of methods.slice(1)) {
        differences[method] = {};
        for (const [i, metric] of ['overlap', 'zero'].entries()) {
          const mean = stats[method][metric] - stats.slots[metric];
          const se = Math.sqrt(Math.max(0, deltaSquares[pair][k][method][i] / trials - mean ** 2) / (trials - 1));
          differences[method][metric] = { mean, confidence95: [mean - 1.96 * se, mean + 1.96 * se] };
        }
      }
      const exactOverlapStats = Object.fromEntries(methods.map(method => [method, exactOverlap(method, a, b, m, k)]));
      for (const method of methods) for (const metric of ['overlap', 'zero']) assert.ok(Math.abs(exactOverlapStats[method][metric] - stats[method][metric]) < (metric === 'zero' ? 0.02 : 0.05));
      large.push({ size, kind, a, b, m, k, trials, stats, differences, exactOverlapStats });
    }
  }
  console.log(`Completed pool size ${size}.`);
}

const largeSummaries = {};
for (const method of methods.slice(1)) {
  const summary = { cases: large.length, overlap: [0, 0, 0], zero: [0, 0, 0], identicalOverlapDistributions: 0 };
  for (const row of large) {
    const baseline = row.exactOverlapStats.slots, alternative = row.exactOverlapStats[method];
    for (const metric of ['overlap', 'zero']) {
      const difference = (metric === 'zero' ? -1 : 1) * (alternative[metric] - baseline[metric]);
      summary[metric][difference > 1e-9 ? 0 : difference < -1e-9 ? 2 : 1]++;
    }
    if (baseline.histogram.every((p, i) => Math.abs(p - alternative.histogram[i]) < 1e-9)) summary.identicalOverlapDistributions++;
  }
  largeSummaries[method] = summary;
}

const result = {
  scope: 'Same ordered incoming batch; survivor lists are subsequences of a common inherited order. Exact all distinct subset pairs of five old IDs, incoming counts 1/2/3/4, prefix lengths 2/3/5 where both outputs are long enough. Larger pools use six identity patterns, sizes 8/16/32, incoming counts 1/2/4/8. These are synthetic comparisons, not route-weighted gameplay rates.',
  metrics: 'prefix = exact agreement through k; overlap = shared identities among first k ignoring order; zero = no shared identities; belowHalf = fewer than half shared; afterMismatch metrics condition on failure of exact k-prefix agreement. Conditional rates can compare different selected cases.',
  methods: 'slots = rejected shared-position priorities; corrected = design sorted random priorities, using a shared prefix of old draws sorted onto survivor order and shared new priorities; fixed = evenly spaced old priorities plus shared new priorities (biased for m>1); merge = shared per-output-position weighted merge draws.',
  comparisonCounts: 'Summary arrays count [better, tied, worse] relative to slots; lower zero-overlap risk is better. Conditional metrics omit cases with no mismatch.',
  oracleCases, exactCases: small.length, summaries, strongest,
  largeCases: large.length, largeSummaries,
};
writeFileSync(new URL('./early-overlap-results.json', import.meta.url), JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify({ oracleCases, exactCases: small.length, summaries }, null, 2));
