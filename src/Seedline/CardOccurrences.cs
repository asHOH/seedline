using System.IO.Hashing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Seedline;

internal sealed record CardOccurrence(
    [property: JsonRequired] string Address,
    [property: JsonRequired] string Key,
    [property: JsonRequired] bool SourceKey = false,
    [property: JsonRequired] long? Acquisition = null);

internal static class CardOccurrences
{
    private sealed record CopyGrant(RandomAddress Address, RandomState Random, Dictionary<CardModel, int>? Indices)
    {
        internal long? Ordinal { get; set; }
    }
    private static readonly AsyncLocal<CopyGrant?> copyGrant = new();
    internal const string SavedName = "SeedlineOccurrence";

    [HarmonyPatch(typeof(ModelIdSerializationCache), "CachePropertiesForType")]
    private static class RegisterSavedName
    {
        private static void Postfix(XxHash32? hasher, Dictionary<string, int> ____propertyNameToNetIdMap,
            List<string> ____netIdToPropertyNameMap)
        {
            if (hasher == null || ____propertyNameToNetIdMap.ContainsKey(SavedName))
                return;
            // Register during initialization so the game's final bit width and hash include our property.
            ____propertyNameToNetIdMap.Add(SavedName, ____netIdToPropertyNameMap.Count);
            ____netIdToPropertyNameMap.Add(SavedName);
            hasher.Append(Encoding.UTF8.GetBytes(SavedName));
        }
    }

    private static readonly ConditionalWeakTable<CardModel, CardOccurrence> cards = new();
    internal static CardOccurrence Info(CardModel card) => cards.TryGetValue(card, out var info)
        ? info : throw new InvalidOperationException($"Missing card occurrence: {card.Id}.");
    internal static CardOccurrence? Find(CardModel card) => cards.TryGetValue(card, out var info) ? info : null;
    private static void Attach(CardModel card, CardOccurrence info) => cards.AddOrUpdate(card, info);
    private static void Forget(CardModel card)
    {
        cards.Remove(card);
        CardOffers.Forget(card);
    }
    internal static void Create(CardModel card, string address, bool sourceKey = false)
    {
        CardOffers.Forget(card);
        Attach(card, new(address, address, sourceKey));
    }
    internal static void AssignKey(CardModel card, string key) => Attach(card, Info(card) with { Key = key, SourceKey = true });
    internal static void Acquire(CardModel card, long batch) => Attach(card, Info(card) with { Acquisition = batch });
    internal static void Recreate(CardModel original, CardModel copy)
    {
        if (Find(original) is { } info)
            Attach(copy, info);
        CardOffers.Recreate(original, copy);
    }

    internal static void Reassociate(IReadOnlyList<CardModel> copies, IReadOnlyList<CardModel> logicalOrder)
    {
        var occurrences = logicalOrder.Select(Info).ToArray();
        var offers = logicalOrder.Select(CardOffers.Find).ToArray();
        for (var i = 0; i < copies.Count; i++)
        {
            Attach(copies[i], occurrences[i]);
            CardOffers.Rebind(copies[i], offers[i]);
        }
    }
    internal static void Initialize(RunState run)
    {
        var batch = RunCheckpoints.Require(run).Random.Advance(RandomAddress.For("card acquisition"));
        foreach (var player in run.Players)
        {
            var index = 0;
            foreach (var card in player.Deck.Cards.OrderBy(c => c, CardOrder.Permanent))
            {
                var address = RandomAddress.For("card occurrence", "starting deck").At(index++).Key;
                Create(card, address, true);
                Acquire(card, batch);
            }
        }
    }

