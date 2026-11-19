using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static void CheckCardOfferPolicies()
    {
        CardModel[]? control = null;
        foreach (var multiple in new[] { false, true })
        {
            RunManager.Instance.CleanUp();
            var run = StartGoldRun(0);
            var player = run.Players[0];
            var state = RunCheckpoints.Require(run);
            var triples = RandomAddress.For("card triple", player.Character.CardPool.Id.Entry, "rare-bearing");
            var triple = Enumerable.Range(0, 1000).First(i => Enumerable.Range(1, 2).All(slot =>
                CardRewards.Unit(state, "card rarity", triples.At(i).At(slot).Key) < 0.405));
            for (var i = 0; i < triple; i++) state.Random.Advance(triples);
            var thresholds = RandomAddress.For("Rare threshold");
            var step = -Math.Log(0.97);
            double Threshold(int i) => -Math.Log(AddressRandom.OpenUnit(AddressRandom.UInt64(state.Seed, thresholds.At(i))));
            var renewal = Enumerable.Range(0, 10000).First(i => multiple
                ? Threshold(i) > step && Threshold(i) < 2 * step && Threshold(i + 1) > 2 * step
                : Threshold(i) > 4 * step);
            for (var i = 0; i < renewal; i++) state.Random.Advance(thresholds);
            state.Rewards.Rare.Threshold = 0.000001;
            state.Rewards.Rare.Offset = 0.25;

            var pool = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
                .Where(c => c.IsAllowedInRun(run)).ToArray();
            var commonCount = pool.Count(c => c.Rarity == CardRarity.Common);
            var eventSeed = Enumerable.Range(0, 1000).First(i =>
            {
                var probe = new Rng((ulong)i);
                if (probe.NextFloat() < 0.405) return false;
                probe.NextInt(commonCount);
                return probe.NextFloat() >= 0.405;
            });
            var options = CardCreationOptions.ForNonCombatWithDefaultOdds([player.Character.CardPool])
                .WithRngOverride(new Rng((ulong)eventSeed));
            var cards = CardRewards.Generate(player, 5, options, CardOfferKind.OwnCharacter).Select(c => c.Card).ToArray();
            Assert(state.Rewards.Rare.Offset == 0.25 && state.Random.Position(triples) == triple + 1 &&
                cards.Select(c => c.Id).Distinct().Count() == 5,
                "base-odds five-card offers preserve combat offset and consume one distinct rare-bearing triple");
            if (!multiple)
            {
                control = cards;
                Assert(cards.Select(c => c.Rarity).SequenceEqual([CardRarity.Rare, CardRarity.Uncommon, CardRarity.Uncommon, CardRarity.Common, CardRarity.Common]),
                    "extra-Rare fixture starts with R,U,U plus two supplementary Commons");
                continue;
            }
            var extra = cards.Single(c => CardOffers.Info(c).RareOrder == 1);
            var displaced = CardOffers.Info(control![3]);
            var info = CardOffers.Info(extra);
            var extraAddress = RandomAddress.For("card identity", "extra Rare", player.Character.CardPool.Id.Entry, "Rare");
            var rares = pool.Where(c => c.Rarity == CardRarity.Rare).Select(c => c.Id.Entry).ToArray();
            var expected = new RewardState().Draw(state.Seed, extraAddress, rares, rares.Where(id => id != control[0].Id.Entry));
            Assert(cards[2] == extra && cards.Count(c => c.Rarity == CardRarity.Rare) == 2 &&
                cards.Where(c => c != extra).Select(c => c.Id).OrderBy(id => id.Entry)
                    .SequenceEqual(control.Where((_, i) => i != 3).Select(c => c.Id).OrderBy(id => id.Entry)),
                "A0 extra Rare replaces the first supplementary Common before swapping into simulated slot three");
            Assert(RewardEntries.Find(extra) == expected && info.Supplementary && info.EntryOrder == displaced.EntryOrder &&
                info.WingAddress == displaced.WingAddress && info.Offer == displaced.Offer &&
                CardOccurrences.Info(extra).Address != CardOccurrences.Info(control[3]).Address,
                "extra Rare uses its new supply address while retaining the replaced slot's offer, membership and Wing Charm priority");
        }

        foreach (var modifier in new RelicModel?[] { null, ModelDb.Relic<DingyRug>(), ModelDb.Relic<PrismaticGem>() })
        {
            RunManager.Instance.CleanUp();
            var run = StartGoldRun();
            var player = run.Players[0];
            run.AppendToMapPointHistory(MapPointType.Monster, RoomType.Monster, null);
            if (modifier != null) player.AddRelicInternal(modifier.ToMutable());
            var state = RunCheckpoints.Require(run);
            state.Rewards.Rare.Threshold = 1000;
            var vanilla = player.PlayerRng.Rewards.ToSerializable();
            var options = CardCreationOptions.ForRoom(player, RoomType.Monster);
            var reward = new CardReward(options, 3, player);
            reward.Populate();
            var first = reward.Cards.Select(c => CardOccurrences.Info(c).Address).ToHashSet();
            var firstOffer = CardOffers.Info(reward.Cards.First()).Offer;
            reward.Reroll();
            Assert(reward.Cards.Count() == 3 && reward.Cards.All(c => !first.Contains(CardOccurrences.Info(c).Address)) &&
                reward.Cards.Select(c => CardOffers.Info(c).Offer).Distinct().Single() != firstOffer &&
                state.Rewards.Rare.NonRareTriples == 2 && options.CardPools.Count == 1 &&
                vanilla.Equals(player.PlayerRng.Rewards.ToSerializable()),
                $"{modifier?.Id.Entry ?? "unmodified"}: actual reroll consumes the next shared triple without mutating source options or vanilla reward RNG");
        }
        RunManager.Instance.CleanUp();
    }
}
