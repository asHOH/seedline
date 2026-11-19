using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCombatGenerationPolicy()
    {
        foreach (var seed in new[] { "GENERATION0", "GENERATION1" })
        {
            var run = StartGoldRun(seed: seed);
            var player = run.Players[0];
            for (var battle = 0; battle < 2; battle++)
            {
                var combat = CombatFixture.CreateActiveCombat(run);
                var scope = CombatStateScope.Require(combat);
                var oracle = new GenerationSequenceOracle(scope.Seed, scope.Address);
                var hand = player.PlayerCombatState!.Hand;
                var vanilla = run.Rng.CombatCardGeneration.ToSerializable();
                var context = new ThrowingPlayerChoiceContext();
                CardModel[] Pool(CardPoolModel pool) => CardFactory.FilterForCombat(
                    pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)).ToArray();
                var own = Pool(player.Character.CardPool);
                var colorless = Pool(ModelDb.CardPool<ColorlessCardPool>());
                var other = player.UnlockState.CharacterCardPools.Where(p => p != player.Character.CardPool).SelectMany(Pool).ToArray();
                var ownId = player.Character.CardPool.Id.Entry;
                var colorlessId = ModelDb.CardPool<ColorlessCardPool>().Id.Entry;

                void Check(IEnumerable<CardModel> actual, SupplyEntry[] expected, string source)
                {
                    var cards = actual.ToArray();
                    Assert(cards.Select(c => new SupplyEntry(c.Id.Entry, CardOccurrences.Info(c).Address)).SequenceEqual(expected),
                        $"{seed}, battle {battle + 1}: {source} matches independent filtered identities and original entry addresses");
                }

                async Task Play<T>(bool upgraded = false) where T : CardModel
                {
                    var card = combat.CreateCard<T>(player);
                    if (upgraded) CardCmd.Upgrade(card);
                    player.PlayerCombatState.PlayPile.AddInternal(card);
                    await (Task)AccessTools.Method(typeof(T), "OnPlay").Invoke(card,
                        [context, new CardPlay
                        {
                            Card = card, Player = player, Target = combat.Enemies[0], ResultPile = PileType.Discard,
                            Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                            IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
                        }])!;
                    card.RemoveFromCurrentPile();
                }

                async Task<CardModel[]> Generated(string group, string poolId, CardModel[] pool, IEnumerable<CardModel> eligible,
                    int count, bool distinct, Func<Task> action, string source)
                {
                    foreach (var card in hand.Cards.ToArray()) card.RemoveFromCurrentPile();
                    var expected = oracle.Draw(group, poolId, pool, eligible, count, distinct);
                    var history = CombatManager.Instance.History.Entries.Count();
                    await action();
                    var cards = CombatManager.Instance.History.Entries.Skip(history).OfType<CardGeneratedEntry>().Select(e => e.Card).ToArray();
                    Check(cards, expected, source);
                    return cards;
                }

                async Task Offer<T>(string group, string poolId, CardModel[] pool, IEnumerable<CardModel> eligible,
                    bool skip, bool upgraded = false, bool upgradedResults = false) where T : CardModel
                {
                    foreach (var card in hand.Cards.ToArray()) card.RemoveFromCurrentPile();
                    var expected = oracle.Draw(group, poolId, pool, eligible, 3, true);
                    var selector = new GenerationOfferSelector(skip);
                    using (CardSelectorStack.SetTestSelector(selector)) await Play<T>(upgraded);
                    Check(selector.Offered, expected, typeof(T).Name);
                    Assert(selector.Offered.Length == 3 && selector.Offered.Select(c => c.Id).Distinct().Count() == 3 &&
                        selector.Offered.All(c => c.IsUpgraded == upgradedResults) &&
                        (skip ? hand.Cards.Count == 0 : hand.Cards.SequenceEqual(selector.Offered.Take(1)) &&
                            hand.Cards[0].EnergyCost.GetWithModifiers(CostModifiers.All) == 0),
                        $"{typeof(T).Name} consumes all alternatives while preserving distinct offers, upgrades, skip and free chosen card");
                }

                await Offer<Discovery>("free this turn", ownId, own, own, skip: true);
                var generated = await Generated("free this turn", ownId, own, own.Where(c => c.Type == CardType.Attack), 1, true,
                    () => Play<InfernalBlade>(), "Infernal Blade after skipped Discovery");
                Assert(generated.Single().EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "Infernal Blade applies its free-this-turn effect");
                await Offer<Abundance>("free this turn", ownId, own, own.Where(c => c.Type == CardType.Power), skip: false, upgradedResults: true);
                var puzzlebox = (VexingPuzzlebox)ModelDb.Relic<VexingPuzzlebox>().ToMutable();
                player.AddRelicInternal(puzzlebox);
                await Generated("free this turn", ownId, own, own, 1, true,
                    () => puzzlebox.AfterPlayerTurnStart(context, player), "Vexing Puzzlebox shares filtered free generation");
                player.RemoveRelicInternal(puzzlebox);

                // Printed zero costs and the Hierophant's discount do not select a free-cost group.
                generated = await Generated("ordinary costs", ownId, own,
                    own.Where(c => c.EnergyCost.Canonical == 0 && !c.EnergyCost.CostsX), 3, false,
                    () => Play<Jackpot>(true), "upgraded Jackpot");
                Assert(generated.All(c => c.IsUpgraded), "Jackpot preserves its upgrades without changing its generation group");
                var voidPower = (CallOfTheVoidPower)ModelDb.Power<CallOfTheVoidPower>().ToMutable();
                AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(voidPower, player.Creature);
                voidPower.SetAmount(3);
                generated = await Generated("ordinary costs", ownId, own, own, 3, false,
                    () => voidPower.BeforeHandDraw(player, context, combat), "Call of the Void's separate single-card draws");
                Assert(generated.All(c => c.Keywords.Contains(CardKeyword.Ethereal)), "Call of the Void preserves Ethereal on every result");
                var creative = (CreativeAiPower)ModelDb.Power<CreativeAiPower>().ToMutable();
                AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(creative, player.Creature);
                creative.SetAmount(2);
                await Generated("ordinary costs", ownId, own, own.Where(c => c.Type == CardType.Power), 2, false,
                    () => creative.BeforeHandDraw(player, context, combat), "Creative AI permits repeats across separate draws");
                var choices = (ChoicesParadox)ModelDb.Relic<ChoicesParadox>().ToMutable();
                player.AddRelicInternal(choices);
                var choicesExpected = oracle.Draw("ordinary costs", ownId, own, own, 5, true);
                var choicesSelector = new GenerationOfferSelector(false);
                using (CardSelectorStack.SetTestSelector(choicesSelector)) await choices.AfterPlayerTurnStart(context, player);
                Check(choicesSelector.Offered, choicesExpected, "Choices Paradox after Jackpot's zero-cost filter");
                Assert(choicesSelector.Offered.All(c => c.Keywords.Contains(CardKeyword.Retain)), "Choices Paradox preserves Retain on every alternative");
                player.RemoveRelicInternal(choices);

                generated = await Generated("ordinary costs", colorlessId, colorless, colorless, 3, true,
                    () => Play<TheHierophant>(), "The Hierophant");
                Assert(generated.All(c => c.EnergyCost.GetWithModifiers(CostModifiers.All) == Math.Max(0, c.EnergyCost.Canonical - 1)),
                    "The Hierophant preserves its minus-one discount in the ordinary group");
                await Generated("ordinary costs", colorlessId, colorless, colorless.Where(c => c is not JackOfAllTrades), 1, true,
                    () => Play<JackOfAllTrades>(), "Jack of All Trades excludes itself in the shared colorless sequence");

                await Offer<Splash>("Splash", "other characters", other.Where(c => c.Type == CardType.Attack).ToArray(),
                    other.Where(c => c.Type == CardType.Attack), skip: true, upgraded: true, upgradedResults: true);
                await Generated("ordinary costs", "other characters", other, other, CardPile.MaxCardsInHand, false,
                    () => Play<TheLovers>(), "The Lovers samples the combined identity pool independently of Splash");
                var fullHand = hand.Cards.ToArray();
                var loversSupply = scope.Cards.Supplies[RandomAddress.For("card generation", "ordinary costs", "other characters").With(scope.Address.Key).Key];
                var loversGenerated = loversSupply.Generated;
                await Play<TheLovers>();
                Assert(hand.Cards.SequenceEqual(fullHand) && loversSupply.Generated == loversGenerated,
                    "The Lovers with a full hand generates no identities and preserves its sequence");
                var attacks = own.Where(c => c.Type == CardType.Attack).ToArray();
                generated = await Generated("free this combat", ownId, attacks, attacks, 3, false,
                    () => Play<Metamorphosis>(), "Metamorphosis");
                Assert(generated.All(c => c.Pile?.Type == PileType.Draw && c.EnergyCost.GetWithModifiers(CostModifiers.All) == 0),
                    "Metamorphosis preserves free-combat attacks and draw-pile insertion");
                var melodies = player.UnlockState.CharacterCardPools.SelectMany(Pool)
                    .Where(c => c.Tags.Any(t => t is CardTag.Wah or CardTag.Cho or CardTag.Ki)).ToArray();
                var ocarina = (Ocarina)ModelDb.Relic<Ocarina>().ToMutable();
                player.AddRelicInternal(ocarina);
                await Generated("ordinary costs", "melodies", melodies, melodies, 1, true,
                    () => ocarina.BeforeHandDraw(player, context, combat), "Ocarina's combined melody identities");
                player.RemoveRelicInternal(ocarina);
                var mythic = (MythicFormPower)ModelDb.Power<MythicFormPower>().ToMutable();
                AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(mythic, player.Creature);
                mythic.SetAmount(2);
                mythic.IncrementUpgraded();
                var selectionRng = run.Rng.CombatCardSelection.ToSerializable();
                generated = await Generated("ordinary costs", "Tarot", TarotCards.All.ToArray(),
                    TarotCards.All.Where(c => c.IsAllowedInRun(player.RunState)), 2, false,
                    () => mythic.BeforeHandDraw(player, context, combat), "Mythic Form's independent Tarot sequence");
                Assert(generated[0].IsUpgraded && !generated[1].IsUpgraded && selectionRng.Equals(run.Rng.CombatCardSelection.ToSerializable()),
                    "Mythic Form preserves ordered upgrades without advancing vanilla selection RNG");

                // Entropy uses each original's transformation pool, not the player's character pool.
                foreach (var card in hand.Cards.ToArray()) card.RemoveFromCurrentPile();
                CardModel[] originals = [combat.CreateCard<Anger>(player), combat.CreateCard<Bash>(player),
                    combat.CreateCard<Shiv>(player), combat.CreateCard<Injury>(player), combat.CreateCard<Burn>(player),
                    combat.CreateCard<TheLovers>(player)];
                foreach (var card in originals) hand.AddInternal(card);
                var replacements = new Dictionary<CardModel, SupplyEntry>();
                foreach (var original in originals.OrderBy(c => c, CardOrder.Identity))
                {
                    var pool = original is Shiv or TheLovers ? ModelDb.CardPool<ColorlessCardPool>() : original.Pool;
                    var eligible = Pool(pool).Where(c => c.Id != original.Id && (original is Injury or Burn ||
                        c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare));
                    replacements.Add(original, oracle.Draw("ordinary costs", pool.Id.Entry, Pool(pool), eligible, 1, false).Single());
                }
                var entropy = (EntropyPower)ModelDb.Power<EntropyPower>().ToMutable();
                AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(entropy, player.Creature);
                entropy.SetAmount(originals.Length);
                var selected = new TestCardSelector();
                selected.PrepareToSelect(originals.Reverse());
                using (CardSelectorStack.SetTestSelector(selected)) await entropy.AfterPlayerTurnStart(context, player);
                Check(hand.Cards, originals.Select(c => replacements[c]).ToArray(), "Entropy's mixed-pool batch in reversed click order");
                Assert(originals.All(c => c.Pile == null) && hand.Cards.All(c => !c.IsUpgraded),
                    "Entropy retires originals and creates unupgraded replacements at their original hand positions");
                await Offer<Discovery>("free this turn", ownId, own, own, skip: false, upgraded: true);
                Assert(vanilla.Equals(run.Rng.CombatCardGeneration.ToSerializable()), "all covered source hooks leave vanilla generation RNG untouched");

                // A factory call outside a covered source must not inherit its AsyncLocal context.
                var supplyCount = scope.Cards.Supplies.Count;
                var fallback = CardFactory.GetForCombat(player, own, 1, run.Rng.CombatCardGeneration).Single();
                Assert(!vanilla.Equals(run.Rng.CombatCardGeneration.ToSerializable()) && scope.Cards.Supplies.Count == supplyCount &&
                    fallback.Owner == player, "completed source hooks restore vanilla factory behavior outside their context");
                CombatManager.Instance.Reset(true);
            }
            RunManager.Instance.CleanUp();
        }
    }

    private sealed class GenerationOfferSelector(bool skip) : ICardSelector
    {
        internal CardModel[] Offered { get; private set; } = [];
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            Offered = options.ToArray();
            return Task.FromResult(skip ? Enumerable.Empty<CardModel>() : Offered.Take(1));
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives) =>
            throw new InvalidOperationException("Unexpected reward selection during combat generation.");
    }

    private sealed class GenerationSequenceOracle(string seed, RandomAddress combat)
    {
        private readonly Dictionary<string, List<SupplyEntry>> sequences = [];

        internal SupplyEntry[] Draw(string group, string poolId, IEnumerable<CardModel> pool, IEnumerable<CardModel> eligible, int count, bool distinct)
        {
            var address = RandomAddress.For("card generation", group, poolId).With(combat.Key);
            var identities = pool.Select(c => c.Id.Entry).Distinct().Order(StringComparer.Ordinal).ToArray();
            if (!sequences.TryGetValue(address.Key, out var sequence))
            {
                sequence = Enumerable.Range(0, 2048).Select(i => new SupplyEntry(
                    identities[AddressRandom.IntInclusive(seed, address.At(i), 0, identities.Length - 1)], address.At(i).Key)).ToList();
                sequences.Add(address.Key, sequence);
            }
            var allowed = eligible.Select(c => c.Id.Entry).ToHashSet();
            var results = new List<SupplyEntry>();
            for (var draw = 0; draw < count && allowed.Count > 0; draw++)
            {
                // Eagerly advance every eligible position, independently of production's deferred markers.
                var positions = Enumerable.Range(0, sequence.Count).Where(i => allowed.Contains(sequence[i].Identity)).ToArray();
                Assert(positions.Length > count && positions[0] < 256, "generation oracle retains ample unused tail beyond tested draws");
                results.Add(sequence[positions[0]]);
                for (var i = 1; i < positions.Length; i++) sequence[positions[i - 1]] = sequence[positions[i]];
                sequence.RemoveAt(positions[^1]);
                if (distinct) allowed.Remove(results[^1].Identity);
            }
            return results.ToArray();
        }
    }
}
