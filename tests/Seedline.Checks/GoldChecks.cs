using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;

namespace Seedline.Checks;

public static partial class Checks
{
    private static readonly RandomAddress normalGold = RandomAddress.For("gold reward", "normal");
    private static readonly RandomAddress eliteGold = RandomAddress.For("gold reward", "elite");

    private static async Task CheckGameplayRewards()
    {
        var wasTestMode = TestMode.IsOn;
        var localId = LocalContext.NetId;
        var uiNotifications = new Harmony("Seedline.Checks.GoldUiNotifications");
        // RitsuLib's live UI subscribers require the normal notification path during backend tests.
        uiNotifications.Patch(AccessTools.Method(typeof(CombatStateTracker), "NotifyCombatStateChanged"),
            prefix: new HarmonyMethod(typeof(Checks), nameof(BeginGoldUiNotification)),
            finalizer: new HarmonyMethod(typeof(Checks), nameof(EndGoldUiNotification)));
        LocalContext.NetId = 1;
        try
        {
            await CheckRunContent();
            TestMode.IsOn = true;
            foreach (var ascension in new[] { 0, 7 })
                await CheckGoldGeneration(ascension);
            await CheckGoldRouteOrder();
            await CheckGoldReload(ModelDb.Encounter<AxebotsNormal>(), null);
            await CheckGoldReload(ModelDb.Encounter<BygoneEffigyElite>(), null);
            await CheckGoldReload(ModelDb.Encounter<DenseVegetationEventEncounter>(), ModelDb.Event<DenseVegetation>());
            await CheckDummyReload(false);
            await CheckDummyReload(true);
            await CheckRewardSupplies();
            await CheckPotionRewardPolicies();
            await CheckShopPolicies();
            await CheckCardRewards();
            await CheckPermanentSelection();
            await CheckNonCombatPolicies();
            await CheckCombatStateFoundation();
            await CheckCombatOrdering();
            await CheckCombatEnvironment();
            await CheckCombatGeneration();
            await CheckCombatInsertion();
            await CheckCombatOrbs();
            await CheckEnemyAi();
            await CheckCombatCosts();
            await CheckEnemyTargets();
            await CheckCombatShuffle();
            await CheckCombatSelection();
            await CheckPotionEffects();
            await CheckOilCosts();
            await CheckCombatRouting();
            await CheckCombatSerialization();
        }
        finally
        {
            RewardsSet.testSelector = null;
            TestMode.IsOn = wasTestMode;
            RunManager.Instance.CleanUp();
            uiNotifications.UnpatchAll(uiNotifications.Id);
            LocalContext.NetId = localId;
        }
    }

    private static void BeginGoldUiNotification(out bool __state)
    {
        __state = TestMode.IsOn;
        TestMode.IsOn = false;
    }

    private static void EndGoldUiNotification(bool __state) => TestMode.IsOn = __state;

    private static RunState StartGoldRun(int ascension = 7, GameMode mode = GameMode.Standard, string seed = "GOLDCHECK")
    {
        var run = RunState.CreateForNewRun([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
            ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), [], mode, false, ascension, seed);
        RunManager.Instance.SetUpNewSingleplayer(run, true);
        LocalContext.NetId = RunManager.Instance.NetService.NetId;
        return run;
    }

    private static GoldReward PrepareGold(RunState run, EncounterModel encounter) =>
        new RewardsSet(run.Players[0]).WithRewardsFromRoom(new CombatRoom(encounter.ToMutable(), run))
            .Rewards.OfType<GoldReward>().Single();

