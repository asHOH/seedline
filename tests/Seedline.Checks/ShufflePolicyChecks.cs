using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckShufflePriorities()
    {
        foreach (var act in new[] { 0, 1, 2 })
            foreach (var remove in new[] { false, true })
            {
                var run = StartGoldRun(seed: "SHUFFLECHECK");
                var player = run.Players[0];
                var starters = player.Deck.Cards.ToArray();
                CardModel anger = run.CreateCard<Anger>(player);
                CardModel inflame = run.CreateCard<Inflame>(player);
                await CardPileCmd.Add(new CardModel[] { inflame, anger }, PileType.Deck);
                CardModel laterAnger = run.CreateCard<Anger>(player);
                await CardPileCmd.Add(laterAnger, PileType.Deck);
                CardModel innate = run.CreateCard<Apotheosis>(player);
                await CardPileCmd.Add(innate, PileType.Deck);
                CardOccurrences.AssignKey(innate, RandomAddress.For("shuffle fixture reward", "Innate").Key);
                CardModel reward = run.CreateCard<PommelStrike>(player);
                await CardPileCmd.Add(reward, PileType.Deck);
                CardOccurrences.AssignKey(reward, RandomAddress.For("shuffle fixture reward", "Attack").Key);
                var copy = run.CloneCard(reward);
                await CardPileCmd.Add(copy, PileType.Deck);
                Assert(CardOccurrences.Info(anger).Acquisition == CardOccurrences.Info(inflame).Acquisition &&
                    CardOccurrences.Info(laterAnger).Acquisition > CardOccurrences.Info(anger).Acquisition &&
                    !CardOccurrences.Info(copy).SourceKey,
                    "shuffle fixture distinguishes acquisition batches, within-batch rank, reward keys and permanent copies");
                if (remove)
                {
                    await CardPileCmd.RemoveFromDeck(laterAnger, false);
                    CardCmd.Upgrade(laterAnger);
                    foreach (var card in player.Deck.Cards.ToArray()) player.Deck.MoveToTopInternal(card);
                }
                run.CurrentActIndex = act;
                var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
                combat.AddPlayer(player);
                combat.Encounter!.GenerateMonstersWithSlots(run);
                foreach (var (monster, slot) in combat.Encounter.MonstersWithSlots)
                    combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
                var vanilla = run.Rng.Shuffle.ToSerializable();
                CombatManager.Instance.SetUpCombat(combat);
                var scope = CombatStateScope.Require(combat);
                var pcs = player.PlayerCombatState!;
                var clones = pcs.DrawPile.Cards.ToDictionary(c => c.DeckVersion!);
                Dictionary<CardModel, string> Keys(bool opening, bool retained, bool perfectFit)
                {
                    var result = new Dictionary<CardModel, string>();
                    void Sequence(IEnumerable<CardModel> cards, string name)
                    {
                        var index = 0;
                        foreach (var card in cards) result.Add(card, RandomAddress.For("shuffle key", name).At(index++).Key);
                    }
                    IEnumerable<CardModel> ByAddress(IEnumerable<CardModel> cards) =>
                        cards.OrderBy(c => CardOccurrences.Info(c).Address, StringComparer.Ordinal);
                    Sequence(starters.OfType<AscendersBane>().Cast<CardModel>().Concat(ByAddress(starters.OfType<StrikeIronclad>()))
                        .Concat(ByAddress(starters.OfType<DefendIronclad>())), "first");
                    var angers = retained || !remove ? new CardModel[] { anger, laterAnger } : [anger];
                    if (opening) Sequence([innate], "Innate");
                    if (act < 2)
                    {
                        foreach (var card in starters.OfType<Bash>().Cast<CardModel>().Concat(opening ? [reward] : new[] { reward, innate }))
                            result.Add(card, CardOccurrences.Info(card).Key);
                        Sequence(new[] { anger, inflame }.Concat(angers.Where(c => c == laterAnger)).Append(copy), "second");
                    }
                    else
                    {
                        // Explicit category/usefulness order, independent of CombatShuffle.Keys and CardOrder.
                        var attacks = perfectFit ? new[] { reward, copy } : ByAddress([reward, copy]);
                        Sequence(new[] { inflame }.Concat(opening ? [] : new[] { innate }).Concat(attacks)
                            .Concat(ByAddress(angers)).Concat(starters.OfType<Bash>()), "second");
                    }
                    return result;
                }
                CardModel[] Order(Dictionary<CardModel, string> keys, long ordinal)
                {
                    var address = RandomAddress.For("shuffle").With(scope.Address.Key).At(ordinal);
                    return clones.Where(p => keys.ContainsKey(p.Key)).OrderBy(p => AddressRandom.UInt64(scope.Seed, address.With(keys[p.Key])))
                        .ThenBy(p => CardOccurrences.Info(p.Key).Address, StringComparer.Ordinal).Select(p => p.Value).ToArray();
                }
                var selectionKeys = Keys(false, true, false);
                var opening = Order(Keys(true, true, false), 0);
                Assert(pcs.DrawPile.Cards.SequenceEqual(opening) && clones.All(p => CombatCards.Info(p.Value).Key == selectionKeys[p.Key]),
                    $"act {act + 1}: exact opening priorities use prescribed keys and frozen removed ranks, with separate ordinary selection keys");
                CombatFixture.SetInProgress(true);
                RunManager.Instance.ActionExecutor.Pause();
                await CombatFixture.StartTurn();
                var innateClone = clones[innate];
                Assert(pcs.Hand.Cards.SequenceEqual(new[] { innateClone }.Concat(opening.Where(c => c != innateClone).Take(4))) &&
                    scope.Random.Position(scope.For("shuffle")) == 1,
                    "vanilla first-turn drawing promotes Innate after the addressed shuffle without consuming another shuffle");

                var fixedKeys = clones.Values.ToDictionary(c => c, c => CombatCards.Info(c).Key);
                CardCmd.Enchant<PerfectFit>(clones[reward], 1);
                foreach (var card in clones.Values.Reverse())
                {
                    card.RemoveFromCurrentPile();
                    pcs.DiscardPile.AddInternal(card);
                }
                var reshuffled = Order(Keys(false, false, true), 1).ToList();
                reshuffled.Remove(clones[reward]);
                reshuffled.Insert(0, clones[reward]);
                await CardPileCmd.Shuffle(new ThrowingPlayerChoiceContext(), player);
                Assert(pcs.DrawPile.Cards.SequenceEqual(reshuffled) && fixedKeys.All(p => CombatCards.Info(p.Key).Key == p.Value) &&
                    scope.Random.Position(scope.For("shuffle")) == 2 && vanilla.Equals(run.Rng.Shuffle.ToSerializable()),
                    "reshuffling recomputes keys without retained removals or Innate separation, then applies Perfect Fit while preserving selection keys and vanilla RNG");
                RunManager.Instance.CleanUp();
            }
    }
}
