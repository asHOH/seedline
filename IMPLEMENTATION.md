# Implementation guide

[DESIGN.md](DESIGN.md) owns gameplay requirements; [CARD_ORDER.md](CARD_ORDER.md) owns ranking.

## Technical baseline

- **Stack:** C# `net9.0` class library; pin a tested .NET 10 SDK in `global.json`.
- **Integration:** built-in loader, explicit initializer, bundled Harmony, DLL/manifest packaging; prefer prefixes/postfixes.
- **Dependencies:** local game assemblies, platform JSON and game logging; omit RitsuLib. Pin packages, commit lock files and exclude game assemblies from distribution.
- **Persistence:** one versioned vanilla/mod checkpoint snapshot through game save facilities, using a mod-specific filename; validate restoration, reload rewrites, backups and cloud behavior in Stage 1.
- **Compatibility:** initially Windows and the scoped vanilla build; reject Catfood and unsupported game builds.
- **Runs:** require the mod from creation through completion; reject missing/incompatible state while preserving saves.
- **Identity/deployment:** `SameSeedConsistency`, display name "Same-Seed Consistency", version `0.1.0`, gameplay-affecting manifest; local game path and explicit deployment.
- **Checks:** `.editorconfig`, nullable checking, built-in analyzers, native precommit whitespace/excluded-file checks, one formatting/build/test command, xUnit v3 from Stage 2, and in-game integration checks; narrow dot-directory ignores.

## Implementation order

1. **Integration:** establish a loadable mod for the scoped version. Identify interception points from reference code. Record each operation's state owner, commitment point and restoration path.
2. **Foundation:** implement stable address composition, independent RNG derivation and run-owned persistent state. Preserve existing checkpoints; distinguish fresh opportunities from reconstruction to prevent double consumption.
3. **First playable path:** implement combat-gold addressing through generation, acquisition and reload. Verify ordinary rewards and parent-event reconstruction after event combat.
4. **Rewards:** implement shared supplies, then relics/chests, potions, shops and card rewards. Establish processing order, occurrence lifetime and reward keys as dependencies arise.
5. **Non-combat:** implement clocks and deck routing before transformations and remaining policies; verify card lifetime and persistence.
6. **Combat:** integrate combat policies, including card-lifetime rules.

## Completion

Track coverage against the design's architecture overview. Reuse existing checks; exercise matching-seed runs, changed routes, equivalent-copy choices and checkpoint reloads. Compare against preservation rules and deviations.
