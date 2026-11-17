using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Random;

namespace Seedline;

internal static class CombatPileInsertion
{
    // Defer the gap roll until the actual card and destination are available after vanilla guards.
    private const int randomGap = -2;
    private static int Defer(Rng _, int maximum) => randomGap;

    private static void Insert(CardPile pile, CardModel card, int index, bool silent)
    {
        if (index == randomGap)
        {
            var group = card switch
            {
                Soul => "Soul",
                Dazed => "Dazed",
                Soot => "Biiig Hug",
                Beckon => "Soul Fysh",
                Wither => "Aeonglass",
                FranticEscape => "The Insatiable",
                _ when CombatGeneration.IsMetamorphosis => "Metamorphosis",
                _ => null
            };
            if (group != null && pile.IsCombatPile && card.Owner.Creature.CombatState is CombatState combat)
            {
                var scope = CombatStateScope.Require(combat);
                index = (int)(scope.Random.NextUnit(scope.For("insertion", group, pile.Type.ToString())) * (pile.Cards.Count + 1));
            }
            else
                index = card.Owner.RunState.Rng.Shuffle.NextInt(pile.Cards.Count + 1);
        }
        pile.AddInternal(card, index, silent);
    }

    [HarmonyPatch]
    private static class PileAdd
    {
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.Add),
            [typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool), typeof(bool)])
            .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType, "MoveNext");
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            instructions = RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Rng) && m.Name == nameof(Rng.NextInt),
                AccessTools.Method(typeof(CombatPileInsertion), nameof(Defer)));
            return RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(CardPile) && m.Name == nameof(CardPile.AddInternal),
                AccessTools.Method(typeof(CombatPileInsertion), nameof(Insert)));
        }
    }
}
