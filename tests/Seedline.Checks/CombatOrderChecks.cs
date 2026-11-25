using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCombatOrdering()
    {
        var run = StartGoldRun();
        var player = run.Players[0];
        var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
        combat.AddPlayer(player);
        CombatManager.Instance.SetUpCombat(combat);
        T Card<T>() where T : CardModel
        {
            var card = combat.CreateCard<T>(player);
            player.PlayerCombatState!.Hand.AddInternal(card);
            return card;
        }
        var bash = Card<Bash>();
        var plainSlimed = Card<Slimed>();
        var once = Card<Slimed>();
        var twice = Card<Slimed>();
        once.BaseReplayCount = 1;
        twice.BaseReplayCount = 2;
        var dazed = Card<Dazed>();
        dazed.BaseReplayCount = 3;
        var escape = Card<FranticEscape>();
        var replayedEscape = Card<FranticEscape>();
        replayedEscape.BaseReplayCount = 2;
        CardModel[] statusOrder = [plainSlimed, Card<Sadness>(), dazed, escape, replayedEscape, once, twice];
        Assert(statusOrder.Zip(statusOrder.Skip(1)).All(p => CardOrder.Combat.Compare(p.First, p.Second) < 0) &&
            !CombatRouting.Equivalent(once, twice) && !CombatRouting.Equivalent(escape, replayedEscape),
            "Status identity precedes replay count, with replayed Slimed after every Frantic Escape");
        foreach (var card in new CardModel[] { Card<Debris>(), Card<SporeMind>(), Card<Dowsing>() })
        {
            var same = combat.CreateCard(card.CanonicalInstance, player);
            player.PlayerCombatState!.Hand.AddInternal(same);
            card.BaseReplayCount = 2;
            Assert(CardOrder.Combat.Compare(card, same) > 0 && !CombatRouting.Equivalent(card, same),
                $"{card.Id.Entry} ranks higher with more replay and keeps a separate routing class");
        }
        var other = Card<Bash>();
        Assert(CardOrder.CombatEquivalent(bash, other), "fresh combat copies have equal gameplay ordering properties");
        var shot = Card<TheShot>();
        var otherShot = Card<TheShot>();
        Assert(CardOrder.CombatEquivalent(shot, otherShot), "The Shot has no carried damage state");
        CardCmd.Upgrade(otherShot);
        Assert(!CardOrder.CombatEquivalent(shot, otherShot), "The Shot's upgrade still distinguishes combat copies");
        bash.EnergyCost.AddUntilPlayed(-1);
        Assert(CardOrder.Combat.Compare(bash, other) < 0, "lasting discounts participate in primary energy-cost ranking");
        other.EnergyCost.SetThisTurnOrUntilPlayed(0);
        Assert(CardOrder.Combat.Compare(bash, other) < 0, "turn-only discounts leave primary lasting-cost order intact");
        var frozen = CardOrder.CaptureCombat(bash);
        bash.EnergyCost.SetThisCombat(3);
        Assert(frozen.CompareTo(CardOrder.CaptureCombat(bash)) < 0, "combat ordering snapshots freeze costs before later changes");

        var historyA = Card<Bash>();
        var historyB = Card<Bash>();
        historyA.EnergyCost.AddThisCombat(-1);
        historyA.EnergyCost.AddThisCombat(-1);
        historyB.EnergyCost.AddThisCombat(-2);
        Assert(!CardOrder.CombatEquivalent(historyA, historyB), "stored modifier lists preserve history distinctions at equal final cost");
        var turnA = Card<Bash>();
        var turnB = Card<Bash>();
        turnA.EnergyCost.SetThisTurnOrUntilPlayed(0);
        turnB.EnergyCost.SetThisTurnOrUntilPlayed(1);
        Assert(CardOrder.Combat.Compare(turnA, turnB) < 0, "matching turn-effect presence compares resulting temporary costs");
        var retained = Card<Bash>();
        var sly = Card<Bash>();
        retained.GiveSingleTurnRetain();
        sly.GiveSingleTurnSly();
        Assert(CardOrder.Combat.Compare(retained, sly) < 0 && !CardOrder.CombatEquivalent(retained, Card<Bash>()),
            "temporary Retain and Sly use the specified copy-state ordering");
        var slitherA = Card<Bash>();
        var slitherB = Card<Bash>();
        CardCmd.Enchant<Slither>(slitherA, 1);
        CardCmd.Enchant<Slither>(slitherB, 1);
        slitherA.EnergyCost.SetThisCombat(0);
        slitherB.EnergyCost.SetThisCombat(3);
        Assert(CardOrder.Combat.Compare(slitherA, slitherB) < 0, "Slither keeps its fixed primary tier and compares lasting rolled cost later");
        var ga = Card<GeneticAlgorithm>();
        var gaOther = Card<GeneticAlgorithm>();
        ga.CurrentBlock += 9;
        Assert(CardOrder.Combat.Compare(ga, gaOther) > 0, "combat ordering includes carried Block gains");
        var show = Card<ShowOff>();
        var showOther = Card<ShowOff>();
        show.CurrentAim += 3;
        Assert(CardOrder.Combat.Compare(show, showOther) > 0, "combat ordering includes carried Aim gains");
        var rampage = Card<Rampage>();
        var rampageOther = Card<Rampage>();
        AccessTools.Property(typeof(Rampage), "ExtraDamageFromPlays").SetValue(rampage, 6m);
        Assert(CardOrder.Combat.Compare(rampage, rampageOther) > 0, "combat ordering includes card-owned accumulated damage");
        var permanent = player.PlayerCombatState!.DrawPile.Cards.First(c => c is Bash);
        var combatOnly = Card<Bash>();
        Assert(CardOrder.Combat.Compare(permanent, combatOnly) > 0 && CardOrder.CombatEquivalent(permanent, combatOnly),
            "origin breaks ordering ties but is excluded from combat equivalence");
        var powers = new List<PowerModel>();
        T Power<T>() where T : PowerModel
        {
            var power = (T)ModelDb.Power<T>().ToMutable();
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(power, player.Creature);
            player.Creature.ApplyPowerInternal(power);
            powers.Add(power);
            return power;
        }
        var skill = Card<DefendIronclad>();
        var beforeCharges = CardOrder.CaptureCombat(skill);
        Power<FreeSkillPower>();
        Power<VoidFormPower>();
        Assert(CardOrder.CaptureCombat(skill).CompareTo(beforeCharges) == 0,
            "shared consumable free-card charges leave processing rank unchanged");
        Power<CorruptionPower>();
        Assert(CardOrder.CaptureCombat(skill).CompareTo(beforeCharges) < 0,
            "persistent global Corruption cost contributes to lasting rank");
        var hexed = Card<Bash>();
        var beforeHex = CardOrder.CaptureCombat(hexed);
        hexed.AfflictInternal(ModelDb.Affliction<Hexed>().ToMutable(), 1);
        Power<HexPower>();
        Assert(CardOrder.CaptureCombat(hexed).CompareTo(beforeHex) == 0,
            "Hex-derived Ethereal is excluded from combat ordering");
        Power<PhantomBladesPower>();
        var shiv = Card<Shiv>();
        var beforeRetain = CardOrder.CaptureCombat(shiv);
        shiv.GiveSingleTurnRetain();
        Assert(CardOrder.CaptureCombat(shiv).CompareTo(beforeRetain) == 0,
            "Phantom Blades' Retain masks a Shiv's temporary Retain flag");
        foreach (var power in powers)
            player.Creature.RemovePowerInternal(power);

        var restoredUpgrade = Card<Bash>();
        var ordinary = Card<Bash>();
        CardCmd.Upgrade(restoredUpgrade);
        var dampen = Power<DampenPower>();
        await dampen.AfterApplied(null, null);
        Assert(!restoredUpgrade.IsUpgraded && CardOrder.Combat.Compare(restoredUpgrade, ordinary) > 0 &&
            !CardOrder.CombatEquivalent(restoredUpgrade, ordinary),
            "Dampen restoration distinguishes temporarily downgraded cards from ordinary unupgraded copies");
        await dampen.AfterRemoved(player.Creature);
        player.Creature.RemovePowerInternal(dampen);
        Assert(restoredUpgrade.IsUpgraded, "Dampen ordering inspection preserves normal upgrade restoration");

        var tangledCard = Card<Bash>();
        var untangled = Card<Bash>();
        tangledCard.AfflictInternal(ModelDb.Affliction<Entangled>().ToMutable(), 1);
        var tangled = Power<TangledPower>();
        tangled.SetAmount(2);
        untangled.EnergyCost.SetThisTurnOrUntilPlayed(3);
        Assert(CardOrder.Combat.Compare(tangledCard, untangled) > 0,
            "Entangled cost is compared with other turn effects after equal lasting costs");
        tangledCard.EnergyCost.SetThisTurnOrUntilPlayed(0);
        Assert(CardOrder.Combat.Compare(tangledCard, untangled) < 0,
            "turn-only Tangled applies after local costs without changing primary energy rank");
        player.Creature.RemovePowerInternal(tangled);

        var returning = Card<Bolas>();
        var idle = Card<Bolas>();
        void Played(CardModel card) => CombatManager.Instance.History.CardPlayFinished(combat, new()
        {
            Card = card,
            Player = player,
            Target = null,
            ResultPile = PileType.Discard,
            Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
            IsAutoPlay = false,
            PlayIndex = 0,
            PlayCount = 1
        });
        var coda = Card<Coda>();
        var beforeMelody = CardOrder.CaptureCombat(coda);
        var cho = Card<ChoLimbs>();
        var batch = new CardOrder.CombatRankBatch();
        foreach (var candidate in new CardModel[] { coda, cho, combat.CreateCard<Coda>(player) })
            Assert(batch.Listeners(combat, candidate).SequenceEqual(combat.IterateHookListeners().Where(m => m is not CardModel || m == candidate)),
                "only the admitted copy's own modifier remains in game listener order");
        Played(cho);
        Assert(CardOrder.CaptureCombat(coda).CompareTo(beforeMelody) != 0,
            "Coda's own cost hook still affects its turn rank after playing a melody");
        Played(returning);
        foreach (var candidate in new[] { permanent, retained, returning })
            Assert(batch.ShuffleRank(candidate, combat).CompareTo(batch.ShuffleRank(candidate)) == 0,
                "known combat pile and discovered pile produce the same origin, temporary effects, and play history rank");
        combat.CurrentSide = CombatSide.Enemy;
        Assert(CardOrder.Combat.Compare(returning, idle) > 0, "pending hand-draw return survives the enemy side");
        player.PlayerCombatState!.IncrementTurnNumber();
        combat.CurrentSide = CombatSide.Player;
        combat.RoundNumber++;
        Assert(CardOrder.Combat.Compare(returning, idle) > 0, "last-turn play remains pending before the next hand-draw hook");
        await returning.BeforeHandDraw(player, new ThrowingPlayerChoiceContext(), combat);
        Assert(CardOrder.CombatEquivalent(returning, idle), "resolved hand-draw return no longer counts as a future pending return");
        var routingScope = CombatStateScope.Require(combat);
        var returnRoute = routingScope.For("combat routing", "return check", returning.Id.Entry);
        routingScope.Selection.Get(routingScope.Seed, returnRoute, CombatCards.Info(returning).Key).Remaining = 0.25;
        routingScope.Selection.Get(routingScope.Seed, returnRoute, CombatCards.Info(idle).Key).Remaining = 1;
        new CombatRouting.Choice([returning, idle], "return check").Commit([idle]);
        Assert(CardOrder.CombatEquivalent(returning, idle), "routing transfers resolved return history without resurrecting a pending draw");
        Played(returning);
        Assert(!CardOrder.CombatEquivalent(returning, idle), "replaying a returned card makes the following hand-draw return pending again");

        var all = player.UnlockState.CardPools.SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(c => c.Rarity != CardRarity.None).Select(c => combat.CreateCard(c, player)).ToArray();
        var ordered = all.OrderBy(c => c, CardOrder.Combat).ToArray();
        Assert(ordered.Length > 500 && ordered.Zip(ordered.Skip(1)).All(pair => CardOrder.Combat.Compare(pair.First, pair.Second) <= 0),
            "full unlocked card pools admit a stable combat order without missing property accessors");
        RunManager.Instance.CleanUp();
    }
}
