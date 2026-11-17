using System.Text;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckPermanentSelection()
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
            player = run.Players[0];
        }
        static string Clocks(CheckpointState state) => JsonSerializer.Serialize(state.Selection);
        async Task<(string Key, string Clocks)> PickUpgrade(int index)
        {
            await Reset();
            var selector = new TestCardSelector();
            using var selection = CardSelectorStack.SetTestSelector(selector);
            var strikes = player.Deck.Cards.OfType<StrikeIronclad>().ToArray();
            selector.PrepareToSelect(new[] { strikes[index] });
            var chosen = (await CardSelectCmd.FromDeckForUpgrade(player, new(CardSelectorPrefs.UpgradeSelectionPrompt, 1))).Single();
            Assert(ReferenceEquals(chosen, strikes[index]), "deck routing preserves the clicked physical copy");
            CardCmd.Upgrade(chosen);
            return (PermanentSelection.Key(chosen), Clocks(RunCheckpoints.Require(run)));
        }
        var first = await PickUpgrade(0);
        var other = await PickUpgrade(3);
        Assert(first == other, "equivalent upgrade clicks select the same logical occurrence and clock state");
        Assert(bytes.SequenceEqual(RunCheckpoints.Encode(snapshot)), "selection mutations leave captured checkpoints frozen");

        async Task<string> RemoveCopy(int index)
        {
            await Reset();
            var removalSelector = new TestCardSelector();
            using var selection = CardSelectorStack.SetTestSelector(removalSelector);
            removalSelector.PrepareToSelect(new[] { player.Deck.Cards.OfType<StrikeIronclad>().ElementAt(index) });
            var removed = (await CardSelectCmd.FromDeckForRemoval(player, new(CardSelectorPrefs.RemoveSelectionPrompt, 1))).Single();
            await CardPileCmd.RemoveFromDeck(removed);
            return string.Join("|", player.Deck.Cards.Select(PermanentSelection.Key).Order(StringComparer.Ordinal)) +
                Clocks(RunCheckpoints.Require(run));
        }
        Assert(await RemoveCopy(0) == await RemoveCopy(3), "equivalent removal clicks leave matching survivors and retirement clocks");

        await Reset();
        var state = RunCheckpoints.Require(run);
        var selector = new TestCardSelector();
        using (CardSelectorStack.SetTestSelector(selector))
        {
            var pending = selector.SetupForAsyncCardSelection();
            var task = CardSelectCmd.FromDeckForUpgrade(player, new(CardSelectorPrefs.UpgradeSelectionPrompt, 1));
            Assert(state.Selection.Clocks.Count == 0 && !task.IsCompleted, "opening a player choice leaves clocks untouched");
            pending.SetResult([]);
            await task;
            Assert(state.Selection.Clocks.Count == 0, "cancelled deck choices leave selection state untouched");
        }

        var strikes = player.Deck.Cards.OfType<StrikeIronclad>().ToArray();
        var scope = RandomAddress.For("deck routing", "enchant", strikes[0].Id.Entry);
        var expectedKeys = state.Selection.Copy().Select(state.Seed, scope, strikes.Select(PermanentSelection.Key), 2);
        using (CardSelectorStack.SetTestSelector(selector))
        {
            selector.PrepareToSelect(new[] { strikes[3], strikes[0] });
            var picks = (await CardSelectCmd.FromDeckForEnchantment(strikes, ModelDb.Enchantment<Swift>(), 2,
                new(CardSelectorPrefs.UpgradeSelectionPrompt, 2))).ToArray();
            Assert(picks.SequenceEqual(new[] { strikes[3], strikes[0] }) &&
                PermanentSelection.Key(strikes[0]) == expectedKeys[0] && PermanentSelection.Key(strikes[3]) == expectedKeys[1],
                "batch routing assigns by presentation order while preserving returned click order");
            foreach (var card in picks)
                CardCmd.Enchant<Swift>(card, 2);
        }
        Assert(expectedKeys.All(k => state.Selection.Get(state.Seed, scope, k).Renewals == 1),
            "selected enchantment routing clocks renew exactly once");

        var saveAfterRouting = RunManager.Instance.ToSave(null);
        var encoded = RunCheckpoints.Encode(saveAfterRouting);
        var expected = PermanentSelection.UpgradeBatch(player.Deck.Cards.Where(c => c.IsUpgradable), 2).Select(PermanentSelection.Key).ToArray();
        var afterRandom = Clocks(state);
        RunManager.Instance.CleanUp();
        var restoredSave = RunCheckpoints.Decode(Encoding.UTF8.GetString(encoded), saveAfterRouting.SchemaVersion);
        run = RunState.FromSerializable(restoredSave);
        await RunManager.Instance.SetUpSavedSingleplayer(run, restoredSave);
        player = run.Players[0];
        state = RunCheckpoints.Require(run);
        Assert(expected.SequenceEqual(PermanentSelection.UpgradeBatch(player.Deck.Cards.Where(c => c.IsUpgradable), 2).Select(PermanentSelection.Key)) &&
            afterRandom == Clocks(state), "checkpoint reload restores reassociation and subsequent random selection exactly");
        await Reset();
        state = RunCheckpoints.Require(run);
        var expectedWhetstone = state.Selection.Copy().Select(state.Seed, PermanentSelection.Upgrade,
            player.Deck.Cards.Where(c => c.Type == CardType.Attack && c.IsUpgradable).Select(PermanentSelection.Key), 2);
        var rngCounter = player.PlayerRng.NonCombatGeneration.ToSerializable().counter;
        var whetstone = ModelDb.Relic<Whetstone>().ToMutable();
        player.AddRelicInternal(whetstone);
        await whetstone.AfterObtained();
        Assert(player.Deck.Cards.Where(c => c.IsUpgraded).Select(PermanentSelection.Key).Order().SequenceEqual(expectedWhetstone.Order()) &&
            player.PlayerRng.NonCombatGeneration.ToSerializable().counter == rngCounter, "Whetstone uses persistent upgrade clocks and isolates the vanilla RNG");
        var upgraded = player.Deck.Cards.First(c => c.IsUpgraded);
        var refill = RandomAddress.For("upgrade refill");
        var eventRng = new Rng(13);
        var beforeRefill = state.Random.Position(refill);
        PermanentSelection.DowngradeOne(eventRng, [upgraded]);
        CardCmd.Downgrade(upgraded);
        Assert(state.Random.Position(refill) == beforeRefill + 1 && state.Selection.Get(state.Seed, PermanentSelection.Upgrade, PermanentSelection.Key(upgraded)).Remaining > 0 &&
            eventRng.ToSerializable().counter == 1, "random downgrade refills a consumed upgrade clock and renews downgrade with event RNG");
        var retained = state.Selection.Get(state.Seed, PermanentSelection.Upgrade, PermanentSelection.Key(upgraded)).Remaining;
        CardCmd.Upgrade(upgraded);
        PermanentSelection.DowngradeOne(eventRng, [upgraded]);
        CardCmd.Downgrade(upgraded);
        Assert(state.Random.Position(refill) == beforeRefill + 1 &&
            state.Selection.Get(state.Seed, PermanentSelection.Upgrade, PermanentSelection.Key(upgraded)).Remaining == retained,
            "random downgrade retains an unconsumed upgrade clock");
        var dagger = run.CreateCard<DingyDagger>(player);
        await CardPileCmd.Add(dagger, PileType.Deck, skipVisuals: true);
        PermanentSelection.UpgradeBatch([dagger], 1).ToArray();
        Assert(state.Selection.Get(state.Seed, PermanentSelection.Upgrade, PermanentSelection.Key(dagger)).Remaining > 0 &&
            state.Random.Position(refill) == beforeRefill + 2, "Dingy Dagger refills its consumed random-upgrade clock");

        await Reset();
        state = RunCheckpoints.Require(run);
        foreach (var card in player.Deck.Cards.Where(c => c.IsUpgradable).Take(3))
            CardCmd.Upgrade(card);
        var preMirror = state.Selection.Copy();
        var expectedDown = preMirror.Select(state.Seed, PermanentSelection.Downgrade,
            player.Deck.Cards.Where(c => c.IsUpgraded).Select(PermanentSelection.Key), 2);
        var reflections = ModelDb.Event<Reflections>().ToMutable();
        run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, reflections.Id);
        await reflections.BeginEvent(player, null, false);
        var beforeMirror = reflections.Rng.ToSerializable().counter;
        await (Task)AccessTools.Method(typeof(Reflections), "TouchAMirror").Invoke(reflections, [])!;
        Assert(player.Deck.Cards.Count(c => c.IsUpgraded) == 5 && reflections.Rng.ToSerializable().counter == beforeMirror + 2 &&
            expectedDown.All(key => state.Selection.Get(state.Seed, PermanentSelection.Downgrade, key).Remaining > 0) &&
            state.Selection.Clocks.Where(p => p.Key.StartsWith("[\"permanent upgrade\",", StringComparison.Ordinal)).Count(p => p.Value.Remaining == 0) == 4,
            "Reflections commits two downgrades before its four-card upgrade batch");

        await Reset();
        state = RunCheckpoints.Require(run);
        var tooth = (PaelsTooth)ModelDb.Relic<PaelsTooth>().ToMutable();
        player.AddRelicInternal(tooth);
        using (CardSelectorStack.SetTestSelector(selector))
        {
            selector.PrepareToSelect(player.Deck.Cards.Where(c => c.IsRemovable && c.IsUpgradable).Take(5).Reverse());
            await tooth.AfterObtained();
        }
        Assert(tooth.SerializableCards.Count == 5 && state.Selection.Clocks.Keys.Any(k => k.StartsWith("[\"deck routing\",\"upgrade\",", StringComparison.Ordinal)) &&
            !state.Selection.Clocks.Keys.Any(k => k.StartsWith("[\"deck routing\",\"removal\",", StringComparison.Ordinal)),
            "Pael's Tooth initial storage uses upgrade routing and retains five occurrences");
        var stored = tooth.SerializableCards.Select(c => CardModel.FromSerializable(c)).ToArray();
        foreach (var card in stored)
            run.AddCard(card, player);
        var expectedReturn = PermanentSelection.Fixed(stored, 1, "Pael's Tooth return").Single();
        var beforeReturn = player.PlayerRng.Rewards.ToSerializable().counter;
        await tooth.AfterCombatEnd(null!);
        Assert(player.Deck.Cards.Any(c => PermanentSelection.Key(c) == PermanentSelection.Key(expectedReturn) && c.IsUpgraded) &&
            tooth.SerializableCards.Count == 4 && player.PlayerRng.Rewards.ToSerializable().counter == beforeReturn,
            "Tooth returns the fixed-priority stored occurrence upgraded without consuming reward RNG");

        await Reset();
        state = RunCheckpoints.Require(run);
        var bracelet = ModelDb.Relic<BeautifulBracelet>().ToMutable();
        player.AddRelicInternal(bracelet);
        var expectedSwift = PermanentSelection.Fixed(player.Deck.Cards.Where(c => ModelDb.Enchantment<Swift>().CanEnchant(c)), 4, "Beautiful Bracelet");
        await bracelet.AfterObtained();
        Assert(player.Deck.Cards.Where(c => c.Enchantment is Swift).ToHashSet().SetEquals(expectedSwift) && state.Selection.Clocks.Count == 0,
            "Beautiful Bracelet uses fixed priorities and bypasses player routing");

        var bridge = ModelDb.Event<SlipperyBridge>().ToMutable();
        run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, bridge.Id);
        await bridge.BeginEvent(player, null, false);
        var offered = (CardModel)AccessTools.Field(typeof(SlipperyBridge), "_randomCardToLose").GetValue(bridge)!;
        var offeredKey = PermanentSelection.Key(offered);
        Assert(state.Selection.Get(state.Seed, PermanentSelection.Bridge, offeredKey).Remaining == 0,
            "Slippery Bridge commits its offered target's clock");
        AccessTools.Method(typeof(SlipperyBridge), "GetNewRandomCard").Invoke(bridge, []);
        var next = (CardModel)AccessTools.Field(typeof(SlipperyBridge), "_randomCardToLose").GetValue(bridge)!;
        Assert(state.Selection.Get(state.Seed, PermanentSelection.Bridge, offeredKey).Renewals == 1 && next.Id != offered.Id,
            "Bridge renews the declined occurrence once and preserves vanilla identity exclusions");

        await Reset();
        state = RunCheckpoints.Require(run);
        player.AddPotionInternal(ModelDb.Potion<BlockPotion>().ToMutable());
        Assert(ReferenceEquals(PermanentSelection.Sacrifice(player.Potions), player.Potions.Single()) && state.Selection.Clocks.Count == 0,
            "singleton potion sacrifice preserves clocks");
        player.AddToMaxPotionCount(1);
        player.AddPotionInternal(ModelDb.Potion<BlockPotion>().ToMutable());
        player.AddPotionInternal(ModelDb.Potion<StrengthPotion>().ToMutable());
        var sacrificeScope = RandomAddress.For("potion sacrifice");
        state.Selection.Get(state.Seed, sacrificeScope, ModelDb.Potion<BlockPotion>().Id.Entry).Remaining = 0.6;
        state.Selection.Get(state.Seed, sacrificeScope, ModelDb.Potion<StrengthPotion>().Id.Entry).Remaining = 0.5;
        var stone = ModelDb.Event<StoneOfAllTime>().ToMutable();
        run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, stone.Id);
        await stone.BeginEvent(player, null, false);
        var sacrifice = (PotionModel)AccessTools.Field(typeof(StoneOfAllTime), "_drinkAndLiftPotion").GetValue(stone)!;
        Assert(sacrifice is BlockPotion && state.Selection.Get(state.Seed, sacrificeScope, sacrifice.Id.Entry).Renewals == 1 &&
            Math.Abs(state.Selection.Get(state.Seed, sacrificeScope, ModelDb.Potion<StrengthPotion>().Id.Entry).Remaining - 0.2) < 1e-12,
            "Stone of All Time weights potion types by owned count and corrects surviving types");
        RunManager.Instance.CleanUp();
    }
}
