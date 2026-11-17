using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;

namespace Seedline;

internal static class CombatSelection
{
    private sealed class SelectionSource(AbstractModel model)
    {
        internal AbstractModel Model { get; } = model;
        internal int PowerCellTaken;
        internal int PowerCellTier;
    }
    private static readonly AsyncLocal<SelectionSource?> source = new();
    private static readonly MethodInfo[] methods =
    [AccessTools.Method(typeof(Cinder), "OnPlay"), AccessTools.Method(typeof(TrueGrit), "OnPlay"),
        AccessTools.Method(typeof(Thrash), "OnPlay"), AccessTools.Method(typeof(HiddenGem), "OnPlay"),
        AccessTools.Method(typeof(MummifiedHand), nameof(MummifiedHand.AfterCardPlayed)),
        AccessTools.Method(typeof(Bookmark), nameof(Bookmark.AfterFlush)),
        AccessTools.Method(typeof(JeweledMask), nameof(JeweledMask.BeforeHandDraw)),
        AccessTools.Method(typeof(SimpletonPower), nameof(SimpletonPower.AfterAutoPostPlayPhaseEntered)),
        AccessTools.Method(typeof(StampedePower), nameof(StampedePower.AfterAutoPostPlayPhaseEntered)),
        AccessTools.Method(typeof(BeatDown), "OnPlay"), AccessTools.Method(typeof(Uproar), "OnPlay"),
        AccessTools.Method(typeof(Catastrophe), "OnPlay"), AccessTools.Method(typeof(DrainPower), "OnPlay"),
        AccessTools.Method(typeof(StoneCracker), nameof(StoneCracker.AfterRoomEntered)),
        AccessTools.Method(typeof(AggressionPower), nameof(AggressionPower.BeforeSideTurnStart)),
        AccessTools.Method(typeof(EcstasyPower), nameof(EcstasyPower.AfterPlayerTurnStart)),
        AccessTools.Method(typeof(PunchOfJoy), "OnPlay"), AccessTools.Method(typeof(SeekerStrike), "OnPlay"),
        AccessTools.Method(typeof(TheFool), "OnPlay"), AccessTools.Method(typeof(PowerCell), nameof(PowerCell.BeforeSideTurnStart)),
        AccessTools.Method(typeof(Anointed), "OnPlay"), AccessTools.Method(typeof(TheWorld), "OnPlay")];

    internal static string Group(AbstractModel model) => model switch
    {
        Cinder or TrueGrit => "exhaust",
        DrainPower or StoneCracker => "upgrade",
        Anointed or JeweledMask or PowerCell or TheWorld => "return",
        BeatDown or Uproar or StampedePower or SimpletonPower or TheFool => "auto-play",
        PunchOfJoy or EcstasyPower => "Sadness conversion",
        _ => model.Id.Entry
    };

    internal static List<CardModel> Select(IEnumerable<CardModel> candidates, int count, string group,
        bool preserveWhole = false, bool incidental = true)
    {
        var eligible = candidates.ToArray();
        count = Math.Clamp(count, 0, eligible.Length);
        if (count == 0)
            return [];
        if (eligible.Length == 1)
            return [eligible[0]];
        var scope = CombatStateScope.Require((CombatState)eligible[0].Owner.Creature.CombatState!);
        if (preserveWhole && count == eligible.Length)
            return incidental ? Incidental(eligible, scope) : eligible.ToList();
        var address = scope.For("combat card selection", group);
        var keys = scope.Selection.Select(scope.Seed, address, eligible.Select(c => CombatCards.Info(c).Key), count);
        var selected = keys.Select(k => eligible.Single(c => CombatCards.Info(c).Key == k)).ToList();
        foreach (var key in keys)
            scope.Selection.Renew(scope.Seed, address, key);
        return selected;
    }

