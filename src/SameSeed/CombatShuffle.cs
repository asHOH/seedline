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

namespace SameSeed;

internal static class CombatShuffle
{
    private sealed record ShuffleContext(Player Player, CombatState Combat, bool Opening);
    private static readonly AsyncLocal<ShuffleContext?> context = new();

    internal static Dictionary<CardModel, string> Keys(IEnumerable<CardModel> candidates, int act, bool innate)
    {
        var cards = candidates.Select(c => (Card: c, Rank: CardOrder.CaptureCombatRank(c), Info: CardOccurrences.Info(c))).ToArray();
        var result = new Dictionary<CardModel, string>();
        void Sequence(IEnumerable<(CardModel Card, FrozenCardOrder Rank, CardOccurrence Info)> group, string name)
        {
            var i = 0;
            foreach (var entry in group)
                result.Add(entry.Card, RandomAddress.For("shuffle key", name).At(i++).Key);
        }
        var ordinary = cards.Where(c => !innate || !c.Card.Keywords.Contains(CardKeyword.Innate)).ToArray();
        if (innate)
            Sequence(cards.Where(c => c.Card.Keywords.Contains(CardKeyword.Innate)).OrderBy(c => c.Rank)
                .ThenBy(c => c.Info.Address, StringComparer.Ordinal), "Innate");
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
            var ordinary = Keys(candidates, current.Combat.RunState.CurrentActIndex, false);
            foreach (var card in cards)
                CombatCards.Info(card).Key = ordinary[card];
        }
        var keys = Keys(candidates, current.Combat.RunState.CurrentActIndex, current.Opening);
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
