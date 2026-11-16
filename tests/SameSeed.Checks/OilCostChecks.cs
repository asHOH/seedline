using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace SameSeed.Checks;

public static partial class Checks
{
    private static async Task CheckOilCosts()
    {
        string? expected = null;
        for (var branch = 0; branch < 2; branch++)
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
            if (branch == 1)
                RunCheckpoints.Require(run).Random.Advance(RandomAddress.For("combat", combat.Encounter!.RoomType.ToString()).At(run.CurrentActIndex));
            combat.AddPlayer(player);
            combat.Encounter!.GenerateMonstersWithSlots(run);
            foreach (var (monster, slot) in combat.Encounter.MonstersWithSlots)
                combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
            CombatManager.Instance.SetUpCombat(combat);
            var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
            AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);
            await CreatureCmd.SetMaxAndCurrentHp(combat.Enemies[0], 1000);
            var pcs = player.PlayerCombatState!;
            foreach (var card in pcs.DrawPile.Cards.ToArray())
                card.RemoveFromCurrentPile();
            var context = new ThrowingPlayerChoiceContext();
            var state = RunCheckpoints.Require(run);
            var scope = CombatStateScope.Require(combat);
            CardModel[] batch = [combat.CreateCard<Anger>(player), combat.CreateCard<PommelStrike>(player), combat.CreateCard<Bash>(player)];
            await CardPileCmd.AddGeneratedCardsToCombat(branch == 0 ? batch : batch.Reverse(), PileType.Hand, player);
            Assert(batch.Select(c => HandEntries.Info(c).Ordinal).Distinct().Count() == 1,
                "generated singleton additions share the unordered outer batch entry ordinal");
            var frozen = batch.ToDictionary(c => c, HandEntries.Info);
            if (branch == 1)
                foreach (var card in pcs.Hand.Cards.ToArray())
                    pcs.Hand.MoveToTopInternal(card);
            Assert(batch.All(c => ReferenceEquals(HandEntries.Info(c), frozen[c])),
                "hand rearrangement and retained presence preserve the frozen entry record");
            CardModel[] draws = [combat.CreateCard<StrikeIronclad>(player), combat.CreateCard<DefendIronclad>(player),
                combat.CreateCard<Bash>(player), combat.CreateCard<Anger>(player), combat.CreateCard<Headbutt>(player),
                combat.CreateCard<Whirlwind>(player), combat.CreateCard<Wound>(player)];
            await CardPileCmd.AddGeneratedCardsToCombat(draws, PileType.Draw, player);
            Assert(draws.All(c => HandEntries.Find(c) == null), "draw-pile generation assigns no hand entry");
            var vanilla = run.Rng.CombatEnergyCosts.ToSerializable();
            var oil = (SneckoOil)ModelDb.Potion<SneckoOil>().ToMutable();
            player.AddPotionInternal(oil);
            oil.RemoveBeforeUse();
            await (Task)AccessTools.Method(typeof(SneckoOil), "OnUse").Invoke(oil, [context, player.Creature])!;
            Assert(pcs.Hand.Cards.Count == 10 && draws.Select(c => HandEntries.Info(c).Ordinal).Distinct().Count() == 7 &&
                draws.Select(c => HandEntries.Info(c).Ordinal).SequenceEqual(draws.Select(c => HandEntries.Info(c).Ordinal).Order()),
                "Oil's seven sequential draws receive distinct hand entries in draw order");
            var eligible = pcs.Hand.Cards.Where(c => !c.EnergyCost.CostsX && c.EnergyCost.GetWithModifiers(CostModifiers.None) >= 0).ToArray();
            var random = new RandomState(state.Seed);
            foreach (var group in eligible.GroupBy(CombatCosts.Category))
            {
                var address = HandEntries.OilAddress(oil, group.Key);
                foreach (var card in group.OrderBy(c => HandEntries.Info(c).Ordinal).ThenBy(c => HandEntries.Info(c).Rank))
                    Assert(card.EnergyCost.GetWithModifiers(CostModifiers.Local) == random.NextIntInclusive(address, 0, 3),
                        "Oil assigns each category in frozen hand-entry order");
                Assert(state.Random.Position(address) == group.Count(), "Oil consumes exactly one category entry per eligible card");
            }
            Assert(eligible.Length == 8 && vanilla.Equals(run.Rng.CombatEnergyCosts.ToSerializable()),
                "Oil skips X and negative-cost cards and preserves vanilla energy RNG");
            var signature = string.Join("|", batch.Concat(draws).Select(c => c.EnergyCost.GetWithModifiers(CostModifiers.Local)));
            expected ??= signature;
            Assert(signature == expected, "Oil assignments replay across different combat opportunities and reversed unordered generation/hand display order");
            var overflow = combat.CreateCard<Anger>(player);
            var entryPosition = scope.Random.Position(scope.For("hand entry"));
            await CardPileCmd.AddGeneratedCardToCombat(overflow, PileType.Hand, player);
            Assert(overflow.Pile == pcs.DiscardPile && HandEntries.Find(overflow) == null &&
                scope.Random.Position(scope.For("hand entry")) == entryPosition,
                "full-hand redirection creates no entry position");

