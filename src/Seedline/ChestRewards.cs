using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class ChestRewards
{
    private sealed record Chest(long Ordinal, int Gold);
    private static readonly ConditionalWeakTable<RunState, Chest> current = new();
    private static Chest Current(RunState run) => current.TryGetValue(run, out var chest) ? chest
        : throw new InvalidOperationException("Chest rewards have not been generated.");

    [HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.BeginRelicPicking))]
    private static class Generate
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var rarity = RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(RelicFactory) && m.Name == "RollRarity",
                AccessTools.Method(typeof(Generate), nameof(Roll)), 2);
            return RewardPatches.Replace(rarity, m => m.DeclaringType == typeof(RelicGrabBag) && m.Name == "PullFromFront",
                AccessTools.Method(typeof(Generate), nameof(Draw)), 2);
        }

        private static RelicRarity Roll(Rng _)
        {
            var run = RunManager.Instance.DebugOnlyGetState()!;
            var state = RunCheckpoints.Require(run);
            var ordinal = state.Random.Advance(RandomAddress.For("chest"));
            current.Remove(run);
            current.Add(run, new(ordinal, AddressRandom.IntInclusive(state.Seed, RandomAddress.For("chest gold").At(ordinal), 42, 52)));
            return RelicRewards.Roll(run.Players[0], "generic");
        }

        private static RelicModel Draw(RelicGrabBag _, RelicRarity rarity, IRunState run) =>
            RelicRewards.Draw(run.Players[0], "generic", rarity, chest: true, chestOrdinal: Current((RunState)run).Ordinal);
    }

    [HarmonyPatch]
    private static class Gold
    {
        private static System.Reflection.MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(OneOffSynchronizer), "DoTreasureRoomRewards");
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Rng) && m.Name == "NextInt",
                AccessTools.Method(typeof(Gold), nameof(Amount)));
        private static int Amount(Rng _, int minimum, int maximum) => Current(RunManager.Instance.DebugOnlyGetState()!).Gold;
    }
}
