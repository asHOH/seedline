using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

[HarmonyPatch(typeof(MerchantInventory), nameof(MerchantInventory.CreateForNormalMerchant))]
internal static class ShopRewards
{
    private sealed class Shop(CheckpointState state, long ordinal)
    {
        internal CheckpointState State { get; } = state;
        internal long Ordinal { get; } = ordinal;
        internal int Slot { get; set; }
    }
    private static Shop? current;
    private static Rng? fakePrices;
    internal static bool IsInitial => current != null;

    private static void Prefix(Player player, out Shop? __state)
    {
        __state = current;
        var state = RunCheckpoints.Require((RunState)player.RunState);
        current = new(state, state.Random.Advance(RandomAddress.For("shop")));
    }
    private static void Finalizer(Shop? __state) => current = __state;

    [HarmonyPatch(typeof(MerchantInventory), nameof(MerchantInventory.Setup))]
    private static class Sale
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Rng) && m.Name == "NextInt",
                AccessTools.Method(typeof(Sale), nameof(Choose)));
        private static int Choose(Rng rng, int count) => current == null ? rng.NextInt(count) : AddressRandom.IntInclusive(current.State.Seed,
            RandomAddress.For("sale slot").At(current.Ordinal), 0, count - 1);
    }

    [HarmonyPatch]
    private static class Cards
    {
        private static IEnumerable<MethodBase> TargetMethods() => typeof(CardFactory).GetMethods().Where(m => m.Name == "CreateForMerchant");
        private static bool Prefix(Player player, IEnumerable<CardModel> options, IEnumerable<CardModel> blacklist, object[] __args, ref CardCreationResult __result)
        {
            var state = RunCheckpoints.Require((RunState)player.RunState);
            var type = __args.OfType<CardType>().Cast<CardType?>().FirstOrDefault();
            var requested = __args.OfType<CardRarity>().Cast<CardRarity?>().FirstOrDefault();
            var eligible = Hook.ModifyMerchantCardPool(player.RunState, player, options)
                .Where(c => c.Rarity != CardRarity.Basic && c.IsAllowedInRun(player.RunState)).Except(blacklist).ToArray();
            int? slot = current == null ? null : current.Slot++;
            var initial = slot.HasValue ? RandomAddress.For("shop card").At(current!.Ordinal).At(slot.Value) : null;
            var roll = initial != null ? AddressRandom.Unit(AddressRandom.UInt64(state.Seed, RandomAddress.For("shop card rarity").With(initial.Key)))
                : requested == null ? state.Random.NextUnit(RandomAddress.For("Courier card rarity", type.ToString()!)) : 0;
            var rareChance = player.RunState.AscensionLevel >= 7 ? 0.045 : 0.108;
            var rarity = Hook.ModifyMerchantCardRarity(player.RunState, player,
                requested ?? (roll < rareChance ? CardRarity.Rare : roll < rareChance + 0.37 ? CardRarity.Uncommon : CardRarity.Common));
            if (type != null)
            {
                for (var attempts = 0; attempts < 3 && !eligible.Any(c => c.Type == type && c.Rarity == rarity); attempts++)
                    rarity = rarity == CardRarity.Common ? CardRarity.Uncommon : rarity == CardRarity.Uncommon ? CardRarity.Rare : CardRarity.Common;
            }
            eligible = eligible.Where(c => c.Rarity == rarity && (type == null || c.Type == type)).OrderBy(c => c.Id.Entry, StringComparer.Ordinal).ToArray();
            if (eligible.Length == 0)
                throw new InvalidOperationException("No eligible shop card.");
            CardModel card;
            if (initial != null)
            {
                var identity = eligible[AddressRandom.IntInclusive(state.Seed, RandomAddress.For("shop card identity").With(initial.Key), 0, eligible.Length - 1)];
                card = CardGeneration.Create(player, identity, initial.Key);
            }
            else
            {
                var basePool = type == null ? ModelDb.CardPool<ColorlessCardPool>() : player.Character.CardPool;
                var full = Hook.ModifyMerchantCardPool(player.RunState, player,
                    basePool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
                    .Where(c => c.Rarity == rarity && (type == null || c.Type == type)).ToArray();
                card = CardGeneration.Draw(player, RandomAddress.For("Courier card identity", type?.ToString() ?? "colorless", rarity.ToString()),
                    full, eligible);
            }
            CardGeneration.Upgrade(player, card, -999999999m);
            __result = new(card);
            return false;
        }
    }

    [HarmonyPatch]
    private static class Prices
    {
        private static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(MerchantCardEntry), typeof(MerchantRelicEntry), typeof(MerchantPotionEntry) }
            .Select(t => AccessTools.Method(t, "CalcCost"));
        private static bool Prefix(MerchantEntry __instance, Player ____player, ref int ____cost)
        {
            AbstractModel? model = __instance switch
            {
                MerchantCardEntry c => c.CreationResult?.Card,
                MerchantRelicEntry r => r.Model,
                MerchantPotionEntry p => p.Model,
                _ => null
            };
            if (model == null)
                return true;
            var entry = RewardEntries.Find(model);
            if (entry == null && fakePrices == null)
                return true;
            var state = RunCheckpoints.Require((RunState)____player.RunState);
            var unit = entry != null ? AddressRandom.Unit(AddressRandom.UInt64(state.Seed, RandomAddress.For("base price").With(entry.Address)))
                : fakePrices!.NextFloat();
            if (model is RelicModel relic)
                ____cost = (int)Math.Round(relic.MerchantCost * (float)(0.85 + 0.30 * unit));
            else if (model is PotionModel potion)
                ____cost = Mathf.RoundToInt((potion.Rarity == PotionRarity.Rare ? 100 : potion.Rarity == PotionRarity.Uncommon ? 75 : 50) * (float)(0.95 + 0.10 * unit));
            else if (model is CardModel card)
            {
                var cost = card.Rarity == CardRarity.Rare ? 150 : card.Rarity == CardRarity.Uncommon ? 75 : 50;
                if (card.Pool is ColorlessCardPool)
                    cost = Mathf.RoundToInt(cost * 1.15f);
                ____cost = Mathf.RoundToInt(cost * (float)(0.95 + 0.10 * unit));
                if (((MerchantCardEntry)__instance).IsOnSale)
                    ____cost /= 2;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(FakeMerchant), "BeforeEventStarted")]
    private static class FakePrices
    {
        private static void Prefix(FakeMerchant __instance, out Rng? __state)
        {
            __state = fakePrices;
            fakePrices = __instance.Rng;
        }
        private static void Finalizer(Rng? __state) => fakePrices = __state;
    }
}
