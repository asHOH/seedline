using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
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

namespace SameSeed.Checks;

public static partial class Checks
{
    private static async Task CheckEnemyTargets()
    {
        string? expected = null;
        for (var pass = 0; pass < 2; pass++)
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
            combat.AddPlayer(player);
            for (var i = 0; i < 3; i++)
                combat.AddCreature(combat.CreateCreature(ModelDb.Monster<Axebot>().ToMutable(), CombatSide.Enemy, null));
            CombatManager.Instance.SetUpCombat(combat);
            var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
            AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);
            foreach (var enemy in combat.Enemies)
                await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000);
            var scope = CombatStateScope.Require(combat);
            var damage = scope.For("targeting", "Direct damage");
            var other = scope.For("targeting", "Other");
            var lightning = scope.For("targeting", "Lightning");
            var relics = scope.For("targeting", "Forgotten Soul / Obsidian Vice");
            var vanilla = run.Rng.CombatTargets.ToSerializable();
            var candidates = pass == 0 ? combat.Enemies.ToArray() : combat.Enemies.Reverse().ToArray();
            var first = EnemyTargets.Select(candidates, "Direct damage")!;
            if (pass == 1)
            {
                EnemyTargets.Select(candidates, "Other");
                EnemyTargets.Select(candidates, "Lightning");
            }
            var second = EnemyTargets.Select(candidates, "Direct damage")!;
            var signature = EnemyOccurrences.Info(first).Address.Key + EnemyOccurrences.Info(second).Address.Key;
            expected ??= signature;
            Assert(signature == expected, "random enemy targets ignore input order and unrelated target groups");
            Assert(EnemyTargets.Select([], "Direct damage") == null &&
                EnemyTargets.Select([candidates[0]], "Direct damage") == candidates[0] && scope.Random.Position(damage) == 2,
                "empty and forced enemy targets preserve the group selection ordinal");
            var subset = candidates.Take(2).ToArray();
            Assert(subset.Contains(EnemyTargets.Select(subset, "Direct damage")), "enemy targeting retains source eligibility");
            var context = new ThrowingPlayerChoiceContext();
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
            var flask = combat.CreateCard<BouncingFlask>(player);
            player.PlayerCombatState!.PlayPile.AddInternal(flask);
            await (Task)AccessTools.Method(typeof(BouncingFlask), "OnPlay").Invoke(flask, [context, Play(flask)])!;
            Assert(scope.Random.Position(damage) == 6 && combat.Enemies.Sum(e => e.GetPower<PoisonPower>()?.Amount ?? 0) == 9,
                "Bouncing Flask retains per-hit targets and poison while sharing direct-damage progression");
            var attack = combat.CreateCard<Anger>(player);
            player.PlayerCombatState.PlayPile.AddInternal(attack);
            await DamageCmd.Attack(0).FromCard(attack, Play(attack)).TargetingRandomOpponents(combat).WithHitCount(3).Execute(context);
            Assert(scope.Random.Position(damage) == 9, "zero-damage random attacks consume one target opportunity per hit");
            Assert(EnemyTargets.CardGroup(combat.CreateCard<KnifeTrap>(player)) == "Direct damage" &&
                EnemyTargets.CardGroup(combat.CreateCard<Judgement>(player)) == "Direct damage" &&
                EnemyTargets.CardGroup(combat.CreateCard<DeadlyPoison>(player)) == "Direct damage" &&
                EnemyTargets.CardGroup(combat.CreateCard<Death>(player)) == "Other",
                "automatic target classification includes conditional damage and immediate HP loss while separating Doom");
            await CardCmd.AutoPlay(context, combat.CreateCard<KnifeTrap>(player), null);
            Assert(scope.Random.Position(damage) == 10, "automatic Knife Trap targets use direct damage even with no Shivs");
            var beforeOther = scope.Random.Position(other);
            await CardCmd.AutoPlay(context, combat.CreateCard<Defy>(player), null);
            Assert(scope.Random.Position(other) == beforeOther + 1, "automatic debuff targets use the Other group");
            await CardCmd.AutoPlay(context, combat.CreateCard<Defy>(player), candidates[0]);
            Assert(scope.Random.Position(other) == beforeOther + 1, "automatic play preserves a supplied target without consuming selection");
            var orb = ModelDb.Orb<LightningOrb>().ToMutable();
            orb.Owner = player;
            var beforeLightning = scope.Random.Position(lightning);
            await orb.Passive(context, null);
            await orb.Evoke(context);
            await orb.Passive(context, candidates[0]);
            Assert(scope.Random.Position(lightning) == beforeLightning + 2,
                "Lightning passive and evoke share a group while supplied targets consume zero");
            var forgotten = (ForgottenSoul)ModelDb.Relic<ForgottenSoul>().ToMutable();
            var vice = (ObsidianVice)ModelDb.Relic<ObsidianVice>().ToMutable();
            player.AddRelicInternal(forgotten);
            player.AddRelicInternal(vice);
            var handCard = combat.CreateCard<Anger>(player);
            player.PlayerCombatState!.Hand.AddInternal(handCard);
            await forgotten.AfterCardExhausted(context, handCard, false);
            await vice.BeforeSideTurnEnd(context, CombatSide.Player, [player.Creature]);
            Assert(scope.Random.Position(relics) == 2, "Forgotten Soul and Obsidian Vice share their separate target sequence");
            var countdown = (CountdownPower)ModelDb.Power<CountdownPower>().ToMutable();
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(countdown, player.Creature);
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Amount)).SetValue(countdown, 1);
            await countdown.AfterSideTurnStart(CombatSide.Player, [player.Creature], combat);
            Assert(scope.Random.Position(other) == beforeOther + 2 && vanilla.Equals(run.Rng.CombatTargets.ToSerializable()),
                "Countdown shares Other targeting and covered effects preserve vanilla CombatTargets RNG");
            RunManager.Instance.CleanUp();
        }
    }
}