    private static async Task CheckGoldGeneration(int ascension)
    {
        var run = StartGoldRun(ascension);
        var player = run.Players[0];
        var random = RunCheckpoints.Require(run).Random;
        foreach (var (encounter, address, min, max) in new (EncounterModel, RandomAddress, int, int)[]
        {
            (ModelDb.Encounter<AxebotsNormal>(), normalGold, ascension == 0 ? 10 : 7, ascension == 0 ? 20 : 15),
            (ModelDb.Encounter<BygoneEffigyElite>(), eliteGold, ascension == 0 ? 35 : 26, ascension == 0 ? 45 : 33)
        })
        {
            var reward = PrepareGold(run, encounter);
            Assert(random.Position(address) == 0 && !reward.IsPopulated, $"A{ascension}: constructing {address.Key} leaves the opportunity unconsumed");
            var vanilla = player.PlayerRng.Rewards.ToSerializable();
            reward.Populate();
            Assert(reward.Amount == AddressRandom.IntInclusive("GOLDCHECK", address.At(0), min, max) &&
                random.Position(address) == 1 && player.PlayerRng.Rewards.ToSerializable().Equals(vanilla),
                $"A{ascension}: {address.Key} uses the adjusted inclusive range and leaves vanilla RNG unchanged");
            reward.Populate();
            reward.OnSkipped();
            Assert(random.Position(address) == 1, $"A{ascension}: repeat population and skipping consume no additional gold opportunity");
        }

        var previousAct = run.Act;
        run.SetActDebug(ModelDb.Act<MegaCrit.Sts2.Core.Models.Acts.Underdocks>().ToMutable());
        var merc = new CombatRoom(ModelDb.Encounter<GremlinMercNormal>().ToMutable(), run);
        var fat = merc.CombatState.CreateCreature(ModelDb.Monster<FatGremlin>().ToMutable(), CombatSide.Enemy, "fat");
        run.SetActDebug(previousAct);
        merc.CombatState.AddCreature(fat);
        merc.CombatState.CreatureEscaped(fat);
        merc.OnCombatEnded();
        var half = new RewardsSet(player).WithRewardsFromRoom(merc).Rewards.OfType<GoldReward>().Single();
        half.Populate();
        Assert(merc.GoldProportion == 0.5f && half.Amount == AddressRandom.IntInclusive("GOLDCHECK", normalGold.At(1),
                ascension == 0 ? 5 : 4, ascension == 0 ? 10 : 8) && random.Position(normalGold) == 2,
            $"A{ascension}: Gremlin Merc half gold preserves vanilla endpoint rounding and advances normal gold");
        ((GremlinMercNormal)merc.Encounter).MarkGoldStolen();
        merc.OnCombatEnded();
        Assert(merc.GoldProportion == 0 && !new RewardsSet(player).WithRewardsFromRoom(merc).Rewards.OfType<GoldReward>().Any() &&
            random.Position(normalGold) == 2, $"A{ascension}: escaped stolen gold suppresses the ordinary reward and counter");

        var fixedRewards = new[]
        {
            PrepareGold(run, ModelDb.Encounter<FakeMerchantEventEncounter>()),
            PrepareGold(run, ModelDb.Encounter<CeremonialBeastBoss>()),
            new GoldReward(19, player, wasGoldStolenBack: true),
            new GoldReward(17, player)
        };
        foreach (var reward in fixedRewards)
            reward.Populate();
        Assert(fixedRewards.Select(r => r.Amount).SequenceEqual(new[] { 300, ascension == 0 ? 100 : 75, 19, 17 }) &&
            random.Position(normalGold) == 2 && random.Position(eliteGold) == 1,
            $"A{ascension}: Fake Merchant, boss, recovered gold and fixed grants preserve amounts and mod counters");
        var unrelated = new GoldReward(10, 20, player);
        var unrelatedRng = new MegaCrit.Sts2.Core.Random.Rng(player.PlayerRng.Rewards.ToSerializable());
        unrelated.Populate();
        Assert(unrelated.Amount == unrelatedRng.NextInt(10, 21) && random.Position(normalGold) == 2,
            $"A{ascension}: custom ranged gold keeps vanilla generation");

        player.AddRelicInternal(ModelDb.Relic<BowlerHat>().ToMutable());
        run.AppendToMapPointHistory(MapPointType.Monster, RoomType.Monster, ModelDb.Encounter<AxebotsNormal>().Id);
        var set = await RewardsCmd.GenerateForRoomEnd(player, new CombatRoom(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run));
        var gold = set.Rewards.OfType<GoldReward>().Single();
        var before = player.Gold;
        RewardsSet.testSelector = async offered =>
        {
            await RunManager.Instance.RewardsSetSynchronizer.SelectLocalReward(offered.Rewards.OfType<GoldReward>().Single());
            RunManager.Instance.RewardsSetSynchronizer.SkipLocalRewardsSet();
        };
        await set.Offer();
        await set.GenerateWithoutOffering();
        Assert(player.Gold == before + (int)(gold.Amount * 1.25m) && gold.SuccessfullySelected && random.Position(normalGold) == 3,
            $"A{ascension}: real reward selection applies Bowler Hat and truncation once without re-consuming generation");
        run.CurrentActIndex = run.Acts.Count - 1;
        var finalBoss = await RewardsCmd.GenerateForRoomEnd(player, new CombatRoom(ModelDb.Encounter<CeremonialBeastBoss>().ToMutable(), run));
        Assert(finalBoss.Rewards.Count == 0 && random.Position(normalGold) == 3 && random.Position(eliteGold) == 1,
            $"A{ascension}: final boss omits ordinary rewards and leaves gold counters unchanged");
        RunManager.Instance.CleanUp();
    }