    private static List<CardModel> Incidental(IEnumerable<CardModel> cards, CombatStateScope scope)
    {
        var result = cards.OrderBy(c => CombatCards.Info(c).Address, StringComparer.Ordinal).ToList();
        var address = scope.For("incidental shuffle");
        for (var i = result.Count - 1; i > 0; i--)
        {
            var j = scope.Random.NextIntInclusive(address, 0, i);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }

    private static List<CardModel> Choose(IEnumerable<CardModel> candidates, int count)
    {
        var current = source.Value ?? throw new InvalidOperationException("Missing combat selection source.");
        var model = current.Model;
        var eligible = candidates.ToArray();
        var whole = model is Anointed or DrainPower or StoneCracker or SeekerStrike or AggressionPower or PowerCell or TheWorld or EcstasyPower;
        if (model is Anointed or AggressionPower or PowerCell or TheWorld && eligible.Length > 0)
            whole &= CardPile.MaxCardsInHand - eligible[0].Owner.PlayerCombatState!.Hand.Cards.Count >= Math.Min(count, eligible.Length);
        return Select(eligible, count, Group(model), whole, model is not SeekerStrike);
    }

    private static CardModel? One(Rng _, IEnumerable<CardModel> cards) => Choose(cards, 1).FirstOrDefault();
    private static IEnumerable<CardModel> Batch(IEnumerable<CardModel> cards, int count, Rng _) => Choose(cards, count);

    private static List<T> Ordered<T>(List<T> cards, Rng _) where T : CardModel
    {
        var current = source.Value ?? throw new InvalidOperationException("Missing combat selection source.");
        var count = current.Model switch
        {
            BeatDown card => card.DynamicVars.Cards.IntValue,
            SeekerStrike card => card.DynamicVars.Cards.IntValue,
            StoneCracker relic => relic.DynamicVars.Cards.IntValue,
            PowerCell relic => Math.Max(0, relic.DynamicVars.Cards.IntValue - current.PowerCellTaken),
            AggressionPower power => power.Amount,
            EcstasyPower power => power.Amount,
            _ => 1
        };
        if (count <= 0)
            return cards;
        List<CardModel> selected;
        if (current.Model is PowerCell && current.PowerCellTier++ > 0)
        {
            // Innate fallback orders the entire tier without consuming its return clocks.
            selected = cards.Count < 2 ? cards.Cast<CardModel>().ToList() :
                Incidental(cards, CombatStateScope.Require((CombatState)cards[0].Owner.Creature.CombatState!));
        }
        else
            selected = Choose(cards, count);
        if (current.Model is PowerCell)
            current.PowerCellTaken += Math.Min(count, cards.Count);
        var remainder = cards.Where(c => !selected.Contains(c)).ToArray();
        cards.Clear();
        cards.AddRange(selected.Cast<T>());
        cards.AddRange(remainder);
        return cards;
    }

    [HarmonyPatch]
    private static class Context
    {
        private static IEnumerable<MethodBase> TargetMethods() => methods;
        private static void Prefix(AbstractModel __instance, out SelectionSource? __state)
        {
            __state = source.Value;
            source.Value = new(__instance);
        }
        private static void Finalizer(SelectionSource? __state) => source.Value = __state;
    }

    [HarmonyPatch]
    private static class Selectors
    {
        private static IEnumerable<MethodBase> TargetMethods() => methods.Select(m =>
            m.GetCustomAttribute<AsyncStateMachineAttribute>() is { } async
                ? AccessTools.Method(async.StateMachineType, "MoveNext") : m);
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var count = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.operand is MethodInfo method && method.IsGenericMethod &&
                    (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt))
                {
                    MethodInfo? replacement = null;
                    if (method.DeclaringType == typeof(Rng) && method.Name == nameof(Rng.NextItem) && method.GetGenericArguments()[0] == typeof(CardModel))
                        replacement = AccessTools.Method(typeof(CombatSelection), nameof(One));
                    else if (method.DeclaringType == typeof(IEnumerableExtensions) && method.Name == nameof(IEnumerableExtensions.TakeRandom))
                        replacement = AccessTools.Method(typeof(CombatSelection), nameof(Batch));
                    else if (method.DeclaringType == typeof(ListExtensions) && method.Name is nameof(ListExtensions.StableShuffle) or nameof(ListExtensions.UnstableShuffle))
                        replacement = AccessTools.Method(typeof(CombatSelection), nameof(Ordered)).MakeGenericMethod(method.GetGenericArguments());
                    if (replacement != null)
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = replacement;
                        count++;
                    }
                }
                yield return instruction;
            }
            var type = __originalMethod.DeclaringType!;
            if (type.DeclaringType != null)
                type = type.DeclaringType;
            var expected = type == typeof(MummifiedHand) ? 4 :
                type == typeof(Uproar) || type == typeof(Catastrophe) || type == typeof(TheFool) || type == typeof(PowerCell) ? 2 : 1;
            if (count != expected)
                throw new InvalidOperationException($"Expected {expected} combat selection calls in {type.Name}, found {count}.");
        }
    }

    [HarmonyPatch]
    private static class Hopper
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(ThievingHopper), "ThieveryMove");
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Rng) && m.Name == nameof(Rng.NextItem),
                AccessTools.Method(typeof(Hopper), nameof(Choose)));
        private static CardModel? Choose(Rng _, IEnumerable<CardModel> cards)
        {
            var eligible = cards.ToArray();
            if (eligible.Length == 0)
                return null;
            var scope = CombatStateScope.Require((CombatState)eligible[0].Owner.Creature.CombatState!);
            return eligible.OrderBy(c => AddressRandom.UInt64(scope.Seed,
                RandomAddress.For("Thieving Hopper").With(CardOccurrences.Info(c.DeckVersion!).Key)))
                .ThenBy(c => CardOccurrences.Info(c.DeckVersion!).Key, StringComparer.Ordinal).First();
        }
    }
}
