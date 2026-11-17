// Run: node research/reward-calibration/calibrate.cjs [runs per scenario] [output.json] [a6]
// Standard-library Monte Carlo; uncommon outcomes are integrated analytically.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {rng, route, eligible, nextEvent, otherSpending, simulateEnvironment, poolSizes} = require('./environment.cjs');
const BASE = {normal: 0.0149, elite: 0.05, fixed: 0.0149};
const opening = [[0.3349, 0.3399, 0.3449], [0.3499, 0.3549, 0.3599]];
const A7 = {base: BASE, growth: 0.005, maxOffset: 90, opening, scarcity: true, shop: 0.045, shopOffset: 0};
const A6 = {base: {normal: 0.03, elite: 0.1, fixed: 0.03}, growth: 0.01, maxOffset: 45,
  opening: [[0.35, 0.36, 0.37], [0.37 / 0.99, 0.37 / 0.98, 0.37 / 0.97]], scarcity: false, shop: 0.09, shopOffset: 0.018};
const clamp = x => Math.max(0, Math.min(1, x));

function vanillaOffer(state, type, size, random, rules = A7) {
  let rare = 0, uncommon = 0;
  for (let i = 0; i < size; i++) {
    const t = rules.base[type] + (type === 'fixed' ? 0 : -0.05 + state.offset * rules.growth);
    const p = clamp(t);
    if (random() < p) { rare++; if (type !== 'fixed') state.offset = 0; }
    else {
      // Negative Rare thresholds shrink the Uncommon interval in vanilla.
      uncommon += (clamp(t + (type === 'elite' ? 0.4 : 0.37)) - p) / (1 - p);
      if (type !== 'fixed') state.offset = Math.min(rules.maxOffset, state.offset + 1);
    }
  }
  return {rare, uncommon, companions: size - rare};
}
function modOffer(state, type, size, threshold, rules = A7) {
  let rare = 0;
  const eligible = type === 'fixed' || rules.base[type] - 0.05 + Math.min(rules.maxOffset, state.offset + size - 1) * rules.growth > 1e-12;
  for (let i = 0; i < size; i++) {
    const p = clamp(rules.base[type] + (type === 'fixed' ? 0 : -0.05 + state.offset * rules.growth));
    if (i === 0 && state.pending && eligible) {
      rare = 1; state.pending = false; state.luck = 0; state.threshold = threshold();
      if (type !== 'fixed') state.offset = 0;
    } else {
      state.luck += -Math.log1p(-p);
      if (state.luck >= state.threshold && (!rules.scarcity || (rare === 0 && !state.pending))) {
        rare++; state.luck = 0; state.threshold = threshold();
        if (type !== 'fixed') state.offset = 0;
      } else {
        if (rules.scarcity && state.luck >= state.threshold) state.pending = true;
        if (type !== 'fixed') state.offset = Math.min(rules.maxOffset, state.offset + 1);
      }
    }
  }
  let fixed = 0, coefficient = size - rare;
  if (!rare) {
    if (state.nonRareOrdinal < 2) {
      fixed = rules.opening[state.nonRareOrdinal].reduce((a, b) => a + b, 0);
      coefficient -= 3;
    }
    state.nonRareOrdinal++;
  }
  return {rare, fixed, coefficient};
}
function blank() { return {offers: 0, slots: 0, vRare: 0, mRare: 0, vBearing: 0, mBearing: 0,
  vU0: 0, vU1: 0, vC0: 0, vC1: 0, mFixed: 0, mC0: 0, mC1: 0, vMulti: 0, mMulti: 0,
  m3r1: 0, m3r2: 0, m3r3: 0, m5r1: 0, m5r2: 0, m5r3: 0, m5r4: 0, m5r5: 0}; }
