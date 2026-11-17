# RitsuLib compatibility

The bridge supports official RitsuLib **v0.6.14** on the [supported game build](../README.md), with current-schema run/player `RunSavedData` slots. Model-attached data, slot migrations and custom pile behavior remain outside this compatibility claim. SameSeed owns its RNG and state independently.

RitsuLib's `compat/0.113.0` directory supplies the runtime used on StS2 v0.113.1; preserve the complete package. SameSeed requires schema 6 checkpoints.

Use [STS2.RitsuLib.0.6.14.github.zip](https://github.com/BAKAOLC/STS2-RitsuLib/releases/download/v0.6.14/STS2.RitsuLib.0.6.14.github.zip), then run:

```powershell
./scripts/verify.ps1 -GameDir '<game directory>' -RitsuLibDir '<extracted RitsuLib package>'
```

The isolated checks use the production bridge and real RitsuLib in both initialization orders. They cover empty payloads, different primary/backup values, missing/empty/broken primaries, snapshot isolation, reload-count rewrites, capture and disk failures, and silent or caught restoration failures. Store-hook checks verify that writes bypass RitsuLib's reinjection context.

The earlier bounded prototype established that comparing retained JSON could hide failed imports. The production guard checks live slot values before run setup. [Checkpoint contract and boundaries](stage-1.md).

Each run writes its results under `.cache/ritsu-checks/<run>/`; Steam is disabled in these tests. Live Steam synchronization and normal-UI disable/re-enable remain unverified.
