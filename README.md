# Seedline

A Slay the Spire 2 mod that gives same-seed runs similar gameplay experiences — rewards, choices and combat challenges — across different routes and decisions.

Seedline reduces the influence of unrelated actions on later luck. For example, in the base game, reshuffling your deck an extra time in one fight can change your opening hand in the next. Seedline keeps each fight's shuffles separate, so that extra shuffle alone won't change your next opening hand.

To keep same-seed experiences similar, Seedline changes some reward probabilities and how actions affect later random results.

If you save and reload to try different actions (SL), expect more changes from the base game. Some actions that previously let you reroll an outcome no longer do, so you may find this way of playing very different. See the [gameplay changes](design/DESIGN.md#shared-vanilla-deviations) for details.

## Supported setup

Requires **StS2 v0.113.1 (`public-beta`)** on Windows. Supports standard and Dread single-player runs without custom modifiers. [RitsuLib](docs/compatibility.md#ritsulib) is optional.

## Performance

With 30-card decks, [benchmarks](research/README.md#performance) measured under **1.5 ms median added time per tested operation**.

## Install

Download `Seedline-<version>.zip` from the [latest release](https://github.com/asHOH/seedline/releases/latest). Close the game and extract the `Seedline` folder into the game's `mods` folder, creating `mods` if needed.

Enable **Seedline / 不歪种神器** in the game's mod loader and start a new run. New runs use fully unlocked content without changing profile unlocks or Ascension limits.

## Saves

Keep the game build, Seedline and RitsuLib setup fixed during a run. Disabling Seedline can discard its saved state. See [compatibility and save guidance](docs/compatibility.md).

## Learn more

- [Gameplay design](design/DESIGN.md) — rules and tradeoffs.
- [Development](docs/development.md) — building, testing and releases.
- [Research](research/README.md) — evidence and methods.

## License

[MIT](LICENSE).
