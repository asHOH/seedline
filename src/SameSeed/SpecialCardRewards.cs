using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace SameSeed;

internal static class SpecialCardRewards
{
    [HarmonyPatch(typeof(WingCharm), nameof(WingCharm.TryModifyCardRewardOptionsLate))]
    private static class Wing
    {
        private static bool Prefix(WingCharm __instance, Player player, List<CardCreationResult> cardRewards, ref bool __result)
        {
            if (player != __instance.Owner || cardRewards.Any(c => CardOccurrences.Find(c.Card) == null))
                return true;
            var swift = ModelDb.Enchantment<Swift>();
            var eligible = cardRewards.Where(c => swift.CanEnchant(c.Card)).ToArray();
            if (eligible.Length == 0)
            {
                __result = false;
                return false;
            }
            var state = RunCheckpoints.Require((RunState)player.RunState);
            var candidates = eligible;
            var candy = eligible.Where(c => CardOffers.Find(c.Card)?.Candy == true).ToArray();
            if (eligible.Length > 1 && candy.Length > 0)
                candidates = CardRewards.Unit(state, "Wing Charm", CardOccurrences.Info(candy[0].Card).Address) < 1d / eligible.Length
                    ? candy : eligible.Except(candy).ToArray();
            var supplementary = candidates.Where(c => CardOffers.Find(c.Card)?.Supplementary == true).ToArray();
            if (candidates.Length > 1 && supplementary.Length > 0)
            {
                var triple = candidates.Except(supplementary).ToArray();
                var rare = cardRewards.Any(c => CardOffers.Find(c.Card)?.Candy != true && c.Card.Rarity == CardRarity.Rare);
                if (triple.Length == 0 || (rare && CardRewards.EventRng(player).NextFloat() < (double)supplementary.Length / candidates.Length))
                {
                    var index = rare ? AddressRandom.IntInclusive(state.Seed, RandomAddress.For("Wing Charm supplementary selection", "BRAIN_LEECH"), 0, supplementary.Length - 1)
                        : CardRewards.EventRng(player).NextInt(supplementary.Length);
                    candidates = [supplementary[index]];
                }
                else
                    candidates = triple;
            }
            var chosen = candidates.OrderBy(c => CardRewards.Unit(state, "Wing Charm", CardOffers.Find(c.Card)?.WingAddress ?? CardOccurrences.Info(c.Card).Address))
                .ThenBy(c => CardOccurrences.Info(c.Card).Address, StringComparer.Ordinal).First();
            var card = player.RunState.CloneCard(chosen.Card);
            CardCmd.Enchant<Swift>(card, __instance.DynamicVars["SwiftAmount"].BaseValue);
            chosen.ModifyCard(card, __instance);
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ScrollBoxes), nameof(ScrollBoxes.AfterObtained))]
    private static class Scrolls
    {
        private static bool Prefix(ScrollBoxes __instance, ref Task __result)
        {
            __result = Obtain(__instance.Owner);
            return false;
        }
        private static async Task Obtain(Player player)
        {
            var bundles = GenerateBundles(player);
            foreach (var card in await CardSelectCmd.FromChooseABundleScreen(player, bundles))
                await CardPileCmd.Add(card, PileType.Deck);
        }
    }

    internal static List<IReadOnlyList<CardModel>> GenerateBundles(Player player)
    {
        var rng = CardRewards.EventRng(player);
        var claws = new[] { player.Character is Defect && rng.NextInt(100) < 1, player.Character is Defect && rng.NextInt(100) < 1 };
        var state = RunCheckpoints.Require((RunState)player.RunState);
        var address = RandomAddress.For("card offer", "SCROLL_BOXES");
        var opportunity = address.At(state.Random.Advance(address));
        var commons = claws.Any(c => c) ? [] : CardFactory.CreateForReward(player, 3,
            CardCreationOptions.ForNonCombatWithUniformOdds([player.Character.CardPool], c => c.Rarity == CardRarity.Common)
                .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoModifyHooks)).Select(c => c.Card).ToList();
        var offer = commons.Count == 0 ? opportunity.Key : CardOffers.Info(commons[0]).Offer!;
        var ordinary = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(c => c.IsAllowedInRun(player.RunState)).ToArray();
        var used = commons.Select(c => c.Id).ToHashSet();
        var commonIndex = 0;
        var bundles = new List<IReadOnlyList<CardModel>>();
        for (var bundle = 0; bundle < 2; bundle++)
        {
            var cards = new List<CardModel>();
            for (var slot = 0; slot < 3; slot++)
            {
                CardModel card;
                if (!claws[bundle] && slot < 2 && commonIndex < commons.Count)
                    card = commons[commonIndex++];
                else
                {
                    var rarity = slot < 2 ? CardRarity.Common : CardRarity.Uncommon;
                    var eligible = ordinary.Where(c => c.Rarity == rarity && !used.Contains(c.Id)).ToArray();
                    var canonical = claws[bundle] ? ModelDb.Card<Claw>() : eligible[rng.NextInt(eligible.Length)];
                    card = player.RunState.CreateCard(canonical, player);
                    RewardEntries.Attach(card, new(canonical.Id.Entry, opportunity.At(bundle * 3 + slot).Key));
                    if (!claws[bundle])
                        used.Add(canonical.Id);
                }
                CardOffers.Attach(card, offer, bundle * 3 + slot);
                cards.Add(card);
            }
            bundles.Add(cards);
        }
        CardOffers.NumberOffer(bundles.SelectMany(b => b));
        return bundles;
    }

    [HarmonyPatch]
    private static class KaleidoscopeRng
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(Kaleidoscope), "AfterObtained");
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.Name == "get_NonCombatGeneration", AccessTools.Method(typeof(KaleidoscopeRng), nameof(Event)));
        private static Rng Event(PlayerRngSet _) => CardRewards.EventRng(RunManager.Instance.DebugOnlyGetState()!.Players[0]);
    }
}