    private static async Task CheckGoldRouteOrder()
    {
        var routes = new[] { new[] { false, true, false, true }, new[] { true, true, false, false } };
        var amounts = new List<(bool Elite, int Gold)>[2];
        for (var route = 0; route < routes.Length; route++)
        {
            var run = StartGoldRun(mode: route == 0 ? GameMode.Standard : GameMode.Dread);
            var player = run.Players[0];
            player.AddRelicInternal(ModelDb.Relic<BowlerHat>().ToMutable());
            amounts[route] = [];
            foreach (var elite in routes[route])
            {
                run.ActFloor += route + 1;
                player.PlayerRng.Rewards.NextInt();
                var reward = PrepareGold(run, elite ? ModelDb.Encounter<BygoneEffigyElite>() : ModelDb.Encounter<AxebotsNormal>());
                reward.Populate();
                var before = player.Gold;
                await reward.SelectUnsynchronized();
                amounts[route].Add((elite, player.Gold - before));
                run.CurrentActIndex = 1;
            }
            RunManager.Instance.CleanUp();
        }
        Assert(amounts[0].Where(x => !x.Elite).SequenceEqual(amounts[1].Where(x => !x.Elite)) &&
            amounts[0].Where(x => x.Elite).SequenceEqual(amounts[1].Where(x => x.Elite)) &&
            amounts[0].Sum(x => x.Gold) == amounts[1].Sum(x => x.Gold),
            "reordered routes, floors, acts and Dread preserve per-category payouts and Bowler Hat totals");
    }

    private static async Task CheckGoldReload(EncounterModel encounter, EventModel? parent)
    {
        var run = StartGoldRun();
        var manager = RunManager.Instance;
        var address = encounter.RoomType == RoomType.Elite ? eliteGold : normalGold;
        var mapType = parent != null ? MapPointType.Unknown : encounter.RoomType == RoomType.Elite ? MapPointType.Elite : MapPointType.Monster;
        var room = new CombatRoom(encounter.ToMutable(), run)
        {
            ParentEventId = parent?.Id,
            ShouldResumeParentEventAfterCombat = false
        };
        room.MarkPreFinished();
        run.AppendToMapPointHistory(mapType, room.RoomType, room.ModelId);
        await SaveManager.Instance.SaveRun(room, saveProgress: false);
        var checkpoint = SaveManager.Instance.LoadRunSave().SaveData!;
        var generated = await RewardsCmd.GenerateForRoomEnd(run.Players[0], room);
        var expected = generated.Rewards.OfType<GoldReward>().Single().Amount;
        var before = run.Players[0].Gold;
        await generated.Rewards.OfType<GoldReward>().Single().SelectUnsynchronized();
        Assert(run.Players[0].Gold == before + expected && RunCheckpoints.Require(checkpoint).Random.Position(address) == 0,
            $"{encounter.Id}: victory checkpoint freezes pre-generation gold and player balance");
        manager.CleanUp();

        for (var reload = 0; reload < 2; reload++)
        {
            var save = SaveManager.Instance.LoadRunSave().SaveData!;
            run = RunState.FromSerializable(save);
            await manager.SetUpSavedSingleplayer(run, save);
            LocalContext.NetId = manager.NetService.NetId;
            var restoredRoom = CombatRoom.FromSerializable(save.PreFinishedRoom!, run);
            var offered = new TaskCompletionSource();
            RewardsSet.testSelector = async set =>
            {
                try
                {
                    var gold = set.Rewards.OfType<GoldReward>().Single();
                    Assert(gold.Amount == expected && RunCheckpoints.Require(run).Random.Position(address) == 1 &&
                        run.Players[0].Gold == before, $"{encounter.Id}: reload {reload} regenerates the same gold once before collection");
                    if (reload == 1)
                        await manager.RewardsSetSynchronizer.SelectLocalReward(gold);
                    manager.RewardsSetSynchronizer.SkipLocalRewardsSet();
                    offered.SetResult();
                }
                catch (Exception error) { offered.SetException(error); throw; }
            };
            await manager.EnterMapPointInternal(1, mapType, restoredRoom, false);
            await offered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert(run.CurrentRoom == restoredRoom && run.CurrentRoomCount == (parent == null ? 1 : 2) &&
                (parent == null || manager.EventSynchronizer.GetLocalEvent().Id == parent.Id),
                $"{encounter.Id}: load rebuilds the correct room stack and parent event");
            Assert(run.Players[0].Gold == before + (reload == 1 ? expected : 0),
                $"{encounter.Id}: collection or skipping preserves the regenerated offer's balance effect");
            await manager.ProceedFromTerminalRewardsScreenAsync();
            Assert(run.CurrentRoom == restoredRoom, $"{encounter.Id}: terminal rewards preserve the no-resume event flag");
            if (reload == 1)
            {
                await manager.EnterMapPointInternal(2, MapPointType.RestSite, null, true);
                var next = SaveManager.Instance.LoadRunSave().SaveData!;
                Assert(RunCheckpoints.Require(next).Random.Position(address) == 1 && next.Players[0].Gold == before + expected,
                    $"{encounter.Id}: next-room checkpoint commits gold consumption and collected balance");
                manager.CleanUp();
                run = RunState.FromSerializable(next);
                await manager.SetUpSavedSingleplayer(run, next);
                LocalContext.NetId = manager.NetService.NetId;
                var following = PrepareGold(run, encounter);
                following.Populate();
                Assert(following.Amount == AddressRandom.IntInclusive("GOLDCHECK", address.At(1), encounter.MinGoldReward, encounter.MaxGoldReward) &&
                    RunCheckpoints.Require(run).Random.Position(address) == 2,
                    $"{encounter.Id}: continuation consumes the following gold opportunity");
            }
            manager.CleanUp();
        }
    }

