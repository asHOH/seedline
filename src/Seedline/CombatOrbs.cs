using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;

namespace Seedline;

internal static class CombatOrbs
{
    private static readonly ConditionalWeakTable<OrbModel, CombatStateScope> pending = new();

    private static OrbModel Choose(Rng _)
    {
        var scope = CombatStateScope.Require(CombatManager.Instance.DebugOnlyGetState()!);
        var address = scope.For("orb type");
        OrbModel[] pool = [ModelDb.Orb<LightningOrb>(), ModelDb.Orb<FrostOrb>(), ModelDb.Orb<DarkOrb>(),
            ModelDb.Orb<PlasmaOrb>(), ModelDb.Orb<GlassOrb>()];
        return pool[AddressRandom.IntInclusive(scope.Seed, address.At(scope.Random.Position(address)), 0, pool.Length - 1)];
    }

    private static Task Channel(PlayerChoiceContext context, OrbModel orb, Player player)
    {
        pending.Add(orb, CombatStateScope.Require((CombatState)player.Creature.CombatState!));
        return OrbCmd.Channel(context, orb, player);
    }

    [HarmonyPatch]
    private static class Sources
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [RewardPatches.AsyncMethod(typeof(Chaos), "OnPlay"), RewardPatches.AsyncMethod(typeof(TheMagician), "OnPlay"),
            RewardPatches.AsyncMethod(typeof(TrashToTreasurePower), nameof(TrashToTreasurePower.AfterCardGeneratedForCombat))];

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            instructions = RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(OrbModel) && m.Name == nameof(OrbModel.GetRandomOrb),
                AccessTools.Method(typeof(CombatOrbs), nameof(Choose)));
            return RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(OrbCmd) && m.Name == nameof(OrbCmd.Channel),
                AccessTools.Method(typeof(CombatOrbs), nameof(Channel)));
        }
    }

    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.OrbChanneled))]
    private static class Commit
    {
        private static void Prefix(OrbModel orb)
        {
            // Commit before channel hooks can generate the next random orb.
            if (pending.TryGetValue(orb, out var scope))
            {
                scope.Random.Advance(scope.For("orb type"));
                pending.Remove(orb);
            }
        }
    }
}