function addRecord(a, v, m, size) {
  a.offers++; a.slots += size; a.vRare += v.rare; a.mRare += m.rare;
  a.vBearing += Number(v.rare > 0); a.mBearing += Number(m.rare > 0);
  a.vMulti += Number(v.rare > 1);
  a.mMulti += Number(m.rare > 1);
  if (m.rare) a[`m${size}r${m.rare}`]++;
  a[v.rare ? 'vU1' : 'vU0'] += v.uncommon;
  a[v.rare ? 'vC1' : 'vC0'] += v.companions;
  a[m.rare ? 'mC1' : 'mC0'] += m.coefficient; a.mFixed += m.fixed;
}
function sumRecords(records) {
  const result = blank();
  for (const a of records) for (const k of Object.keys(result)) result[k] += a[k];
  return result;
}
function retainedUncommons(size, rares, q) {
  // Common-first replacement retains min(original Uncommons, remaining non-Rare slots).
  const n = size - 1, remaining = size - rares;
  let expected = 0, choose = 1;
  for (let u = 0; u <= n; u++) {
    expected += Math.min(u, remaining) * choose * q ** u * (1 - q) ** (n - u);
    choose = choose * (n - u) / (u + 1);
  }
  return expected;
}
function companionTotal(a, q) {
  if (!a.mMulti) return q * a.mC1;
  let total = 0;
  for (const size of [3, 5]) for (let k = 1; k <= size; k++) total += a[`m${size}r${k}`] * retainedUncommons(size, k, q);
  return total;
}
function fit(a) {
  let lo = 0, hi = 1;
  for (let i = 0; i < 50; i++) { const mid = (lo + hi) / 2; if (companionTotal(a, mid) < a.vU1) lo = mid; else hi = mid; }
  return {ordinary: (a.vU0 - a.mFixed) / a.mC0, companion: (lo + hi) / 2};
}
function evaluate(a, p, runs) {
  const modUncommon = a.mFixed + p.ordinary * a.mC0 + companionTotal(a, p.companion);
  return {offersPerRun: a.offers / runs, slotsPerRun: a.slots / runs,
    vanillaUncommonPerRun: (a.vU0 + a.vU1) / runs, modUncommonPerRun: modUncommon / runs,
    uncommonDifferencePer100Slots: 100 * (modUncommon - a.vU0 - a.vU1) / a.slots,
    vanillaRarePerRun: a.vRare / runs, modRarePerRun: a.mRare / runs,
    vanillaBearingOffersPerRun: a.vBearing / runs, modBearingOffersPerRun: a.mBearing / runs,
    vanillaMultiRareOffersPerRun: a.vMulti / runs,
    modMultiRareOffersPerRun: a.mMulti / runs,
    vanillaOrdinaryConditionalUncommon: a.vU0 / a.vC0,
    vanillaCompanionConditionalUncommon: a.vU1 / a.vC1};
}
function interval(values) {
  const mean = values.reduce((a, b) => a + b, 0) / values.length;
  const variance = values.reduce((a, b) => a + (b - mean) ** 2, 0) / (values.length - 1);
  // Batch means keep all cards/shops in a run together; 2.04 is t(0.975,31).
  return {mean, halfWidth95: 2.04 * Math.sqrt(variance / values.length)};
}

