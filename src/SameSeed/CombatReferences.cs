using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace SameSeed;

internal static class CombatReferences
{
    // Suspended game iterators keep physical references. Distinct outstanding slots must remain distinct.
    private sealed class PendingFrame(PendingFrame? parent, int repeats = 0)
    {
        internal PendingFrame? Parent { get; } = parent;
        internal HashSet<CardModel> Cards { get; } = new();
        internal HashSet<CardModel> Sly { get; } = new();
        internal int Repeats = repeats;
    }
    private static readonly AsyncLocal<PendingFrame?> pending = new();

    // Only gameplay references participate; UI and queued actions retain physical cards.
    private static readonly Dictionary<string, string> dataFields = new()
    {
        ["AfterimagePower"] = "amountsForPlayedCards",
        ["CalamityPower"] = "amountsForPlayedCards",
        ["GravityPower"] = "amountsForPlayedCards",
        ["MonologuePower"] = "amountsForPlayedCards",
        ["OblivionPower"] = "amountsForPlayedCards",
        ["SerpentFormPower"] = "amountsForPlayedCards",
        ["StormPower"] = "amountsForPlayedCards",
        ["StranglePower"] = "amountsForPlayedCards",
        ["SubroutinePower"] = "amountsForPlayedCards",
        ["RupturePower"] = "playedCards",
        ["DampenPower"] = "downgradedCardsToOldUpgradeLevels",
        ["EncorePower"] = "cardsBeingPlayed",
        ["HellraiserPower"] = "autoPlayingCards",
        ["CurlUpPower"] = "playedCard",
        ["JugglingPower"] = "cardToClone",
        ["NightmarePower"] = "selectedCard",
        ["StretchPower"] = "cardPlay",
        ["ImitationLearningPower"] = "cardsAndClones"
    };
    private static readonly Dictionary<string, string> directFields = new()
    {
        ["ShowOffPower"] = "_showOffInstance",
        ["MusicBox"] = "_cardBeingPlayed",
        ["PenNib"] = "_attackToDouble",
        ["Vambrace"] = "_triggeringCard",
        ["UnsettlingLamp"] = "_triggeringCard",
        ["PaelsLegion"] = "_affectedCardPlay",
        ["SwipePower"] = "_stolenCard"
    };
    private static readonly FieldInfo internalData = Field(typeof(PowerModel), "_internalData");
    private static readonly FieldInfo allCards = Field(typeof(CombatState), "_allCards");
    private static readonly FieldInfo deck = Field(typeof(CardModel), "_deckVersion");
    private static readonly FieldInfo clone = Field(typeof(CardModel), "_cloneOf");
    private static readonly FieldInfo dupe = Field(typeof(CardModel), "_isDupe");
    private static readonly FieldInfo playCard = Field(typeof(CardPlay), "<Card>k__BackingField");

    private static FieldInfo Field(Type type, string name) => AccessTools.Field(type, name)
        ?? throw new MissingFieldException(type.FullName, name);

    private static IEnumerable<(object Owner, FieldInfo Field)> Slots(CombatState combat)
    {
        foreach (var power in combat.Creatures.Concat(combat.EscapedCreatures).Distinct().SelectMany(c => c.Powers))
        {
            if (dataFields.TryGetValue(power.GetType().Name, out var name) && internalData.GetValue(power) is { } data)
                yield return (data, Field(data.GetType(), name));
            if (directFields.TryGetValue(power.GetType().Name, out name))
                yield return (power, Field(power.GetType(), name));
        }
        foreach (var relic in combat.Players.SelectMany(p => p.Relics))
            if (directFields.TryGetValue(relic.GetType().Name, out var name))
                yield return (relic, Field(relic.GetType(), name));
    }

