using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

[HarmonyPatch(typeof(RewardsSet), "GenerateRewardsFor")]
internal static class CombatGold
{
    private static readonly ConditionalWeakTable<GoldReward, RandomAddress> sources = new();
    private static readonly AccessTools.FieldRef<GoldReward, int> amount =
        AccessTools.FieldRefAccess<GoldReward, int>("<Amount>k__BackingField");

    private static void Postfix(AbstractRoom room, List<Reward> __result)
    {
        if (room is not CombatRoom || room.RoomType is not (RoomType.Monster or RoomType.Elite))
            return;
        var address = RandomAddress.For("gold reward", room.RoomType == RoomType.Monster ? "normal" : "elite");
        foreach (var reward in __result.OfType<GoldReward>())
            sources.Add(reward, address);
    }

    [HarmonyPatch(typeof(GoldReward), nameof(GoldReward.Populate))]
    private static class PopulatePatch
    {
        private static bool Prefix(GoldReward __instance, int ____min, int ____max)
        {
            if (____min == ____max || !sources.TryGetValue(__instance, out var address))
                return true;
            // A generated offer consumes once, whether collected or skipped.
            if (!__instance.IsPopulated)
                amount(__instance) = RunCheckpoints.Require((RunState)__instance.Player.RunState).Random
                    .NextIntInclusive(address, ____min, ____max);
            return false;
        }
    }
}
