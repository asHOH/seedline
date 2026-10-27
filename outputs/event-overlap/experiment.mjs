import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';

// shortcut: gold-only nested predicates, passing rare checks, and no wrapping;
// extend the model before applying these results to intersecting predicates or real runs.
const names = ['O', 'L', 'H'];
const rounds = 3;
const epsilon = 1e-10;

function permutations(counts, prefix = [], result = []) {
  if (!counts.some(Boolean)) result.push([...prefix]);
  for (let c = 0; c < counts.length; c++) {
    if (!counts[c]) continue;
    counts[c]--;
    prefix.push(c);
    permutations(counts, prefix, result);
    prefix.pop();
    counts[c]++;
  }
  return result;
}

function histories(length) {
  let values = [[]];
  for (let i = 0; i < length; i++) values = values.flatMap(h => names.map((_, c) => [...h, c]));
  return values;
}

function schedulePath(order, history, family = false) {
  const inner = order.filter(c => c > 0);
  const used = [0, 0, 0];
  const supply = names.map((_, c) => order.filter(x => x === c).length);
  let cursor = 0;
  let innerCursor = 0;
  return history.map(level => {
    while (cursor < order.length) {
      let c = order[cursor++];
      if (family && c > 0) {
        if (![1, 2].some(k => k <= level && used[k] < supply[k])) continue;
        if (innerCursor >= inner.length) continue;
        c = inner[innerCursor++];
      }
      if (c <= level && used[c] < supply[c]) return names[c] + (++used[c]);
    }
    return 'END';
  });
}

function add(map, key, amount) { map.set(key, (map.get(key) ?? 0) + amount); }
function near(a, b, context) { assert.ok(Math.abs(a - b) < epsilon, `${context}: ${a} != ${b}`); }
function sameDistribution(a, b, context) {
  for (const key of new Set([...a.keys(), ...b.keys()])) near(a.get(key) ?? 0, b.get(key) ?? 0, `${context}/${key}`);
}

