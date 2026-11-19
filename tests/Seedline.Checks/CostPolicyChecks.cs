using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCostPolicy()
    {
        (CharacterModel Character, CardModel[] Starters)[] characters =
        [
            (ModelDb.Character<Ironclad>(), [ModelDb.Card<StrikeIronclad>(), ModelDb.Card<DefendIronclad>(), ModelDb.Card<Bash>()]),
            (ModelDb.Character<Silent>(), [ModelDb.Card<StrikeSilent>(), ModelDb.Card<DefendSilent>(), ModelDb.Card<Neutralize>(), ModelDb.Card<Survivor>()]),
            (ModelDb.Character<Defect>(), [ModelDb.Card<StrikeDefect>(), ModelDb.Card<DefendDefect>(), ModelDb.Card<Zap>(), ModelDb.Card<Dualcast>()]),
            (ModelDb.Character<Necrobinder>(), [ModelDb.Card<StrikeNecrobinder>(), ModelDb.Card<DefendNecrobinder>(), ModelDb.Card<Bodyguard>(), ModelDb.Card<Unleash>()]),
            (ModelDb.Character<Regent>(), [ModelDb.Card<StrikeRegent>(), ModelDb.Card<DefendRegent>(), ModelDb.Card<FallingStar>(), ModelDb.Card<Venerate>()]),
            (ModelDb.Character<Fool>(), [ModelDb.Card<StrikeFool>(), ModelDb.Card<DefendFool>(), ModelDb.Card<FoolishShot>()])
        ];
        string[] categories = ["starter Strikes", "starter Defends", "other Basic 1", "other Basic 2"];
        foreach (var (character, starters) in characters)
        {
            var run = RunState.CreateForNewRun([Player.CreateForNewRun(character, UnlockState.all, 1)],
                ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), [], GameMode.Standard, false, 7, "COSTCATEGORIES");
            RunManager.Instance.SetUpNewSingleplayer(run, true);
            LocalContext.NetId = RunManager.Instance.NetService.NetId;
            var player = run.Players[0];
            // Removing the first other Basic must not renumber the second category.
            player.Deck.Cards.First(c => c.Id == starters[2].Id).RemoveFromCurrentPile();
            for (var battle = 0; battle < 2; battle++)
            {
                var combat = CombatFixture.CreateActiveCombat(run);
                var scope = CombatStateScope.Require(combat);
                var context = new ThrowingPlayerChoiceContext();
                foreach (var card in player.PlayerCombatState!.AllCards.ToArray()) card.RemoveFromCurrentPile();
                RelicModel eye = battle == 0 ? ModelDb.Relic<SneckoEye>().ToMutable() : ModelDb.Relic<FakeSneckoEye>().ToMutable();
                player.AddRelicInternal(eye);
                await eye.BeforeCombatStart();
                Assert(player.Creature.GetPower<ConfusedPower>() != null && eye.ModifyHandDraw(player, 5) == (battle == 0 ? 7 : 5),
                    "Snecko Eye and Fake Snecko Eye apply Confused while preserving their different draw bonuses");
                var vanilla = run.Rng.CombatEnergyCosts.ToSerializable();
                for (var i = 0; i < starters.Length; i++)
                {
                    var card = combat.CreateCard(starters[i], player);
                    var address = RandomAddress.For("Confused cost", categories[i]).With(scope.Address.Key);
                    for (var draw = 0; draw < 2; draw++)
                    {
                        if (draw == 1) CardCmd.Upgrade(card);
                        card.RemoveFromCurrentPile();
                        player.PlayerCombatState.DrawPile.AddInternal(card);
                        await CardPileCmd.Draw(context, 1, player);
                        Assert(CombatCosts.Category(card) == categories[i] && card.EnergyCost.GetWithModifiers(CostModifiers.Local) ==
                            AddressRandom.IntInclusive(scope.Seed, address.At(draw), 0, 3) && scope.Random.Position(address) == draw + 1,
                            $"{character.Id.Entry}, battle {battle + 1}: {card.Id.Entry} keeps its original starter category through removal, upgrade and redraw");
                    }
                    card.RemoveFromCurrentPile();
                }
                var nonstarter = combat.CreateCard<PommelStrike>(player);
                player.PlayerCombatState.DrawPile.AddInternal(nonstarter);
                await CardPileCmd.Draw(context, 1, player);
                var others = RandomAddress.For("Confused cost", "others").With(scope.Address.Key);
                Assert(CombatCosts.Category(nonstarter) == "others" && nonstarter.EnergyCost.GetWithModifiers(CostModifiers.Local) ==
                    AddressRandom.IntInclusive(scope.Seed, others.At(0), 0, 3), "nonstarter Strike-tagged attacks use Others, not starter Strikes");
                CardModel[] skipped = [combat.CreateCard<Whirlwind>(player), combat.CreateCard<Wound>(player)];
                await CardPileCmd.AddGeneratedCardsToCombat(skipped, PileType.Draw, player);
                await CardPileCmd.Draw(context, 2, player);
                Assert(scope.Random.Position(others) == 1 && !skipped.Any(c => c.EnergyCost.HasLocalModifiers) &&
                    vanilla.Equals(run.Rng.CombatEnergyCosts.ToSerializable()), "actual draws skip X and negative canonical costs without consuming any vanilla energy RNG");
                player.RemoveRelicInternal(eye);
                CombatManager.Instance.Reset(true);
            }
            RunManager.Instance.CleanUp();
        }
        await CheckCostDrawAndOil();
    }

    private static async Task CheckCostDrawAndOil()
    {
        var witnessedRoutingCosts = false;
        foreach (var seed in new[] { "COST0", "COST1" })
        {
            var run = StartGoldRun(seed: seed);
            var player = run.Players[0];
            var combat = CombatFixture.CreateActiveCombat(run);
            var scope = CombatStateScope.Require(combat);
            var context = new ThrowingPlayerChoiceContext();
            var pcs = player.PlayerCombatState!;
            foreach (var card in pcs.AllCards.ToArray()) card.RemoveFromCurrentPile();
            foreach (var enemy in combat.Enemies) await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000);
            var confused = (await PowerCmd.Apply<ConfusedPower>(context, player.Creature, 1, player.Creature, null))!;
            var hellraiser = (await PowerCmd.Apply<HellraiserPower>(context, player.Creature, 1, player.Creature, null))!;
            var vanilla = run.Rng.CombatEnergyCosts.ToSerializable();
            var outer = combat.CreateCard<PommelStrike>(player);
            var inner = combat.CreateCard<KinglyKick>(player);
            CardCmd.Enchant<Slither>(outer, 1);
            CardCmd.Enchant<Slither>(inner, 1);
            pcs.DrawPile.AddInternal(outer);
            pcs.DrawPile.AddInternal(inner);
            await CardPileCmd.Draw(context, 1, player);
            var confusedAddress = RandomAddress.For("Confused cost", "others").With(scope.Address.Key);
            var slitherAddress = RandomAddress.For("Slither cost").With(scope.Address.Key);
            Assert(outer.Pile == pcs.DiscardPile && inner.Pile == pcs.Hand &&
                inner.EnergyCost.GetWithModifiers(CostModifiers.Local) == AddressRandom.IntInclusive(scope.Seed, confusedAddress.At(0), 0, 3) &&
                outer.EnergyCost.GetWithModifiers(CostModifiers.Local) == AddressRandom.IntInclusive(scope.Seed, confusedAddress.At(1), 0, 3) &&
                scope.Random.Position(confusedAddress) == 2 && scope.Random.Position(slitherAddress) == 0,
                "Hellraiser's nested draw reuses each card's own Confused roll, restores Kingly Kick's cost, and skips Slither on the auto-played card");
            player.Creature.RemovePowerInternal(hellraiser);
            player.Creature.RemovePowerInternal(confused);
            foreach (var card in new CardModel[] { inner, outer })
            {
                var ordinal = card == inner ? 0 : 1;
                card.RemoveFromCurrentPile();
                pcs.DrawPile.AddInternal(card);
                await CardPileCmd.Draw(context, 1, player);
                Assert(card.EnergyCost.GetWithModifiers(CostModifiers.Local) == AddressRandom.IntInclusive(scope.Seed, slitherAddress.At(ordinal), 0, 3) &&
                    scope.Random.Position(slitherAddress) == ordinal + 1 && scope.Random.Position(confusedAddress) == 2,
                    "separate Slither copies share successive exact rolls after Confused ends, without stale nested-draw state");
                card.EnergyCost.EndOfTurnCleanup();
                card.EnergyCost.AfterCardPlayedCleanup();
                Assert(card.EnergyCost.GetWithModifiers(CostModifiers.Local) == AddressRandom.IntInclusive(scope.Seed, slitherAddress.At(ordinal), 0, 3),
                    "Slither's cost survives both turn and play cleanup");
            }
            Assert(vanilla.Equals(run.Rng.CombatEnergyCosts.ToSerializable()), "nested draws preserve vanilla energy RNG");
            CombatManager.Instance.Reset(true);

            combat = CombatFixture.CreateActiveCombat(run);
            scope = CombatStateScope.Require(combat);
            pcs = player.PlayerCombatState!;
            foreach (var card in pcs.AllCards.ToArray()) card.RemoveFromCurrentPile();
            await PowerCmd.Apply<ConfusedPower>(context, player.Creature, 1, player.Creature, null);
            var state = RunCheckpoints.Require(run);
            var early = combat.CreateCard<Anger>(player);
            var middle = combat.CreateCard<Headbutt>(player);
            await CardPileCmd.AddGeneratedCardsToCombat(new CardModel[] { middle, early }, PileType.Hand, player);
            // Anger ranked before Headbutt on entry. Later lasting cost changes must not reverse that assignment.
            early.EnergyCost.SetThisCombat(3);
            var late = combat.CreateCard<Anger>(player);
            await CardPileCmd.AddGeneratedCardToCombat(late, PileType.Hand, player);
            pcs.Hand.MoveToTopInternal(late);
            CardModel[] draws = [combat.CreateCard<StrikeIronclad>(player), combat.CreateCard<DefendIronclad>(player),
                combat.CreateCard<Bash>(player), combat.CreateCard<PommelStrike>(player), combat.CreateCard<Whirlwind>(player),
                combat.CreateCard<Wound>(player), combat.CreateCard<KinglyKick>(player)];
            CardCmd.Enchant<Slither>(draws[^1], 1);
            await CardPileCmd.AddGeneratedCardsToCombat(draws, PileType.Draw, player);

            async Task<SneckoOil> UseOil()
            {
                var potion = (SneckoOil)ModelDb.Potion<SneckoOil>().ToMutable();
                player.AddPotionInternal(potion);
                potion.RemoveBeforeUse();
                await (Task)AccessTools.Method(typeof(SneckoOil), "OnUse").Invoke(potion, [context, player.Creature])!;
                return potion;
            }
            int OilCost(long ordinal, string category, int index) => AddressRandom.IntInclusive(state.Seed,
                RandomAddress.For("Oil cost").At(ordinal).With(category).At(index), 0, 3);
            void CheckOil(long ordinal, string category, CardModel[] ordered)
            {
                for (var i = 0; i < ordered.Length; i++)
                    Assert(ordered[i].EnergyCost.GetWithModifiers(CostModifiers.Local) == OilCost(ordinal, category, i),
                        $"Oil {ordinal} assigns {category} roll {i} in independently specified hand-entry order");
                Assert(state.Random.Position(RandomAddress.For("Oil cost").At(ordinal).With(category)) == ordered.Length,
                    "Oil consumes exactly the eligible category count");
            }
            var oil = await UseOil();
            Assert(PotionEffects.EffectOrdinal(oil) == 0 && pcs.Hand.Cards.Count == 10, "Oil commits before its seven successful draws");
            CheckOil(0, "others", [early, middle, late, draws[3], draws[6]]);
            CheckOil(0, "starter Strikes", [draws[0]]);
            CheckOil(0, "starter Defends", [draws[1]]);
            CheckOil(0, "other Basic 1", [draws[2]]);
            confusedAddress = RandomAddress.For("Confused cost", "others").With(scope.Address.Key);
            Assert(scope.Random.Position(confusedAddress) == 2 && scope.Random.Position(RandomAddress.For("Slither cost").With(scope.Address.Key)) == 0 &&
                !draws[4].EnergyCost.HasLocalModifiers && !draws[5].EnergyCost.HasLocalModifiers,
                "Oil's drawing advances Confused independently, reuses Slither overlap, and skips X and unplayable cards");
            early.EnergyCost.AfterCardPlayedCleanup();
            middle.EnergyCost.EndOfTurnCleanup();
            Assert(early.EnergyCost.GetWithModifiers(CostModifiers.Local) == 3 && middle.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1,
                "Oil expires on play or turn end and reveals the earlier lasting cost");
            foreach (var (card, ordinal) in new[] { (draws[3], 0), (draws[6], 1) })
            {
                card.EnergyCost.EndOfTurnCleanup();
                Assert(card.EnergyCost.GetWithModifiers(CostModifiers.Local) == AddressRandom.IntInclusive(scope.Seed, confusedAddress.At(ordinal), 0, 3),
                    "Oil expiry restores the exact Confused/Slither draw value underneath it");
            }
            foreach (var card in pcs.AllCards.ToArray()) card.RemoveFromCurrentPile();
            var empty = await UseOil();
            Assert(PotionEffects.EffectOrdinal(empty) == 1 && state.Random.Position(RandomAddress.For("Oil cost").At(1).With("others")) == 0,
                "an empty Oil effect advances its opportunity without consuming a cost roll");
            var discarded = ModelDb.Potion<SneckoOil>().ToMutable();
            player.AddPotionInternal(discarded);
            await PotionCmd.Discard(discarded);
            var first = combat.CreateCard<Anger>(player);
            var second = combat.CreateCard<Anger>(player);
            await CardPileCmd.AddGeneratedCardToCombat(first, PileType.Hand, player);
            await CardPileCmd.AddGeneratedCardToCombat(second, PileType.Hand, player);
            await CardPileCmd.Add(first, PileType.Discard);
            await CardPileCmd.Add(first, PileType.Hand);
            CombatCards.Reassociate([first, second], [second, first]);
            oil = await UseOil();
            Assert(PotionEffects.EffectOrdinal(oil) == 3, "discarded Oil advances the next effect address");
            CheckOil(3, "others", [first, second]);
            witnessedRoutingCosts |= OilCost(3, "others", 0) != OilCost(3, "others", 1);
            Assert(vanilla.Equals(run.Rng.CombatEnergyCosts.ToSerializable()), "all Oil rolls and intervening draws preserve vanilla energy RNG");
            RunManager.Instance.CleanUp();
        }
        Assert(witnessedRoutingCosts, "Oil routing coverage includes distinct costs for the swapped latest-entry assignments");
    }
}
