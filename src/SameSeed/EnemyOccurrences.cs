using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace SameSeed;

internal sealed class EnemyOccurrence(RandomAddress address, long? summonedRat)
{
    internal RandomAddress Address { get; } = address;
    internal long? SummonedRat { get; } = summonedRat;
    internal long Branches;
}

internal static class EnemyOccurrences
{
    private sealed class SpawnBatch(Creature source)
    {
        internal Creature Source { get; } = source;
        internal long? Ordinal;
        internal long Index;
    }
    private static readonly AsyncLocal<SpawnBatch?> spawning = new();
    private static readonly ConditionalWeakTable<Creature, EnemyOccurrence> occurrences = new();
    internal static EnemyOccurrence Info(Creature enemy) => occurrences.TryGetValue(enemy, out var info)
        ? info : throw new InvalidOperationException($"Missing enemy occurrence: {enemy.Monster?.Id}.");

    [HarmonyPatch(typeof(CombatState), nameof(CombatState.CreateCreature))]
    private static class Creation
    {
        private static void Postfix(CombatState __instance, Creature __result)
        {
            if (__result.Side != CombatSide.Enemy || __instance.RunState is not RunState run || RunCheckpoints.Find(run) == null)
                return;
            var scope = CombatStateScope.Prepare(__instance);
            RandomAddress address;
            long? rat = null;
            if (spawning.Value is { } batch)
            {
                var source = Info(batch.Source).Address;
                batch.Ordinal ??= scope.Random.Advance(scope.For("enemy spawn", source.Key));
                address = source.With("spawn").At(batch.Ordinal.Value).At(batch.Index++);
                if (__result.Monster is TwoTailedRat)
                    rat = scope.Random.Advance(scope.For("summoned rat"));
            }
            else
                address = scope.For("enemy occurrence").At(0).At(scope.Random.Advance(scope.For("initial enemy")));
            occurrences.Add(__result, new(address, rat));
        }
    }

    [HarmonyPatch]
    private static class Spawns
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(MonsterModel), nameof(MonsterModel.PerformMove)),
            AccessTools.Method(typeof(InfestedPower), nameof(InfestedPower.AfterDeath)),
            AccessTools.Method(typeof(StockPower), nameof(StockPower.AfterDeath)),
            AccessTools.Method(typeof(SurprisePower), nameof(SurprisePower.AfterDeath))];

        private static void Prefix(object __instance, out SpawnBatch? __state)
        {
            __state = spawning.Value;
            spawning.Value = new(__instance is MonsterModel monster ? monster.Creature : ((PowerModel)__instance).Owner);
        }
        private static void Finalizer(SpawnBatch? __state) => spawning.Value = __state;
    }
}
