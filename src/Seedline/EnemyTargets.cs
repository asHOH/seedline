using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;

namespace Seedline;

internal static class EnemyTargets
{
    internal static string CardGroup(CardModel card) => card.Type == CardType.Attack ||
        card is BubbleBubble or CaptureSpirit or DeadlyPoison or Judgement or KnifeTrap or Snakebite
        ? "Direct damage" : "Other";

    internal static Creature? Select(IEnumerable<Creature> candidates, string group)
    {
        var eligible = candidates.ToArray();
        if (eligible.Length < 2)
            return eligible.FirstOrDefault();
        var scope = CombatStateScope.Require((CombatState)eligible[0].CombatState!);
        var sequence = scope.For("targeting", group);
        var opportunity = sequence.At(scope.Random.Advance(sequence));
        return eligible.OrderBy(c => AddressRandom.UInt64(scope.Seed, opportunity.With(EnemyOccurrences.Info(c).Address.Key)))
            .ThenBy(c => EnemyOccurrences.Info(c).Address.Key, StringComparer.Ordinal).First();
    }

    private static Creature? Choose(Rng rng, IEnumerable<Creature> candidates, string group) =>
        CharacterCompatibility.IsModdedRun ? rng.NextItem(candidates) : Select(candidates, group);
    private static Creature? Damage(Rng rng, IEnumerable<Creature> candidates) => Choose(rng, candidates, "Direct damage");
    private static Creature? Lightning(Rng rng, IEnumerable<Creature> candidates) => Choose(rng, candidates, "Lightning");
    private static Creature? Relics(Rng rng, IEnumerable<Creature> candidates) => Choose(rng, candidates, "Forgotten Soul / Obsidian Vice");
    private static Creature? Other(Rng rng, IEnumerable<Creature> candidates) => Choose(rng, candidates, "Other");
    private static Creature? AutoTarget(Rng rng, IEnumerable<Creature> candidates, CardModel card)
    {
        // Ally targeting is outside the enemy-target policy.
        return card.TargetType == TargetType.AnyAlly ? rng.NextItem(candidates) : Choose(rng, candidates, CardGroup(card));
    }

    private static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions, string method, int count = 1) =>
        RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Rng) && m.Name == nameof(Rng.NextItem) &&
            m.GetGenericArguments().SequenceEqual([typeof(Creature)]), AccessTools.Method(typeof(EnemyTargets), method), count);

    [HarmonyPatch]
    private static class DamageSources
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [RewardPatches.AsyncMethod(typeof(AttackCommand), nameof(AttackCommand.Execute)),
            RewardPatches.AsyncMethod(typeof(BeatDown), "OnPlay"), RewardPatches.AsyncMethod(typeof(BouncingFlask), "OnPlay"),
            RewardPatches.AsyncMethod(typeof(CacophonyPower), nameof(CacophonyPower.AfterCardDrawn)),
            RewardPatches.AsyncMethod(typeof(HauntPower), nameof(HauntPower.AfterCardPlayed)),
            RewardPatches.AsyncMethod(typeof(JuggernautPower), nameof(JuggernautPower.AfterBlockGained)),
            RewardPatches.AsyncMethod(typeof(SerpentFormPower), nameof(SerpentFormPower.AfterCardPlayed)),
            RewardPatches.AsyncMethod(typeof(Tingsha), nameof(Tingsha.AfterCardDiscarded)),
            RewardPatches.AsyncMethod(typeof(Kusarigama), nameof(Kusarigama.AfterCardPlayed)),
            RewardPatches.AsyncMethod(typeof(ParryingShield), nameof(ParryingShield.AfterSideTurnEnd))];
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Replace(instructions, nameof(Damage));
    }

    [HarmonyPatch]
    private static class LightningSource
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(LightningOrb), "ApplyLightningDamage");
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Replace(instructions, nameof(Lightning));
    }

    [HarmonyPatch]
    private static class RelicSources
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [RewardPatches.AsyncMethod(typeof(ForgottenSoul), nameof(ForgottenSoul.AfterCardExhausted)),
            RewardPatches.AsyncMethod(typeof(ObsidianVice), nameof(ObsidianVice.BeforeSideTurnEnd))];
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Replace(instructions, nameof(Relics));
    }

    [HarmonyPatch]
    private static class Countdown
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(CountdownPower), nameof(CountdownPower.AfterSideTurnStart));
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Replace(instructions, nameof(Other));
    }

    [HarmonyPatch]
    private static class AutoPlayTargets
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(CardCmd), nameof(CardCmd.AutoPlay));
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var card = AccessTools.Field(__originalMethod.DeclaringType, "card");
            var count = 0;
            foreach (var instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                    instruction.operand is MethodInfo method && method.DeclaringType == typeof(Rng) &&
                    method.Name == nameof(Rng.NextItem) && method.GetGenericArguments().SequenceEqual([typeof(Creature)]))
                {
                    // Read the operation's card even when callers inline the AutoPlay async entry point.
                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                    yield return new CodeInstruction(OpCodes.Ldfld, card);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(EnemyTargets), nameof(AutoTarget));
                    count++;
                }
                yield return instruction;
            }
            if (count != 2)
                throw new InvalidOperationException($"Expected two automatic-play target selections, found {count}.");
        }
    }
}
