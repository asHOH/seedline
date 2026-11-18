using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class PermanentSelection
{
    internal static readonly RandomAddress Upgrade = RandomAddress.For("permanent upgrade");
    internal static readonly RandomAddress Downgrade = RandomAddress.For("permanent downgrade");
    internal static readonly RandomAddress Bridge = RandomAddress.For("Slippery Bridge");
    internal static CheckpointState State(CardModel card) => RunCheckpoints.Require((RunState)card.Owner.RunState);
    internal static string Key(CardModel card) => CardOccurrences.Info(card).Key;

    internal static CardModel[] Select(IEnumerable<CardModel> eligible, int count, RandomAddress scope)
    {
        var cards = eligible.ToArray();
        if (cards.Length == 0 || count <= 0)
            return [];
        var state = State(cards[0]);
        var byKey = cards.ToDictionary(Key, StringComparer.Ordinal);
        return state.Selection.Select(state.Seed, scope, byKey.Keys, count).Select(key => byKey[key]).ToArray();
    }

    internal static IEnumerable<CardModel> UpgradeBatch(IEnumerable<CardModel> cards, int count)
    {
        var selected = Select(cards, count, Upgrade);
        foreach (var card in selected.OfType<DingyDagger>())
            RefillUpgrade(card);
        return selected;
    }

    internal static CardModel? UpgradeOne(Rng _, IEnumerable<CardModel> cards) => UpgradeBatch(cards, 1).FirstOrDefault();

    private static void RefillUpgrade(CardModel card)
    {
        var state = State(card);
        var clock = state.Selection.Get(state.Seed, Upgrade, Key(card));
        if (clock.Remaining == 0)
            clock.Remaining = -Math.Log(state.Random.NextOpenUnit(RandomAddress.For("upgrade refill")));
    }

    internal static CardModel? DowngradeOne(Rng rng, IEnumerable<CardModel> cards) =>
        RefillDowngrade(rng, Select(cards, 1, Downgrade).FirstOrDefault());

    internal static CardModel? RefillDowngrade(Rng rng, CardModel? card)
    {
        if (card == null)
            return null;
        RefillUpgrade(card);
        var state = State(card);
        // Midpoints keep the retained event RNG's exponential draw strictly inside (0, 1).
        state.Selection.Get(state.Seed, Downgrade, Key(card)).Remaining =
            -Math.Log(AddressRandom.OpenUnit(rng.NextUnsignedLong()));
        return card;
    }

    internal static CardModel[] Fixed(IEnumerable<CardModel> eligible, int count, string purpose)
    {
        var cards = eligible.ToArray();
        if (cards.Length == 0)
            return [];
        var seed = State(cards[0]).Seed;
        return cards.OrderBy(c => AddressRandom.UInt64(seed, RandomAddress.For(purpose, Key(c))))
            .ThenBy(Key, StringComparer.Ordinal).Take(count).ToArray();
    }

    internal static void Route(IReadOnlyList<CardModel> eligible, IReadOnlyCollection<CardModel> clicked, string operation)
    {
        if (clicked.Count == 0)
            return;
        var state = State(clicked.First());
        // Removal retires its inputs; Tooth uses upgrade routing because its stored cards return.
        CardRouting.Commit(eligible.GroupBy(CardOrder.Equivalence), clicked.ToHashSet(), state.Selection, state.Seed,
            id => RandomAddress.For("deck routing", operation, id), operation != "removal",
            Key, c => CardOccurrences.Info(c).Address, CardOccurrences.Reassociate);
    }

    internal static PotionModel? Sacrifice(IEnumerable<PotionModel> inventory)
    {
        var potions = inventory.ToArray();
        if (potions.Length <= 1)
            return potions.FirstOrDefault();
        var state = RunCheckpoints.Require((RunState)potions[0].Owner.RunState);
        var scope = RandomAddress.For("potion sacrifice");
        var types = potions.GroupBy(p => p.Id.Entry).Select(g => new
        {
            Key = g.Key,
            Copies = g.ToArray(),
            Clock = state.Selection.Get(state.Seed, scope, g.Key)
        }).OrderBy(g => g.Clock.Remaining / g.Copies.Length).ThenBy(g => g.Key, StringComparer.Ordinal).ToArray();
        var winner = types[0];
        var minimum = winner.Clock.Remaining / winner.Copies.Length;
        foreach (var other in types.Skip(1))
            other.Clock.Remaining = Math.Max(0, other.Clock.Remaining - other.Copies.Length * minimum);
        var copy = AddressRandom.IntInclusive(state.Seed,
            RandomAddress.For("potion sacrifice copy", winner.Key).At(winner.Clock.Renewals), 0, winner.Copies.Length - 1);
        state.Selection.Renew(state.Seed, scope, winner.Key);
        return winner.Copies[copy];
    }
}
