# Implementation guide

[DESIGN.md](DESIGN.md) owns gameplay requirements; [CARD_ORDER.md](CARD_ORDER.md) owns ranking.

## Implementation order

1. **Integration:** establish a loadable mod for the scoped version. Identify interception points from reference code. Record each operation's state owner, commitment point and restoration path.
2. **Foundation:** implement stable address composition, independent RNG derivation and run-owned persistent state. Preserve existing checkpoints; distinguish fresh opportunities from reconstruction to prevent double consumption.
3. **First playable path:** implement combat-gold addressing through generation, acquisition and reload. Verify ordinary rewards and parent-event reconstruction after event combat.
4. **Rewards:** implement shared supplies, then relics/chests, potions, shops and card rewards. Establish processing order, occurrence lifetime and reward keys as dependencies arise.
5. **Non-combat:** implement clocks and deck routing before transformations and remaining policies; verify card lifetime and persistence.
6. **Combat:** integrate combat policies, including card-lifetime rules.

## Completion

Track coverage against the design's architecture overview. Reuse existing checks; exercise matching-seed runs, changed routes, equivalent-copy choices and checkpoint reloads. Compare against preservation rules and deviations.
