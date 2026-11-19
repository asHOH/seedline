using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Seedline.Checks;

public static partial class Checks
{
    private sealed class EnemyTargetOracle(CombatStateScope scope)
    {
        private readonly Dictionary<string, long> ordinals = new(StringComparer.Ordinal);
        internal Creature? Pick(IEnumerable<Creature> candidates, string group)
        {
            var eligible = candidates.ToArray();
            if (eligible.Length < 2) return eligible.FirstOrDefault();
            var ordinal = ordinals.GetValueOrDefault(group);
            ordinals[group] = ordinal + 1;
            var address = RandomAddress.For("targeting", group).With(scope.Address.Key).At(ordinal);
            return eligible.OrderBy(e => AddressRandom.UInt64(scope.Seed, address.With(EnemyOccurrences.Info(e).Address.Key)))
                .ThenBy(e => EnemyOccurrences.Info(e).Address.Key, StringComparer.Ordinal).First();
        }
        internal void Check(string label) => Assert(new[] { "Direct damage", "Other", "Lightning", "Forgotten Soul / Obsidian Vice" }
            .All(group => scope.Random.Position(RandomAddress.For("targeting", group).With(scope.Address.Key)) == ordinals.GetValueOrDefault(group)),
            $"{label}: target opportunities advance only the expected sharing group");
    }

    private static async Task CheckEnemyTargetPolicy()
    {
        foreach (var seed in new[] { "TARGET0", "TARGET1" })
        {
            var run = StartGoldRun(seed: seed);
            var player = run.Players[0];
            var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
            combat.AddPlayer(player);
            // Without room-entry hooks these Axebots have no Stock replacement power.
            for (var i = 0; i < 3; i++)
                combat.AddCreature(combat.CreateCreature(ModelDb.Monster<Axebot>().ToMutable(), CombatSide.Enemy, null));
            CombatManager.Instance.SetUpCombat(combat);
            CombatFixture.SetInProgress(true);
            foreach (var enemy in combat.Enemies) await CreatureCmd.SetMaxAndCurrentHp(enemy, 10000);
            var scope = CombatStateScope.Require(combat);
            var oracle = new EnemyTargetOracle(scope);
            var vanilla = run.Rng.CombatTargets.ToSerializable();
            var context = new ThrowingPlayerChoiceContext();
            var pcs = player.PlayerCombatState!;
            Creature Pick(string group) => oracle.Pick(combat.HittableEnemies, group)!;
            CardPlay Play(CardModel card) => new()
            {
                Card = card,
                Player = player,
                Target = null,
                ResultPile = PileType.Discard,
                Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                IsAutoPlay = false,
                PlayIndex = 0,
                PlayCount = 1
            };
            async Task Auto<T>(string group) where T : CardModel
            {
                var expected = Pick(group);
                var card = combat.CreateCard<T>(player);
                await CardCmd.AutoPlay(context, card, null);
                Assert(CombatManager.Instance.History.CardPlaysStarted.Last().CardPlay is var play && play.Card == card && play.Target == expected,
                    $"{typeof(T).Name}: automatic play uses the exact {group} priority, including conditional no-effect cases");
                oracle.Check(typeof(T).Name);
            }
            async Task DamageTo(string label, Creature[] expected, int amount, Func<Task> action)
            {
                var history = CombatManager.Instance.History.Entries.Count();
                await action();
                var hits = CombatManager.Instance.History.Entries.Skip(history).OfType<DamageReceivedEntry>().ToArray();
                Assert(hits.Select(h => h.Receiver).SequenceEqual(expected) && hits.All(h => h.Result.UnblockedDamage == amount),
                    $"{label}: exact damage receivers, hit order and amounts match addressed target priorities");
                oracle.Check(label);
            }
            Task RandomDamage(string label, string group, int amount, Func<Task> action, int rolls = 1) =>
                DamageTo(label, Enumerable.Range(0, rolls).Select(_ => Pick(group)).ToArray(), amount, action);
            T Power<T>(int amount) where T : PowerModel
            {
                var power = (T)ModelDb.Power<T>().ToMutable();
                AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(power, player.Creature);
                AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Amount)).SetValue(power, amount);
                return power;
            }
            T Relic<T>() where T : RelicModel
            {
                var relic = (T)ModelDb.Relic<T>().ToMutable();
                player.AddRelicInternal(relic);
                return relic;
            }

