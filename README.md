# Same-Seed Consistency

Stage 1 integrates a standalone Windows mod with Slay the Spire 2 **v0.113.0, build 16ac0fe3**. It establishes run ownership and combined vanilla/mod checkpoints. Gameplay alignment begins in later stages.

Gameplay requirements: [DESIGN.md](DESIGN.md). Delivery order: [IMPLEMENTATION.md](IMPLEMENTATION.md). Save integration and validation: [Stage 1](docs/stage-1.md).

## Build and validate

Install the .NET SDK pinned in [global.json](global.json). Use PowerShell 7:

```powershell
./scripts/verify.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Use `-Dotnet 'path/to/dotnet.exe'` for a portable SDK. The command restores framework reference packs, checks formatting, builds with nullable checking and analyzers, and runs assertions inside an isolated headless copy of the game. It requires local space for that copy. Test saves, logs and results stay under `.cache/stage1-checks/`; Steam is disabled for the test process.

The package at `artifacts/SameSeed/` contains only `SameSeed.dll` and `SameSeed.json`. The game supplies Harmony and its other assemblies.

## Deploy

Close the game, then explicitly deploy the validated package:

```powershell
./scripts/deploy.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Enable mods through the game's loader. Start a new standard or Dread single-player run with SameSeed enabled, and keep it enabled through completion. Disable Catfood. Unsupported builds and run modes are rejected.

Checkpoints use `modded/profileN/saves/same_seed_run.save` within the game's account storage. Vanilla checkpoints remain separate. Disabling SameSeed hides its runs; re-enabling it restores access. Keep both the checkpoint and its `.backup` when making a manual backup.
