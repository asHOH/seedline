using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckSupportedModifiers()
    {
        await CheckHoarderCopySources();
        ModifierModel[] supported = [ModelDb.Modifier<Flight>(), ModelDb.Modifier<Murderous>(), ModelDb.Modifier<Terminal>(),
            ModelDb.Modifier<NightTerrors>(), ModelDb.Modifier<Midas>(), ModelDb.Modifier<BigGameHunter>(),
            ModelDb.Modifier<CharacterCards>(), ModelDb.Modifier<Hoarder>(), ModelDb.Modifier<Draft>()];
        ModifierModel Mutable(ModifierModel model)
        {
            var modifier = model.ToMutable();
            if (modifier is CharacterCards cards) cards.CharacterModel = ModelDb.GetId<Silent>();
            return modifier;
        }
        var lobby = new StartRunLobby(GameMode.Standard, true, new NetSingleplayerGameService(), null!, 1);
        var lobbyModifiers = (List<ModifierModel>)AccessTools.Field(typeof(StartRunLobby), "_modifiers").GetValue(lobby)!;
        for (var mask = 0; mask < 1 << supported.Length; mask++)
        {
            lobbyModifiers.Clear();
            lobbyModifiers.AddRange(supported.Where((_, i) => (mask & (1 << i)) != 0).Select(Mutable));
            Assert(RunContent.Covers(lobby), $"supported modifier combination {mask} receives full-content lobby handling");
        }
        foreach (var modifier in ModelDb.GoodModifiers.Concat(ModelDb.BadModifiers).Where(m => !supported.Contains(m)))
        {
            lobbyModifiers.Clear();
            lobbyModifiers.AddRange([Mutable(supported[0]), Mutable(modifier)]);
            Assert(!RunContent.Covers(lobby), $"{modifier.Id}: unsupported modifier disables lobby grants even with a supported modifier");
        }
        Assert(Harmony.GetPatchInfo(AccessTools.Method(typeof(NCustomRunScreen), "OnEmbarkPressed"))!.Owners.Contains(ModEntry.Id),
            "custom-run embark uses the full-content notice");
        lobby.CleanUp(false);

        foreach (var mode in new[] { GameMode.Standard, GameMode.Dread })
            foreach (var seed in new[] { "CUSTOMMODIFIERS", SeedHelper.GetRandomSeed() })
                foreach (var selected in supported.Select(m => new[] { m }).Prepend([]).Append(supported))
                {
                    RunManager.Instance.CleanUp();
                    var run = RunState.CreateForNewRun([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                        ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), selected.Select(Mutable).ToArray(), mode, true, 7, seed);
                    RunManager.Instance.SetUpNewSingleplayer(run, true);
                    LocalContext.NetId = RunManager.Instance.NetService.NetId;
                    var player = run.Players[0];
                    var state = RunCheckpoints.Require(run);
                    Assert(!RunScope.Bypasses(run) && state.Seed == seed && run.IsCustomGame,
                        $"{mode}/{string.Join(',', selected.Select(m => m.Id.Entry))}: custom run activates with entered or generated seed");
                    run.AppendToMapPointHistory(MapPointType.Ancient, RoomType.Event, ModelDb.Event<Neow>().Id);
                    var reward = new CardReward(CardCreationOptions.ForRoom(player, RoomType.Elite), 3, player);
                    state.Rewards.Rare.Offset = 0.2;
                    reward.Populate();
                    if (selected.Any(m => m is BigGameHunter))
                        Assert(reward.Cards.All(c => c.Rarity == CardRarity.Rare) && state.Rewards.Rare.Offset == 0.2,
                            "Big Game Hunter uses fixed-Rare triples without resetting the combat offset");
                    if (selected.Any(m => m is CharacterCards))
                    {
                        var poolIds = string.Join('/', new[] { player.Character.CardPool.Id.Entry, ModelDb.Character<Silent>().CardPool.Id.Entry }.Order(StringComparer.Ordinal));
                        Assert(CardOffers.Info(reward.Cards.First()).Offer.Contains(poolIds, StringComparison.Ordinal),
                            "Character Cards addresses rewards by the expanded pool");
                        var shop = MerchantInventory.CreateForNormalMerchant(player);
                        Assert(shop.CardEntries.All(c => CardOccurrences.Find(c.CreationResult!.Card) != null),
                            "Character Cards shop generation remains aligned");
                    }
                    if (selected.Any(m => m is Draft))
                    {
                        Assert(player.Deck.Cards.All(c => c is AscendersBane), "Draft removes the starting deck while preserving the later Ascension curse");
                        var initialCount = player.Deck.Cards.Count;
                        var neow = ModelDb.Event<Neow>().ToMutable();
                        AccessTools.Property(typeof(EventModel), nameof(EventModel.Owner)).SetValue(neow, player);
                        var offset = state.Rewards.Rare.Offset;
                        var selector = new TestCardSelector();
                        var offers = 0;
                        selector.PrepareToSelectCardReward((options, _) =>
                        {
                            offers++;
                            Assert(options.Count == 3 && options.All(c => !c.Card.IsUpgraded), "Draft offers three unupgraded tracked cards");
                            return new CardRewardSelection { card = options[0].Card };
                        });
                        using (CardSelectorStack.SetTestSelector(selector))
                            await run.Modifiers.OfType<Draft>().Single().GenerateNeowOption(neow)();
                        Assert(offers == 10 && player.Deck.Cards.Count == initialCount + (selected.Any(m => m is Hoarder) ? 30 : 10) &&
                            state.Rewards.Rare.Offset == offset, "Draft takes ten offers, preserves the combat offset and composes with Hoarder");
                    }
                    else if (selected.Any(m => m is Hoarder))
                    {
                        var before = player.Deck.Cards.Count;
                        await CardPileCmd.Add(reward.Cards.First(), PileType.Deck);
                        Assert(player.Deck.Cards.Count == before + 3, "Hoarder adds two copies of the selected reward");
                    }
                    Assert(player.Deck.Cards.Select(c => CardOccurrences.Info(c).Address).Distinct().Count() == player.Deck.Cards.Count,
                        "supported modifiers keep every deck occurrence distinct");
                    var snapshot = RunManager.Instance.ToSave(null);
                    var store = new MockGodotFileIo("supported-modifiers");
                    var saves = new RunSaveManager(1, store, new MigrationManager(store));
                    await saves.SaveRun(snapshot, false);
                    var loaded = saves.LoadRunSave().SaveData!;
                    Assert(loaded.Modifiers.Count == selected.Length && SameState(RunCheckpoints.Require(loaded), state),
                        "supported custom save retains modifiers and Seedline state");
                    RunManager.Instance.CleanUp();
                    var restored = RunState.FromSerializable(loaded);
                    await RunManager.Instance.SetUpSavedSingleplayer(restored, loaded);
                    Assert(!RunScope.Bypasses(restored) && SameState(RunCheckpoints.Require(restored), state) &&
                        restored.Modifiers.OfType<CharacterCards>().All(c => c.CharacterModel == ModelDb.GetId<Silent>()),
                        "supported custom restore retains active state and Character Cards configuration");
                }
        RunManager.Instance.CleanUp();
    }

    private static async Task CheckHoarderCopySources()
    {
        foreach (var sourceName in new[] { "Clone rest site", "Reflections", "Dolly's Mirror" })
        {
            string[]? expectedOccurrences = null;
            string[]? expectedCombatKeys = null;
            foreach (var reverse in new[] { false, true })
            {
                var run = RunState.CreateForNewRun([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                    ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), [ModelDb.Modifier<Hoarder>().ToMutable()],
                    GameMode.Standard, true, 7, "HOARDERCOPIES");
                RunManager.Instance.SetUpNewSingleplayer(run, true);
                LocalContext.NetId = RunManager.Instance.NetService.NetId;
                var player = run.Players[0];
                run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, ModelDb.Event<Reflections>().Id);
                var initialCount = player.Deck.Cards.Count;
                var originals = player.Deck.Cards.ToArray();
                if (reverse)
                {
                    foreach (var card in originals) card.RemoveFromCurrentPile();
                    foreach (var card in originals.Reverse()) player.Deck.AddInternal(card);
                }
                if (sourceName == "Reflections")
                {
                    var source = ModelDb.Event<Reflections>().ToMutable();
                    await source.BeginEvent(player, null, false);
                    await (Task)AccessTools.Method(typeof(Reflections), "Shatter").Invoke(source, [])!;
                }
                else if (sourceName == "Clone rest site")
                {
                    foreach (var card in originals.DistinctBy(c => c.Id).Take(2)) CardCmd.Enchant<Clone>(card, 1);
                    await new CloneRestSiteOption(player).OnSelect();
                }
                else
                {
                    var mirror = ModelDb.Relic<DollysMirror>().ToMutable();
                    player.AddRelicInternal(mirror);
                    var selector = new TestCardSelector();
                    selector.PrepareToSelect([originals[0]]);
                    using (CardSelectorStack.SetTestSelector(selector)) await mirror.AfterObtained();
                }
                var expectedCount = sourceName switch { "Reflections" => initialCount * 4 + 3, "Clone rest site" => initialCount + 6, _ => initialCount + 3 };
                Assert(player.Deck.Cards.Count == expectedCount &&
                    player.Deck.Cards.Select(c => CardOccurrences.Info(c).Address).Distinct().Count() == player.Deck.Cards.Count,
                    $"Hoarder with {sourceName} preserves copy counts and distinct occurrences");
                var occurrences = player.Deck.Cards.Select(c => $"{c.Id}/{CardOccurrences.Info(c)}").Order(StringComparer.Ordinal).ToArray();
                expectedOccurrences ??= occurrences;
                Assert(occurrences.SequenceEqual(expectedOccurrences), $"Hoarder with {sourceName} preserves occurrences and acquisition batches under deck reversal");
                var snapshot = RunManager.Instance.ToSave(null);
                var bytes = RunCheckpoints.Encode(snapshot);
                RunManager.Instance.CleanUp();
                var loaded = RunCheckpoints.Decode(System.Text.Encoding.UTF8.GetString(bytes), snapshot.SchemaVersion);
                var restored = RunState.FromSerializable(loaded);
                await RunManager.Instance.SetUpSavedSingleplayer(restored, loaded);
                var combat = CombatFixture.CreateActiveCombat(restored);
                var combatCards = combat.Players[0].PlayerCombatState!.DrawPile.Cards;
                Assert(combatCards.Count == expectedCount && combatCards.All(c => CombatCards.Find(c) != null),
                    "nested Hoarder copies save, reload and enter the first combat with valid tracking");
                var combatKeys = combatCards.Select(c => $"{c.Id}/{CombatCards.Info(c).Key}").Order(StringComparer.Ordinal).ToArray();
                expectedCombatKeys ??= combatKeys;
                Assert(combatKeys.SequenceEqual(expectedCombatKeys), $"Hoarder with {sourceName} preserves combat keys under deck reversal");
                CombatFixture.SetInProgress(false);
                CombatManager.Instance.Reset(false);
                RunManager.Instance.CleanUp();
            }
        }
    }
}
