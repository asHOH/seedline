using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckRugRewards()
    {
        CheckRugBaseOffers();
        CheckRugRestrictions();
        await CheckRugCandy();
    }

    private static void CheckRugBaseOffers()
    {
        foreach (var gem in new[] { false, true })
            foreach (var ascension in new[] { 0, 7 })
            {
                var baseline = new List<(CardModel[] Cards, string State)>();
                var replacements = 0;
                var extraReplacements = 0;
                foreach (var rug in new[] { false, true })
                {
                    RunManager.Instance.CleanUp();
                    var run = StartGoldRun(ascension, seed: "RUGCHECK");
                    run.CurrentActIndex = 2;
                    var player = run.Players[0];
                    if (rug) player.AddRelicInternal(ModelDb.Relic<DingyRug>().ToMutable());
                    if (gem) player.AddRelicInternal(ModelDb.Relic<PrismaticGem>().ToMutable());
                    var state = RunCheckpoints.Require(run);
                    var vanilla = player.PlayerRng.Rewards.ToSerializable();
                    for (var batch = 0; batch < 30; batch++)
                    {
                        var options = batch < 4 ? CardCreationOptions.ForRoom(player, RoomType.Monster) :
                            batch < 10 ? CardCreationOptions.ForNonCombatWithUniformOdds([player.Character.CardPool], c => c.Rarity == CardRarity.Uncommon) :
                            CardCreationOptions.ForNonCombatWithDefaultOdds([player.Character.CardPool]);
                        options.WithFlags(CardCreationFlags.IsCardReward);
                        if (batch >= 10)
                        {
                            state.Rewards.Rare.Threshold = 0.000001;
                            state.Rewards.Rare.Luck = 0;
                            state.Rewards.Rare.Pending = false;
                            var thresholds = RandomAddress.For("Rare threshold");
                            while (-Math.Log(AddressRandom.OpenUnit(AddressRandom.UInt64(state.Seed,
                                thresholds.At(state.Random.Position(thresholds))))) >= -Math.Log(0.97))
                                state.Random.Advance(thresholds);
                        }
                        var cards = CardRewards.Generate(player, 3, options, CardOfferKind.OwnCharacter).Select(c => c.Card).ToArray();
                        if (!rug)
                        {
                            baseline.Add((cards, RugRandomState(state)));
                            continue;
                        }
                        var control = baseline[batch];
                        Assert(RugRandomState(state) == control.State && options.CardPools.Count == 1,
                            $"Rug A{ascension} Gem={gem} batch {batch}: base supplies, Rare timing and source options are unchanged");
                        var pools = gem ? player.UnlockState.CharacterCardPools : [player.Character.CardPool];
                        var eligiblePool = pools.Append(ModelDb.CardPool<ColorlessCardPool>())
                            .SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
                            .Where(c => c.IsAllowedInRun(run) && (options.CardPoolFilter == null || options.CardPoolFilter(c)))
                            .Distinct().ToArray();
                        var retained = new HashSet<ModelId>();
                        foreach (var original in control.Cards.OrderBy(c => CardOffers.Info(c).EntryOrder))
                        {
                            var info = CardOffers.Info(original);
                            var card = cards.Single(c => CardOffers.Info(c).EntryOrder == info.EntryOrder);
                            var candidates = eligiblePool.Where(c => c.Rarity == original.Rarity && !retained.Contains(c.Id)).ToArray();
                            var colorless = candidates.Where(c => c.Pool.IsColorless).OrderBy(c => c.Id.Entry, StringComparer.Ordinal).ToArray();
                            var address = CardOccurrences.Info(original).Address;
                            if (CardRewards.Unit(state, "Rug replacement", address) < (double)colorless.Length / candidates.Length)
                            {
                                var replacementAddress = RandomAddress.For("Rug colorless identity").With(address);
                                var expected = colorless[AddressRandom.IntInclusive(state.Seed, replacementAddress, 0, colorless.Length - 1)];
                                Assert(card.Id == expected.Id && CardOccurrences.Info(card).Address == replacementAddress.Key,
                                    "Rug identity uses the post-extra-Rare entry and excludes only earlier final identities");
                                replacements++;
                                if (info.RareOrder > 0) extraReplacements++;
                            }
                            else
                                Assert(card.Id == original.Id && CardOccurrences.Info(card).Address == address,
                                    "Rug retains the original identity and occurrence when its entry roll does not replace it");
                            Assert(CardOffers.Info(card) == info && Array.IndexOf(cards, card) == Array.IndexOf(control.Cards, original),
                                "Rug preserves offer, entry order, Wing priority, membership and simulated Rare position");
                            if (!options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll))
                            {
                                var chance = card.Rarity == CardRarity.Rare ? 0 : ascension >= 7 ? 0.25 : 0.50;
                                var upgraded = CardRewards.Unit(state, "upgrade", CardOccurrences.Info(card).Address) <= chance;
                                Assert((card.CurrentUpgradeLevel > 0) == upgraded, "Rug upgrades use the final entry's roll");
                            }
                            retained.Add(card.Id);
                        }
                        Assert(retained.Count == cards.Length, "Rug offers retain distinct identities after replacements");
                    }
                    Assert(vanilla.Equals(player.PlayerRng.Rewards.ToSerializable()), "Rug generation leaves vanilla reward RNG untouched");
                }
                Assert(replacements > 0 && (ascension >= 7 || extraReplacements > 0),
                    $"Rug A{ascension} Gem={gem} fixtures exercise replacements, including A0 extra Rares");
            }
        RunManager.Instance.CleanUp();
    }

    private static void CheckRugRestrictions()
    {
        var baseline = new List<(string[] Cards, string State)>();
        foreach (var rug in new[] { false, true })
        {
            RunManager.Instance.CleanUp();
            var run = StartGoldRun();
            var player = run.Players[0];
            if (rug) player.AddRelicInternal(ModelDb.Relic<DingyRug>().ToMutable());
            var requests = new[]
            {
                (CardCreationOptions.ForNonCombatWithDefaultOdds([player.Character.CardPool]), CardOfferKind.OwnCharacter),
                (CardCreationOptions.ForNonCombatWithDefaultOdds([player.Character.CardPool])
                    .WithFlags(CardCreationFlags.IsCardReward | CardCreationFlags.NoCardPoolModifications), CardOfferKind.OwnCharacter),
                (CardCreationOptions.ForNonCombatWithUniformOdds([player.Character.CardPool], c => c.Rarity == CardRarity.Common)
                    .WithFlags(CardCreationFlags.IsCardReward), CardOfferKind.OwnCharacter),
                (CardCreationOptions.ForNonCombatWithUniformOdds([ModelDb.CardPool<ColorlessCardPool>()])
                    .WithFlags(CardCreationFlags.IsCardReward), CardOfferKind.Colorless)
            };
            for (var i = 0; i < requests.Length; i++)
            {
                var (options, kind) = requests[i];
                var cards = CardRewards.Generate(player, 3, options, kind).Select(c => RugCard(c.Card)).ToArray();
                var state = RugRandomState(RunCheckpoints.Require(run));
                if (!rug) baseline.Add((cards, state));
                else Assert(cards.SequenceEqual(baseline[i].Cards) && state == baseline[i].State,
                    $"Rug restriction {i}: flagless, blocked, fixed-Common and already-colorless requests retain outcomes and state");
            }
        }
        RunManager.Instance.CleanUp();
    }

    private static async Task CheckRugCandy()
    {
        var run = StartGoldRun(seed: "RUGCANDY");
        run.AppendToMapPointHistory(MapPointType.Monster, RoomType.Monster, null);
        var player = run.Players[0];
        player.AddRelicInternal(ModelDb.Relic<DingyRug>().ToMutable());
        var candy = (LastingCandy)ModelDb.Relic<LastingCandy>().ToMutable();
        candy.CombatRewardsSeen = 1;
        player.AddRelicInternal(candy);
        var colorlessDraws = 0;
        var coloredDraws = 0;
        for (var i = 0; i < 24; i++)
        {
            if (i == 12) player.AddRelicInternal(ModelDb.Relic<PrismaticGem>().ToMutable());
            var state = RunCheckpoints.Require(run);
            var before = state.Copy();
            var reward = new CardReward(CardCreationOptions.ForRoom(player, RoomType.Monster)
                .WithFlags(CardCreationFlags.IsFromCombat), 3, player);
            reward.Populate();
            var cards = reward.Cards.ToArray();
            var power = cards.Single(c => CardOffers.Info(c).Candy);
            var parent = cards.Where(c => c != power).ToArray();
            var pools = i < 12 ? new[] { player.Character.CardPool } : player.UnlockState.CharacterCardPools.ToArray();
            var full = pools.Append(ModelDb.CardPool<ColorlessCardPool>())
                .SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
                .Where(c => c.IsAllowedInRun(run) && c.Type == CardType.Power && c.Rarity == power.Rarity).Distinct().ToArray();
            var eligible = full.Where(c => parent.All(p => p.Id != c.Id)).ToArray();
            var choice = RandomAddress.For("Rug color choice", power.Rarity.ToString());
            var colorless = before.Random.NextUnit(choice) < (double)eligible.Count(c => c.Pool.IsColorless) / eligible.Length;
            var poolIds = string.Join("/", pools.Select(p => p.Id.Entry).Order(StringComparer.Ordinal));
            var supply = colorless ? RandomAddress.For("card identity", "Rug Power", power.Rarity.ToString()) :
                RandomAddress.For("card identity", "Power", poolIds, power.Rarity.ToString());
            var expected = before.Rewards.Draw(state.Seed, supply, full.Where(c => c.Pool.IsColorless == colorless).Select(c => c.Id.Entry),
                eligible.Where(c => c.Pool.IsColorless == colorless).Select(c => c.Id.Entry));
            Assert(RewardEntries.Find(power) == expected && CardOffers.Info(power).Offer == CardOffers.Info(parent[0]).Offer &&
                cards.Select(c => c.Id).Distinct().Count() == cards.Length,
                "Candy chooses color from post-Rug exclusions and consumes the selected Power supply in its parent offer");
            Assert(JsonSerializer.Serialize(state.Rewards.Supplies) == JsonSerializer.Serialize(before.Rewards.Supplies) &&
                state.Random.Position(choice) == before.Random.Position(choice),
                "Candy advances only its chosen identity supply and one rarity-partitioned color-choice roll");
            if (colorless) colorlessDraws++; else coloredDraws++;
        }
        Assert(colorlessDraws > 0 && coloredDraws > 0, "Candy fixtures exercise both color branches across Gem acquisition");

        var snapshot = RunManager.Instance.ToSave(null);
        var bytes = RunCheckpoints.Encode(snapshot);
        string[] Next()
        {
            var reward = new CardReward(CardCreationOptions.ForRoom(player, RoomType.Elite)
                .WithFlags(CardCreationFlags.IsFromCombat), 3, player);
            reward.Populate();
            reward.Reroll();
            return reward.Cards.Select(RugCard).ToArray();
        }
        var expectedReload = Next();
        var expectedState = RugRandomState(RunCheckpoints.Require(run));
        var restored = RunCheckpoints.Decode(Encoding.UTF8.GetString(bytes), snapshot.SchemaVersion);
        RunManager.Instance.CleanUp();
        run = RunState.FromSerializable(restored);
        await RunManager.Instance.SetUpSavedSingleplayer(run, restored);
        player = run.Players[0];
        Assert(Next().SequenceEqual(expectedReload) && RugRandomState(RunCheckpoints.Require(run)) == expectedState,
            "Rug, Gem and Candy checkpoint replay reproduces rerolled identities, metadata, upgrades and supply state");
        RunManager.Instance.CleanUp();
    }

    private static string RugCard(CardModel card) =>
        $"{card.Id}|{card.CurrentUpgradeLevel}|{CardOccurrences.Info(card).Address}|{CardOffers.Info(card)}";

    private static string RugRandomState(CheckpointState state)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            state.Random.Write(writer);
            writer.WritePropertyName("supplies");
            JsonSerializer.Serialize(writer, state.Rewards.Supplies);
            writer.WritePropertyName("rare");
            JsonSerializer.Serialize(writer, state.Rewards.Rare);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
