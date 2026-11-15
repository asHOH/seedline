# Same-Seed Consistency

Windows mod for Slay the Spire 2 **v0.113.0, build 16ac0fe3**. Steps 1–2 provide standard-path combined checkpoints, optional RitsuLib v0.6.13 compatibility and deterministic random state. Gameplay alignment begins with combat gold in step 3.

Rules: [design](DESIGN.md). Delivery: [implementation](IMPLEMENTATION.md). Contracts and limits: [checkpoints](docs/stage-1.md), [random state](docs/stage-2.md).

## Build and validate

Install the .NET SDK pinned in [global.json](global.json). Use PowerShell 7:

```powershell
./scripts/verify.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Use `-Dotnet 'path/to/dotnet.exe'` for a portable SDK. The command restores dependencies, checks formatting and analyzers, runs xUnit tests, then checks integration in an isolated headless game copy. Allow disk space for that copy. Test evidence stays under `.cache/stage1-checks/`; Steam is disabled for testing.

Add `-RitsuLibDir '<extracted RitsuLib v0.6.13 package>'` to also run the combined checks in both mod initialization orders. [Compatibility scope and evidence](docs/ritsulib.md).

The package at `artifacts/SameSeed/` contains only `SameSeed.dll` and `SameSeed.json`. The game supplies Harmony and its other assemblies.

## Deploy

Close the game, then explicitly deploy the validated package:

```powershell
./scripts/deploy.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Enable mods through the game's loader. Start a new standard or Dread single-player run with SameSeed enabled, and keep it enabled through completion. Disable Catfood. Unsupported builds and run modes are rejected.

Saves use `modded/profileN/saves/current_run.save` within the game's account storage. Keep SameSeed and the same RitsuLib setup enabled throughout each run: continuing with SameSeed disabled can discard its state. Back up both the checkpoint and its `.backup`.

Start fresh after upgrading from the dedicated `same_seed_run.save` build. Development saves remain untouched.
