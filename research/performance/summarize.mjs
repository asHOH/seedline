import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import assert from 'node:assert/strict';
import { gzipSync } from 'node:zlib';

const root = process.argv[2];
const environment = JSON.parse(readFileSync(join(root, 'environment.json'), 'utf8').replace(/^\uFEFF/, ''));
const samples = [];
const decks = new Map();
for (let pair = 0; pair < environment.pairs; pair++) {
  for (const mode of ['vanilla', 'seedline']) {
    const rows = readFileSync(join(root, `${pair}-${mode}`, 'measurements.jsonl'), 'utf8').trim().split(/\r?\n/).map(JSON.parse);
    assert.equal(rows.at(-1).op, 'done');
    assert.equal(rows[0].mod, mode === 'seedline');
    assert.equal(rows[0].openingTiming, 'within-combat-entry');
    environment.runtime ??= rows[0].runtime;
    environment.os ??= rows[0].os;
    assert.equal(rows[0].runtime, environment.runtime);
    assert.equal(rows[0].os, environment.os);
    const fixtures = rows.filter(r => r.op === 'deck');
    assert.equal(fixtures.length, 78);
    for (const fixture of fixtures) {
      const key = JSON.stringify([fixture.size, fixture.duplicate]);
      const identities = Object.fromEntries(Object.entries(fixture.identities).sort(([a], [b]) => a.localeCompare(b)));
      assert.equal(Object.values(identities).reduce((sum, count) => sum + count, 0), fixture.size);
      if (fixture.size === 2000) assert.equal(identities.ANGER, 1900);
      if (decks.has(key)) assert.deepEqual(identities, decks.get(key).identities);
      else decks.set(key, { size: fixture.size, duplicate: fixture.duplicate, identities });
    }
    const measured = rows.filter(r => 'ms' in r);
    assert.equal(measured.length, 300);
    assert.equal(new Set(measured.map(r => JSON.stringify([r.size, r.act, r.duplicate, r.iteration, r.op]))).size, 300);
    assert.ok(measured.every(r => Number.isFinite(r.ms) && r.ms >= 0 && r.bytes >= 0));
    assert.ok(measured.filter(r => r.size === 2000).every(r => r.duplicate && r.angerCount === 1900));
    for (const opening of measured.filter(r => r.op === 'opening-shuffle')) {
      const entry = measured.find(r => r.op === 'combat-entry' && r.size === opening.size &&
        r.act === opening.act && r.duplicate === opening.duplicate && r.iteration === opening.iteration);
      assert.equal(opening.openingCalls, 1);
      assert.ok(entry && opening.ms <= entry.ms && opening.bytes <= entry.bytes);
    }
    samples.push(...measured.map(r => ({ ...r, pair, mode })));
  }
}
function median(values) {
  const sorted = values.toSorted((a, b) => a - b);
  return (sorted[Math.floor((sorted.length - 1) / 2)] + sorted[Math.floor(sorted.length / 2)]) / 2;
}
function stats(rows) {
  const times = rows.map(r => r.ms).toSorted((a, b) => a - b);
  return {
    samples: rows.length,
    median_ms: median(times),
    p95_ms: times[Math.ceil(times.length * 0.95) - 1],
    median_allocated_bytes: median(rows.map(r => r.bytes)),
  };
}
const results = [];
for (const size of [30, 2000]) {
  for (const op of ['combat-entry', 'opening-shuffle', 'hand-choice', 'pile-retrieval', 'checkpoint-encode']) {
    const selected = samples.filter(r => r.size === size && r.op === op);
    const vanilla = stats(selected.filter(r => r.mode === 'vanilla'));
    const seedline = stats(selected.filter(r => r.mode === 'seedline'));
    const scenarios = [];
    for (const act of [1, 3]) {
      for (const duplicate of size === 2000 ? [true] : [false, true]) {
        const scenario = selected.filter(r => r.act === act && r.duplicate === duplicate);
        const baseline = stats(scenario.filter(r => r.mode === 'vanilla'));
        const mod = stats(scenario.filter(r => r.mode === 'seedline'));
        assert.equal(baseline.samples, environment.pairs * 10);
        assert.equal(mod.samples, environment.pairs * 10);
        scenarios.push({ act, duplicate, vanilla: baseline, seedline: mod, added_median_ms: mod.median_ms - baseline.median_ms });
      }
    }
    results.push({ size, op, vanilla, seedline, added_median_ms: seedline.median_ms - vanilla.median_ms, scenarios });
  }
}
const report = { environment, opening_shuffle_timing: 'within-combat-entry', decks: [...decks.values()],
  warmup_iterations_per_scenario: 3, measured_iterations_per_scenario_per_process: 10, results };
writeFileSync(join(root, 'results.json'), JSON.stringify(report, null, 2) + '\n');
writeFileSync(join(root, 'samples.jsonl.gz'), gzipSync(samples.map(r => JSON.stringify(r)).join('\n') + '\n'));
console.table(results.map(r => ({ cards: r.size, operation: r.op,
  vanilla_ms: r.vanilla.median_ms.toFixed(3), seedline_ms: r.seedline.median_ms.toFixed(3),
  added_ms: r.added_median_ms.toFixed(3), seedline_p95_ms: r.seedline.p95_ms.toFixed(3) })));