            await Auto<BubbleBubble>("Direct damage"); // No target is poisoned yet.
            await Auto<Judgement>("Direct damage"); // All targets exceed the execution threshold.
            await Auto<KnifeTrap>("Direct damage"); // No Shivs exist yet.
            await Auto<CaptureSpirit>("Direct damage");
            await Auto<DeadlyPoison>("Direct damage");
            await Auto<Snakebite>("Direct damage");
            await Auto<Death>("Other");
            await Auto<Defy>("Other");
            var supplied = combat.Enemies[0];
            await CardCmd.AutoPlay(context, combat.CreateCard<Defy>(player), supplied);
            Assert(CombatManager.Instance.History.CardPlaysStarted.Last().CardPlay.Target == supplied,
                "automatic debuffs retain their supplied target");
            var unplayable = combat.CreateCard<StrikeIronclad>(player);
            unplayable.AddKeyword(CardKeyword.Unplayable);
            var plays = CombatManager.Instance.History.CardPlaysStarted.Count();
            await CardCmd.AutoPlay(context, unplayable, null);
            Assert(unplayable.Pile == pcs.DiscardPile && CombatManager.Instance.History.CardPlaysStarted.Count() == plays,
                "an Unplayable auto-play is moved without selecting or playing an enemy target");
            oracle.Check("supplied and rejected auto-play targets");

            for (var i = 0; i < 2; i++) pcs.ExhaustPile.AddInternal(combat.CreateCard<Shiv>(player));
            var knifeTarget = Pick("Direct damage");
            await DamageTo("Knife Trap's nested Shivs", [knifeTarget, knifeTarget], 4,
                () => CardCmd.AutoPlay(context, combat.CreateCard<KnifeTrap>(player), null));
            // The rejected Strike is also in discard; remove it before Beat Down's eligibility snapshot.
            unplayable.RemoveFromCurrentPile();
            for (var i = 0; i < 2; i++) pcs.DiscardPile.AddInternal(combat.CreateCard<StrikeIronclad>(player));
            await RandomDamage("Beat Down", "Direct damage", 6,
                () => CardCmd.AutoPlay(context, combat.CreateCard<BeatDown>(player), null), 2);

            var poison = combat.Enemies.ToDictionary(e => e, e => e.GetPowerAmount<PoisonPower>());
            var bounces = Enumerable.Range(0, 3).Select(_ => Pick("Direct damage")).ToArray();
            await CardCmd.AutoPlay(context, combat.CreateCard<BouncingFlask>(player), null);
            Assert(combat.Enemies.All(e => e.GetPowerAmount<PoisonPower>() == poison[e] + 3 * bounces.Count(t => t == e)),
                "Bouncing Flask applies each poison bounce to its exact direct-damage target with replacement");
            oracle.Check("Bouncing Flask");

            var lightningA = ModelDb.Orb<LightningOrb>().ToMutable();
            var lightningB = ModelDb.Orb<LightningOrb>().ToMutable();
            lightningA.Owner = lightningB.Owner = player;
            await RandomDamage("Lightning passive", "Lightning", 3, () => lightningA.Passive(context, null));
            await RandomDamage("another Lightning's evoke", "Lightning", 8, async () => await lightningB.Evoke(context));
            await DamageTo("supplied Lightning", [supplied], 3, () => lightningA.Passive(context, supplied));

            var countdown = Power<CountdownPower>(7);
            var doom = combat.Enemies.ToDictionary(e => e, e => e.GetPowerAmount<DoomPower>());
            var doomTarget = Pick("Other");
            await countdown.AfterSideTurnStart(CombatSide.Player, [player.Creature], combat);
            Assert(combat.Enemies.All(e => e.GetPowerAmount<DoomPower>() == doom[e] + (e == doomTarget ? 7 : 0)),
                "Countdown continues the automatic-debuff target sequence with exact Doom placement");
            oracle.Check("Countdown");

