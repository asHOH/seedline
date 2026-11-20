# Compatibility and saves

For players choosing mods. See the [supported game setup and deployment steps](../README.md#supported-setup) to get started, or [implementation](implementation.md) for developer integration details.

## New runs

Seedline uses fully unlocked content in new runs while preserving profile unlocks and Ascension limits. If content is missing, the startup prompt summarizes the additions for you.

## RitsuLib

RitsuLib is optional. Keep its complete package, including `compat/0.113.0`, which supplies the runtime for StS2 v0.113.1. Untested versions produce a warning rather than a version-based startup failure. See the [supported save-data scope](implementation.md#ritsulib).

## Modded characters

- Seedline retains alignment for rewards (including card offers from their pools), shops, chests, vanilla rooms/events, and vanilla enemy setup, HP and AI.
- Card shuffling, insertion, selection, copy routing, changes to existing permanent cards, random costs, combat card generation, orb generation and player-side random targeting follow game/mod behavior.

## Tested mods

- BaseLib
- RitsuLib
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

Saves use `modded/profileN/saves/current_run.save` within the game's account storage. Keep the game build, Seedline and RitsuLib setup fixed throughout each run. Continuing with Seedline disabled can discard its state.

Keep copies of the checkpoint and its `.backup` outside the game's save directory. Steam startup synchronization can replace or remove local saves.

Start a new run after incompatible updates.

### Verification limits

Live Steam synchronization remains unverified.
