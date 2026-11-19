using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckEnemyAi()
    {
        string? expected = null;
        for (var pass = 0; pass < 2; pass++)
        {
            var run = StartGoldRun();
            var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
            combat.AddPlayer(run.Players[0]);
            Creature Create<T>() where T : MonsterModel
            {
                var creature = combat.CreateCreature(ModelDb.Monster<T>().ToMutable(), CombatSide.Enemy, null);
                combat.AddCreature(creature);
                return creature;
            }
            var twig = Create<TwigSlimeM>();
            var exo = Create<Exoskeleton>();
            var leaf = Create<LeafSlimeS>();
            var outer = Create<Inklet>();
            var middle = Create<Inklet>();
            ((Inklet)middle.Monster!).MiddleInklet = true;
            CombatManager.Instance.SetUpCombat(combat);
            var scope = CombatStateScope.Require(combat);
            var vanilla = run.Rng.MonsterAi.ToSerializable();
            var branch = new RandomBranchState("CHECK");
            var a = new MoveState("A", _ => Task.CompletedTask);
            var b = new MoveState("B", _ => Task.CompletedTask);
            branch.AddBranch(a, MoveRepeatType.CanRepeatForever, 1);
            branch.AddBranch(b, MoveRepeatType.CanRepeatForever, 3);
            var first = branch.GetNextState(twig, run.Rng.MonsterAi);
            if (pass == 1)
                for (var i = 0; i < 7; i++)
                    branch.GetNextState(exo, run.Rng.MonsterAi);
            var next = branch.GetNextState(twig, run.Rng.MonsterAi);
            expected ??= first + next;
            Assert(first + next == expected && EnemyOccurrences.Info(twig).Branches == 2,
                "enemy identity partitions preserve random branches across unrelated effective decisions");
            var forced = new RandomBranchState("FORCED");
            forced.AddBranch(a, MoveRepeatType.CanRepeatForever, 0);
            forced.AddBranch(b, MoveRepeatType.CanRepeatForever, 1);
            var exoAddress = scope.For("enemy intent", exo.Monster!.Id.Entry);
            var prior = scope.Random.Position(exoAddress);
            Assert(forced.GetNextState(exo, run.Rng.MonsterAi) == "B" && scope.Random.Position(exoAddress) == prior,
                "effective-decision singletons skip RNG and select the positive-weight move after zero-weight entries");
            forced.States[1] = new() { stateId = "B", repeatType = MoveRepeatType.CanRepeatForever, weightLambda = () => 0 };
            Assert(forced.GetNextState(twig, run.Rng.MonsterAi) == "A" && EnemyOccurrences.Info(twig).Branches == 3,
                "general all-zero branches retain the first-move fallback and consume their branch opportunity");
            var leafBranch = leaf.Monster!.MoveStateMachine!.States.Values.OfType<RandomBranchState>().Single();
            var opening = leafBranch.GetNextState(leaf, run.Rng.MonsterAi);
            leaf.Monster.MoveStateMachine.StateLog.Clear();
            leaf.Monster.MoveStateMachine.StateLog.Add(leaf.Monster.MoveStateMachine.States[opening]);
            Assert(leafBranch.GetNextState(leaf, run.Rng.MonsterAi) != opening &&
                scope.Random.Position(scope.For("enemy intent", leaf.Monster.Id.Entry)) == 1,
                "Small Leaf Slime consumes one opening roll and retains forced alternation without further rolls");
            branch.GetNextState(outer, run.Rng.MonsterAi);
            branch.GetNextState(middle, run.Rng.MonsterAi);
            Assert(scope.Random.Position(scope.For("enemy intent", "INKLET").With("outer")) == 1 &&
                scope.Random.Position(scope.For("enemy intent", "INKLET").With("middle")) == 1,
                "Inklet middle and outer effective decisions have separate retained scopes");
            CombatFixture.SetInProgress(true);
            var spawning = new MoveState("TEST_SUMMON", async _ =>
            {
                await CreatureCmd.Add<TwoTailedRat>(combat, "first");
                await CreatureCmd.Add<TwoTailedRat>(combat, "second");
            });
            twig.Monster!.SetMoveImmediate(spawning, true);
            await twig.Monster.PerformMove();
            var rats = combat.Enemies.Where(c => c.Monster is TwoTailedRat).ToArray();
            var parent = EnemyOccurrences.Info(twig).Address;
            Assert(rats.Length == 2 && EnemyOccurrences.Info(rats[0]).Address == parent.With("spawn").At(0).At(0) &&
                EnemyOccurrences.Info(rats[1]).Address == parent.With("spawn").At(0).At(1) &&
                EnemyOccurrences.Info(rats[0]).SummonedRat == 0 && EnemyOccurrences.Info(rats[1]).SummonedRat == 1,
                "one summon trigger assigns parent-local creation indices and combat-wide summoned-rat ordinals");
            var ratRound = scope.For("rat intent").At(combat.RoundNumber);
            var beforeRound = scope.Random.Position(ratRound);
            branch.GetNextState(rats[0], run.Rng.MonsterAi);
            Assert(scope.Random.Position(ratRound) == beforeRound + 1,
                "rat choices after their summoned opening share the current round ordinal");
            combat.RoundNumber++;
            branch.GetNextState(rats[1], run.Rng.MonsterAi);
            Assert(scope.Random.Position(scope.For("rat intent").At(combat.RoundNumber)) == 1,
                "rat non-Summon opportunities realign at the next round");
            var ratBranch = new RandomBranchState("SUMMON");
            ratBranch.AddBranch(new MoveState("CALL_FOR_BACKUP_MOVE", _ => Task.CompletedTask), MoveRepeatType.CanRepeatForever, 1);
            ratBranch.AddBranch(a, MoveRepeatType.CanRepeatForever, 0);
            Assert(ratBranch.GetNextState(rats[0], run.Rng.MonsterAi) == "CALL_FOR_BACKUP_MOVE" &&
                scope.Random.Position(scope.For("rat summon")) == 1,
                "positive Summon eligibility consumes its separate shared decision even when forced");
            var stock = (StockPower)ModelDb.Power<StockPower>().ToMutable();
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(stock, exo);
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Amount)).SetValue(stock, 1);
            await stock.AfterDeath(new ThrowingPlayerChoiceContext(), exo, null, false, 0);
            var replacement = combat.Enemies.Last(c => c.Monster is Axebot);
            Assert(EnemyOccurrences.Info(replacement).Address == EnemyOccurrences.Info(exo).Address.With("spawn").At(0).At(0),
                "death-triggered replacement gets a fresh occurrence under its replaced parent");
            Assert(vanilla.Equals(run.Rng.MonsterAi.ToSerializable()), "covered enemy intent decisions preserve vanilla MonsterAi RNG");
            RunManager.Instance.CleanUp();
        }
        var botRun = StartGoldRun();
        var botCombat = new CombatState(ModelDb.Encounter<FabricatorNormal>().ToMutable(), botRun);
        botCombat.AddPlayer(botRun.Players[0]);
        var fabricator = (Fabricator)ModelDb.Monster<Fabricator>().ToMutable();
        var owner = botCombat.CreateCreature(fabricator, CombatSide.Enemy, "first");
        botCombat.AddCreature(owner);
        CombatManager.Instance.SetUpCombat(botCombat);
        CombatFixture.SetInProgress(true);
        fabricator.SetMoveImmediate((MoveState)fabricator.MoveStateMachine!.States["FABRICATE_MOVE"], true);
        var botVanilla = botRun.Rng.MonsterAi.ToSerializable();
        await fabricator.PerformMove();
        var bots = botCombat.Enemies.Where(c => c != owner).ToArray();
        var botScope = CombatStateScope.Require(botCombat);
        var botParent = EnemyOccurrences.Info(owner).Address;
        Assert(bots.Length == 2 && bots[0].Monster is Guardbot or Noisebot && bots[1].Monster is Zapbot or Stabbot &&
            botScope.Random.Position(botScope.For("bot generation", botParent.Key)) == 2 &&
            EnemyOccurrences.Info(bots[1]).Address == botParent.With("spawn").At(0).At(1) &&
            botVanilla.Equals(botRun.Rng.MonsterAi.ToSerializable()),
            "Fabricator's actual move shares bot identity progression across pools and one summon batch");
        RunManager.Instance.CleanUp();
    }
}
