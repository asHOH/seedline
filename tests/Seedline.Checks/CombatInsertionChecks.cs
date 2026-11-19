using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCombatInsertion()
    {
        var run = StartGoldRun();
        var player = run.Players[0];
        var combat = CombatFixture.CreateActiveCombat(run);
        var scope = CombatStateScope.Require(combat);
        var draw = player.PlayerCombatState!.DrawPile;
        var discard = player.PlayerCombatState.DiscardPile;
        var shuffle = run.Rng.Shuffle.ToSerializable();
        foreach (var card in draw.Cards.ToArray())
            card.RemoveFromCurrentPile();
        async Task Insert<T>(CardPile pile, string group) where T : CardModel
        {
            var address = scope.For("insertion", group, pile.Type.ToString());
            var position = scope.Random.Position(address);
            var expected = (int)(AddressRandom.Unit(AddressRandom.UInt64(scope.Seed, address.At(position))) * (pile.Cards.Count + 1));
            var card = combat.CreateCard<T>(player);
            await CardPileCmd.AddGeneratedCardToCombat(card, pile.Type, player, CardPilePosition.Random);
            Assert(pile.Cards.IndexOf(card) == expected && scope.Random.Position(address) == position + 1,
                $"{group} insertion into {pile.Type} uses the next relative gap and commits one roll");
        }
        await Insert<Soul>(draw, "Soul");
        await Insert<Dazed>(draw, "Dazed");
        await Insert<Soul>(discard, "Soul");
        await Insert<Soul>(draw, "Soul");
        await Insert<Soot>(draw, "Biiig Hug");
        await Insert<Beckon>(draw, "Soul Fysh");
        await Insert<Wither>(draw, "Aeonglass");
        await Insert<Wither>(discard, "Aeonglass");
        await Insert<FranticEscape>(draw, "The Insatiable");
        await Insert<FranticEscape>(discard, "The Insatiable");
        Assert(scope.Random.Position(scope.For("insertion", "Soul", "Draw")) == 2 &&
            scope.Random.Position(scope.For("insertion", "Soul", "Discard")) == 1,
            "insertion groups and destination piles advance independently, including an empty-pile insertion");
        var soulAddress = scope.For("insertion", "Soul", "Draw");
        var removed = combat.CreateCard<Soul>(player);
        removed.RemoveFromState();
        var failed = await CardPileCmd.Add(removed, draw, CardPilePosition.Random);
        Assert(!failed.success && scope.Random.Position(soulAddress) == 2,
            "rejected insertions preserve random state through vanilla lifecycle guards");
        await CardPileCmd.Add(combat.CreateCard<Soul>(player), draw, CardPilePosition.Top);
        Assert(scope.Random.Position(soulAddress) == 2, "fixed top insertion preserves random gap state");
        var before = draw.Cards.ToArray();
        var historyCount = CombatManager.Instance.History.Entries.Count();
        var metamorphosis = combat.CreateCard<Metamorphosis>(player);
        await (Task)AccessTools.Method(typeof(Metamorphosis), "OnPlay").Invoke(metamorphosis,
            [new ThrowingPlayerChoiceContext(), new CardPlay
            {
                Card = metamorphosis, Player = player, Target = null, ResultPile = PileType.Exhaust,
                Resources = new() { EnergySpent = 2, EnergyValue = 2, StarsSpent = 0, StarValue = 0 },
                IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
            }])!;
        var generated = draw.Cards.Except(before).ToArray();
        Assert(generated.Length == 3 && generated.All(c => c.Type == CardType.Attack && c.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0) &&
            scope.Random.Position(scope.For("insertion", "Metamorphosis", "Draw")) == 3,
            "Metamorphosis's actual play shares its insertion group and retains generated free attacks");
        var expectedOrder = before.ToList();
        var insertionAddress = RandomAddress.For("insertion", "Metamorphosis", "Draw").With(scope.Address.Key);
        var ordinal = 0;
        foreach (var entry in CombatManager.Instance.History.Entries.Skip(historyCount).OfType<CardGeneratedEntry>())
        {
            var u = AddressRandom.Unit(AddressRandom.UInt64(scope.Seed, insertionAddress.At(ordinal++)));
            expectedOrder.Insert((int)Math.Floor(u * (expectedOrder.Count + 1)), entry.Card);
        }
        Assert(ordinal == 3 && draw.Cards.SequenceEqual(expectedOrder),
            "Metamorphosis assigns exact gaps in generation order using each insertion's current pile size");
        Assert(shuffle.Equals(run.Rng.Shuffle.ToSerializable()), "covered random insertions preserve vanilla shuffle RNG");

        var batch = new[] { combat.CreateCard<Soul>(player), combat.CreateCard<Soul>(player), combat.CreateCard<Soul>(player) };
        batch[1].RemoveFromState();
        expectedOrder = draw.Cards.ToList();
        var soulOrdinal = 2;
        foreach (var card in new[] { batch[0], batch[2] })
        {
            var u = AddressRandom.Unit(AddressRandom.UInt64(scope.Seed, soulAddress.At(soulOrdinal++)));
            expectedOrder.Insert((int)Math.Floor(u * (expectedOrder.Count + 1)), card);
        }
        var results = await CardPileCmd.Add(batch, draw, CardPilePosition.Random);
        Assert(results.Select(r => r.success).SequenceEqual(new[] { true, false, true }) &&
            draw.Cards.SequenceEqual(expectedOrder) && scope.Random.Position(soulAddress) == 4,
            "a mixed insertion batch skips a rejected card without a roll and commits successful cards in input order");
        await CardPileCmd.Add(Array.Empty<CardModel>(), draw, CardPilePosition.Random);
        var bottom = combat.CreateCard<Soul>(player);
        await CardPileCmd.Add(bottom, draw, CardPilePosition.Bottom);
        Assert(draw.Cards.Last() == bottom && scope.Random.Position(soulAddress) == 4,
            "empty batches and fixed bottom insertion leave random gap state unchanged");
        Assert(shuffle.Equals(run.Rng.Shuffle.ToSerializable()),
            "successful, rejected and fixed insertions leave vanilla Shuffle RNG untouched");

        var vanilla = new Rng(run.Rng.Shuffle.ToSerializable());
        var fallbackGap = vanilla.NextInt(draw.Cards.Count + 1);
        var fallback = combat.CreateCard<Anger>(player);
        await CardPileCmd.AddGeneratedCardToCombat(fallback, PileType.Draw, player, CardPilePosition.Random);
        Assert(draw.Cards.IndexOf(fallback) == fallbackGap && vanilla.ToSerializable().Equals(run.Rng.Shuffle.ToSerializable()) &&
            scope.Random.Position(insertionAddress) == 3,
            "uncovered insertion retains the vanilla gap and RNG advancement after Metamorphosis returns");
        CombatFixture.SetInProgress(false);
        var stopped = combat.CreateCard<Soul>(player);
        await CardPileCmd.Add(stopped, draw, CardPilePosition.Random);
        Assert(stopped.Pile == null && scope.Random.Position(soulAddress) == 4,
            "inactive combat leaves insertion state unchanged");
        RunManager.Instance.CleanUp();
        await CheckInsertionSources();
    }
}
