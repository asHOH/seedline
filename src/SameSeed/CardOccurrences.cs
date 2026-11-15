using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SameSeed;

internal sealed class CardOccurrence(string address, string key)
{
    [JsonRequired] public string Address { get; set; } = address;
    [JsonRequired] public string Key { get; set; } = key;
    [JsonIgnore] internal string? Offer { get; set; }
    [JsonIgnore] internal int EntryOrder { get; set; }
    [JsonIgnore] internal int CopyNumber { get; set; }
    [JsonIgnore] internal bool Supplementary { get; set; }
    [JsonIgnore] internal bool Candy { get; set; }
    [JsonIgnore] internal int RareOrder { get; set; } = -1;
    [JsonIgnore] internal string WingAddress { get; set; } = address;
    [JsonIgnore] internal bool KeyAssigned { get; set; }
    internal CardOccurrence Copy() => (CardOccurrence)MemberwiseClone();
}

internal static class CardOccurrences
{
    private sealed record CopyGrant(RandomAddress Address, RandomState Random, Dictionary<CardModel, int>? Indices)
    {
        internal long? Ordinal { get; set; }
    }
    private static readonly AsyncLocal<CopyGrant?> copyGrant = new();
    internal const string SavedName = "SameSeedOccurrence";
    private static readonly ConditionalWeakTable<CardModel, CardOccurrence> cards = new();
    internal static CardOccurrence Info(CardModel card) => cards.TryGetValue(card, out var info)
        ? info : throw new InvalidOperationException($"Missing card occurrence: {card.Id}.");
    internal static CardOccurrence? Find(CardModel card) => cards.TryGetValue(card, out var info) ? info : null;
    private static void Attach(CardModel card, CardOccurrence info)
    {
        cards.Remove(card);
        cards.Add(card, info);
    }
    internal static void Generated(CardModel card, SupplyEntry entry) => Attach(card, new(entry.Address, entry.Address));
    internal static void Clone(CardModel original, CardModel copy)
    {
        if (Find(original) is { } info)
            Attach(copy, info.Copy());
    }
    internal static void Offer(CardModel card, string offer, int order, bool supplementary = false, string? wing = null)
    {
        var info = Info(card);
        info.Offer = offer;
        info.EntryOrder = order;
        info.Supplementary = supplementary;
        info.WingAddress = wing ?? info.Address;
        info.KeyAssigned = false;
    }
    internal static void NumberOffer(IEnumerable<CardModel> offered)
    {
        foreach (var group in offered.GroupBy(c => c.Id))
        {
            var index = 0;
            foreach (var card in group.OrderBy(c => Info(c).EntryOrder))
                Info(card).CopyNumber = ++index;
        }
    }
    internal static void AssignKeys(IEnumerable<CardModel> selected)
    {
        foreach (var selection in selected.Where(c => Find(c)?.Offer != null).GroupBy(c => Info(c).Offer!))
        {
            var pending = selection.ToList();
            var slots = Enumerable.Range(1, pending.Count).ToList();
            var state = RunCheckpoints.Require((RunState)pending[0].Owner.RunState);
            while (pending.Count > 0)
            {
                var pair = (from card in pending
                            from slot in slots
                            select (Card: card, Slot: slot, Priority: AddressRandom.UInt64(state.Seed,
                                RandomAddress.For("card-key assignment").With(selection.Key).With(card.Id.Entry).At(Info(card).CopyNumber).At(slot))))
                    .OrderBy(p => p.Priority).ThenBy(p => p.Card.Id.Entry, StringComparer.Ordinal)
                    .ThenBy(p => Info(p.Card).CopyNumber).ThenBy(p => p.Slot).First();
                var info = Info(pair.Card);
                info.Key = RandomAddress.For("card key").With(selection.Key).At(pair.Slot).Key;
                info.KeyAssigned = true;
                pending.Remove(pair.Card);
                slots.Remove(pair.Slot);
            }
        }
    }
    internal static void Initialize(RunState run)
    {
        foreach (var player in run.Players)
        {
            var index = 0;
            foreach (var card in player.Deck.Cards.OrderBy(c => c, CardOrder.Permanent))
            {
                var address = RandomAddress.For("card occurrence", "starting deck").At(index++).Key;
                Attach(card, new(address, address));
            }
        }
    }

