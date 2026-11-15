# Implementation guide

[DESIGN.md](DESIGN.md) owns gameplay rules; [CARD_ORDER.md](CARD_ORDER.md) owns ranking.

## Architecture

- C# `net9.0`, pinned .NET 10 SDK, built-in loader and bundled Harmony; prefer prefixes/postfixes.
- Use local game assemblies, platform JSON and game logging. Package only the mod DLL and manifest; deploy explicitly.
- Save vanilla and mod state in one versioned checkpoint at the standard modded save path, using game save and cloud facilities. [Checkpoint contract](docs/stage-1.md).
- Keep RitsuLib optional, with identical SameSeed RNG behavior and state format in both configurations.
- Support Windows and the scoped game build. Require SameSeed throughout the run; reject Catfood and incompatible state while preserving saves.

## Delivery order

For each operation, record its interception point, state owner, commitment point and restoration path.

1. **Integration:** standard-path checkpoints and optional RitsuLib v0.6.13 bridge. [Boundaries and verification](docs/stage-1.md).
2. **Foundation:** complete. Stable addresses, independent randomness and run-owned state, with deterministic-roll, snapshot-isolation and replay checks. [Foundation contract](docs/stage-2.md).
3. **First playable path:** combat-gold generation, acquisition and reload implemented, including parent-event reconstruction checks. [Coverage and pending gameplay checks](docs/stage-3.md).
4. **Rewards:** shared supplies, relics/chests, potions, shops and card offers implemented, with permanent-card processing order, occurrence lifetime, keys and checkpoint rebinding. [Coverage and gameplay checks](docs/stage-4.md).
5. **Non-combat:** clocks and deck routing, then transformations and remaining policies.
6. **Combat:** combat policies and card-lifetime rules.

## Validation

Run [one command](README.md#build-and-validate) for formatting, analyzers, xUnit v3 tests and isolated in-engine checks. Require automated and relevant gameplay checks at each stage. Explain failures, their impact and remaining limits in validation reports.

Track coverage against the design's architecture overview. Check matching seeds, changed routes, equivalent-copy choices and reloads against preservation rules and declared deviations. See [verification limits](docs/stage-1.md#cloud-and-verification-limits).
