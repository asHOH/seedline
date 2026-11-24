using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static string ExpectedIntent(string seed, RandomAddress address, params (string Id, float Weight)[] moves) =>
        moves.Where(m => m.Weight > 0).OrderBy(m => -Math.Log(AddressRandom.OpenUnit(AddressRandom.UInt64(seed, address.With(m.Id)))) / m.Weight)
            .ThenBy(m => m.Id, StringComparer.Ordinal).Select(m => m.Id).FirstOrDefault() ?? moves[0].Id;

    private static void SetIntentHistory(Creature creature, params string[] moves)
    {
        var machine = creature.Monster!.MoveStateMachine!;
        machine.StateLog.Clear();
        machine.StateLog.AddRange(moves.Select(id => machine.States[id]));
    }

    private static async Task CheckIntentSampling()
    {
        foreach (var seed in new[] { "INTENT0", "INTENT1", "INTENT2" })
        {
            var run = StartGoldRun(seed: seed);
            var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
            combat.AddPlayer(run.Players[0]);
            Creature Add(MonsterModel monster)
            {
                var creature = combat.CreateCreature(monster.ToMutable(), CombatSide.Enemy, null);
                combat.AddCreature(creature);
                return creature;
            }
            var fly = Add(ModelDb.Monster<Flyconid>());
            var exos = new[] { Add(ModelDb.Monster<Exoskeleton>()), Add(ModelDb.Monster<Exoskeleton>()) };
            var scroll = Add(ModelDb.Monster<ScrollOfBiting>());
            var leaves = new[] { Add(ModelDb.Monster<LeafSlimeS>()), Add(ModelDb.Monster<LeafSlimeS>()) };
            var inks = new[] { Add(ModelDb.Monster<Inklet>()), Add(ModelDb.Monster<Inklet>()), Add(ModelDb.Monster<Inklet>()) };
            ((Inklet)inks[1].Monster!).MiddleInklet = true;
            var segments = new[] { Add(ModelDb.Monster<DecimillipedeSegmentFront>()), Add(ModelDb.Monster<DecimillipedeSegmentBack>()) };
            CombatManager.Instance.SetUpCombat(combat);
            CombatFixture.SetInProgress(true);
            var scope = CombatStateScope.Require(combat);
            var vanilla = run.Rng.MonsterAi.ToSerializable();
            var positions = new Dictionary<string, long>();
            void Percentile(Creature creature, string branch, string group, string? partition, params (string Id, float Weight)[] moves)
            {
                var address = RandomAddress.For("enemy intent", group).With(scope.Address.Key);
                if (partition != null) address = address.With(partition);
                var ordinal = positions.GetValueOrDefault(address.Key);
                var expected = moves.FirstOrDefault(m => m.Weight > 0).Id ?? moves[0].Id;
                if (moves.Count(m => m.Weight > 0) >= 2)
                {
                    var roll = (float)AddressRandom.Unit(AddressRandom.UInt64(seed, address.At(ordinal))) * moves.Sum(m => m.Weight);
                    expected = moves.First(m => (roll -= m.Weight) <= 0).Id;
                    positions[address.Key] = ++ordinal;
                }
                var state = (RandomBranchState)creature.Monster!.MoveStateMachine!.States[branch];
                Assert(state.GetNextState(creature, run.Rng.MonsterAi) == expected && scope.Random.Position(address) == ordinal,
                    $"{creature.Monster.Id}: exact percentile result and effective-decision consumption at {ordinal}");
                var evidence = DecisionRecords("enemy-percentile").Last()["data"]!;
                Assert((string?)evidence["selected"] == expected && (string?)evidence["after"] == ordinal.ToString() &&
                    evidence["moves"]!.AsArray().Count == moves.Length,
                    "enemy diagnostic records include actual effective weights, result and advancement");
            }
            foreach (var exo in exos)
            {
                SetIntentHistory(exo, "ENRAGE_MOVE");
                Percentile(exo, "RAND", "EXOSKELETON", null, ("SKITTER_MOVE", 1), ("MANDIBLES_MOVE", 1));
                SetIntentHistory(exo, "SKITTER_MOVE");
                Percentile(exo, "RAND", "EXOSKELETON", null, ("SKITTER_MOVE", 0), ("MANDIBLES_MOVE", 1));
            }
            SetIntentHistory(scroll, "CHEW");
            Percentile(scroll, "rand", "SCROLL_OF_BITING", null, ("CHOMP", 1), ("CHEW", 1));
            SetIntentHistory(scroll, "CHEW", "CHEW");
            Percentile(scroll, "rand", "SCROLL_OF_BITING", null, ("CHOMP", 1), ("CHEW", 0));
            foreach (var leaf in leaves)
            {
                SetIntentHistory(leaf);
                Percentile(leaf, "RAND", "LEAF_SLIME_S", null, ("TACKLE_MOVE", 1), ("GOOP_MOVE", 1));
                SetIntentHistory(leaf, "TACKLE_MOVE");
                Percentile(leaf, "RAND", "LEAF_SLIME_S", null, ("TACKLE_MOVE", 0), ("GOOP_MOVE", 1));
            }
            foreach (var ink in inks)
            {
                SetIntentHistory(ink, "JAB_MOVE");
                Percentile(ink, "RAND", "INKLET", ink == inks[1] ? "middle" : "outer", ("PIERCING_GAZE_MOVE", 1), ("WHIRLWIND_MOVE", 1));
            }
            await CreatureCmd.Kill(inks[1]);
            Percentile(inks[2], "RAND", "INKLET", "outer", ("PIERCING_GAZE_MOVE", 1), ("WHIRLWIND_MOVE", 1));
            foreach (var segment in segments)
            {
                SetIntentHistory(segment, "DEAD_MOVE", "REATTACH_MOVE");
                Percentile(segment, "RAND", "Decimillipede", null, ("WRITHE_MOVE", 1), ("BULK_MOVE", 1), ("CONSTRICT_MOVE", 1));
            }
            var flyBranch = (RandomBranchState)fly.Monster!.MoveStateMachine!.States["RAND"];
            var flyAddress = RandomAddress.For("enemy intent", "FLYCONID").With(scope.Address.Key);
            SetIntentHistory(fly, "SMASH_MOVE", "VULNERABLE_SPORES_MOVE", "FRAIL_SPORES_MOVE", "SMASH_MOVE");
            Assert(flyBranch.GetNextState(fly, run.Rng.MonsterAi) == "VULNERABLE_SPORES_MOVE" && EnemyOccurrences.Info(fly).Branches == 1,
                "Flyconid's actual all-zero history preserves first-listed fallback and advances its random-branch ordinal");
            SetIntentHistory(fly, "SMASH_MOVE", "FRAIL_SPORES_MOVE");
            Assert(flyBranch.GetNextState(fly, run.Rng.MonsterAi) == ExpectedIntent(seed, flyAddress.At(1),
                ("VULNERABLE_SPORES_MOVE", 1), ("SMASH_MOVE", 1)), "Flyconid cooldown eligibility stays outside the weighted-priority address");
            Assert(vanilla.Equals(run.Rng.MonsterAi.ToSerializable()), "actual enemy histories and shared sequences preserve vanilla MonsterAi RNG");
            RunManager.Instance.CleanUp();
        }
    }

    private static async Task CheckRatSampling()
    {
        var sawSummon = false;
        var sawRejectedSummon = false;
        for (var seedIndex = 0; seedIndex < 8; seedIndex++)
        {
            var seed = $"RAT{seedIndex}";
            var run = StartGoldRun(seed: seed);
            run.SetActDebug(ModelDb.Act<MegaCrit.Sts2.Core.Models.Acts.Underdocks>().ToMutable());
            var combat = new CombatState(ModelDb.Encounter<TwoTailedRatsNormal>().ToMutable(), run);
            combat.AddPlayer(run.Players[0]);
            var rats = new List<Creature>();
            foreach (var slot in new[] { "third", "fourth" })
            {
                var rat = (TwoTailedRat)ModelDb.Monster<TwoTailedRat>().ToMutable();
                rat.StarterMoveIndex = 0;
                var creature = combat.CreateCreature(rat, CombatSide.Enemy, slot);
                combat.AddCreature(creature);
                rats.Add(creature);
            }
            CombatManager.Instance.SetUpCombat(combat);
            CombatFixture.SetInProgress(true);
            var scope = CombatStateScope.Require(combat);
            var vanilla = run.Rng.MonsterAi.ToSerializable();
            var summonAddress = RandomAddress.For("rat summon").With(scope.Address.Key);
            var roundAddress = RandomAddress.For("rat intent").With(scope.Address.Key).At(combat.RoundNumber);
            long summonOrdinal = 0, roundOrdinal = 0;
            foreach (var rat in rats)
            {
                rat.Monster!.RollMove([run.Players[0].Creature]);
                SetIntentHistory(rat, "SCREECH_MOVE");
                AccessTools.Property(typeof(TwoTailedRat), "TurnsUntilSummonable").SetValue(rat.Monster, 0);
            }
            foreach (var rat in rats)
            {
                var blocked = rats.Any(other => other != rat && other.Monster!.NextMove.Id == "CALL_FOR_BACKUP_MOVE");
                var summon = false;
                if (!blocked)
                {
                    var roll = AddressRandom.Unit(AddressRandom.UInt64(seed, summonAddress.At(summonOrdinal++)));
                    summon = roll < 0.75f / (0.75f + 1f / 12f + 1f / 12f);
                    sawSummon |= summon;
                    sawRejectedSummon |= !summon;
                }
                var expected = summon ? "CALL_FOR_BACKUP_MOVE" : ExpectedIntent(seed, roundAddress.At(roundOrdinal++),
                    ("SCRATCH_MOVE", 1), ("DISEASE_BITE_MOVE", 1));
                var branch = (RandomBranchState)rat.Monster!.MoveStateMachine!.States["RAND"];
                var actual = branch.GetNextState(rat, run.Rng.MonsterAi);
                Assert(actual == expected && scope.Random.Position(summonAddress) == summonOrdinal &&
                    scope.Random.Position(roundAddress) == roundOrdinal,
                    "rats preserve exact effective Summon odds, sequential teammate blocking, and separate non-Summon progression");
                rat.Monster.SetMoveImmediate((MoveState)rat.Monster.MoveStateMachine.States[actual], true);
            }
            SetIntentHistory(rats[0], "SCREECH_MOVE", "SCRATCH_MOVE");
            AccessTools.Property(typeof(TwoTailedRat), "TurnsUntilSummonable").SetValue(rats[0].Monster, 2);
            var forced = (RandomBranchState)rats[0].Monster!.MoveStateMachine!.States["RAND"];
            Assert(forced.GetNextState(rats[0], run.Rng.MonsterAi) == "DISEASE_BITE_MOVE" &&
                scope.Random.Position(roundAddress) == roundOrdinal && scope.Random.Position(summonAddress) == summonOrdinal,
                "a cooldown-forced rat attack consumes neither the non-Summon round ordinal nor blocked Summon RNG");
            combat.RoundNumber++;
            roundAddress = RandomAddress.For("rat intent").With(scope.Address.Key).At(combat.RoundNumber);
            SetIntentHistory(rats[0], "SCREECH_MOVE");
            Assert(forced.GetNextState(rats[0], run.Rng.MonsterAi) == ExpectedIntent(seed, roundAddress.At(0),
                ("SCRATCH_MOVE", 1), ("DISEASE_BITE_MOVE", 1)) && scope.Random.Position(roundAddress) == 1,
                "rat non-Summon selection starts at ordinal zero in the next combat round");

            var spawning = new MoveState("OPENING_CHECK", async _ =>
            {
                await CreatureCmd.Add<TwoTailedRat>(combat, "first");
                await CreatureCmd.Add<TwoTailedRat>(combat, "second");
            });
            rats[0].Monster!.SetMoveImmediate(spawning, true);
            await rats[0].Monster!.PerformMove();
            var children = combat.Enemies.Where(c => !rats.Contains(c)).ToArray();
            for (var i = 0; i < children.Length; i++)
            {
                var openingAddress = RandomAddress.For("rat opening").With(scope.Address.Key).At(i);
                Assert(children[i].Monster!.NextMove.Id == ExpectedIntent(seed, openingAddress,
                    ("SCRATCH_MOVE", 1), ("DISEASE_BITE_MOVE", 1), ("SCREECH_MOVE", 1)) &&
                    EnemyOccurrences.Info(children[i]).Branches == 1 && EnemyOccurrences.Info(children[i]).SummonedRat == i,
                    "live summoned-rat openings use exact independent priorities in creation order");
            }
            Assert(children.Length == 2 && scope.Random.Position(roundAddress) == 1 &&
                scope.Random.Position(summonAddress) == summonOrdinal && vanilla.Equals(run.Rng.MonsterAi.ToSerializable()),
                "summoned openings preserve later round and Summon progress and vanilla MonsterAi RNG");
            RunManager.Instance.CleanUp();
        }
        Assert(sawSummon && sawRejectedSummon, "rat fixtures cover both successful and rejected positive-weight Summon rolls");
    }
}
