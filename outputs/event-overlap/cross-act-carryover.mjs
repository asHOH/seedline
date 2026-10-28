import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';

function permutations(items) {
  if (!items.length) return [[]];
  return items.flatMap((item, index) =>
    permutations(items.filter((_, other) => other !== index)).map(tail => [item, ...tail]));
}

function near(actual, expected) {
  assert.ok(Math.abs(actual - expected) < 1e-10, `${actual} != ${expected}`);
}

// All K events fail their common condition in the previous act; O always passes.
// Keep an O after the last previous-act visit, so this calculation never wraps.
const scheduleCases = [];
for (let k = 1; k <= 3; k++) for (let o = 2; o <= 4; o++) {
  const ids = [
    ...Array.from({ length: k }, (_, i) => `K${i}`),
    ...Array.from({ length: o }, (_, i) => `O${i}`),
  ];
  const orders = permutations(ids);
  for (let visits = 1; visits < o; visits++) {
    let carry = 0;
    for (const order of orders) {
      let cursor = 0, seen = 0;
      while (seen < visits) if (order[cursor++].startsWith('O')) seen++;
      const remainingK = order.slice(cursor).filter(id => id.startsWith('K')).length;
      carry += remainingK / (remainingK + o - visits) / orders.length;
    }
    const restore = k / (k + o - visits);
    near(carry, k / (k + o));
    scheduleCases.push({ k, o, visits, carry, restore, vanilla: restore });
  }
}
const scheduleExample = scheduleCases.find(c => c.k === 1 && c.o === 2 && c.visits === 1);
near(scheduleExample.carry, 1 / 3);
near(scheduleExample.restore, 1 / 2);

// A deterministic trace through the settled exponential scheduling rule.
function draw(state, eligible, exponentials) {
  const index = [0, 0];
  const priorities = state.remaining.map((count, c) => count ? exponentials[c][0] / count : Infinity);
  for (;;) {
    const c = priorities[0] <= priorities[1] ? 0 : 1;
    assert.ok(Number.isFinite(priorities[c]), 'Trace must succeed before wrapping');
    state.remaining[c]--;
    if (eligible[c]) {
      const id = `${c === 0 ? 'A' : 'B'}${++state.used[c]}`;
      state.visited.push(id);
      return id;
    }
    if (state.remaining[c]) {
      const next = exponentials[c][++index[c]];
      assert.ok(next > 0, 'Every consumed exponential is supplied');
      priorities[c] += next / state.remaining[c];
    } else priorities[c] = Infinity;
  }
}

const left = { remaining: [2, 2], used: [0, 0], visited: [] };
const right = structuredClone(left);
assert.equal(draw(left, [true, true], [[0.2, 0.4], [0.6]]), 'A1');
assert.equal(draw(right, [false, true], [[0.2, 0.4], [0.6]]), 'B1');
assert.equal(draw(left, [true, true], [[0.6], [0.8]]), 'B1');
assert.equal(draw(right, [true, true], [[0.6], [0.8]]), 'A1');
assert.deepEqual([...left.visited].sort(), [...right.visited].sort());
assert.deepEqual(left.remaining, [1, 1]);
assert.deepEqual(right.remaining, [0, 1]);

// With two conditions and shared independent exponential draws, A wins below
// one threshold of the same draw ratio in each run: disagreement is |pA - qA|.
function nextAgreement(a, b) {
  const p = a[0] / (a[0] + a[1]);
  const q = b[0] / (b[0] + b[1]);
  return 1 - Math.abs(p - q);
}
const consistencyExample = {
  left, right,
  carryAgreement: nextAgreement(left.remaining, right.remaining),
  restoreAgreement: nextAgreement([1, 1], [1, 1]),
};
near(consistencyExample.carryAgreement, 1 / 2);
near(consistencyExample.restoreAgreement, 1);

