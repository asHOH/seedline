# Same-Seed Consistency

Windows mod for Slay the Spire 2 **v0.113.0, build 16ac0fe3**. Steps 1–2 provide combined checkpoints and deterministic random state. Gameplay alignment begins with combat gold in step 3.

Rules: [design](DESIGN.md). Delivery: [implementation](IMPLEMENTATION.md). Contracts and limits: [checkpoints](docs/stage-1.md), [random state](docs/stage-2.md).

## Build and validate

Install the .NET SDK pinned in [global.json](global.json). Use PowerShell 7:

```powershell
./scripts/verify.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Use `-Dotnet 'path/to/dotnet.exe'` for a portable SDK. The command restores dependencies, checks formatting and analyzers, runs xUnit tests, then checks integration in an isolated headless game copy. Allow disk space for that copy. Test evidence stays under `.cache/stage1-checks/`; Steam is disabled for testing.

The package at `artifacts/SameSeed/` contains only `SameSeed.dll` and `SameSeed.json`. The game supplies Harmony and its other assemblies.

## Deploy

Close the game, then explicitly deploy the validated package:

```powershell
./scripts/deploy.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Enable mods through the game's loader. Start a new standard or Dread single-player run with SameSeed enabled, and keep it enabled through completion. Disable Catfood. Unsupported builds and run modes are rejected.

Checkpoints use `modded/profileN/saves/same_seed_run.save` within the game's account storage. Vanilla checkpoints remain separate. Disabling SameSeed hides its runs; re-enabling it restores access. Keep both the checkpoint and its `.backup` when making a manual backup.
