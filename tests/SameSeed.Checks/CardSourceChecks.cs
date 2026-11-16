using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace SameSeed.Checks;

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
        Assert(player.Deck.Cards.Count == initialCount + 1 && state.Random.Position(rareTriples) == 0,
            "Arcane Scroll direct addition keeps its existing path outside card-offer supplies");
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
        RunManager.Instance.CleanUp();
    }
}
