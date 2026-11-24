using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCombatShuffle()
    {
        await CheckShufflePriorities();
        foreach (var size in new[] { 2000, 2001 })
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            var filler = RandomAddress.For("deck size fixture");
            while (player.Deck.Cards.Count < size)
            {
                var card = run.CreateCard<StrikeIronclad>(player);
                CardOccurrences.Create(card, filler.At(player.Deck.Cards.Count).Key);
                player.Deck.AddInternal(card, silent: true);
            }
            var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
            combat.AddPlayer(player);
            combat.Encounter!.GenerateMonstersWithSlots(run);
            foreach (var (monster, slot) in combat.Encounter.MonstersWithSlots)
                combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
            var vanilla = run.Rng.Shuffle.ToSerializable();
            var referenceRng = new Rng(vanilla);
            var expectedOpening = player.Deck.Cards.ToList().UnstableShuffle(referenceRng);
            CombatManager.Instance.SetUpCombat(combat);
            var scope = CombatStateScope.Require(combat);
            var large = size > 2000;
            if (!large)
                Assert(DecisionRecords("shuffle").Last()["data"]!["order"]!.AsArray().Count == size,
                    "large controlled shuffle diagnostics retain every logical occurrence and assigned key");
            Assert(CombatCards.UseVanilla(player) == large && scope.Random.Position(scope.For("shuffle")) == (large ? 0 : 1) &&
                vanilla.Equals(run.Rng.Shuffle.ToSerializable()) == !large, $"a {size}-card deck opens with the policy's shuffle RNG");
            if (large)
            {
                Assert(player.PlayerCombatState!.DrawPile.Cards.Select(c => c.DeckVersion).SequenceEqual(expectedOpening) &&
                    referenceRng.ToSerializable().Equals(run.Rng.Shuffle.ToSerializable()),
                    "large-deck opening preserves exact vanilla copy order and RNG state");
                player.Deck.RemoveInternal(player.Deck.Cards[^1]);
                Assert(CombatCards.UseVanilla(player), "shrinking to 2000 after entry preserves the combat's vanilla selection and shuffle mode");
                await CheckLargeDeckActions(combat);
            }
            else
            {
                var extra = run.CreateCard<StrikeIronclad>(player);
                CardOccurrences.Create(extra, filler.At(size).Key);
                player.Deck.AddInternal(extra, silent: true);
                Assert(!CombatCards.UseVanilla(player), "growing past 2000 after entry preserves controlled mode for this combat");
            }
            CombatManager.Instance.Reset(true);
            var next = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
            next.AddPlayer(player);
            CombatManager.Instance.SetUpCombat(next);
            Assert(CombatCards.UseVanilla(player) == !large,
                "the next combat re-evaluates deck size after either direction of threshold crossing");
            RunManager.Instance.CleanUp();
        }
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
                CombatFixture.SetInProgress(true);

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

    private static async Task CheckLargeDeckActions(CombatState combat)
    {
        var player = combat.Players[0];
        var pcs = player.PlayerCombatState!;
        var scope = CombatStateScope.Require(combat);
        CombatFixture.SetInProgress(true);
        var context = new ThrowingPlayerChoiceContext();
        foreach (var card in pcs.DrawPile.Cards.ToArray())
        {
            card.RemoveFromCurrentPile();
            pcs.DiscardPile.AddInternal(card);
        }
        var rng = new Rng(player.RunState.Rng.Shuffle.ToSerializable());
        var expected = pcs.DiscardPile.Cards.ToList().StableShuffle(rng);
        await CardPileCmd.Shuffle(context, player);
        Assert(pcs.DrawPile.Cards.SequenceEqual(expected) && rng.ToSerializable().Equals(player.RunState.Rng.Shuffle.ToSerializable()) &&
            scope.Random.Position(scope.For("shuffle")) == 0,
            "large-deck reshuffle preserves exact vanilla order and RNG consumption after the permanent deck shrinks");

        RunManager.Instance.ActionExecutor.Pause();
        await CombatFixture.StartTurn();
        foreach (var card in pcs.Hand.Cards.ToArray())
        {
            card.RemoveFromCurrentPile();
            pcs.DiscardPile.AddInternal(card);
        }
        var defends = pcs.AllCards.OfType<DefendIronclad>().Cast<CardModel>().ToArray();
        foreach (var card in defends)
        {
            card.RemoveFromCurrentPile();
            pcs.Hand.AddInternal(card);
        }
        var keys = defends.Select(c => CombatCards.Info(c).Key).ToArray();
        var selector = new TestCardSelector();
        using (CardSelectorStack.SetTestSelector(selector))
        {
            selector.PrepareToSelect([defends[^1]]);
            var chosen = (await CardSelectCmd.FromHandForDiscard(context, player,
                new(CardSelectorPrefs.DiscardSelectionPrompt, 1), null, ModelDb.Card<Prepared>())).Single();
            Assert(chosen == defends[^1] && defends.Select(c => CombatCards.Info(c).Key).SequenceEqual(keys) && scope.Selection.Clocks.Count == 0,
                "large-deck manual choices preserve clicked logical copies without routing clocks");
        }
        pcs.Energy = 3;
        await ((Task)AccessTools.Method(typeof(PlayCardAction), "ExecuteAction")
            .Invoke(new PlayCardAction(defends[0], null), null)!).WaitAsync(TimeSpan.FromSeconds(15));
        Assert(defends[0].Pile == pcs.DiscardPile && pcs.Energy == 2 &&
            defends.Select(c => CombatCards.Info(c).Key).SequenceEqual(keys) && scope.Selection.Clocks.Count == 0,
            "large-deck manual play spends resources without routing logical copies");

        var selectionRng = new Rng(player.RunState.Rng.CombatCardSelection.ToSerializable());
        var exhausted = selectionRng.NextItem(pcs.Hand.Cards);
        var grit = combat.CreateCard<TrueGrit>(player);
        pcs.PlayPile.AddInternal(grit);
        await (Task)AccessTools.Method(typeof(TrueGrit), "OnPlay").Invoke(grit, [context, new CardPlay
        {
            Card = grit, Player = player, Target = null, ResultPile = PileType.Discard,
            Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
            IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
        }])!;
        Assert(exhausted!.Pile == pcs.ExhaustPile && scope.Selection.Clocks.Count == 0 &&
            selectionRng.ToSerializable().Equals(player.RunState.Rng.CombatCardSelection.ToSerializable()),
            "large-deck random card selection retains the exact vanilla target and RNG state");

        var theftCandidates = pcs.DrawPile.Cards.OfType<StrikeIronclad>()
            .Where(c => player.Deck.Cards.Contains(c.DeckVersion!)).Take(2).Cast<CardModel>().ToArray();
        foreach (var card in pcs.DrawPile.Cards.Concat(pcs.DiscardPile.Cards).Except(theftCandidates).ToArray())
        {
            card.RemoveFromCurrentPile();
            pcs.ExhaustPile.AddInternal(card);
        }
        var stolen = selectionRng.NextItem(theftCandidates)!;
        var hopper = ModelDb.Monster<ThievingHopper>().ToMutable();
        combat.AddCreature(combat.CreateCreature(hopper, CombatSide.Enemy, null));
        await (Task)AccessTools.Method(typeof(ThievingHopper), "ThieveryMove")
            .Invoke(hopper, [new[] { player.Creature }])!;
        Assert(!player.Deck.Cards.Contains(stolen.DeckVersion!) && theftCandidates.Single(c => c != stolen).Pile == pcs.DrawPile &&
            selectionRng.ToSerializable().Equals(player.RunState.Rng.CombatCardSelection.ToSerializable()) && scope.Selection.Clocks.Count == 0,
            "large-deck Hopper theft uses the vanilla per-copy selection and shared RNG position");
    }
}
