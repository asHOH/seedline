# Same-Seed Consistency

Windows mod for Slay the Spire 2 **v0.113.1/d09d48e5**. Keeps rewards and combat challenges comparable across same-seed runs with different routes and decisions, with combined checkpoints and optional RitsuLib support.

Authoritative designs: [gameplay rules](design/DESIGN.md) and [card processing order](design/CARD_ORDER.md). Supporting guides: [implementation](docs/implementation.md) and [validation](docs/validation.md). Research: [overview](research/README.md).

## Build and validate

Install the .NET SDK pinned in [global.json](global.json). Use PowerShell 7:

```powershell
./scripts/verify.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Use `-Dotnet 'path/to/dotnet.exe'` for a portable SDK. The command restores dependencies, checks formatting and analyzers, runs xUnit tests, then checks integration in an isolated headless game copy. Allow disk space for that copy. Test evidence stays under `.cache/stage1-checks/`; Steam is disabled for testing.

For combined checks, see [RitsuLib compatibility](#ritsulib-compatibility).

The package at `artifacts/SameSeed/` contains only `SameSeed.dll` and `SameSeed.json`. The game supplies Harmony and its other assemblies.

## RitsuLib compatibility

RitsuLib is optional. The bridge supports official **v0.6.14** with current-schema run/player `RunSavedData` slots. Model-attached data, slot migrations, custom pile behavior and compatibility with other mods remain outside this claim.

Use [STS2.RitsuLib.0.6.14.github.zip](https://github.com/BAKAOLC/STS2-RitsuLib/releases/download/v0.6.14/STS2.RitsuLib.0.6.14.github.zip). Preserve the complete package: its `compat/0.113.0` directory supplies the runtime used on StS2 v0.113.1.

Add `-RitsuLibDir '<extracted RitsuLib package>'` to the validation command to exercise the production bridge with real RitsuLib in both initialization orders. Results are stored under `.cache/ritsu-checks/<run>/`. See the [checkpoint contract](docs/implementation.md#checkpoint-contract).

## Event workbook

The [workbook](references/StS2_Events.xlsx) records v0.113.1. Regeneration requires that game build, ILSpyCmd 9.1.0.7988 with its .NET 8 runtime, and Codex's bundled Node runtime (which supplies `@oai/artifact-tool`). From the repository root:

```powershell
ilspycmd -p -o sts2-decompiled '<v0.113.1 game>/data_sts2_windows_x86_64/sts2.dll'
& '<Codex bundled Node executable>' scripts/build-events.mjs
```

The [generator](scripts/build-events.mjs) uses the workbook as its template and checks event coverage against the extracted source. Review its export in `research/event-workbook/` before replacing `references/StS2_Events.xlsx`. Game sources and previews stay local.

## Deploy

Close the game, then explicitly deploy the validated package:

```powershell
./scripts/deploy.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Enable mods through the game's loader. Start a new standard or Dread single-player run with SameSeed enabled, and keep it enabled through completion. Disable Catfood. Unsupported builds and run modes are rejected.

SameSeed grants full content within new runs. Profile unlocks and Ascension limits stay unchanged. When starting with missing content, review the listed additions and choose Proceed (the default) or Cancel.

## Saves

Saves use `modded/profileN/saves/current_run.save` within the game's account storage. Keep the game build, SameSeed and RitsuLib setup fixed throughout each run: continuing with SameSeed disabled can discard its state.

Keep copies of the checkpoint and its `.backup` outside the game's save directory. Steam startup synchronization can replace or remove local saves. See [cloud verification limits](docs/validation.md#current-evidence).

Start a new run after incompatible updates.
