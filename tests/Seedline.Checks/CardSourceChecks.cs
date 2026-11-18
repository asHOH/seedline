using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCardRewardSources()
    {
        var run = StartGoldRun();
        var player = run.Players[0];
        run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, ModelDb.Event<Neow>().Id);
        var state = RunCheckpoints.Require(run);
        var selector = new TestCardSelector();
        using var selection = CardSelectorStack.SetTestSelector(selector);
        async Task Obtain(RelicModel relic)
        {
            player.AddRelicInternal(relic);
            await relic.AfterObtained();
        }

        var rareTriples = RandomAddress.For("card triple", player.Character.CardPool.Id.Entry, "Rare");
        var initialCount = player.Deck.Cards.Count;
        await Obtain(ModelDb.Relic<ArcaneScroll>().ToMutable());
        Assert(player.Deck.Cards.Count == initialCount + 1 && state.Random.Position(rareTriples) == 0 &&
            CardOccurrences.Info(player.Deck.Cards.Last()).Address.StartsWith("[\"permanent card identity\",", StringComparison.Ordinal),
            "Arcane Scroll uses the permanent-card supply outside card offers");
        selector.PrepareToSelect(new[] { 0 });
        await Obtain(ModelDb.Relic<HeftyTablet>().ToMutable());
        Assert(player.Deck.Cards.Count == initialCount + 3 && state.Random.Position(rareTriples) == 1,
            "Hefty Tablet source consumes one fixed-Rare offer and preserves its Injury grant");

        selector.PrepareToSelect(new[] { 0 });
        var before = player.Deck.Cards.ToArray();
        await Obtain(ModelDb.Relic<LeadPaperweight>().ToMutable());
        var paperweight = player.Deck.Cards.Except(before).Single();
        Assert(CardOccurrences.Info(paperweight).Address.StartsWith("[\"card identity\",\"colorless\",", StringComparison.Ordinal),
            "Lead Paperweight source uses colorless offer entries");

        var glass = (SeaGlass)ModelDb.Relic<SeaGlass>().ToMutable();
        glass.CharacterId = ModelDb.Character<Silent>().Id;
        selector.PrepareToSelect(new[] { 0, 5, 10 });
        before = player.Deck.Cards.ToArray();
        await Obtain(glass);
        var glassPicks = player.Deck.Cards.Except(before).ToArray();
        Assert(glassPicks.Length == 3 && glassPicks.Select(c => c.Rarity).Distinct().Count() == 3 &&
            glassPicks.Select(c => CardOffers.Info(c).Offer).Distinct().Count() == 1 &&
            glassPicks.Select(c => CardOccurrences.Info(c).Key).Distinct().Count() == 3,
            "Sea Glass sources all three rarity batches and assigns one offer's distinct acquisition keys");

        var previous = RewardsSet.testSelector;
        try
        {
            CardModel[] offered = [];
            RewardsSet.testSelector = set =>
            {
                offered = set.Rewards.OfType<CardReward>().SelectMany(r => r.Cards).ToArray();
                RunManager.Instance.RewardsSetSynchronizer.SkipLocalRewardsSet();
                return Task.CompletedTask;
            };
            await Obtain(ModelDb.Relic<Kaleidoscope>().ToMutable());
            Assert(offered.Length == 6 && offered.All(c => CardOccurrences.Info(c).Address
                .StartsWith("[\"card identity\",\"other character\",", StringComparison.Ordinal)),
                "Kaleidoscope single-card calls retain other-character offer supplies");
            before = player.Deck.Cards.ToArray();
            RewardsSet.testSelector = async set =>
            {
                foreach (var reward in set.Rewards.OfType<CardReward>().Take(2).ToArray())
                {
                    selector.PrepareToSelectCardRewardAtIndex(0);
                    await RunManager.Instance.RewardsSetSynchronizer.SelectLocalReward(reward);
                }
                RunManager.Instance.RewardsSetSynchronizer.SkipLocalRewardsSet();
            };
            await RelicCmd.Obtain<Orrery>(player);
            var claims = player.Deck.Cards.Except(before).Select(c => CardOccurrences.Info(c).Acquisition).ToArray();
            Assert(claims.Length == 2 && claims[0] < claims[1], "separate Orrery claims have separate acquisition ordinals");
        }
        finally { RewardsSet.testSelector = previous; }

        foreach (var (model, method, picks, size) in new (EventModel, string, int[], int)[]
        {
            (ModelDb.Event<BrainLeech>(), "ShareKnowledge", [0], 5),
            (ModelDb.Event<RoomFullOfCheese>(), "Gorge", [0, 1], 8)
        })
        {
            var eventModel = model.ToMutable();
            run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, eventModel.Id);
            await eventModel.BeginEvent(player, null, false);
            selector.PrepareToSelect(picks);
            before = player.Deck.Cards.ToArray();
            await (Task)AccessTools.Method(eventModel.GetType(), method).Invoke(eventModel, [])!;
            var acquired = player.Deck.Cards.Except(before).ToArray();
            Assert(acquired.Length == picks.Length && acquired.All(c => CardOffers.Find(c) is { KeyAssigned: true }) &&
                run.CurrentMapPointHistoryEntry!.GetEntry(player.NetId).CardChoices.Count == size - picks.Length,
                $"{eventModel.Id.Entry}: source interception preserves offer size and committed grid picks");
        }
        var originals = player.Deck.Cards.Where(c => c.IsTransformable && c.IsUpgradable).Take(2).ToArray();
        CardCmd.Upgrade(originals[0]);
        selector.PrepareToSelect(originals);
        var claws = await RelicCmd.Obtain<Claws>(player);
        var mauls = player.Deck.Cards.OfType<Maul>().OrderBy(c => c.IsUpgraded).ToArray();
        var grant = RandomAddress.For("card occurrence", claws.Id.Entry).At(0);
        Assert(mauls.Length == 2 && !mauls[0].IsUpgraded && mauls[1].IsUpgraded &&
            mauls.Select(c => CardOccurrences.Info(c).Address).SequenceEqual([grant.At(0).Key, grant.At(1).Key]),
            "Claws indexes its fixed replacements by their inherited status even when the upgraded original is first");
        var swoops = new[] { run.CreateCard<ByrdSwoop>(player), run.CreateCard<ByrdSwoop>(player) };
        CardCmd.Upgrade(swoops[0]);
        await CardPileCmd.Add(swoops, PileType.Deck);
        var byrdine = await RelicCmd.Obtain<Byrdine>(player);
        grant = RandomAddress.For("card occurrence", byrdine.Id.Entry).At(0);
        Assert(player.Deck.Cards.OfType<GodByrd>().OrderBy(c => c.IsUpgraded).Select(c => CardOccurrences.Info(c).Address)
            .SequenceEqual([grant.At(0).Key, grant.At(1).Key]), "Byrdine ranks sequential replacements without reordering deck effects");
        RunManager.Instance.CleanUp();
    }
}
