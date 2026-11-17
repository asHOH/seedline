using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class EventChoices
{
    internal static double Priority(EventModel source, string purpose, params string[] keys)
    {
        var state = RunCheckpoints.Require((RunState)source.Owner!.RunState);
        return AddressRandom.OpenUnit(AddressRandom.UInt64(state.Seed, RandomAddress.For(purpose, [source.Id.Entry, .. keys])));
    }

    internal static List<EventOption> Order(AncientEventModel source, IEnumerable<EventOption> options, string pool) => options
        .Where(o => o.Relic?.IsAllowed(source.Owner!.RunState) ?? true).GroupBy(o => o.Relic?.Id.Entry ?? o.TextKey)
        .OrderBy(g => source is Pael && pool == "2" ? -Math.Log(Priority(source, "Ancient offering", pool, g.Key)) / g.Count()
            : Priority(source, "Ancient offering", pool, g.Key))
        .ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.First()).ToList();

    [HarmonyPatch(typeof(AncientEventModel), "GetOptionsFromThreePools")]
    private static class AncientPools
    {
        private static void Prefix(AncientEventModel __instance, ref List<EventOption> pool1, ref List<EventOption> pool2, ref List<EventOption> pool3)
        {
            // Singleton pools retain the vanilla RNG calls for the other menu components.
            if (__instance is Tezcatara)
                pool1 = Order(__instance, pool1, "1").Take(1).ToList();
            if (__instance is Pael)
                pool2 = Order(__instance, pool2, "2").Take(1).ToList();
            if (__instance is Pael or Orobas)
                pool3 = Order(__instance, pool3, "3").Take(1).ToList();
        }
    }

    [HarmonyPatch]
    private static class AncientMenus
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(Tanx), "GenerateInitialOptions"), AccessTools.Method(typeof(Nonupeipe), "GenerateInitialOptions")];
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var count = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.operand is MethodInfo method && method.Name == "UnstableShuffle")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(AncientMenus), nameof(Shuffle));
                    count++;
                }
                yield return instruction;
            }
            if (count != 1)
                throw new InvalidOperationException("Expected one Ancient menu shuffle.");
        }
        private static List<EventOption> Shuffle(List<EventOption> options, Rng _, AncientEventModel source)
        {
            var ordered = Order(source, options, "main");
            options.Clear();
            options.AddRange(ordered);
            return options;
        }
    }

    [HarmonyPatch(typeof(TheFutureOfPotions), "get_PotionToCardType")]
    private static class PotionTypes
    {
        private static bool Prefix(TheFutureOfPotions __instance, ref Dictionary<PotionModel, CardType>? ____cardTypes, ref Dictionary<PotionModel, CardType> __result)
        {
            ____cardTypes ??= AssignTypes(__instance);
            __result = ____cardTypes;
            return false;
        }
    }

    internal static Dictionary<PotionModel, CardType> AssignTypes(TheFutureOfPotions source)
    {
        var state = RunCheckpoints.Require((RunState)source.Owner!.RunState);
        var copies = new Dictionary<string, int>();
        var offered = source.Owner.Potions.Take(3).Select(p =>
        {
            copies[p.Id.Entry] = copies.GetValueOrDefault(p.Id.Entry) + 1;
            return (Potion: p, Copy: copies[p.Id.Entry]);
        }).ToArray();
        var result = new Dictionary<PotionModel, CardType>();
        foreach (var group in offered.GroupBy(p => p.Potion.Rarity switch
        {
            PotionRarity.Common or PotionRarity.Token => CardRarity.Common,
            PotionRarity.Uncommon => CardRarity.Uncommon,
            _ => CardRarity.Rare
        }))
        {
            var index = 0;
            foreach (var item in group.OrderBy(p => Priority(source, "ordering", p.Potion.Id.Entry, p.Copy.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .ThenBy(p => p.Potion.Id.Entry, StringComparer.Ordinal).ThenBy(p => p.Copy))
            {
                CardType[] types = group.Key == CardRarity.Common ? [CardType.Attack, CardType.Skill] : [CardType.Attack, CardType.Skill, CardType.Power];
                result.Add(item.Potion, types[AddressRandom.IntInclusive(state.Seed,
                    RandomAddress.For("type assignment", source.Id.Entry, group.Key.ToString()).At(index++), 0, types.Length - 1)]);
            }
        }
        return result;
    }

    [HarmonyPatch(typeof(RanwidTheElder), "GenerateInitialOptions")]
    private static class RanwidRelic
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => RewardPatches.Replace(instructions,
            m => m.DeclaringType == typeof(Rng) && m.Name == nameof(Rng.NextItem) && m.GetGenericArguments()[0] == typeof(RelicModel),
            AccessTools.Method(typeof(RanwidRelic), nameof(Choose)));
        private static RelicModel? Choose(Rng _, IEnumerable<RelicModel> relics)
        {
            var state = RunCheckpoints.Require(RunManager.Instance.DebugOnlyGetState()!);
            return relics.OrderBy(r => AddressRandom.UInt64(state.Seed, RandomAddress.For("relic sacrifice", "RANWID_THE_ELDER", r.Id.Entry)))
                .ThenBy(r => r.Id.Entry, StringComparer.Ordinal).FirstOrDefault();
        }
    }

    internal static RelicModel[] Outgoing(RelicTrader source)
    {
        var pairs = source.Owner!.Relics.Where(r => r.IsTradable).SelectMany(r => Enumerable.Range(0, 3)
            .Select(slot => (Relic: r, Slot: slot, Priority: Priority(source, "outgoing assignment", r.Id.Entry, slot.ToString(System.Globalization.CultureInfo.InvariantCulture)))))
            .OrderBy(p => p.Priority).ThenBy(p => p.Relic.Id.Entry, StringComparer.Ordinal).ThenBy(p => p.Slot).ToArray();
        var selected = pairs.Select(p => p.Relic).Distinct().Take(3).ToHashSet();
        var assigned = new Dictionary<int, RelicModel>();
        foreach (var pair in pairs)
            if (selected.Contains(pair.Relic) && !assigned.ContainsKey(pair.Slot))
            {
                assigned.Add(pair.Slot, pair.Relic);
                selected.Remove(pair.Relic);
            }
        return assigned.OrderBy(p => p.Key).Select(p => p.Value).ToArray();
    }

    [HarmonyPatch(typeof(RelicTrader), "get_OwnedRelics")]
    private static class Trader
    {
        private static bool Prefix(RelicTrader __instance, ref IReadOnlyList<RelicModel>? ____ownedRelics, ref IReadOnlyList<RelicModel> __result)
        {
            ____ownedRelics ??= Outgoing(__instance);
            __result = ____ownedRelics;
            return false;
        }
    }

    internal static readonly (string Id, int Weight)[] Dishes =
    [("CAVIAR", 6), ("SPICY_SNAPPY", 3), ("JELLY_LIVER", 3), ("FRIED_EEL", 3), ("SUSPICIOUS_CONDIMENT", 3), ("CLAM_ROLL", 6), ("GOLDEN_FYSH", 1)];

    internal static string Dish(CheckpointState state, HashSet<string> eligible)
    {
        while (true)
        {
            var roll = state.Random.NextIntInclusive(RandomAddress.For("dish selection"), 0, 24);
            foreach (var dish in Dishes)
            {
                roll -= dish.Weight;
                if (roll >= 0)
                    continue;
                if (eligible.Contains(dish.Id))
                    return dish.Id;
                break;
            }
        }
    }

    [HarmonyPatch(typeof(EndlessConveyor), "RollDish")]
    private static class Conveyor
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var count = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(Rng) && method.Name == nameof(Rng.NextFloat))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(Conveyor), nameof(Roll));
                    count++;
                }
                yield return instruction;
            }
            if (count != 1)
                throw new InvalidOperationException("Expected one random dish roll.");
        }
        private static float Roll(Rng _, float maximum, EndlessConveyor source)
        {
            var player = source.Owner!;
            var previous = (string)AccessTools.Field(typeof(EndlessConveyor), "_lastDishId").GetValue(source)!;
            var grabs = (int)AccessTools.Field(typeof(EndlessConveyor), "_numOfGrabs").GetValue(source)!;
            var eligible = Dishes.Where(d => d.Id != previous && (d.Id != "SUSPICIOUS_CONDIMENT" || player.HasOpenPotionSlots) &&
                (d.Id != "CLAM_ROLL" || player.Creature.CurrentHp != player.Creature.MaxHp) && (d.Id != "GOLDEN_FYSH" || grabs > 1)).ToArray();
            var chosen = Dish(RunCheckpoints.Require((RunState)player.RunState), eligible.Select(d => d.Id).ToHashSet(StringComparer.Ordinal));
            // Feed the chosen interval's midpoint into vanilla's dish construction and effects.
            return (eligible.TakeWhile(d => d.Id != chosen).Sum(d => d.Weight) + 0.5f) / eligible.Sum(d => d.Weight);
        }
    }
}
