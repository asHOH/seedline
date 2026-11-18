using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace Seedline;

internal static partial class CardOrder
{
    internal static readonly IComparer<CardModel> Combat = Comparer<CardModel>.Create((a, b) => Compare(a, b, CaptureCombat));
    internal static FrozenCardOrder CaptureCombat(CardModel card) => new(Rank(card, new CombatRankBatch()), CardOccurrences.Find(card)?.Address);
    internal static bool CombatEquivalent(CardModel a, CardModel b)
    {
        var batch = new CombatRankBatch();
        return batch.RoutingRank(a).CompareTo(batch.RoutingRank(b)) == 0;
    }

    // Only the listener roster is shared. Each card's modifiers still run in game order.
    internal sealed class CombatRankBatch
    {
        private ICombatState? combat;
        private AbstractModel[] shared = [];
        private readonly Dictionary<CardModel, int> selfPositions = new();

        internal FrozenCardOrder ShuffleRank(CardModel card) => new(Rank(card, this), null);
        internal FrozenCardOrder RoutingRank(CardModel card) => new(Rank(card, this, includeOrigin: false), null);

        internal AbstractModel[] Listeners(ICombatState? state, CardModel card)
        {
            if (state == null) return [];
            if (!ReferenceEquals(state, combat))
            {
                var listeners = new List<AbstractModel>();
                selfPositions.Clear();
                foreach (var model in state.IterateHookListeners())
                {
                    // These cost hooks affect only their own copy. Keep their position among shared hooks.
                    if (model is Coda or ChoLimbs)
                        selfPositions.Add((CardModel)model, listeners.Count);
                    else if (model is not CardModel)
                        listeners.Add(model);
                }
                shared = listeners.ToArray();
                combat = state;
            }
            return selfPositions.TryGetValue(card, out var index)
                ? [.. shared.AsSpan(0, index), card, .. shared.AsSpan(index)]
                : shared;
        }
    }

    private static readonly AccessTools.FieldRef<CardEnergyCost, List<LocalCostModifier>> energyModifiers =
        AccessTools.FieldRefAccess<CardEnergyCost, List<LocalCostModifier>>("_localModifiers");
    private static readonly AccessTools.FieldRef<CardModel, List<TemporaryCardCost>> starModifiers =
        AccessTools.FieldRefAccess<CardModel, List<TemporaryCardCost>>("_temporaryStarCosts");
    private static readonly AccessTools.FieldRef<CombatHistoryEntry, Dictionary<ulong, int>> playerTurns =
        AccessTools.FieldRefAccess<CombatHistoryEntry, Dictionary<ulong, int>>("_playerTurnNumbers");
    private sealed record ReturnDraw(int Turn);
    private static readonly ConditionalWeakTable<CardModel, ReturnDraw> returnDraws = new();

    internal static void Reassociate(IReadOnlyList<CardModel> physical, IReadOnlyList<CardModel> logical)
    {
        var records = logical.Select(c => returnDraws.TryGetValue(c, out var draw) ? draw : null).ToArray();
        for (var i = 0; i < physical.Count; i++)
        {
            returnDraws.Remove(physical[i]);
            if (records[i] is { } draw)
                returnDraws.Add(physical[i], draw);
        }
    }

    private static bool Turn(LocalCostModifier modifier) => modifier.Expiration.HasFlag(LocalCostModifierExpiration.EndOfTurn);
    // Pending grants have no pile yet; frozen removed cards identify themselves as their deck version.
    private static ICombatState? ComparisonCombat(CardModel card) => card.CombatState ??
        (card.Pile == null && card.DeckVersion != card ? card.Owner?.Creature.CombatState : null);
    private static bool AffectsRankCost(AbstractModel m, bool turn) =>
        m is not (FreeAttackPower or FreeSkillPower or FreePowerPower or FreeCardPower or VeilpiercerPower or VoidFormPower or BrilliantScarf or EnsemblePower) &&
        (turn || m is not (BorrowedTimePower or TangledPower or Coda or ChoLimbs));

    private static int CombatEnergy(CardModel card, AbstractModel[] listeners, bool turn)
    {
        var value = card.EnergyCost.GetWithModifiers(CostModifiers.None);
        if (value < 0 || card.EnergyCost.CostsX || card.IsCanonical)
            return value;
        foreach (var modifier in energyModifiers(card.EnergyCost).Where(m => turn || !Turn(m)))
            value = modifier.Modify(value);
        if (value < 0)
            return 0;
        decimal cost = value;
        foreach (var listener in listeners)
            if (AffectsRankCost(listener, turn))
                listener.TryModifyEnergyCostInCombat(card, cost, out cost);
        foreach (var listener in listeners)
            if (AffectsRankCost(listener, turn))
                listener.TryModifyEnergyCostInCombatLate(card, cost, out cost);
        return Math.Max(0, (int)cost);
    }

    private static int CombatStars(CardModel card, AbstractModel[] listeners, bool turn)
    {
        if (card.IsCanonical || card.HasStarCostX)
            return card.BaseStarCost;
        decimal cost = starModifiers(card).LastOrDefault(m => turn || !m.ClearsWhenTurnEnds)?.Cost ?? card.BaseStarCost;
        if (cost < 0)
            return (int)cost;
        foreach (var listener in listeners)
            if (AffectsRankCost(listener, turn))
                listener.TryModifyStarCost(card, cost, out cost);
        return (int)cost;
    }

