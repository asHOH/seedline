using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class PermanentGeneration
{
    private static readonly ConditionalWeakTable<CardModel, CardModel> prepared = new();
    internal static CardModel Draw(Player player, CardPoolModel pool, IEnumerable<CardModel> eligible, bool sourceKey = true) => CardGeneration.Draw(player,
        RandomAddress.For("permanent card identity", pool.Id.Entry),
        pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(c => pool is CurseCardPool || c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare), eligible, sourceKey);

    internal static CardTransformation[] Prepare(IEnumerable<CardTransformation> transformations)
    {
        var batch = transformations.ToArray();
        if (batch.Length > 0 && CharacterCompatibility.UseVanillaCards(batch[0].Original.Owner))
            return batch;
        if (batch.Length > 0 && !batch[0].IsInCombat)
            CardOccurrences.Prepare(batch.Select(t => t.Replacement).OfType<CardModel>(), batch[0].Original.Owner.Deck, null);
        var replacements = new Dictionary<CardModel, CardModel>();
        foreach (var item in batch.Where(t => t.Replacement == null)
            .OrderBy(t => t.Original, CardOrder.Identity).ThenBy(t => t.Original.Pile!.Cards.IndexOf(t.Original)))
        {
            var original = item.Original;
            var eligible = item.ReplacementOptions == null ? CardFactory.GetDefaultTransformationOptions(original, item.IsInCombat)
                : (CardModel[])AccessTools.Method(typeof(CardFactory), "GetFilteredTransformationOptions")
                    .Invoke(null, [original, item.ReplacementOptions, item.IsInCombat])!;
            var cards = eligible.ToArray();
            var pool = cards[0].Pool;
            replacements.Add(original, item.IsInCombat
                ? CombatGeneration.Draw(original.Owner, "ordinary costs", pool.Id.Entry, CombatGeneration.Pool(original.Owner, pool), cards)
                : Draw(original.Owner, pool, cards));
        }
        return batch.Select(t => replacements.TryGetValue(t.Original, out var replacement)
            ? new CardTransformation(t.Original, replacement) : t).ToArray();
    }

    internal static List<CardCreationResult> Generate(Player player, int count, CardCreationOptions options, string source)
    {
        options = Hook.ModifyCardRewardCreationOptions(player.RunState, player, options);
        var pool = options.CardPools.Single();
        var eligible = options.GetPossibleCards(player).Where(c => c.IsAllowedInRun(player.RunState) &&
            c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare).ToArray();
        var state = RunCheckpoints.Require((RunState)player.RunState);
        var results = new List<CardCreationResult>();
        for (var i = 0; i < count; i++)
        {
            var candidates = eligible.Where(c => results.All(r => r.Card.Id != c.Id)).ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException("No eligible permanent card addition.");
            if (options.RarityOdds != CardRarityOddsType.Uniform)
            {
                var roll = source == "Fried Eel" ? state.Random.NextUnit(RandomAddress.For("card rarity", source))
                    : CardRewards.EventRng(player).NextFloat();
                var rare = player.RunState.AscensionLevel >= 7 ? 0.0149 : 0.03;
                var rarity = roll < rare ? CardRarity.Rare : roll < rare + 0.37 ? CardRarity.Uncommon : CardRarity.Common;
                while (!candidates.Any(c => c.Rarity == rarity))
                    rarity = rarity == CardRarity.Common ? CardRarity.Uncommon : rarity == CardRarity.Uncommon ? CardRarity.Rare : CardRarity.Common;
                candidates = candidates.Where(c => c.Rarity == rarity).ToArray();
            }
            var card = Draw(player, pool, candidates, source != "Infested Automaton");
            if (!options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll))
                CardGeneration.Upgrade(player, card);
            results.Add(new(card));
        }
        if (!options.Flags.HasFlag(CardCreationFlags.NoModifyHooks) && Hook.TryModifyCardRewardOptions(player.RunState, player, results, options, out var modifiers))
            TaskHelper.RunSafely(Hook.AfterModifyingCardRewardOptions(player.RunState, modifiers));
        return results;
    }

    [HarmonyPatch]
    private static class TransformBatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.Method(typeof(CardCmd), nameof(CardCmd.Transform),
            [typeof(IEnumerable<CardTransformation>), typeof(Rng), typeof(CardPreviewStyle)])
            .GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()!.StateMachineType, "MoveNext");
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => RewardPatches.Replace(instructions,
            m => m.DeclaringType == typeof(Enumerable) && m.Name == nameof(Enumerable.ToArray) && m.GetGenericArguments()[0] == typeof(CardTransformation),
            AccessTools.Method(typeof(PermanentGeneration), nameof(Prepare)));
    }

    [HarmonyPatch]
    private static class SequentialBatches
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [RewardPatches.AsyncMethod(typeof(Astrolabe), nameof(Astrolabe.AfterObtained)),
            RewardPatches.AsyncMethod(typeof(MorphicGrove), "Group"), RewardPatches.AsyncMethod(typeof(Trial), "NondescriptInnocent"),
            RewardPatches.AsyncMethod(typeof(EntropyPower), nameof(EntropyPower.AfterPlayerTurnStart))];

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            instructions = RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Enumerable) && m.Name == nameof(Enumerable.ToList) &&
                m.GetGenericArguments()[0] == typeof(CardModel), AccessTools.Method(typeof(SequentialBatches), nameof(Resolve)));
            return __originalMethod.DeclaringType!.DeclaringType == typeof(Astrolabe)
                ? RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(CardFactory) && m.Name == nameof(CardFactory.CreateRandomCardForTransform),
                    AccessTools.Method(typeof(SequentialBatches), nameof(Replacement)))
                : RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(CardCmd) && m.Name == nameof(CardCmd.TransformToRandom),
                    AccessTools.Method(typeof(SequentialBatches), nameof(Transform)));
        }
        private static List<CardModel> Resolve(IEnumerable<CardModel> selected)
        {
            var cards = selected.ToList();
            if (CharacterCompatibility.IsModdedRun) return cards;
            foreach (var item in Prepare(cards.Select(c => new CardTransformation(c))))
                prepared.Add(item.Original, item.Replacement!);
            return cards;
        }
        private static CardModel Replacement(CardModel original, bool isInCombat, Rng rng)
        {
            if (CharacterCompatibility.UseVanillaCards(original.Owner))
                return CardFactory.CreateRandomCardForTransform(original, isInCombat, rng);
            var replacement = prepared.GetValue(original, _ => throw new InvalidOperationException("Missing prepared transformation."));
            prepared.Remove(original);
            return replacement;
        }
        private static async Task<CardPileAddResult> Transform(CardModel original, Rng rng, CardPreviewStyle style) =>
            CharacterCompatibility.UseVanillaCards(original.Owner) ? await CardCmd.TransformToRandom(original, rng, style) :
                (await CardCmd.Transform(original, Replacement(original, false, rng), style))!.Value;
    }

    [HarmonyPatch]
    private static class PandoraBatch
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(PandorasBox), nameof(PandorasBox.AfterObtained));
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => RewardPatches.Replace(instructions,
            m => m.DeclaringType == typeof(Enumerable) && m.Name == nameof(Enumerable.Select), AccessTools.Method(typeof(PandoraBatch), nameof(Deferred)));
        private static IEnumerable<CardTransformation> Deferred(IEnumerable<CardModel> cards, Func<CardModel, CardTransformation> transform) =>
            CharacterCompatibility.IsModdedRun ? cards.Select(transform) : cards.Select(c => new CardTransformation(c));
    }

    [HarmonyPatch]
    private static class Additions
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [RewardPatches.AsyncMethod(typeof(InfestedAutomaton), "Study"), RewardPatches.AsyncMethod(typeof(InfestedAutomaton), "TouchCore"),
            RewardPatches.AsyncMethod(typeof(EndlessConveyor), "FriedEel"), RewardPatches.AsyncMethod(typeof(ArcaneScroll), "AfterObtained"),
            RewardPatches.AsyncMethod(typeof(WaterfallOfObviousSecrets), "Negotiate")];
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(CardFactory) && m.Name == nameof(CardFactory.CreateForReward),
                AccessTools.Method(typeof(Additions), __originalMethod.DeclaringType!.DeclaringType == typeof(EndlessConveyor) ? nameof(Eel) :
                    __originalMethod.DeclaringType!.DeclaringType == typeof(InfestedAutomaton) ? nameof(Automaton) : nameof(Create)));
        private static IEnumerable<CardCreationResult> Automaton(Player player, int count, CardCreationOptions options) => Generate(player, count, options, "Infested Automaton");
        private static IEnumerable<CardCreationResult> Create(Player player, int count, CardCreationOptions options) => Generate(player, count, options, "direct addition");
        private static IEnumerable<CardCreationResult> Eel(Player player, int count, CardCreationOptions options) => Generate(player, count, options, "Fried Eel");
    }

    [HarmonyPatch]
    private static class Curses
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [RewardPatches.AsyncMethod(typeof(NeowsBones), "AfterObtained"), RewardPatches.AsyncMethod(typeof(DistinguishedCape), "AfterObtained"),
            RewardPatches.AsyncMethod(typeof(MuseumOfTorture), "TakeRelic")];
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            instructions = RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Rng) && m.Name == nameof(Rng.NextItem),
                AccessTools.Method(typeof(Curses), nameof(Choose)));
            instructions = RewardPatches.Replace(instructions, m => m.Name == "CreateCard" && !m.IsGenericMethod,
                AccessTools.Method(typeof(Curses), nameof(Created)));
            return __originalMethod.DeclaringType!.DeclaringType == typeof(MuseumOfTorture) ? instructions : RewardPatches.Replace(instructions,
                m => m.DeclaringType == typeof(List<CardModel>) && m.Name == "Remove", AccessTools.Method(typeof(Curses), nameof(Remove)));
        }
        private static CardModel Choose(Rng _, IEnumerable<CardModel> eligible) => Draw(RunManager.Instance.DebugOnlyGetState()!.Players[0], ModelDb.CardPool<CurseCardPool>(), eligible, false);
        private static CardModel Created(IRunState _, CardModel card, Player player) => card;
        private static bool Remove(List<CardModel> cards, CardModel card) => cards.Remove(card.CanonicalInstance);
    }
}