function runScenario(config, runs, seedOffset) {
  const rules = config.scarcity === false ? A6 : A7;
  const batches = Array.from({length: 32}, () => ({reward: blank(), shopRare: 0, shopAny: 0, shops: 0}));
  const bySource = {}, byAct = [blank(), blank(), blank()];
  const shopsByAct = Array.from({length: 3}, () => ({n: 0, rare: 0, any: 0, gold: 0, offset: 0}));
  const counters = {}, waits = {vanilla: Array(42).fill(0), mod: Array(42).fill(0)};
  for (let run = 0; run < runs; run++) {
    const seed = (seedOffset + Math.imul(run + 1, 2654435761)) >>> 0;
    const vr = rng(seed ^ 0x85EBCA6B), mr = rng(seed ^ 0xC2B2AE35);
    const threshold = () => -Math.log(1 - (mr() + 0.5 / 4294967296));
    const v = {offset: 0}, m = {offset: 0, luck: 0, threshold: threshold(), pending: false, nonRareOrdinal: 0};
    const batch = batches[run % batches.length];
    let vWait = 0, mWait = 0;
    simulateEnvironment(seed, config, {
      count(name, n) { counters[name] = (counters[name] ?? 0) + n; },
      actStart() { v.offset = 0; m.offset = 0; },
      actEnd(act) { if (m.pending) counters['pendingAtActEnd:' + act] = (counters['pendingAtActEnd:' + act] ?? 0) + 1; },
      offer(type, size, source, act) {
        const a = vanillaOffer(v, type, size, vr, rules), b = modOffer(m, type, size, threshold, rules);
        for (const group of [batch.reward, byAct[act], bySource[source] ??= blank()]) addRecord(group, a, b, size);
        vWait++; mWait++;
        if (a.rare) { waits.vanilla[Math.min(41, vWait)]++; vWait = 0; }
        if (b.rare) { waits.mod[Math.min(41, mWait)]++; mWait = 0; }
      },
      shop(act, gold) {
        const offset = -0.05 + v.offset * rules.growth, p = clamp(rules.shop + offset);
        batch.shops++; batch.shopRare += p; batch.shopAny += 1 - (1 - p) ** 5;
        const a = shopsByAct[act]; a.n++; a.rare += p; a.any += 1 - (1 - p) ** 5; a.gold += gold; a.offset += offset;
      }
    });
  }
  const reward = sumRecords(batches.map(b => b.reward)), parameters = fit(reward);
  const shopCount = batches.reduce((n, b) => n + b.shops, 0);
  const shopRare = batches.reduce((n, b) => n + b.shopRare, 0) / shopCount;
  const shopAny = batches.reduce((n, b) => n + b.shopAny, 0) / shopCount;
  return {name: config.name, config, runs, seedOffset, parameters,
    monteCarlo95: {ordinary: interval(batches.map(b => fit(b.reward).ordinary)),
      companion: interval(batches.map(b => fit(b.reward).companion)),
      shopOffset: interval(batches.map(b => b.shopRare / b.shops - rules.shop))},
    shop: {rarePerCharacterSlot: shopRare, rarePerShop: 5 * shopRare, atLeastOneCharacterRare: shopAny,
      offsetMatchingMean: shopRare - rules.shop, constantAtMatchedMeanAny: 1 - (1 - shopRare) ** 5,
      currentPolicyRarePerShop: 5 * (rules.shop + rules.shopOffset), currentPolicyAny: 1 - (1 - rules.shop - rules.shopOffset) ** 5,
      byAct: shopsByAct.map(a => ({rarePerSlot: a.rare / a.n, any: a.any / a.n, meanGold: a.gold / a.n, meanOffset: a.offset / a.n}))},
    counters, waits, raw: reward, byAct, bySource};
}

