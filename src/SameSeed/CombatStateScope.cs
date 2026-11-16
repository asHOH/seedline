using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SameSeed;

internal sealed class CombatStateScope(string seed, RandomAddress address)
{
    private static readonly ConditionalWeakTable<CombatState, CombatStateScope> combats = new();
    internal string Seed { get; } = seed;
    internal RandomAddress Address { get; } = address;
    internal RandomState Random { get; } = new(seed);
    internal RandomAddress For(string purpose, params string[] group) => RandomAddress.For(purpose, group).With(Address.Key);

    internal static CombatStateScope Require(CombatState combat) => combats.TryGetValue(combat, out var scope)
        ? scope : throw new InvalidOperationException("Combat opportunity has not begun.");

    internal static CombatStateScope Begin(CombatState combat)
    {
        if (combats.TryGetValue(combat, out var existing))
            return existing;
        var run = (RunState)combat.RunState;
        var state = RunCheckpoints.Require(run);
        var encounter = combat.Encounter ?? throw new InvalidOperationException("Combat has no encounter.");
        RandomAddress counter;
        if (run.Acts.Any(a => a.AllEncounters.Any(e => e.Id == encounter.Id)))
            counter = RandomAddress.For("combat", encounter.RoomType.ToString()).At(run.CurrentActIndex);
        else
        {
            var parent = (run.BaseRoom as EventRoom)?.ModelId ?? (run.CurrentRoom as CombatRoom)?.ParentEventId
                ?? throw new InvalidOperationException($"Missing event source for combat {encounter.Id}.");
            counter = RandomAddress.For("event combat", parent.Entry);
        }
        var scope = new CombatStateScope(state.Seed, counter.At(state.Random.Advance(counter)).With(encounter.Id.Entry));
        combats.Add(combat, scope);
        return scope;
    }

    [HarmonyPatch(typeof(CombatRoom), "StartCombat")]
    private static class RoomStart
    {
        private static void Prefix(CombatRoom __instance) => Begin(__instance.CombatState);
    }

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
    private static class SetUp
    {
        private static void Prefix(CombatState state) => Begin(state);
    }
}
