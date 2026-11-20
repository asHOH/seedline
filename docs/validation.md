# Validation

## Tested mods on StS2 v0.113.1

- BaseLib
- RitsuLib
- JmcModLib
- Quick SL (快速SL); requires JmcModLib
- Quick Restart 2 (快速重启 2); requires BaseLib
- Loadout 2; requires BaseLib
- 劣人TV之尖塔MOD
- Intent Graph (意图状态机)
- DanmakuSpire (弹幕尖塔)

**Note:** Loadout 2 verification only covers card grants, adding Innate, and event jumps.

## Modded characters

For modded characters:

1. Seedline retains alignment for rewards (including card offers from their pools), shops, chests, vanilla rooms/events, and vanilla enemy setup, HP and AI.

2. Seedline leaves Card shuffling, insertion, selection, copy routing, changes to existing permanent cards, random costs, combat card generation, orb generation and player-side random targeting to use the game/mod behavior.

## Verification limits

Live Steam synchronization remain unverified.

## Before publication

- [ ] Run the [verification command](../README.md#build-and-validate) once on the final build, including RitsuLib when used. Pass: `All checks passed`.
