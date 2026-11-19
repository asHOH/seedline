using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static void CheckWingCharmPolicies()
    {
        var selectedGroups = new HashSet<string>();
        foreach (var seed in new[] { 0, 1, 4, 6 })
            foreach (var (size, rare, mask, candy) in new (int, bool, int, bool)[]
            {
                (3, false, 7, false), (3, false, 2, false), (3, false, 0, false),
                (5, false, 31, false), (5, false, 24, false),
                (8, false, 255, false), (8, false, 248, false),
                (5, true, 31, false), (5, true, 25, false), (5, true, 24, false), (5, true, 9, false),
                (4, false, 15, true), (4, false, 7, true), (4, false, 8, true)
            })
                foreach (var reverse in new[] { false, true })
                {
                    RunManager.Instance.CleanUp();
                    var run = StartGoldRun(seed: $"WING{seed}");
                    var player = run.Players[0];
                    var state = RunCheckpoints.Require(run);
                    var wing = ModelDb.Relic<WingCharm>().ToMutable();
                    player.AddRelicInternal(wing);
                    if (size > 3 && !candy)
                    {
                        EventModel source = size == 8 ? ModelDb.Event<RoomFullOfCheese>() : ModelDb.Event<BrainLeech>();
                        run.PushRoom(new EventRoom(source));
                        RunManager.Instance.EventSynchronizer.BeginEvent(source);
                    }
                    var options = CardCreationOptions.ForNonCombatWithDefaultOdds([player.Character.CardPool]);
                    var pool = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
                        .Where(c => c.Rarity == CardRarity.Common).OrderBy(c => c.Id.Entry, StringComparer.Ordinal).Take(size).ToArray();
                    var offer = RandomAddress.For("Wing check offer").Key;
                    var entries = pool.Select((canonical, i) =>
                    {
                        var identity = candy && i == 3 ? ModelDb.Card<Inflame>() : rare && i == 0 ? ModelDb.Card<Offering>() : canonical;
                        var card = CardGeneration.Create(player, identity, RandomAddress.For("Wing check entry").At(i).Key);
                        CardOffers.Attach(card, offer, i, supplementary: !candy && i >= 3);
                        CardOffers.Info(card).Candy = candy && i == 3;
                        if ((mask & (1 << i)) == 0) CardCmd.Enchant<Glam>(card, 1);
                        return new CardCreationResult(card);
                    }).ToArray();
                    var eventRng = CardRewards.EventRng(player);
                    var expectedRng = new Rng(eventRng.ToSerializable());
                    var rewardsRng = player.PlayerRng.Rewards.ToSerializable();
                    var eligible = entries.Where((_, i) => (mask & (1 << i)) != 0).ToArray();
                    CardCreationResult? expected = null;
                    CardCreationResult Lowest(IEnumerable<CardCreationResult> candidates) => candidates
                        .OrderBy(c => AddressRandom.UInt64(state.Seed, RandomAddress.For("Wing Charm").With(CardOffers.Info(c.Card).WingAddress)))
                        .ThenBy(c => CardOffers.Info(c.Card).WingAddress, StringComparer.Ordinal).First();
                    if (eligible.Length == 1)
                        expected = eligible[0];
                    else if (eligible.Length > 1)
                    {
                        var triple = eligible.Where(c => CardOffers.Info(c.Card).EntryOrder < 3).ToArray();
                        var extras = eligible.Except(triple).ToArray();
                        if (candy && extras.Length > 0)
                        {
                            var roll = AddressRandom.Unit(AddressRandom.UInt64(state.Seed,
                                RandomAddress.For("Wing Charm").With(CardOccurrences.Info(extras[0].Card).Address)));
                            expected = roll < 1d / eligible.Length ? extras[0] : Lowest(triple);
                            selectedGroups.Add(expected == extras[0] ? "Candy extra" : "Candy triple");
                        }
                        else if (!candy && extras.Length > 0 &&
                            (triple.Length == 0 || rare && expectedRng.NextFloat() < (double)extras.Length / eligible.Length))
                        {
                            var index = rare ? AddressRandom.IntInclusive(state.Seed,
                                RandomAddress.For("Wing Charm supplementary selection", "BRAIN_LEECH"), 0, extras.Length - 1)
                                : expectedRng.NextInt(extras.Length);
                            expected = extras[index];
                            if (rare && triple.Length > 0) selectedGroups.Add("Leech extra");
                        }
                        else
                        {
                            expected = Lowest(triple);
                            if (rare && extras.Length > 0) selectedGroups.Add("Leech triple");
                        }
                    }
                    var batch = (reverse ? entries.Reverse() : entries).ToList();
                    var order = batch.Select(c => CardOccurrences.Info(c.Card).Address).ToArray();
                    var before = entries.Select(c => c.Card).ToArray();
                    var changed = wing.TryModifyCardRewardOptionsLate(player, batch, options);
                    var label = $"Wing seed {seed}, size {size}, Rare {rare}, eligible {mask}, Candy {candy}, reversed {reverse}";
                    Assert(changed == (expected != null) && entries.Where(c => c.Card.Enchantment is Swift).SequenceEqual(
                        expected == null ? [] : new[] { expected }), $"{label}: enchants the policy-selected original offer position");
                    Assert(eventRng.ToSerializable().Equals(expectedRng.ToSerializable()) && rewardsRng.Equals(player.PlayerRng.Rewards.ToSerializable()),
                        $"{label}: consumes only the specified event rolls, including empty and singleton cases");
                    Assert(order.SequenceEqual(batch.Select(c => CardOccurrences.Info(c.Card).Address)) && entries.Select((entry, i) =>
                        entry == expected ? entry.Card != before[i] && entry.ModifyingRelics.Contains(wing) &&
                            CardOccurrences.Info(entry.Card) == CardOccurrences.Info(before[i]) && CardOffers.Info(entry.Card) == CardOffers.Info(before[i])
                            : entry.Card == before[i]).All(same => same),
                        $"{label}: preserves display order, untouched cards, and cloned occurrence/offer metadata");
                }
        Assert(selectedGroups.SetEquals(["Candy extra", "Candy triple", "Leech extra", "Leech triple"]),
            "Wing fixtures exercise both outcomes of each weighted group selection");
        foreach (var earlier in new RelicModel[] { ModelDb.Relic<FresnelLens>(), ModelDb.Relic<Glitter>() })
        {
            RunManager.Instance.CleanUp();
            var run = StartGoldRun();
            var player = run.Players[0];
            player.AddRelicInternal(earlier.ToMutable());
            var wing = ModelDb.Relic<WingCharm>().ToMutable();
            player.AddRelicInternal(wing);
            var batch = new CardModel[] { ModelDb.Card<DefendIronclad>(), ModelDb.Card<Anger>(), ModelDb.Card<Bludgeon>() }
                .Select((c, i) => new CardCreationResult(CardGeneration.Create(player, c, RandomAddress.For("Wing modifier check").At(i).Key))).ToList();
            var rewardsRng = player.PlayerRng.Rewards.ToSerializable();
            Hook.TryModifyCardRewardOptions(run, player, batch,
                CardCreationOptions.ForNonCombatWithDefaultOdds([player.Character.CardPool]), out var modifiers);
            Assert(earlier is Glitter ? batch.All(c => c.Card.Enchantment is Glam) && !modifiers.Contains(wing)
                : batch[0].Card.Enchantment is Nimble && batch.Skip(1).Count(c => c.Card.Enchantment is Swift) == 1,
                $"{earlier.Id.Entry}: Wing Charm evaluates eligibility after the actual earlier modifier");
            Assert(rewardsRng.Equals(player.PlayerRng.Rewards.ToSerializable()),
                $"{earlier.Id.Entry}: earlier enchantments and Wing Charm preserve vanilla reward RNG");
        }
        RunManager.Instance.CleanUp();
    }
}
