using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Seedline;

internal static class CombatCosts
{
    private sealed class DrawCost(CardModel card)
    {
        internal CardModel Card { get; } = card;
        internal int? Confused;
    }
    private static readonly AsyncLocal<DrawCost?> draw = new();
    private static readonly Func<ConfusedPower, int> vanillaConfused = AccessTools.MethodDelegate<Func<ConfusedPower, int>>(
        AccessTools.Method(typeof(ConfusedPower), "NextEnergyCost"));
    private static readonly AccessTools.FieldRef<ConfusedPower, int> testConfused =
        AccessTools.FieldRefAccess<ConfusedPower, int>("_testEnergyCostOverride");

    internal static string Category(CardModel card)
    {
        var starters = card.Owner.Character.StartingDeck.DistinctBy(c => c.Id).ToArray();
        var starter = starters.FirstOrDefault(c => c.Id == card.Id);
        if (starter == null)
            return "others";
        if (starter.Tags.Contains(CardTag.Strike))
            return "starter Strikes";
        if (starter.Tags.Contains(CardTag.Defend))
            return "starter Defends";
        var basics = starters.Where(c => c.Rarity == CardRarity.Basic &&
            !c.Tags.Contains(CardTag.Strike) && !c.Tags.Contains(CardTag.Defend)).ToArray();
        var index = Array.FindIndex(basics, c => c.Id == card.Id);
        return index < 0 ? "others" : $"other Basic {index + 1}";
    }

    private static int Confused(ConfusedPower power, CardModel card)
    {
        if (CharacterCompatibility.UseVanillaCards(card.Owner))
            return vanillaConfused(power);
        var scope = CombatStateScope.Require((CombatState)power.Owner.CombatState!);
        var value = testConfused(power) >= 0 ? testConfused(power)
            : scope.Random.NextIntInclusive(scope.For("Confused cost", Category(card)), 0, 3);
        if (draw.Value is { } current && current.Card == card)
            current.Confused = value;
        return value;
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDrawn))]
    private static class Draw
    {
        private static void Prefix(CardModel card, out DrawCost? __state)
        {
            __state = draw.Value;
            draw.Value = new(card);
        }
        private static void Finalizer(DrawCost? __state) => draw.Value = __state;
    }

    [HarmonyPatch(typeof(ConfusedPower), nameof(ConfusedPower.AfterCardDrawn))]
    private static class ConfusedDraw
    {
        private static bool Prefix(CardModel card, ref Task __result)
        {
            if (CharacterCompatibility.UseVanillaCards(card.Owner) || !card.EnergyCost.CostsX)
                return true;
            __result = Task.CompletedTask;
            return false;
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var count = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(AccessTools.Method(typeof(ConfusedPower), "NextEnergyCost")))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_2).MoveLabelsFrom(instruction);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(CombatCosts), nameof(Confused));
                    count++;
                }
                yield return instruction;
            }
            if (count != 1)
                throw new InvalidOperationException("Expected one Confused cost roll.");
        }
    }

    [HarmonyPatch(typeof(Slither), "NextEnergyCost")]
    private static class SlitherCost
    {
        private static bool Prefix(Slither __instance, ref int __result)
        {
            if (CharacterCompatibility.UseVanillaCards(__instance.Card.Owner)) return true;
            if (draw.Value is { Confused: int reused } current && current.Card == __instance.Card)
                __result = reused;
            else if (__instance.TestEnergyCostOverride >= 0)
                __result = __instance.TestEnergyCostOverride;
            else
            {
                var scope = CombatStateScope.Require((CombatState)__instance.Card.Owner.Creature.CombatState!);
                __result = scope.Random.NextIntInclusive(scope.For("Slither cost"), 0, 3);
            }
            return false;
        }
    }
}
