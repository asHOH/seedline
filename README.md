# Seedline

A Slay the Spire 2 mod that keeps same-seed runs comparable across different routes and decisions.

In the base game, unrelated actions can shift later random results, making gameplay experience diverge. Seedline reduces that interference across rewards, shops, events and combat. For example, each run's first shop can offer the same initial cards on different floors when eligible cards match.

Choices still matter. Different decks, owned items, eligibility and numbers of rewards or effects used can change outcomes. Seedline preserves base-game behavior except for deliberate consistency tradeoffs and corrections. The [gameplay design](design/DESIGN.md) explains what stays comparable, what can still diverge, and changes to probabilities and save/load reroll options. The [card processing order](design/CARD_ORDER.md) is just a part of the design.

See [compatibility and saves](docs/compatibility.md) for playing with other mods, [implementation and build checks](docs/implementation.md) for development, and [research](research/README.md) for evidence and methods, including [event workbook maintenance](research/README.md#event-workbook).

## Supported setup

Requires **StS2 v0.113.1/d09d48e5** on Windows. Supports standard and Dread single-player runs without custom modifiers.

## Performance

In [headless tests](research/README.md#performance), median added time was under **1.5 ms per tested operation** with 30 cards and **11–25 ms for whole-deck operations** at the **2,000-permanent-card** full-alignment limit, including **1,900 Angers**.

| Operation (median ms) | 30 cards, vanilla | 30 cards, Seedline | 2,000 cards, vanilla | 2,000 cards, Seedline |
|---|---:|---:|---:|---:|
| Enter combat (includes shuffle) | 0.34 | 0.97 | 17.22 | 31.88 |
| Opening shuffle | 0.02 | 0.62 | 0.30 | 11.66 |
| Choose from hand | 0.12 | 0.41 | 0.16 | 1.11 |
| Retrieve from discard | 0.28 | 1.04 | 1.69 | 26.33 |
| Prepare save data | 0.56 | 2.02 | 1.20 | 19.26 |

## Deploy

Follow the [build and validation instructions](docs/implementation.md#build-and-validate) to create the package in `artifacts/Seedline/`.

Close the game, then run from the repository root in PowerShell 7, replacing the example game path with your installation:

```powershell
./scripts/deploy.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Enable mods through the game's loader and start a new run with **Seedline / 不歪种神器** enabled.

Seedline uses fully unlocked content in new runs while preserving profile unlocks and Ascension limits. If content is missing, the startup prompt summarizes the additions for you to accept or cancel.

The notice follows the game's language setting and supports all 17 game languages.

## Saves

Keep the game build, Seedline and RitsuLib setup fixed throughout each run. Continuing with Seedline disabled can discard its state. See [save locations, backups and update guidance](docs/compatibility.md#saves).
