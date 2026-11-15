using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Context;

namespace SameSeed.Checks;

public static partial class Checks
{
    private static async Task CheckCardRewards()
    {
        var previous = TestMode.IsOn;
        TestMode.IsOn = true;
        try
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            var state = RunCheckpoints.Require(run);
            var draws = AccessTools.Method(typeof(CardOrder), "Draws");
            Assert(new CardModel[] { ModelDb.Card<MachineLearning>(), ModelDb.Card<DarkEmbrace>(), ModelDb.Card<ToolsOfTheTrade>(), ModelDb.Card<Predator>(), ModelDb.Card<Acrobatics>() }
                    .All(c => (bool)draws.Invoke(null, [c])!) && !(bool)draws.Invoke(null, [ModelDb.Card<Bash>()])!,
                "permanent processing order recognizes immediate and power-mediated draw effects");
            Assert(player.Deck.Cards.All(c => CardOccurrences.Find(c) != null) &&
                player.Deck.Cards.Select(c => CardOccurrences.Info(c).Address).Distinct().Count() == player.Deck.Cards.Count,
                "starting deck and Ascender's Bane have distinct permanent occurrences");
            var start = RunManager.Instance.ToSave(null);
            var bytes = RunCheckpoints.Encode(start);
            CardModel[] Offer(RoomType room) => CardFactory.CreateForReward(player, 3, CardCreationOptions.ForRoom(player, room)).Select(c => c.Card).ToArray();
            var normal = Offer(RoomType.Monster);
            Assert(normal.Length == 3 && normal.All(c => c.Rarity != CardRarity.Rare) && normal.Select(c => c.Id).Distinct().Count() == 3,
                "first Scarcity normal reward is a distinct non-Rare triple");
            var next = Offer(RoomType.Monster);
            Assert(next.All(c => c.Rarity != CardRarity.Rare) && state.Rewards.Rare.NonRareTriples == 2,
                "opening two triples preserve zero Rare chance and shared progression");
            var chosen = normal[1];
            var chosenEntry = CardOccurrences.Info(chosen).Address;
            await CardPileCmd.Add(chosen, PileType.Deck, skipVisuals: true);
            Assert(CardOccurrences.Info(chosen).Address == chosenEntry && CardOccurrences.Info(chosen).Key != chosenEntry,
                "reward acquisition keeps its entry occurrence and assigns an offer key before deck hooks");
            var savedCard = chosen.ToSerializable();
            var recreated = CardModel.FromSerializable(savedCard);
            Assert(CardOccurrences.Info(recreated).Address == chosenEntry && CardOccurrences.Info(recreated).Key == CardOccurrences.Info(chosen).Key,
                "serialized physical recreation preserves occurrence and card key");
            var copy = run.CloneCard(chosen);
            await CardPileCmd.Add(copy, PileType.Deck, skipVisuals: true);
            Assert(CardOccurrences.Info(copy).Address != chosenEntry && CardOccurrences.Info(copy).Key != CardOccurrences.Info(chosen).Key,
                "a permanent copy starts a distinct occurrence and selection key");
            var tooth = (PaelsTooth)ModelDb.Relic<PaelsTooth>().ToMutable();
            player.AddRelicInternal(tooth);
            tooth.DebugAddCard(((CardModel)chosen.MutableClone()).ToSerializable());
            await CardPileCmd.RemoveFromDeck(chosen);
            var snapshot = RunManager.Instance.ToSave(null);
            var storedBytes = RunCheckpoints.Encode(snapshot);
            var expected = Offer(RoomType.Elite).Select(c => (c.Id, CardOccurrences.Info(c).Address)).ToArray();
            Assert(storedBytes.SequenceEqual(RunCheckpoints.Encode(snapshot)), "card generation leaves a captured checkpoint unchanged");
            var restored = RunCheckpoints.Decode(Encoding.UTF8.GetString(storedBytes), snapshot.SchemaVersion);
            RunManager.Instance.CleanUp();
            run = RunState.FromSerializable(restored);
            await RunManager.Instance.SetUpSavedSingleplayer(run, restored);
            player = run.Players[0];
            Assert(expected.SequenceEqual(Offer(RoomType.Elite).Select(c => (c.Id, CardOccurrences.Info(c).Address))),
                "reload restores Rare timing and triple identities before reward regeneration");
            var returned = CardModel.FromSerializable(player.Relics.OfType<PaelsTooth>().Single().SerializableCards.Single());
            run.AddCard(returned, player);
            await CardPileCmd.Add(returned, PileType.Deck, skipVisuals: true);
            Assert(CardOccurrences.Info(returned).Address == chosenEntry && CardOccurrences.Info(returned).Key == CardOccurrences.Info(chosen).Key,
                "Pael's Tooth storage survives checkpoint reload and retains identity on return");

            restored = RunCheckpoints.Decode(Encoding.UTF8.GetString(bytes), start.SchemaVersion);
            RunManager.Instance.CleanUp();
            run = RunState.FromSerializable(restored);
            await RunManager.Instance.SetUpSavedSingleplayer(run, restored);
            player = run.Players[0];
            state = RunCheckpoints.Require(run);
            var baseOffer = CardFactory.CreateForReward(player, 5, CardCreationOptions.ForNonCombatWithDefaultOdds([player.Character.CardPool])).Select(c => c.Card).ToArray();
            var shared = baseOffer.Where(c => !CardOccurrences.Info(c).Supplementary).OrderBy(c => CardOccurrences.Info(c).EntryOrder).ToArray();
            Assert(normal.Select(c => c.Id).SequenceEqual(shared.Select(c => c.Id)), "Brain Leech shares the same first non-Rare triple across offer sizes");
            Assert(baseOffer.Select(c => c.Id).Distinct().Count() == 5, "supplementary event cards exclude triple and earlier identities");
            var fixedOptions = CardCreationOptions.ForNonCombatWithUniformOdds([player.Character.CardPool], c => c.Rarity == CardRarity.Common);
            var fixedCards = CardFactory.CreateForReward(player, 3, fixedOptions).Select(c => c.Card).ToArray();
            var bundles = SpecialCardRewards.GenerateBundles(player);
            Assert(bundles.SelectMany(b => b).Select(c => c.Id).Distinct().Count() == 6 && bundles.All(b => b.Count == 3 && b[2].Rarity == CardRarity.Uncommon),
                "Scroll Boxes produces two distinct Common/Common/Uncommon bundles");
            var commonCounter = RandomAddress.For("card triple", player.Character.CardPool.Id.Entry, "Common");
            Assert(state.Random.Position(commonCounter) == 2, "Scroll Boxes consumes exactly one Common triple");
            CardOccurrences.AssignKeys(bundles[0]);
            var keySet = bundles[0].Select(c => CardOccurrences.Info(c).Key).Order().ToArray();
            CardOccurrences.AssignKeys(bundles[0].Reverse());
            Assert(keySet.SequenceEqual(bundles[0].Select(c => CardOccurrences.Info(c).Key).Order()), "bundle key assignment ignores selection enumeration order");
            var mapping = bundles[0].ToDictionary(c => c.Id, c => CardOccurrences.Info(c).Key);
            CardOccurrences.AssignKeys(bundles[0].Reverse());
            Assert(bundles[0].All(c => mapping[c.Id] == CardOccurrences.Info(c).Key), "pair priorities preserve identity-to-key mapping under reordered picks");
            var wing = ModelDb.Relic<WingCharm>().ToMutable();
            player.AddRelicInternal(wing);
            var wingOffer = Offer(RoomType.Monster);
            Assert(wingOffer.Count(c => c.Enchantment is Swift) == 1 && wingOffer.All(c => CardOccurrences.Find(c) != null),
                "Wing Charm selects one eligible shared entry and preserves metadata through its clone");
            var gem = ModelDb.Relic<PrismaticGem>().ToMutable();
            player.AddRelicInternal(gem);
            var reward = new CardReward(CardCreationOptions.ForRoom(player, RoomType.Monster), 3, player);
            reward.Populate();
            var before = state.Rewards.Rare.NonRareTriples;
            state.Rewards.Rare.Threshold = 1000;
            var options = (CardCreationOptions)AccessTools.Property(typeof(CardReward), "RerollOptions").GetValue(reward)!;
            var reroll = CardFactory.CreateForReward(player, 3, options).ToArray();
            Assert(reroll.Length == 3 && state.Rewards.Rare.NonRareTriples == before + 1 && options.CardPools.Count == 1,
                "expanded-pool rerolls retain own-character offer classification and opening progression");
            var candy = (LastingCandy)ModelDb.Relic<LastingCandy>().ToMutable();
            player.AddRelicInternal(candy);
            candy.CombatRewardsSeen = 1;
            var candyOffer = CardFactory.CreateForReward(player, 3, CardCreationOptions.ForRoom(player, RoomType.Monster)
                .WithFlags(CardCreationFlags.IsCardReward | CardCreationFlags.IsFromCombat)).ToArray();
            Assert(candyOffer.Length == 4 && candyOffer[^1].Card.Type == CardType.Power &&
                CardOccurrences.Info(candyOffer[^1].Card).Offer == CardOccurrences.Info(candyOffer[0].Card).Offer,
                "Lasting Candy inherits expanded Power supply and parent offer address");
            var otherOptions = CardCreationOptions.ForNonCombatWithUniformOdds([ModelDb.Character<Silent>().CardPool], c => c.Rarity == CardRarity.Rare)
                .WithFlags(CardCreationFlags.NoCardPoolModifications);
            var otherCards = CardFactory.CreateForReward(player, 5, otherOptions).ToArray();
            Assert(otherCards.Length == 5 && otherCards.Select(c => c.Card.Id).Distinct().Count() == 5 && otherCards.All(c => c.Card.Rarity == CardRarity.Rare),
                "other-character rewards consume distinct individual fixed-rarity entries");

            RunManager.Instance.CleanUp();
            run = StartGoldRun();
            player = run.Players[0];
            var same = player.Deck.Cards.Where(c => c.Id.Entry == "STRIKE_IRONCLAD").Take(2).ToArray();
            tooth = (PaelsTooth)ModelDb.Relic<PaelsTooth>().ToMutable();
            player.AddRelicInternal(tooth);
            foreach (var card in same)
            {
                tooth.DebugAddCard(card.ToSerializable());
                await CardPileCmd.RemoveFromDeck(card);
            }
            ulong secondCopySeed = 0;
            while (new Rng(secondCopySeed).NextInt(2) != 1)
                secondCopySeed++;
            player.PlayerRng.Rewards.LoadFromSerializable(new Rng(secondCopySeed).ToSerializable());
            await tooth.AfterCombatEnd(null!);
            var active = player.Deck.Cards.Select(c => CardOccurrences.Info(c).Address)
                .Concat(tooth.SerializableCards.Select(c => CardOccurrences.Info(CardModel.FromSerializable(c)).Address)).ToArray();
            Assert(tooth.SerializableCards.Count == 1 && active.Distinct().Count() == active.Length,
                "Pael's Tooth returns exactly one stored occurrence even when serialized cards compare equal");
            var freshSnapshot = RunManager.Instance.ToSave(null);
            var validBytes = RunCheckpoints.Encode(freshSnapshot);
            var invalid = System.Text.Json.Nodes.JsonNode.Parse(validBytes)!.AsObject();
            invalid["players"]![0]!["deck"]![0]!["props"]!["strings"] = new System.Text.Json.Nodes.JsonArray();
            try
            {
                RunCheckpoints.Decode(invalid.ToJsonString(), freshSnapshot.SchemaVersion);
                throw new InvalidOperationException("Missing card metadata was accepted.");
            }
            catch (InvalidDataException)
            {
                Assert(validBytes.SequenceEqual(RunCheckpoints.Encode(freshSnapshot)), "invalid card metadata is rejected without changing the checkpoint");
            }
            foreach (var character in ModelDb.AllCharacters)
            {
                foreach (var ascension in new[] { 0, 7 })
                {
                    RunManager.Instance.CleanUp();
                    player = Player.CreateForNewRun(character, UnlockState.all, 1);
                    run = RunState.CreateForNewRun([player], ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), [],
                        GameMode.Standard, false, ascension, "CARDCHARACTERS");
                    RunManager.Instance.SetUpNewSingleplayer(run, true);
                    LocalContext.NetId = RunManager.Instance.NetService.NetId;
                    RelicRewards.Initialize(player);
                    var offers = new[] { RoomType.Monster, RoomType.Elite, RoomType.Boss }.Select(Offer).ToArray();
                    Assert(offers.All(o => o.Length == 3 && o.Select(c => c.Id).Distinct().Count() == 3) &&
                        offers[2].All(c => c.Rarity == CardRarity.Rare) && RunCheckpoints.Require(run).Rewards.Rare.Offset == -0.05,
                        $"{character.Id.Entry} A{ascension}: relic allocation and all three combat card offer categories generate successfully");
                    if (character is Defect && ascension == 7)
                    {
                        ulong clawSeed = 0;
                        var commonPool = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
                            .Where(c => c.IsAllowedInRun(run) && c.Rarity == CardRarity.Common).ToArray();
                        for (; clawSeed < 1000000; clawSeed++)
                        {
                            var probe = new Rng(clawSeed);
                            if (probe.NextInt(100) == 0 && probe.NextInt(100) > 0 && commonPool[probe.NextInt(commonPool.Length)] is Claw)
                                break;
                        }
                        Assert(clawSeed < 1000000, "found a deterministic substituted-and-ordinary Claw fixture");
                        player.PlayerRng.NonCombatGeneration.LoadFromSerializable(new Rng(clawSeed).ToSerializable());
                        var clawBundles = SpecialCardRewards.GenerateBundles(player);
                        Assert(clawBundles[0].All(c => c is Claw) &&
                            RunCheckpoints.Require(run).Random.Position(RandomAddress.For("card triple", player.Character.CardPool.Id.Entry, "Common")) == 0,
                            "Defect's triple-Claw substitution consumes no Common triple");
                        Assert(clawBundles[1][0] is Claw && clawBundles[1].Select(c => c.Id).Distinct().Count() == 3,
                            "a substituted Claw bundle preserves Claw eligibility in the ordinary bundle");
                        CardOccurrences.AssignKeys(clawBundles[0]);
                        Assert(clawBundles[0].Select(c => CardOccurrences.Info(c).Key).Distinct().Count() == 3,
                            "identical Claws in one chosen bundle receive distinct offer keys");
                    }
                }
            }
            RunManager.Instance.CleanUp();
            run = StartGoldRun();
            player = run.Players[0];
            var originals = player.Deck.Cards.ToArray();
            var reflections = ModelDb.Event<Reflections>().ToMutable();
            await reflections.BeginEvent(player, null, false);
            await (Task)AccessTools.Method(typeof(Reflections), "Shatter").Invoke(reflections, [])!;
            Assert(player.Deck.Cards.Count == originals.Length * 2 + 1 && player.Deck.Cards
                .Select(c => CardOccurrences.Info(c).Address).Distinct().Count() == player.Deck.Cards.Count,
                "asynchronous Reflections copies create distinct source-addressed occurrences across the entire batch");
            var copyAddresses = player.Deck.Cards.Except(originals).Where(c => c is not BadLuck)
                .Select(c => CardOccurrences.Info(c).Address).ToArray();
            Assert(copyAddresses.All(a => a.StartsWith("[\"card copy\",\"REFLECTIONS\",0,", StringComparison.Ordinal)),
                "all Reflections copies share one committed source opportunity through asynchronous waits");
        }
        finally
        {
            RunManager.Instance.CleanUp();
            TestMode.IsOn = previous;
        }
    }
}