// A and B are available in both acts; C first becomes available in the next act.
const insertionRows = permutations(['A', 'B', 'C']).map(order => {
  const firstVisit = order.find(id => id !== 'C');
  const nextView = order.filter(id => id !== firstVisit);
  return { masterOrder: order, firstVisit, nextView, nextVisit: nextView[0] };
});
const carriedNewEventProbability = insertionRows.filter(row => row.nextVisit === 'C').length / insertionRows.length;
let vanillaNewEventProbability = 0;
for (const firstOrder of permutations(['A', 'B'])) {
  const survivors = ['A', 'B', 'C'].filter(id => id !== firstOrder[0]);
  for (const nextOrder of permutations(survivors)) {
    if (nextOrder[0] === 'C') vanillaNewEventProbability += 1 / 4;
  }
}
near(carriedNewEventProbability, 2 / 3);
near(vanillaNewEventProbability, 1 / 2);
const conditionalRows = insertionRows.filter(row => row.firstVisit === 'A');
near(conditionalRows.filter(row => row.nextVisit === 'C').length / conditionalRows.length, 2 / 3);
assert.deepEqual(insertionRows.find(row => row.masterOrder.join('') === 'CAB').nextView, ['C', 'B']);

function interleavings(oldIds, newIds, probability = 1) {
  const total = oldIds.length + newIds.length;
  if (!total) return [{ order: [], probability }];
  const paths = [];
  for (const [ids, isNew] of [[oldIds, false], [newIds, true]]) {
    if (!ids.length) continue;
    const tails = interleavings(
      isNew ? oldIds : oldIds.slice(1),
      isNew ? newIds.slice(1) : newIds,
      probability * ids.length / total,
    );
    for (const tail of tails) paths.push({ order: [ids[0], ...tail.order], probability: tail.probability });
  }
  return paths;
}

const insertionFixCases = [];
for (let oldCount = 0; oldCount <= 3; oldCount++) for (let newCount = 0; newCount <= 3; newCount++) {
  if (!oldCount && !newCount) continue;
  const oldOrders = permutations(Array.from({ length: oldCount }, (_, i) => `O${i}`));
  const newOrders = permutations(Array.from({ length: newCount }, (_, i) => `N${i}`));
  const probabilities = new Map();
  for (const oldIds of oldOrders) for (const newIds of newOrders) {
    for (const path of interleavings(oldIds, newIds)) {
      assert.deepEqual(path.order.filter(id => id.startsWith('O')), oldIds);
      assert.deepEqual(path.order.filter(id => id.startsWith('N')), newIds);
      const key = path.order.join(',');
      probabilities.set(key, (probabilities.get(key) ?? 0) + path.probability / oldOrders.length / newOrders.length);
    }
  }
  const vanillaOrders = permutations([...oldOrders[0], ...newOrders[0]]);
  assert.equal(probabilities.size, vanillaOrders.length);
  for (const order of vanillaOrders) near(probabilities.get(order.join(',')), 1 / vanillaOrders.length);
  insertionFixCases.push({ oldCount, newCount, permutations: vanillaOrders.length });
}
const insertionFix = {
  rule: 'Preserve old and newly eligible cohort orders; choose the next cohort in proportion to its remaining size, using shared act/condition/merge-position draws.',
  example: interleavings(['B'], ['C']),
  cases: insertionFixCases,
};
near(insertionFix.example.find(path => path.order[0] === 'C').probability, 1 / 2);

const result = {
  method: 'Exact enumeration and deterministic state traces; no Monte Carlo.',
  scope: 'Scheduling examples keep the act pool unchanged and avoid wrapping and rare failures. The insertion example isolates one condition whose act pool expands.',
  scheduleExample, scheduleCases, consistencyExample,
  insertionExample: { carriedNewEventProbability, vanillaNewEventProbability, rows: insertionRows },
  insertionFix,
  checks: '18 schedule cases; shared-priority trace with equal visited sets; all six master orders; independent next-act vanilla shuffle; activation before the previous cursor; uniform insertion for 15 old/new pool sizes including empty cohorts.',
};
writeFileSync(new URL('./cross-act-carryover-results.json', import.meta.url), JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify({ scheduleExample, consistencyExample, insertionExample: result.insertionExample, insertionFix, checks: result.checks }, null, 2));
