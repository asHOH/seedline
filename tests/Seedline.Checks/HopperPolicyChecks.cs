using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckHopperPolicy()
    {
        for (var tier = 0; tier < 7; tier++)
        {
            string? firstKey = null;
            // Claim and skip every tier's reward; also exercise escape with a nonempty eligible set.
            for (var outcome = 0; outcome < (tier == 0 ? 3 : 2); outcome++)
            {
                var run = StartGoldRun();
                var player = run.Players[0];
                var state = RunCheckpoints.Require(run);
                CardModel[] added = [run.CreateCard<Inflame>(player), run.CreateCard<FlameBarrier>(player),
                    run.CreateCard<Anger>(player), run.CreateCard<DemonForm>(player), run.CreateCard<Metamorphosis>(player),
                    run.CreateCard<Relax>(player), run.CreateCard<Shockwave>(player),
                    run.CreateCard<Injury>(player), run.CreateCard<Decay>(player), run.CreateCard<SpoilsMap>(player)];
                await CardPileCmd.Add(added, PileType.Deck);
                var combat = CombatFixture.CreateActiveCombat(run);
                var pcs = player.PlayerCombatState!;
                var room = new CombatRoom(combat);
                run.PushRoom(room);
                run.AppendToMapPointHistory(MapPointType.Monster, RoomType.Monster, room.ModelId);
                var hopper = ModelDb.Monster<ThievingHopper>().ToMutable();
                var creature = combat.CreateCreature(hopper, CombatSide.Enemy, null);
                combat.AddCreature(creature);
                var originals = pcs.DrawPile.Cards.ToArray();
                CardModel Copy(CardModel deckCard) => originals.Single(c => c.DeckVersion == deckCard);
                var imbued = Copy(added[6]);
                CardCmd.Enchant<Imbued>(imbued, 1);
                Assert(imbued.Enchantment is Imbued && added[6].Enchantment == null,
                    "Hopper eligibility fixture changes only the combat copy's enchantment");
                CardModel[][] tiers =
                [
                    [Copy(added[0]), Copy(added[1])],
                    [Copy(added[2]), Copy(added[3]), Copy(added[4])],
                    originals.Where(c => c.Rarity == CardRarity.Basic).ToArray(),
                    [Copy(added[5]), imbued],
                    [Copy(added[7]), Copy(added[8])]
                ];
                var candidates = tier < 5 ? tiers[tier] : tier == 5 ? new[] { Copy(added[7]) } : [];
                var available = tier < 5 ? tiers.Skip(tier).SelectMany(c => c).ToArray() : candidates;
                foreach (var card in originals)
                {
                    card.RemoveFromCurrentPile();
                    pcs.ExhaustPile.AddInternal(card);
                }
                var order = outcome == 1 ? available.Reverse().ToArray() : available;
                for (var i = 0; i < order.Length; i++)
                {
                    order[i].RemoveFromCurrentPile();
                    (i % 2 == 0 ? pcs.DrawPile : pcs.DiscardPile).AddInternal(order[i]);
                }
                // Neither a linked Quest nor a fresh clone of a linked Uncommon may enter the theft pool.
                var quest = Copy(added[9]);
                quest.RemoveFromCurrentPile();
                pcs.DrawPile.AddInternal(quest);
                var clone = Copy(added[0]).CreateClone();
                pcs.DrawPile.AddInternal(clone);
                Assert(clone.DeckVersion == null && quest.DeckVersion != null,
                    "a combat clone loses its deck link while Quest exclusion remains necessary");
                var scope = CombatStateScope.Require(combat);
                var expected = candidates.OrderBy(c => AddressRandom.UInt64(scope.Seed,
                    RandomAddress.For("Thieving Hopper").With(CardOccurrences.Info(c.DeckVersion!).Key)))
                    .ThenBy(c => CardOccurrences.Info(c.DeckVersion!).Key, StringComparer.Ordinal).FirstOrDefault();
                Assert(candidates.Length == 0 || candidates.Any(c => CombatCards.Info(c).Key != CardOccurrences.Info(c.DeckVersion!).Key),
                    "Hopper's fixture distinguishes combat shuffle keys from permanent card keys");
                var original = expected?.DeckVersion;
                var info = original == null ? null : CardOccurrences.Info(original);
                if (info != null)
                {
                    state.Selection.Get(state.Seed, PermanentSelection.Upgrade, info.Key);
                    firstKey ??= info.Key;
                    Assert(info.Key == firstKey, "Hopper's fixed priorities ignore draw/discard order and distribution");
                }
                var savedClocks = JsonSerializer.Serialize(state.Selection);
                // Unrelated combat selection must not enter Hopper's run-wide priority address.
                if (outcome == 1) CombatSelection.Select([clone, quest], 1, "Hopper unrelated selection");
                var combatClocks = JsonSerializer.Serialize(scope.Selection);
                var vanilla = run.Rng.CombatCardSelection.ToSerializable();
                var deckCount = player.Deck.Cards.Count;
                await (Task)AccessTools.Method(typeof(ThievingHopper), "ThieveryMove").Invoke(hopper, [new[] { player.Creature }])!;
                var swipe = creature.Powers.OfType<SwipePower>().SingleOrDefault();
                Assert((expected == null ? swipe == null : swipe?.StolenCard == expected) &&
                    player.Deck.Cards.Count == deckCount - (expected == null ? 0 : 1) &&
                    clone.Pile == pcs.DrawPile && quest.Pile == pcs.DrawPile &&
                    vanilla.Equals(run.Rng.CombatCardSelection.ToSerializable()) && combatClocks == JsonSerializer.Serialize(scope.Selection),
                    $"Hopper tier {tier}: exact permanent-key priority selects only eligible combat copies without consuming selection clocks or RNG");
                if (original != null && info != null)
                {
                    Assert(!player.Deck.Cards.Contains(original) && state.Cards.Removed.ContainsKey(info.Address),
                        "theft retires the linked permanent occurrence and retains its removed shuffle entry");
                    if (outcome == 2)
                    {
                        await (Task)AccessTools.Method(typeof(ThievingHopper), "EscapeMove").Invoke(hopper, [new[] { player.Creature }])!;
                        Assert(!room.ExtraRewards.ContainsKey(player) && state.Cards.Removed.ContainsKey(info.Address) &&
                            !player.Deck.Cards.Contains(original), "Hopper escape leaves the stolen occurrence removed without a reward");
                    }
                    else
                    {
                        await CreatureCmd.Kill(creature);
                        var reward = room.ExtraRewards[player].OfType<SpecialCardReward>().Single();
                        Assert(!player.Deck.Cards.Contains(original) && state.Cards.Removed.ContainsKey(info.Address),
                            "Hopper's death offers recovery without prematurely reacquiring the stolen card");
                        if (outcome == 0)
                        {
                            await reward.SelectUnsynchronized();
                            Assert(player.Deck.Cards.Contains(original) && CardOccurrences.Info(original) == info &&
                                !state.Cards.Removed.ContainsKey(info.Address),
                                "claiming Hopper's reward restores the exact occurrence, acquisition and card key and clears its removal entry");
                        }
                        else
                        {
                            reward.OnSkipped();
                            Assert(!player.Deck.Cards.Contains(original) && state.Cards.Removed.ContainsKey(info.Address),
                                "skipping Hopper's recovery leaves the removed occurrence retired");
                        }
                    }
                    Assert(savedClocks == JsonSerializer.Serialize(state.Selection),
                        "Hopper theft and recovery preserve permanent selection history");
                }
                RunManager.Instance.CleanUp();
            }
        }
    }
}
