using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace SameSeed;

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
internal static class RewardEntries
{
    private static readonly ConditionalWeakTable<AbstractModel, SupplyEntry> entries = new();

    internal static void Attach(AbstractModel model, SupplyEntry entry)
    {
        entries.Remove(model);
        entries.Add(model, entry);
        if (model is CardModel card)
            CardOccurrences.Generated(card, entry);
    }

    internal static SupplyEntry? Find(AbstractModel model) => entries.TryGetValue(model, out var entry) ? entry : null;

    private static void Postfix(AbstractModel __instance, AbstractModel __result)
    {
        if (Find(__instance) is { } entry)
            Attach(__result, entry);
        if (__instance is CardModel original && __result is CardModel copy)
            CardOccurrences.Clone(original, copy);
    }
}