function experiment(counts) {
  assert.ok(counts[0] >= rounds, 'Enough always-eligible events keep every tested draw before wrapping.');
  const orders = permutations([...counts]);
  const hs = histories(rounds);
  const pathKey = p => p.join(',');
  const paths = Object.fromEntries(['flat', 'family'].map(model => [model, hs.map(h => orders.map(o => schedulePath(o, h, model === 'family')))]));
  const distributions = paths.flat.map(list => {
    const d = new Map();
    for (const p of list) {
      assert.ok(!p.includes('END'));
      add(d, pathKey(p), 1 / orders.length);
    }
    return d;
  });
  for (let h = 0; h < hs.length; h++) {
    const d = new Map();
    for (const p of paths.family[h]) add(d, pathKey(p), 1 / orders.length);
    sameDistribution(d, distributions[h], `family history ${hs[h]}`);
  }

  // A belief contains every vanilla hidden schedule consistent with observations.
  // Conditioning on the selected condition retains uniform weights over survivors.
  let nodeId = 0;
  function node(states, used) { return { id: nodeId++, states, used, transitions: new Map() }; }
  const root = node(orders.map((_, index) => [index, 0]), [0, 0, 0]);
  function transition(current, level) {
    if (current.transitions.has(level)) return current.transitions.get(level);
    const buckets = [[], [], []];
    for (const [index, start] of current.states) {
      let cursor = start;
      while (cursor < orders[index].length && orders[index][cursor] > level) cursor++;
      assert.ok(cursor < orders[index].length, 'The bounded experiment never wraps.');
      buckets[orders[index][cursor]].push([index, cursor + 1]);
    }
    const probabilities = buckets.map(b => b.length / current.states.length);
    const next = buckets.map((b, c) => b.length ? node(b, current.used.map((n, k) => n + (k === c))) : null);
    const result = { probabilities, next };
    current.transitions.set(level, result);
    return result;
  }
  const afterLowDraw = transition(root, 0).next[0];
  const reopened = transition(afterLowDraw, 2).probabilities;
  const totalCount = counts.reduce((a, b) => a + b, 0);
  counts.forEach((n, c) => near(reopened[c], n / totalCount, 'vanilla inactive-condition probability'));

  function gateJoint(p, q, order) {
    let active = [{ a: -1, b: -1, probability: 1 }];
    let remainingA = 1;
    let remainingB = 1;
    for (const c of order) {
      const thresholdA = remainingA > epsilon ? Math.min(1, p[c] / remainingA) : 0;
      const thresholdB = remainingB > epsilon ? Math.min(1, q[c] / remainingB) : 0;
      const cuts = [...new Set([0, thresholdA, thresholdB, 1])].sort((a, b) => a - b);
      const next = new Map();
      for (const state of active) for (let i = 1; i < cuts.length; i++) {
        const width = cuts[i] - cuts[i - 1];
        if (width <= epsilon) continue;
        const u = (cuts[i] + cuts[i - 1]) / 2;
        const a = state.a < 0 && u < thresholdA ? c : state.a;
        const b = state.b < 0 && u < thresholdB ? c : state.b;
        add(next, `${a},${b}`, state.probability * width);
      }
      active = [...next].map(([key, probability]) => { const [a, b] = key.split(',').map(Number); return { a, b, probability }; });
      remainingA -= p[c];
      remainingB -= q[c];
    }
    for (const s of active) assert.ok(s.a >= 0 && s.b >= 0);
    for (let c = 0; c < 3; c++) {
      near(active.filter(s => s.a === c).reduce((n, s) => n + s.probability, 0), p[c], 'gate A marginal');
      near(active.filter(s => s.b === c).reduce((n, s) => n + s.probability, 0), q[c], 'gate B marginal');
    }
    return active;
  }

  // Shared exponential priorities give an independent control without a family tree.
  function raceJoint(p, q) {
    const result = [];
    for (let a = 0; a < 3; a++) for (let b = 0; b < 3; b++) {
      if (!p[a] || !q[b]) continue;
      let probability = 0;
      if (a === b) {
        probability = 1 / p.reduce((sum, _, k) => sum + Math.max(p[k] / p[a], q[k] / q[a]), 0);
      } else {
        const lower = p[b] / p[a];
        const upper = q[a] ? q[b] / q[a] : Infinity;
        if (upper <= lower) continue;
        const other = [0, 1, 2].filter(k => k !== a && k !== b);
        const breaks = other.filter(k => q[k]).map(k => p[k] * q[b] / (p[a] * q[k]));
        const cuts = [lower, ...breaks.filter(t => t > lower && t < upper), upper].sort((x, y) => x - y);
        for (let k = 1; k < cuts.length; k++) {
          const l = cuts[k - 1], u = cuts[k];
          const middle = Number.isFinite(u) ? (l + u) / 2 : l + 1;
          let intercept = 1, slope = 1;
          for (const c of other) {
            const constant = p[c] / p[a], coefficient = q[c] / q[b];
            if (constant >= coefficient * middle) intercept += constant;
            else slope += coefficient;
          }
          probability += (1 / (intercept + slope * l) - (Number.isFinite(u) ? 1 / (intercept + slope * u) : 0)) / slope;
        }
      }
      if (probability > epsilon) result.push({ a, b, probability });
    }
    for (let c = 0; c < 3; c++) {
      near(result.filter(s => s.a === c).reduce((n, s) => n + s.probability, 0), p[c], 'race A marginal');
      near(result.filter(s => s.b === c).reduce((n, s) => n + s.probability, 0), q[c], 'race B marginal');
    }
    return result;
  }

  function pairedGates(h1, h2, order) {
    const result = [];
    function visit(a, b, pathA, pathB, probability) {
      const t = pathA.length;
      if (t === rounds) { result.push({ pathA, pathB, probability }); return; }
      const ta = transition(a, h1[t]);
      const tb = transition(b, h2[t]);
      for (const joint of gateJoint(ta.probabilities, tb.probabilities, order)) {
        const eventA = names[joint.a] + (a.used[joint.a] + 1);
        const eventB = names[joint.b] + (b.used[joint.b] + 1);
        visit(ta.next[joint.a], tb.next[joint.b], [...pathA, eventA], [...pathB, eventB], probability * joint.probability);
      }
    }
    visit(root, root, [], [], 1);
    return result;
  }

  const resetCache = new Map();
  function resetDraw(countA, countB, levelA, levelB, order) {
    const cacheKey = [countA, countB, levelA, levelB, order].join('|');
    if (resetCache.has(cacheKey)) return resetCache.get(cacheKey);
    const complete = new Map();
    let active = new Map();
    const stateKey = s => [s.countA, s.countB, s.selectedA, s.selectedB].join('|');
    const initial = { countA, countB, selectedA: -1, selectedB: -1, probability: 1 };
    active.set(stateKey(initial), initial);
    while (active.size) {
      const next = new Map();
      for (const state of active.values()) {
        const sumA = state.countA.reduce((a, b) => a + b, 0);
        const sumB = state.countB.reduce((a, b) => a + b, 0);
        assert.ok((state.selectedA >= 0 || sumA > 0) && (state.selectedB >= 0 || sumB > 0));
        const p = state.selectedA < 0 ? state.countA.map(n => n / sumA) : [1, 0, 0];
        const q = state.selectedB < 0 ? state.countB.map(n => n / sumB) : [1, 0, 0];
        for (const draw of order === 'race' ? raceJoint(p, q) : gateJoint(p, q, order)) {
          const result = { countA: [...state.countA], countB: [...state.countB],
            selectedA: state.selectedA, selectedB: state.selectedB,
            probability: state.probability * draw.probability };
          if (result.selectedA < 0) { result.countA[draw.a]--; if (draw.a <= levelA) result.selectedA = draw.a; }
          if (result.selectedB < 0) { result.countB[draw.b]--; if (draw.b <= levelB) result.selectedB = draw.b; }
          const target = result.selectedA >= 0 && result.selectedB >= 0 ? complete : next;
          const key = stateKey(result);
          if (target.has(key)) target.get(key).probability += result.probability;
          else target.set(key, result);
        }
      }
      active = next;
    }
    const result = [...complete.values()];
    near(result.reduce((sum, s) => sum + s.probability, 0), 1, 'reset draw total');
    if (String(countA) === String(countB)) for (const s of result) {
      if (s.selectedA === s.selectedB) assert.deepEqual(s.countA, s.countB, 'same starting state + same selection preserves state');
    }
    resetCache.set(cacheKey, result);
    return result;
  }

  function pairedReset(h1, h2, order) {
    const result = [];
    function visit(countA, countB, usedA, usedB, pathA, pathB, probability) {
      const t = pathA.length;
      if (t === rounds) { result.push({ pathA, pathB, probability }); return; }
      for (const draw of resetDraw(countA, countB, h1[t], h2[t], order)) {
        const a = draw.selectedA, b = draw.selectedB;
        const nextUsedA = usedA.map((n, c) => n + (c === a));
        const nextUsedB = usedB.map((n, c) => n + (c === b));
        visit(draw.countA, draw.countB, nextUsedA, nextUsedB,
          [...pathA, names[a] + nextUsedA[a]], [...pathB, names[b] + nextUsedB[b]], probability * draw.probability);
      }
    }
    visit([...counts], [...counts], [0, 0, 0], [0, 0, 0], [], [], 1);
    return result;
  }

  function metrics(records) {
    const matches = Array(rounds).fill(0);
    const prefixes = Array(rounds).fill(0);
    let all = 0;
    let firstSame = 0;
    let secondSameAfterFirstSame = 0;
    for (const { pathA, pathB, probability } of records) {
      const equal = pathA.map((e, t) => e === pathB[t]);
      equal.forEach((e, t) => { if (e) matches[t] += probability; });
      equal.forEach((_, t) => { if (equal.slice(0, t + 1).every(Boolean)) prefixes[t] += probability; });
      if (equal.every(Boolean)) all += probability;
      if (equal[0]) { firstSame += probability; if (equal[1]) secondSameAfterFirstSame += probability; }
    }
    return { matches, prefixes, average: matches.reduce((a, b) => a + b, 0) / rounds, all,
      secondGivenFirstMatch: secondSameAfterFirstSame / firstSame };
  }

  function upperBound(a, b) {
    return Array.from({ length: rounds }, (_, t) => {
      const da = new Map(), db = new Map();
      for (const [path, p] of distributions[a]) add(da, path.split(',')[t], p);
      for (const [path, p] of distributions[b]) add(db, path.split(',')[t], p);
      return [...da].reduce((sum, [event, p]) => sum + Math.min(p, db.get(event) ?? 0), 0);
    });
  }

  const rows = [];
  for (let a = 0; a < hs.length; a++) for (let b = a + 1; b < hs.length; b++) {
    const row = { a: hs[a], b: hs[b], bound: upperBound(a, b) };
    for (const model of ['flat', 'family']) row[model] = metrics(orders.map((_, seed) => ({ pathA: paths[model][a][seed], pathB: paths[model][b][seed], probability: 1 / orders.length })));
    for (const [model, order, sampler] of [['highFirst', [2, 1, 0], pairedGates], ['lowFirst', [0, 1, 2], pairedGates],
      ['resetHigh', [2, 1, 0], pairedReset], ['resetLow', [0, 1, 2], pairedReset],
      ['resetMiddle', [1, 2, 0], pairedReset], ['resetRace', 'race', pairedReset]]) {
      const joint = sampler(hs[a], hs[b], order);
      const marginalA = new Map(), marginalB = new Map();
      for (const r of joint) {
        add(marginalA, pathKey(r.pathA), r.probability);
        add(marginalB, pathKey(r.pathB), r.probability);
      }
      sameDistribution(marginalA, distributions[a], `${model} A ${hs[a]}`);
      sameDistribution(marginalB, distributions[b], `${model} B ${hs[b]}`);
      row[model] = metrics(joint);
    }
    for (const model of ['flat', 'family', 'highFirst', 'lowFirst', 'resetHigh', 'resetLow', 'resetMiddle', 'resetRace']) {
      for (let t = 0; t < rounds; t++) assert.ok(row[model].matches[t] <= row.bound[t] + epsilon);
    }
    // Nested initial eligible sets admit maximal immediate coupling.
    near(row.flat.matches[0], row.bound[0], 'flat first-draw optimum');
    near(row.highFirst.matches[0], row.bound[0], 'high-first first-draw optimum');
    near(row.resetHigh.matches[0], row.bound[0], 'reset first-draw optimum');
    for (const model of ['resetHigh', 'resetLow', 'resetMiddle', 'resetRace']) {
      for (let t = 0; t < rounds; t++) near(row[model].prefixes[t], row.flat.prefixes[t], 'refresh preserves matched-prefix probabilities');
    }
    rows.push(row);
  }

  const summary = {};
  for (const model of ['flat', 'family', 'highFirst', 'lowFirst', 'resetHigh', 'resetLow', 'resetMiddle', 'resetRace']) {
    const advantage = rows.map(r => r[model].average - r.flat.average);
    summary[model] = {
      matches: Array.from({ length: rounds }, (_, t) => rows.reduce((sum, r) => sum + r[model].matches[t], 0) / rows.length),
      average: rows.reduce((sum, r) => sum + r[model].average, 0) / rows.length,
      winsVsFlat: advantage.filter(d => d > epsilon).length,
      tiesVsFlat: advantage.filter(d => Math.abs(d) <= epsilon).length,
      lossesVsFlat: advantage.filter(d => d < -epsilon).length,
      bestVsFlat: Math.max(...advantage), worstVsFlat: Math.min(...advantage),
    };
  }
  const selected = [
    [[0, 2, 2], [1, 2, 2]],
    [[1, 2, 2], [2, 2, 2]],
    [[0, 0, 2], [1, 1, 2]],
    [[1, 1, 1], [2, 2, 2]],
    [[0, 1, 2], [1, 0, 2]],
  ].map(([a, b]) => rows.find(r => String(r.a) === String(a) && String(r.b) === String(b)));
  const best = [...rows].sort((a, b) => (b.resetHigh.average - b.flat.average) - (a.resetHigh.average - a.flat.average)).slice(0, 3);
  const worst = [...rows].sort((a, b) => (a.resetHigh.average - a.flat.average) - (b.resetHigh.average - b.flat.average)).slice(0, 3);
  const rising = h => h.every((level, t) => !t || h[t - 1] <= level);
  const subsets = Object.fromEntries([
    ['rising', rows.filter(r => rising(r.a) && rising(r.b))],
    ['sameFinalGoldLevel', rows.filter(r => r.a[rounds - 1] === r.b[rounds - 1])],
  ].map(([label, subset]) => [label, {
    pairs: subset.length,
    flat: subset.reduce((sum, r) => sum + r.flat.average, 0) / subset.length,
    family: subset.reduce((sum, r) => sum + r.family.average, 0) / subset.length,
    resetHigh: subset.reduce((sum, r) => sum + r.resetHigh.average, 0) / subset.length,
    wins: subset.filter(r => r.resetHigh.average > r.flat.average + epsilon).length,
    losses: subset.filter(r => r.resetHigh.average < r.flat.average - epsilon).length,
  }]));
  return { counts, rounds, orders: orders.length, histories: hs.length, pairs: rows.length, summary, subsets, selected, best, worst };
}