    internal static bool PendingEquivalent(CardModel a, CardModel b)
    {
        if (!ReferenceEquals(a, b))
            for (var frame = pending.Value; frame != null; frame = frame.Parent)
                if (frame.Cards.Contains(a) || frame.Cards.Contains(b)) return false;
        if (a.CombatState is not CombatState combat) return true;
        foreach (var (owner, field) in Slots(combat))
        {
            var value = field.GetValue(owner);
            switch (value)
            {
                case CardPlay play:
                    if (ReferenceEquals(play.Card, a) != ReferenceEquals(play.Card, b)) return false;
                    break;
                case List<(CardModel Card, CardModel Clone)> pairs:
                    foreach (var pair in pairs)
                        if (ReferenceEquals(pair.Card, a) != ReferenceEquals(pair.Card, b) ||
                            ReferenceEquals(pair.Clone, a) != ReferenceEquals(pair.Clone, b)) return false;
                    break;
                case Dictionary<CardModel, int> amounts:
                    var hasA = amounts.TryGetValue(a, out var amountA);
                    var hasB = amounts.TryGetValue(b, out var amountB);
                    if (hasA != hasB || hasA && amountA != amountB) return false;
                    break;
                case HashSet<CardModel> set:
                    if (set.Contains(a) != set.Contains(b)) return false;
                    break;
                case List<CardModel> list:
                    // Positions are consequential when a pending effect uses an ordered list.
                    if (!list.Select((c, i) => (c, i)).Where(x => ReferenceEquals(x.c, a)).Select(x => x.i)
                        .SequenceEqual(list.Select((c, i) => (c, i)).Where(x => ReferenceEquals(x.c, b)).Select(x => x.i)))
                        return false;
                    break;
                case CardModel card:
                    if (ReferenceEquals(card, a) != ReferenceEquals(card, b)) return false;
                    break;
            }
        }
        return true;
    }

    internal static void Rebind(CombatState combat, IReadOnlyList<CardModel> physical, IReadOnlyList<CardModel> logical)
    {
        if (physical.Count != logical.Count || physical.Distinct().Count() != physical.Count ||
            logical.Distinct().Count() != logical.Count || !physical.ToHashSet().SetEquals(logical))
            throw new ArgumentException("Combat reference rebinding requires a permutation of participating cards.");
        var map = logical.Select((card, i) => (card, destination: physical[i])).ToDictionary(x => x.card, x => x.destination);
        CardModel? Map(CardModel? card) => card != null && map.TryGetValue(card, out var replacement) ? replacement : card;

        // Snapshot relationship state before assigning any participant, including incoming clone links.
        var cards = ((IEnumerable<CardModel>)allCards.GetValue(combat)!).Concat(logical).Distinct().ToArray();
        var relationships = cards.Select(card => (card, deck: deck.GetValue(card),
            clone: (CardModel?)clone.GetValue(card), dupe: dupe.GetValue(card))).ToArray();
        foreach (var state in relationships)
        {
            var destination = Map(state.card)!;
            deck.SetValue(destination, state.deck);
            clone.SetValue(destination, Map(state.clone));
            dupe.SetValue(destination, state.dupe);
        }

        var plays = new HashSet<CardPlay>();
        foreach (var entry in CombatManager.Instance.History.Entries)
        {
            foreach (var name in new[] { "Card", "CardSource" })
            {
                var field = AccessTools.Field(entry.GetType(), $"<{name}>k__BackingField");
                if (field?.GetValue(entry) is CardModel card) field.SetValue(entry, Map(card));
            }
            if (AccessTools.Property(entry.GetType(), "CardPlay")?.GetValue(entry) is CardPlay play)
                plays.Add(play);
        }
        foreach (var (owner, field) in Slots(combat))
        {
            switch (field.GetValue(owner))
            {
                case CardPlay play:
                    plays.Add(play);
                    break;
                case List<(CardModel Card, CardModel Clone)> pairs:
                    for (var i = 0; i < pairs.Count; i++) pairs[i] = (Map(pairs[i].Card)!, Map(pairs[i].Clone)!);
                    break;
                case Dictionary<CardModel, int> amounts:
                    var entries = amounts.ToArray();
                    amounts.Clear();
                    foreach (var entry in entries) amounts.Add(Map(entry.Key)!, entry.Value);
                    break;
                case HashSet<CardModel> set:
                    var members = set.ToArray();
                    set.Clear();
                    foreach (var card in members) set.Add(Map(card)!);
                    break;
                case List<CardModel> list:
                    for (var i = 0; i < list.Count; i++) list[i] = Map(list[i])!;
                    break;
                case CardModel card:
                    field.SetValue(owner, Map(card));
                    break;
            }
        }
        foreach (var play in plays) playCard.SetValue(play, Map(play.Card));
    }

