using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Seedline;

internal static class PermanentSelectionPatches
{
    private static bool NextCard(MethodInfo method) => method.DeclaringType == typeof(Rng) && method.Name == nameof(Rng.NextItem) &&
        method.IsGenericMethod && method.GetGenericArguments()[0] == typeof(CardModel);

    [HarmonyPatch]
    private static class UpgradeBatches
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(Whetstone), nameof(Whetstone.AfterObtained)), AccessTools.Method(typeof(WarPaint), nameof(WarPaint.AfterObtained)),
            AccessTools.Method(typeof(SandCastle), nameof(SandCastle.AfterObtained)), AccessTools.Method(typeof(WarHammer), nameof(WarHammer.AfterCombatVictory)),
            RewardPatches.AsyncMethod(typeof(FragrantMushroom), nameof(FragrantMushroom.AfterObtained)),
            RewardPatches.AsyncMethod(typeof(BattlewornDummy), nameof(BattlewornDummy.Resume))];

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(RewardPatches.Replace(instructions,
                m => m.DeclaringType == typeof(ListExtensions) && m.Name == nameof(ListExtensions.StableShuffle),
                AccessTools.Method(typeof(UpgradeBatches), nameof(Snapshot))),
                m => m.DeclaringType == typeof(Enumerable) && m.Name == nameof(Enumerable.Take),
                AccessTools.Method(typeof(PermanentSelection), nameof(PermanentSelection.UpgradeBatch)));
        private static List<CardModel> Snapshot(List<CardModel> cards, Rng rng) =>
            RunScope.IsBypassed || CharacterCompatibility.IsModdedRun ? cards.StableShuffle(rng) : cards;
    }

    [HarmonyPatch]
    private static class UpgradeSingles
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(FishingRod), nameof(FishingRod.AfterCombatEnd)),
            AccessTools.Method(typeof(EndlessConveyor), "SpicySnappy"), AccessTools.Method(typeof(EndlessConveyor), "ObserveChef"),
            RewardPatches.AsyncMethod(typeof(TabletOfTruth), "LoseMaxHpAndUpgrade")];
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, NextCard, AccessTools.Method(typeof(PermanentSelection), nameof(PermanentSelection.UpgradeOne)));
    }

    [HarmonyPatch(typeof(ImprovementPower), nameof(ImprovementPower.AfterCombatEnd))]
    private static class Improvement
    {
        private static bool Prefix(ImprovementPower __instance, ref Task __result)
        {
            if (CharacterCompatibility.UseVanillaCards(__instance.Owner.Player)) return true;
            foreach (var card in PermanentSelection.UpgradeBatch(__instance.Owner.Player!.Deck.Cards.Where(c => c.IsUpgradable), (int)__instance.Amount))
                CardCmd.Upgrade(card);
            __result = Task.CompletedTask;
            return false;
        }
    }

    [HarmonyPatch]
    private static class Wongos
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(WelcomeToWongos), "Leave");
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, NextCard, AccessTools.Method(typeof(PermanentSelection), nameof(PermanentSelection.DowngradeOne)));
    }

    [HarmonyPatch]
    private static class Mirrors
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(Reflections), "TouchAMirror");
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var lists = 0;
            var draws = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.operand is MethodInfo method)
                {
                    if (method.DeclaringType == typeof(Enumerable) && method.Name == nameof(Enumerable.ToList) &&
                        method.GetGenericArguments()[0] == typeof(CardModel))
                        instruction.operand = AccessTools.Method(typeof(Mirrors), lists++ == 0 ? nameof(Downgrades) : nameof(Upgrades));
                    else if (NextCard(method))
                    {
                        instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                        instruction.operand = AccessTools.Method(typeof(Mirrors), draws++ == 0 ? nameof(Downgrade) : nameof(Upgrade));
                    }
                }
                yield return instruction;
            }
            if (lists != 2 || draws != 2)
                throw new InvalidOperationException("Expected two Reflections selection batches.");
        }
        private static List<CardModel> Downgrades(IEnumerable<CardModel> cards) => RunScope.IsBypassed || CharacterCompatibility.IsModdedRun
            ? cards.ToList() : PermanentSelection.Select(cards, 2, PermanentSelection.Downgrade).ToList();
        private static List<CardModel> Upgrades(IEnumerable<CardModel> cards) => RunScope.IsBypassed || CharacterCompatibility.IsModdedRun
            ? cards.ToList() : PermanentSelection.UpgradeBatch(cards, 4).ToList();
        private static CardModel? Downgrade(Rng rng, IEnumerable<CardModel> cards) => RunScope.IsBypassed || CharacterCompatibility.IsModdedRun
            ? rng.NextItem(cards) : PermanentSelection.RefillDowngrade(rng, cards.FirstOrDefault());
        private static CardModel Upgrade(Rng rng, IEnumerable<CardModel> cards) => RunScope.IsBypassed || CharacterCompatibility.IsModdedRun
            ? rng.NextItem(cards)! : cards.First();
    }

    [HarmonyPatch(typeof(SlipperyBridge), "GetNewRandomCard")]
    private static class Bridge
    {
        private static void Prefix(CardModel? ____randomCardToLose)
        {
            if (RunScope.IsBypassed || CharacterCompatibility.IsModdedRun || ____randomCardToLose is not { } card)
                return;
            var state = PermanentSelection.State(card);
            state.Selection.Renew(state.Seed, PermanentSelection.Bridge, PermanentSelection.Key(card));
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, NextCard, AccessTools.Method(typeof(Bridge), nameof(Select)));
        private static CardModel? Select(Rng rng, IEnumerable<CardModel> cards) => RunScope.IsBypassed || CharacterCompatibility.IsModdedRun
            ? rng.NextItem(cards) : PermanentSelection.Select(cards, 1, PermanentSelection.Bridge).FirstOrDefault();
    }

    [HarmonyPatch(typeof(BeautifulBracelet), nameof(BeautifulBracelet.AfterObtained))]
    private static class Bracelet
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(IEnumerableExtensions) && m.Name == nameof(IEnumerableExtensions.TakeRandom),
                AccessTools.Method(typeof(Bracelet), nameof(Select)));
        private static IEnumerable<CardModel> Select(IEnumerable<CardModel> cards, int count, Rng rng) => RunScope.IsBypassed || CharacterCompatibility.IsModdedRun
            ? cards.TakeRandom(count, rng) : PermanentSelection.Fixed(cards, count, "Beautiful Bracelet");
    }

    [HarmonyPatch]
    private static class Tooth
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(PaelsTooth), nameof(PaelsTooth.AfterCombatEnd));
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Rng) && m.Name == nameof(Rng.NextItem),
                AccessTools.Method(typeof(Tooth), nameof(Select)));
        private static SerializableCard Select(Rng rng, IEnumerable<SerializableCard> cards)
        {
            if (RunScope.IsBypassed || CharacterCompatibility.IsModdedRun) return rng.NextItem(cards)!;
            var seed = RunCheckpoints.Require(RunManager.Instance.DebugOnlyGetState()!).Seed;
            return cards.OrderBy(c => AddressRandom.UInt64(seed, RandomAddress.For("Pael's Tooth return", Info(c).Key)))
                .ThenBy(c => Info(c).Key, StringComparer.Ordinal).First();
        }
        private static CardOccurrence Info(SerializableCard card) => CardOccurrences.Read(card.Props!.strings!.Single(p => p.name == CardOccurrences.SavedName).value);
    }

    [HarmonyPatch]
    private static class PotionSacrifice
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(RanwidTheElder), "GenerateInitialOptions"), AccessTools.Method(typeof(StoneOfAllTime), "GenerateInitialOptions")];
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Rng) && m.Name == nameof(Rng.NextItem) &&
                m.IsGenericMethod && m.GetGenericArguments()[0] == typeof(PotionModel), AccessTools.Method(typeof(PotionSacrifice), nameof(Select)));
        private static PotionModel? Select(Rng rng, IEnumerable<PotionModel> potions) => RunScope.IsBypassed ? rng.NextItem(potions)
            : PermanentSelection.Sacrifice(potions);
    }
}
