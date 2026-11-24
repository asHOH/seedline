using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Timeline;
using MegaCrit.Sts2.Core.Timeline.Epochs;
using MegaCrit.Sts2.Core.Unlocks;

namespace Seedline.Checks;

public static partial class Checks
{
    // Exercise multiplayer setup without sockets or remote peers.
    private sealed class BypassNetService : NetSingleplayerGameService, INetGameService
    {
        NetGameType INetGameService.Type => NetGameType.Client;
    }

    private static async Task CheckModeBypass()
    {
        var manager = RunManager.Instance;
        var previous = TestMode.IsOn;
        TestMode.IsOn = true;
        try
        {
            foreach (var (mode, multiplayer, count, custom) in new[]
            {
                (GameMode.Blitz, false, 1, false), (GameMode.Daily, false, 1, false),
                (GameMode.Standard, true, 1, false), (GameMode.Standard, true, 2, true)
            })
            {
                manager.CleanUp();
                var label = $"{mode}, multiplayer={multiplayer}, players={count}, custom={custom}";
                var unlocks = new UnlockState(EpochModel.AllEpochIds.Except([EpochModel.GetId<Fool6Epoch>()]), [], 0);
                var players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(unlocks, (ulong)id)).ToArray();
                var run = RunState.CreateForNewRun(players, ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(),
                    mode == GameMode.Daily || custom ? [ModelDb.Modifier<DeprecatedModifier>().ToMutable()] : [],
                    mode, custom, mode == GameMode.Blitz ? 0 : 7, "MODEBYPASS");
                if (multiplayer)
                {
                    var lobby = new StartRunLobby(mode, custom, new BypassNetService(), null!, count);
                    lobby.Players.AddRange(players.Select(p => new StartRunLobbyPlayer
                    {
                        id = p.NetId,
                        character = p.Character,
                        unlockState = unlocks.ToSerializable()
                    }));
                    Assert(!RunContent.Covers(lobby), $"{label}: lobby preserves ordinary unlocks");
                    manager.SetUpNewMultiplayer(run, lobby, true);
                    lobby.CleanUp(false);
                }
                else
                    manager.SetUpNewSingleplayer(run, true);
                Assert(RunScope.Bypasses(run) && RunCheckpoints.Find(run) == null &&
                    players.All(p => p.UnlockState == unlocks && p.Deck.Cards.All(c => CardOccurrences.Find(c) == null)),
                    $"{label}: startup bypasses without Seedline state or unlock grants");

                var snapshot = manager.ToSave(null);
                if (!multiplayer)
                {
                    var json = JsonSerializationUtility.ToJson(snapshot).TrimEnd();
                    foreach (var damaged in new[] { json[..^1], json[..^1] + ",}" })
                    {
                        var repairStore = new MockGodotFileIo("bypass-repair");
                        var repairPath = RunSaveManager.GetRunSavePath(1, RunSaveManager.runSaveFileName);
                        repairStore.WriteFile(repairPath, damaged);
                        var repaired = new RunSaveManager(1, repairStore, new MigrationManager(repairStore)).LoadRunSave();
                        Assert(repaired.Success && repaired.Status == ReadSaveStatus.JsonRepaired && repaired.SaveData!.GameMode == mode &&
                            repairStore.ReadFile(repairPath) == damaged && repairStore.ReadFile(repairPath + ".pre-repair") == damaged,
                            $"{label}: vanilla repairs JSON without a backup and preserves the original bytes");
                    }
                }
                foreach (var synchronous in new[] { false, true })
                {
                    var store = new MockGodotFileIo("bypass");
                    var saves = new RunSaveManager(1, store, new MigrationManager(store), synchronous);
                    var path = RunSaveManager.GetRunSavePath(1, multiplayer ? RunSaveManager.multiplayerRunSaveFileName : RunSaveManager.runSaveFileName);
                    var saved = 0;
                    saves.Saved += () => saved++;
                    await saves.SaveRun(snapshot, multiplayer);
                    Assert(saved == 1 && !JsonNode.Parse(store.ReadFile(path)!)!.AsObject().ContainsKey("seedline"),
                        $"{label}, sync={synchronous}: normal save path, callback and no Seedline envelope");
                    var loaded = multiplayer ? saves.LoadMultiplayerRunSave() : saves.LoadRunSave();
                    Assert(loaded.Success && loaded.SaveData!.GameMode == mode, $"{label}: ordinary save loads");
                    snapshot = loaded.SaveData!;
                    snapshot.NumReloads++;
                    await saves.SaveRun(snapshot, multiplayer);
                    store.WriteFile(path, "{torn");
                    Assert((multiplayer ? saves.LoadMultiplayerRunSave() : saves.LoadRunSave()).Success,
                        $"{label}: normal backup recovery");
                    store.DeleteFile(path);
                    Assert((multiplayer ? saves.LoadMultiplayerRunSave() : saves.LoadRunSave()).Success,
                        $"{label}: backup-only loading");
                }

                manager.CleanUp();
                run = RunState.FromSerializable(snapshot);
                if (multiplayer)
                {
                    var lobby = new LoadRunLobby(new BypassNetService(), null!, snapshot);
                    lobby.Players.AddRange(run.Players.Select(p => new LoadRunLobbyPlayer { id = p.NetId }));
                    await manager.SetUpSavedMultiplayer(run, lobby);
                    lobby.CleanUp(false);
                }
                else
                    await manager.SetUpSavedSingleplayer(run, snapshot);
                Assert(RunScope.IsBypassed && RunCheckpoints.Find(run) == null && run.Modifiers.Count == snapshot.Modifiers.Count,
                    $"{label}: resume bypasses before modifier validation");
                await CheckBypassedGameplay(run, label);
                manager.CleanUp();
                Assert(!RunScope.IsBypassed, "cleanup leaves no global bypass flag");
            }

            foreach (var mode in new[] { GameMode.Standard, GameMode.Dread })
            {
                var run = RunState.CreateForNewRun([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                    ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), [], mode, false, 7, "ACTIVEAGAIN");
                manager.SetUpNewSingleplayer(run, true);
                Assert(!RunScope.IsBypassed && RunCheckpoints.Find(run) != null &&
                    run.Players[0].Deck.Cards.All(c => CardOccurrences.Find(c) != null), $"{mode}: Seedline activates again in the same session");
                var snapshot = manager.ToSave(null);
                var store = new MockGodotFileIo("active-again");
                var path = RunSaveManager.GetRunSavePath(1, RunSaveManager.runSaveFileName);
                var saves = new RunSaveManager(1, store, new MigrationManager(store));
                store.WriteFile(path, JsonSerializationUtility.ToJson(snapshot));
                Assert(saves.LoadRunSave().Status == ReadSaveStatus.ValidationFailed,
                    $"{mode}: missing Seedline envelope still fails rather than bypassing");
                var changedMode = JsonNode.Parse(RunCheckpoints.Encode(snapshot))!;
                changedMode["game_mode"] = "daily";
                store.WriteFile(path, changedMode.ToJsonString());
                Assert(saves.LoadRunSave().Status == ReadSaveStatus.ValidationFailed,
                    $"{mode}: a Seedline envelope with an invalid mode still fails");
                foreach (var json in new[] { JsonSerializationUtility.ToJson(snapshot).TrimEnd(), changedMode.ToJsonString() })
                {
                    var strictStore = new MockGodotFileIo("strict-repair");
                    var damaged = json[..^1];
                    strictStore.WriteFile(path, damaged);
                    var rejected = new RunSaveManager(1, strictStore, new MigrationManager(strictStore)).LoadRunSave();
                    Assert(!rejected.Success && rejected.Status == ReadSaveStatus.JsonParseError &&
                        strictStore.ReadFile(path) == damaged && !strictStore.FileExists(path + ".pre-repair"),
                        $"{mode}: repair does not admit an active-mode save or a Seedline envelope relabeled Daily");
                }
                manager.CleanUp();
            }
        }
        finally
        {
            manager.CleanUp();
            TestMode.IsOn = previous;
        }
    }

    private static async Task CheckBypassedGameplay(RunState run, string label)
    {
        var player = run.Players[0];
        await Hook.BeforeRoomEntered(run, new RestSiteRoom());
        var reward = new CardReward(CardCreationOptions.ForRoom(player, RoomType.Monster), 3, player);
        reward.Populate();
        var shop = MerchantInventory.CreateForNormalMerchant(player);
        Assert(reward.Cards.Count() == 3 && reward.Cards.All(c => CardOccurrences.Find(c) == null) &&
            shop.CardEntries.All(c => CardOccurrences.Find(c.CreationResult!.Card) == null),
            $"{label}: reward and shop factories run without Seedline metadata");
        _ = RelicFactory.PullNextRelicFromFront(player, RelicRarity.Common);
        _ = PotionFactory.CreateRandomPotionsOutOfCombat(player, 2, player.PlayerRng.Rewards).ToArray();
        _ = run.Odds.UnknownMapPoint.Roll([], run);
        _ = run.Act.PullNextEvent(run);

        var stone = ModelDb.Relic<Whetstone>().ToMutable();
        player.AddRelicInternal(stone);
        var upgradeRng = new Rng(player.PlayerRng.NonCombatGeneration.ToSerializable());
        var expected = player.Deck.Cards.Where(c => c.Type == CardType.Attack && c.IsUpgradable)
            .ToList().StableShuffle(upgradeRng).Take(stone.DynamicVars.Cards.IntValue).ToArray();
        await stone.AfterObtained();
        Assert(player.Deck.Cards.Where(c => c.IsUpgraded).ToHashSet().SetEquals(expected) &&
            upgradeRng.ToSerializable().Equals(player.PlayerRng.NonCombatGeneration.ToSerializable()),
            $"{label}: permanent upgrade targets and RNG match vanilla");

        var shuffleRng = new Rng(run.Rng.Shuffle.ToSerializable());
        var opening = player.Deck.Cards.ToList().UnstableShuffle(shuffleRng);
        var combat = CombatFixture.CreateActiveCombat(run);
        var pcs = player.PlayerCombatState!;
        Assert(pcs.DrawPile.Cards.Select(c => c.DeckVersion).SequenceEqual(opening) &&
            shuffleRng.ToSerializable().Equals(run.Rng.Shuffle.ToSerializable()) &&
            pcs.DrawPile.Cards.All(c => CombatCards.Find(c) == null), $"{label}: opening shuffle and RNG match vanilla");
        var soul = combat.CreateCard<Soul>(player);
        var gap = shuffleRng.NextInt(pcs.DrawPile.Cards.Count + 1);
        await CardPileCmd.AddGeneratedCardToCombat(soul, PileType.Draw, player, CardPilePosition.Random);
        Assert(pcs.DrawPile.Cards.IndexOf(soul) == gap && shuffleRng.ToSerializable().Equals(run.Rng.Shuffle.ToSerializable()),
            $"{label}: random insertion and RNG match vanilla");
        foreach (var card in pcs.DrawPile.Cards.ToArray())
        {
            card.RemoveFromCurrentPile();
            pcs.DiscardPile.AddInternal(card);
        }
        var reshuffle = pcs.DiscardPile.Cards.ToList().StableShuffle(shuffleRng);
        await CardPileCmd.Shuffle(new ThrowingPlayerChoiceContext(), player);
        Assert(pcs.DrawPile.Cards.SequenceEqual(reshuffle) && shuffleRng.ToSerializable().Equals(run.Rng.Shuffle.ToSerializable()),
            $"{label}: reshuffle order and RNG match vanilla");

        var context = new ThrowingPlayerChoiceContext();
        foreach (var card in pcs.DrawPile.Cards.Take(3).ToArray())
        {
            card.RemoveFromCurrentPile();
            pcs.Hand.AddInternal(card);
        }
        var grit = combat.CreateCard<TrueGrit>(player);
        var selectionRng = new Rng(run.Rng.CombatCardSelection.ToSerializable());
        var exhaust = selectionRng.NextItem(pcs.Hand.Cards);
        await (Task)AccessTools.Method(typeof(TrueGrit), "OnPlay").Invoke(grit, [context, new CardPlay
        {
            Card = grit, Player = player, Target = null, IsAutoPlay = false, ResultPile = PileType.Discard,
            Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 }, PlayIndex = 0, PlayCount = 1
        }])!;
        Assert(exhaust!.Pile == pcs.ExhaustPile && selectionRng.ToSerializable().Equals(run.Rng.CombatCardSelection.ToSerializable()),
            $"{label}: random exhaust target and RNG match vanilla");

        var confused = (ConfusedPower)ModelDb.Power<ConfusedPower>().ToMutable();
        AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(confused, player.Creature);
        player.Creature.ApplyPowerInternal(confused);
        var costRng = new Rng(run.Rng.CombatEnergyCosts.ToSerializable());
        var kick = combat.CreateCard<KinglyKick>(player);
        CardCmd.Enchant<Slither>(kick, 1);
        pcs.Hand.AddInternal(kick);
        costRng.NextInt(4);
        var cost = costRng.NextInt(4);
        await Hook.AfterCardDrawn(combat, context, kick, false);
        Assert(kick.EnergyCost.GetWithModifiers(CostModifiers.Local) == cost &&
            costRng.ToSerializable().Equals(run.Rng.CombatEnergyCosts.ToSerializable()),
            $"{label}: Confused and Slither keep separate vanilla rolls");
        player.Creature.RemoveAllPowersInternalExcept();

        foreach (var card in pcs.Hand.Cards.ToArray()) card.RemoveFromCurrentPile();
        var generationRng = new Rng(run.Rng.CombatCardGeneration.ToSerializable());
        var generated = CardFactory.GetDistinctForCombat(player, ModelDb.CardPool<ColorlessCardPool>().GetUnlockedCards(player.UnlockState,
            player.RunState.CardMultiplayerConstraint), 3, generationRng).Select(c => c.Id).ToArray();
        var potion = ModelDb.Potion<CosmicConcoction>().ToMutable();
        player.AddPotionInternal(potion);
        potion.RemoveBeforeUse();
        await (Task)AccessTools.Method(typeof(CosmicConcoction), "OnUse").Invoke(potion, [context, player.Creature])!;
        Assert(pcs.Hand.Cards.Select(c => c.Id).SequenceEqual(generated) &&
            generationRng.ToSerializable().Equals(run.Rng.CombatCardGeneration.ToSerializable()),
            $"{label}: potion generation and RNG match vanilla without effect ordinals");

        var enemy = combat.Enemies[0];
        var branch = new RandomBranchState("BYPASS");
        branch.AddBranch(new MoveState("A", _ => Task.CompletedTask), MoveRepeatType.CanRepeatForever, 1);
        branch.AddBranch(new MoveState("B", _ => Task.CompletedTask), MoveRepeatType.CanRepeatForever, 3);
        var aiRng = new Rng(run.Rng.MonsterAi.ToSerializable());
        var intent = branch.GetNextState(enemy, aiRng);
        Assert(branch.GetNextState(enemy, run.Rng.MonsterAi) == intent &&
            aiRng.ToSerializable().Equals(run.Rng.MonsterAi.ToSerializable()), $"{label}: enemy AI retains vanilla RNG");
        CombatFixture.SetInProgress(false);
        CombatManager.Instance.Reset(false);
    }
}
