// Gold/reward projection of vanilla v0.111.0; reference sources remain read-only.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..', 'sts2-decompiled');
const read = (dir, name) => fs.readFileSync(path.join(root, dir, name + '.cs'), 'utf8');
const names = (text, kind) => [...text.matchAll(new RegExp(`ModelDb\\.${kind}<(\\w+)>`, 'g'))].map(m => m[1]);
const db = read('MegaCrit.Sts2.Core.Models', 'ModelDb');
const shared = [...db.slice(db.indexOf('AllSharedEvents'), db.indexOf('AllSharedAncients')).matchAll(/\bEvent<(\w+)>/g)].map(m => m[1]);
const acts = Object.fromEntries(['Overgrowth', 'Underdocks', 'Hive', 'Glory'].map(name => {
  const text = read('MegaCrit.Sts2.Core.Models.Acts', name);
  return [name, names(text.slice(text.indexOf('AllEvents'), text.indexOf('NumberOfWeakEncounters')), 'Event').concat(shared)];
}));
const characters = ['Ironclad', 'Silent', 'Regent', 'Necrobinder', 'Defect'];
const relicInfo = {};
const pools = Object.fromEntries(characters.map(character => {
  const ids = [...new Set(['Shared', character].flatMap(prefix => {
    const text = read('MegaCrit.Sts2.Core.Models.RelicPools', prefix + 'RelicPool');
    return names(text.slice(text.indexOf('GenerateAllRelics'), text.indexOf('GetUnlockedRelics') < 0 ? undefined : text.indexOf('GetUnlockedRelics')), 'Relic');
  }))];
  const rarities = {Common: [], Uncommon: [], Rare: [], Shop: []};
  for (const id of ids) {
    const text = read('MegaCrit.Sts2.Core.Models.Relics', id);
    const rarity = text.match(/Rarity => RelicRarity\.(\w+)/)?.[1];
    relicInfo[id] = {rarity, shop: !text.includes('IsAllowedInShops => false'), cutoff: text.includes('IsBeforeAct3TreasureChest(runState)')};
    if (rarities[rarity]) rarities[rarity].push(id);
  }
  assert(rarities.Shop.includes('Orrery'));
  return [character, rarities];
}));

