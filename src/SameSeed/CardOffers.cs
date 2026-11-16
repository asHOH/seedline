using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace SameSeed;

internal sealed record CardOfferEntry(string Offer, int EntryOrder, string WingAddress)
{
    internal string Offer { get; set; } = Offer;
    internal int EntryOrder { get; set; } = EntryOrder;
    internal int CopyNumber { get; set; }
    internal bool Supplementary { get; init; }
    internal bool Candy { get; set; }
    internal int RareOrder { get; set; } = -1;
    internal string WingAddress { get; init; } = WingAddress;
    internal bool KeyAssigned { get; set; }
}

internal static class CardOffers
{
    private static readonly ConditionalWeakTable<CardModel, CardOfferEntry> entries = new();

    internal static CardOfferEntry? Find(CardModel card) => entries.TryGetValue(card, out var entry) ? entry : null;
    internal static CardOfferEntry Info(CardModel card) => Find(card)
        ?? throw new InvalidOperationException($"Missing card offer: {card.Id}.");

    internal static void Attach(CardModel card, string offer, int order, bool supplementary = false, string? wing = null)
    {
        var wingAddress = wing ?? CardOccurrences.Info(card).Address;
        var entry = Find(card) ?? new(offer, order, wingAddress);
        entries.AddOrUpdate(card, entry with
        {
            Offer = offer,
            EntryOrder = order,
            Supplementary = supplementary,
            WingAddress = wingAddress,
            KeyAssigned = false
        });
    }

    internal static void Recreate(CardModel original, CardModel replacement)
    {
        if (Find(original) is { } entry)
            entries.AddOrUpdate(replacement, entry with { });
    }

    internal static void Forget(CardModel card) => entries.Remove(card);

    internal static void Rebind(CardModel card, CardOfferEntry? entry)
    {
        if (entry == null)
            Forget(card);
        else
            entries.AddOrUpdate(card, entry);
    }

    internal static void NumberOffer(IEnumerable<CardModel> offered)
    {
        foreach (var group in offered.GroupBy(c => c.Id))
        {
            var index = 0;
            foreach (var card in group.OrderBy(c => Info(c).EntryOrder))
                Info(card).CopyNumber = ++index;
        }
    }

    internal static void AssignKeys(IEnumerable<CardModel> selected)
    {
        foreach (var selection in selected.Where(c => Find(c) != null).GroupBy(c => Info(c).Offer))
        {
            var pending = selection.ToList();
            var slots = Enumerable.Range(1, pending.Count).ToList();
            var state = RunCheckpoints.Require((RunState)pending[0].Owner.RunState);
            while (pending.Count > 0)
            {
                var pair = (from card in pending
                            from slot in slots
                            select (Card: card, Slot: slot, Priority: AddressRandom.UInt64(state.Seed,
                                RandomAddress.For("card-key assignment").With(selection.Key).With(card.Id.Entry).At(Info(card).CopyNumber).At(slot))))
                    .OrderBy(p => p.Priority).ThenBy(p => p.Card.Id.Entry, StringComparer.Ordinal)
                    .ThenBy(p => Info(p.Card).CopyNumber).ThenBy(p => p.Slot).First();
                CardOccurrences.AssignKey(pair.Card, RandomAddress.For("card key").With(selection.Key).At(pair.Slot).Key);
                Info(pair.Card).KeyAssigned = true;
                pending.Remove(pair.Card);
                slots.Remove(pair.Slot);
            }
        }
    }

    internal static void PrepareAcquisition(IEnumerable<CardModel> cards)
    {
        foreach (var card in cards.Where(c => Find(c) is { KeyAssigned: false }))
            AssignKeys([card]);
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromSimpleGridForRewards))]
    private static class GridKeys
    {
        private static void Prefix(List<CardCreationResult> cards)
        {
            var generated = cards.Select(c => c.Card).Where(c => Find(c) != null).ToArray();
            if (generated.Length == 0)
                return;
            var offer = Info(generated[0]).Offer;
            for (var i = 0; i < generated.Length; i++)
            {
                Info(generated[i]).Offer = offer;
                Info(generated[i]).EntryOrder = i;
            }
            NumberOffer(generated);
        }
        private static void Postfix(ref Task<IEnumerable<CardModel>> __result) => __result = CompleteSelection(__result);
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseABundleScreen))]
    private static class BundleKeys
    {
        private static void Postfix(ref Task<IEnumerable<CardModel>> __result) => __result = CompleteSelection(__result);
    }

    private static async Task<IEnumerable<CardModel>> CompleteSelection(Task<IEnumerable<CardModel>> task)
    {
        var selected = (await task).ToArray();
        AssignKeys(selected);
        return selected;
    }

    [HarmonyPatch(typeof(CardReward), nameof(CardReward.Populate))]
    private static class ManualOffer
    {
        private static void Prefix(List<CardCreationResult> ____cards, bool ____cardsWereManuallySet)
        {
            if (!____cardsWereManuallySet || ____cards.Count == 0 || ____cards.Any(c => CardOccurrences.Find(c.Card) == null))
                return;
            var offer = CardOccurrences.Info(____cards[0].Card).Address;
            for (var i = 0; i < ____cards.Count; i++)
                Attach(____cards[i].Card, offer, i);
            NumberOffer(____cards.Select(c => c.Card));
        }
    }
}
