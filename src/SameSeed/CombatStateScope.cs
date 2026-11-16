using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SameSeed;

internal sealed class CombatStateScope(string seed, RandomAddress address, RandomAddress counter, long ordinal)
{
    private static readonly ConditionalWeakTable<CombatState, CombatStateScope> combats = new();
    private static readonly ConditionalWeakTable<EncounterModel, CombatStateScope> encounters = new();
    private bool committed;
    internal string Seed { get; } = seed;
    internal RandomAddress Address { get; } = address;
    internal RandomState Random { get; } = new(seed);
    internal RewardState Cards { get; } = new();
    internal SelectionState Selection { get; } = new();
    internal RandomAddress For(string purpose, params string[] group) => RandomAddress.For(purpose, group).With(Address.Key);

    internal static CombatStateScope Require(CombatState combat) => combats.TryGetValue(combat, out var scope)
        ? scope : throw new InvalidOperationException("Combat opportunity has not begun.");

    internal static CombatStateScope Begin(CombatState combat)
    {
        var scope = Prepare(combat);
        if (!scope.committed)
        {
            var random = RunCheckpoints.Require((RunState)combat.RunState).Random;
            if (random.Position(scope.Counter) != scope.Ordinal)
                throw new InvalidOperationException("Prepared combat opportunity changed before entry.");
            random.Advance(scope.Counter);
            scope.committed = true;
        }
        return scope;
    }

    private RandomAddress Counter { get; } = counter;
    private long Ordinal { get; } = ordinal;

    internal static CombatStateScope Prepare(CombatState combat) => combats.GetValue(combat, c =>
        Prepare(c.Encounter ?? throw new InvalidOperationException("Combat has no encounter."), (RunState)c.RunState));

    internal static CombatStateScope Prepare(EncounterModel encounter, RunState run) => encounters.GetValue(encounter, _ =>
    {
        var state = RunCheckpoints.Require(run);
        RandomAddress counter;
        if (run.Acts.Any(a => a.AllEncounters.Any(e => e.Id == encounter.Id)))
            counter = RandomAddress.For("combat", encounter.RoomType.ToString()).At(run.CurrentActIndex);
        else
        {
            var parent = (run.BaseRoom as EventRoom)?.ModelId ?? (run.CurrentRoom as CombatRoom)?.ParentEventId
                ?? throw new InvalidOperationException($"Missing event source for combat {encounter.Id}.");
            counter = RandomAddress.For("event combat", parent.Entry);
        }
        var ordinal = state.Random.Position(counter);
        return new(state.Seed, counter.At(ordinal).With(encounter.Id.Entry), counter, ordinal);
    });

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
