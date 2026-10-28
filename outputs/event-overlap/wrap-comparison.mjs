import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';

const n = 4, full = (1 << n) - 1;
const bits = mask => Array.from({ length: n }, (_, i) => i).filter(i => mask & (1 << i));
const permutations = xs => xs.length ? xs.flatMap((x, i) => permutations(xs.filter((_, j) => j !== i)).map(t => [x, ...t])) : [[]];
const orders = permutations(bits(full));
const near = (a, b) => assert.ok(Math.abs(a - b) < 1e-9, `${a} != ${b}`);
const add = (map, key, p) => map.set(key, (map.get(key) || 0) + p);

function vanilla(history) {
  const paths = new Map(), wraps = new Map(), preWrapPaths = new Map(), final = Array(n).fill(0);
  for (const order of orders) {
    let cursor = 0, visited = 0, firstWrap = 0, path = '';
    for (let t = 0; t < history.length; t++) {
      assert.ok(history[t] & ~visited);
      while (true) {
        if (cursor >= n && firstWrap === 0) firstWrap = t + 1;
        const event = order[cursor++ % n];
        if ((history[t] & (1 << event)) && !(visited & (1 << event))) {
          visited |= 1 << event;
          path += String.fromCharCode(65 + event);
          if (t === history.length - 1) final[event] += 1 / orders.length;
          break;
        }
      }
    }
    add(paths, path, 1 / orders.length);
    add(wraps, firstWrap, 1 / orders.length);
    if (firstWrap === 0) add(preWrapPaths, path, 1 / orders.length);
  }
  return { paths, wraps, preWrapPaths, final };
}

// Weighted singleton priorities equal uniform sampling of remaining scheduling entries.
function refreshed(history) {
  const paths = new Map(), wraps = new Map(), preWrapPaths = new Map(), final = Array(n).fill(0);
  function visit(t, remaining, visited, firstWrap, path, p) {
    if (t === history.length) {
      add(paths, path, p); add(wraps, firstWrap, p);
      if (firstWrap === 0) add(preWrapPaths, path, p);
      final[path.charCodeAt(path.length - 1) - 65] += p;
      return;
    }
    assert.ok(history[t] & ~visited);
    if (remaining === 0) { remaining = full & ~visited; firstWrap ||= t + 1; }
    const candidates = bits(remaining);
    for (const event of candidates) {
      const bit = 1 << event;
      if (history[t] & bit) visit(t + 1, remaining & ~bit, visited | bit, firstWrap, path + String.fromCharCode(65 + event), p / candidates.length);
      else visit(t, remaining & ~bit, visited, firstWrap, path, p / candidates.length);
    }
  }
  visit(0, full, 0, 0, '', 1);
  return { paths, wraps, preWrapPaths, final };
}

const histories = [];
for (let a = 1; a <= full; a++) for (let b = 1; b <= full; b++) for (let c = 1; c <= full; c++) {
  if (bits(b).length >= 2 && bits(c).length >= 3) histories.push([a, b, c]);
}
let differentFinal = 0, differentPaths = 0;
const examples = [];
for (const history of histories) {
  const a = vanilla(history), b = refreshed(history);
  for (const key of new Set([...a.wraps.keys(), ...b.wraps.keys()])) near(a.wraps.get(key) || 0, b.wraps.get(key) || 0);
  for (const key of new Set([...a.preWrapPaths.keys(), ...b.preWrapPaths.keys()])) near(a.preWrapPaths.get(key) || 0, b.preWrapPaths.get(key) || 0);
  const delta = Math.max(...a.final.map((p, i) => Math.abs(p - b.final[i])));
  const pathDelta = Math.max(...[...new Set([...a.paths.keys(), ...b.paths.keys()])].map(key => Math.abs((a.paths.get(key) || 0) - (b.paths.get(key) || 0))));
  if (pathDelta > 1e-9) differentPaths++;
  if (delta > 1e-9) {
    differentFinal++;
    examples.push({ allowed: history.map(mask => bits(mask).map(i => String.fromCharCode(65 + i)).join('')), firstWrapOpportunity: Object.fromEntries(a.wraps), vanillaFinal: a.final, policyFinal: b.final, delta, vanillaPaths: Object.fromEntries(a.paths), policyPaths: Object.fromEntries(b.paths) });
  }
}
examples.sort((a, b) => b.delta - a.delta);
const result = {
  scope: 'Four singleton conditions; three predetermined eligibility sets, always with an unvisited eligible candidate. Uniform hidden vanilla permutation versus refreshed weighted scheduling, including schedule wrapping. No rare checks, fallback or forced events.',
  histories: histories.length, firstWrapDistributionMatches: histories.length, preWrapPathDistributionMatches: histories.length, differentFinal, differentPaths,
  maximumDifference: examples[0],
  simpleExample: examples.find(x => x.allowed[0] === 'A' && x.allowed[1] === 'BC' && x.allowed[2] === 'ABCD'),
};
near(result.simpleExample.vanillaFinal[3], 1 / 3);
near(result.simpleExample.policyFinal[3], 29 / 72);
writeFileSync(new URL('./wrap-comparison-results.json', import.meta.url), JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify(result, null, 2));
