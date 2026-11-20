using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Merchant;
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
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;

namespace Seedline.Checks;

// A non-game character type exercises detection without a dependency on a character mod.
public sealed class CompatibilityCharacter : CharacterModel
{
    private static Ironclad Baseline => ModelDb.Character<Ironclad>();
    public override bool IsPlayable => false;
    public override Color NameColor => Baseline.NameColor;
    public override CharacterGender Gender => Baseline.Gender;
    protected override CharacterModel? UnlocksAfterRunAs => null;
    public override int StartingHp => Baseline.StartingHp;
    public override int StartingGold => Baseline.StartingGold;
    public override int BaseOrbSlotCount => 3;
    public override CardPoolModel CardPool => Baseline.CardPool;
    public override RelicPoolModel RelicPool => Baseline.RelicPool;
    public override PotionPoolModel PotionPool => Baseline.PotionPool;
    public override IEnumerable<CardModel> StartingDeck => Baseline.StartingDeck;
    public override IReadOnlyList<RelicModel> StartingRelics => Baseline.StartingRelics;
    public override float AttackAnimDelay => 0;
    public override float CastAnimDelay => 0;
    public override List<string> GetArchitectAttackVfx() => [];
}

public static partial class Checks
{
    private static async Task CheckCharacterCompatibility()
    {
        RunManager.Instance.CleanUp();
        ModelDb.Inject(typeof(CompatibilityCharacter));
        var player = Player.CreateForNewRun<CompatibilityCharacter>(UnlockState.all, 1);
        var run = RunState.CreateForNewRun([player], ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(),
            [], GameMode.Standard, false, 7, "CHARACTERCOMPATIBILITY");
        RunManager.Instance.SetUpNewSingleplayer(run, true);
        try
        {
            Assert(CharacterCompatibility.UseVanillaCards(player) && CharacterCompatibility.IsModdedRun && CombatCards.UseVanilla(player),
                "a character from another assembly selects the fallback before combat");
            Assert(ModelDb.AllCharacters.All(c => c.GetType().Assembly == typeof(CharacterModel).Assembly),
                "all vanilla characters retain Seedline card policies");
            var state = RunCheckpoints.Require(run);
            var snapshot = RunManager.Instance.ToSave(null);
            var bytes = RunCheckpoints.Encode(snapshot);
            string Rewards()
            {
                var reward = new CardReward(CardCreationOptions.ForRoom(player, RoomType.Monster), 3, player);
                reward.Populate();
                Assert(reward.Cards.All(c => CardOffers.Info(c).Offer.Length > 0),
                    "modded-character reward cards retain Seedline offer addresses");
                var shop = MerchantInventory.CreateForNormalMerchant(player);
                return string.Join("/", reward.Cards.Select(c => c.Id).Concat(shop.CardEntries.Select(e => e.CreationResult!.Card.Id))) +
                    RelicRewards.Draw(player, "generic", RelicRarity.Common).Id +
                    string.Join("/", shop.RelicEntries.Select(e => e.Model!.Id));
            }
            var expectedRewards = Rewards();
            var loaded = RunCheckpoints.Decode(Encoding.UTF8.GetString(bytes), snapshot.SchemaVersion);
            RunManager.Instance.CleanUp();
            run = RunState.FromSerializable(loaded);
            await RunManager.Instance.SetUpSavedSingleplayer(run, loaded);
            player = run.Players[0];
            state = RunCheckpoints.Require(run);
            Assert(CharacterCompatibility.UseVanillaCards(player) && Rewards() == expectedRewards,
                "registered external character restores with aligned rewards and shops after checkpoint reload");

            var strikes = player.Deck.Cards.OfType<StrikeIronclad>().Cast<CardModel>().ToArray();
            var keys = strikes.Select(CardOccurrences.Info).ToArray();
            var selector = new TestCardSelector();
            using (CardSelectorStack.SetTestSelector(selector))
            {
                selector.PrepareToSelect([strikes[^1]]);
                var selected = await CardSelectCmd.FromDeckForUpgrade(player, new(CardSelectorPrefs.UpgradeSelectionPrompt, 1));
                Assert(selected.Single() == strikes[^1] && strikes.Select(CardOccurrences.Info).SequenceEqual(keys) && state.Selection.Clocks.Count == 0,
                    "modded-character deck choices preserve clicked copies without equivalence routing");
            }
            var stone = ModelDb.Relic<Whetstone>().ToMutable();
            player.AddRelicInternal(stone);
            var upgradeRng = new Rng(player.PlayerRng.NonCombatGeneration.ToSerializable());
            var expectedUpgrades = player.Deck.Cards.Where(c => c.Type == CardType.Attack && c.IsUpgradable)
                .ToList().StableShuffle(upgradeRng).Take(stone.DynamicVars.Cards.IntValue).ToArray();
            await stone.AfterObtained();
            Assert(player.Deck.Cards.Where(c => c.IsUpgraded).ToHashSet().SetEquals(expectedUpgrades) &&
                upgradeRng.ToSerializable().Equals(player.PlayerRng.NonCombatGeneration.ToSerializable()) && state.Selection.Clocks.Count == 0,
                "modded-character random upgrades preserve vanilla targets and RNG consumption");
            var original = player.Deck.Cards.OfType<DefendIronclad>().First();
            var transformRng = new Rng(player.PlayerRng.Transformations.ToSerializable());
            var expectedTransform = CardFactory.CreateRandomCardForTransform(original, false, transformRng);
            var transformed = (await CardCmd.TransformToRandom(original, player.PlayerRng.Transformations, CardPreviewStyle.None)).cardAdded;
            Assert(transformed.Id == expectedTransform.Id && transformRng.ToSerializable().Equals(player.PlayerRng.Transformations.ToSerializable()) &&
                state.Cards.Removed.Count == 0,
                "modded-character transformation uses vanilla identity/RNG without ranked removed-card history");

            var changedDeck = player.Deck.Cards.Select(c => (c.Id, c.CurrentUpgradeLevel, CardOccurrences.Info(c))).ToArray();
            snapshot = RunManager.Instance.ToSave(null);
            loaded = RunCheckpoints.Decode(Encoding.UTF8.GetString(RunCheckpoints.Encode(snapshot)), snapshot.SchemaVersion);
            RunManager.Instance.CleanUp();
            run = RunState.FromSerializable(loaded);
            await RunManager.Instance.SetUpSavedSingleplayer(run, loaded);
            player = run.Players[0];
            state = RunCheckpoints.Require(run);
            Assert(CharacterCompatibility.UseVanillaCards(player) &&
                player.Deck.Cards.Select(c => (c.Id, c.CurrentUpgradeLevel, CardOccurrences.Info(c))).SequenceEqual(changedDeck),
                "modded-character checkpoint restores upgraded/transformed cards and their required occurrence metadata");

            var shuffleRng = new Rng(run.Rng.Shuffle.ToSerializable());
            var expectedOpening = player.Deck.Cards.ToList().UnstableShuffle(shuffleRng);
            var combat = CombatFixture.CreateActiveCombat(run);
            var scope = CombatStateScope.Require(combat);
            var pcs = player.PlayerCombatState!;
            var context = new ThrowingPlayerChoiceContext();
            Assert(pcs.DrawPile.Cards.Select(c => c.DeckVersion).SequenceEqual(expectedOpening) &&
                shuffleRng.ToSerializable().Equals(run.Rng.Shuffle.ToSerializable()) && scope.Random.Position(scope.For("shuffle")) == 0 &&
                pcs.DrawPile.Cards.All(c => CombatCards.Find(c) == null),
                "modded-character opening uses vanilla shuffle and skips combat-card state/ranking");
            var monster = ModelDb.Monster<TwigSlimeM>().ToMutable();
            var hpOracle = new EnemyHpOracle(scope.Seed, scope.Address);
            var expectedHp = hpOracle.Next(monster, combat.Enemies.Select(c => c.MaxHp));
            var creature = combat.CreateCreature(monster, CombatSide.Enemy, null);
            combat.AddCreature(creature);
            Assert(creature.MaxHp == expectedHp && scope.Random.Position(scope.For("enemy HP")) == hpOracle.Ordinal,
                "vanilla enemy HP remains aligned for a modded character");
            foreach (var card in pcs.DrawPile.Cards.ToArray())
            {
                card.RemoveFromCurrentPile();
                pcs.DiscardPile.AddInternal(card);
            }
            var expectedReshuffle = pcs.DiscardPile.Cards.ToList().StableShuffle(shuffleRng);
            await CardPileCmd.Shuffle(context, player);
            Assert(pcs.DrawPile.Cards.SequenceEqual(expectedReshuffle) && shuffleRng.ToSerializable().Equals(run.Rng.Shuffle.ToSerializable()),
                "modded-character reshuffle preserves vanilla copy order and RNG");
            var soul = combat.CreateCard<Soul>(player);
            var expectedGap = shuffleRng.NextInt(pcs.DrawPile.Cards.Count + 1);
            await CardPileCmd.AddGeneratedCardToCombat(soul, PileType.Draw, player, CardPilePosition.Random);
            Assert(pcs.DrawPile.Cards.IndexOf(soul) == expectedGap && shuffleRng.ToSerializable().Equals(run.Rng.Shuffle.ToSerializable()),
                "modded-character insertion retains vanilla gap and RNG");
            foreach (var card in pcs.DrawPile.Cards.Take(3).ToArray())
            {
                card.RemoveFromCurrentPile();
                pcs.Hand.AddInternal(card);
            }
            using (CardSelectorStack.SetTestSelector(selector))
            {
                var clicked = pcs.Hand.Cards[^1];
                selector.PrepareToSelect([clicked]);
                var selected = await CardSelectCmd.FromHandForDiscard(context, player,
                    new(CardSelectorPrefs.DiscardSelectionPrompt, 1), null, ModelDb.Card<Prepared>());
                Assert(selected.Single() == clicked && scope.Selection.Clocks.Count == 0,
                    "modded-character combat choices require no card-equivalence state");
            }
            async Task Play<T>() where T : CardModel
            {
                var card = combat.CreateCard<T>(player);
                await (Task)AccessTools.Method(typeof(T), "OnPlay").Invoke(card, [context, new CardPlay
                {
                    Card = card, Player = player, Target = null, IsAutoPlay = false, ResultPile = PileType.Discard,
                    Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                    PlayIndex = 0, PlayCount = 1
                }])!;
            }
            var selectionRng = new Rng(run.Rng.CombatCardSelection.ToSerializable());
            var expectedExhaust = selectionRng.NextItem(pcs.Hand.Cards);
            await Play<TrueGrit>();
            Assert(expectedExhaust!.Pile == pcs.ExhaustPile && selectionRng.ToSerializable().Equals(run.Rng.CombatCardSelection.ToSerializable()),
                "modded-character random selection preserves vanilla target and RNG");
            var confused = (ConfusedPower)ModelDb.Power<ConfusedPower>().ToMutable();
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(confused, player.Creature);
            player.Creature.ApplyPowerInternal(confused);
            var costRng = new Rng(run.Rng.CombatEnergyCosts.ToSerializable());
            var kick = combat.CreateCard<KinglyKick>(player);
            CardCmd.Enchant<Slither>(kick, 1);
            pcs.Hand.AddInternal(kick);
            costRng.NextInt(4);
            var expectedCost = costRng.NextInt(4);
            await Hook.AfterCardDrawn(combat, context, kick, false);
            Assert(kick.EnergyCost.GetWithModifiers(CostModifiers.Local) == expectedCost &&
                costRng.ToSerializable().Equals(run.Rng.CombatEnergyCosts.ToSerializable()),
                "modded-character Confused and Slither retain separate vanilla rolls");
            var x = combat.CreateCard<Whirlwind>(player);
            pcs.Hand.AddInternal(x);
            costRng.NextInt(4);
            await Hook.AfterCardDrawn(combat, context, x, false);
            Assert(x.EnergyCost.CostsX && costRng.ToSerializable().Equals(run.Rng.CombatEnergyCosts.ToSerializable()),
                "modded-character Confused retains vanilla X-card RNG consumption");
            player.Creature.RemoveAllPowersInternalExcept();
            var generationRng = new Rng(run.Rng.CombatCardGeneration.ToSerializable());
            var expectedCards = CardFactory.GetForCombat(player, player.Character.CardPool.GetUnlockedCards(player.UnlockState,
                player.RunState.CardMultiplayerConstraint), pcs.Hand.Cards.Count, generationRng).Select(c => c.Id).ToArray();
            await Play<Stoke>();
            Assert(pcs.Hand.Cards.Select(c => c.Id).SequenceEqual(expectedCards) &&
                generationRng.ToSerializable().Equals(run.Rng.CombatCardGeneration.ToSerializable()) && scope.Cards.Supplies.Count == 0,
                "modded-character combat generation uses the vanilla factory and RNG");
            foreach (var card in pcs.Hand.Cards.ToArray()) card.RemoveFromCurrentPile();
            expectedCards = CardFactory.GetDistinctForCombat(player, ModelDb.CardPool<ColorlessCardPool>().GetUnlockedCards(player.UnlockState,
                player.RunState.CardMultiplayerConstraint),
                3, generationRng).Select(c => c.Id).ToArray();
            var potion = ModelDb.Potion<CosmicConcoction>().ToMutable();
            player.AddPotionInternal(potion);
            potion.RemoveBeforeUse();
            await (Task)AccessTools.Method(typeof(CosmicConcoction), "OnUse").Invoke(potion, [context, player.Creature])!;
            Assert(pcs.Hand.Cards.Select(c => c.Id).SequenceEqual(expectedCards) &&
                generationRng.ToSerializable().Equals(run.Rng.CombatCardGeneration.ToSerializable()) &&
                state.Random.Position(PotionEffects.Counter(potion.Id.Entry)) == 0,
                "modded-character potion generation bypasses Seedline effect supplies and ordinals");
            var oil = ModelDb.Potion<SneckoOil>().ToMutable();
            player.AddPotionInternal(oil);
            oil.RemoveBeforeUse();
            costRng = new Rng(run.Rng.CombatEnergyCosts.ToSerializable());
            await (Task)AccessTools.Method(typeof(SneckoOil), "OnUse").Invoke(oil, [context, player.Creature])!;
            foreach (var card in pcs.Hand.Cards.Where(c => !c.EnergyCost.CostsX && c.EnergyCost.GetWithModifiers(CostModifiers.None) >= 0))
                Assert(card.EnergyCost.GetWithModifiers(CostModifiers.Local) == costRng.NextInt(4),
                    "modded-character Oil assigns vanilla costs in hand order without hand-entry metadata");
            Assert(costRng.ToSerializable().Equals(run.Rng.CombatEnergyCosts.ToSerializable()),
                "modded-character Oil preserves vanilla RNG consumption");
            var targetRng = new Rng(run.Rng.CombatTargets.ToSerializable());
            var expectedTarget = targetRng.NextItem(combat.Enemies);
            var actualTarget = AccessTools.Method(typeof(EnemyTargets), "Damage").Invoke(null, [run.Rng.CombatTargets, combat.Enemies]);
            Assert(ReferenceEquals(actualTarget, expectedTarget) && targetRng.ToSerializable().Equals(run.Rng.CombatTargets.ToSerializable()),
                "modded-character player-side targeting preserves vanilla target and RNG");
            var orbRng = new Rng(run.Rng.CombatOrbGeneration.ToSerializable());
            var expectedOrb = OrbModel.GetRandomOrb(orbRng).Id;
            await Play<Chaos>();
            Assert(pcs.OrbQueue.Orbs.Single().Id == expectedOrb && orbRng.ToSerializable().Equals(run.Rng.CombatOrbGeneration.ToSerializable()) &&
                scope.Random.Position(scope.For("orb type")) == 0,
                "modded-character orb generation retains vanilla identity and RNG");
        }
        finally { RunManager.Instance.CleanUp(); }
    }
}
