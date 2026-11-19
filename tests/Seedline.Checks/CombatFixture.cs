using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

internal static class CombatFixture
{
    // Standard single-player Axebots setup, without room-entry or turn-start hooks.
    internal static CombatState CreateActiveCombat(RunState run)
    {
        var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
        combat.AddPlayer(run.Players[0]);
        combat.Encounter!.GenerateMonstersWithSlots(run);
        foreach (var (monster, slot) in combat.Encounter.MonstersWithSlots)
            combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
        CombatManager.Instance.SetUpCombat(combat);
        SetInProgress(true);
        return combat;
    }

    private static object Turn => AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;

    internal static void SetInProgress(bool value)
    {
        var turn = Turn;
        AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, value);
    }

    internal static Task StartTurn() => ((Task)AccessTools.Method(typeof(CombatManager), "StartTurn")
        .Invoke(CombatManager.Instance, [Turn, null])!).WaitAsync(TimeSpan.FromSeconds(15));
}