            var old = HandEntries.Info(batch[0]);
            await CardPileCmd.Add(batch[0], PileType.Discard);
            await CardPileCmd.Add(batch[0], PileType.Hand);
            Assert(HandEntries.Info(batch[0]).Ordinal > old.Ordinal,
                "leaving and returning refreshes the hand-entry history");
            foreach (var card in pcs.Hand.Cards.ToArray())
                card.RemoveFromCurrentPile();
            foreach (var card in pcs.DiscardPile.Cards.ToArray())
                card.RemoveFromCurrentPile();
            var first = combat.CreateCard<Anger>(player);
            var second = combat.CreateCard<Anger>(player);
            pcs.DiscardPile.AddInternal(first);
            pcs.DiscardPile.AddInternal(second);
            var all = combat.CreateCard<AllForOne>(player);
            pcs.PlayPile.AddInternal(all);
            CardPlay play = new()
            {
                Card = all,
                Player = player,
                Target = combat.Enemies[0],
                ResultPile = PileType.Discard,
                Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                IsAutoPlay = false,
                PlayIndex = 0,
                PlayCount = 1
            };
            await (Task)AccessTools.Method(typeof(AllForOne), "OnPlay").Invoke(all, [context, play])!;
            var earlier = HandEntries.Info(first);
            var later = HandEntries.Info(second);
            Assert(earlier.Ordinal < later.Ordinal, "All for One preserves its explicit sequential return order");
            CombatCards.Reassociate([first, second], [second, first]);
            Assert(ReferenceEquals(HandEntries.Info(first), later) && ReferenceEquals(HandEntries.Info(second), earlier),
                "combat routing moves hand-entry history with the logical occurrences");
            var forced = (SneckoOil)ModelDb.Potion<SneckoOil>().ToMutable();
            player.AddPotionInternal(forced);
            forced.RemoveBeforeUse();
            forced.TestEnergyCostOverride = 2;
            await (Task)AccessTools.Method(typeof(SneckoOil), "OnUse").Invoke(forced, [context, player.Creature])!;
            Assert(PotionEffects.EffectOrdinal(forced) == 1 && state.Random.Position(HandEntries.OilAddress(forced, "others")) == 0 &&
                pcs.Hand.Cards.All(c => c.EnergyCost.GetWithModifiers(CostModifiers.Local) == 2),
                "actual Oil test override preserves forced values without consuming cost entries");
            foreach (var card in pcs.Hand.Cards.ToArray())
                card.RemoveFromCurrentPile();
            var cosmic = (CosmicConcoction)ModelDb.Potion<CosmicConcoction>().ToMutable();
            player.AddPotionInternal(cosmic);
            cosmic.RemoveBeforeUse();
            await (Task)AccessTools.Method(typeof(CosmicConcoction), "OnUse").Invoke(cosmic, [context, player.Creature])!;
            Assert(pcs.Hand.Cards.Count == 3 && pcs.Hand.Cards.Select(c => HandEntries.Info(c).Ordinal).Distinct().Count() == 1,
                "Cosmic Concoction's sequential generated admissions share one Oil hand-entry ordinal");
            foreach (var card in pcs.Hand.Cards.ToArray())
                card.RemoveFromCurrentPile();
            var sadnessA = combat.CreateCard<Sadness>(player);
            var sadnessB = combat.CreateCard<Sadness>(player);
            pcs.Hand.AddInternal(sadnessA);
            pcs.Hand.AddInternal(sadnessB);
            var ecstasy = (EcstasyPower)ModelDb.Power<EcstasyPower>().ToMutable();
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(ecstasy, player.Creature);
            ecstasy.SetAmount(2);
            await ecstasy.AfterPlayerTurnStart(context, player);
            Assert(pcs.Hand.Cards.Count == 2 && pcs.Hand.Cards.All(c => c is Joy) &&
                pcs.Hand.Cards.Select(c => HandEntries.Info(c).Ordinal).Distinct().Count() == 1,
                "direct transformation admissions share their source creation batch for Oil assignment");
            foreach (var card in pcs.Hand.Cards.ToArray())
                card.RemoveFromCurrentPile();
            CardModel[] retrieved = [combat.CreateCard<Anger>(player), combat.CreateCard<Headbutt>(player),
                combat.CreateCard<Bash>(player), combat.CreateCard<DefendIronclad>(player)];
            await CardPileCmd.AddGeneratedCardsToCombat(retrieved, PileType.Draw, player);
            var stratagem = (StratagemPower)ModelDb.Power<StratagemPower>().ToMutable();
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(stratagem, player.Creature);
            stratagem.SetAmount(2);
            var selector = new TestCardSelector();
            selector.PrepareToSelect(branch == 0 ? retrieved.Take(2) : retrieved.Take(2).Reverse());
            using (CardSelectorStack.SetTestSelector(selector))
                await stratagem.AfterShuffle(context, player);
            var retrievalEntry = HandEntries.Info(retrieved[0]).Ordinal;
            Assert(pcs.Hand.Cards.Count == 2 && HandEntries.Info(retrieved[1]).Ordinal == retrievalEntry,
                "Stratagem's singleton returns form one unordered entry batch across click order");
            await CardPileCmd.Draw(context, 2, player);
            Assert(HandEntries.Info(retrieved[2]).Ordinal > retrievalEntry &&
                HandEntries.Info(retrieved[3]).Ordinal > HandEntries.Info(retrieved[2]).Ordinal,
                "Stratagem restores its source context before subsequent sequential draws");
            RunManager.Instance.CleanUp();
        }
    }
}
