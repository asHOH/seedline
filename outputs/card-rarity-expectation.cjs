// Vanilla v0.111.0: exact probability propagation, three cards per offer.
// Shop averages weight post-offer room boundaries equally; three cards per offer.
// Stationary estimates omit bosses; eliteShare is an independent per-offer mix.
const assert = require('node:assert/strict');

function model(scarcity) {
  const growth = scarcity ? 0.005 : 0.01;
  const normal = scarcity ? 0.0149 : 0.03;
  const elite = scarcity ? 0.05 : 0.1;
  const shop = scarcity ? 0.045 : 0.09;
  const offsets = Array.from({length: Math.round(0.45 / growth) + 1}, (_, i) => -0.05 + i * growth);
  const initial = offsets.map((_, i) => Number(i === 0));
  function step(dist, base) {
    const next = offsets.map(() => 0);
    dist.forEach((mass, i) => {
      const rare = base === 1 ? 1 : Math.max(0, Math.min(1, base + offsets[i]));
      next[0] += mass * rare;
      next[Math.min(i + 1, offsets.length - 1)] += mass * (1 - rare);
    });
    assert(Math.abs(next.reduce((a, b) => a + b, 0) - 1) < 1e-10);
    return next;
  }
  function offer(dist, base) {
    for (let i = 0; i < 3; i++) dist = step(dist, base);
    return dist;
  }
  function stats(dist) {
    const offset = dist.reduce((sum, mass, i) => sum + mass * offsets[i], 0);
    const shopRare = dist.reduce((sum, mass, i) => sum + mass * Math.max(0, shop + offsets[i]), 0);
    return {offset, shopRare, equivalentShopOffset: shopRare - shop};
  }
  const early = [];
  let dist = initial;
  for (let fight = 1; fight <= 10; fight++) {
    dist = offer(dist, normal);
    if ([1, 2, 3, 4, 5, 10].includes(fight)) early.push({fight, ...stats(dist)});
  }
  assert(stats(step(initial, 1)).offset === -0.05); // Boss resets shared offset.
  assert(Math.abs(stats(step(initial, normal)).offset - (-0.05 + growth)) < 1e-12);
  const stationary = [0, 0.2].map(eliteShare => {
    let dist = initial;
    for (let iteration = 0; iteration < 10000; iteration++) {
      const a = offer(dist, normal), b = offer(dist, elite);
      const next = a.map((mass, i) => mass * (1 - eliteShare) + b[i] * eliteShare);
      const error = Math.max(...next.map((mass, i) => Math.abs(mass - dist[i])));
      dist = next;
      if (error < 1e-14) return {eliteShare, ...stats(dist)};
    }
    throw new Error('Stationary distribution did not converge');
  });
  const sequence = [normal, normal, normal, elite, normal, normal, elite];
  function cycle(start) {
    let end = start;
    const average = offsets.map(() => 0);
    const phases = sequence.map((base, i) => {
      end = offer(end, base);
      end.forEach((mass, j) => average[j] += mass / sequence.length);
      return {room: i + 1, type: base === normal ? 'normal' : 'elite', ...stats(end)};
    });
    return {end, average: stats(average), phases};
  }
  const firstCycle = cycle(initial);
  let repeated = firstCycle;
  let converged = false;
  for (let i = 0; i < 10000; i++) {
    const next = cycle(repeated.end);
    const error = Math.max(...next.end.map((mass, j) => Math.abs(mass - repeated.end[j])));
    repeated = next;
    if (error < 1e-14) { converged = true; break; }
  }
  assert(converged);
  assert(Math.abs(stats(offer(firstCycle.end, 1)).offset + 0.05) < 1e-12);
  return {scarcity, early, stationary, sequence: {
    firstCycle: {average: firstCycle.average, phases: firstCycle.phases},
    repeatingCycle: {average: repeated.average, phases: repeated.phases}
  }};
}
console.log(JSON.stringify([model(false), model(true)], null, 2));
