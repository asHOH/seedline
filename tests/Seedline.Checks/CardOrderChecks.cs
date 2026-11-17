using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace Seedline.Checks;

public static partial class Checks
{
    private static void CheckCardOrdering(Player player)
    {
        Assert(CardOrder.Permanent.Compare(ModelDb.Card<Backflip>(), ModelDb.Card<DodgeAndRoll>()) > 0 &&
            CardOrder.Permanent.Compare(ModelDb.Card<DarkEmbrace>(), ModelDb.Card<Envenom>()) > 0,
            "immediate and power-mediated draw effects rank before alphabetical tie-breaks");

        var strikes = player.Deck.Cards.Where(c => c.Id.Entry == "STRIKE_IRONCLAD").Take(2).ToArray();
        Assert(CardOrder.Equivalence(strikes[0]) == CardOrder.Equivalence(strikes[1]) &&
            CardOrder.Permanent.Compare(strikes[0], strikes[1]) != 0 && CardOrder.Identity.Compare(strikes[0], strikes[1]) == 0,
            "equivalence and identity ordering ignore the occurrence tie-break used for permanent processing");

        var first = player.RunState.CreateCard<Bash>(player);
        var second = player.RunState.CreateCard<Bash>(player);
        var frozen = CardOrder.CapturePermanent(first);
        var unchanged = CardOrder.CapturePermanent(second);
        CardCmd.Upgrade(first);
        Assert(CardOrder.Equivalence(first) != CardOrder.Equivalence(second) && CardOrder.Permanent.Compare(first, second) > 0 &&
            CardOrder.Identity.Compare(first, second) == 0 && frozen.CompareTo(unchanged) == 0,
            "identity order uses base properties while captured permanent order survives later upgrades");
        CardCmd.Upgrade(second);
        CardCmd.Enchant<Swift>(first, 1);
        Assert(CardOrder.Equivalence(first) != CardOrder.Equivalence(second) && CardOrder.Identity.Compare(first, second) == 0,
            "permanent equivalence includes enchantment identity while identity order excludes it");
        CardCmd.Enchant<Swift>(second, 2);
        Assert(CardOrder.Equivalence(first) != CardOrder.Equivalence(second),
            "different enchantment amounts split permanent equivalence classes");

        var dowsing = player.RunState.CreateCard<Dowsing>(player);
        var freshDowsing = player.RunState.CreateCard<Dowsing>(player);
        dowsing.RoomsEntered = 3;
        Assert(CardOrder.Equivalence(dowsing) != CardOrder.Equivalence(freshDowsing) &&
            CardOrder.Permanent.Compare(dowsing, freshDowsing) == 0,
            "Dowsing progress splits routing equivalence without changing processing rank");
    }
}