            var attack = combat.CreateCard<StrikeIronclad>(player);
            pcs.PlayPile.AddInternal(attack);
            await RandomDamage("zero-damage random attack", "Direct damage", 0, async () =>
                await DamageCmd.Attack(0).FromCard(attack, Play(attack)).TargetingRandomOpponents(combat).WithHitCount(3).Execute(context), 3);
            var cacophony = Power<CacophonyPower>(7);
            cacophony.DynamicVars.Cards.BaseValue = 2;
            await cacophony.AfterCardDrawn(context, attack, false);
            oracle.Check("Cacophony before its trigger");
            await RandomDamage("Cacophony", "Direct damage", 7, () => cacophony.AfterCardDrawn(context, attack, false));
            Assert(cacophony.DynamicVars.Cards.IntValue == 33, "Cacophony retains its draw-count reset");
            var haunt = Power<HauntPower>(5);
            await RandomDamage("Haunt", "Direct damage", 5, () => haunt.AfterCardPlayed(context, Play(combat.CreateCard<Soul>(player))));
            var juggernaut = Power<JuggernautPower>(4);
            await juggernaut.AfterBlockGained(player.Creature, 0, ValueProp.Unpowered, null);
            oracle.Check("zero Block Juggernaut");
            await RandomDamage("Juggernaut", "Direct damage", 4, () => juggernaut.AfterBlockGained(player.Creature, 1, ValueProp.Unpowered, null));
            var serpent = Power<SerpentFormPower>(9);
            await serpent.BeforeCardPlayed(Play(attack));
            await RandomDamage("Serpent Form", "Direct damage", 9, () => serpent.AfterCardPlayed(context, Play(attack)));
            await serpent.AfterCardPlayed(context, Play(attack));
            oracle.Check("Serpent Form without a pending play");

            var tingsha = Relic<Tingsha>();
            await RandomDamage("Tingsha", "Direct damage", 3, () => tingsha.AfterCardDiscarded(context, attack));
            var shield = Relic<ParryingShield>();
            player.Creature.LoseBlockInternal(player.Creature.Block);
            await shield.AfterSideTurnEnd(context, CombatSide.Player, [player.Creature]);
            oracle.Check("Parrying Shield below threshold");
            player.Creature.GainBlockInternal(10);
            await RandomDamage("Parrying Shield", "Direct damage", 6, () => shield.AfterSideTurnEnd(context, CombatSide.Player, [player.Creature]));
            var kusarigama = Relic<Kusarigama>();
            await kusarigama.AfterCardPlayed(context, Play(attack));
            await kusarigama.AfterCardPlayed(context, Play(attack));
            oracle.Check("Kusarigama before its third Attack");
            await RandomDamage("Kusarigama", "Direct damage", 6, () => kusarigama.AfterCardPlayed(context, Play(attack)));
            var forgotten = Relic<ForgottenSoul>();
            var vice = Relic<ObsidianVice>();
            await RandomDamage("Forgotten Soul", "Forgotten Soul / Obsidian Vice", 1, () => forgotten.AfterCardExhausted(context, attack, false));
            await vice.BeforeSideTurnEnd(context, CombatSide.Player, [player.Creature]);
            oracle.Check("Obsidian Vice with an empty hand");
            pcs.Hand.AddInternal(combat.CreateCard<Anger>(player));
            pcs.Hand.AddInternal(combat.CreateCard<DefendIronclad>(player));
            await RandomDamage("Obsidian Vice", "Forgotten Soul / Obsidian Vice", 2, () => vice.BeforeSideTurnEnd(context, CombatSide.Player, [player.Creature]));
            foreach (var relic in new RelicModel[] { tingsha, shield, kusarigama, forgotten, vice }) player.RemoveRelicInternal(relic);

            // Predict the first two kills, then two forced hits on the sole survivor.
            var remaining = combat.Enemies.ToList();
            var kill1 = oracle.Pick(remaining, "Direct damage")!;
            remaining.Remove(kill1);
            var kill2 = oracle.Pick(remaining, "Direct damage")!;
            remaining.Remove(kill2);
            kill1.SetCurrentHpInternal(1);
            kill2.SetCurrentHpInternal(1);
            await DamageTo("multi-hit deaths and forced survivor", [kill1, kill2, remaining[0], remaining[0]], 1, async () =>
                await DamageCmd.Attack(1).FromCard(attack, Play(attack)).TargetingRandomOpponents(combat).WithHitCount(4).Execute(context));
            Assert(combat.Enemies.Count == 1, "multi-hit targeting reevaluates eligibility after each death");
            Assert(vanilla.Equals(run.Rng.CombatTargets.ToSerializable()), "all covered target sources preserve vanilla CombatTargets RNG");

            CombatManager.Instance.Reset(true);
            var next = CombatFixture.CreateActiveCombat(run);
            var nextScope = CombatStateScope.Require(next);
            var nextOracle = new EnemyTargetOracle(nextScope);
            var nextTarget = nextOracle.Pick(next.HittableEnemies, "Direct damage");
            await CardCmd.AutoPlay(context, next.CreateCard<StrikeIronclad>(player), null);
            Assert(nextScope.Address != scope.Address && CombatManager.Instance.History.CardPlaysStarted.Last().CardPlay.Target == nextTarget,
                "the next combat uses its own targeting address and starts at opportunity zero");
            nextOracle.Check("new combat");
            RunManager.Instance.CleanUp();
        }
    }
}