const results = [[3, 2, 2], [3, 1, 3], [3, 3, 1], [3, 1, 1]].map(experiment);
writeFileSync(new URL('./results.json', import.meta.url), JSON.stringify({
  method: 'Exact enumeration and analytical integration of shared uniforms/exponentials; no Monte Carlo.',
  scope: 'One act, three act-local event ordinals; O always eligible, L at gold >=100, H at gold >=125; fixed gold histories; per-condition ID sequences shared; rare checks pass; no wrapping.',
  metric: 'Probability of the same event ID at a corresponding ordinal, averaged equally across ordinals and distinct unordered history pairs. A stress suite, not a model of gameplay frequencies.',
  checks: 'Every paired model reproduces the full vanilla single-run condition/visit-index path distribution; refreshing preserves matched-prefix probabilities and same-selection state alignment.',
  models: {
    flat: 'One fixed uniformly shuffled condition schedule.',
    family: 'Freeze the L/H internal schedule when its whole family is unavailable.',
    highFirst: 'Exact vanilla conditional probabilities reconstructed from observed history; fresh H-first shared gates per event opportunity.',
    lowFirst: 'Same conditional-probability model with O-first gates.',
    resetHigh: 'Refresh only unpassed scheduling entries per act-local event ordinal; sample each attempted label with H-first gates, decrementing every attempted label.',
    resetLow: 'Same refresh with O-first gates.',
    resetMiddle: 'Same refresh with L-first gates, a control that groups O and H.',
    resetRace: 'Same refresh with fresh shared exponential priorities weighted by remaining scheduling counts on every attempt; no paused clocks.',
  }, results,
}, null, 2));
console.log('PASS: four pools, 1,404 history pairs, full-path marginals, matched-prefix probabilities, and same-selection state checks.');
console.log(JSON.stringify(results.map(({ counts, summary }) => ({ counts, flat: summary.flat.average, family: summary.family.average, refresh: summary.resetHigh.average })), null, 2));
