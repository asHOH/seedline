using System.Text;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckNonCombatPolicies()
    {
        var run = StartGoldRun();
        var player = run.Players[0];
        var snapshot = RunManager.Instance.ToSave(null);
        var bytes = RunCheckpoints.Encode(snapshot);
        async Task Reset()
        {
            RunManager.Instance.CleanUp();
            var save = RunCheckpoints.Decode(Encoding.UTF8.GetString(bytes), snapshot.SchemaVersion);
            run = RunState.FromSerializable(save);
            await RunManager.Instance.SetUpSavedSingleplayer(run, save);
            LocalContext.NetId = RunManager.Instance.NetService.NetId;
            player = run.Players[0];
        }
        async Task<EventModel> Begin(EventModel canonical)
        {
            var source = canonical.ToMutable();
            run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, source.Id);
            await source.BeginEvent(player, null, false);
            return source;
        }
        static string Supplies(CheckpointState state) => JsonSerializer.Serialize(state.Rewards);

        var candidates = player.Deck.Cards.Take(3).Append(player.Deck.Cards.OfType<Bash>().Single()).ToArray();
        var ordered = PermanentGeneration.Prepare(candidates.Select(c => new CardTransformation(c)));
        var expected = ordered.Select(t => (t.Original.Id, t.Replacement!.Id, CardOccurrences.Info(t.Replacement).Address)).ToArray();
        var expectedSupply = Supplies(RunCheckpoints.Require(run));
        await Reset();
        candidates = player.Deck.Cards.Take(3).Append(player.Deck.Cards.OfType<Bash>().Single()).Reverse().ToArray();
        var reversed = PermanentGeneration.Prepare(candidates.Select(c => new CardTransformation(c))).Reverse().ToArray();
        Assert(expected.SequenceEqual(reversed.Select(t => (t.Original.Id, t.Replacement!.Id, CardOccurrences.Info(t.Replacement).Address))) &&
            expectedSupply == Supplies(RunCheckpoints.Require(run)), "transformation batches preserve original-to-result assignment and supply state across click orders");
        Assert(reversed.All(t => t.Replacement!.Id != t.Original.Id), "permanent transformation supply excludes each original identity");

        foreach (var (canonical, method) in new (EventModel, string)[]
        {
            (ModelDb.Event<MorphicGrove>(), "Group"), (ModelDb.Event<Trial>(), "NondescriptInnocent")
        })
        {
            async Task<string> TransformSource(bool reverse)
            {
                await Reset();
                var source = await Begin(canonical);
                var selector = new TestCardSelector();
                using var selection = CardSelectorStack.SetTestSelector(selector);
                CardModel[] chosen = [player.Deck.Cards.OfType<StrikeIronclad>().First(), player.Deck.Cards.OfType<Bash>().Single()];
                if (reverse)
                    Array.Reverse(chosen);
                selector.PrepareToSelect(chosen);
                var before = player.Deck.Cards.ToArray();
                await (Task)AccessTools.Method(source.GetType(), method).Invoke(source, [])!;
                var replacements = player.Deck.Cards.Except(before).Where(c => c is not Doubt).ToArray();
                Assert(replacements.Length == 2 && (canonical is not Trial || player.Deck.Cards.Any(c => c is Doubt)) &&
                    (canonical is not MorphicGrove || player.Gold == 0), $"{source.Id.Entry}: transformation preserves the source cost and reward count");
                return string.Join("|", chosen.Zip(replacements).OrderBy(p => p.First.Id.Entry, StringComparer.Ordinal)
                    .Select(p => $"{p.First.Id.Entry}:{p.Second.Id.Entry}:{CardOccurrences.Info(p.Second).Address}")) + Supplies(RunCheckpoints.Require(run));
            }
            Assert(await TransformSource(false) == await TransformSource(true), $"{canonical.Id.Entry}: actual sequential source transforms its selected batch independently of click order");
        }

        await Reset();
        var cursePool = ModelDb.CardPool<CurseCardPool>();
        var curse = run.CreateCard<Injury>(player);
        await CardPileCmd.Add(curse, PileType.Deck, skipVisuals: true);
        var transformed = (await CardCmd.TransformToRandom(curse, player.PlayerRng.Transformations, CardPreviewStyle.None)).cardAdded;
        Assert(transformed.Pool == cursePool && transformed.Id != curse.Id &&
            CardOccurrences.Info(transformed).Address.StartsWith(RandomAddress.For("permanent card identity", cursePool.Id.Entry).Key[..^1], StringComparison.Ordinal),
            "curse transformations use the shared curse supply with distinct replacement occurrences");

        foreach (var relic in new RelicModel[] { ModelDb.Relic<PandorasBox>(), ModelDb.Relic<Astrolabe>(), ModelDb.Relic<DistinguishedCape>() })
        {
            await Reset();
            var selector = new TestCardSelector();
            using var selection = CardSelectorStack.SetTestSelector(selector);
            if (relic is Astrolabe)
                selector.PrepareToSelect(new[] { player.Deck.Cards[0], player.Deck.Cards[5], player.Deck.Cards.OfType<Bash>().Single() });
            var original = player.Deck.Cards.ToArray();
            var mutable = relic.ToMutable();
            player.AddRelicInternal(mutable);
            await mutable.AfterObtained();
            var generated = player.Deck.Cards.Except(original).ToArray();
            var supplied = generated.Where(c => CardOccurrences.Info(c).Address.StartsWith("[\"permanent card identity\",", StringComparison.Ordinal)).ToArray();
            Assert(supplied.Length == (relic is PandorasBox ? 9 : relic is Astrolabe ? 3 : 2) &&
                (relic is not Astrolabe || supplied.All(c => c.IsUpgraded)) &&
                (relic is not DistinguishedCape || supplied.Select(c => c.Id).Distinct().Count() == 2),
                $"{relic.Id.Entry}: source uses shared permanent supplies while retaining upgrades and curse duplicate rules");
        }

        await Reset();
        var state = RunCheckpoints.Require(run);
        var own = player.Character.CardPool;
        var power = PermanentGeneration.Generate(player, 1, CardCreationOptions.ForNonCombatWithDefaultOdds([own], c => c.Type == CardType.Power), "direct addition").Single().Card;
        Assert(power.Type == CardType.Power && power.Rarity is CardRarity.Uncommon or CardRarity.Rare,
            "direct Power additions apply unavailable-rarity fallback before identity filtering");
        var eel = PermanentGeneration.Generate(player, 1, CardCreationOptions.ForNonCombatWithDefaultOdds([ModelDb.CardPool<ColorlessCardPool>()]), "Fried Eel").Single().Card;
        Assert(eel.Pool is ColorlessCardPool && state.Random.Position(RandomAddress.For("card rarity", "Fried Eel")) == 1 && state.Rewards.Rare.Luck == 0,
            "Fried Eel uses its own rarity sequence and colorless supply without combat luck");

        foreach (var (canonical, method) in new (EventModel, string)[]
        {
            (ModelDb.Event<InfestedAutomaton>(), "Study"), (ModelDb.Event<InfestedAutomaton>(), "TouchCore"),
            (ModelDb.Event<EndlessConveyor>(), "FriedEel"), (ModelDb.Event<MuseumOfTorture>(), "TakeRelic"),
            (ModelDb.Event<WaterfallOfObviousSecrets>(), "Negotiate")
        })
        {
            await Reset();
            var source = await Begin(canonical);
            var before = player.Deck.Cards.ToArray();
            await (Task)AccessTools.Method(source.GetType(), method).Invoke(source, [])!;
            Assert(player.Deck.Cards.Except(before).Any(c => CardOccurrences.Info(c).Address.StartsWith("[\"permanent card identity\",", StringComparison.Ordinal)),
                $"{source.Id.Entry}/{method}: direct-addition interception carries its shared entry into the deck");
        }

        await Reset();
        state = RunCheckpoints.Require(run);
        var odds = run.Odds.UnknownMapPoint;
        for (var i = 0; i < 15; i++)
        {
            var expectedRoom = RoomSelection.Resolve(AddressRandom.Unit(AddressRandom.UInt64(state.Seed, RandomAddress.For("room type").At(0).At(i))),
                odds.MonsterOdds, odds.TreasureOdds, odds.ShopOdds);
            Assert(odds.Roll([], run) == expectedRoom, $"unknown room {i}: independent ordinal roll and preserved chance updates");
        }
        Assert(RoomSelection.Resolve(0.01, .7, .14, .21) == RoomType.Treasure && RoomSelection.Resolve(.5, .7, .14, .21) == RoomType.Monster &&
            RoomSelection.Resolve(.9, .7, .14, .21) == RoomType.Shop, "overflow widths retain normal/chest/shop allocation with centered fight placement");
        var compass = (GoldenCompass)ModelDb.Relic<GoldenCompass>().ToMutable();
        player.AddRelicInternal(compass);
        compass.GoldenPathAct = 0;
        var beforeForced = (odds.MonsterOdds, odds.TreasureOdds, odds.ShopOdds);
        Assert(odds.Roll([], run) == RoomType.Event && state.Random.Position(RandomAddress.For("room type").At(0)) == 15 &&
            beforeForced == (odds.MonsterOdds, odds.TreasureOdds, odds.ShopOdds), "Golden Compass freezes room ordinal and stored chances");

        await Reset();
        for (var i = 0; i < 12; i++)
        {
            var selected = run.Act.PullNextEvent(run);
            Assert(selected.IsAllowed(run) && run.VisitedEventIds.Contains(selected.Id), $"ordinary event {i}: eligibility and visit commitment");
        }
        var eventSnapshot = RunManager.Instance.ToSave(null);
        var eventBytes = RunCheckpoints.Encode(eventSnapshot);
        var next = run.Act.PullNextEvent(run).Id;
        var eventState = JsonSerializer.Serialize(RunCheckpoints.Require(run).Events);
        RunManager.Instance.CleanUp();
        var restored = RunCheckpoints.Decode(Encoding.UTF8.GetString(eventBytes), eventSnapshot.SchemaVersion);
        run = RunState.FromSerializable(restored);
        await RunManager.Instance.SetUpSavedSingleplayer(run, restored);
        player = run.Players[0];
        Assert(run.Act.PullNextEvent(run).Id == next && JsonSerializer.Serialize(RunCheckpoints.Require(run).Events) == eventState,
            "ordinary event checkpoint replays counts, ID positions, condition rolls and priorities");
        Assert(eventBytes.SequenceEqual(RunCheckpoints.Encode(eventSnapshot)), "event checkpoints remain isolated from later selection");
        run.CurrentActIndex = 1;
        var act2 = run.Act.PullNextEvent(run);
        Assert(act2.IsAllowed(run) && RunCheckpoints.Require(run).Events.Act == 1 && RunCheckpoints.Require(run).Events.Ordinal == 1,
            "act transition rebuilds available event queues and resets the ordinary-event ordinal");
        run.CurrentActIndex = 2;
        await CardPileCmd.Add(run.CreateCard<LanternKey>(player), PileType.Deck, skipVisuals: true);
        var priorEvents = JsonSerializer.Serialize(RunCheckpoints.Require(run).Events);
        Assert(run.Act.PullNextEvent(run) is WarHistorianRepy && priorEvents == JsonSerializer.Serialize(RunCheckpoints.Require(run).Events) &&
            run.Odds.UnknownMapPoint.Roll([], run) == RoomType.Event && RunCheckpoints.Require(run).Random.Position(RandomAddress.For("room type").At(2)) == 0,
            "Lantern Key records Repy while bypassing ordinary-event selection and room-type advancement");

        await Reset();
        foreach (var canonical in new AncientEventModel[] { ModelDb.AncientEvent<Orobas>(), ModelDb.AncientEvent<Pael>(), ModelDb.AncientEvent<Tezcatara>(), ModelDb.AncientEvent<Tanx>(), ModelDb.AncientEvent<Nonupeipe>() })
        {
            var source = (AncientEventModel)await Begin(canonical);
            var menu = (IReadOnlyList<EventOption>)AccessTools.Method(source.GetType(), "GenerateInitialOptions").Invoke(source, [])!;
            Assert(menu.Count == 3 && menu.Select(o => o.TextKey).Distinct().Count() == 3, $"{source.Id.Entry}: targeted Ancient pools retain three distinct menu choices");
            if (source is Tanx or Nonupeipe)
                Assert(menu.SequenceEqual(EventChoices.Order(source, menu, "main")), $"{source.Id.Entry}: menu follows fixed priority order");
        }

        // The first two Common entries coincide for both Attack and Skill on this seed.
        RunManager.Instance.CleanUp();
        run = StartGoldRun(seed: "POTIONCHECK126");
        player = run.Players[0];
        player.AddRelicInternal(ModelDb.Relic<Driftwood>().ToMutable());
        player.AddToMaxPotionCount(2);
        foreach (var canonical in new PotionModel[] { ModelDb.Potion<AttackPotion>(), ModelDb.Potion<SkillPotion>(), ModelDb.Potion<AttackPotion>() })
            await PotionCmd.TryToProcure(canonical.ToMutable(), player);
        await PotionCmd.TryToProcure(ModelDb.Potion<PowerPotion>().ToMutable(), player);
        var future = (TheFutureOfPotions)await Begin(ModelDb.Event<TheFutureOfPotions>());
        var types = EventChoices.AssignTypes(future);
        Assert(types.Count == 3 && types.Values.All(t => t is CardType.Attack or CardType.Skill) &&
            types.SequenceEqual(EventChoices.AssignTypes(future)), "Future of Potions caches three slot-preserving assignments with Common reward types");
        var traded = player.Potions.First();
        var tradeCounter = PotionEffects.Counter(traded.Id.Entry);
        var beforeTrade = RunCheckpoints.Require(run).Random.Position(tradeCounter);
        var type = types[traded];
        var pool = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(c => c.IsAllowedInRun(run) && c.Rarity == CardRarity.Common && c.Type == type).ToList();
        var eventSeed = run.Rng.Seed + StringHelper.GetDeterministicHashCode(future.Id.Entry);
        var partition = RandomAddress.For("event card", future.Id.Entry, CardRarity.Common.ToString(), type.ToString());
        var expectedCards = new List<(ModelId Id, string Address)>();
        for (var i = 0; expectedCards.Count < 3; i++)
        {
            var address = partition.At(i).Key;
            var pick = new Rng(eventSeed, address).NextItem(pool)!;
            if (expectedCards.All(c => c.Id != pick.Id))
                expectedCards.Add((pick.Id, address));
        }
        var previousSelector = RewardsSet.testSelector;
        (ModelId Id, string Address)[] offeredCards = [];
        (ModelId Id, string Address) rerolledFirst = default;
        try
        {
            RewardsSet.testSelector = set =>
            {
                var reward = set.Rewards.OfType<CardReward>().Single();
                offeredCards = reward.Cards.Select(c => (c.Id, CardOccurrences.Info(c).Address)).ToArray();
                Assert(reward.CanReroll, "Driftwood enables the Future of Potions reward reroll");
                reward.Reroll();
                var first = reward.Cards.First();
                rerolledFirst = (first.Id, CardOccurrences.Info(first).Address);
                RunManager.Instance.RewardsSetSynchronizer.SkipLocalRewardsSet();
                return Task.CompletedTask;
            };
            await future.CurrentOptions[0].Chosen();
        }
        finally { RewardsSet.testSelector = previousSelector; }
        Assert(offeredCards.SequenceEqual(expectedCards),
            "Future of Potions offers the first distinct event-seeded identities with their original entry addresses");
        Assert(expectedCards[1].Address != partition.At(1).Key && rerolledFirst == (expectedCards[0].Id, partition.At(1).Key),
            "reroll recovers the skipped duplicate at its original event-seeded address");
        Assert(!player.Potions.Contains(traded) && RunCheckpoints.Require(run).Random.Position(tradeCounter) == beforeTrade + 1,
            "a committed Future of Potions trade removes the potion and advances its type's effect ordinal once");

        await Reset();
        var ranwid = await Begin(ModelDb.Event<RanwidTheElder>());
        var locked = (IReadOnlyList<EventOption>)AccessTools.Method(typeof(RanwidTheElder), "GenerateInitialOptions").Invoke(ranwid, [])!;
        Assert(locked.Any(o => o.TextKey.EndsWith("RELIC_LOCKED", StringComparison.Ordinal) && o.IsLocked),
            "Ranwid fallback opens its locked relic option with no tradable relics");

        await Reset();
        foreach (var canonical in RelicRewards.Pool(player).Where(r => r.IsTradable).Take(7))
            player.AddRelicInternal(canonical.ToMutable());
        var trader = (RelicTrader)await Begin(ModelDb.Event<RelicTrader>());
        var outgoing = EventChoices.Outgoing(trader);
        var replacements = (IReadOnlyList<RelicModel>)AccessTools.Property(typeof(RelicTrader), "NewRelics").GetValue(trader)!;
        Assert(outgoing.Length == 3 && outgoing.Distinct().Count() == 3 && replacements.Count == 3 &&
            replacements.All(r => RunCheckpoints.Require(run).Rewards.RetiredRelics.Contains(r.Id.Entry)), "Relic Trader assigns three owned relics and consumes all replacement offers");
        var outgoingPairs = outgoing.Select(r => r.Id).ToHashSet();
        await RelicCmd.Remove(outgoing[0]);
        Assert(EventChoices.Outgoing(trader).Count(r => outgoingPairs.Contains(r.Id)) >= 2, "Relic Trader retains two outgoing identities after one owned relic disappears");

        await Reset();
        var conveyor = await Begin(ModelDb.Event<EndlessConveyor>());
        var position = RunCheckpoints.Require(run).Random.Position(RandomAddress.For("dish selection"));
        var previous = (string)AccessTools.Field(typeof(EndlessConveyor), "_lastDishId").GetValue(conveyor)!;
        AccessTools.Method(typeof(EndlessConveyor), "RollDish").Invoke(conveyor, []);
        Assert((string)AccessTools.Field(typeof(EndlessConveyor), "_lastDishId").GetValue(conveyor)! != previous &&
            RunCheckpoints.Require(run).Random.Position(RandomAddress.For("dish selection")) > position, "Conveyor skips ineligible and immediately repeated dishes in its shared sequence");
        AccessTools.Field(typeof(EndlessConveyor), "_numOfGrabs").SetValue(conveyor, 4);
        position = RunCheckpoints.Require(run).Random.Position(RandomAddress.For("dish selection"));
        AccessTools.Method(typeof(EndlessConveyor), "RollDish").Invoke(conveyor, []);
        Assert((string)AccessTools.Field(typeof(EndlessConveyor), "_lastDishId").GetValue(conveyor)! == "SEAPUNK_SALAD" &&
            RunCheckpoints.Require(run).Random.Position(RandomAddress.For("dish selection")) == position, "fixed Conveyor salad consumes no random dish entry");
        RunManager.Instance.CleanUp();
    }
}
