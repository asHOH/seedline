using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class PotionEffects
{
    private sealed record Opportunity(long Ordinal);
    private sealed record Generation(PotionModel Potion, RewardState Supply);
    private static readonly AsyncLocal<Generation?> generating = new();
    private static readonly Type[] types = [typeof(AttackPotion), typeof(SkillPotion), typeof(PowerPotion),
        typeof(ColorlessPotion), typeof(CosmicConcoction), typeof(OrobicAcid), typeof(BottledSong), typeof(SneckoOil)];
    private static readonly ConditionalWeakTable<PotionModel, Opportunity> committed = new();
    private static readonly ConditionalWeakTable<PotionReward, object> cauldronRewards = new();
    private static readonly ConditionalWeakTable<PotionReward, object> skippedRewards = new();

    internal static RandomAddress Counter(string type) => RandomAddress.For("potion effect", type);
    internal static long EffectOrdinal(PotionModel potion) => committed.TryGetValue(potion, out var opportunity)
        ? opportunity.Ordinal : throw new InvalidOperationException("Potion effect has not committed.");

    internal static List<CardModel> Generate(Player player, PotionModel potion, IEnumerable<CardModel> cards, int count, RewardState supply)
    {
        var options = CardFactory.FilterForCombat(cards).Where(c => c.IsAllowedInRun(player.RunState)).ToArray();
        var pool = potion switch
        {
            OrobicAcid => CombatGeneration.Pool(player, player.Character.CardPool),
            BottledSong => player.UnlockState.CharacterCardPools.SelectMany(p => CombatGeneration.Pool(player, p))
                .Where(c => c.Tags.Any(t => t is CardTag.Wah or CardTag.Cho or CardTag.Ki)),
            _ => options
        };
        var identities = pool.ToArray();
        var address = RandomAddress.For("potion card generation", potion.Id.Entry).At(EffectOrdinal(potion));
        var seed = RunCheckpoints.Require((RunState)player.RunState).Seed;
        var result = new List<CardModel>();
        for (var i = 0; i < Math.Min(count, options.Length); i++)
        {
            var entry = supply.Draw(seed, address, identities.Select(c => c.Id.Entry),
                options.Where(c => result.All(r => r.Id != c.Id)).Select(c => c.Id.Entry));
            var card = player.Creature.CombatState!.CreateCard(identities.First(c => c.Id.Entry == entry.Identity), player);
            CardOccurrences.Create(card, entry.Address);
            result.Add(card);
        }
        return result;
    }

    [HarmonyPatch]
    internal static class Sources
    {
        internal static IEnumerable<MethodBase> TargetMethods() => types.Where(t => t != typeof(SneckoOil))
            .Select(t => AccessTools.Method(t, "OnUse"));
        private static void Prefix(PotionModel __instance, out Generation? __state)
        {
            __state = generating.Value;
            generating.Value = new(__instance, new());
        }
        private static void Finalizer(Generation? __state) => generating.Value = __state;
    }

    [HarmonyPatch]
    private static class Factory
    {
        private static IEnumerable<MethodBase> TargetMethods() => types.Where(t => t != typeof(SneckoOil))
            .Select(t => RewardPatches.AsyncMethod(t, "OnUse"));
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(CardFactory) && m.Name == nameof(CardFactory.GetDistinctForCombat),
                AccessTools.Method(typeof(Factory), nameof(Create)), __originalMethod.DeclaringType!.DeclaringType == typeof(OrobicAcid) ? 3 : 1);
        private static IEnumerable<CardModel> Create(Player player, IEnumerable<CardModel> cards, int count, Rng rng)
        {
            if (CharacterCompatibility.UseVanillaCards(player))
                return CardFactory.GetDistinctForCombat(player, cards, count, rng);
            var generation = generating.Value ?? throw new InvalidOperationException("Missing potion generation source.");
            return Generate(player, generation.Potion, cards, count, generation.Supply);
        }
    }

    private static long Consume(Player player, string type)
    {
        var random = RunCheckpoints.Require((RunState)player.RunState).Random;
        var ordinal = random.Position(Counter(type));
        var granted = RandomAddress.For("Cauldron exemption", type);
        var consumed = RandomAddress.For("Cauldron exemption consumed", type);
        if (random.Position(consumed) < random.Position(granted))
            random.Advance(consumed);
        else
            random.Advance(Counter(type));
        return ordinal;
    }

    [HarmonyPatch(typeof(Player), "RemovePotionInternal")]
    private static class Removed
    {
        private static void Postfix(Player __instance, PotionModel potion)
        {
            if (!CharacterCompatibility.UseVanillaCards(__instance) && types.Contains(potion.GetType()))
                committed.AddOrUpdate(potion, new(Consume(__instance, potion.Id.Entry)));
        }
    }

    [HarmonyPatch(typeof(PotionReward), nameof(PotionReward.OnSkipped))]
    private static class Skipped
    {
        private static void Postfix(PotionReward __instance, bool ____wasTaken)
        {
            if (CharacterCompatibility.UseVanillaCards(__instance.Player) ||
                ____wasTaken || __instance.Potion is not { } potion || !types.Contains(potion.GetType()) ||
                skippedRewards.TryGetValue(__instance, out _))
                return;
            Consume(__instance.Player, potion.Id.Entry);
            skippedRewards.Add(__instance, new());
        }
    }

    [HarmonyPatch(typeof(Cauldron), "GenerateRewards")]
    private static class CauldronOffers
    {
        private static void Postfix(List<Reward> __result)
        {
            foreach (var reward in __result.OfType<PotionReward>())
                cauldronRewards.Add(reward, new());
        }
    }

    [HarmonyPatch(typeof(RewardsSetSynchronizer), nameof(RewardsSetSynchronizer.BeginRewardsSet))]
    private static class Offered
    {
        private static void Prefix(RewardsSet set)
        {
            if (CharacterCompatibility.UseVanillaCards(set.Player)) return;
            foreach (var reward in set.Rewards.OfType<PotionReward>())
                if (cauldronRewards.Remove(reward) && reward.Potion is { } potion && types.Contains(potion.GetType()))
                    RunCheckpoints.Require((RunState)set.Player.RunState).Random.Advance(RandomAddress.For("Cauldron exemption", potion.Id.Entry));
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeRoomEntered))]
    private static class ExpireExemptions
    {
        private static void Prefix(IRunState runState)
        {
            if (CharacterCompatibility.IsModdedRun) return;
            var random = RunCheckpoints.Require((RunState)runState).Random;
            foreach (var type in types)
            {
                var id = ModelDb.GetId(type).Entry;
                var granted = random.Position(RandomAddress.For("Cauldron exemption", id));
                var consumed = RandomAddress.For("Cauldron exemption consumed", id);
                while (random.Position(consumed) < granted)
                    random.Advance(consumed);
            }
        }
    }
}
