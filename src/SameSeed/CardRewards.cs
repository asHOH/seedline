using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SameSeed;

[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForReward), [typeof(Player), typeof(int), typeof(CardCreationOptions)])]
internal static class CardRewards
{
    internal static bool Candy { get; private set; }
    internal static Rng EventRng(Player player) => player.RunState.CurrentRoom is EventRoom room
        ? room.LocalMutableEvent.Rng : player.PlayerRng.NonCombatGeneration;

    private static bool Prefix(Player player, int cardCount, CardCreationOptions options, ref IEnumerable<CardCreationResult> __result)
    {
        var originalPools = options.CardPools.ToArray();
        var own = originalPools.Length == 1 && originalPools[0] == player.Character.CardPool;
        var other = originalPools.Length == 1 && !own && originalPools[0] is not ColorlessCardPool;
        // Single-card permanent grants belong to the transformation/addition policy in stage 5.
        if (cardCount < 2 && !Candy && !other)
            return true;
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
        var groupPool = Candy ? options.CardPools.SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(c => c.Type == CardType.Power && c.IsAllowedInRun(player.RunState)) : pool;
        var group = string.Join("/", groupPool.Select(c => c.Pool.Id.Entry).Distinct().Order(StringComparer.Ordinal));
        var rarities = pool.Select(c => c.Rarity).Distinct().ToArray();
        var types = pool.Select(c => c.Type).Distinct().ToArray();
        var typed = Candy || (own && types.Length == 1 && options.CardPoolFilter != null);
        var fixedRarity = options.RarityOdds == CardRarityOddsType.BossEncounter ? CardRarity.Rare :
            !Candy && rarities.Length == 1 ? rarities[0] : (CardRarity?)null;
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
            var address = RandomAddress.For("card triple", group, category);
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
                Add(eligible[AddressRandom.IntInclusive(state.Seed, RandomAddress.For("card identity").With(entryAddress.Key), 0, eligible.Length - 1)], entryAddress.Key, slot >= 3);
                if (randomTriple && rareSlots.Count > 0 && slot == 0)
                    CardOffers.Info(cards[^1].Card).RareOrder = 0;
            }
            for (var slot = 3; slot < cardCount; slot++)
            {
                var rng = options.RngOverride ?? EventRng(player);
                var rarity = fixedRarity ?? (rng.NextFloat() < UncommonChance(scarcity, rareSlots.Count > 0, -1, slot) ? CardRarity.Uncommon : CardRarity.Common);
                var eligible = pool.Where(c => c.Rarity == rarity && cards.All(r => r.Card.Id != c.Id)).ToArray();
                Add(eligible[rng.NextInt(eligible.Length)], RandomAddress.For("supplementary card").With(offer).At(slot).Key, true);
            }
            for (var extra = 1; extra < rareSlots.Count; extra++)
            {
                var index = cards.FindIndex(c => c.Card.Rarity == CardRarity.Common);
                if (index < 0)
                    index = cards.FindIndex(c => c.Card.Rarity == CardRarity.Uncommon);
                var displaced = CardOffers.Info(cards[index].Card);
                var entry = Draw(CardRarity.Rare, "extra Rare", pool.Where(c => c.Rarity == CardRarity.Rare));
                var card = Create(ModelDb.AllCards.Single(c => c.Id.Entry == entry.Identity), entry);
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
                        state.Random.NextUnit(RandomAddress.For("card rarity", Candy ? "Lasting Candy" : "colorless", Candy ? options.RarityOdds.ToString() : "shared"));
                    var rare = Candy ? options.RarityOdds == CardRarityOddsType.EliteEncounter ? (scarcity ? 0.05 : 0.10) : (scarcity ? 0.0149 : 0.03) : other ? (scarcity ? 0.0149 : 0.03) : 0.0149;
                    rarity = roll < rare ? CardRarity.Rare : roll < rare + 0.37 ? CardRarity.Uncommon : CardRarity.Common;
                    if (!Candy && !other)
                        rarity = roll < rare ? CardRarity.Rare : CardRarity.Uncommon;
                    for (var attempt = 0; attempt < 3 && !pool.Any(c => c.Rarity == rarity); attempt++)
                        rarity = rarity == CardRarity.Common ? CardRarity.Uncommon : rarity == CardRarity.Uncommon ? CardRarity.Rare : CardRarity.Common;
                }
                SupplyEntry entry;
                if (typed && types[0] != CardType.Power)
                {
                    var address = RandomAddress.For("event card", "THE_FUTURE_OF_POTIONS", rarity.ToString()!, types[0].ToString());
                    var eligible = pool.Where(c => c.Rarity == rarity && cards.All(r => r.Card.Id != c.Id)).ToArray();
                    var index = state.Random.Advance(address);
                    entry = new(eligible[AddressRandom.IntInclusive(state.Seed, address.At(index), 0, eligible.Length - 1)].Id.Entry, address.At(index).Key);
                }
                else
                {
                    var full = options.CardPools.SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
                        .Where(c => c.Rarity == rarity && c.IsAllowedInRun(player.RunState) && (!typed || c.Type == CardType.Power));
                    entry = Draw(rarity.Value, typed ? "Power" : other ? "other character" : "colorless", full);
                }
                if (slot == 0)
                    offer = entry.Address;
                Add(ModelDb.AllCards.Single(c => c.Id.Entry == entry.Identity), entry.Address, false);
            }
        }
        foreach (var result in cards)
            if (!options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll))
                ShopRewards.Upgrade(player, result.Card);
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
        __result = cards;
        return false;

        CardModel Create(CardModel model, SupplyEntry entry)
        {
            var card = player.RunState.CreateCard(model, player);
            RewardEntries.Attach(card, entry);
            return card;
        }
        void Add(CardModel model, string address, bool supplementary)
        {
            var card = Create(model, new(model.Id.Entry, address));
            CardOffers.Attach(card, offer, cards.Count, supplementary);
            cards.Add(new(card));
        }
        SupplyEntry Draw(CardRarity rarity, string kind, IEnumerable<CardModel> full) => state.Rewards.Draw(state.Seed,
            RandomAddress.For("card identity", kind, group, rarity.ToString()), full.Select(c => c.Id.Entry),
            pool.Where(c => c.Rarity == rarity && cards.All(r => r.Card.Id != c.Id)).Select(c => c.Id.Entry));
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

    [HarmonyPatch(typeof(LastingCandy), nameof(LastingCandy.TryModifyCardRewardOptions))]
    private static class CandyScope
    {
        private static void Prefix(out bool __state) { __state = Candy; Candy = true; }
        private static void Postfix(List<CardCreationResult> rewardOptions, bool __result)
        {
            if (!__result || rewardOptions.Count < 2 || CardOffers.Find(rewardOptions[0].Card)?.Offer is not { } offer)
                return;
            var card = rewardOptions[^1].Card;
            CardOffers.Attach(card, offer, rewardOptions.Count - 1);
            CardOffers.Info(card).Candy = true;
        }
        private static void Finalizer(bool __state) => Candy = __state;
    }
}
