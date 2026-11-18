using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCombatShuffle()
    {
        foreach (var act in new[] { 0, 2 })
        {
            string[]? expectedOpening = null;
            Dictionary<string, string>? expectedKeys = null;
            string? expectedReshuffle = null;
            for (var branch = 0; branch < 3; branch++)
            {
                var run = StartGoldRun();
                var player = run.Players[0];
                var innate = run.CreateCard<Apotheosis>(player);
                await CardPileCmd.Add(innate, PileType.Deck);
                CardOccurrences.AssignKey(innate, RandomAddress.For("shuffle check reward").Key);
                CardModel removed = branch == 2 ? innate : player.Deck.Cards.OfType<StrikeIronclad>().First();
                var removedAddress = CardOccurrences.Info(removed).Address;
                if (branch != 0)
                {
                    await CardPileCmd.RemoveFromDeck(removed, false);
                    foreach (var card in player.Deck.Cards.ToArray())
                        player.Deck.MoveToTopInternal(card);
                }
                run.CurrentActIndex = act;
                var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
                combat.AddPlayer(player);
                combat.Encounter!.GenerateMonstersWithSlots(run);
                foreach (var (monster, slot) in combat.Encounter.MonstersWithSlots)
                    combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
                var shuffleRng = run.Rng.Shuffle.ToSerializable();
                var overrideCalled = false;
                TestRngInjector.SetInitialShuffleOverride(_ => overrideCalled = true);
                CombatManager.Instance.SetUpCombat(combat);
                var scope = CombatStateScope.Require(combat);
                var draw = player.PlayerCombatState!.DrawPile;
                var discard = player.PlayerCombatState.DiscardPile;
                var opening = draw.Cards.Select(c => CombatCards.Info(c).Address).ToArray();
                var openingKeys = draw.Cards.ToDictionary(c => CombatCards.Info(c).Address, c => CombatCards.Info(c).Key);
                expectedOpening ??= opening;
                expectedKeys ??= openingKeys;
                Assert(opening.SequenceEqual(expectedOpening.Where(a => branch == 0 || a != removedAddress)) &&
                    openingKeys.All(p => expectedKeys[p.Key] == p.Value),
                    $"act {act + 1} opening shuffle and fixed selection keys preserve removed-card positions and ignore deck order");
                Assert(overrideCalled && scope.Random.Position(scope.For("shuffle")) == 1 && shuffleRng.Equals(run.Rng.Shuffle.ToSerializable()),
                    "opening shuffle commits once and preserves the game's test override and vanilla RNG");
                if (branch == 2) // The removed snapshot was the only Innate card in the opening field.
                {
                    RunManager.Instance.CleanUp();
                    continue;
                }
                var combatInnate = draw.Cards.Single(c => c.DeckVersion == innate);
                var innateKeys = CombatShuffle.Keys(draw.Cards, act, true);
                Assert(CombatCards.Info(combatInnate).Key != innateKeys[combatInnate] &&
                    (act == 0 ? CombatCards.Info(combatInnate).Key == CardOccurrences.Info(innate).Key :
                        CombatCards.Info(combatInnate).Key.Contains("second", StringComparison.Ordinal)),
                    "Innate uses its own opening priority while selection retains the ordinary source/rank key");
                var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
                AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);

                // Compare the same live multiset at the same second-shuffle ordinal in both runs.
                var live = draw.Cards.Where(c => CombatCards.Info(c).Address != removedAddress).ToArray();
                if (branch == 0)
                    removed = draw.Cards.Single(c => CombatCards.Info(c).Address == removedAddress);
                if (branch == 0)
                    removed.RemoveFromCurrentPile();
                foreach (var card in branch == 0 ? live : live.Reverse())
                {
                    card.RemoveFromCurrentPile();
                    discard.AddInternal(card);
                }
                var fixedKeys = live.ToDictionary(c => c, c => CombatCards.Info(c).Key);
                await CardPileCmd.Shuffle(new ThrowingPlayerChoiceContext(), player);
                var actual = string.Join("|", draw.Cards.Select(c => CombatCards.Info(c).Address));
                expectedReshuffle ??= actual;
                Assert(actual == expectedReshuffle && fixedKeys.All(p => CombatCards.Info(p.Key).Key == p.Value),
                    "reshuffling ignores discard order while retaining fixed selection keys");

                var generated = CombatGeneration.Generate(player, [ModelDb.Card<Anger>()], 1, false, typeof(Stoke)).Single();
                var entry = CardOccurrences.Info(generated).Address;
                await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, player);
                Assert(CombatCards.Info(generated).Key == entry && CombatCards.Info(generated).Address == entry,
                    "admitted random generation retains its original entry key");
                var copy = generated.CreateClone();
                await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, player);
                Assert(CombatCards.Info(copy).Key != entry && !CardOccurrences.Info(copy).SourceKey && copy.CloneOf == generated,
                    "combat copies receive fresh occurrence state while preserving vanilla copy relationships");
                var rawDeckCopy = combat.CloneCard(innate);
                await CardPileCmd.AddGeneratedCardToCombat(rawDeckCopy, PileType.Hand, player);
                Assert(CombatCards.Info(rawDeckCopy).Address == CardOccurrences.Info(innate).Address,
                    "cloning from the permanent deck outside population still recognizes the source pile");
                rawDeckCopy.RemoveFromCurrentPile();
                var tied = new[] { generated, copy }.OrderBy(c => CombatCards.Info(c).Address, StringComparer.Ordinal).ToArray();
                var tiedKeys = CombatShuffle.Keys(tied.Reverse(), 2, false);
                Assert(tiedKeys[tied[0]] == RandomAddress.For("shuffle key", "second").At(0).Key &&
                    tiedKeys[tied[1]] == RandomAddress.For("shuffle key", "second").At(1).Key,
                    "Act 3 reverses usefulness ranks while retaining ascending occurrence tie-breaks");
                var upgradedCopy = generated.CreateClone();
                await CardPileCmd.AddGeneratedCardToCombat(upgradedCopy, PileType.Hand, player);
                CardCmd.Upgrade(upgradedCopy);
                CardModel[] variants = [copy, upgradedCopy, generated];
                var batch = new CardOrder.CombatRankBatch();
                var reference = variants.OrderByDescending(c => batch.ShuffleRank(c))
                    .ThenBy(c => CardOccurrences.Info(c).Address, StringComparer.Ordinal).ToArray();
                foreach (var first in variants)
                {
                    var keys = CombatShuffle.Keys(variants.Prepend(first).Distinct(), 2, false);
                    Assert(reference.Select((c, i) => keys[c] == RandomAddress.For("shuffle key", "second").At(i).Key).All(x => x),
                        "equal copies retain address order and an upgraded copy keeps its own rank with any representative");
                }
                var token1 = combat.CreateCard<Soul>(player);
                var token2 = combat.CreateCard<Soul>(player);
                await CardPileCmd.AddGeneratedCardToCombat(token1, PileType.Hand, player);
                await CardPileCmd.AddGeneratedCardToCombat(token2, PileType.Draw, player);
                var tokenCounter = scope.For("token occurrence", "SOUL");
                var failed = combat.CreateCard<Soul>(player);
                failed.RemoveFromState();
                await CardPileCmd.Add(failed, draw);
                Assert(CombatCards.Info(token1).Key == tokenCounter.At(0).Key && CombatCards.Info(token2).Key == tokenCounter.At(1).Key &&
                    scope.Random.Position(tokenCounter) == 2 && CombatCards.Find(failed) == null,
                    "fixed tokens share identity counters across destinations and rejected admissions consume none");
                var beforeMove = CombatCards.Info(token1);
                await CardPileCmd.Add(token1, discard);
                CardCmd.Upgrade(token1);
                Assert(ReferenceEquals(beforeMove, CombatCards.Info(token1)), "movement and upgrades preserve combat occurrence state");

                var dazed = combat.CreateCard<Dazed>(player);
                var burn = combat.CreateCard<Burn>(player);
                var grant = scope.For("card grant", "direct grant");
                var grantOrdinal = scope.Random.Position(grant);
                await CardPileCmd.AddGeneratedCardsToCombat(branch == 0 ? [dazed, burn] : [burn, dazed], PileType.Discard, player);
                Assert(CombatCards.Info(burn).Address == grant.At(grantOrdinal).At(0).Key &&
                    CombatCards.Info(dazed).Address == grant.At(grantOrdinal).At(1).Key &&
                    CombatCards.Info(burn).CreationBatch == CombatCards.Info(dazed).CreationBatch,
                    "unordered mixed-identity grants assign copy indices in processing order across input order");

                var barrier = combat.CreateCard<FlameBarrier>(player);
                var shrug = combat.CreateCard<ShrugItOff>(player);
                Assert(CardOrder.Combat.Compare(barrier, shrug) > 0, "generated skill fixture starts in energy-cost order");
                var corruption = (CorruptionPower)ModelDb.Power<CorruptionPower>().ToMutable();
                AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(corruption, player.Creature);
                player.Creature.ApplyPowerInternal(corruption);
                grantOrdinal = scope.Random.Position(grant);
                await CardPileCmd.AddGeneratedCardsToCombat([shrug, barrier], PileType.Discard, player);
                Assert(CombatCards.Info(barrier).Address == grant.At(grantOrdinal).At(0).Key &&
                    CombatCards.Info(shrug).Address == grant.At(grantOrdinal).At(1).Key,
                    "pending generated batches include shared Corruption costs before assigning occurrence indices");
                player.Creature.RemovePowerInternal(corruption);

                var nightmare = (NightmarePower)ModelDb.Power<NightmarePower>().ToMutable();
                AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(nightmare, player.Creature);
                AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Amount)).SetValue(nightmare, 2);
                nightmare.SetSelectedCard(generated);
                var oldCards = player.PlayerCombatState.AllPiles.SelectMany(p => p.Cards).ToHashSet();
                await nightmare.BeforeHandDraw(player, new ThrowingPlayerChoiceContext(), combat);
                var copies = player.PlayerCombatState.AllPiles.SelectMany(p => p.Cards).Where(c => !oldCards.Contains(c)).ToArray();
                Assert(copies.Length == 2 && copies.Select(c => CombatCards.Info(c).CreationBatch).Distinct().Count() == 1 &&
                    scope.Random.Position(scope.For("card grant", nightmare.Id.Entry)) == 1 &&
                    copies.Select(c => CombatCards.Info(c).Address).Distinct().Count() == 2,
                    "Nightmare's sequential copies share one committed source grant and creation batch");

                foreach (var pile in player.PlayerCombatState.AllPiles)
                    foreach (var card in pile.Cards.ToArray())
                        card.RemoveFromCurrentPile();
                var before = scope.Random.Position(scope.For("shuffle"));
                await CardPileCmd.Shuffle(new ThrowingPlayerChoiceContext(), player);
                draw.AddInternal(token1);
                await CardPileCmd.Shuffle(new ThrowingPlayerChoiceContext(), player);
                Assert(scope.Random.Position(scope.For("shuffle")) == before + 2 && shuffleRng.Equals(run.Rng.Shuffle.ToSerializable()),
                    "empty and singleton reshuffles each commit one opportunity without advancing vanilla shuffle RNG");
                RunManager.Instance.CleanUp();
            }
        }
    }
}
