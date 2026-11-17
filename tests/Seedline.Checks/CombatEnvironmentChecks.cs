using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCombatEnvironment()
    {
        foreach (var ascension in new[] { 0, 7 })
        {
            string? first = null;
            foreach (var floor in new[] { 1, 6 })
            {
                var run = StartGoldRun(ascension);
                for (var i = 0; i < floor; i++)
                    run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, ModelDb.Event<PunchOff>().Id);
                run.PushRoom(new EventRoom(ModelDb.Event<PunchOff>()));
                var random = RunCheckpoints.Require(run).Random;
                var counter = RandomAddress.For("event combat", "PUNCH_OFF");
                var encounter = ModelDb.Encounter<PunchOffEventEncounter>().ToMutable();
                encounter.GenerateMonstersWithSlots(run);
                var combat = new CombatState(encounter, run);
                combat.AddPlayer(run.Players[0]);
                var scope = CombatStateScope.Prepare(combat);
                var hpAddress = scope.For("enemy HP");
                var niche = run.Rng.Niche.ToSerializable();
                foreach (var (monster, slot) in encounter.MonstersWithSlots)
                    combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
                var signature = string.Join("|", combat.Enemies.Select(c =>
                    $"{c.Monster!.Id}:{((PunchConstruct)c.Monster).StartingHpReduction}:{c.MaxHp}"));
                first ??= signature;
                Assert(signature == first && niche.Equals(run.Rng.Niche.ToSerializable()),
                    $"A{ascension}: event setup and HP align across floors without consuming Niche");
                Assert(random.Position(counter) == 0, "event layout setup and HP leave the battle opportunity uncommitted");
                Assert(ReferenceEquals(scope, CombatStateScope.Begin(combat)) && random.Position(counter) == 1,
                    "entering a prepared event combat retains its HP progression and commits once");
                CombatStateScope.Begin(combat);
                Assert(random.Position(counter) == 1, "repeated entry leaves the prepared combat committed once");

                var count = scope.Random.Position(hpAddress);
                var fixedHp = combat.CreateCreature(ModelDb.Monster<Axebot>().ToMutable(), CombatSide.Enemy, null);
                Assert(scope.Random.Position(hpAddress) == count && fixedHp.MaxHp == fixedHp.Monster!.MinInitialHp,
                    "fixed enemy HP consumes no local roll");
                var egg = (ToughEgg)ModelDb.Monster<ToughEgg>().ToMutable();
                var creature = combat.CreateCreature(egg, CombatSide.Enemy, null);
                combat.AddCreature(creature);
                Assert(scope.Random.Position(hpAddress) == count + 1 && creature.MonsterMaxHpBeforeModification == creature.MaxHp,
                    "summoned variable HP follows the initial lineup and preserves original HP metadata");
                await (Task)AccessTools.Method(typeof(ToughEgg), "Hatch").Invoke(egg, null)!;
                Assert(scope.Random.Position(hpAddress) == count + 2 && creature.CurrentHp == creature.MaxHp &&
                    creature.MaxHp >= egg.HatchlingMinHp && creature.MaxHp <= egg.HatchlingMaxHp,
                    "Tough Egg hatch shares the combat HP ordinal and applies its full hatchling range");
                var preview = scope.Random.Copy();
                preview.NextUnit(hpAddress);
                Assert(CombatEnvironment.RollHp(scope, 10, 12, [10, 12]) == 11 && scope.Random.Position(hpAddress) == count + 3,
                    "filtered singleton HP consumes one sampling call");
                var u = preview.NextUnit(hpAddress);
                Assert(CombatEnvironment.RollHp(scope, 10, 12, [10, 11, 12]) == 10 + (int)(u * 3),
                    "exhausted HP exclusions restore the full ascending range");
                var finished = new CombatRoom(encounter.CanonicalInstance.ToMutable(), run) { ParentEventId = ModelDb.Event<PunchOff>().Id };
                finished.MarkPreFinished();
                finished.Encounter.GenerateMonstersWithSlots(run);
                Assert(random.Position(counter) == 1, "finished-room setup leaves future battle counters unchanged");
                RunManager.Instance.CleanUp();
            }
        }
        var declined = StartGoldRun();
        declined.PushRoom(new EventRoom(ModelDb.Event<PunchOff>()));
        ModelDb.Encounter<PunchOffEventEncounter>().ToMutable().GenerateMonstersWithSlots(declined);
        declined.PopCurrentRoom();
        var ordinary = CombatStateScope.Begin(new(ModelDb.Encounter<AxebotsNormal>().ToMutable(), declined));
        Assert(RunCheckpoints.Require(declined).Random.Position(RandomAddress.For("event combat", "PUNCH_OFF")) == 0 &&
            ordinary.Address == RandomAddress.For("combat", RoomType.Monster.ToString()).At(0).At(0).With("AXEBOTS_NORMAL"),
            "declined event layouts consume neither event battles nor ordinary combat opportunities");
        RunManager.Instance.CleanUp();
    }
}
