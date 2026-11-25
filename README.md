# Seedline

A Slay the Spire 2 mod that gives same-seed runs similar gameplay experiences — rewards, choices and combat challenges — across different routes and decisions.

Seedline reduces the influence of unrelated actions on later luck. For example, in the base game, reshuffling your deck an extra time in one fight can change your opening hand in the next. Seedline keeps each fight's shuffles separate, so that extra shuffle alone won't change your next opening hand.

To keep same-seed experiences similar, Seedline changes some reward probabilities and how actions affect later random results.

If you save and reload to try different actions (SL), expect more changes from the base game. Some actions that previously let you reroll an outcome no longer do, so you may find this way of playing very different. See the [gameplay changes](design/DESIGN.md#shared-vanilla-deviations) for details.

## Supported setup

Requires **StS2 v0.114.0 (`public-beta`)** on Windows. Supports standard and Dread single-player.

Custom runs may have any combination of **Flight, Murderous, Terminal, Night Terrors, Midas, Big Game Hunter, Character Cards, Hoarder and Draft**.

Multiplayer, Blitz, Daily and runs containing any other modifier play normally with Seedline inactive.

Seedline runs use fully unlocked content without changing profile unlocks or Ascension limits.

## Install

Subscribe on [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3843171761) (recommended).

For manual installation, download `Seedline-<version>.zip` from the [latest release](https://github.com/asHOH/seedline/releases/latest). Close the game and extract the `Seedline` folder into the game's `mods` folder (for example, D:\Programs\Steam\steamapps\common\Slay the Spire 2\mods), creating `mods` if needed.

## Saves

Keep the game build, Seedline and RitsuLib setup fixed during a run. Disabling Seedline can discard its saved state. See [compatibility and save guidance](docs/compatibility.md).

## Report a problem

Report problems in Steam Workshop discussions (preferred) or QQ group **609823795**: describe expected versus actual behavior and steps to reproduce.

In Settings, choose **Seedline diagnostics → Export diagnostics** and send the generated ZIP privately to the maintainer.

## Learn more

- [Gameplay design](design/DESIGN.md) — rules and tradeoffs.
- [Development](docs/development.md) — building, testing and releases.
- [Research](research/README.md) — evidence and methods.

## License

[MIT](LICENSE).
