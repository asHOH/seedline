using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCombatGeneration()
    {
        string? expected = null;
        string? transformed = null;
        string? entropyResult = null;
        for (var branch = 0; branch < 2; branch++)
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
            combat.AddPlayer(player);
            combat.Encounter!.GenerateMonstersWithSlots(run);
            foreach (var (monster, slot) in combat.Encounter.MonstersWithSlots)
                combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
            CombatManager.Instance.SetUpCombat(combat);
            var scope = CombatStateScope.Require(combat);
            var pool = CombatGeneration.Pool(player, player.Character.CardPool).ToArray();
            var vanilla = run.Rng.CombatCardGeneration.ToSerializable();
            var initial = CombatGeneration.Generate(player, pool, 1, false, typeof(Stoke));
            if (branch == 1)
                CombatGeneration.Generate(player, pool, 7, true, typeof(Discovery));
            var later = CombatGeneration.Generate(player, pool, 3, false, typeof(Stoke));
            var sequence = string.Join("|", initial.Concat(later).Select(c => CardOccurrences.Info(c).Address + c.Id));
            expected ??= sequence;
            Assert(sequence == expected && vanilla.Equals(run.Rng.CombatCardGeneration.ToSerializable()),
                "ordinary combat generation is isolated from free-this-turn draws and vanilla RNG");
            var distinct = CombatGeneration.Generate(player, pool, pool.Length + 1, true, typeof(Discovery));
            Assert(distinct.Count == pool.Length && distinct.Select(c => c.Id).Distinct().Count() == pool.Length,
                "distinct combat factories preserve batch exclusions and cap output at the eligible count");
            var repeats = CombatGeneration.Generate(player, [ModelDb.Card<Anger>()], 3, false, typeof(Stoke));
            Assert(repeats.All(c => c is Anger) && repeats.Select(c => CardOccurrences.Info(c).Address).Distinct().Count() == 3,
                "repeat-allowing generation assigns distinct sequence entries to repeated identities");
            var a = combat.CreateCard<Anger>(player);
            var b = combat.CreateCard<Bash>(player);
            player.PlayerCombatState!.Hand.AddInternal(a);
            player.PlayerCombatState.Hand.AddInternal(b);
            CardModel[] inputs = branch == 0 ? [a, b] : [b, a];
            var prepared = PermanentGeneration.Prepare(inputs.Select(c => new CardTransformation(c)));
            var mapping = string.Join("|", prepared.OrderBy(t => t.Original.Id.Entry).Select(t =>
                t.Original.Id + ":" + t.Replacement!.Id + ":" + CardOccurrences.Info(t.Replacement).Address));
            transformed ??= mapping;
            Assert(mapping == transformed && prepared.All(t => t.Replacement!.Id != t.Original.Id),
                "combat transformation batches preserve identity results across selection order and exclude originals");
            Assert(prepared.All(t => CardOccurrences.Info(t.Replacement!).Address.Contains("ordinary costs", StringComparison.Ordinal)),
                "Entropy replacements draw from ordinary combat generation entries");
            var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
            AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);
            await CardCmd.Transform(prepared, run.Rng.CombatCardGeneration);
            Assert(prepared.All(t => t.Replacement!.Pile?.Type == PileType.Hand && t.Original.Pile == null),
                "prepared combat transformations use the game's replacement and pile-movement path");

            var ocarina = (Ocarina)ModelDb.Relic<Ocarina>().ToMutable();
            player.AddRelicInternal(ocarina);
            var melody = scope.For("card generation", "ordinary costs", "melodies");
            var before = player.PlayerCombatState.Hand.Cards.ToArray();
            await ocarina.BeforeHandDraw(player, new ThrowingPlayerChoiceContext(), combat);
            var added = player.PlayerCombatState.Hand.Cards.Except(before).Single();
            Assert(added.Tags.Any(t => t is CardTag.Wah or CardTag.Cho or CardTag.Ki) &&
                scope.Cards.Supplies[melody.Key].Generated == 1 && CardOccurrences.Info(added).Address == melody.At(0).Key,
                "Ocarina's actual first-turn hook uses its dedicated combined melody supply");
            player.PlayerCombatState.IncrementTurnNumber();
            await ocarina.BeforeHandDraw(player, new ThrowingPlayerChoiceContext(), combat);
            Assert(scope.Cards.Supplies[melody.Key].Generated == 1, "later turns preserve Ocarina's first-turn timing");

            var mythic = (MythicFormPower)ModelDb.Power<MythicFormPower>().ToMutable();
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(mythic, player.Creature);
            player.Creature.ApplyPowerInternal(mythic);
            mythic.SetAmount(2);
            mythic.IncrementUpgraded();
            before = player.PlayerCombatState.Hand.Cards.ToArray();
            var selection = run.Rng.CombatCardSelection.ToSerializable();
            await mythic.BeforeHandDraw(player, new ThrowingPlayerChoiceContext(), combat);
            var tarot = player.PlayerCombatState.Hand.Cards.Except(before).ToArray();
            Assert(tarot.Length == 2 && tarot[0].IsUpgraded && !tarot[1].IsUpgraded &&
                tarot.All(c => CardOccurrences.Info(c).Address.Contains("Tarot", StringComparison.Ordinal)) &&
                selection.Equals(run.Rng.CombatCardSelection.ToSerializable()),
                "Mythic Form uses Tarot entries while preserving ordered upgrades and selection RNG");
            var tarotKeys = CombatShuffle.Keys(tarot.Reverse(), 0, false);
            var tarotOrder = tarot.OrderBy(c => c, CardOrder.Combat).ToArray();
            Assert(tarot.Select(c => CombatCards.Info(c).CreationBatch).Distinct().Count() == 1 &&
                tarotKeys[tarotOrder[0]] == RandomAddress.For("shuffle key", "second").At(0).Key &&
                tarotKeys[tarotOrder[1]] == RandomAddress.For("shuffle key", "second").At(1).Key,
                "Mythic Form's sequential grants share a creation batch ranked in processing order");
            var entropyA = combat.CreateCard<Anger>(player);
            var entropyB = combat.CreateCard<Bash>(player);
            var hand = player.PlayerCombatState.Hand;
            var firstIndex = hand.Cards.Count;
            hand.AddInternal(entropyA);
            hand.AddInternal(entropyB);
            var entropy = (EntropyPower)ModelDb.Power<EntropyPower>().ToMutable();
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(entropy, player.Creature);
            entropy.SetAmount(2);
            var selector = new TestCardSelector();
            selector.PrepareToSelect(branch == 0 ? new CardModel[] { entropyA, entropyB } : [entropyB, entropyA]);
            using (CardSelectorStack.SetTestSelector(selector))
                await entropy.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), player);
            var entropyCards = hand.Cards.Skip(firstIndex).ToArray();
            var entropyMapping = string.Join("|", entropyCards.Select(c => c.Id + ":" + CardOccurrences.Info(c).Address));
            entropyResult ??= entropyMapping;
            Assert(entropyCards.Length == 2 && entropyA.Pile == null && entropyB.Pile == null && entropyMapping == entropyResult,
                "Entropy's actual selection hook prepares and consumes sequential replacements independently of click order");
            Assert(entropyCards.Select(c => CombatCards.Info(c).CreationBatch).Distinct().Count() == 1,
                "Entropy's sequential replacements share one creation batch");
            Assert(vanilla.Equals(run.Rng.CombatCardGeneration.ToSerializable()), "covered generation leaves run-wide combat RNG unchanged");
            RunManager.Instance.CleanUp();
        }
    }
}
