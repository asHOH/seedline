using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Dread.Dominions;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckInsertionSources()
    {
        foreach (var mode in new[] { GameMode.Standard, GameMode.Dread })
        {
            var run = StartGoldRun(mode: mode);
            var player = run.Players[0];
            RandomAddress? previousCombat = null;
            for (var battle = 0; battle < 2; battle++)
            {
                var combat = CombatFixture.CreateActiveCombat(run);
                var scope = CombatStateScope.Require(combat);
                var draw = player.PlayerCombatState!.DrawPile;
                var discard = player.PlayerCombatState.DiscardPile;
                var shuffle = run.Rng.Shuffle.ToSerializable();
                var ordinals = new Dictionary<RandomAddress, long>();
                RandomAddress Address(string group, CardPile pile) =>
                    RandomAddress.For("insertion", group, pile.Type.ToString()).With(scope.Address.Key);

                async Task Check(string group, CardPile pile, int count, Func<Task> action, int drawn = 0)
                {
                    var expected = pile.Cards.Skip(drawn).ToList();
                    var historyCount = CombatManager.Instance.History.Entries.Count();
                    await action();
                    // History records generation before insertion, independently of final pile order.
                    var generated = CombatManager.Instance.History.Entries.Skip(historyCount).OfType<CardGeneratedEntry>()
                        .Select(e => e.Card).Where(c => c.Pile == pile).ToArray();
                    var address = Address(group, pile);
                    var ordinal = ordinals.GetValueOrDefault(address);
                    foreach (var card in generated)
                    {
                        var u = AddressRandom.Unit(AddressRandom.UInt64(scope.Seed, address.At(ordinal++)));
                        expected.Insert((int)Math.Floor(u * (expected.Count + 1)), card);
                    }
                    ordinals[address] = ordinal;
                    Assert(generated.Length == count && pile.Cards.SequenceEqual(expected) &&
                        ordinals.All(p => scope.Random.Position(p.Key) == p.Value),
                        $"{mode}, battle {battle + 1}: {group} source inserts {count} cards into {pile.Type} at exact sequential gaps without advancing other groups");
                }

                async Task Play<T>(bool upgrade = false) where T : CardModel
                {
                    var card = combat.CreateCard<T>(player);
                    if (upgrade) CardCmd.Upgrade(card);
                    player.PlayerCombatState.PlayPile.AddInternal(card);
                    await (Task)AccessTools.Method(typeof(T), "OnPlay").Invoke(card,
                        [new ThrowingPlayerChoiceContext(), new CardPlay
                        {
                            Card = card, Player = player, Target = combat.Enemies[0], ResultPile = PileType.Discard,
                            Resources = new() { EnergySpent = 2, EnergyValue = 2, StarsSpent = 0, StarValue = 0 },
                            IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
                        }])!;
                    card.RemoveFromCurrentPile();
                }

                async Task Move<T>(string method) where T : MonsterModel
                {
                    var creature = await CreatureCmd.Add<T>(combat);
                    await (Task)AccessTools.Method(typeof(T), method).Invoke(creature.Monster, [new[] { player.Creature }])!;
                }

                Assert(previousCombat != scope.Address && scope.Random.Position(Address("Soul", draw)) == 0,
                    "each combat starts a fresh, separately addressed insertion sequence");
                await Check("Soul", draw, 1, () => Play<GraveWarden>());
                await Check("Soul", draw, 3, () => Play<CaptureSpirit>());
                await Check("The Sun", draw, 2, () => Play<TheSun>(), drawn: 2);
                var hug = (BiiigHug)ModelDb.Relic<BiiigHug>().ToMutable();
                player.AddRelicInternal(hug);
                await Check("Biiig Hug", draw, 1, () => hug.AfterShuffle(new ThrowingPlayerChoiceContext(), player));
                player.RemoveRelicInternal(hug);
                await Check("The Sun", draw, 2, () => Play<TheSun>(true), drawn: 3);
                Assert(scope.Random.Position(Address("The Sun", draw)) == 4 && scope.Random.Position(Address("Biiig Hug", draw)) == 1,
                    "The Sun's two-card batches and Biiig Hug's Soot keep independent insertion progression");
                if (battle == 0)
                {
                    await Check("Dazed", draw, 3, () => Play<MightyShield>());
                    await Check("Dazed", draw, 1, () => Move<Noisebot>("NoiseMove"));
                    Assert(discard.Cards.Count(c => c is Dazed) == 1 && scope.Random.Position(Address("Dazed", discard)) == 0,
                        "Noisebot's fixed discard Dazed does not consume a random insertion");

                    await Check("Metamorphosis", draw, 3, () => Play<Metamorphosis>());
                    await Check("Metamorphosis", draw, 5, () => Play<Metamorphosis>(true));
                    await CardPileCmd.Shuffle(new ThrowingPlayerChoiceContext(), player);
                    await Check("Soul", draw, 1, () => Play<GraveWarden>());

                    if (mode == GameMode.Dread)
                    {
                        var dominion = ModelDb.Dominion<SoulFyshDominion>().MutableClone();
                        await Check("Soul Fysh", draw, 1, async () =>
                            await (Task)AccessTools.Method(typeof(SoulFyshDominion), "AfterPlayersEnteredCombat")
                                .Invoke(dominion, [new[] { player }])!);
                    }
                    await Check("Soul Fysh", draw, 1, () => Move<SoulFysh>("BeckonMove"));
                    Assert(discard.Cards.Count(c => c is Beckon) == 1 && scope.Random.Position(Address("Soul Fysh", discard)) == 0 &&
                        player.PlayerCombatState.Hand.Cards.Count(c => c is Beckon) == (mode == GameMode.Dread ? 1 : 0) &&
                        scope.Random.Position(Address("Soul Fysh", player.PlayerCombatState.Hand)) == 0,
                        "Soul Fysh shares Dominion's draw sequence while fixed discard and Dread hand grants consume no gaps");

                    var witherPile = mode == GameMode.Dread ? draw : discard;
                    await Check("Aeonglass", witherPile, mode == GameMode.Dread ? 2 : 1,
                        () => Move<Aeonglass>("IncreasingIntensityMove"));
                    Assert(scope.Random.Position(Address("Aeonglass", mode == GameMode.Dread ? discard : draw)) == 0,
                        "Aeonglass's Dread override consumes only the actual destination's insertion sequence");
                }
                Assert(shuffle.Equals(run.Rng.Shuffle.ToSerializable()),
                    "actual card, enemy and Dominion insertions plus an intervening reshuffle leave vanilla Shuffle RNG untouched");
                previousCombat = scope.Address;
                CombatManager.Instance.Reset(true);
            }
            RunManager.Instance.CleanUp();
        }
    }
}
