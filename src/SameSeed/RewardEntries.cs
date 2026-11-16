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
        if (model is CardModel card)
            CardOccurrences.Create(card, entry.Address);
        else
            entries.AddOrUpdate(model, entry);
    }

    internal static SupplyEntry? Find(AbstractModel model) => model is CardModel card
        ? CardOccurrences.Find(card) is { } occurrence ? new(card.Id.Entry, occurrence.Address) : null
        : entries.TryGetValue(model, out var entry) ? entry : null;

    private static void Postfix(AbstractModel __instance, AbstractModel __result)
    {
        if (__instance is CardModel original && __result is CardModel copy)
            CardOccurrences.Recreate(original, copy);
        else if (Find(__instance) is { } entry)
            Attach(__result, entry);
    }
}
