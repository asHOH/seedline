using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace SameSeed;

internal sealed class CardHistory
{
    // Frozen JSON keeps removed-card properties independent of later physical-object mutations.
    [JsonRequired] public SortedDictionary<string, string> Removed { get; set; } = new(StringComparer.Ordinal);
    internal CardHistory Copy() => new() { Removed = new(Removed, StringComparer.Ordinal) };
    internal void Write(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("card_history");
        JsonSerializer.Serialize(writer, this);
    }

    internal static CardHistory Read(JsonElement element)
    {
        try
        {
            var history = element.Deserialize<CardHistory>() ?? throw new InvalidDataException("Missing card history.");
            if (history.Removed == null)
                throw new InvalidDataException("Missing removed cards.");
            foreach (var (address, json) in history.Removed)
            {
                var card = JsonSerializer.Deserialize(json, JsonSerializationUtility.GetTypeInfo<SerializableCard>()) ?? throw new InvalidDataException("Missing removed card.");
                var properties = card.Props?.strings?.Where(p => p.name == CardOccurrences.SavedName).ToArray();
                if (card.Id == null || card.CurrentUpgradeLevel < 0 || properties?.Length != 1 ||
                    CardOccurrences.Read(properties[0].value) is not { Acquisition: not null } info || info.Address != address)
                    throw new InvalidDataException("Invalid removed card occurrence.");
            }
            return history;
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Invalid SameSeed card history. Save preserved.", error);
        }
    }

    private sealed class AcquisitionBatch { internal long? Ordinal; }
    private static readonly AsyncLocal<AcquisitionBatch?> batch = new();

    [HarmonyPatch]
    private static class Batches
    {
        private static IEnumerable<MethodBase> TargetMethods() => CardOccurrences.CopySources.TargetMethods().Concat(
        [AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.Add),
            [typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool), typeof(bool)]),
            AccessTools.Method(typeof(CardCmd), nameof(CardCmd.Transform),
                [typeof(IEnumerable<CardTransformation>), typeof(Rng), typeof(CardPreviewStyle)]),
            AccessTools.Method(typeof(Astrolabe), nameof(Astrolabe.AfterObtained)),
            AccessTools.Method(typeof(ScrollBoxes), nameof(ScrollBoxes.AfterObtained)),
            AccessTools.Method(typeof(MorphicGrove), "Group"), AccessTools.Method(typeof(Trial), "NondescriptInnocent"),
            AccessTools.Method(typeof(SeaGlass), nameof(SeaGlass.AfterObtained))]);
        private static void Prefix(MethodBase __originalMethod, out AcquisitionBatch? __state)
        {
            __state = batch.Value;
            if (__state == null || (__originalMethod.DeclaringType != typeof(CardPileCmd) && __originalMethod.DeclaringType != typeof(CardCmd)))
                batch.Value = new();
        }
        private static void Finalizer(AcquisitionBatch? __state) => batch.Value = __state;
    }

    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    private static class Acquired
    {
        private static void Postfix(CardPile __instance, CardModel card)
        {
            if (__instance.Type != PileType.Deck || CardOccurrences.Find(card) is not { } info ||
                card.Owner?.RunState is not RunState run || RunCheckpoints.Find(run) is not { } state)
                return;
            if (info.Acquisition == null)
            {
                var current = batch.Value ?? new();
                current.Ordinal ??= state.Random.Advance(RandomAddress.For("card acquisition"));
                CardOccurrences.Acquire(card, current.Ordinal.Value);
            }
            state.Cards.Removed.Remove(info.Address);
        }
    }

    [HarmonyPatch(typeof(CardPile), nameof(CardPile.RemoveInternal))]
    private static class RemovedCard
    {
        private static void Prefix(CardPile __instance, CardModel card)
        {
            if (__instance.Type != PileType.Deck || !__instance.Cards.Contains(card) ||
                CardOccurrences.Find(card) is not { Acquisition: not null } info ||
                card.Owner?.RunState is not RunState run || RunCheckpoints.Find(run) is not { } state)
                return;
            var stored = card.Owner.Relics.OfType<PaelsTooth>().SelectMany(r => r.SerializableCards);
            if (stored.Any(c => c.Props?.strings?.Any(p => p.name == CardOccurrences.SavedName &&
                CardOccurrences.Read(p.value).Address == info.Address) == true))
                return;
            state.Cards.Removed[info.Address] = JsonSerializer.Serialize(card.ToSerializable(), JsonSerializationUtility.GetTypeInfo<SerializableCard>());
        }
    }
}
