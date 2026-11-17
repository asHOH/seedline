import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { pools, histories, states, predicates, simulate, shuffle, uniform, range, lcs, measure, formatResults } from './within-act-sequences.mjs';

// shortcut: prescribed states and five event opportunities per act isolate scheduling;
// add event effects and route weights before claiming gameplay frequencies.
const trials = 30_000;
const policies = ['refresh', 'fixed', 'act1-fixed'];
const rich = 'rich-stable';
const profiles = [
  [rich, rich, rich], ['poor-stable', 'poor-stable', 'poor-stable'],
  ['purchase-and-recovery', 'purchase-and-recovery', 'purchase-and-recovery'],
  ['damage-and-healing', 'damage-and-healing', 'damage-and-healing'],
  ['empty-inventory', 'empty-inventory', 'empty-inventory'],
  ['crossing-conditions', 'crossing-conditions', 'crossing-conditions'],
  ['poor-stable', rich, rich], [rich, 'poor-stable', rich], [rich, rich, 'poor-stable'],
  ['rare-N-rejection', 'rare-N-rejection', rich],
];
const pairs = [...range(profiles.length - 1).map(i => [0, i + 1]), [6, 7], [2, 3], [4, 5]];
const singletonIds = { g44: [100], g55: [110], g120: [111], g149: [104], g150: [105], h70: [106], h5: [112], h10: [107], f6: [113], f7: [108], p2: [109], g125: [114], h19: [115], basics: [116], p1: [117], merchant: [118], ranwid: [119], r5: [120], h12: [121], soul: [122] };
const names = ['prefixLength', 'firstEvent', 'firstThree', 'firstFive', 'orderedThree', 'orderedFive', 'sameOrdinalFive', 'sharedFive', 'reversedSharedOrder'];
function insert(old, fresh, draws) {
  const a = draws.old.slice(0, old.length).sort((a, b) => a - b).map((v, i) => [v, old[i]]);
  const b = draws.fresh.slice(0, fresh.length).sort((a, b) => a - b).map((v, i) => [v, fresh[i]]);
  return a.concat(b).sort((a, b) => a[0] - b[0]).map(x => x[1]);
}
function strongestCases(rows) {
  return Object.fromEntries(['prefixLength', 'firstEvent', 'firstThree', 'orderedFive'].map(metric => [metric,
    Object.fromEntries(policies.slice(1).map(policy => {
      const ranked = rows.flatMap(row => row.acts.map((act, i) => ({ a: row.a, b: row.b, act: i + 1,
        metrics: { refresh: act[metric].refresh, ...act[metric][policy] },
      }))).sort((a, b) => a.metrics.delta - b.metrics.delta);
      return [policy, { lowestDelta: ranked[0], highestDelta: ranked.at(-1) }];
    })),
  ]));
}
const checkCases = strongestCases([{ a: ['a'], b: ['b'], acts: [2, -1].map(delta => Object.fromEntries(names.map(metric =>
  [metric, { refresh: 5, fixed: { delta }, 'act1-fixed': { delta: -delta } }],
))) }]);
assert.deepEqual(Object.values(checkCases.prefixLength).map(c => [c.lowestDelta.act, c.highestDelta.act]), [[2, 1], [1, 2]]);
function run(firstPool) {
  const acts = [firstPool, pools[2], pools[3]];
  const specs = profiles.map(profile => profile.map((name, act) => {
    const ss = states(histories.find(h => h.name === name), acts[act]);
    return { eligible: ss.map(s => Object.keys(acts[act].counts).map(key => predicates[key](s))), rare: ss.map(s => !s.rejectRareN) };
  }));
  const sums = pairs.map(() => range(3).map(() => Object.fromEntries(policies.map(p => [p, Array(names.length).fill(0)]))));
  const squares = pairs.map(() => range(3).map(() => Object.fromEntries(policies.slice(1).map(p => [p, Array(names.length).fill(0)]))));
  const aggregateSquares = range(3).map(() => Object.fromEntries(policies.slice(1).map(p => [p, Array(names.length).fill(0)])));
  const sequenceSums = policies.map(() => 0), sequenceSquares = policies.map(() => 0);
  for (let trial = 0; trial < trials; trial++) {
    const cohorts = [shuffle([0, 1, 2, 3, ...range(firstPool.counts.N - 4).map(i => i + 19)]), shuffle(range(10).map(i => i + 4)), shuffle(range(5).map(i => i + 14))];
    const otherOrders = { ...singletonIds, g100_0: [101], g100_1: shuffle([102, 103]), g100_2: [103] };
    const insertion = [10, 5].map(m => ({ old: range(4).map(uniform), fresh: range(m).map(uniform) }));
    const exps = acts.map(pool => range(5).map(() => Object.values(pool.counts).map(n => range(n).map(() => -Math.log(uniform())))));
    const paths = policies.map(policy => profiles.map((_, profile) => {
      const visited = new Set(), output = []; let previousN;
      for (let act = 0; act < 3; act++) {
        const old = act ? previousN.filter(id => !visited.has(id) && (act === 1 ? id < 4 : id < 2 || id === 4 || id === 5)) : [];
        const nOrder = act ? insert(old, cohorts[act], insertion[act - 1]) : cohorts[0];
        const ids = Object.keys(acts[act].counts).map(key => key === 'N' ? nOrder : (key === 'g100' ? otherOrders['g100_' + act] : otherOrders[key]).filter(id => !visited.has(id)));
        const method = policy === 'act1-fixed' ? (act === 0 ? 'fixed' : 'refresh') : policy;
        const rareId = act === 0 ? 19 : act === 1 ? 6 : -1;
        const selected = simulate(method, ids, specs[profile][act].eligible, specs[profile][act].rare, exps[act], rareId);
        assert.equal(selected.length, 5);
        for (const id of selected) { assert.ok(!visited.has(id)); visited.add(id); }
        output.push(selected); previousN = nOrder;
      }
      return output;
    }));
    const aggregateDelta = range(3).map(() => Object.fromEntries(policies.slice(1).map(p => [p, Array(names.length).fill(0)])));
    const chainDelta = policies.map(() => 0);
    for (let pair = 0; pair < pairs.length; pair++) {
      const [a, b] = pairs[pair];
      const allOrders = paths.map(p => lcs(p[a].flat(), p[b].flat()));
      for (let policy = 0; policy < policies.length; policy++) { sequenceSums[policy] += allOrders[policy]; chainDelta[policy] += (allOrders[policy] - allOrders[0]) / pairs.length; }
      for (let act = 0; act < 3; act++) {
        const values = paths.map(p => measure(p[a][act], p[b][act]));
        for (let policy = 0; policy < policies.length; policy++) for (let metric = 0; metric < names.length; metric++) {
          const p = policies[policy], v = values[policy][metric]; sums[pair][act][p][metric] += v;
          if (policy) {
            const delta = v - values[0][metric]; squares[pair][act][p][metric] += delta ** 2; aggregateDelta[act][p][metric] += delta / pairs.length;
          }
        }
      }
    }
    for (let policy = 1; policy < policies.length; policy++) sequenceSquares[policy] += chainDelta[policy] ** 2;
    for (let act = 0; act < 3; act++) for (const p of policies.slice(1)) for (let k = 0; k < names.length; k++) aggregateSquares[act][p][k] += aggregateDelta[act][p][k] ** 2;
  }
  function stats(mean, baseline, square) {
    const delta = mean - baseline, se = Math.sqrt(Math.max(0, square / trials - delta ** 2) / (trials - 1));
    return { mean, delta, confidence95: [delta - 1.96 * se, delta + 1.96 * se], sixSE: 6 * se };
  }
  const aggregates = range(3).map(act => Object.fromEntries(names.map((metric, k) => {
    const means = policies.map(p => sums.reduce((sum, row) => sum + row[act][p][k], 0) / pairs.length / trials);
    return [metric, { refresh: means[0], ...Object.fromEntries(policies.slice(1).map((p, i) => [p, stats(means[i + 1], means[0], aggregateSquares[act][p][k])])) }];
  })));
  const rows = pairs.map(([a, b], pair) => ({ a: profiles[a], b: profiles[b], acts: range(3).map(act => Object.fromEntries(names.map((metric, k) => [metric, {
    refresh: sums[pair][act].refresh[k] / trials,
    ...Object.fromEntries(policies.slice(1).map(p => [p, stats(sums[pair][act][p][k] / trials, sums[pair][act].refresh[k] / trials, squares[pair][act][p][k])])),
  }])))}));
  const sequenceMeans = sequenceSums.map(n => n / trials / pairs.length);
  return { firstAct: firstPool.name, trials, pairs: pairs.length, aggregates,
    wholeRunOrderedOverlap: { refresh: sequenceMeans[0], ...Object.fromEntries(policies.slice(1).map((p, i) => [p, stats(sequenceMeans[i + 1], sequenceMeans[0], sequenceSquares[i + 1])])) },
    clearLosses: Object.fromEntries(policies.slice(1).map(p => [p, Object.fromEntries(['prefixLength', 'firstEvent', 'firstThree', 'orderedFive'].map(metric => [metric, rows.reduce((sum, r) => sum + r.acts.filter(a => a[metric][p].delta < -a[metric][p].sixSE - 1e-10).length, 0)]))])),
    strongest: strongestCases(rows),
  };
}
export { strongestCases };

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const result = { scope: 'Three actual act pools with settled cross-act insertion, run-wide visits, expiration, and five ordinary-event opportunities per act. Policy-generated survivor identities feed subsequent acts.',
    limitations: 'Prescribed states exclude event-effect feedback and empirical route weights. Rare-N failure sensitivity only; no wrapping. Six-SE exploratory comparison threshold.', profiles, results: [run(pools[0]), run(pools[1])] };
  result.retainedResults = 'All aggregates, whole-run overlap and clear-loss counts; minimum and maximum deltas by policy for prefix length, first-event/first-three agreement and ordered overlap. Routine per-history rows omitted.';
  writeFileSync(new URL('./within-act-chain-results.json', import.meta.url), formatResults(result));
  console.log(JSON.stringify(result.results.map(({ firstAct, aggregates, wholeRunOrderedOverlap, clearLosses }) => ({ firstAct, aggregates, wholeRunOrderedOverlap, clearLosses })), null, 2));
}