    private static async Task PendingAutoPlay(PlayerChoiceContext context, CardModel card, Creature? target,
        AutoPlayType type, bool skipXCapture, bool skipCardPileVisuals)
    {
        var frame = pending.Value ?? throw new InvalidOperationException("Missing pending auto-play frame.");
        if (frame.Repeats > 0) frame.Cards.Add(card);
        try { await CardCmd.AutoPlay(context, card, target, type, skipXCapture, skipCardPileVisuals); }
        finally
        {
            if (frame.Repeats == 0 || --frame.Repeats == 0) frame.Cards.Remove(card);
        }
    }

    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.DiscardAndDraw))]
    private static class PendingDiscard
    {
        private static void Prefix(ref IEnumerable<CardModel> cardsToDiscard, out PendingFrame? __state)
        {
            __state = pending.Value;
            cardsToDiscard = cardsToDiscard.ToArray();
            var frame = new PendingFrame(__state);
            frame.Cards.UnionWith(cardsToDiscard);
            pending.Value = frame;
        }
        private static void Finalizer(PendingFrame? __state) => pending.Value = __state;
    }

    [HarmonyPatch]
    private static class PendingDiscardSteps
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(CardCmd), nameof(CardCmd.DiscardAndDraw));
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var patched = RewardPatches.Replace(instructions,
                m => m.DeclaringType == typeof(List<CardModel>) && m.Name == nameof(List<CardModel>.Add),
                AccessTools.Method(typeof(PendingDiscardSteps), nameof(AddSly)));
            patched = RewardPatches.Replace(patched,
                m => m.DeclaringType == typeof(Hook) && m.Name == nameof(Hook.AfterCardDiscarded),
                AccessTools.Method(typeof(PendingDiscardSteps), nameof(AfterDiscard)));
            return RewardPatches.Replace(patched,
                m => m.DeclaringType == typeof(CardCmd) && m.Name == nameof(CardCmd.AutoPlay),
                AccessTools.Method(typeof(CombatReferences), nameof(PendingAutoPlay)));
        }
        private static void AddSly(List<CardModel> cards, CardModel card)
        {
            cards.Add(card);
            pending.Value!.Sly.Add(card);
        }
        private static async Task AfterDiscard(ICombatState combat, PlayerChoiceContext context, CardModel card)
        {
            var frame = pending.Value!;
            try { await Hook.AfterCardDiscarded(combat, context, card); }
            finally { if (!frame.Sly.Contains(card)) frame.Cards.Remove(card); }
        }
    }

    [HarmonyPatch]
    private static class PendingPlayBatches
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            new[] { typeof(BeatDown), typeof(ChoChorus), typeof(Eidolon), typeof(DecisionsDecisions) }
                .Select(t => AccessTools.Method(t, "OnPlay"));
        private static void Prefix(CardModel __instance, out PendingFrame? __state)
        {
            __state = pending.Value;
            pending.Value = new(__state, __instance is DecisionsDecisions ? __instance.DynamicVars.Repeat.IntValue : 0);
        }
        private static void Finalizer(PendingFrame? __state) => pending.Value = __state;
    }

    [HarmonyPatch]
    private static class PendingPlaySteps
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            new[] { typeof(BeatDown), typeof(ChoChorus), typeof(Eidolon), typeof(DecisionsDecisions) }
                .Select(t => RewardPatches.AsyncMethod(t, "OnPlay"));
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var type = __originalMethod.DeclaringType!.DeclaringType!;
            var patched = instructions;
            if (type != typeof(DecisionsDecisions))
                patched = RewardPatches.Replace(patched,
                    m => m.DeclaringType == typeof(Enumerable) && m.IsGenericMethod && m.GetGenericArguments()[0] == typeof(CardModel) &&
                        m.Name == (type == typeof(BeatDown) ? nameof(Enumerable.Take) : nameof(Enumerable.ToList)),
                    AccessTools.Method(typeof(PendingPlaySteps), type == typeof(BeatDown) ? nameof(Take) : nameof(List)));
            return RewardPatches.Replace(patched,
                m => m.DeclaringType == typeof(CardCmd) && m.Name == nameof(CardCmd.AutoPlay),
                AccessTools.Method(typeof(CombatReferences), nameof(PendingAutoPlay)), type == typeof(BeatDown) ? 2 : 1);
        }
        private static List<CardModel> List(IEnumerable<CardModel> cards)
        {
            var result = cards.ToList();
            pending.Value!.Cards.UnionWith(result);
            return result;
        }
        private static IEnumerable<CardModel> Take(IEnumerable<CardModel> cards, int count) => List(cards.Take(count));
    }
}
