using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace SameSeed.Checks;

public static partial class Checks
{
    private static async Task CheckCombatOrbs()
    {
        string? expected = null;
        for (var branch = 0; branch < 2; branch++)
        {
            var player = Player.CreateForNewRun<Defect>(UnlockState.all, 1);
            var run = RunState.CreateForNewRun([player], ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(),
                [], GameMode.Standard, false, 7, "ORBCHECK");
            RunManager.Instance.SetUpNewSingleplayer(run, true);
            var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
            combat.AddPlayer(player);
            combat.Encounter!.GenerateMonstersWithSlots(run);
            foreach (var (monster, slot) in combat.Encounter.MonstersWithSlots)
                combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
            CombatManager.Instance.SetUpCombat(combat);
            var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
            AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);
            var scope = CombatStateScope.Require(combat);
            var address = scope.For("orb type");
            var queue = player.PlayerCombatState!.OrbQueue;
            var vanilla = run.Rng.CombatOrbGeneration.ToSerializable();
            async Task Play<T>() where T : CardModel
            {
                var card = combat.CreateCard<T>(player);
                await (Task)AccessTools.Method(typeof(T), "OnPlay").Invoke(card, [new ThrowingPlayerChoiceContext(),
                    new CardPlay
                    {
                        Card = card, Player = player, Target = null, ResultPile = PileType.Discard,
                        Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                        IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
                    }])!;
            }
            if (branch == 1)
            {
                queue.RemoveCapacity(queue.Capacity);
                await Play<Chaos>();
                Assert(queue.Orbs.Count == 0 && scope.Random.Position(address) == 0,
                    "failed Defect channeling leaves the next random orb available");
                queue.AddCapacity(3);
            }
            await Play<Chaos>();
            await Play<TheMagician>();
            var power = (TrashToTreasurePower)ModelDb.Power<TrashToTreasurePower>().ToMutable();
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(power, player.Creature);
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Amount)).SetValue(power, 1);
            await power.AfterCardGeneratedForCombat(combat.CreateCard<Dazed>(player), player);
            var actual = string.Join("|", queue.Orbs.Select(o => o.Id.Entry));
            expected ??= actual;
            Assert(actual == expected && queue.Orbs.Count == 5 && scope.Random.Position(address) == 5,
                $"Chaos, The Magician and Trash to Treasure share successful channel opportunities across failed attempts ({branch}: {actual}; count {queue.Orbs.Count}, position {scope.Random.Position(address)})");
            await OrbCmd.Channel<LightningOrb>(new ThrowingPlayerChoiceContext(), player);
            Assert(scope.Random.Position(address) == 5 && vanilla.Equals(run.Rng.CombatOrbGeneration.ToSerializable()),
                "fixed channels preserve random-orb state and covered sources preserve vanilla RNG");
            AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, false);
            await Play<Chaos>();
            Assert(scope.Random.Position(address) == 5, "ended combat consumes no random-orb opportunity");
            RunManager.Instance.CleanUp();
        }
        var otherRun = StartGoldRun();
        var otherPlayer = otherRun.Players[0];
        var otherCombat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), otherRun);
        otherCombat.AddPlayer(otherPlayer);
        otherCombat.Encounter!.GenerateMonstersWithSlots(otherRun);
        foreach (var (monster, slot) in otherCombat.Encounter.MonstersWithSlots)
            otherCombat.AddCreature(otherCombat.CreateCreature(monster, CombatSide.Enemy, slot));
        CombatManager.Instance.SetUpCombat(otherCombat);
        var otherTurn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
        AccessTools.Property(otherTurn.GetType(), "IsInProgress").SetValue(otherTurn, true);
        var otherPower = (TrashToTreasurePower)ModelDb.Power<TrashToTreasurePower>().ToMutable();
        AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(otherPower, otherPlayer.Creature);
        AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Amount)).SetValue(otherPower, 1);
        await otherPower.AfterCardGeneratedForCombat(otherCombat.CreateCard<Dazed>(otherPlayer), otherPlayer);
        var otherScope = CombatStateScope.Require(otherCombat);
        Assert(otherPlayer.PlayerCombatState!.OrbQueue.Capacity == 1 && otherPlayer.PlayerCombatState.OrbQueue.Orbs.Count == 1 &&
            otherScope.Random.Position(otherScope.For("orb type")) == 1,
            "characters without base orb slots retain the automatic slot and start fresh combat-local orb state");
        RunManager.Instance.CleanUp();
    }
}
