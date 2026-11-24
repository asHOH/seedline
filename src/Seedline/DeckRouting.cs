using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace Seedline;

internal static class DeckRouting
{
    private static async Task<IEnumerable<CardModel>> Complete(Task<IEnumerable<CardModel>> task, CardModel[] eligible, string operation)
    {
        var clicked = (await task).ToArray();
        PermanentSelection.Route(eligible, clicked, operation);
        return clicked;
    }

    [HarmonyPatch]
    private static class DeckChoices
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForUpgrade)),
            AccessTools.Method(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForTransformation)),
            AccessTools.Method(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForRemoval))];

        private static void Prefix(MethodBase __originalMethod, Player player, object[] __args, out CardModel[] __state)
        {
            if (RunScope.Bypasses(player.RunState))
            {
                __state = [];
                return;
            }
            var filter = __args.OfType<Func<CardModel, bool>>().FirstOrDefault();
            __state = player.Deck.Cards.Where(c => __originalMethod.Name switch
            {
                nameof(CardSelectCmd.FromDeckForUpgrade) => c.IsUpgradable,
                nameof(CardSelectCmd.FromDeckForTransformation) => c.Type != CardType.Quest && c.IsTransformable,
                _ => c.IsRemovable && (filter?.Invoke(c) ?? true)
            }).ToArray();
        }

        private static void Postfix(MethodBase __originalMethod, CardModel[] __state, ref Task<IEnumerable<CardModel>> __result) =>
            __result = Complete(__result, __state, __originalMethod.Name == nameof(CardSelectCmd.FromDeckForUpgrade) ? "upgrade" : "removal");
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForEnchantment),
        [typeof(IReadOnlyList<CardModel>), typeof(EnchantmentModel), typeof(int), typeof(CardSelectorPrefs)])]
    private static class EnchantChoice
    {
        private static void Prefix(IReadOnlyList<CardModel> cards, out CardModel[] __state) =>
            __state = cards.Count == 0 ? [] : cards[0].Owner.Deck.Cards.Where(cards.Contains).ToArray();
        private static void Postfix(CardModel[] __state, ref Task<IEnumerable<CardModel>> __result) =>
            __result = Complete(__result, __state, "enchant");
    }

    [HarmonyPatch]
    private static class SpecialChoices
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [RewardPatches.AsyncMethod(typeof(PaelsTooth), nameof(PaelsTooth.AfterObtained)),
            RewardPatches.AsyncMethod(typeof(WoodCarvings), "Torus"), RewardPatches.AsyncMethod(typeof(WoodCarvings), "Bird")];

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var tooth = __originalMethod.DeclaringType?.DeclaringType == typeof(PaelsTooth);
            return RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(CardSelectCmd) &&
                m.Name == (tooth ? nameof(CardSelectCmd.FromDeckForRemoval) : nameof(CardSelectCmd.FromDeckGeneric)),
                AccessTools.Method(typeof(SpecialChoices), tooth ? nameof(Store) : nameof(Transform)));
        }

        private static Task<IEnumerable<CardModel>> Store(Player player, CardSelectorPrefs prefs, Func<CardModel, bool>? filter)
        {
            if (RunScope.Bypasses(player.RunState)) return CardSelectCmd.FromDeckForRemoval(player, prefs, filter);
            bool Eligible(CardModel c) => c.IsRemovable && (filter?.Invoke(c) ?? true);
            var deck = player.Deck.Cards.ToArray();
            return Complete(CardSelectCmd.FromDeckGeneric(player, prefs, Eligible,
                c => c.Type == CardType.Curse ? -999999999 : Array.IndexOf(deck, c)), deck.Where(Eligible).ToArray(), "upgrade");
        }

        private static Task<IEnumerable<CardModel>> Transform(Player player, CardSelectorPrefs prefs,
            Func<CardModel, bool>? filter, Func<CardModel, int>? sortingOrder)
        {
            if (RunScope.Bypasses(player.RunState)) return CardSelectCmd.FromDeckGeneric(player, prefs, filter, sortingOrder);
            var eligible = player.Deck.Cards.Where(c => filter?.Invoke(c) ?? true).ToArray();
            if (sortingOrder != null)
                eligible = eligible.OrderBy(sortingOrder).ToArray();
            return Complete(CardSelectCmd.FromDeckGeneric(player, prefs, filter, sortingOrder), eligible, "removal");
        }
    }
}