    private static void Prepare(IEnumerable<CardModel> added, CardPile pile, AbstractModel? source)
    {
        if (pile.Type != PileType.Deck)
            return;
        var batch = added.ToArray();
        foreach (var card in batch.Where(c => Find(c)?.Offer != null && !Info(c).KeyAssigned))
            AssignKeys([card]);
        var fresh = batch.Where(c => Find(c) == null).OrderBy(c => c, CardOrder.Permanent).ToArray();
        if (fresh.Length == 0 || fresh[0].Owner.RunState is not RunState run || RunCheckpoints.Find(run) is not { } state)
            return;
        var address = RandomAddress.For("card occurrence", source?.Id.Entry ?? run.CurrentRoom?.ModelId?.Entry ?? "initial grants");
        var grant = state.Random.Advance(address);
        for (var i = 0; i < fresh.Length; i++)
        {
            var occurrence = address.At(grant).At(i).Key;
            Attach(fresh[i], new(occurrence, occurrence));
        }
    }

    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add),
        [typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool), typeof(bool)])]
    private static class DeckAdd
    {
        private static void Prefix(IEnumerable<CardModel> cards, CardPile newPile, AbstractModel? clonedBy) => Prepare(cards, newPile, clonedBy);
    }

    [HarmonyPatch(typeof(RunState), nameof(RunState.CloneCard))]
    private static class PermanentCopy
    {
        private static void Postfix(CardModel mutableCard, CardModel __result)
        {
            // Reward modifiers recreate a generated card; copying a permanent card starts a new occurrence.
            if (mutableCard.Pile?.Type == PileType.Deck)
            {
                cards.Remove(__result);
                if (copyGrant.Value is { } grant)
                {
                    grant.Ordinal ??= grant.Random.Advance(grant.Address);
                    var address = grant.Address.At(grant.Ordinal.Value).At(grant.Indices?.GetValueOrDefault(mutableCard) ?? 0).Key;
                    Attach(__result, new(address, address));
                }
            }
        }
    }

    [HarmonyPatch]
    private static class CopySources
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
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

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromSimpleGridForRewards))]
    private static class GridKeys
    {
        private static void Prefix(List<CardCreationResult> cards)
        {
            var generated = cards.Select(c => c.Card).Where(c => Find(c)?.Offer != null).ToArray();
            if (generated.Length == 0)
                return;
            var offer = Info(generated[0]).Offer!;
            for (var i = 0; i < generated.Length; i++)
            {
                Info(generated[i]).Offer = offer;
                Info(generated[i]).EntryOrder = i;
            }
            NumberOffer(generated);
        }
        private static void Postfix(ref Task<IEnumerable<CardModel>> __result) => __result = CompleteSelection(__result);
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseABundleScreen))]
    private static class BundleKeys
    {
        private static void Postfix(ref Task<IEnumerable<CardModel>> __result) => __result = CompleteSelection(__result);
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
    private static async Task<IEnumerable<CardModel>> CompleteSelection(Task<IEnumerable<CardModel>> task)
    {
        var selected = (await task).ToArray();
        AssignKeys(selected);
        return selected;
    }

    [HarmonyPatch(typeof(CardReward), nameof(CardReward.Populate))]
    private static class ManualOffer
    {
        private static void Prefix(List<CardCreationResult> ____cards, bool ____cardsWereManuallySet)
        {
            if (!____cardsWereManuallySet || ____cards.Count == 0 || ____cards.Any(c => Find(c.Card) == null))
                return;
            var offer = Info(____cards[0].Card).Address;
            for (var i = 0; i < ____cards.Count; i++)
                Offer(____cards[i].Card, offer, i);
            NumberOffer(____cards.Select(c => c.Card));
        }
    }
}
