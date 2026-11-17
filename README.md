# Same-Seed Consistency

A Slay the Spire 2 mod that keeps same-seed runs comparable across different routes and decisions.

In the base game, unrelated actions can shift later random results. SameSeed aligns corresponding opportunities across rewards, shops, events and combat to reduce that interference. For example, each run's first shop can offer the same initial cards on different floors when eligible cards match.

Choices still matter: decks, owned items, eligibility and the number of rewards or effects used can change outcomes. The mod makes deliberate changes to some probabilities and reroll options to improve outcome consistency.

Authoritative designs: [gameplay rules](design/DESIGN.md) and [card processing order](design/CARD_ORDER.md). Guides: [implementation](docs/implementation.md), [validation results](docs/validation.md), and [research](research/README.md), including [event workbook maintenance](research/README.md#event-workbook).

## Supported setup

Requires **StS2 v0.113.1/d09d48e5** on Windows. Supports standard and Dread single-player runs without custom modifiers. Compare runs with matching seeds, game/mod versions and rules. Disable Catfood.

## Build and validate

Install the .NET SDK pinned in [global.json](global.json). In PowerShell 7, run from the repository root, replacing the example game path with your installation:

```powershell
./scripts/verify.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

The command builds and checks the mod, using an isolated game copy for integration checks; allow disk space for that copy. Use `-Dotnet 'path/to/dotnet.exe'` for a portable SDK. With RitsuLib installed, include the [RitsuLib checks](#ritsulib-compatibility).

The validated package is written to `artifacts/SameSeed/`.

## Deploy

Close the game, then deploy the validated package:

```powershell
./scripts/deploy.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Enable mods through the game's loader and start a new run with SameSeed enabled.

SameSeed uses fully unlocked content in new runs while preserving profile unlocks and Ascension limits. If content is missing, the startup prompt summarizes the additions for you to accept or cancel.

The notice follows the game's language setting and supports all 17 game languages.

## RitsuLib compatibility

RitsuLib is optional. Use the official [v0.6.14 package](https://github.com/BAKAOLC/STS2-RitsuLib/releases/download/v0.6.14/STS2.RitsuLib.0.6.14.github.zip), keeping its `compat/0.113.0` directory, which supplies the runtime for StS2 v0.113.1. See the [supported save-data scope](docs/implementation.md#checkpoint-contract).

Add `-RitsuLibDir '<extracted RitsuLib package>'` to the build-and-validate command when using RitsuLib.

## Saves

Saves use `modded/profileN/saves/current_run.save` within the game's account storage. Keep the game build, SameSeed and RitsuLib setup fixed throughout each run: continuing with SameSeed disabled can discard its state.

Keep copies of the checkpoint and its `.backup` outside the game's save directory. Steam startup synchronization can replace or remove local saves. See [cloud verification limits](docs/validation.md#verification-limits).

Start a new run after incompatible updates.
