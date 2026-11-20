using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class CombatEnvironment
{
    private static CombatStateScope? creating;
    private static readonly AccessTools.FieldRef<Creature, int?> originalHp =
        AccessTools.FieldRefAccess<Creature, int?>("<MonsterMaxHpBeforeModification>k__BackingField");

    internal static int RollHp(CombatStateScope scope, int minimum, int maximum, IEnumerable<int> excluded)
    {
        if (minimum == maximum)
            return minimum;
        var all = Enumerable.Range(minimum, checked(maximum - minimum + 1)).ToArray();
        var eligible = all.Except(excluded).ToArray();
        if (eligible.Length == 0)
            eligible = all;
        return eligible[(int)(scope.Random.NextUnit(scope.For("enemy HP")) * eligible.Length)];
    }

    [HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.GenerateMonstersWithSlots))]
    private static class Setup
    {
        private static void Prefix(EncounterModel __instance, IRunState runState, ref Rng ____rng)
        {
            if (runState is not RunState run || RunCheckpoints.Find(run) == null || __instance.HaveMonstersBeenGenerated)
                return;
            var scope = CombatStateScope.Prepare(__instance, run);
            if (!CharacterCompatibility.IsModdedRun || __instance.GetType().Assembly == typeof(EncounterModel).Assembly)
                ____rng = new Rng(AddressRandom.UInt64(scope.Seed, scope.For("setup")));
        }
    }

    // Creature HP is rolled before the game attaches the creature to its combat.
    [HarmonyPatch(typeof(CombatState), nameof(CombatState.CreateCreature))]
    private static class Creation
    {
        private static void Prefix(CombatState __instance, out CombatStateScope? __state)
        {
            __state = creating;
            creating = __instance.RunState is RunState run && RunCheckpoints.Find(run) != null
                ? CombatStateScope.Prepare(__instance) : null;
        }
        private static void Finalizer(CombatStateScope? __state) => creating = __state;
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.SetUniqueMonsterHpValue))]
    private static class InitialHp
    {
        private static bool Prefix(Creature __instance, IReadOnlyList<Creature> creaturesOnSide,
            ref int ____maxHp, ref int ____currentHp)
        {
            if (creating == null)
                return true;
            var monster = __instance.Monster!;
            if (CharacterCompatibility.IsModdedRun && monster.GetType().Assembly != typeof(MonsterModel).Assembly)
                return true;
            var hp = RollHp(creating, monster.MinInitialHp, monster.MaxInitialHp,
                creaturesOnSide.Select(c => c.MaxHp));
            originalHp(__instance) = ____maxHp = ____currentHp = hp;
            return false;
        }
    }

    [HarmonyPatch(typeof(ToughEgg), "Hatch")]
    private static class Hatch
    {
        private static bool Prefix(ToughEgg __instance, ref Task __result)
        {
            __result = Apply(__instance);
            return false;
        }
        private static async Task Apply(ToughEgg egg)
        {
            await CreatureCmd.TriggerAnim(egg.Creature, "Hatch", 0.5f);
            var combat = (CombatState)egg.CombatState;
            var hp = RollHp(CombatStateScope.Require(combat), egg.HatchlingMinHp, egg.HatchlingMaxHp, []);
            await CreatureCmd.SetMaxAndCurrentHp(egg.Creature,
                Creature.ScaleHpForMultiplayer(hp, combat.Encounter, combat.Players.Count, combat.RunState.CurrentActIndex));
        }
    }
}
