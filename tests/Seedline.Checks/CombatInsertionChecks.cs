using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCombatInsertion()
    {
        var run = StartGoldRun();
        var player = run.Players[0];
        var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
        combat.AddPlayer(player);
        combat.Encounter!.GenerateMonstersWithSlots(run);
        foreach (var (monster, slot) in combat.Encounter.MonstersWithSlots)
            combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
        CombatManager.Instance.SetUpCombat(combat);
        var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
        AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);
        var scope = CombatStateScope.Require(combat);
        var draw = player.PlayerCombatState!.DrawPile;
        var discard = player.PlayerCombatState.DiscardPile;
        var shuffle = run.Rng.Shuffle.ToSerializable();
        foreach (var card in draw.Cards.ToArray())
            card.RemoveFromCurrentPile();
        async Task Insert<T>(CardPile pile, string group) where T : CardModel
        {
            var address = scope.For("insertion", group, pile.Type.ToString());
            var position = scope.Random.Position(address);
            var expected = (int)(scope.Random.Copy().NextUnit(address) * (pile.Cards.Count + 1));
            var card = combat.CreateCard<T>(player);
            await CardPileCmd.AddGeneratedCardToCombat(card, pile.Type, player, CardPilePosition.Random);
            Assert(pile.Cards.IndexOf(card) == expected && scope.Random.Position(address) == position + 1,
                $"{group} insertion into {pile.Type} uses the next relative gap and commits one roll");
        }
        await Insert<Soul>(draw, "Soul");
        await Insert<Dazed>(draw, "Dazed");
        await Insert<Soul>(discard, "Soul");
        await Insert<Soul>(draw, "Soul");
        await Insert<Soot>(draw, "Biiig Hug");
        await Insert<Beckon>(draw, "Soul Fysh");
        await Insert<Wither>(draw, "Aeonglass");
        await Insert<Wither>(discard, "Aeonglass");
        await Insert<FranticEscape>(draw, "The Insatiable");
        await Insert<FranticEscape>(discard, "The Insatiable");
        Assert(scope.Random.Position(scope.For("insertion", "Soul", "Draw")) == 2 &&
            scope.Random.Position(scope.For("insertion", "Soul", "Discard")) == 1,
            "insertion groups and destination piles advance independently, including an empty-pile insertion");
        var soulAddress = scope.For("insertion", "Soul", "Draw");
        var removed = combat.CreateCard<Soul>(player);
        removed.RemoveFromState();
        var failed = await CardPileCmd.Add(removed, draw, CardPilePosition.Random);
        Assert(!failed.success && scope.Random.Position(soulAddress) == 2,
            "rejected insertions preserve random state through vanilla lifecycle guards");
        await CardPileCmd.Add(combat.CreateCard<Soul>(player), draw, CardPilePosition.Top);
        Assert(scope.Random.Position(soulAddress) == 2, "fixed top insertion preserves random gap state");
        var before = draw.Cards.ToArray();
        var metamorphosis = combat.CreateCard<Metamorphosis>(player);
        await (Task)AccessTools.Method(typeof(Metamorphosis), "OnPlay").Invoke(metamorphosis,
            [new ThrowingPlayerChoiceContext(), new CardPlay
            {
                Card = metamorphosis, Player = player, Target = null, ResultPile = PileType.Exhaust,
                Resources = new() { EnergySpent = 2, EnergyValue = 2, StarsSpent = 0, StarValue = 0 },
                IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
            }])!;
        var generated = draw.Cards.Except(before).ToArray();
        Assert(generated.Length == 3 && generated.All(c => c.Type == CardType.Attack && c.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0) &&
            scope.Random.Position(scope.For("insertion", "Metamorphosis", "Draw")) == 3,
            "Metamorphosis's actual play shares its insertion group and retains generated free attacks");
        Assert(shuffle.Equals(run.Rng.Shuffle.ToSerializable()), "covered random insertions preserve vanilla shuffle RNG");
        AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, false);
        var stopped = combat.CreateCard<Soul>(player);
        await CardPileCmd.Add(stopped, draw, CardPilePosition.Random);
        Assert(stopped.Pile == null && scope.Random.Position(soulAddress) == 2,
            "inactive combat leaves insertion state unchanged");
        RunManager.Instance.CleanUp();
    }
}