    private static async Task CheckDummyReload(bool ranOutOfTime)
    {
        var run = StartGoldRun();
        var manager = RunManager.Instance;
        var encounter = (BattlewornDummyEventV2Encounter)ModelDb.Encounter<BattlewornDummyEventV2Encounter>().ToMutable();
        encounter.RanOutOfTime = ranOutOfTime;
        var room = new CombatRoom(encounter, run) { ParentEventId = ModelDb.Event<BattlewornDummy>().Id };
        room.MarkPreFinished();
        run.AppendToMapPointHistory(MapPointType.Unknown, room.RoomType, room.ModelId);
        await SaveManager.Instance.SaveRun(room, saveProgress: false);
        var save = SaveManager.Instance.LoadRunSave().SaveData!;
        manager.CleanUp();
        run = RunState.FromSerializable(save);
        await manager.SetUpSavedSingleplayer(run, save);
        LocalContext.NetId = manager.NetService.NetId;
        var restoredRoom = CombatRoom.FromSerializable(save.PreFinishedRoom!, run);
        RewardsSet.testSelector = _ => throw new Exception("Battleworn Dummy offered ordinary combat rewards.");
        await manager.EnterMapPointInternal(1, MapPointType.Unknown, restoredRoom, false);
        Assert(run.CurrentRoomCount == 2 && ((BattlewornDummyEventV2Encounter)restoredRoom.Encounter).RanOutOfTime == ranOutOfTime &&
            RunCheckpoints.Require(run).Random.Position(normalGold) == 0,
            $"Battleworn Dummy ({ranOutOfTime}): reconstruction restores encounter state and suppresses ordinary gold");
        var upgrades = run.Players[0].Deck.Cards.Sum(c => c.CurrentUpgradeLevel);
        await manager.ProceedFromTerminalRewardsScreenAsync();
        Assert(run.CurrentRoom is EventRoom && run.CurrentRoomCount == 1 && manager.EventSynchronizer.GetLocalEvent().IsFinished &&
            run.Players[0].Deck.Cards.Sum(c => c.CurrentUpgradeLevel) == upgrades + (ranOutOfTime ? 0 : 2) &&
            RunCheckpoints.Require(run).Random.Position(normalGold) == 0,
            $"Battleworn Dummy ({ranOutOfTime}): resumed parent resolves the saved outcome without consuming gold");
        manager.CleanUp();
    }
}
