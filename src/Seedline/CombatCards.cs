using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Dread.Dominions;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace Seedline;

internal sealed class CombatCardOccurrence(string address, string key, long creationBatch)
{
    internal string Address { get; } = address;
    internal string Key { get; set; } = key;
    internal long CreationBatch { get; } = creationBatch;
}

internal static class CombatCards
{
    private const int maxControlledDeckSize = 2000;
    private static readonly ConditionalWeakTable<PlayerCombatState, object> vanillaDecks = new();
    internal static bool UseVanilla(Player? player) =>
        player?.PlayerCombatState is { } state && vanillaDecks.TryGetValue(state, out _);

    [HarmonyPatch(typeof(Player), nameof(Player.PopulateCombatState))]
    private static class DeckSize
    {
        private static void Prefix(Player __instance)
        {
            if (__instance.Deck.Cards.Count > maxControlledDeckSize)
                vanillaDecks.GetOrCreateValue(__instance.PlayerCombatState!);
        }
    }

    private sealed class Grant(string source)
    {
        internal string Source { get; } = source;
        internal long? Ordinal;
        internal long? Creation;
        internal Dictionary<CardModel, int> Indices { get; } = new();
    }

    private static readonly AsyncLocal<Grant?> grant = new();
    private static readonly ConditionalWeakTable<CardModel, CombatCardOccurrence> cards = new();
    private static readonly ConditionalWeakTable<CardModel, object> copies = new();
    internal static CombatCardOccurrence? Find(CardModel card) => cards.TryGetValue(card, out var info) ? info : null;
    internal static CombatCardOccurrence Info(CardModel card) => Find(card)
        ?? throw new InvalidOperationException($"Combat card has not entered a pile: {card.Id}.");

    internal static void Reassociate(IReadOnlyList<CardModel> physical, IReadOnlyList<CardModel> logical)
    {
        var records = logical.Select(Info).ToArray();
        for (var i = 0; i < physical.Count; i++)
            cards.AddOrUpdate(physical[i], records[i]);
    }

    private static void Prepare(Grant current, IEnumerable<CardModel> candidates)
    {
        foreach (var card in candidates.Where(c => Find(c) == null && !c.HasBeenRemovedFromState && !c.Owner.Creature.IsDead)
            .OrderBy(c => c, CardOrder.Combat))
            if (!current.Indices.ContainsKey(card))
                current.Indices.Add(card, current.Indices.Count);
    }

    private static CombatCardOccurrence CreateOccurrence(CardModel card)
    {
        var scope = CombatStateScope.Require((CombatState)card.Owner.Creature.CombatState!);
        var occurrence = CardOccurrences.Find(card);
        if (card.DeckVersion != null && !copies.TryGetValue(card, out _))
        {
            occurrence ??= CardOccurrences.Info(card.DeckVersion);
            return new(occurrence.Address, occurrence.Key, -1);
        }
        var current = grant.Value ?? new Grant("direct grant");
        Prepare(current, [card]);
        current.Creation ??= scope.Random.Advance(scope.For("card creation batch"));
        string address;
        if (occurrence != null && !copies.TryGetValue(card, out _))
            address = occurrence.Address;
        else if (card.Rarity == CardRarity.Token && !copies.TryGetValue(card, out _))
        {
            var counter = scope.For("token occurrence", card.Id.Entry);
            address = counter.At(scope.Random.Advance(counter)).Key;
        }
        else
        {
            var counter = scope.For("card grant", current.Source);
            current.Ordinal ??= scope.Random.Advance(counter);
            address = counter.At(current.Ordinal.Value).At(current.Indices[card]).Key;
        }
        CardOccurrences.Create(card, address);
        copies.Remove(card);
        return new(address, address, current.Creation.Value);
    }

