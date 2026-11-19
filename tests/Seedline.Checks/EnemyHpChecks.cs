using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private sealed class EnemyHpOracle(string seed, RandomAddress combatAddress)
    {
        internal long Ordinal;
        internal RandomAddress Address { get; } = RandomAddress.For("enemy HP").With(combatAddress.Key);
        internal int Next(int minimum, int maximum, IEnumerable<int> excluded)
        {
            if (minimum == maximum) return minimum;
            var values = Enumerable.Range(minimum, maximum - minimum + 1).ToArray();
            var eligible = values.Where(hp => !excluded.Contains(hp)).ToArray();
            if (eligible.Length == 0) eligible = values;
            var u = AddressRandom.Unit(AddressRandom.UInt64(seed, Address.At(Ordinal++)));
            return eligible[(int)Math.Floor(u * eligible.Length)];
        }
        internal int Next(MonsterModel monster, IEnumerable<int> excluded) =>
            Next(monster.MinInitialHp, monster.MaxInitialHp, excluded);
    }

    private static async Task CheckEnemyHp()
    {
        foreach (var ascension in new[] { 0, 7 })
            foreach (var seed in new[] { "HP0", "HP1", "HP30" })
            {
                var run = StartGoldRun(ascension, seed: seed);
                var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
                combat.AddPlayer(run.Players[0]);
                var scope = CombatStateScope.Prepare(combat);
                var oracle = new EnemyHpOracle(seed, scope.Address);
                var niche = run.Rng.Niche.ToSerializable();
                for (var i = 0; i < 4; i++)
                {
                    var monster = ModelDb.Monster<TwigSlimeM>().ToMutable();
                    var expected = oracle.Next(monster, combat.Enemies.Select(c => c.MaxHp));
                    var creature = combat.CreateCreature(monster, CombatSide.Enemy, null);
                    combat.AddCreature(creature);
                    Assert(creature.MaxHp == expected && creature.CurrentHp == expected && creature.MonsterMaxHpBeforeModification == expected &&
                        scope.Random.Position(oracle.Address) == oracle.Ordinal,
                        $"A{ascension}, {seed}, creation {i + 1}: HP follows the exact combat-local percentile, including singleton and exhausted exclusions");
                    // Exclusions use maximum HP, not the damaged creature's current HP.
                    creature.SetCurrentHpInternal(1);
                }
                Assert(combat.Enemies.Take(3).Select(c => c.MaxHp).Distinct().Count() == 3 && oracle.Ordinal == 4,
                    "a three-value HP range exhausts without repeats, then still consumes one full-range fallback roll");
                CombatManager.Instance.SetUpCombat(combat);
                CombatFixture.SetInProgress(true);
                foreach (var model in new MonsterModel[] { ModelDb.Monster<EyeWithTeeth>(), ModelDb.Monster<Parafright>(), ModelDb.Monster<GasBomb>() })
                {
                    var fixedHp = combat.CreateCreature(model.ToMutable(), CombatSide.Enemy, null);
                    Assert(fixedHp.MaxHp == model.MinInitialHp && fixedHp.MonsterMaxHpBeforeModification == model.MinInitialHp &&
                        scope.Random.Position(oracle.Address) == oracle.Ordinal,
                        $"{model.Id}: fixed-HP creation preserves original metadata and consumes no roll");
                }
                var egg = (ToughEgg)ModelDb.Monster<ToughEgg>().ToMutable();
                var expectedEgg = oracle.Next(egg, combat.Enemies.Select(c => c.MaxHp));
                var eggCreature = await CreatureCmd.Add(egg, combat);
                Assert(eggCreature.MaxHp == expectedEgg && scope.Random.Position(oracle.Address) == oracle.Ordinal,
                    "a live summon continues the initial lineup's exact HP sequence");
                var expectedHatch = oracle.Next(egg.HatchlingMinHp, egg.HatchlingMaxHp, []);
                await CreatureCmd.SetMaxAndCurrentHp(combat.Enemies[0], expectedHatch);
                await (Task)AccessTools.Method(typeof(ToughEgg), "HatchMove").Invoke(egg, [new[] { run.Players[0].Creature }])!;
                Assert(egg.IsHatched && eggCreature.MaxHp == expectedHatch && eggCreature.CurrentHp == expectedHatch &&
                    eggCreature.MonsterMaxHpBeforeModification == expectedEgg && scope.Random.Position(oracle.Address) == oracle.Ordinal,
                    "the actual hatch transition uses the full range despite matching teammate HP and retains the original egg HP metadata");

                var axebot = await CreatureCmd.Add<Axebot>(combat);
                var oldHp = axebot.MaxHp;
                await CreatureCmd.Kill(axebot);
                var replacement = combat.Enemies.Single(c => c.Monster is Axebot);
                Assert(replacement != axebot && replacement.MaxHp == oldHp + 10 &&
                    replacement.MonsterMaxHpBeforeModification == replacement.MaxHp && scope.Random.Position(oracle.Address) == oracle.Ordinal,
                    "actual Stock death replacement preserves its fixed HP increase without consuming a roll");
                Assert(niche.Equals(run.Rng.Niche.ToSerializable()), "initial HP, summons, hatch and death replacement leave vanilla Niche RNG untouched");

                CombatManager.Instance.Reset(true);
                var next = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
                next.AddPlayer(run.Players[0]);
                var nextScope = CombatStateScope.Begin(next);
                var nextOracle = new EnemyHpOracle(seed, nextScope.Address);
                var nextMonster = ModelDb.Monster<TwigSlimeM>().ToMutable();
                var nextHp = nextOracle.Next(nextMonster, []);
                Assert(next.CreateCreature(nextMonster, CombatSide.Enemy, null).MaxHp == nextHp &&
                    nextScope.Address != scope.Address && nextScope.Random.Position(nextOracle.Address) == 1,
                    "the next combat starts a separately addressed HP sequence at ordinal zero");
                RunManager.Instance.CleanUp();
                await CheckGremlinHp(ascension, seed);
                await CheckSegmentHp(ascension, seed);
            }
    }

    private static async Task CheckGremlinHp(int ascension, string seed)
    {
        var run = StartGoldRun(ascension, seed: seed);
        run.SetActDebug(ModelDb.Act<MegaCrit.Sts2.Core.Models.Acts.Underdocks>().ToMutable());
        var combat = new CombatState(ModelDb.Encounter<GremlinMercNormal>().ToMutable(), run);
        combat.AddPlayer(run.Players[0]);
        var scope = CombatStateScope.Begin(combat);
        var oracle = new EnemyHpOracle(seed, scope.Address);
        CombatManager.Instance.SetUpCombat(combat);
        CombatFixture.SetInProgress(true);
        var mercHp = oracle.Next(ModelDb.Monster<GremlinMerc>(), []);
        var merc = await CreatureCmd.Add<GremlinMerc>(combat);
        Assert(merc.MaxHp == mercHp, "Gremlin Merc uses the first combat HP roll");
        // Keep the dying parent in both children's HP range to exercise death-time exclusions.
        var parentHp = ModelDb.Monster<FatGremlin>().MinInitialHp;
        await CreatureCmd.SetMaxAndCurrentHp(merc, parentHp);
        var fatHp = oracle.Next(ModelDb.Monster<FatGremlin>(), [parentHp]);
        var sneakyHp = oracle.Next(ModelDb.Monster<SneakyGremlin>(), [parentHp]);
        if (seed == "HP30")
            Assert(fatHp == sneakyHp && fatHp != parentHp,
                "the Gremlin fixture distinguishes unadded Fat from an already present HP exclusion");
        var niche = run.Rng.Niche.ToSerializable();
        await CreatureCmd.Kill(merc);
        Assert(combat.Enemies.Select(c => c.Monster!.GetType()).SequenceEqual(new[] { typeof(SneakyGremlin), typeof(FatGremlin) }) &&
            combat.Enemies[0].MaxHp == sneakyHp && combat.Enemies[1].MaxHp == fatHp &&
            scope.Random.Position(oracle.Address) == oracle.Ordinal && niche.Equals(run.Rng.Niche.ToSerializable()),
            "Surprise rolls Fat before Sneaky despite reverse add order, excludes the dying parent, and does not exclude the unadded Fat");
        RunManager.Instance.CleanUp();
    }

    private static async Task CheckSegmentHp(int ascension, string seed)
    {
        var run = StartGoldRun(ascension, seed: seed);
        var encounter = ModelDb.Encounter<DecimillipedeElite>().ToMutable();
        var combat = new CombatState(encounter, run);
        combat.AddPlayer(run.Players[0]);
        var scope = CombatStateScope.Begin(combat);
        var oracle = new EnemyHpOracle(seed, scope.Address);
        encounter.GenerateMonstersWithSlots(run);
        foreach (var (monster, slot) in encounter.MonstersWithSlots)
        {
            var expected = oracle.Next(monster, combat.Enemies.Select(c => c.MaxHp));
            var creature = combat.CreateCreature(monster, CombatSide.Enemy, slot);
            combat.AddCreature(creature);
            Assert(creature.MaxHp == expected && creature.MonsterMaxHpBeforeModification == expected,
                "Decimillipede samples ordinary HP before applying its even-value adjustment");
        }
        CombatManager.Instance.SetUpCombat(combat);
        var niche = run.Rng.Niche.ToSerializable();
        foreach (var creature in combat.Enemies)
        {
            var raw = creature.MaxHp;
            var expected = raw + raw % 2;
            while (combat.Enemies.Any(c => c != creature && c.MaxHp == expected))
            {
                expected += 2;
                if (expected > creature.Monster!.MaxInitialHp) expected = creature.Monster.MinInitialHp;
            }
            await creature.Monster!.AfterAddedToRoom();
            Assert(creature.MaxHp == expected && creature.CurrentHp == expected && creature.MonsterMaxHpBeforeModification == raw &&
                scope.Random.Position(oracle.Address) == oracle.Ordinal,
                "Decimillipede's vanilla distinct-even adjustment preserves sampled metadata and consumes no extra HP roll");
        }
        Assert(combat.Enemies.All(c => c.MaxHp % 2 == 0) && combat.Enemies.Select(c => c.MaxHp).Distinct().Count() == 3 &&
            niche.Equals(run.Rng.Niche.ToSerializable()), "all Decimillipede segments retain distinct even HP without consuming Niche");
        RunManager.Instance.CleanUp();
    }
}
