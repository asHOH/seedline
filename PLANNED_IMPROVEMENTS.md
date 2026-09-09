# Planned improvements

## Aim

Make same-seed competitive runs reproduce the same outcomes at equivalent ordinal opportunities. Preserve meaningful consequences of choices, while removing accidental RNG coupling between unrelated systems.

## Core invariants

- Same seed, rules, ordinal opportunity, and eligible pool produce the same result.
- Only consuming an opportunity in a domain advances that domain's sequence.
- Act-local systems reset at act boundaries; explicitly global sequences remain cumulative.
- Save/load and irrelevant UI actions do not advance gameplay RNG.

## Rare-card progression — unresolved

Catfood initializes its separated rarity offsets at `0` instead of vanilla's `-0.05`. This partly compensates for slower independent accumulation, but visibly permits a rare card in the first fight.

Ancient events reset the offsets while reward ordinals remain cumulative. Different earlier fight counts can therefore place a rare result onto different card-choice sets. Preserve this as an explicit open design tradeoff until we choose between vanilla global accumulation and a modified act-local schedule.

## Lantern Key forced event

Catfood increments `eventEntered` for War Historian Repy like any random event; it has no explicit exception.

Treat this forced first Act 3 event as ordinal `0` and do not advance the ordinary Act 3 event sequence. The key-holder's following random event should match the non-key-holder's first random event.

## Potion sequences

Use one sequence for free potions from rewards, events, and combat generation. Keep shop potion generation separate: entering or skipping a shop may advance later shop potions, but must not change later free-potion drops or identities.

## Fixed-rarity items

- Wongos is already correct: a fixed-rarity offer bypasses Catfood's random-rarity list and pulls the first eligible relic of that rarity from the relic grab bag.
- Crystal Sphere fixed-rarity potions bypass Catfood's potion sequence and use event RNG. Route them through the free-potion sequence with a fixed-rarity filter.
- Fixed-rarity rewards consume an item identity, but not a random-rarity roll.

## Act-local encounter setup and shuffle

Catfood uses cumulative fight counters for private encounter RNG and initial combat shuffle. Key both by act, category, and within-act ordinal instead. Ordinary monster HP is outside the private encounter RNG, and reward ordinals remain cumulative.

```text
Private encounter RNG / initial shuffle = seed + act + category + within-act ordinal
Reward RNG                              = seed + cumulative category ordinal
```

Example: after 5 versus 6 Act 1 fights, the same first Act 2 encounter gets the same randomized setup and initial shuffle, while its rewards may differ.
