using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class CharacterCompatibility
{
    internal static bool UseVanillaCards(Player? player) => player != null &&
        (RunScope.Bypasses(player.RunState) || player.Character.GetType().Assembly != typeof(CharacterModel).Assembly);

    internal static bool IsModdedRun => RunManager.Instance.DebugOnlyGetState()?.Players.FirstOrDefault() is { } player &&
        player.Character.GetType().Assembly != typeof(CharacterModel).Assembly;
}
