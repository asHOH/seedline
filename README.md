# Seedline

A Slay the Spire 2 mod that gives same-seed runs similar gameplay experiences — rewards, choices and combat challenges — across different routes and decisions.

Seedline reduces the influence of unrelated actions on later luck. For example, your first shop can offer the same initial cards on different routes when eligible cards match.

Your choices still matter. Some probabilities and save/load reroll options change to keep experiences similar.

## Supported setup

Requires **StS2 v0.113.1** on Windows. Supports standard and Dread single-player runs without custom modifiers.

## Performance

With 30-card decks, [benchmarks](research/README.md#performance) measured under **1.5 ms median added time per tested operation**.

## Deploy

[Build and validate](docs/implementation.md#build-and-validate), then close the game. From the repository root in PowerShell 7, deploy using your game path:

```powershell
./scripts/deploy.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Enable **Seedline / 不歪种神器** in the game's mod loader and start a new run. New runs use fully unlocked content without changing profile unlocks or Ascension limits.

## Saves

Keep the game build, Seedline and RitsuLib setup fixed during a run. Disabling Seedline can discard its saved state. See [compatibility and save guidance](docs/compatibility.md).

## Learn more

- [Gameplay design](design/DESIGN.md) — rules and tradeoffs.
- [Development](docs/implementation.md) — building, testing and implementation.
- [Research](research/README.md) — evidence and methods.