function selfCheck() {
  const x = {offset: 0};
  const first = vanillaOffer(x, 'normal', 3, () => 0.999);
  assert.equal(first.rare, 0); assert.equal(x.offset, 3);
  assert(Math.abs(first.uncommon - opening[0].reduce((a, b) => a + b, 0)) < 1e-12);
  assert.equal(vanillaOffer({offset: 0}, 'fixed', 3, () => 0).rare, 3);
  const rare = {offset: 0}; vanillaOffer(rare, 'elite', 3, () => 0);
  assert.equal(rare.offset, 1); // Zero-chance first slot, Rare second slot, non-Rare third slot.
  const t = {offset: 0, luck: 0, threshold: 1e-6, pending: false, nonRareOrdinal: 0};
  const result = modOffer(t, 'fixed', 5, () => 1e-6);
  assert.equal(result.rare, 1); assert(t.pending);
  const thresholdBefore = t.threshold;
  assert.equal(modOffer(t, 'normal', 3, () => 999).rare, 0);
  assert.equal(modOffer(t, 'normal', 3, () => 999).rare, 0);
  assert(t.pending && t.threshold === thresholdBefore);
  assert.equal(modOffer(t, 'normal', 3, () => 999).rare, 1);
  assert(!t.pending);
  const s = {gold: 99, act: 0, hp: 50, maxHp: 75, deck: 12, basic: 8, floor: 2,
    visited: new Set(), relics: new Set(), potions: 0};
  const list = ['CrystalSphere', 'BrainLeech', 'RoomFullOfCheese'];
  const a = nextEvent(list, 0, s); assert.equal(a.id, 'BrainLeech'); assert.equal(a.skipped, 1);
  s.visited.add(a.id); assert.equal(nextEvent(list, a.cursor, s).id, 'RoomFullOfCheese');
  assert(!eligible('CrystalSphere', s)); s.act = 1; s.gold = 100; assert(eligible('CrystalSphere', s));
  assert.equal(otherSpending(25, () => 0.9), 0);
  assert.equal(otherSpending(100, () => 0.05), 0);
  let spendTotal = 0; const spendRng = rng(18);
  for (let i = 0; i < 20000; i++) spendTotal += otherSpending(100, spendRng);
  assert(Math.abs(spendTotal / 20000 - 22.5) < 0.4);
  const counts = [0, 0, 0];
  const r = rng(2311);
  for (let i = 0; i < 12000; i++) {
    const {nodes, elites} = route(i % 3, r);
    counts[elites - 1]++;
    assert.equal(nodes.filter(n => n.startsWith('E')).length, elites);
    assert.equal(nodes.filter(n => n === 'event').length, 3);
    assert.equal(nodes.filter(n => n === 'shop').length, 1);
    assert(nodes[0].startsWith('M'));
    assert(['M4', 'E4'].includes(nodes[nodes.indexOf('chest') + 1]));
  }
  assert(counts.every((n, i) => Math.abs(n / 12000 - [0.2, 0.6, 0.2][i]) < 0.02));
  const act3Counts = [0, 0, 0, 0];
  for (let i = 0; i < 12000; i++) {
    const {nodes, elites} = route(2, r, undefined, true);
    act3Counts[elites]++;
    assert.equal(nodes.filter(n => n.startsWith('E')).length, elites);
    assert.equal(nodes.filter(n => n === 'event').length, elites === 0 ? 4 : 3);
    assert(['M4', 'E4'].includes(nodes[nodes.indexOf('chest') + 1]));
  }
  assert(act3Counts.every((n, i) => Math.abs(n / 12000 - [0.4, 0.4, 0.15, 0.05][i]) < 0.02));
  // Independent exact propagation of ten normal offers checks the sampled rarity law.
  let dist = Array(91).fill(0); dist[0] = 1; let exactRare = 0;
  for (let slot = 0; slot < 30; slot++) {
    const next = Array(91).fill(0);
    dist.forEach((mass, i) => { const p = Math.max(0, 0.0149 - 0.05 + i * 0.005); exactRare += mass * p; next[0] += mass * p; next[Math.min(90, i + 1)] += mass * (1 - p); });
    dist = next;
  }
  let sampled = 0; const vr = rng(727);
  for (let run = 0; run < 20000; run++) { const state = {offset: 0}; for (let i = 0; i < 10; i++) sampled += vanillaOffer(state, 'normal', 3, vr).rare; }
  assert(Math.abs(sampled / 20000 - exactRare) < 0.02);
  const a6Opening = {offset: 0};
  for (let i = 0; i < 2; i++) {
    const offer = vanillaOffer(a6Opening, 'normal', 3, () => 0.999, A6);
    assert.equal(offer.rare, 0);
    assert(Math.abs(offer.uncommon - A6.opening[i].reduce((a, b) => a + b, 0)) < 1e-12);
  }
  for (const size of [3, 5]) for (let k = 1; k <= size; k++) {
    let delivered = 0;
    const state = {offset: 0, luck: 0, threshold: 1e-6, pending: false, nonRareOrdinal: 0};
    assert.equal(modOffer(state, 'fixed', size, () => ++delivered < k ? 1e-6 : 999, A6).rare, k);
    assert.equal(delivered, k); assert(!state.pending); assert.equal(state.offset, 0);
    for (const q of [0, 0.39, 1]) {
      let enumerated = 0;
      for (let mask = 0; mask < 2 ** (size - 1); mask++) {
        const cards = Array.from({length: size - 1}, (_, i) => mask & (1 << i) ? 'U' : 'C');
        const u = cards.filter(c => c === 'U').length;
        const mass = q ** u * (1 - q) ** (size - 1 - u);
        for (let extra = 1; extra < k; extra++) {
          const common = cards.indexOf('C'); cards[common >= 0 ? common : cards.indexOf('U')] = 'R';
        }
        enumerated += mass * cards.filter(c => c === 'U').length;
      }
      assert(Math.abs(enumerated - retainedUncommons(size, k, q)) < 1e-12);
    }
  }
  let a6Dist = Array(46).fill(0); a6Dist[0] = 1; let a6Exact = 0;
  for (let slot = 0; slot < 30; slot++) {
    const next = Array(46).fill(0);
    a6Dist.forEach((mass, i) => { const p = Math.max(0, 0.03 - 0.05 + i * 0.01); a6Exact += mass * p; next[0] += mass * p; next[Math.min(45, i + 1)] += mass * (1 - p); });
    a6Dist = next;
  }
  const ar = rng(891); let a6Vanilla = 0, a6Mod = 0;
  const threshold = () => -Math.log(1 - (ar() + 0.5 / 4294967296));
  for (let run = 0; run < 20000; run++) {
    const v = {offset: 0}, m = {offset: 0, luck: 0, threshold: threshold(), pending: false, nonRareOrdinal: 0};
    for (let i = 0; i < 10; i++) { a6Vanilla += vanillaOffer(v, 'normal', 3, ar, A6).rare; a6Mod += modOffer(m, 'normal', 3, threshold, A6).rare; }
  }
  assert(Math.abs(a6Vanilla / 20000 - a6Exact) < 0.025);
  assert(Math.abs(a6Mod / 20000 - a6Exact) < 0.025);
  return {status: 'passed', exactTenNormalRareCount: exactRare, sampledTenNormalRareCount: sampled / 20000,
    a6ExactTenNormalRareCount: a6Exact, a6VanillaTenNormalRareCount: a6Vanilla / 20000, a6ModTenNormalRareCount: a6Mod / 20000};
}