    private static void Prepare(IEnumerable<CardModel> added, CardPile pile, AbstractModel? source)
    {
        if (pile.Type != PileType.Deck)
            return;
        var batch = added.ToArray();
        CardOffers.PrepareAcquisition(batch);
        var fresh = batch.Where(c => Find(c) == null).OrderBy(c => c, CardOrder.Permanent).ToArray();
        if (fresh.Length == 0 || fresh[0].Owner.RunState is not RunState run || RunCheckpoints.Find(run) is not { } state)
            return;
        var address = RandomAddress.For("card occurrence", source?.Id.Entry ?? run.CurrentRoom?.ModelId?.Entry ?? "initial grants");
        var grant = state.Random.Advance(address);
        for (var i = 0; i < fresh.Length; i++)
        {
            var occurrence = address.At(grant).At(i).Key;
            Create(fresh[i], occurrence);
        }
    }

    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add),
        [typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool), typeof(bool)])]
    private static class DeckAdd
    {
        private static void Prefix(IEnumerable<CardModel> cards, CardPile newPile, AbstractModel? clonedBy) => Prepare(cards, newPile, clonedBy);
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyCardBeingAddedToDeck))]
    private static class DeckModifiers
    {
        // Transformations bypass CardPileCmd.Add; initialize before modifiers can clone the replacement.
        private static void Prefix(CardModel card) => Prepare([card], card.Owner.Deck, null);
    }

    [HarmonyPatch(typeof(RunState), nameof(RunState.CloneCard))]
    private static class PermanentCopy
    {
        private static void Postfix(CardModel mutableCard, CardModel __result)
        {
            // Reward modifiers recreate a generated card; copying a permanent card starts a new occurrence.
            if (mutableCard.Pile?.Type == PileType.Deck)
            {
                Forget(__result);
                if (copyGrant.Value is { } grant)
                {
                    grant.Ordinal ??= grant.Random.Advance(grant.Address);
                    var address = grant.Address.At(grant.Ordinal.Value).At(grant.Indices?.GetValueOrDefault(mutableCard) ?? 0).Key;
                    Create(__result, address);
                }
            }
        }
    }

    [HarmonyPatch]
    internal static class CopySources
    {
        internal static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(DollysMirror), "AfterObtained"), AccessTools.Method(typeof(BingBong), "AfterCardChangedPiles"),
            AccessTools.Method(typeof(Reflections), "Shatter"), AccessTools.Method(typeof(CloneRestSiteOption), "OnSelect")];
        private static void Prefix(object __instance, out CopyGrant? __state)
        {
            __state = copyGrant.Value;
            var player = (Player)AccessTools.Property(__instance.GetType(), "Owner").GetValue(__instance)!;
            var state = RunCheckpoints.Require((RunState)player.RunState);
            var source = __instance is AbstractModel model ? model.Id.Entry : "CLONE_REST_SITE";
            var address = RandomAddress.For("card copy", source);
            Dictionary<CardModel, int>? indices = null;
            if (__instance is Reflections or CloneRestSiteOption)
                indices = player.Deck.Cards.Where(c => __instance is Reflections || c.Enchantment is Clone)
                    .OrderBy(c => c, CardOrder.Permanent).Select((card, index) => (card, index)).ToDictionary(p => p.card, p => p.index);
            copyGrant.Value = new(address, state.Random, indices);
        }
        private static void Finalizer(CopyGrant? __state) => copyGrant.Value = __state;
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.ToSerializable))]
    private static class Save
    {
        private static void Postfix(CardModel __instance, SerializableCard __result)
        {
            if (Find(__instance) is not { } info)
                return;
            __result.Props ??= new();
            __result.Props.strings ??= [];
            __result.Props.strings.Add(new(SavedName, JsonSerializer.Serialize(info)));
        }
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.FromSerializable))]
    private static class Load
    {
        private static void Postfix(SerializableCard save, CardModel __result)
        {
            var properties = save.Props?.strings?.Where(p => p.name == SavedName).ToArray();
            if (properties == null || properties.Length == 0)
                return;
            if (properties.Length != 1)
                throw new InvalidDataException("Duplicate card occurrence metadata.");
            Attach(__result, Read(properties[0].value));
        }
    }

    internal static CardOccurrence Read(string json)
    {
        try
        {
            var info = JsonSerializer.Deserialize<CardOccurrence>(json) ?? throw new InvalidDataException("Missing card occurrence.");
            RandomAddress.Parse(info.Address);
            RandomAddress.Parse(info.Key);
            if (info.Acquisition < 0)
                throw new InvalidDataException("Invalid card acquisition batch.");
            return info;
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Invalid card occurrence. Save preserved.", error);
        }
    }

    internal static void Validate(SerializableRun save)
    {
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var player in save.Players)
        {
            var stored = player.Relics.Where(r => r.Id?.Entry == "PAELS_TOOTH")
                .SelectMany(r => r.Props?.cardArrays?.SelectMany(p => p.value) ?? []);
            foreach (var card in player.Deck.Concat(stored))
            {
                var properties = card.Props?.strings?.Where(p => p.name == SavedName).ToArray();
                if (properties == null || properties.Length != 1 || !addresses.Add(Read(properties[0].value).Address))
                    throw new InvalidDataException("Missing or duplicate permanent card occurrence. Save preserved.");
            }
        }
    }

    [HarmonyPatch]
    private static class StoredReturn
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(PaelsTooth), "AfterCombatEnd");
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(List<SerializableCard>) && m.Name == "Remove",
                AccessTools.Method(typeof(StoredReturn), nameof(Remove)));
        private static bool Remove(List<SerializableCard> stored, SerializableCard returned)
        {
            // SerializableCard.Equals ignores the occurrence metadata of otherwise identical copies.
            var index = stored.FindIndex(card => ReferenceEquals(card, returned));
            if (index < 0)
                return false;
            stored.RemoveAt(index);
            return true;
        }
    }
}
