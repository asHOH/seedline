using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCombatCosts()
    {
        await CheckCostPolicy();
        var run = StartGoldRun();
        var player = run.Players[0];
        var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
        combat.AddPlayer(player);
        combat.Encounter!.GenerateMonstersWithSlots(run);
        foreach (var (monster, slot) in combat.Encounter.MonstersWithSlots)
            combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
        CombatManager.Instance.SetUpCombat(combat);
        var scope = CombatStateScope.Require(combat);
        var hand = player.PlayerCombatState!.Hand;
        var confused = (ConfusedPower)ModelDb.Power<ConfusedPower>().ToMutable();
        AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(confused, player.Creature);
        player.Creature.ApplyPowerInternal(confused);
        var context = new ThrowingPlayerChoiceContext();
        var original = run.Rng.CombatEnergyCosts.ToSerializable();
        var kick = combat.CreateCard<KinglyKick>(player);
        CardCmd.Enchant<Slither>(kick, 1);
        hand.AddInternal(kick);
        var address = scope.For("Confused cost", "others");
        var expected = scope.Random.Copy().NextIntInclusive(address, 0, 3);
        await Hook.AfterCardDrawn(combat, context, kick, false);
        Assert(kick.EnergyCost.GetWithModifiers(CostModifiers.Local) == expected && scope.Random.Position(address) == 1 &&
            scope.Random.Position(scope.For("Slither cost")) == 0,
            "Slither reapplies the same draw's Confused cost after Kingly Kick's discount without consuming its own sequence");
        var x = combat.CreateCard<Whirlwind>(player);
        hand.AddInternal(x);
        await Hook.AfterCardDrawn(combat, context, x, false);
        Assert(x.EnergyCost.CostsX && scope.Random.Position(address) == 1, "Confused X cards preserve cost state and random positions");
        var bash = combat.CreateCard<Bash>(player);
        hand.AddInternal(bash);
        await Hook.AfterCardDrawn(combat, context, bash, false);
        Assert(CombatCosts.Category(bash) == "other Basic 1" && scope.Random.Position(scope.For("Confused cost", "other Basic 1")) == 1 &&
            scope.Random.Position(address) == 1, "starter Basic identities partition Confused costs independently of other cards");
        var strike = player.PlayerCombatState.DrawPile.Cards.First(c => c.Tags.Contains(CardTag.Strike));
        strike.RemoveFromCurrentPile();
        hand.AddInternal(strike);
        await Hook.AfterCardDrawn(combat, context, strike, false);
        Assert(scope.Random.Position(scope.For("Confused cost", "starter Strikes")) == 1,
            "starter Strikes retain a separate cost category");
        player.Creature.RemoveAllPowersInternalExcept();
        await Hook.AfterCardDrawn(combat, context, kick, false);
        Assert(scope.Random.Position(scope.For("Slither cost")) == 1 && scope.Random.Position(address) == 1,
            "a later Slither draw without Confused uses its own sequence instead of a stale previous roll");
        kick.RemoveFromCurrentPile();
        player.PlayerCombatState.DiscardPile.AddInternal(kick);
        await Hook.AfterCardDrawn(combat, context, kick, false);
        Assert(scope.Random.Position(scope.For("Slither cost")) == 1 && original.Equals(run.Rng.CombatEnergyCosts.ToSerializable()),
            "Slither draws outside the hand consume zero and random costs preserve vanilla energy RNG");
        RunManager.Instance.CleanUp();
    }
}
