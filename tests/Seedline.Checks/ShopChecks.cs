using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckShopPolicies()
    {
        string? expected = null;
        foreach (var potionFirst in new[] { false, true })
        {
            RunManager.Instance.CleanUp();
            var run = StartGoldRun();
            var player = run.Players[0];
            run.PushRoom(new MerchantRoom());
            run.AppendToMapPointHistory(MapPointType.Shop, RoomType.Shop, null);
            var vanilla = player.PlayerRng.Shops.ToSerializable();
            var shop = MerchantInventory.CreateForNormalMerchant(player);
            var state = RunCheckpoints.Require(run);
            var entries = shop.AllEntries.ToArray();
            var costs = entries.Select(e => e.Cost).ToArray();
            player.AddRelicInternal(ModelDb.Relic<TheCourier>().ToMutable());
            Assert(entries.Select(e => e.Cost).SequenceEqual(costs.Select(c => (int)(c * 0.8m))),
                "Courier applies its live discount to every shop entry without rerolling prices");
            player.AddRelicInternal(ModelDb.Relic<MembershipCard>().ToMutable());
            Assert(entries.Select(e => e.Cost).SequenceEqual(costs.Select(c => (int)(c * 0.4m))),
                "Membership and Courier discounts compose before integer truncation");

            var attacks = shop.CharacterCardEntries.Where(e => e.CreationResult!.Card.Type == CardType.Attack).ToArray();
            var addresses = attacks.Select(e => RewardEntries.Find(e.CreationResult!.Card)).ToArray();
            player.AddRelicInternal(ModelDb.Relic<MoltenEgg>().ToMutable());
            foreach (var entry in attacks) entry.OnMerchantInventoryUpdated();
            Assert(attacks.All(e => e.CreationResult!.Card.IsUpgraded) &&
                attacks.Select(e => RewardEntries.Find(e.CreationResult!.Card)).SequenceEqual(addresses),
                "merchant modifier updates upgrade displayed attacks without replacing their price addresses");

            var cardEntry = shop.CharacterCardEntries.Single(e => e.IsOnSale);
            var original = cardEntry.CreationResult!.Card;
            var originalColorless = shop.ColorlessCardEntries[0].CreationResult!.Card;
            var potionEntry = shop.PotionEntries[0];
            var originalPotion = potionEntry.Model!;
            if (potionFirst)
                Assert(await potionEntry.OnTryPurchaseUnsynchronized(shop, ignoreCost: true), "Courier potion purchase succeeds");
            Assert(await cardEntry.OnTryPurchaseUnsynchronized(shop, ignoreCost: true), "Courier sale-card purchase succeeds");
            if (!potionFirst)
                Assert(await potionEntry.OnTryPurchaseUnsynchronized(shop, ignoreCost: true), "Courier potion purchase succeeds");
            var rarityPositions = Enum.GetValues<CardType>().Select(t => state.Random.Position(RandomAddress.For("Courier card rarity", t.ToString()))).ToArray();
            Assert(await shop.ColorlessCardEntries[0].OnTryPurchaseUnsynchronized(shop, ignoreCost: true), "Courier colorless purchase succeeds");
            var restocked = cardEntry.CreationResult!.Card;
            Assert(player.Deck.Cards.Any(c => c.Id == original.Id) && player.Potions.Contains(originalPotion) &&
                restocked.Type == original.Type && !cardEntry.IsOnSale && restocked.Id != original.Id &&
                shop.ColorlessCardEntries[0].CreationResult!.Card.Rarity == originalColorless.Rarity,
                "actual purchases acquire items, clear the sale and preserve restock type or fixed rarity");
            Assert(shop.CardEntries.Select(e => e.CreationResult!.Card.Id).Distinct().Count() == 7 &&
                state.Random.Position(RandomAddress.For("Courier card rarity", original.Type.ToString())) == 1 &&
                Enum.GetValues<CardType>().Select(t => state.Random.Position(RandomAddress.For("Courier card rarity", t.ToString()))).SequenceEqual(rarityPositions),
                "Courier card restocks exclude displayed identities and fixed-rarity cards consume no rarity roll");

            AccessTools.Method(typeof(MerchantRelicEntry), "RestockAfterPurchase").Invoke(shop.RelicEntries[2], [shop]);
            Assert(state.Random.Position(RandomAddress.For("relic rarity", "Courier")) == 1 &&
                state.Random.Position(RandomAddress.For("relic rarity", "shop")) == 2 &&
                state.Random.Position(RandomAddress.For("relic rarity", "generic")) == 0 &&
                shop.RelicEntries[2].Model!.Rarity != RelicRarity.Shop &&
                state.Random.Position(RandomAddress.For("shop")) == 1,
                "Courier restocks the Shop slot with ordinary relic rarity without advancing initial-shop or generic sequences");
            Assert(vanilla.Equals(player.PlayerRng.Shops.ToSerializable()), "shop generation, modifiers, purchases and restocks preserve vanilla shop RNG");
            var actual = string.Join("|", shop.CardEntries.Select(e => RewardEntries.Find(e.CreationResult!.Card) + ":" + e.Cost)) +
                RewardEntries.Find(potionEntry.Model!) + ":" + potionEntry.Cost +
                JsonSerializer.Serialize(state.Rewards.Supplies.OrderBy(p => p.Key, StringComparer.Ordinal));
            if (expected == null) expected = actual;
            else Assert(actual == expected, "interleaving Courier card and potion purchases preserves identities, addresses, prices and remaining supplies");
        }

        foreach (var gold in new[] { 199, 200 })
        {
            RunManager.Instance.CleanUp();
            var run = StartGoldRun();
            var player = run.Players[0];
            player.Gold = gold;
            RelicRewards.Initialize(player);
            var state = RunCheckpoints.Require(run);
            var before = JsonSerializer.Serialize(state.Rewards);
            var pool = RelicRewards.Pool(player).ToDictionary(r => r.Id.Entry, StringComparer.Ordinal);
            bool Eligible(SupplyEntry entry) => pool[entry.Identity].IsAllowedInShops && pool[entry.Identity].IsAllowed(run);
            var rareSupply = state.Rewards.Supply(RelicRewards.Address("shop", RelicRarity.Rare));
            var expectedFeatured = rareSupply.Positions.Select(p => p.Entry!).First(Eligible);
            var source = ModelDb.Event<WelcomeToWongos>().ToMutable();
            run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, source.Id);
            await source.BeginEvent(player, null, false);
            var featured = (RelicModel)AccessTools.Property(typeof(WelcomeToWongos), "FeaturedItem").GetValue(source)!;
            if (gold == 199)
                Assert(JsonSerializer.Serialize(state.Rewards) == before, "unaffordable Wongos featured stock consumes no relic entry or marker");
            else
                Assert(featured.Rarity == RelicRarity.Rare && featured.IsAllowedInShops &&
                    RewardEntries.Find(featured) == expectedFeatured && rareSupply.Positions.All(p => p.Entry != null) &&
                    state.Rewards.RetiredRelics.Contains(featured.Id.Entry),
                    "affordable Wongos featured Rare removes the first eligible shop entry without a marker when offered");
            var commonSupply = state.Rewards.Supply(RelicRewards.Address("generic", RelicRarity.Common));
            var common = commonSupply.Positions.Select(p => p.Entry!).First(Eligible);
            await (Task)AccessTools.Method(typeof(WelcomeToWongos), "BuyBargainBin").Invoke(source, [])!;
            Assert(player.Gold == gold - 100 && player.Relics.Any(r => r.Id.Entry == common.Identity) &&
                commonSupply.Positions.All(p => p.Entry != null && p.Entry.Identity != common.Identity) &&
                state.Random.Position(RandomAddress.For("relic rarity", "generic")) == 0 &&
                state.Random.Position(RandomAddress.For("relic rarity", "shop")) == 0,
                "Wongos Common purchase removes the first eligible generic entry without a marker or rarity roll");
        }
        RunManager.Instance.CleanUp();
    }
}