function main() {
  const a6 = process.argv[4] === 'a6';
  const runs = Number(process.argv[2] ?? 100000);
  assert(Number.isInteger(runs) && runs >= 1000, 'Use at least 1000 runs per scenario.');
  const output = path.resolve(process.argv[3] ?? path.join(__dirname, 'results.json'));
  const checks = selfCheck();
  let configs = [
    {name: 'baseline'}, {name: 'holdout'},
    {name: 'one_elite', routeVariant: 'one'}, {name: 'three_elites', routeVariant: 'three'},
    {name: 'overgrowth', act1: 'Overgrowth'}, {name: 'underdocks', act1: 'Underdocks'},
    {name: 'healthy', hpFraction: 0.95}, {name: 'low_hp', hpFraction: 0.2},
    {name: 'crystal_full_gold', crystalGold: 110}, {name: 'no_events', noEvents: true},
    {name: 'extra_normal_offer', extraNormal: true}, {name: 'prayer_wheel', prayerWheel: true},
    {name: 'lost_coffer', lostCoffer: true}, {name: 'no_orrery', orreryBuy: 0},
    {name: 'always_buy_orrery', orreryBuy: 1}, {name: 'brain_leech_always_share', cardChoice: 1},
    {name: 'retain_gold', keepGold: 1}
  ];
  if (a6) configs = configs.filter(c => ['baseline', 'holdout', 'one_elite', 'three_elites', 'prayer_wheel',
    'no_orrery', 'brain_leech_always_share', 'retain_gold'].includes(c.name))
    .concat({name: 'no_poverty', poverty: false}).map(c => ({...c, scarcity: false, act3LowElites: true}));
  const scenarios = configs.map((config, i) => {
    const n = i < 2 ? runs : Math.max(1000, Math.floor(runs / 4));
    const result = runScenario(config, n, i === 1 ? 0x3B6D1289 : 0x12AF31C7);
    console.log(JSON.stringify({scenario: config.name, runs: n, fit: result.parameters, shopOffset: result.shop.offsetMatchingMean}));
    return result;
  });
  const fitParameters = scenarios[0].parameters;
  const grid = a6 ? 200 : 400;
  const proposed = {ordinary: Math.round(fitParameters.ordinary * grid) / grid,
    companion: Math.round(fitParameters.companion * grid) / grid};
  for (const s of scenarios) {
    s.withBaselineFit = evaluate(s.raw, fitParameters, s.runs);
    s.withRounded = evaluate(s.raw, proposed, s.runs);
    if (a6) s.withA7Policy = evaluate(s.raw, {ordinary: 0.38, companion: 0.39}, s.runs);
    else s.withProvisional = evaluate(s.raw, {ordinary: 37 / 98.51, companion: 38 / 95}, s.runs);
    s.byActEvaluation = s.byAct.map(a => evaluate(a, proposed, s.runs));
    s.bySourceEvaluation = Object.fromEntries(Object.entries(s.bySource).map(([k, a]) => [k, evaluate(a, proposed, s.runs)]));
  }
  const report = {version: `StS2 v0.111.0, ${a6 ? 'A0–6' : 'A7+'} rarity; three-act completion model`, checks, poolSizes,
    fittingObjective: 'Match vanilla expected displayed Uncommon totals separately in non-Rare and Rare-bearing offers, accounting for mod opening triples and changed group sizes.',
    assumptions: [
      'All content unlocked; five characters equally weighted; Act 1 biomes 50/50 unless sensitivity specifies one.',
      'User route distribution, uniform insertions after first normal fight, chest anchored to original M4, act-one extra initial fight.',
      'Uniform legal modeled event branches; vanilla shuffled act+shared lists, live eligibility skipping and run-wide visited exclusions.',
      'Orrery 50% bought when entry gold covers actual 170-230 price; other spending follows afterwards, 10% keep remaining gold, otherwise integer uniform 0..max(0,gold-50), including fake shops.',
      'Shared vanilla economy/opportunity history drives both rarity models; card-choice feedback and mod relic-stock changes are outside this conditional comparison.',
      'Ordinary shop mean Rare count uses five character slots; fixed Rare/Uncommon colorless cards are excluded from fitted rates.',
      'HP is a 65%-of-75-max-HP representative living-player snapshot before each room; coarse deck/basic counts approximate enchantability. Potion use is 50% per combat when holding a potion.',
      'Projected deck changes approximate card acceptance/removal, and owned relic count approximates tradable inventory; these are not full vanilla IsAllowed state execution.',
      'All combats completed; no abandonment, death or HP-dependent routing. Actual rest sites, Ancient choices, Courier purchases and general purchased-item effects are absent.',
      'Random relic bags and Prayer Wheel are modeled; most relic effects on gold/HP and event mini-game outcomes are not. Crystal Sphere recovery defaults to 0; the earlier A7+ study also tests all 110 board gold as a sensitivity bound.',
      'Fake shops apply the requested spending rule, without the optional Foul Potion fight; vanilla ordinary-event advancement is retained before a Lantern Key forced event.',
      'Event outcomes outside gold, coarse eligibility and the shared random-rarity group are projected away; source code and the existing event catalog supply branches.',
      'This reproduces the specified rarity probability laws, not the game RNG bitstream or exact seed histories. Monte Carlo intervals exclude modeling uncertainty.'
    ], fitParameters, roundedCandidate: proposed, scenarios};
  if (a6) {
    report.settled = {ordinary: 0.4, companion: 0.405, shopOffset: A6.shopOffset};
    for (const s of scenarios) {
      s.withSettled = evaluate(s.raw, report.settled, s.runs);
      s.shop.previousPolicyRarePerShop = 5 * (A6.shop + 0.008);
      s.shop.previousPolicyAny = 1 - (1 - A6.shop - 0.008) ** 5;
    }
    report.assumptions.push(
    'A0–6 rarity odds with immediate multiple Rares and Common-first replacement across the entire offer; E[retained Uncommons] = E[min(Binomial(size-1,q),size-Rares)].',
    'Opening non-Rare triples use the settled vanilla probabilities conditional on no Rare: 35/36/37 percent, then 37/99,37/98,37/97.',
    'Act 3 elite counts 0/1/2/3 have probabilities 40/40/15/5 percent, except all-act routeVariant stress cases; zero elites deletes E1 and replaces E2 with an event, keeping the M4 chest anchor.',
    'Baseline keeps the previous A6-compatible gold economy. no_poverty changes combat/chest/boss gold for A0–2; other lower-ascension effects on HP, potion capacity, deck and route choice remain projected away.',
    'A0–6 settled shop offset is +0.018, replacing +0.008; the latter is retained only for historical comparison.'
    );
  }
  fs.writeFileSync(output, JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify({output, checks, roundedCandidate: proposed}));
}
if (require.main === module) main();
module.exports = {vanillaOffer, modOffer, selfCheck, runScenario, fit, evaluate, retainedUncommons, A6};
