# Same-Seed Consistency

Windows mod for Slay the Spire 2 **v0.113.0/16ac0fe3** and **v0.113.1/d09d48e5**. Steps 1–4 provide standard-path combined checkpoints, optional RitsuLib v0.6.14 compatibility, deterministic random state and aligned gold, relics, chests, potions, shops and card rewards. The first [stage 5 increment](docs/stage-5.md) adds persistent selection and deck routing.

Rules: [design](DESIGN.md). Delivery: [implementation](IMPLEMENTATION.md). Contracts and limits: [checkpoints](docs/stage-1.md), [random state](docs/stage-2.md), [combat gold](docs/stage-3.md), [reward supplies and card occurrences](docs/stage-4.md).

## Build and validate

Install the .NET SDK pinned in [global.json](global.json). Use PowerShell 7:

```powershell
./scripts/verify.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Use `-Dotnet 'path/to/dotnet.exe'` for a portable SDK. The command restores dependencies, checks formatting and analyzers, runs xUnit tests, then checks integration in an isolated headless game copy. Allow disk space for that copy. Test evidence stays under `.cache/stage1-checks/`; Steam is disabled for testing.

Add `-RitsuLibDir '<extracted RitsuLib v0.6.14 package>'` to also run the combined checks in both mod initialization orders. [Compatibility scope and evidence](docs/ritsulib.md).

The package at `artifacts/SameSeed/` contains only `SameSeed.dll` and `SameSeed.json`. The game supplies Harmony and its other assemblies.

## Event workbook

The [workbook](references/StS2_Events.xlsx) records v0.113.0. Regeneration requires that game build, ILSpyCmd 9.1.0.7988 with its .NET 8 runtime, and Codex's bundled Node runtime (which supplies `@oai/artifact-tool`). From the repository root:

```powershell
ilspycmd -p -o .cache/sts2-v0.113.0/decompiled '<v0.113.0 game>/data_sts2_windows_x86_64/sts2.dll'
& '<Codex bundled Node executable>' scripts/build-events.mjs
```

The [generator](scripts/build-events.mjs) reads the committed workbook as its template and checks event coverage against the extracted source. Review its export in `outputs/event-workbook/` before replacing `references/StS2_Events.xlsx`. Game-source inputs and generated previews remain local.

## Deploy

Close the game, then explicitly deploy the validated package:

```powershell
./scripts/deploy.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Enable mods through the game's loader. Start a new standard or Dread single-player run with SameSeed enabled, and keep it enabled through completion. Disable Catfood. Unsupported builds and run modes are rejected.

SameSeed grants full content within new runs. Profile unlocks and Ascension limits stay unchanged. When starting with missing content, review the listed additions and choose Proceed (the default) or Cancel.

Saves use `modded/profileN/saves/current_run.save` within the game's account storage. Keep the game build, SameSeed and RitsuLib setup fixed throughout each run: continuing with SameSeed disabled can discard its state. Back up both the checkpoint and its `.backup`.

Start fresh after upgrading to v0.5.0. Schema 4 requires persistent selection history that earlier saves lack; incompatible saves remain untouched.

Perform the [stage 5 gameplay checks](docs/stage-5.md#gameplay-checks) alongside the remaining [stage 4 checks](docs/stage-4.md#gameplay-checks).
