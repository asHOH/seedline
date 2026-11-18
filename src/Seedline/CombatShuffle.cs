using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Seedline;

internal static class CombatShuffle
{
    private sealed record ShuffleContext(Player Player, CombatState Combat, bool Opening);
    private static readonly AsyncLocal<ShuffleContext?> context = new();

    private readonly record struct RankedCard(CardModel Card, FrozenCardOrder Rank, CardOccurrence Info);
    private static RankedCard[] Capture(IEnumerable<CardModel> cards, ICombatState? pileCombat = null)
    {
        var batch = new CardOrder.CombatRankBatch();
        // Removed snapshots point to themselves and have no live pile or combat hooks.
        return cards.Select(c => new RankedCard(c, batch.ShuffleRank(c, c.DeckVersion == c ? null : pileCombat),
            CardOccurrences.Info(c))).ToArray();
    }

    internal static Dictionary<CardModel, string> Keys(IEnumerable<CardModel> candidates, int act, bool innate) =>
        AssignKeys(Capture(candidates), act, innate);

    private static Dictionary<CardModel, string> AssignKeys(RankedCard[] cards, int act, bool innate)
    {
        var result = new Dictionary<CardModel, string>();
        void Sequence(IEnumerable<RankedCard> group, string name)
        {
            var i = 0;
            foreach (var entry in group)
                result.Add(entry.Card, RandomAddress.For("shuffle key", name).At(i++).Key);
        }
        IEnumerable<RankedCard> ordinary = cards;
        if (innate)
        {
            // Combat keyword hooks change Ethereal and Retain; Innate comes from the card itself.
            var byInnate = cards.ToLookup(c => c.Card.GetKeywordsWithSources(KeywordSources.Local).Contains(CardKeyword.Innate));
            Sequence(byInnate[true].OrderBy(c => c.Rank)
                .ThenBy(c => c.Info.Address, StringComparer.Ordinal), "Innate");
            ordinary = byInnate[false];
        }
        Sequence(ordinary.Where(c => c.Rank.Category <= 4).OrderBy(c => c.Rank)
            .ThenBy(c => c.Info.Address, StringComparer.Ordinal), "first");
        var later = ordinary.Where(c => c.Rank.Category > 4).ToArray();
        if (act >= 2)
            Sequence(later.OrderByDescending(c => c.Rank).ThenBy(c => c.Info.Address, StringComparer.Ordinal), "second");
        else
        {
            foreach (var entry in later.Where(c => c.Info.SourceKey))
                result.Add(entry.Card, entry.Info.Key);
            Sequence(later.Where(c => !c.Info.SourceKey)
                .OrderBy(c => c.Info.Acquisition == null ? 1 : 0)
                .ThenBy(c => c.Info.Acquisition ?? CombatCards.Info(c.Card).CreationBatch)
                .ThenBy(c => c.Rank).ThenBy(c => c.Info.Address, StringComparer.Ordinal), "second");
        }
        return result;
    }

    private static List<CardModel> Shuffle(List<CardModel> cards, Rng _)
    {
        var current = context.Value ?? throw new InvalidOperationException("Missing combat shuffle context.");
        var scope = CombatStateScope.Require(current.Combat);
        var candidates = cards.ToList();
        if (current.Opening)
        {
            foreach (var json in RunCheckpoints.Require((RunState)current.Combat.RunState).Cards.Removed.Values)
            {
                var saved = JsonSerializer.Deserialize(json, JsonSerializationUtility.GetTypeInfo<SerializableCard>())!;
                var removed = CardModel.FromSerializable(saved);
                removed.Owner = current.Player;
                removed.DeckVersion = removed;
                candidates.Add(removed);
            }
        }
        var ranked = Capture(candidates, current.Combat);
        var act = current.Combat.RunState.CurrentActIndex;
        if (current.Opening)
        {
            var ordinary = AssignKeys(ranked, act, false);
            foreach (var card in cards)
                CombatCards.Info(card).Key = ordinary[card];
        }
        var keys = AssignKeys(ranked, act, current.Opening);
        var counter = scope.For("shuffle");
        var address = counter.At(scope.Random.Advance(counter));
        var priorities = cards.ToDictionary(c => c, c => AddressRandom.UInt64(scope.Seed, address.With(keys[c])));
        cards.Sort((a, b) =>
        {
            var order = priorities[a].CompareTo(priorities[b]);
            return order != 0 ? order : StringComparer.Ordinal.Compare(CombatCards.Info(a).Address, CombatCards.Info(b).Address);
        });
        return cards;
    }

    [HarmonyPatch]
    private static class Shuffles
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(CardPile), nameof(CardPile.RandomizeOrderInternal)),
            AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.Shuffle))];
        private static void Prefix(Player player, MethodBase __originalMethod, out ShuffleContext? __state)
        {
            __state = context.Value;
            context.Value = new(player, (CombatState)player.Creature.CombatState!, __originalMethod.DeclaringType == typeof(CardPile));
        }
        private static void Finalizer(ShuffleContext? __state) => context.Value = __state;
    }

    [HarmonyPatch]
    private static class Rolls
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(CardPile), nameof(CardPile.RandomizeOrderInternal)),
            RewardPatches.AsyncMethod(typeof(CardPileCmd), nameof(CardPileCmd.Shuffle))];
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.Name is "UnstableShuffle" or "StableShuffle",
                AccessTools.Method(typeof(CombatShuffle), nameof(Shuffle)));
    }
}
