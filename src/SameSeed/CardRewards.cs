using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SameSeed;

internal enum CardOfferKind { OwnCharacter, OtherCharacter, Colorless, LastingCandy }

internal static class CardRewards
{
    internal static Rng EventRng(Player player) => player.RunState.CurrentRoom is EventRoom room
        ? room.LocalMutableEvent.Rng : player.PlayerRng.NonCombatGeneration;

    internal static List<CardCreationResult> Generate(Player player, int cardCount, CardCreationOptions options, CardOfferKind kind)
    {
        var originalPools = options.CardPools.ToArray();
        var own = kind == CardOfferKind.OwnCharacter;
        var other = kind == CardOfferKind.OtherCharacter;
        var candy = kind == CardOfferKind.LastingCandy;
        var copy = new CardCreationOptions(originalPools, options.Source, options.RarityOdds, options.CardPoolFilter).WithFlags(options.Flags);
        if (options.RngOverride != null)
            copy.WithRngOverride(options.RngOverride);
        options = Hook.ModifyCardRewardCreationOptions(player.RunState, player, copy);
        var pool = options.GetPossibleCards(player).Where(c => c.IsAllowedInRun(player.RunState) &&
            c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare).Distinct().ToArray();
        if (pool.Length == 0)
            throw new InvalidOperationException("Empty card reward pool.");
        var state = RunCheckpoints.Require((RunState)player.RunState);
        var timing = state.Rewards.Rare;
        if (timing.Act != player.RunState.CurrentActIndex)
        {
            timing.Act = player.RunState.CurrentActIndex;
            timing.Offset = -0.05;
        }
        var scarcity = player.RunState.AscensionLevel >= 7;
        var addressPool = candy ? options.CardPools.SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(c => c.Type == CardType.Power && c.IsAllowedInRun(player.RunState)) : pool;
        var poolIds = string.Join("/", addressPool.Select(c => c.Pool.Id.Entry).Distinct().Order(StringComparer.Ordinal));
        var rarities = pool.Select(c => c.Rarity).Distinct().ToArray();
        var types = pool.Select(c => c.Type).Distinct().ToArray();
        var typed = candy || (own && types.Length == 1 && options.CardPoolFilter != null);
        var fixedRarity = options.RarityOdds == CardRarityOddsType.BossEncounter ? CardRarity.Rare :
            !candy && rarities.Length == 1 ? rarities[0] : (CardRarity?)null;
        var randomTriple = own && !typed && fixedRarity == null;
        var triple = own && !typed;
        var rareSlots = new List<int>();
        var cards = new List<CardCreationResult>();
        string offer;
        if (randomTriple)
        {
            var elite = options.RarityOdds == CardRarityOddsType.EliteEncounter;
            var combat = options.Source == CardCreationSource.Encounter || options.Flags.HasFlag(CardCreationFlags.ForceRarityOddsChange);
            rareSlots = timing.Simulate(state.Random, cardCount, elite ? (scarcity ? 0.05 : 0.10) : (scarcity ? 0.0149 : 0.03), combat, scarcity);
        }
        if (options.Source == CardCreationSource.Encounter && options.RarityOdds == CardRarityOddsType.BossEncounter)
            timing.Offset = -0.05;
        if (triple)
        {
            var category = fixedRarity?.ToString() ?? (rareSlots.Count > 0 ? "rare-bearing" : "non-Rare");
            var address = RandomAddress.For("card triple", poolIds, category);
            var ordinal = state.Random.Advance(address);
            offer = address.At(ordinal).Key;
            var opening = randomTriple && rareSlots.Count == 0 ? timing.NonRareTriples++ : -1;
            for (var slot = 0; slot < 3; slot++)
            {
                var entryAddress = address.At(ordinal).At(slot);
                var rarity = fixedRarity ?? (rareSlots.Count > 0 && slot == 0 ? CardRarity.Rare :
                    Unit(state, "card rarity", entryAddress.Key) < UncommonChance(scarcity, rareSlots.Count > 0, opening, slot)
                        ? CardRarity.Uncommon : CardRarity.Common);
                var eligible = pool.Where(c => c.Rarity == rarity && cards.All(r => r.Card.Id != c.Id)).OrderBy(c => c.Id.Entry, StringComparer.Ordinal).ToArray();
                if (eligible.Length == 0)
                    throw new InvalidOperationException("Triple has no distinct eligible card.");
                Add(CardGeneration.Create(player, eligible[AddressRandom.IntInclusive(state.Seed,
                    RandomAddress.For("card identity").With(entryAddress.Key), 0, eligible.Length - 1)], entryAddress.Key), false);
                if (randomTriple && rareSlots.Count > 0 && slot == 0)
                    CardOffers.Info(cards[^1].Card).RareOrder = 0;
            }
            for (var slot = 3; slot < cardCount; slot++)
            {
                var rng = options.RngOverride ?? EventRng(player);
                var rarity = fixedRarity ?? (rng.NextFloat() < UncommonChance(scarcity, rareSlots.Count > 0, -1, slot) ? CardRarity.Uncommon : CardRarity.Common);
                var eligible = pool.Where(c => c.Rarity == rarity && cards.All(r => r.Card.Id != c.Id)).ToArray();
                Add(CardGeneration.Create(player, eligible[rng.NextInt(eligible.Length)],
                    RandomAddress.For("supplementary card").With(offer).At(slot).Key), true);
            }
            for (var extra = 1; extra < rareSlots.Count; extra++)
            {
                var index = cards.FindIndex(c => c.Card.Rarity == CardRarity.Common);
                if (index < 0)
                    index = cards.FindIndex(c => c.Card.Rarity == CardRarity.Uncommon);
                var displaced = CardOffers.Info(cards[index].Card);
                var card = Draw(CardRarity.Rare, "extra Rare", pool.Where(c => c.Rarity == CardRarity.Rare));
                CardOffers.Attach(card, offer, displaced.EntryOrder, displaced.Supplementary, displaced.WingAddress);
                CardOffers.Info(card).RareOrder = extra;
                cards[index] = new(card);
            }
        }
        else
        {
            offer = "";
            for (var slot = 0; slot < cardCount; slot++)
            {
                var rarity = fixedRarity;
                if (rarity == null)
                {
                    var roll = other ? (options.RngOverride ?? EventRng(player)).NextFloat() :
                        state.Random.NextUnit(RandomAddress.For("card rarity", candy ? "Lasting Candy" : "colorless", candy ? options.RarityOdds.ToString() : "shared"));
                    var rare = candy ? options.RarityOdds == CardRarityOddsType.EliteEncounter ? (scarcity ? 0.05 : 0.10) : (scarcity ? 0.0149 : 0.03) : other ? (scarcity ? 0.0149 : 0.03) : 0.0149;
                    rarity = roll < rare ? CardRarity.Rare : roll < rare + 0.37 ? CardRarity.Uncommon : CardRarity.Common;
                    if (!candy && !other)
                        rarity = roll < rare ? CardRarity.Rare : CardRarity.Uncommon;
                    for (var attempt = 0; attempt < 3 && !pool.Any(c => c.Rarity == rarity); attempt++)
                        rarity = rarity == CardRarity.Common ? CardRarity.Uncommon : rarity == CardRarity.Uncommon ? CardRarity.Rare : CardRarity.Common;
                }
                CardModel card;
                if (typed && types[0] != CardType.Power)
                {
                    var address = RandomAddress.For("event card", "THE_FUTURE_OF_POTIONS", rarity.ToString()!, types[0].ToString());
                    var eligible = pool.Where(c => c.Rarity == rarity && cards.All(r => r.Card.Id != c.Id)).ToArray();
                    var index = state.Random.Advance(address);
                    card = CardGeneration.Create(player,
                        eligible[AddressRandom.IntInclusive(state.Seed, address.At(index), 0, eligible.Length - 1)], address.At(index).Key);
                }
                else
                {
                    var full = options.CardPools.SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
                        .Where(c => c.Rarity == rarity && c.IsAllowedInRun(player.RunState) && (!typed || c.Type == CardType.Power));
                    card = Draw(rarity.Value, typed ? "Power" : other ? "other character" : "colorless", full);
                }
                if (slot == 0)
                    offer = CardOccurrences.Info(card).Address;
                Add(card, false);
            }
        }
        foreach (var result in cards)
            if (!options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll))
                CardGeneration.Upgrade(player, result.Card);
        if (!options.Flags.HasFlag(CardCreationFlags.NoModifyHooks) && Hook.TryModifyCardRewardOptions(player.RunState, player, cards, options, out var modifiers))
            TaskHelper.RunSafely(Hook.AfterModifyingCardRewardOptions(player.RunState, modifiers));
        for (var i = 0; i < rareSlots.Count; i++)
        {
            var rare = cards.Single(c => CardOffers.Info(c.Card).RareOrder == i);
            var from = cards.IndexOf(rare);
            var to = rareSlots[i];
            (cards[from], cards[to]) = (cards[to], cards[from]);
        }
        if (cardCount > 3 && triple && rareSlots.Count == 0)
        {
            var rng = options.RngOverride ?? EventRng(player);
            for (var i = cards.Count - 1; i > 0; i--)
            {
                var j = rng.NextInt(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }
        CardOffers.NumberOffer(cards.Select(c => c.Card));
        return cards;

        void Add(CardModel card, bool supplementary)
        {
            CardOffers.Attach(card, offer, cards.Count, supplementary);
            cards.Add(new(card));
        }
        CardModel Draw(CardRarity rarity, string supply, IEnumerable<CardModel> full) => CardGeneration.Draw(player,
            RandomAddress.For("card identity", supply, poolIds, rarity.ToString()), full,
            pool.Where(c => c.Rarity == rarity && cards.All(r => r.Card.Id != c.Id)));
    }

    internal static double Unit(CheckpointState state, string purpose, string address) =>
        AddressRandom.Unit(AddressRandom.UInt64(state.Seed, RandomAddress.For(purpose).With(address)));

    internal static double UncommonChance(bool scarcity, bool rare, long opening, int slot)
    {
        if (opening is 0 or 1)
        {
            var offset = -0.05 + (opening * 3 + slot) * (scarcity ? 0.005 : 0.01);
            var threshold = (scarcity ? 0.0149 : 0.03) + offset;
            return (0.37 + Math.Min(0, threshold)) / (1 - Math.Max(0, threshold));
        }
        return scarcity ? rare ? 0.39 : 0.38 : rare ? 0.405 : 0.40;
    }

}
