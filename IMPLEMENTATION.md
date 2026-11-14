# Implementation guide

[DESIGN.md](DESIGN.md) owns gameplay requirements; [CARD_ORDER.md](CARD_ORDER.md) owns ranking.

## Architecture

- **Stack:** C# `net9.0` class library with a pinned, tested .NET 10 SDK.
- **Integration:** built-in loader, explicit initializer and bundled Harmony; prefer prefixes/postfixes. Package as a gameplay-affecting DLL/manifest mod with explicit deployment.
- **Dependencies:** local game assemblies, platform JSON and game logging; omit RitsuLib. Exclude game assemblies from distribution.
- **Persistence:** one versioned vanilla/mod checkpoint snapshot through game save facilities, using a mod-specific filename.
- **Compatibility:** initially Windows and the scoped vanilla build; reject Catfood and unsupported game builds.
- **Runs:** require the mod from creation through completion; reject missing/incompatible state while preserving saves.

## Implementation order

For each implemented operation, identify its interception point from reference code, state owner, commitment point and restoration path.

1. **Integration:** establish a loadable mod. Before proceeding, validate snapshot restoration, reload rewrites, backups and cloud behavior.
2. **Foundation:** implement stable addresses, independent RNG derivation and run-owned state. Verify deterministic results and checkpoint round-trips, including reconstruction without double consumption.
3. **First playable path:** implement combat-gold addressing through generation, acquisition and reload. Verify ordinary rewards and parent-event reconstruction after event combat.
4. **Rewards:** implement shared supplies, then relics/chests, potions, shops and card rewards. Before dependent card policies, establish processing order, occurrence lifetime, card keys and checkpoint rebinding, including starting and temporarily stored cards.
5. **Non-combat:** implement clocks and deck routing before transformations and remaining policies; verify card lifetime and persistence.
6. **Combat:** integrate combat policies, including card-lifetime rules.

## Validation gates

Use one formatting/build/test command with nullable checking and analyzers; add xUnit v3 in Foundation. Require passing automated and in-game checks for each stage.

Track coverage against the design's architecture overview. Reuse existing checks; exercise matching-seed runs, changed routes, equivalent-copy choices and checkpoint reloads. Compare against preservation rules and deviations.
