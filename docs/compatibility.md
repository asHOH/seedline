# Compatibility and saves

For players. See [setup and installation](../README.md#supported-setup) or [developer integration details](implementation.md).

## New runs

Seedline uses fully unlocked content in new runs while preserving profile unlocks and Ascension limits. If content is missing, the startup prompt summarizes the additions for you.

## Game builds

Requires StS2 v0.114.0 or newer. Build `v0.114.0/a7b9a9c0` is validated. Other builds warn and attempt initialization; incompatible APIs still prevent loading.

Missing or invalid release information blocks startup.

A save made on v0.114.0 can load on v0.114.1, and vice versa, if its save-format versions match those expected by the running game and mods, and its stored data passes validation. Loading across v0.114.x and v0.115.x, or between different prerelease labels such as `-beta.1` and `-beta.2`, is blocked.

## RitsuLib

RitsuLib is optional. Seedline 0.1.5 is validated with RitsuLib 0.6.16 on StS2 v0.114.0 in both mod load orders. See the [supported save-data scope](implementation.md#ritsulib).

RitsuLib 0.6.15 fails to initialize on StS2 v0.114.0 because its patches target removed game methods.

## Modded characters

Rewards, shops, chests, base-game rooms/events and base-game enemies remain aligned. Many card and player-side combat effects, including shuffles and random targeting, keep their original behavior. See the [exact support boundary](implementation.md#modded-characters).

## Tested mods

The following were tested on StS2 v0.113.1, not revalidated for v0.114.0.

- BaseLib
- JmcModLib
- Loadout 2 — requires BaseLib. Tested only for card grants, adding Innate, and event jumps.
- Auto-Anthonyology (东尼算法)
- The Minions (仆从们)
- Quick SL (快速SL) — requires JmcModLib
- Quick Restart 2 (快速重启 2) — requires BaseLib
- Minty Spire 2 — requires BaseLib
- 劣人TV之尖塔MOD
- Intent Graph (意图状态机)
- DanmakuSpire (弹幕尖塔)
- RegentFX (万象辉星)

## Incompatible mods

- CardAscension — replaces reward cards and crashes Seedline.
- Combat Solver (战斗求解器) — rejects Seedline's card-play patches.

## Saves

Saves use `modded/profileN/saves/current_run.save` within the game's account storage. Keep Seedline and RitsuLib setup fixed throughout each run. A Seedline run may load with Seedline disabled but show a black screen on Save and Quit. Continuing can also discard Seedline's saved state.

Seedline saves use the game's existing Steam Cloud support. Cloud save handling is covered by simulated tests. Live synchronization remains unverified.

Keep copies of the checkpoint and its `.backup` outside the game's save directory. Steam startup synchronization can replace or remove local saves.

Start a new run after incompatible updates.
