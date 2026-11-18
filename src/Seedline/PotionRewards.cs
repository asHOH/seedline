using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Events.Custom.CrystalSphereEvent.CrystalSphereItems;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class PotionRewards
{
    private sealed record Pending(CheckpointState Preview, string Source);
    private static readonly ConditionalWeakTable<PotionModel, Pending> pending = new();

    internal static PotionModel Draw(Player player, string source, IEnumerable<PotionModel> eligible,
        PotionRarity? fixedRarity = null, bool uniform = false, CheckpointState? preview = null)
    {
        var state = preview ?? RunCheckpoints.Require((RunState)player.RunState);
        var options = eligible.ToArray();
        if (options.Length == 0)
            throw new InvalidOperationException("Cannot generate a potion from an empty pool.");
        var rarity = fixedRarity;
        if (rarity == null)
        {
            var roll = state.Random.NextUnit(RandomAddress.For("potion rarity", source, uniform ? "uniform" : "standard"));
            if (uniform)
            {
                var index = (int)(roll * options.Length);
                rarity = options.OrderBy(p => p.Rarity).ElementAt(index).Rarity;
            }
            else
                rarity = roll < 0.1 ? PotionRarity.Rare : roll < 0.35 ? PotionRarity.Uncommon : PotionRarity.Common;
        }
        var pool = PotionFactory.GetPotionOptions(player).Where(p => p.Rarity == rarity).ToArray();
        var entry = state.Rewards.Draw(state.Seed, RandomAddress.For("potion identity", source, rarity.ToString()!),
            pool.Select(p => p.Id.Entry), options.Where(p => p.Rarity == rarity).Select(p => p.Id.Entry));
        var potion = pool.Single(p => p.Id.Entry == entry.Identity);
        RewardEntries.Attach(potion, entry);
        return potion;
    }

    private static async Task<PotionProcureResult> Grant(Player player, string source, bool combat,
        HashSet<string>? excluded = null, int slot = -1)
    {
        var state = RunCheckpoints.Require((RunState)player.RunState);
        var rewards = new RewardState();
        foreach (var rarity in new[] { PotionRarity.Common, PotionRarity.Uncommon, PotionRarity.Rare })
        {
            var key = RandomAddress.For("potion identity", source, rarity.ToString()).Key;
            if (state.Rewards.Supplies.TryGetValue(key, out var supply))
                rewards.Supplies.Add(key, supply.Copy());
        }
        var preview = state with { Random = state.Random.Copy(), Rewards = rewards };
        var eligible = PotionFactory.GetPotionOptions(player).Where(p => (!combat || p.CanBeGeneratedInCombat) &&
            (excluded == null || !excluded.Contains(p.Id.Entry)));
        var potion = Draw(player, source, eligible, preview: preview).ToMutable();
        pending.Add(potion, new(preview, source));
        try
        {
            var result = await PotionCmd.TryToProcure(potion, player, slot);
            if (result.success)
                excluded?.Add(potion.Id.Entry);
            return result;
        }
        finally { pending.Remove(potion); }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.AddPotionInternal))]
    private static class CommitGrant
    {
        private static void Postfix(Player __instance, PotionModel potion, PotionProcureResult __result)
        {
            if (!__result.success || !pending.TryGetValue(potion, out var grant))
                return;
            var live = RunCheckpoints.Require((RunState)__instance.RunState);
            live.Random.Commit(RandomAddress.For("potion rarity", grant.Source, "standard"), grant.Preview.Random);
            var key = RandomAddress.For("potion identity", grant.Source, potion.Rarity.ToString()).Key;
            live.Rewards.Supplies[key] = grant.Preview.Rewards.Supplies[key];
        }
    }

    [HarmonyPatch(typeof(PotionFactory), nameof(PotionFactory.CreateRandomPotionsOutOfCombat))]
    private static class Factory
    {
        private static bool Prefix(Player player, int count, Rng rng, IEnumerable<PotionModel>? blacklist,
            ref IEnumerable<PotionModel> __result)
        {
            var source = ReferenceEquals(rng, player.PlayerRng.Shops) ? (ShopRewards.IsInitial ? "shop" : "Courier") : "generic";
            var options = PotionFactory.GetPotionOptions(player).Except(blacklist ?? []).ToList();
            var result = new List<PotionModel>();
            for (var i = 0; i < count; i++)
            {
                var potion = Draw(player, source, options);
                result.Add(potion);
                options.Remove(potion);
            }
            __result = result;
            return false;
        }
    }

    [HarmonyPatch(typeof(PotionRewardOdds), nameof(PotionRewardOdds.Roll))]
    private static class Drops
    {
        private static bool Prefix(PotionRewardOdds __instance, Player player, RoomType roomType, ref bool __result)
        {
            if (Hook.ShouldForcePotionReward(player.RunState, player, roomType))
                __result = true;
            else
            {
                var random = RunCheckpoints.Require((RunState)player.RunState).Random;
                __result = random.NextUnit(RandomAddress.For("potion drop")) < __instance.CurrentValue + (roomType == RoomType.Elite ? 0.125f : 0);
                __instance.OverrideCurrentValue(__instance.CurrentValue + (__result ? -0.1f : 0.1f));
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Alchemize), "OnPlay")]
    private static class AlchemizeGrant
    {
        private static bool Prefix(Alchemize __instance, ref Task __result)
        {
            __result = Play(__instance.Owner);
            return false;
        }
        private static async Task Play(Player player)
        {
            await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);
            await Grant(player, "effects", true);
        }
    }

    private static async Task Fill(Player player)
    {
        while (player.HasOpenPotionSlots)
            if (!(await Grant(player, "effects", false)).success)
                break;
    }

    [HarmonyPatch(typeof(EntropicBrew), "OnUse")]
    private static class BrewGrant
    {
        private static bool Prefix(Creature? target, ref Task __result)
        {
            ArgumentNullException.ThrowIfNull(target);
            __result = Fill(target!.Player!);
            return false;
        }
    }

    [HarmonyPatch(typeof(DelicateFrond), nameof(DelicateFrond.BeforeCombatStart))]
    private static class FrondGrant
    {
        private static bool Prefix(DelicateFrond __instance, ref Task __result)
        {
            __instance.Flash();
            __result = Fill(__instance.Owner);
            return false;
        }
    }

    [HarmonyPatch]
    private static class PickupGrant
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(AlchemicalCoffer), "AfterObtained"), AccessTools.Method(typeof(PhialHolster), "AfterObtained")];
        private static bool Prefix(RelicModel __instance, ref Task __result)
        {
            __result = Pickup(__instance);
            return false;
        }
        private static async Task Pickup(RelicModel relic)
        {
            var player = relic.Owner;
            var originalSlots = player.MaxPotionCount;
            await PlayerCmd.GainMaxPotionCount(relic.DynamicVars["PotionSlots"].IntValue, player);
            var count = relic.DynamicVars[relic is AlchemicalCoffer ? "PotionSlots" : "Potions"].IntValue;
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
                await Grant(player, "generic", false, excluded, relic is AlchemicalCoffer ? originalSlots + i : -1);
        }
    }

    [HarmonyPatch]
    private static class EventPotions
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [
            RewardPatches.AsyncMethod(typeof(Wellspring), "Bottle"),
            RewardPatches.AsyncMethod(typeof(TheLegendsWereTrue), "SlowlyFindAnExit"),
            RewardPatches.AsyncMethod(typeof(BattlewornDummy), "Resume"),
            RewardPatches.AsyncMethod(typeof(EndlessConveyor), "SuspiciousCondiment"),
            RewardPatches.AsyncMethod(typeof(PotionCourier), "Ransack"),
            AccessTools.Method(typeof(CrystalSpherePotion), "ToReward")
        ];

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Rng) && m.Name == "NextItem" &&
                m.IsGenericMethod && m.GetGenericArguments()[0] == typeof(PotionModel), AccessTools.Method(typeof(EventPotions), nameof(Choose)));

        private static PotionModel? Choose(Rng _, IEnumerable<PotionModel> candidates)
        {
            var options = candidates.ToArray();
            if (options.Length == 0)
                return null;
            var rarities = options.Select(p => p.Rarity).Distinct().ToArray();
            var player = RunManager.Instance.DebugOnlyGetState()!.Players[0];
            return Draw(player, "generic", options, rarities.Length == 1 ? rarities[0] : null, uniform: true);
        }
    }
}
