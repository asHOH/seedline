using System.Text;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
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
        foreach (var (monster, chest, shop, chestEnd, fightStart, fightEnd, shopStart) in new[]
        {
            (.1, .02, .03, .02, 5d / 12, 31d / 60, .97), // Base chances, centered fight.
            (.7, .14, .21, .14, .14, .84, .84), // Overflow truncates shop, not chest or fight.
            (.2, .6, .1, .6, .6, .8, .9), // Chest shifts fight right.
            (.2, .1, .6, .1, .2, .4, .4), // Shop shifts fight left.
            (1.2, .2, .3, 0d, 0d, 1d, 1d), // Fight consumes the whole range.
            (0d, .02, .03, .02, 7d / 15, 7d / 15, .97) // Excluded fight leaves event space.
        })
        {
            Assert(Enumerable.Range(0, 6000).All(i =>
            {
                var roll = (i + .5) / 6000;
                var expected = roll < chestEnd ? RoomType.Treasure : roll >= shopStart ? RoomType.Shop :
                    roll >= fightStart && roll < fightEnd ? RoomType.Monster : RoomType.Event;
                return RoomSelection.Resolve(roll, monster, chest, shop) == expected;
            }), $"unknown intervals {monster}/{chest}/{shop}: explicit widths and placement");
        }

        void CheckUnknownRoll(bool fightsAllowed = true, bool shopsAllowed = true)
        {
            var address = RandomAddress.For("room type").At(run.CurrentActIndex);
            var position = state.Random.Position(address);
            var before = (odds.MonsterOdds, odds.EliteOdds, odds.TreasureOdds, odds.ShopOdds);
            var expected = RoomSelection.Resolve(AddressRandom.Unit(AddressRandom.UInt64(state.Seed, address.At(position))),
                fightsAllowed ? before.MonsterOdds : 0, before.TreasureOdds, shopsAllowed ? before.ShopOdds : 0);
            var actual = odds.Roll(shopsAllowed ? [] : [RoomType.Shop], run);
            Assert(actual == expected && state.Random.Position(address) == position + 1,
                $"unknown act {run.CurrentActIndex} roll {position}: addressed result and single advancement");
            Assert(odds.MonsterOdds == (!fightsAllowed ? before.MonsterOdds : actual == RoomType.Monster ? .1f : before.MonsterOdds + .1f) &&
                odds.EliteOdds == (fightsAllowed ? before.EliteOdds - 1 : before.EliteOdds) &&
                odds.TreasureOdds == (actual == RoomType.Treasure ? .02f : before.TreasureOdds + .02f) &&
                odds.ShopOdds == (!shopsAllowed ? before.ShopOdds : actual == RoomType.Shop ? .03f : before.ShopOdds + .03f),
                $"unknown act {run.CurrentActIndex} roll {position}: selected chance resets, allowed chances grow, excluded chances freeze");
        }
        for (var i = 0; i < 15; i++)
            CheckUnknownRoll();
        var compass = (GoldenCompass)ModelDb.Relic<GoldenCompass>().ToMutable();
        player.AddRelicInternal(compass);
        compass.GoldenPathAct = 0;
        var beforeForced = (odds.MonsterOdds, odds.EliteOdds, odds.TreasureOdds, odds.ShopOdds);
        Assert(odds.Roll([], run) == RoomType.Event && state.Random.Position(RandomAddress.For("room type").At(0)) == 15 &&
            beforeForced == (odds.MonsterOdds, odds.EliteOdds, odds.TreasureOdds, odds.ShopOdds), "Golden Compass freezes room ordinal and stored chances");
        compass.GoldenPathAct = -1;
        CheckUnknownRoll();

        var unknownSnapshot = RunManager.Instance.ToSave(null);
        var unknownBytes = RunCheckpoints.Encode(unknownSnapshot);
        var nextRooms = Enumerable.Range(0, 8).Select(_ => odds.Roll([], run)).ToArray();
        var nextOdds = (odds.MonsterOdds, odds.EliteOdds, odds.TreasureOdds, odds.ShopOdds);
        RunManager.Instance.CleanUp();
        var unknownSave = RunCheckpoints.Decode(Encoding.UTF8.GetString(unknownBytes), unknownSnapshot.SchemaVersion);
        run = RunState.FromSerializable(unknownSave);
        await RunManager.Instance.SetUpSavedSingleplayer(run, unknownSave);
        player = run.Players[0];
        state = RunCheckpoints.Require(run);
        odds = run.Odds.UnknownMapPoint;
        Assert(nextRooms.SequenceEqual(Enumerable.Range(0, 8).Select(_ => odds.Roll([], run))) &&
            nextOdds == (odds.MonsterOdds, odds.EliteOdds, odds.TreasureOdds, odds.ShopOdds) &&
            state.Random.Position(RandomAddress.For("room type").At(0)) == 24,
            "unknown checkpoint replays room types, stored chances and ordinal continuation");

        CheckUnknownRoll(shopsAllowed: false);
        var dowsing = run.CreateCard<Dowsing>(player);
        await CardPileCmd.Add(dowsing, PileType.Deck, skipVisuals: true);
        dowsing.RoomsEntered = 2;
        CheckUnknownRoll();
        dowsing.RoomsEntered = 3;
        CheckUnknownRoll(fightsAllowed: false, shopsAllowed: false);
        dowsing.RoomsEntered = 0;
        player.AddRelicInternal(ModelDb.Relic<JuzuBracelet>().ToMutable());
        CheckUnknownRoll(fightsAllowed: false);

        await RunManager.Instance.SetActInternal(1);
        Assert((odds.MonsterOdds, odds.EliteOdds, odds.TreasureOdds, odds.ShopOdds) == (.1f, -1f, .02f, .03f) &&
            state.Random.Position(RandomAddress.For("room type").At(1)) == 0 &&
            state.Random.Position(RandomAddress.For("room type").At(0)) == 28,
            "act entry resets unknown chances and starts a separate ordinal without advancing the previous act");
        CheckUnknownRoll(fightsAllowed: false);

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
        Assert(run.Act.PullNextEvent(run) is WarHistorianRepy && run.VisitedEventIds.Contains(ModelDb.Event<WarHistorianRepy>().Id) &&
            priorEvents == JsonSerializer.Serialize(RunCheckpoints.Require(run).Events) &&
            run.Odds.UnknownMapPoint.Roll([], run) == RoomType.Event && RunCheckpoints.Require(run).Random.Position(RandomAddress.For("room type").At(2)) == 0,
            "Lantern Key records Repy while bypassing ordinary-event selection and room-type advancement");

        foreach (var ancient in new AncientEventModel[] { ModelDb.AncientEvent<Tanx>(), ModelDb.AncientEvent<Nonupeipe>(), ModelDb.AncientEvent<Vakuu>() })
        {
            await Reset();
            run.Acts[2].GetRoomsDebug().Ancient = ancient;
            run.CurrentActIndex = 2;
            RoomSelection.EnterActs(run);
            var queued = RunCheckpoints.Require(run).Events.Conditions.Values.SelectMany(c => c.Order).ToHashSet(StringComparer.Ordinal);
            Assert(queued.Contains(ModelDb.Event<ShrineOfTheWarlord>().Id.Entry) == (ancient is not Tanx) &&
                queued.Contains(ModelDb.Event<ThermaeNonupeipe>().Id.Entry) == (ancient is not Nonupeipe) &&
                !queued.Contains(ModelDb.Event<WarHistorianRepy>().Id.Entry),
                $"{ancient.Id.Entry}: ordinary queues apply Ancient exclusions before selection and omit forced Repy");
        }

        await Reset();
        var fallback = run.Act.GetRoomsDebug().events.ToArray();
        foreach (var model in fallback)
            run.AddVisitedEvent(model);
        for (var i = 0; i <= fallback.Length; i++)
        {
            Assert(run.Act.PullNextEvent(run).Id == fallback[i % fallback.Length].Id &&
                RunCheckpoints.Require(run).Events.Ordinal == i + 1,
                $"exhausted ordinary events {i}: fallback follows ordinal indexing and wraps the original act list");
        }

        await Reset();
        foreach (var canonical in new AncientEventModel[] { ModelDb.AncientEvent<Orobas>(), ModelDb.AncientEvent<Pael>(), ModelDb.AncientEvent<Tezcatara>(), ModelDb.AncientEvent<Tanx>(), ModelDb.AncientEvent<Nonupeipe>() })
        {
            var source = (AncientEventModel)await Begin(canonical);
            var menu = source.CurrentOptions;
            Assert(menu.Count == 3 && menu.Select(o => o.TextKey).Distinct().Count() == 3, $"{source.Id.Entry}: targeted Ancient pools retain three distinct menu choices");
            if (source is Orobas or Pael or Tezcatara)
            {
                var expectedRng = new Rng(run.Rng.Seed, source.Id.Entry);
                // Orobas also rolls Sea Glass's character and the Gem/Glass branch before the three pool draws.
                for (var i = 0; i < (source is Orobas ? 5 : 3); i++)
                    expectedRng.NextInt(1);
                Assert(expectedRng.ToSerializable().Equals(source.Rng.ToSerializable()),
                    $"{source.Id.Entry}: displayed menu retains vanilla event RNG position");
            }
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
        var typeProperty = AccessTools.Property(typeof(TheFutureOfPotions), "PotionToCardType");
        var types = (Dictionary<PotionModel, CardType>)typeProperty.GetValue(future)!;
        Assert(types.Count == 3 && types.Keys.ToHashSet().SetEquals(player.Potions.Take(3)) &&
            types.Values.All(t => t is CardType.Attack or CardType.Skill) && ReferenceEquals(types, typeProperty.GetValue(future)),
            "Future of Potions caches assignments for the first three occupied slots with Common reward types");
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
        var locked = ranwid.CurrentOptions;
        Assert(locked.Any(o => o.TextKey.EndsWith("RELIC_LOCKED", StringComparison.Ordinal) && o.IsLocked),
            "Ranwid fallback opens its locked relic option with no tradable relics");

        foreach (var reverse in new[] { false, true })
        {
            await Reset();
            var relics = RelicRewards.Pool(player).Where(r => r.IsTradable).Take(4).ToArray();
            foreach (var relic in reverse ? relics.Reverse() : relics)
                player.AddRelicInternal(relic.ToMutable());
            var before = player.Relics.ToArray();
            var expectedSacrifice = before.Where(r => r.IsTradable)
                .OrderBy(r => AddressRandom.UInt64(run.Rng.StringSeed,
                    RandomAddress.For("relic sacrifice", ModelDb.Event<RanwidTheElder>().Id.Entry, r.Id.Entry)))
                .ThenBy(r => r.Id.Entry, StringComparer.Ordinal).First();
            var rarityAddress = RandomAddress.For("relic rarity", "generic");
            ranwid = await Begin(ModelDb.Event<RanwidTheElder>());
            var sacrifice = ranwid.CurrentOptions.Single(o => o.TextKey.EndsWith(".RELIC", StringComparison.Ordinal));
            Assert(!sacrifice.IsLocked && RunCheckpoints.Require(run).Random.Position(rarityAddress) == 0,
                $"Ranwid relic order {reverse}: nonempty sacrifice offer defers reward generation until choice");
            using var selection = CardSelectorStack.SetTestSelector(new TestCardSelector());
            await sacrifice.Chosen();
            Assert(!player.Relics.Contains(expectedSacrifice) && before.Where(r => r != expectedSacrifice).All(player.Relics.Contains) &&
                player.Relics.Except(before).Count() == 2 && RunCheckpoints.Require(run).Random.Position(rarityAddress) == 2 && ranwid.IsFinished,
                $"Ranwid relic order {reverse}: the displayed choice sacrifices the fixed-priority relic and grants two generic rewards");
        }

        await Reset();
        foreach (var canonical in RelicRewards.Pool(player).Where(r => r.IsTradable).Take(7))
            player.AddRelicInternal(canonical.ToMutable());
        RelicRewards.Initialize(player);
        var traderRewards = RunCheckpoints.Require(run).Rewards;
        var preview = traderRewards.Copy();
        var eligibleRelics = RelicRewards.Pool(player).Where(r => r.IsAllowed(run)).Select(r => r.Id.Entry).ToHashSet();
        var expectedReplacements = new List<SupplyEntry>();
        for (var slot = 0; slot < 3; slot++)
        {
            var roll = AddressRandom.Unit(AddressRandom.UInt64(run.Rng.StringSeed,
                RandomAddress.For("replacement rarity", ModelDb.Event<RelicTrader>().Id.Entry).At(slot)));
            var rarity = roll < .5 ? RelicRarity.Common : roll < .83 ? RelicRarity.Uncommon : RelicRarity.Rare;
            var supply = preview.Supply(RelicRewards.Address("Courier", rarity));
            var tail = supply.Positions.Last(p => p.Entry is { } entry && eligibleRelics.Contains(entry.Identity));
            expectedReplacements.Add(tail.Entry!);
            supply.Positions.Remove(tail);
        }
        var trader = (RelicTrader)await Begin(ModelDb.Event<RelicTrader>());
        var ownedProperty = AccessTools.Property(typeof(RelicTrader), "OwnedRelics");
        var newProperty = AccessTools.Property(typeof(RelicTrader), "NewRelics");
        var outgoing = (IReadOnlyList<RelicModel>)ownedProperty.GetValue(trader)!;
        var replacements = (IReadOnlyList<RelicModel>)newProperty.GetValue(trader)!;
        Assert(outgoing.Count == 3 && outgoing.Distinct().Count() == 3 && replacements.Count == 3 &&
            replacements.All(r => traderRewards.RetiredRelics.Contains(r.Id.Entry)), "Relic Trader assigns three owned relics and consumes all replacement offers");
        Assert(expectedReplacements.SequenceEqual(replacements.Select(RewardEntries.Find)),
            "Relic Trader replacement slots use addressed rarity rolls and Courier tails with original entry addresses");
        var outgoingPairs = outgoing.Select(r => r.Id).ToHashSet();
        var originals = player.Relics.ToArray();
        var retiredOffers = traderRewards.RetiredRelics.ToHashSet();
        using (CardSelectorStack.SetTestSelector(new TestCardSelector()))
            await trader.CurrentOptions[1].Chosen();
        Assert(!player.Relics.Contains(outgoing[1]) && originals.Where(r => r != outgoing[1]).All(player.Relics.Contains) &&
            player.Relics.Except(originals).Select(r => r.Id).SequenceEqual([replacements[1].Id]) && trader.IsFinished,
            "Relic Trader's displayed middle choice exchanges only its cached outgoing/replacement pair");
        Assert(ReferenceEquals(outgoing, ownedProperty.GetValue(trader)) && ReferenceEquals(replacements, newProperty.GetValue(trader)) &&
            retiredOffers.SetEquals(traderRewards.RetiredRelics), "Relic Trader keeps both offer caches and generates no extra replacement on choice");
        Assert(EventChoices.Outgoing(trader).Count(r => outgoingPairs.Contains(r.Id)) >= 2,
            "Relic Trader retains two outgoing identities when one owned relic is replaced");

        var dishAddress = RandomAddress.For("dish selection");
        var lastDish = AccessTools.Field(typeof(EndlessConveyor), "_lastDishId");
        var rollDish = AccessTools.Method(typeof(EndlessConveyor), "RollDish");
        var skippedDishes = new HashSet<string>();
        foreach (var fullHp in new[] { false, true })
            foreach (var fullPotions in new[] { false, true })
            {
                await Reset();
                player.Creature.SetCurrentHpInternal(fullHp ? player.Creature.MaxHp : player.Creature.MaxHp - 20);
                if (fullPotions)
                    while (player.HasOpenPotionSlots)
                        player.AddPotionInternal(ModelDb.Potion<BlockPotion>().ToMutable());
                var dishState = RunCheckpoints.Require(run);
                long expectedPosition = 0;
                var previousDish = "";
                var conveyor = await Begin(ModelDb.Event<EndlessConveyor>());
                var eventRng = conveyor.Rng.ToSerializable();
                for (var display = 1; display <= 20; display++)
                {
                    var expectedDish = "SEAPUNK_SALAD";
                    if (display % 5 != 0)
                        while (true)
                        {
                            var roll = AddressRandom.IntInclusive(dishState.Seed, dishAddress.At(expectedPosition++), 0, 24);
                            expectedDish = roll switch
                            {
                                < 6 => "CAVIAR",
                                < 9 => "SPICY_SNAPPY",
                                < 12 => "JELLY_LIVER",
                                < 15 => "FRIED_EEL",
                                < 18 => "SUSPICIOUS_CONDIMENT",
                                < 24 => "CLAM_ROLL",
                                _ => "GOLDEN_FYSH"
                            };
                            if (expectedDish != previousDish && !(fullPotions && expectedDish == "SUSPICIOUS_CONDIMENT") &&
                                !(fullHp && expectedDish == "CLAM_ROLL") && !(display == 1 && expectedDish == "GOLDEN_FYSH"))
                                break;
                            skippedDishes.Add(expectedDish);
                        }
                    if (display > 1)
                        rollDish.Invoke(conveyor, []);
                    Assert((string)lastDish.GetValue(conveyor)! == expectedDish && dishState.Random.Position(dishAddress) == expectedPosition,
                        $"Conveyor display {display}, full HP {fullHp}, full potions {fullPotions}: weighted sequence skips ineligible entries and pauses for Salad");
                    if (display == 1)
                        Assert(conveyor.CurrentOptions[0].TextKey.EndsWith("." + expectedDish, StringComparison.Ordinal),
                            "Conveyor's initial displayed option matches its selected dish");
                    previousDish = expectedDish;
                }
                Assert(eventRng.Equals(conveyor.Rng.ToSerializable()), "Conveyor dish rolls leave event-effect RNG unchanged");
            }
        Assert(skippedDishes.Contains("CLAM_ROLL") && skippedDishes.Contains("SUSPICIOUS_CONDIMENT"),
            "Conveyor sequence fixtures exercise HP and potion-slot exclusions");

        RunManager.Instance.CleanUp();
        run = StartGoldRun(seed: "6T09M6UV7U1C");
        player = run.Players[0];
        player.Creature.SetMaxHpInternal(95);
        player.Creature.SetCurrentHpInternal(43);
        await PlayerCmd.GainGold(206 - player.Gold, player);
        var purchasedConveyor = await Begin(ModelDb.Event<EndlessConveyor>());
        var conveyorRewardSelector = RewardsSet.testSelector;
        try
        {
            RewardsSet.testSelector = null;
            foreach (var dish in new[] { "SPICY_SNAPPY", "CAVIAR", "SUSPICIOUS_CONDIMENT", "FRIED_EEL", "SEAPUNK_SALAD" })
            {
                Assert(purchasedConveyor.CurrentOptions[0].TextKey.EndsWith("." + dish, StringComparison.Ordinal),
                    $"Conveyor displays {dish} before its purchase");
                await purchasedConveyor.CurrentOptions[0].Chosen();
            }
        }
        finally { RewardsSet.testSelector = conveyorRewardSelector; }
        Assert(player.Gold == 6 && purchasedConveyor.CurrentOptions[0].IsLocked &&
            (string)lastDish.GetValue(purchasedConveyor)! == "CLAM_ROLL" && RunCheckpoints.Require(run).Random.Position(dishAddress) == 5,
            "Conveyor purchases generate the unaffordable sixth dish while the fifth, fixed Salad consumes no sequence entry");
        RunManager.Instance.CleanUp();
    }
}
