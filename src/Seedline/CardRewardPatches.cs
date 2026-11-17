using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class CardRewardPatches
{
    [HarmonyPatch]
    private static class Offers
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [
            AccessTools.Method(typeof(CardReward), nameof(CardReward.Populate)),
            RewardPatches.AsyncMethod(typeof(BrainLeech), "ShareKnowledge"),
            RewardPatches.AsyncMethod(typeof(RoomFullOfCheese), "Gorge"),
            RewardPatches.AsyncMethod(typeof(HeftyTablet), nameof(HeftyTablet.AfterObtained)),
            RewardPatches.AsyncMethod(typeof(LeadPaperweight), nameof(LeadPaperweight.AfterObtained)),
            RewardPatches.AsyncMethod(typeof(Kaleidoscope), nameof(Kaleidoscope.AfterObtained)),
            RewardPatches.AsyncMethod(typeof(SeaGlass), nameof(SeaGlass.AfterObtained))
        ];

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
            RewardPatches.Replace(instructions, IsRewardFactory, AccessTools.Method(typeof(Offers), nameof(Create)),
                __originalMethod.DeclaringType?.DeclaringType == typeof(SeaGlass) ? 3 : 1);

        private static IEnumerable<CardCreationResult> Create(Player player, int cardCount, CardCreationOptions options)
        {
            var pools = options.CardPools.ToArray();
            var kind = pools.Length == 1 && pools[0] == player.Character.CardPool ? CardOfferKind.OwnCharacter
                : pools.Length == 1 && pools[0] is not ColorlessCardPool ? CardOfferKind.OtherCharacter : CardOfferKind.Colorless;
            return CardRewards.Generate(player, cardCount, options, kind);
        }
    }

    [HarmonyPatch(typeof(LastingCandy), nameof(LastingCandy.TryModifyCardRewardOptions))]
    private static class Candy
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, IsRewardFactory, AccessTools.Method(typeof(Candy), nameof(Create)));

        private static IEnumerable<CardCreationResult> Create(Player player, int cardCount, CardCreationOptions options) =>
            CardRewards.Generate(player, cardCount, options, CardOfferKind.LastingCandy);

        private static void Postfix(List<CardCreationResult> rewardOptions, bool __result)
        {
            if (!__result || rewardOptions.Count < 2 || CardOffers.Find(rewardOptions[0].Card)?.Offer is not { } offer)
                return;
            var card = rewardOptions[^1].Card;
            CardOffers.Attach(card, offer, rewardOptions.Count - 1);
            CardOffers.Info(card).Candy = true;
        }
    }

    private static bool IsRewardFactory(MethodInfo method) => method.DeclaringType == typeof(CardFactory) && method.Name == nameof(CardFactory.CreateForReward);
}
