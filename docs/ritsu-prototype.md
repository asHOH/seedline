# RitsuLib compatibility prototype

Test-only integration with the official RitsuLib v0.6.13 package for StS2 v0.113.0. Production save behavior remains unchanged.

Build through the [validation command](../README.md#build-and-validate), then run:

```powershell
./scripts/check-game.ps1 -GameDir '<game directory>' -RitsuLibDir '<extracted RitsuLib package>'
```

Use the release's [STS2.RitsuLib.0.6.13.github.zip](https://github.com/BAKAOLC/STS2-RitsuLib/releases/download/v0.6.13/STS2.RitsuLib.0.6.13.github.zip). The launcher uses a separate game copy and save directory with Steam disabled.

## Findings

- Different primary/backup payloads restore matching SameSeed positions and live RitsuLib values, including missing, empty and broken primaries. Real reload-count rewrites retain the selected payload.
- RitsuLib can silently reject a schema, catch deserialization errors or skip player records. Preserved JSON can still match after a failed import. The prototype checks actual runtime slot values before run setup; failures leave both checkpoints and the reload counter unchanged.
- Patching RitsuLib's field-access helper failed during Harmony patch generation. Patching its callback did not intercept the existing game wrapper reliably. The final approach reattaches the selected payload after RitsuLib's load postfix.

## Evidence and limits

2026-11-15: 39 prototype checks and 72 standalone integration checks passed. Prototype evidence: `.cache/ritsu-prototype-checks/8a487279-681d-4672-8e9b-5cd9a9c4fb96/result.json` and `game.log`.

The guard covers run/player `RunSavedData` slots using their current schema and exact serialized values. Model-attached data, migrations, capture/injection failure handling, startup-order variations and live Steam Cloud remain unverified. This establishes feasibility for the tested restoration boundary; production compatibility remains pending.
