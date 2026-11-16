using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace SameSeed;

internal static class CardGeneration
{
    internal static CardModel Draw(Player player, RandomAddress supply, IEnumerable<CardModel> pool, IEnumerable<CardModel> eligible)
    {
        var state = RunCheckpoints.Require((RunState)player.RunState);
        var identities = pool.ToArray();
        var entry = state.Rewards.Draw(state.Seed, supply, identities.Select(c => c.Id.Entry), eligible.Select(c => c.Id.Entry));
        return Create(player, identities.First(c => c.Id.Entry == entry.Identity), entry.Address);
    }

    internal static CardModel Create(Player player, CardModel identity, string address)
    {
        var card = player.RunState.CreateCard(identity, player);
        CardOccurrences.Create(card, address);
        return card;
    }

    internal static void Upgrade(Player player, CardModel card, decimal baseChance = 0)
    {
        var state = RunCheckpoints.Require((RunState)player.RunState);
        var entry = CardOccurrences.Info(card);
        var roll = AddressRandom.Unit(AddressRandom.UInt64(state.Seed, RandomAddress.For("upgrade").With(entry.Address)));
        if (!card.IsUpgradable)
            return;
        if (card.Rarity != CardRarity.Rare)
            baseChance += player.RunState.CurrentActIndex * (player.RunState.AscensionLevel >= 7 ? 0.125m : 0.25m);
        if ((decimal)roll <= Hook.ModifyCardRewardUpgradeOdds(player.RunState, player, card, baseChance))
            CardCmd.Upgrade(card);
    }
}
