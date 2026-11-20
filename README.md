# Seedline

A Slay the Spire 2 mod that keeps same-seed runs comparable across different routes and decisions.

In the base game, unrelated actions can shift later random results. Seedline aligns corresponding opportunities across rewards, shops, events and combat to reduce that interference. For example, each run's first shop can offer the same initial cards on different floors when eligible cards match.

Choices still matter: decks, owned items, eligibility and the number of rewards or effects used can change outcomes. The mod makes deliberate changes to some probabilities and reroll options to improve outcome consistency.

Designs: [gameplay rules](design/DESIGN.md) and [card processing order](design/CARD_ORDER.md). Guides: [implementation and compatibility](docs/implementation.md) and [research](research/README.md), including [event workbook maintenance](research/README.md#event-workbook).

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

## Build and validate

Install the .NET SDK pinned in [global.json](global.json). In PowerShell 7, run from the repository root, replacing the example game path with your installation:

```powershell
./scripts/verify.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

The command builds and checks the mod, using an isolated game copy for integration checks; allow disk space for that copy. Use `-Dotnet 'path/to/dotnet.exe'` for a portable SDK. With RitsuLib installed, include the [RitsuLib checks](#ritsulib-compatibility).

The validated package is written to `artifacts/Seedline/`.

## Deploy

Close the game, then deploy the validated package:

```powershell
./scripts/deploy.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Enable mods through the game's loader and start a new run with **Seedline / 不歪种神器** enabled.

Seedline uses fully unlocked content in new runs while preserving profile unlocks and Ascension limits. If content is missing, the startup prompt summarizes the additions for you to accept or cancel.

The notice follows the game's language setting and supports all 17 game languages.

## RitsuLib compatibility

RitsuLib is optional. Keep its complete package, including `compat/0.113.0`, which supplies the runtime for StS2 v0.113.1. Untested versions produce a warning rather than a version-based startup failure. See the [tested mods](docs/implementation.md#tested-mods) and [supported save-data scope](docs/implementation.md#ritsulib).

Add `-RitsuLibDir '<extracted RitsuLib package>'` to the build-and-validate command when using RitsuLib.

## Saves

Saves use `modded/profileN/saves/current_run.save` within the game's account storage. Keep the game build, Seedline and RitsuLib setup fixed throughout each run: continuing with Seedline disabled can discard its state.

Keep copies of the checkpoint and its `.backup` outside the game's save directory. Steam startup synchronization can replace or remove local saves. See [cloud verification limits](docs/implementation.md#verification-limits).

Start a new run after incompatible updates.
