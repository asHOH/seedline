import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';

// shortcut: three nested gold levels, three event opportunities, and passing rare checks;
// extend the experiment before deciding wrapping or intersecting-predicate behavior.
const counts = [3, 2, 2];
const offsets = [0, 3, 5];
const histories = Array.from({ length: 27 }, (_, h) => [Math.floor(h / 9), Math.floor(h / 3) % 3, h % 3]);
const trials = 200_000;
let rngState = 0x7f41c295;
function uniform() {
  rngState = (rngState + 0x6d2b79f5) >>> 0;
  let z = Math.imul(rngState ^ (rngState >>> 15), 1 | rngState);
  z ^= z + Math.imul(z ^ (z >>> 7), 61 | z);
  return (((z ^ (z >>> 14)) >>> 0) + 0.5) / 4294967296;
}

// Each table entry stands for an independent draw addressed by
// act + act-local event ordinal + condition key + within-resolution entry index.
function draw(remaining, level, exponentials, ordinal) {
  const base = ordinal * 7;
  let winner = -1, deadline = Infinity;
  for (let c = 0; c <= level; c++) if (remaining[c]) {
    const priority = exponentials[base + offsets[c]] / remaining[c];
    if (priority < deadline) { winner = c; deadline = priority; }
  }
  assert.ok(winner >= 0);
  for (let c = level + 1; c < 3; c++) {
    let index = 0;
    let priority = remaining[c] ? exponentials[base + offsets[c]] / remaining[c] : Infinity;
    while (priority < deadline) {
      remaining[c]--;
      index++;
      priority = remaining[c] ? priority + exponentials[base + offsets[c] + index] / remaining[c] : Infinity;
    }
  }
  remaining[winner]--;
  return winner;
}

function permutations(left, prefix = [], result = []) {
  if (!left.some(Boolean)) result.push([...prefix]);
  for (let c = 0; c < 3; c++) if (left[c]) {
    left[c]--; prefix.push(c); permutations(left, prefix, result); prefix.pop(); left[c]++;
  }
  return result;
}

const orders = permutations([...counts]);
const expected = histories.map(history => {
  const frequencies = Array(27).fill(0);
  for (const order of orders) {
    let cursor = 0, code = 0;
    for (const level of history) {
      while (order[cursor] > level) cursor++;
      code = code * 3 + order[cursor++];
    }
    frequencies[code] += 1 / orders.length;
  }
  return frequencies;
});
const observed = histories.map(() => Array(27).fill(0));
const scenarios = [
  [[0, 2, 2], [1, 2, 2]], [[1, 2, 2], [2, 2, 2]],
  [[1, 1, 1], [2, 2, 2]], [[0, 1, 0], [1, 1, 0]],
].map(([a, b]) => ({ a, b, indexA: a[0] * 9 + a[1] * 3 + a[2], indexB: b[0] * 9 + b[1] * 3 + b[2], matches: [0, 0, 0] }));
const exponentials = new Float64Array(21);
const paths = new Uint8Array(81);
let sum = 0, squareSum = 0;
for (let trial = 0; trial < trials; trial++) {
  for (let k = 0; k < exponentials.length; k++) exponentials[k] = -Math.log(uniform());
  for (let h = 0; h < histories.length; h++) {
    const remaining = [...counts], used = [0, 0, 0];
    let code = 0;
    for (let t = 0; t < 3; t++) {
      const c = draw(remaining, histories[h][t], exponentials, t);
      paths[h * 3 + t] = c * 4 + (++used[c]);
      code = code * 3 + c;
    }
    observed[h][code]++;
  }
  let matches = 0;
  for (let t = 0; t < 3; t++) {
    const bins = new Uint8Array(12);
    for (let h = 0; h < histories.length; h++) bins[paths[h * 3 + t]]++;
    for (const n of bins) matches += n * (n - 1) / 2;
  }
  const mean = matches / (351 * 3);
  sum += mean; squareSum += mean * mean;
  for (const s of scenarios) for (let t = 0; t < 3; t++) if (paths[s.indexA * 3 + t] === paths[s.indexB * 3 + t]) s.matches[t]++;

  if (trial < 100) for (let o = 1; o <= 3; o++) for (let l = 0; l <= 2; l++) for (let h = 0; h <= 2; h++) {
    const states = [0, 1, 2].map(level => { const remaining = [o, l, h]; return { winner: draw(remaining, level, exponentials, 0), remaining }; });
    for (let a = 0; a < 3; a++) for (let b = a + 1; b < 3; b++) if (states[a].winner === states[b].winner) {
      assert.deepEqual(states[a].remaining, states[b].remaining, 'same selection preserves state');
    }
  }
}

let maxMarginalError = 0;
for (let h = 0; h < histories.length; h++) for (let path = 0; path < 27; path++) {
  const probability = expected[h][path];
  const error = Math.abs(observed[h][path] / trials - probability);
  maxMarginalError = Math.max(maxMarginalError, error);
  assert.ok(error < 6 * Math.sqrt(probability * (1 - probability) / trials) + 6 / trials, 'path frequency agrees with exact vanilla reference');
}
const average = sum / trials;
const standardError = Math.sqrt((squareSum / trials - average * average) / (trials - 1));
const result = {
  method: 'Seeded Monte Carlo; within-resolution condition priorities persist, with fresh priorities at each act-local event ordinal.',
  counts, trials, average, confidence95: [average - 1.96 * standardError, average + 1.96 * standardError],
  maxMarginalError, scenarios: scenarios.map(({ a, b, matches }) => ({ a, b, matches: matches.map(n => n / trials) })),
  checks: 'All 27 single-run path distributions checked against exact vanilla enumeration with six-standard-error tolerance; same-selection state preservation checked across 27 starting count vectors and three eligibility levels.',
};
writeFileSync(new URL('./condition-priorities-results.json', import.meta.url), JSON.stringify(result, null, 2));
console.log(JSON.stringify(result, null, 2));
