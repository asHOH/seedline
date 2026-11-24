using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class RelicRewards
{
    private static readonly string[] sources = ["generic", "shop", "Courier"];
    private static readonly RelicRarity[] rarities = [RelicRarity.Shop, RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare];
    internal static RandomAddress Address(string source, RelicRarity rarity) => RandomAddress.For("relic identity", source, rarity.ToString());
    internal static IEnumerable<RelicModel> Pool(Player player) => ModelDb.RelicPool<SharedRelicPool>().GetUnlockedRelics(player.UnlockState)
        .Concat(player.Character.RelicPool.GetUnlockedRelics(player.UnlockState)).Where(r => rarities.Contains(r.Rarity));

    internal static int[] AllocationCounts(RelicRarity rarity, int poolSize)
    {
        if (rarity == RelicRarity.Shop)
            return [0, poolSize, 0];

        int[] weights = [9, 7, 4];
        var counts = weights.Select(weight => (int)((long)poolSize * weight / 20)).ToArray();
        // Largest remainders; reverse source order preserves the vanilla Uncommon split.
        foreach (var source in Enumerable.Range(0, sources.Length)
            .OrderByDescending(i => (long)poolSize * weights[i] % 20).ThenByDescending(i => i)
            .Take(poolSize - counts.Sum()))
            counts[source]++;
        return counts;
    }

    internal static void Initialize(Player player)
    {
        var state = RunCheckpoints.Require((RunState)player.RunState);
        if (state.Rewards.RelicsInitialized)
            return;
        foreach (var rarity in rarities)
        {
            var pool = Pool(player).Where(r => r.Rarity == rarity).OrderBy(r => r.Id.Entry, StringComparer.Ordinal).ToArray();
            Shuffle(pool, state.Seed, RandomAddress.For("relic allocation", rarity.ToString()));
            var counts = AllocationCounts(rarity, pool.Length);
            var start = 0;
            for (var source = 0; source < sources.Length; source++)
            {
                var allocated = pool.Skip(start).Take(counts[source]).ToArray();
                var address = Address(sources[source], rarity);
                Shuffle(allocated, state.Seed, RandomAddress.For("relic order", sources[source], rarity.ToString()));
                state.Rewards.Supplies.Add(address.Key, new()
                {
                    Positions = allocated.Select((r, i) => new SupplyPosition { Entry = new(r.Id.Entry, address.At(i).Key) }).ToList()
                });
                start += counts[source];
            }
        }
        state.Rewards.RelicsInitialized = true;
        foreach (var relic in player.Relics.Where(r => !r.IsStackable))
            Retire(state.Rewards, relic.Id.Entry);
    }

    internal static void Shuffle<T>(T[] items, string seed, RandomAddress address)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = AddressRandom.IntInclusive(seed, address.At(i), 0, i);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    internal static RelicRarity Roll(Player player, string source)
    {
        var state = RunCheckpoints.Require((RunState)player.RunState);
        return Rarity(state.Random.NextUnit(RandomAddress.For("relic rarity", source)));
    }

    internal static RelicRarity Rarity(double value) => value < 0.5 ? RelicRarity.Common : value < 0.83 ? RelicRarity.Uncommon : RelicRarity.Rare;

    internal static RelicModel Draw(Player player, string source, RelicRarity rarity, Func<RelicModel, bool>? filter = null,
        bool remove = false, bool chest = false, long chestOrdinal = 0, bool tail = false)
    {
        Initialize(player);
        var state = RunCheckpoints.Require((RunState)player.RunState);
        var rewards = state.Rewards;
        var pool = Pool(player).ToDictionary(r => r.Id.Entry, StringComparer.Ordinal);
        foreach (var relic in pool.Values.Where(r => !r.IsAllowed(player.RunState)))
            Retire(rewards, relic.Id.Entry);
        var shared = ModelDb.RelicPool<SharedRelicPool>().AllRelics.Select(r => r.Id.Entry).ToHashSet(StringComparer.Ordinal);
        HashSet<string> Eligible(RelicRarity r) => pool.Values.Where(model => model.Rarity == r &&
            !rewards.RetiredRelics.Contains(model.Id.Entry) && (filter == null || filter(model)) &&
            (!chest || (shared.Contains(model.Id.Entry) && !rewards.ChestOffers.Contains(model.Id.Entry))))
            .Select(model => model.Id.Entry).ToHashSet(StringComparer.Ordinal);
        SupplyEntry? Borrow(RelicRarity r, HashSet<string> eligible)
        {
            var donors = source == "Courier" ? new[] { "generic", "shop" } : new[] { "Courier", source == "generic" ? "shop" : "generic" };
            foreach (var donor in donors)
                if (rewards.Supply(Address(donor, r)).Tail(eligible) is { } entry)
                    return entry;
            return null;
        }
        SupplyEntry? chosen = null;
        var removeEntry = remove || (!chest && filter == null);
        var advancement = tail ? "tail" : removeEntry ? "remove" : "filtered marker";
        foreach (var candidateRarity in rarities.Skip(Array.IndexOf(rarities, rarity)))
        {
            var eligible = Eligible(candidateRarity);
            var lifetime = -1;
            if (chest)
            {
                var size = sources.SelectMany(s => rewards.Supply(Address(s, candidateRarity)).Positions)
                    .Count(p => p.Entry != null && eligible.Contains(p.Entry.Identity));
                lifetime = (int)(AddressRandom.Unit(AddressRandom.UInt64(state.Seed,
                    RandomAddress.For("marker lifetime", candidateRarity.ToString()).At(chestOrdinal))) * size);
            }
            var supply = rewards.Supply(Address(source, candidateRarity));
            DecisionTrace.Record("relic-request", () => new
            {
                seed = state.Seed,
                supply = Address(source, candidateRarity).Key,
                eligible = eligible.Order(StringComparer.Ordinal),
                tail,
                advancement,
                chest,
                lifetime
            });
            chosen = tail ? supply.Tail(eligible) ?? Borrow(candidateRarity, eligible)
                : supply.Draw(eligible, borrow: e => Borrow(candidateRarity, e), remove: removeEntry, lifetime: lifetime);
            if (chosen != null)
                break;
        }
        var result = chosen == null ? RelicFactory.FallbackRelic : pool[chosen.Identity];
        DecisionTrace.Record("relic-reward", () => new
        {
            seed = state.Seed,
            source,
            requestedRarity = rarity.ToString(),
            deliveredRarity = result.Rarity.ToString(),
            identity = result.Id.Entry,
            entry = chosen?.Address,
            tail,
            advancement = chosen == null ? "none" : advancement,
            chest,
            chestOrdinal
        });
        if (chosen != null)
            RewardEntries.Attach(result, chosen);
        if (chest)
            rewards.ChestOffers.Add(result.Id.Entry);
        else
        {
            Retire(rewards, result.Id.Entry);
            player.RelicGrabBag.Remove(result);
        }
        player.RunState.SharedRelicGrabBag.Remove(result);
        return result;
    }

    internal static void Retire(RewardState state, string identity)
    {
        state.RetiredRelics.Add(identity);
        foreach (var (key, supply) in state.Supplies)
            if (key.StartsWith("[\"relic identity\",", StringComparison.Ordinal))
                supply.Remove(identity);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.AddRelicInternal))]
    private static class Acquisition
    {
        private static void Postfix(Player __instance, RelicModel relic)
        {
            if (!relic.IsStackable && __instance.RunState is RunState run && RunCheckpoints.Find(run) is { } state)
                Retire(state.Rewards, relic.Id.Entry);
        }
    }

    [HarmonyPatch]
    private static class GenericDraw
    {
        private static IEnumerable<MethodBase> TargetMethods() => typeof(RelicFactory).GetMethods()
            .Where(m => m.Name is nameof(RelicFactory.PullNextRelicFromFront) or nameof(RelicFactory.PullNextRelicForRewardFromFront));

        private static bool Prefix(Player player, object[] __args, ref RelicModel __result)
        {
            if (RunScope.Bypasses(player.RunState)) return true;
            var rarity = __args.OfType<RelicRarity>().Cast<RelicRarity?>().FirstOrDefault() ?? Roll(player, "generic");
            var filter = __args.OfType<Func<RelicModel, bool>>().FirstOrDefault();
            // Wongos is the sole filtered front caller in the supported build.
            if (filter != null && rarity == RelicRarity.Rare && player.Gold < 200)
                __result = RelicFactory.FallbackRelic;
            else
                __result = Draw(player, filter != null && rarity == RelicRarity.Rare ? "shop" : "generic", rarity, filter, remove: filter != null);
            return false;
        }
    }

    [HarmonyPatch(typeof(RelicFactory), nameof(RelicFactory.PullNextRelicFromBack), [typeof(Player), typeof(RelicRarity), typeof(Func<RelicModel, bool>)])]
    private static class ShopDraw
    {
        private static bool Prefix(Player player, RelicRarity rarity, Func<RelicModel, bool> filter, ref RelicModel __result)
        {
            if (RunScope.Bypasses(player.RunState)) return true;
            __result = Draw(player, ShopRewards.IsInitial ? "shop" : "Courier", rarity, filter);
            return false;
        }
    }

    [HarmonyPatch]
    private static class ShopRarity
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(MerchantInventory), "GetDefaultRelicEntries"), AccessTools.Method(typeof(MerchantRelicEntry), "RestockAfterPurchase")];
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(RelicFactory) && m.Name == "RollRarity",
                AccessTools.Method(typeof(ShopRarity), nameof(Choose)), __originalMethod.Name == "GetDefaultRelicEntries" ? 2 : 1);
        private static RelicRarity Choose(Rng rng) => RunScope.IsBypassed ? RelicFactory.RollRarity(rng)
            : Roll(RunManager.Instance.DebugOnlyGetState()!.Players[0], ShopRewards.IsInitial ? "shop" : "Courier");
    }

    [HarmonyPatch(typeof(RelicTrader), "get_NewRelics")]
    private static class TraderSupply
    {
        private static bool Prefix(RelicTrader __instance, ref IReadOnlyList<RelicModel>? ____newRelics, ref IReadOnlyList<RelicModel> __result)
        {
            if (RunScope.Bypasses(__instance.Owner!.RunState)) return true;
            if (____newRelics == null)
            {
                var state = RunCheckpoints.Require((RunState)__instance.Owner!.RunState);
                ____newRelics = Enumerable.Range(0, 3).Select(i => Draw(__instance.Owner, "Courier",
                    Rarity(AddressRandom.Unit(AddressRandom.UInt64(state.Seed,
                        RandomAddress.For("replacement rarity", __instance.Id.Entry).At(i)))), tail: true)).ToArray();
            }
            __result = ____newRelics;
            return false;
        }
    }

    [HarmonyPatch]
    private static class NeowPair
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(NeowsBones), "AfterObtained");
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.Name == "UnstableShuffle" && m.IsGenericMethod &&
                m.GetGenericArguments()[0] == typeof(RelicModel), AccessTools.Method(typeof(NeowPair), nameof(Shuffle)));
        private static List<RelicModel> Shuffle(List<RelicModel> relics, Rng rng) =>
            relics.UnstableShuffle(RunScope.IsBypassed ? rng : CardRewards.EventRng(RunManager.Instance.DebugOnlyGetState()!.Players[0]));
    }
}
