using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;

namespace Seedline;

internal static class EnemyAi
{
    private static readonly Func<RandomBranchState.StateWeight, Creature, float> weight =
        AccessTools.MethodDelegate<Func<RandomBranchState.StateWeight, Creature, float>>(
            AccessTools.Method(typeof(RandomBranchState), "GetStateWeight"));

    private static string Percentile(CombatStateScope scope, RandomAddress address, (string Id, float Weight)[] moves, bool consume, string enemy)
    {
        var before = scope.Random.Position(address);
        string Result(string selected)
        {
            DecisionTrace.Record("enemy-percentile", () => new
            {
                seed = scope.Seed,
                enemy,
                address = address.Key,
                before,
                after = scope.Random.Position(address),
                selected,
                moves = moves.Select(m => new { m.Id, m.Weight })
            });
            return selected;
        }
        if (!consume)
            return Result(moves.FirstOrDefault(m => m.Weight > 0).Id ?? moves[0].Id);
        var roll = (float)scope.Random.NextUnit(address) * moves.Sum(m => m.Weight);
        foreach (var move in moves)
        {
            roll -= move.Weight;
            if (roll <= 0)
                return Result(move.Id);
        }
        throw new InvalidOperationException("No eligible enemy intent.");
    }

    private static string Priorities(CombatStateScope scope, RandomAddress address, (string Id, float Weight)[] moves, string enemy)
    {
        var selected = moves.Where(m => m.Weight > 0).OrderBy(m => -Math.Log(AddressRandom.OpenUnit(
            AddressRandom.UInt64(scope.Seed, address.With(m.Id)))) / m.Weight)
            .ThenBy(m => m.Id, StringComparer.Ordinal).Select(m => m.Id).FirstOrDefault() ?? moves[0].Id;
        DecisionTrace.Record("enemy-priorities", () => new
        {
            seed = scope.Seed,
            enemy,
            address = address.Key,
            selected,
            moves = moves.Select(m => new { m.Id, m.Weight })
        });
        return selected;
    }

    [HarmonyPatch(typeof(RandomBranchState), nameof(RandomBranchState.GetNextState))]
    private static class Intent
    {
        private static bool Prefix(RandomBranchState __instance, Creature owner, ref string __result)
        {
            if (owner.CombatState is not CombatState combat || owner.Side != CombatSide.Enemy)
                return true;
            if (CharacterCompatibility.IsModdedRun && owner.Monster!.GetType().Assembly != typeof(MonsterModel).Assembly)
            {
                DecisionTrace.Record("vanilla-enemy-intent", () => new { enemy = owner.Monster.Id.Entry, reason = "modded enemy" });
                return true;
            }
            var scope = CombatStateScope.Require(combat);
            var occurrence = EnemyOccurrences.Info(owner);
            var ordinal = occurrence.Branches++;
            var moves = __instance.States.Select(s => (Id: s.stateId, Weight: weight(s, owner))).ToArray();
            var count = moves.Count(m => m.Weight > 0);
            var monster = owner.Monster!;
            if (monster is TwoTailedRat)
            {
                var summon = moves.FirstOrDefault(m => m.Id == "CALL_FOR_BACKUP_MOVE");
                var summonBefore = scope.Random.Position(scope.For("rat summon"));
                var intentCounter = scope.For("rat intent").At(combat.RoundNumber);
                var intentBefore = scope.Random.Position(intentCounter);
                if (summon.Weight > 0 && scope.Random.NextUnit(scope.For("rat summon")) < summon.Weight / moves.Sum(m => m.Weight))
                    __result = summon.Id;
                else
                {
                    var other = moves.Where(m => m.Id != "CALL_FOR_BACKUP_MOVE").ToArray();
                    var address = occurrence.SummonedRat is long opening && ordinal == 0
                        ? scope.For("rat opening").At(opening)
                        : scope.For("rat intent").At(combat.RoundNumber);
                    if (!(occurrence.SummonedRat != null && ordinal == 0))
                        address = address.At(other.Count(m => m.Weight > 0) >= 2 ? scope.Random.Advance(address) : 0);
                    __result = Priorities(scope, address, other, occurrence.Address.Key);
                }
                var selected = __result;
                DecisionTrace.Record("rat-summon-check", () => new
                {
                    seed = scope.Seed,
                    enemy = occurrence.Address.Key,
                    ordinal,
                    address = scope.For("rat summon").Key,
                    before = summonBefore,
                    after = scope.Random.Position(scope.For("rat summon")),
                    intentCounter = intentCounter.Key,
                    intentBefore,
                    intentAfter = scope.Random.Position(intentCounter),
                    moves = moves.Select(m => new { m.Id, m.Weight }),
                    selected
                });
            }
            else if (monster is Inklet or Exoskeleton or ScrollOfBiting or LeafSlimeS or DecimillipedeSegment)
            {
                var group = monster is DecimillipedeSegment ? "Decimillipede" : monster.Id.Entry;
                var address = scope.For("enemy intent", group);
                if (monster is Inklet inklet)
                    address = address.With(inklet.MiddleInklet ? "middle" : "outer");
                __result = Percentile(scope, address, moves, monster is LeafSlimeS ? ordinal == 0 : count >= 2, occurrence.Address.Key);
            }
            else
                __result = Priorities(scope, scope.For("enemy intent", monster.Id.Entry).At(ordinal), moves, occurrence.Address.Key);
            return false;
        }
    }

    [HarmonyPatch]
    private static class FabricatorBots
    {
        private static readonly AsyncLocal<Fabricator?> source = new();
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(Fabricator), "SpawnBot");
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Rng) && m.Name == nameof(Rng.NextItem),
                AccessTools.Method(typeof(FabricatorBots), nameof(Choose)));

        private static MonsterModel Choose(Rng _, IEnumerable<MonsterModel> candidates)
        {
            var fabricator = source.Value ?? throw new InvalidOperationException("Missing Fabricator source.");
            var scope = CombatStateScope.Require((CombatState)fabricator.CombatState);
            var address = scope.For("bot generation", EnemyOccurrences.Info(fabricator.Creature).Address.Key);
            var eligible = candidates.OrderBy(m => m.Id.Entry, StringComparer.Ordinal).ToArray();
            return eligible[scope.Random.NextIntInclusive(address, 0, eligible.Length - 1)];
        }

        [HarmonyPatch(typeof(Fabricator), "SpawnBot")]
        private static class Context
        {
            private static void Prefix(Fabricator __instance, out Fabricator? __state)
            {
                __state = source.Value;
                source.Value = __instance;
            }
            private static void Finalizer(Fabricator? __state) => source.Value = __state;
        }
    }
}
