using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class CharacterCompatibility
{
    internal static bool UseVanillaCards(Player? player) => player != null &&
        player.Character.GetType().Assembly != typeof(CharacterModel).Assembly;

    internal static bool IsModdedRun => UseVanillaCards(RunManager.Instance.DebugOnlyGetState()?.Players.FirstOrDefault());
}