    private static IReadOnlySet<CardKeyword> CombatKeywords(CardModel card, AbstractModel[] listeners)
    {
        var keywords = card.GetKeywordsWithSources(KeywordSources.Local).ToHashSet();
        foreach (var listener in listeners)
            if (listener is not HexPower)
                listener.TryModifyKeywordsInCombat(card, keywords);
        return keywords;
    }

    private static readonly AccessTools.FieldRef<CardModel, bool> turnRetain = AccessTools.FieldRefAccess<CardModel, bool>("_hasSingleTurnRetain");
    private static readonly AccessTools.FieldRef<CardModel, bool> turnSly = AccessTools.FieldRefAccess<CardModel, bool>("_hasSingleTurnSly");

    private static int TemporaryKeywords(CardModel card, IReadOnlySet<CardKeyword> rankingKeywords)
    {
        var retain = turnRetain(card);
        var sly = turnSly(card);
        if (!retain && !sly)
            return 0;
        // Pending grants rank with combat hooks, but their actual keywords are still local.
        // Hex, excluded from ranking, only changes Ethereal.
        var keywords = card.CombatState == null || card.IsCanonical ? card.GetKeywordsWithSources(KeywordSources.Local) : rankingKeywords;
        return (retain && !keywords.Contains(CardKeyword.Retain) ? 1 : 0) +
            (sly && !keywords.Contains(CardKeyword.Sly) ? 2 : 0);
    }

    private static int Expiration(bool turn, bool play) => turn ? play ? 0 : 1 : play ? 2 : 3;
    private static FrozenCardOrder StarModifiers(CardModel card) => new(card.IsCanonical ? [] : starModifiers(card)
        .Select(m => (IComparable)(Expiration(m.ClearsWhenTurnEnds, m.ClearsWhenCardIsPlayed), m.Cost)).ToArray(), null);
    private static FrozenCardOrder EnergyModifiers(CardModel card) => new(card.IsCanonical ? [] : energyModifiers(card.EnergyCost)
        .Select(m => (IComparable)(Expiration(Turn(m), m.Expiration.HasFlag(LocalCostModifierExpiration.WhenPlayed)),
            m.Type == LocalCostType.Absolute ? 0 : 1, m.Amount, m.IsReduceOnly)).ToArray(), null);

    private static int Restoration(CardModel card)
    {
        if (card.IsCanonical)
            return 0;
        var power = card.Owner?.Creature.Powers.OfType<DampenPower>().FirstOrDefault();
        if (power == null)
            return 0;
        var data = AccessTools.Field(typeof(PowerModel), "_internalData").GetValue(power)!;
        var levels = (Dictionary<CardModel, int>)AccessTools.Field(data.GetType(), "downgradedCardsToOldUpgradeLevels").GetValue(data)!;
        return levels.GetValueOrDefault(card);
    }

    private static bool PendingUse(CardModel card)
    {
        if (card.CombatState == null)
            return card is Fetch;
        var plays = CombatManager.Instance.History.CardPlaysFinished.Where(e => e.CardPlay.Card == card);
        return card switch
        {
            Fetch => !plays.Any(e => e.HappenedThisTurn(card.CombatState)),
            Bolas or ThrummingHatchet => plays.Any(e => playerTurns(e).GetValueOrDefault(card.Owner.NetId) == card.Owner.PlayerCombatState!.TurnNumber ||
                (e.HappenedLastPlayerTurn(card.Owner) && (!returnDraws.TryGetValue(card, out var draw) || draw.Turn != card.Owner.PlayerCombatState!.TurnNumber))),
            _ => false
        };
    }

    [HarmonyPatch]
    private static class ReturnDrawn
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(Bolas), nameof(Bolas.BeforeHandDraw)), AccessTools.Method(typeof(ThrummingHatchet), nameof(ThrummingHatchet.BeforeHandDraw))];
        private static void Prefix(CardModel __instance, MegaCrit.Sts2.Core.Entities.Players.Player player)
        {
            if (player == __instance.Owner)
                returnDraws.AddOrUpdate(__instance, new(player.PlayerCombatState!.TurnNumber));
        }
    }

    private static decimal CarriedValue(CardModel card) => card switch
    {
        GeneticAlgorithm c => c.CurrentBlock,
        TheScythe c => c.CurrentDamage,
        ShowOff c => c.CurrentAim,
        SovereignBlade => PrivateValue(card, "CurrentDamage"),
        TheShot => PrivateValue(card, "CardsExhaustedByFlorescence") * card.DynamicVars.ExtraDamage.BaseValue,
        Rampage => PrivateValue(card, "ExtraDamageFromPlays"),
        Claw => PrivateValue(card, "ExtraDamageFromClawPlays"),
        Maul => PrivateValue(card, "ExtraDamageFromMaulPlays"),
        KinglyPunch or Thrash => PrivateValue(card, "ExtraDamage"),
        _ => 0
    };
    private static decimal PrivateValue(CardModel card, string property) =>
        Convert.ToDecimal(AccessTools.Property(card.GetType(), property).GetValue(card));
}
