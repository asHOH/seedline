using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckRewardSupplies()
    {
        var previous = TestMode.IsOn;
        TestMode.IsOn = true;
        try
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            RelicRewards.Initialize(player);
            var state = RunCheckpoints.Require(run);
            foreach (var (rarity, counts) in new[]
            {
                (RelicRarity.Common, new[] { 12, 9, 5 }),
                (RelicRarity.Uncommon, new[] { 14, 11, 7 }),
                (RelicRarity.Rare, new[] { 17, 13, 8 })
            })
            {
                var entries = new[] { "generic", "shop", "Courier" }.Select(s => state.Rewards.Supply(RelicRewards.Address(s, rarity)).Positions).ToArray();
                Assert(entries.Select(e => e.Count).SequenceEqual(counts) && entries.SelectMany(e => e).Select(e => e.Entry!.Identity).Distinct().Count() == counts.Sum(),
                    $"{rarity}: fully unlocked allocations have the documented disjoint sizes");
            }
            var beforeShop = state.Random.Position(RandomAddress.For("relic rarity", "generic"));
            var shop = MerchantInventory.CreateForNormalMerchant(player);
            Assert(shop.CardEntries.Select(e => e.CreationResult!.Card.Id).Distinct().Count() == 7 &&
                shop.CharacterCardEntries.Count(e => e.IsOnSale) == 1 && shop.PotionEntries.Select(e => e.Model!.Id).Distinct().Count() == 3,
                "shop generates seven distinct cards, one sale and three distinct potions");
            Assert(state.Random.Position(RandomAddress.For("relic rarity", "generic")) == beforeShop &&
                state.Random.Position(RandomAddress.For("relic rarity", "shop")) == 2,
                "initial shop relic rarity leaves generic rewards unchanged");
            var costs = shop.AllEntries.Select(e => e.Cost).ToArray();
            foreach (var entry in shop.AllEntries.Where(e => e is not MerchantCardRemovalEntry))
                entry.CalcCost();
            Assert(costs.SequenceEqual(shop.AllEntries.Select(e => e.Cost)), "recalculating prices preserves entry-addressed base prices and sale");
            AccessTools.Method(typeof(MerchantPotionEntry), "RestockAfterPurchase").Invoke(shop.PotionEntries[0], [shop]);
            var restocked = shop.PotionEntries[0].Model!;
            var firstCourier = RandomAddress.For("potion identity", "Courier", restocked.Rarity.ToString()).At(0);
            var courierPool = PotionFactory.GetPotionOptions(player).Where(p => p.Rarity == restocked.Rarity).OrderBy(p => p.Id.Entry, StringComparer.Ordinal).ToArray();
            var expectedId = courierPool[AddressRandom.IntInclusive(state.Seed, firstCourier, 0, courierPool.Length - 1)].Id.Entry;
            Assert(restocked.Id.Entry == expectedId && RewardEntries.Find(restocked)?.Address == firstCourier.Key &&
                state.Random.Position(RandomAddress.For("potion rarity", "Courier", "standard")) == 1 &&
                state.Random.Position(RandomAddress.For("potion rarity", "shop", "standard")) == 3,
                "Courier potion restocking has an independent source");
            var snapshot = RunManager.Instance.ToSave(null);
            var bytes = RunCheckpoints.Encode(snapshot);
            var expected = PotionFactory.CreateRandomPotionOutOfCombat(player, player.PlayerRng.Rewards).Id;
            var relic = RelicFactory.PullNextRelicForRewardFromFront(player).Id;
            Assert(RunCheckpoints.Encode(snapshot).SequenceEqual(bytes), "reward supply mutation leaves captured checkpoint isolated");
            var decoded = RunCheckpoints.Decode(Encoding.UTF8.GetString(bytes), snapshot.SchemaVersion);
            RunManager.Instance.CleanUp();
            run = RunState.FromSerializable(decoded);
            await RunManager.Instance.SetUpSavedSingleplayer(run, decoded);
            player = run.Players[0];
            Assert(PotionFactory.CreateRandomPotionOutOfCombat(player, player.PlayerRng.Rewards).Id == expected &&
                RelicFactory.PullNextRelicForRewardFromFront(player).Id == relic,
                "checkpoint restores potion markers and finite relic supplies before subsequent generation");
            state = RunCheckpoints.Require(run);
            var dropAddress = RandomAddress.For("potion drop");
            player.AddRelicInternal(ModelDb.Relic<WhiteBeastStatue>().ToMutable());
            var chance = player.PlayerOdds.PotionReward.CurrentValue;
            var ordinal = state.Random.Position(dropAddress);
            Assert(player.PlayerOdds.PotionReward.Roll(player, MegaCrit.Sts2.Core.Rooms.RoomType.Elite) &&
                player.PlayerOdds.PotionReward.CurrentValue == chance && state.Random.Position(dropAddress) == ordinal,
                "White Beast Statue freezes drop probability and ordinal");
            while (player.HasOpenPotionSlots)
                player.AddPotionInternal(ModelDb.Potion<BlockPotion>().ToMutable());
            SupplyEntry NextPotion(RewardState rewards, PotionRarity rarity)
            {
                var ids = PotionFactory.GetPotionOptions(player).Where(p => p.Rarity == rarity).Select(p => p.Id.Entry).ToArray();
                return rewards.Draw(state.Seed, RandomAddress.For("potion identity", "generic", rarity.ToString()), ids, ids);
            }
            SupplyEntry[] NextByRarity(RewardState rewards) =>
                new[] { PotionRarity.Common, PotionRarity.Uncommon, PotionRarity.Rare }.Select(r => NextPotion(rewards.Copy(), r)).ToArray();
            var control = state.Rewards.Copy();
            var originalPotions = player.Potions.ToArray();
            var generic = state.Random.Position(RandomAddress.For("potion rarity", "generic", "standard"));
            var holster = ModelDb.Relic<PhialHolster>().ToMutable();
            player.AddRelicInternal(holster);
            await holster.AfterObtained();
            var acquired = player.Potions.Except(originalPotions).Single();
            var acquiredEntry = NextPotion(control, acquired.Rarity);
            var nextEntries = NextByRarity(control);
            Assert(acquired.Id.Entry == acquiredEntry.Identity && RewardEntries.Find(acquired) == acquiredEntry &&
                NextByRarity(state.Rewards).SequenceEqual(nextEntries) &&
                state.Random.Position(RandomAddress.For("potion rarity", "generic", "standard")) == generic + 1 && !player.HasOpenPotionSlots,
                "Phial Holster commits one acquired potion while its failed second grant leaves supplies unchanged");
            player.AddRelicInternal(ModelDb.Relic<Sozu>().ToMutable());
            var failedGrant = state.Random.Position(RandomAddress.For("potion rarity", "generic", "standard"));
            var blocked = ModelDb.Relic<PhialHolster>().ToMutable();
            player.AddRelicInternal(blocked);
            await blocked.AfterObtained();
            Assert(state.Random.Position(RandomAddress.For("potion rarity", "generic", "standard")) == failedGrant &&
                NextByRarity(state.Rewards).SequenceEqual(nextEntries),
                "Sozu-blocked direct potion grants preserve rarity and identity progression");

            RunManager.Instance.CleanUp();
            run = StartGoldRun();
            player = run.Players[0];
            state = RunCheckpoints.Require(run);
            var chest = RunManager.Instance.TreasureRoomRelicSynchronizer;
            chest.BeginRelicPicking();
            var offered = chest.CurrentRelics!.Single();
            var chestRarity = state.Random.Position(RandomAddress.For("relic rarity", "generic"));
            Assert(state.Random.Position(RandomAddress.For("chest")) == 1 && chestRarity == 1 && state.Rewards.ChestOffers.Contains(offered.Id.Entry),
                "chest entry generates both rewards and records the offered identity before opening");
            var beforeGold = player.Gold;
            var gold = await (Task<int>)AccessTools.Method(typeof(OneOffSynchronizer), "DoTreasureRoomRewards")
                .Invoke(RunManager.Instance.OneOffSynchronizer, [player])!;
            Assert(gold is >= 31 and <= 39 && player.Gold == beforeGold + gold &&
                state.Random.Position(RandomAddress.For("gold reward", "normal")) == 0,
                "chest gold preserves Poverty rounding and leaves combat-gold progression unchanged");
            chest.OnPicked(player, null);
            chest.OnRoomExited();
            Assert(!state.Rewards.RetiredRelics.Contains(offered.Id.Entry) &&
                state.Rewards.Supplies.Values.SelectMany(s => s.Positions).Any(p => p.Carried?.Identity == offered.Id.Entry),
                "skipping a chest retains its carried entry for later ordinary rewards");
            player.AddRelicInternal(offered.ToMutable());
            Assert(state.Rewards.Supplies.Values.SelectMany(s => s.Positions).All(p => p.Carried?.Identity != offered.Id.Entry),
                "acquiring a carried chest relic clears that entry while retaining marker advancement");
            var crucible = ModelDb.Relic<SilverCrucible>().ToMutable();
            player.AddRelicInternal(crucible);
            chest.BeginRelicPicking();
            Assert(state.Random.Position(RandomAddress.For("chest")) == 1 &&
                state.Random.Position(RandomAddress.For("relic rarity", "generic")) == chestRarity,
                "Silver Crucible's empty chest consumes neither ordinal nor relic rarity");

            RunManager.Instance.CleanUp();
            run = StartGoldRun();
            player = run.Players[0];
            var identities = new List<ModelId>();
            for (var i = 0; i < 150; i++)
            {
                var drawn = RelicRewards.Draw(player, "generic", RelicRarity.Common);
                if (drawn is Circlet)
                    break;
                identities.Add(drawn.Id);
            }
            Assert(identities.Count == 96 && identities.Distinct().Count() == 96,
                "generic draws expose every eligible ordinary relic once across borrowing and rarity fallback");
            foreach (var (source, firstDonor, secondDonor) in new[]
                { ("generic", "Courier", "shop"), ("shop", "Courier", "generic"), ("Courier", "generic", "shop") })
            {
                RunManager.Instance.CleanUp();
                run = StartGoldRun();
                player = run.Players[0];
                RelicRewards.Initialize(player);
                var supplies = RunCheckpoints.Require(run).Rewards;
                var allowed = RelicRewards.Pool(player).Where(r => r.IsAllowed(run)).Select(r => r.Id.Entry).ToHashSet();
                var donorTails = new[] { firstDonor, secondDonor }.SelectMany(donor =>
                    supplies.Supply(RelicRewards.Address(donor, RelicRarity.Common)).Positions.AsEnumerable().Reverse()
                        .Select(p => p.Entry!).Where(e => allowed.Contains(e.Identity)).Take(2)).ToArray();
                var wanted = donorTails.Select(e => e.Identity).ToHashSet();
                var borrowed = donorTails.Select(_ => RewardEntries.Find(RelicRewards.Draw(player, source, RelicRarity.Common,
                    r => wanted.Contains(r.Id.Entry)))).ToArray();
                Assert(donorTails.Length == 4 && borrowed.SequenceEqual(donorTails),
                    $"{source} borrows eligible tails from {firstDonor} before {secondDonor}, retaining donor entry addresses");
            }
        }
        finally
        {
            RunManager.Instance.CleanUp();
            TestMode.IsOn = previous;
        }
    }
}
