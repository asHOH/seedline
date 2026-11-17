using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class HandEntries
{
    internal sealed record Entry(long Ordinal, FrozenCardOrder Rank);
    private sealed class Batch(IEnumerable<CardModel> cards)
    {
        internal HashSet<CardModel> Cards { get; } = cards.ToHashSet();
        internal HashSet<CardModel> Entered { get; } = [];
        internal long? Ordinal;
    }
    private static readonly AsyncLocal<Batch?> batch = new();
    // The logical record carries this history through combat routing.
    private static readonly ConditionalWeakTable<CombatCardOccurrence, Entry> entries = new();
    private static readonly ConditionalWeakTable<CombatState, Dictionary<long, long>> creationEntries = new();
    private static readonly ConditionalWeakTable<SneckoOil, Queue<int>> oilCosts = new();
    internal static Entry? Find(CardModel card) => CombatCards.Find(card) is { } occurrence &&
        entries.TryGetValue(occurrence, out var entry) ? entry : null;
    internal static Entry Info(CardModel card) => Find(card)
        ?? throw new InvalidOperationException($"Missing hand-entry history for {card.Id}.");

    [HarmonyPatch(typeof(StratagemPower), nameof(StratagemPower.AfterShuffle))]
    private static class Stratagem
    {
        private static void Prefix(Player player, out Batch? __state)
        {
            __state = batch.Value;
            batch.Value = new(player.PlayerCombatState!.DrawPile.Cards);
        }
        private static void Finalizer(Batch? __state) => batch.Value = __state;
    }

    [HarmonyPatch(typeof(AggressionPower), nameof(AggressionPower.BeforeSideTurnStart))]
    private static class Aggression
    {
        private static void Prefix(AggressionPower __instance, out Batch? __state)
        {
            __state = batch.Value;
            batch.Value = new(__instance.Owner.Player!.PlayerCombatState!.DiscardPile.Cards.Where(c => c.Type == CardType.Attack));
        }
        private static void Finalizer(Batch? __state) => batch.Value = __state;
    }

    [HarmonyPatch]
    private static class Batches
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardsToCombat)),
            AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.Add),
                [typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool), typeof(bool)])];
        private static void Prefix(IEnumerable<CardModel> cards, object[] __args, out Batch? __state)
        {
            __state = batch.Value;
            if (__args[1] is CardPile { Type: not PileType.Hand } || __args[1] is PileType type && type != PileType.Hand)
                return;
            var candidates = cards.ToArray();
            if (batch.Value is not { } current || candidates.Any(c => !current.Cards.Contains(c) || current.Entered.Contains(c)))
                batch.Value = new(candidates);
        }
        private static void Finalizer(Batch? __state) => batch.Value = __state;
    }

    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    private static class Entered
    {
        private static void Prefix(CardModel card, out bool __state) => __state = CombatCards.Find(card) == null;
        private static void Postfix(CardPile __instance, CardModel card, bool __state)
        {
            if (__instance.Type != PileType.Hand || card.Owner.Creature.CombatState is not CombatState combat)
                return;
            CombatCards.Admit(card);
            var scope = CombatStateScope.Require(combat);
            var creation = CombatCards.Info(card).CreationBatch;
            var created = creationEntries.GetOrCreateValue(combat);
            long ordinal;
            if (__state && creation >= 0 && created.TryGetValue(creation, out var existing))
                ordinal = existing;
            else if (batch.Value is { } current && current.Cards.Contains(card) && current.Entered.Add(card))
                ordinal = current.Ordinal ??= scope.Random.Advance(scope.For("hand entry"));
            else
                ordinal = scope.Random.Advance(scope.For("hand entry"));
            if (__state && creation >= 0)
                created[creation] = ordinal;
            entries.AddOrUpdate(CombatCards.Info(card), new(ordinal, CardOrder.CaptureCombat(card)));
        }
    }

    internal static RandomAddress OilAddress(SneckoOil potion, string category) =>
        RandomAddress.For("Oil cost").At(PotionEffects.EffectOrdinal(potion)).With(category);

    private static Queue<int> Assign(SneckoOil potion)
    {
        var state = RunCheckpoints.Require((RunState)potion.Owner.RunState);
        var cards = potion.Owner.PlayerCombatState!.Hand.Cards
            .Where(c => !c.EnergyCost.CostsX && c.EnergyCost.GetWithModifiers(CostModifiers.None) >= 0).ToArray();
        var costs = new Dictionary<CardModel, int>();
        foreach (var category in cards.GroupBy(CombatCosts.Category))
            foreach (var card in category.OrderBy(c => Info(c).Ordinal).ThenBy(c => Info(c).Rank)
                .ThenBy(c => CombatCards.Info(c).Address, StringComparer.Ordinal))
                costs.Add(card, state.Random.NextIntInclusive(OilAddress(potion, category.Key), 0, 3));
        return new(cards.Select(c => costs[c]));
    }

    [HarmonyPatch(typeof(SneckoOil), "NextEnergyCost")]
    private static class Oil
    {
        private static bool Prefix(SneckoOil __instance, ref int __result)
        {
            if (__instance.TestEnergyCostOverride >= 0)
                return true;
            __result = oilCosts.GetValue(__instance, Assign).Dequeue();
            return false;
        }
    }
}