    [HarmonyPatch(typeof(CombatState), nameof(CombatState.CloneCard))]
    private static class Copy
    {
        private static void Postfix(CardModel mutableCard, CardModel __result)
        {
            // PopulateCombatState clones the deck in order, adding each clone to draw before the next.
            var owner = mutableCard.Owner;
            if (owner?.PlayerCombatState is { } combat)
            {
                var index = combat.DrawPile.Cards.Count;
                var deck = owner.Deck.Cards;
                if (index < deck.Count && ReferenceEquals(deck[index], mutableCard))
                    return;
            }
            if (mutableCard.Pile?.Type != PileType.Deck)
                copies.Add(__result, new());
        }
    }

    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    private static class Entered
    {
        private static void Prefix(CardPile __instance, CardModel card, out bool __state) =>
            __state = __instance.Type == PileType.Hand && Find(card) == null;

        private static void Postfix(CardPile __instance, CardModel card, bool __state)
        {
            if (!__instance.IsCombatPile || card.Owner.Creature.CombatState is not CombatState combat)
                return;
            var occurrence = cards.GetValue(card, CreateOccurrence);
            if (__instance.Type == PileType.Hand)
                HandEntries.Record(combat, card, occurrence, firstAdmission: __state);
        }
    }

    [HarmonyPatch]
    private static class Batches
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardsToCombat)),
            AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.Add),
                [typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool), typeof(bool)])];
        private static void Prefix(IEnumerable<CardModel> cards, object[] __args, out Grant? __state)
        {
            __state = grant.Value;
            if (__args[1] is CardPile { IsCombatPile: false })
                return;
            var current = grant.Value ??= new("direct grant");
            Prepare(current, cards);
        }
        private static void Finalizer(Grant? __state) => grant.Value = __state;
    }

    [HarmonyPatch]
    private static class Sources
    {
        private static readonly Type[] fixedSources =
        [typeof(Anger), typeof(AdaptiveStrike), typeof(DualWield), typeof(HeirloomHammer), typeof(Undeath),
            typeof(NightmarePower), typeof(JugglingPower), typeof(MythicFormPower), typeof(EntropyPower),
            typeof(BurningSticks), typeof(MusicBox), typeof(HistoryCourse),
            typeof(Begone), typeof(BoostAway), typeof(BrightenUp), typeof(CollisionCourse), typeof(Compact),
            typeof(CrashLanding), typeof(FightThrough), typeof(Florescence), typeof(Guards), typeof(GunkUp),
            typeof(Jest), typeof(MightyShield), typeof(MightyWeapon), typeof(MourningBlow), typeof(Overclock),
            typeof(PrimalForce), typeof(PunchOfJoy), typeof(Rummage), typeof(SelfDeprecate), typeof(SorrowfulStrike),
            typeof(Turbo), typeof(Vitriol), typeof(EcstasyPower), typeof(EnnuiPower), typeof(SentryModePower),
            typeof(AeonglassDominionPower), typeof(PersonalHivePower), typeof(BiiigHug), typeof(BizarreLamp),
            typeof(BlessedAntler), typeof(FuneraryMask), typeof(RadiantPearl), typeof(TeaOfDiscourtesy), typeof(Byrdine),
            typeof(Noisebot), typeof(SoulFysh), typeof(TheInsatiable), typeof(Aeonglass), typeof(Myte), typeof(MechaKnight),
            typeof(HauntedShip), typeof(EyeWithTeeth), typeof(LeafSlimeS), typeof(LeafSlimeM), typeof(Wriggler),
            typeof(Vantom), typeof(TwigSlimeM), typeof(TestSubject), typeof(SlimedBerserker), typeof(PhrogParasite),
            typeof(Chomper), typeof(SoulFyshDominion), typeof(PainfulStabsPower), typeof(WitheringPresencePower), typeof(TheSun)];

        private static IEnumerable<MethodBase> TargetMethods() => CombatGeneration.Sources.TargetMethods()
            .Concat(PotionEffects.Sources.TargetMethods()).Concat(
            fixedSources.SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(m => m.GetCustomAttribute<AsyncStateMachineAttribute>() != null)).Distinct();
        private static void Prefix(AbstractModel __instance, out Grant? __state)
        {
            __state = grant.Value;
            grant.Value = new(__instance.Id.Entry);
        }
        private static void Finalizer(Grant? __state) => grant.Value = __state;
    }
}