function rng(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6D2B79F5) >>> 0;
    let t = a;
    t = Math.imul(t ^ t >>> 15, t | 1);
    t ^= t + Math.imul(t ^ t >>> 7, t | 61);
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}
const integer = (r, lo, hi) => lo + Math.floor(r() * (hi - lo + 1));
function shuffle(items, r) {
  const a = items.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = integer(r, 0, i); [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

function route(act, r, variant, act3LowElites = false) {
  let base = ['M1', 'M2', 'E1', 'M3', 'M4', 'E2', 'M5'];
  if (act === 0) base.unshift('M0');
  if (r() < 0.5) {
    const e = base.indexOf('E1'), m = base.indexOf('M3');
    [base[e], base[m]] = [base[m], base[e]];
  }
  if (act === 2 && act3LowElites && variant === undefined) {
    const u = r();
    variant = u < 0.05 ? 'three' : u < 0.20 ? 'two' : u < 0.60 ? 'one' : 'none';
  }
  const v = variant ?? (r() < 0.2 ? 'one' : null);
  // The second draw is conditional so the three probabilities are 20/60/20.
  const change = v ?? (r() < 0.25 ? 'three' : 'two');
  if (change === 'one' || change === 'none') base = base.filter(x => x !== 'E1');
  if (change === 'none') base[base.indexOf('E2')] = 'event';
  const extra = shuffle(['event', 'event', 'event', 'shop', ...(r() < 0.5 ? ['Mx'] : [])], r);
  const first = base.shift();
  const flags = shuffle([...base.map(() => 0), ...extra.map(() => 1)], r);
  const result = [first, ...flags.map(flag => flag ? extra.shift() : base.shift())];
  if (change === 'three') {
    const candidates = result.map((x, i) => x.startsWith('M') ? i : -1).filter(i => i >= 0).slice(-2);
    const i = candidates[integer(r, 0, 1)]; result[i] = result[i] === 'M4' ? 'E4' : 'Ex';
  }
  result.splice(result.findIndex(x => x === 'M4' || x === 'E4'), 0, 'chest');
  return {nodes: result, elites: {none: 0, one: 1, two: 2, three: 3}[change]};
}

const hpThreshold = {ColossalFlower: 19, RoundTeaParty: 12, TheLegendsWereTrue: 10, TrashHeap: 6};
const goldThreshold = {CrystalSphere: 100, EndlessConveyor: 120, LuminousChoir: 149, MorphicGrove: 100,
  RanwidTheElder: 100, TeaMaster: 150, WaterloggedScriptorium: 55, WelcomeToWongos: 100,
  WhisperingHollow: 44, ZenWeaver: 125};
const actLimits = {BrainLeech: [0, 1], CrystalSphere: [1, 2], DollRoom: [1], FakeMerchant: [1, 2],
  PotionCourier: [1, 2], RanwidTheElder: [1, 2], RelicTrader: [1, 2], RoomFullOfCheese: [0, 1],
  StoneOfAllTime: [1], Symbiote: [1, 2], TeaMaster: [0, 1], TheLegendsWereTrue: [0], WelcomeToWongos: [1]};
function eligible(id, s) {
  if (id === 'WarHistorianRepy' || s.visited.has(id)) return false;
  if (actLimits[id] && !actLimits[id].includes(s.act)) return false;
  if (s.gold < (goldThreshold[id] ?? 0) || s.hp < (hpThreshold[id] ?? 0)) return false;
  switch (id) {
    case 'Amalgamator': return s.basic >= 4;
    case 'ByrdonisNest': return !s.pet;
    case 'FakeMerchant': return s.gold >= 100 || s.foul;
    case 'MorphicGrove': return s.deck >= 2;
    case 'RanwidTheElder': return s.potions > 0 && s.relics.size > 0;
    case 'RelicTrader': return s.relics.size >= 5;
    case 'StoneOfAllTime': return s.potions > 0;
    case 'TheFutureOfPotions': return s.potions >= 2;
    case 'PunchOff': return s.floor >= 6;
    case 'SlipperyBridge': return s.floor > 6 && s.deck > 0;
    case 'UnrestSite': return s.hp <= 0.7 * s.maxHp;
    case 'WoodCarvings': return s.basic > 0;
    case 'FieldOfManSizedHoles': case 'GraveOfTheForgotten': case 'SpiralingWhirlpool':
    case 'TheLegendsWereTrue': return s.deck > 0;
    default: return true;
  }
}
function nextEvent(list, cursor, s) {
  // Same shuffled-list cursor and pass-on-failure rule as RoomSet; never fresh-sample eligibles.
  for (let tried = 0; tried < list.length; tried++, cursor++) {
    const id = list[cursor % list.length];
    if (eligible(id, s)) return {id, cursor: cursor + 1, skipped: tried};
  }
  throw new Error('Eligible event pool exhausted; do not silently substitute a different sampling law.');
}

function otherSpending(gold, r, keep = 0.1) {
  return r() < keep ? 0 : integer(r, 0, Math.max(0, gold - 50));
}
function simulateEnvironment(seed, config, callbacks) {
  const r = rng(seed), choice = rng(seed ^ 0x9E3779B9);
  const character = characters[integer(r, 0, 4)];
  const s = {gold: 99, act: 0, floor: 1, deck: 12, basic: 8, maxHp: 75, hp: 49,
    potions: 0, drop: 0.4, foul: false, pet: false, key: false, boxCombats: 0, visited: new Set(), relics: new Set(),
    bags: Object.fromEntries(Object.entries(pools[character]).map(([k, v]) => [k, shuffle(v, r)]))};
  const count = (name, value = 1) => callbacks.count(name, value);
  const offer = (type, n, source) => { callbacks.offer(type, n, source, s.act); s.deck++; };
  function relic(rarity, fromShop = false, acquire = true, shopEligible = false) {
    rarity ??= (() => { const u = r(); return u < 0.5 ? 'Common' : u < 0.83 ? 'Uncommon' : 'Rare'; })();
    const bag = s.bags[rarity];
    // Vanilla permanently removes run-state-ineligible entries before pulling.
    s.bags[rarity] = bag.filter(id => !relicInfo[id].cutoff || s.floor < 41);
    const current = s.bags[rarity];
    const index = fromShop ? current.findLastIndex(id => relicInfo[id].shop) : shopEligible ? current.findIndex(id => relicInfo[id].shop) : 0;
    if (index < 0 || current.length === 0) { count('relicShortage'); return; }
    const id = current.splice(index, 1)[0];
    if (acquire) {
      s.relics.add(id);
      if (id === 'OldCoin') s.gold += 300;
    }
    return id;
  }
  function combat(type, source) {
    s.gold += type === 'elite' ? integer(r, 26, 33) : integer(r, 7, 15);
    offer(type, 3, source);
    if (type === 'normal' && (s.relics.has('PrayerWheel') || config.prayerWheel)) offer(type, 3, 'PrayerWheel');
    if (type === 'elite') relic();
    if (s.potions && choice() < 0.5) s.potions--;
    const drop = s.relics.has('WhiteBeastStatue') || r() < s.drop + (type === 'elite' ? 0.125 : 0);
    if (!s.relics.has('WhiteBeastStatue')) s.drop += drop ? -0.1 : 0.1;
    if (drop) s.potions = Math.min(3, s.potions + 1);
    finishCombat();
  }
  function finishCombat() {
    if (s.boxCombats > 0 && --s.boxCombats === 0) { relic(); relic(); relic(); }
  }
  function shop(fake = false) {
    count(fake ? 'fakeShops' : 'shops');
    if (!fake) {
      callbacks.shop(s.act, s.gold);
      relic(null, true, false); relic(null, true, false);
      const id = relic('Shop', true, false);
      if (id === 'Orrery') {
        count('orreryAppearances');
        const cost = Math.round(200 * (0.85 + 0.3 * r()));
        if (s.gold >= cost) {
          count('orreryAffordable');
          if (choice() < (config.orreryBuy ?? 0.5)) {
            count('orreryBought'); s.gold -= cost; s.relics.add(id);
            for (let i = 0; i < 5; i++) offer('fixed', 3, 'Orrery');
          }
        }
      }
    }
    const spent = otherSpending(s.gold, choice, config.keepGold ?? 0.1);
    s.gold -= spent; count('otherSpending', spent);
  }
  function addPotions(n) { s.potions = Math.min(3, s.potions + n); }
  function remove(n) { const k = Math.min(n, s.deck); s.deck -= k; s.basic = Math.max(0, s.basic - k); }
  function loseRelic() { const ids = [...s.relics]; s.relics.delete(ids[integer(choice, 0, ids.length - 1)]); }
  function event(id) {
    count('event:' + id);
    const pick = n => integer(choice, 0, n - 1);
    switch (id) {
      case 'BrainLeech': if (choice() < (config.cardChoice ?? 0.5)) offer('fixed', 5, id); break;
      case 'Trial': {
        // shortcut: condition on continuing the run, rather than randomly confirming abandonment.
        const trial = pick(3), verdict = pick(2);
        if (trial === 0 && verdict === 0) { relic(); relic(); }
        if (trial === 1 && verdict === 1) s.gold += 300;
        if (trial === 2 && verdict === 0) { offer('fixed', 3, id); offer('fixed', 3, id); }
        break;
      }
      case 'DenseVegetation': if (pick(2)) { if (s.relics.has('DreamCatcher')) offer('normal', 3, 'DreamCatcher'); combat('normal', id); } else s.gold += integer(r, 61, 99); break;
      case 'PunchOff': relic(); if (pick(2)) { combat('normal', id); addPotions(1); } break;
      case 'TheLanternKey': if (pick(2)) { combat('normal', id); s.key = true; } else s.gold += 100; break;
      case 'FakeMerchant': shop(true); break;
      case 'JungleMazeAdventure': s.gold += Math.floor((pick(2) ? 50 : 150) + (r() * 30 - 15)); break;
      case 'SunkenStatue': if (pick(2)) s.gold += Math.floor(111 + r() * 20 - 10); break;
      case 'SunkenTreasury': s.gold += pick(2) ? integer(r, 303, 363) : integer(r, 52, 67); break;
      case 'LostWisp': if (pick(2)) s.gold += Math.floor(60 + r() * 30 - 15); else s.relics.add(id); break;
      case 'ThisOrThat': if (pick(2)) relic(); else s.gold += integer(r, 41, 68); break;
      case 'TrashHeap': if (pick(2)) { s.gold += 100; s.deck++; } else s.relics.add(['DarkstonePeriapt', 'DreamCatcher', 'HandDrill', 'MawBank', 'TheBoot'][pick(5)]); break;
      case 'ColossalFlower': {
        let depth = 0; while (depth < 3 && pick(2)) depth++;
        if (depth < 3) s.gold += [35, 75, 135][depth]; else s.relics.add('PollinousCore');
        break;
      }
      case 'LuminousChoir': if (pick(2)) { remove(2); s.deck++; } else { s.gold -= integer(r, 100, 149); relic(); } break;
      case 'MorphicGrove': if (pick(2)) s.maxHp += 5; else s.gold = 0; break;
      case 'TeaMaster': s.gold -= [50, 150, 0][pick(3)]; break;
      case 'WaterloggedScriptorium': s.gold -= [0, 55, ...(s.gold >= 99 ? [99] : [])][pick(s.gold >= 99 ? 3 : 2)]; break;
      case 'WhisperingHollow': if (pick(2)) { s.gold -= integer(r, 26, 44); addPotions(2); } break;
      case 'ZenWeaver': { const costs = s.gold >= 250 ? [50, 125, 250] : [50, 125]; const cost = costs[pick(costs.length)]; s.gold -= cost; if (cost > 50) remove(cost / 125); else s.deck += 2; break; }
      case 'WelcomeToWongos': {
        const featured = relic('Rare', false, false, true); // Vanilla generates this even below 200 gold.
        const costs = [0, 100, ...(s.gold >= 200 ? [200] : []), ...(s.gold >= 300 ? [300] : [])];
        const cost = costs[pick(costs.length)]; s.gold -= cost;
        if (cost === 100) relic('Common', false, true, true); if (cost === 200 && featured) s.relics.add(featured);
        if (cost === 300) { s.relics.add('WongosMysteryTicket'); s.boxCombats = 5; } break;
      }
      case 'RanwidTheElder': { const p = pick(3); if (p === 0) s.potions--; if (p === 1) s.gold -= 100; if (p === 2) loseRelic(); relic(); if (p === 2) relic(); break; }
      case 'RelicTrader': { const replacements = [relic(null, false, false), relic(null, false, false), relic(null, false, false)]; loseRelic(); const id = replacements[pick(3)]; if (id) s.relics.add(id); break; }
      case 'WarHistorianRepy': if (pick(2)) { relic(); relic(); addPotions(2); } break;
      case 'CrystalSphere': {
        const paid = !pick(2); if (paid) s.gold -= integer(r, 51, 99);
        // shortcut: minigame recovery is bounded by 0/110 gold scenarios; implement board play if this changes calibration.
        s.gold += config.crystalGold ?? 0;
        break;
      }
      case 'EndlessConveyor': {
        let grabs = 0, last = '';
        while (s.gold >= 40 && pick(2)) {
          grabs++;
          const dishes = [['maxHp', 6], ['upgrade', 3], ['transform', 3], ['card', 3],
            ...(s.potions < 3 ? [['potion', 3]] : []), ...(s.hp < s.maxHp ? [['heal', 6]] : []), ...(grabs > 1 ? [['gold', 1]] : [])].filter(([id]) => id !== last);
          let u = r() * dishes.reduce((n, d) => n + d[1], 0);
          const dish = grabs % 5 === 0 ? 'fixedCard' : dishes.find(([, weight]) => (u -= weight) < 0)[0];
          last = dish;
          if (dish === 'gold') s.gold += 75; else s.gold -= 40;
          if (dish === 'maxHp') s.maxHp += 4;
          if (dish === 'heal') s.hp = Math.min(s.maxHp, s.hp + 10);
          if (dish === 'potion') addPotions(1);
          if (dish === 'card' || dish === 'fixedCard') s.deck++;
        }
        break;
      }
      case 'PotionCourier': if (pick(2)) addPotions(1); else { addPotions(3); s.foul = true; } break;
      case 'StoneOfAllTime': if (pick(2)) { s.potions--; s.maxHp += 10; } break;
      case 'TheFutureOfPotions': s.potions--; s.deck++; break;
      case 'ByrdonisNest': if (pick(2)) s.pet = true; else s.maxHp += 7; break;
      case 'TheLegendsWereTrue': if (pick(2)) addPotions(1); else s.relics.add('SpoilsMap'); break;
      case 'BattlewornDummy': { const setting = pick(3); if (setting === 0) addPotions(1); if (setting === 2) relic(); break; }
      case 'RoundTeaParty': if (pick(2)) relic(); else s.relics.add('RoyalPoison'); break;
      case 'UnrestSite': if (pick(2)) { s.maxHp -= 8; relic(); } break;
      case 'Wellspring': if (pick(2)) { remove(1); s.deck++; } else addPotions(1); break;
      case 'DrowningBeacon': if (pick(2)) addPotions(1); else { s.maxHp -= 13; s.relics.add('FresnelLens'); } break;
      case 'SlipperyBridge': remove(1); break;
      case 'Amalgamator': remove(2); s.deck++; break;
      case 'WoodCarvings': if (pick(3) < 2) s.basic = Math.max(0, s.basic - 1); break;
      case 'FieldOfManSizedHoles': if (pick(2)) { remove(2); s.deck++; } break;
      case 'DoorsOfLightAndDark': if (pick(2)) remove(1); break;
      case 'Reflections': if (pick(2)) { s.deck *= 2; s.basic *= 2; } break;
      case 'RoomFullOfCheese': if (pick(2)) s.deck++; break;
      case 'ColorfulPhilosophers': s.deck += 3; break;
      case 'InfestedAutomaton': case 'Bugslayer': case 'TinkerTime': s.deck++; break;
      case 'AbyssalBaths': case 'AromaOfChaos': case 'DollRoom': case 'GraveOfTheForgotten':
      case 'HungryForMushrooms': case 'SapphireSeed': case 'SelfHelpBook': case 'SpiralingWhirlpool':
      case 'SpiritGrafter': case 'Symbiote': case 'TabletOfTruth': break;
      default: throw new Error('Unhandled event: ' + id);
    }
    assert(s.gold >= 0 && Number.isInteger(s.gold), `${id}: invalid gold ${s.gold}`);
  }
  for (let act = 0; act < 3; act++) {
    s.act = act; callbacks.actStart(act);
    const actName = act === 0 ? (config.act1 ?? (r() < 0.5 ? 'Overgrowth' : 'Underdocks')) : act === 1 ? 'Hive' : 'Glory';
    const list = shuffle(acts[actName], r); let cursor = 0;
    const itinerary = route(act, r, config.routeVariant, config.act3LowElites);
    count('routeElites:' + itinerary.elites);
    if (act === 0 && config.lostCoffer) offer('fixed', 3, 'LostCoffer');
    for (let i = 0; i < itinerary.nodes.length; i++) {
      const node = itinerary.nodes[i];
      s.floor = [1, 18, 34][act] + i + 1;
      // shortcut: representative living-player HP and deck counts, replace with run histories if sensitivities matter.
      s.hp = Math.max(1, Math.floor(s.maxHp * (config.hpFraction ?? 0.65)));
      if (node.startsWith('M')) combat('normal', 'normal');
      else if (node.startsWith('E')) combat('elite', 'elite');
      else if (node === 'chest') {
        s.gold += Math.floor(integer(r, 42, 52) * 0.75); relic();
        if (config.extraNormal) offer('normal', 3, 'extraNormal');
      } else if (node === 'shop') shop();
      else if (node === 'event' && !config.noEvents) {
        const next = nextEvent(list, cursor, s); cursor = next.cursor;
        count('eventsPassed', next.skipped);
        // Vanilla advances the ordinary list before replacing it with this forced event.
        const id = s.key && act === 2 ? 'WarHistorianRepy' : next.id;
        if (id === 'WarHistorianRepy') s.key = false;
        s.visited.add(id); event(id);
      }
    }
    count('endGold:' + act, s.gold);
    callbacks.actEnd(act);
    if (act < 2) s.gold += 75;
    finishCombat();
  }
}

module.exports = {rng, integer, shuffle, route, eligible, nextEvent, otherSpending,
  simulateEnvironment, poolSizes: Object.fromEntries(Object.entries(pools).map(([c, p]) => [c, Object.fromEntries(Object.entries(p).map(([r, a]) => [r, a.length]))]))};
