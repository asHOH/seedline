using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline;

internal static class CombatRouting
{
    internal static bool IsChoicePatch(Type type) => type == typeof(HandChoice) || type == typeof(UpgradeChoice) || type == typeof(PileChoice);
    private static readonly AsyncLocal<AbstractModel?> source = new();
    private static readonly ConditionalWeakTable<ThievingHopper, object> theftFinished = new();

    internal static bool Protected(CardModel card) => card is GeneticAlgorithm or TheScythe or ShowOff ||
        card.Enchantment is Goopy || card.CombatState is CombatState combat && combat.Enemies.Any(c =>
            c.IsAlive && c.Monster is ThievingHopper hopper && !theftFinished.TryGetValue(hopper, out _));

    private static bool Equivalent(CardModel a, CardModel b, Func<CardModel, FrozenCardOrder> rank) =>
        rank(a).CompareTo(rank(b)) == 0 &&
        (!Protected(a) && !Protected(b) || ReferenceEquals(a.DeckVersion, b.DeckVersion)) &&
        CombatReferences.PendingEquivalent(a, b);

    internal static bool Equivalent(CardModel a, CardModel b) => Equivalent(a, b, new CardOrder.CombatRankBatch().RoutingRank);

    internal sealed class Choice
    {
        private readonly CardModel[][] groups;
        private readonly string operation;
        internal Choice(IEnumerable<CardModel> eligible, string operation)
        {
            this.operation = operation;
            var classes = new List<List<CardModel>>();
            var byIdentity = new Dictionary<ModelId, List<List<CardModel>>>();
            var ranks = new Dictionary<CardModel, FrozenCardOrder>();
            var batch = new CardOrder.CombatRankBatch();
            FrozenCardOrder Rank(CardModel card)
            {
                if (!ranks.TryGetValue(card, out var rank))
                    ranks.Add(card, rank = batch.RoutingRank(card));
                return rank;
            }
            foreach (var card in eligible)
            {
                if (!byIdentity.TryGetValue(card.Id, out var sameIdentity))
                    byIdentity.Add(card.Id, sameIdentity = []);
                var group = sameIdentity.FirstOrDefault(g => Equivalent(g[0], card, Rank));
                if (group == null)
                {
                    group = [card];
                    sameIdentity.Add(group);
                    classes.Add(group);
                }
                else group.Add(card);
            }
            groups = classes.Select(g => g.ToArray()).ToArray();
        }

        internal void Commit(IReadOnlyCollection<CardModel> clicked)
        {
            if (clicked.Count == 0) return;
            var selected = clicked.ToHashSet();
            if (selected.Count != clicked.Count || !selected.IsSubsetOf(groups.SelectMany(g => g)))
                throw new ArgumentException("Routing selections must be distinct eligible cards.");
            var combat = (CombatState)clicked.First().CombatState!;
            var scope = CombatStateScope.Require(combat);
            CardRouting.Commit(groups, selected, scope.Selection, scope.Seed,
                id => scope.For("combat routing", operation, id), operation != "transformation",
                c => CombatCards.Info(c).Key, c => CombatCards.Info(c).Address, Reassociate);

            void Reassociate(IReadOnlyList<CardModel> physical, IReadOnlyList<CardModel> logical)
            {
                CombatReferences.Rebind(combat, physical, logical);
                CardOrder.Reassociate(physical, logical);
                CardOccurrences.Reassociate(physical, logical);
                CombatCards.Reassociate(physical, logical);
            }
        }
    }

    internal static string Operation(AbstractModel model) => model.GetType().Name switch
    {
        "DecisionsDecisions" => "play",
        "Acrobatics" or "DaggerThrow" or "GamblersBrew" or "GamblingChip" or "HiddenDaggers" or
            "Prepared" or "Survivor" or "TheHermit" or "ToolsOfTheTradePower" => "discard",
        "Ashwater" or "Brand" or "BurningPact" or "Cleanse" or "Purity" or "Scavenge" or
            "ToastyMittens" or "TrueGrit" or "TyrannyPower" => "exhaust",
        "Dredge" or "DropletOfPrecognition" or "ForegoneConclusionPower" or "Graveblast" or
            "Hologram" or "KiChant" or "LiquidMemories" or "LockedIn" or "NeowsFury" or
            "SecretTechnique" or "SecretWeapon" or "SeekerStrike" or "StratagemPower" or "Tutor" or "Wish" => "retrieval",
        "Armaments" => "upgrade",
        "Begone" or "Charge" or "EntropyPower" or "Florescence" or "Guards" or "Seance" => "transformation",
        _ => "choice:" + model.Id.Entry
    };

    private static bool RouteChoices(Player player) => !CombatCards.UseVanilla(player) &&
        RunManager.Instance.CardSelectorStack.GetSelector(player) is not VakuuCardSelector;

    private static async Task<IEnumerable<CardModel>> Complete(Task<IEnumerable<CardModel>> task, Choice? choice)
    {
        var clicked = (await task).ToArray();
        choice?.Commit(clicked);
        return clicked;
    }
    private static async Task<CardModel?> CompleteOne(Task<CardModel?> task, Choice? choice)
    {
        var clicked = await task;
        if (clicked != null) choice?.Commit([clicked]);
        return clicked;
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHand))]
    private static class HandChoice
    {
        private static void Prefix(Player player, Func<CardModel, bool>? filter, AbstractModel? source, out Choice? __state) =>
            __state = source != null && RouteChoices(player) ? new(player.PlayerCombatState!.Hand.Cards.Where(c => filter?.Invoke(c) ?? true), Operation(source)) : null;
        private static void Postfix(Choice? __state, ref Task<IEnumerable<CardModel>> __result) => __result = Complete(__result, __state);
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHandForUpgrade))]
    private static class UpgradeChoice
    {
        private static void Prefix(Player player, out Choice? __state) =>
            __state = RouteChoices(player) ? new(player.PlayerCombatState!.Hand.Cards.Where(c => c.IsUpgradable), "upgrade") : null;
        private static void Postfix(Choice? __state, ref Task<CardModel?> __result) => __result = CompleteOne(__result, __state);
    }

    // The grid and scripted selectors can present different orders for the same draw pile.
    internal sealed class PileSelection(string operation, CardModel[] pileOrder)
    {
        private Choice? presented;
        internal void Present(IEnumerable<CardModel> shown) => presented = new(shown, operation);
        internal Task<IEnumerable<CardModel>> SelectGrid(NCombatPileCardSelectScreen screen)
        {
            Present(screen.GetNode<NCardGrid>("%CardGrid").SortedCards);
            return screen.CardsSelected();
        }
        internal async Task<IEnumerable<CardModel>> Finish(Task<IEnumerable<CardModel>> task)
        {
            var clicked = (await task).ToArray();
            if (clicked.Length > 0)
                (presented ?? new Choice(pileOrder, operation)).Commit(clicked);
            return clicked;
        }
    }

    [HarmonyPatch]
    private static class PileChoice
    {
        private static readonly AsyncLocal<PileSelection?> pendingChoice = new();
        private static MethodBase TargetMethod() => typeof(CardSelectCmd).GetMethods().Single(m =>
            m.Name == nameof(CardSelectCmd.FromCombatPile) && m.GetParameters().Length == 5);
        private static void Prefix(CardPile pile, Player player, Func<CardModel, bool>? filter, out (PileSelection? Previous, PileSelection? Current) __state)
        {
            var current = RouteChoices(player) ? new PileSelection(Operation(source.Value
                ?? throw new InvalidOperationException("Missing combat pile choice source.")), pile.Cards.Where(c => filter?.Invoke(c) ?? true).ToArray()) : null;
            __state = (pendingChoice.Value, current);
            pendingChoice.Value = current;
        }
        private static void Postfix((PileSelection? Previous, PileSelection? Current) __state, ref Task<IEnumerable<CardModel>> __result)
        {
            pendingChoice.Value = __state.Previous;
            if (__state.Current is { } current)
                __result = current.Finish(__result);
        }

        [HarmonyPatch]
        private static class Presentation
        {
            private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(CardSelectCmd), nameof(CardSelectCmd.FromCombatPile),
                PileChoice.TargetMethod().GetParameters().Select(p => p.ParameterType).ToArray());

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var patched = RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(ICardSelector) && m.Name == nameof(ICardSelector.GetSelectedCards),
                    AccessTools.Method(typeof(Presentation), nameof(Select)), 2);
                return RewardPatches.Replace(patched, m => m.Name == nameof(NCardGridSelectionScreen.CardsSelected),
                    AccessTools.Method(typeof(Presentation), nameof(SelectGrid)));
            }
            private static Task<IEnumerable<CardModel>> Select(ICardSelector selector, IEnumerable<CardModel> cards, int min, int max)
            {
                if (pendingChoice.Value is { } current)
                {
                    var shown = cards.ToArray();
                    current.Present(shown);
                    return selector.GetSelectedCards(shown, min, max);
                }
                return selector.GetSelectedCards(cards, min, max);
            }
            private static Task<IEnumerable<CardModel>> SelectGrid(NCombatPileCardSelectScreen screen) =>
                pendingChoice.Value?.SelectGrid(screen) ?? screen.CardsSelected();
        }
    }

    // Context follows asynchronous continuations; nested sources restore their caller's context.
    [HarmonyPatch]
    private static class SourceContext
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] cards = [typeof(Charge), typeof(Cleanse), typeof(CosmicIndifference), typeof(Dredge),
                typeof(Graveblast), typeof(Headbutt), typeof(Hologram), typeof(KiChant), typeof(LockedIn), typeof(NeowsFury),
                typeof(Seance), typeof(SecretTechnique), typeof(SecretWeapon), typeof(SeekerStrike), typeof(Tutor), typeof(WahKi), typeof(Wish)];
            foreach (var type in cards) yield return AccessTools.Method(type, "OnPlay");
            yield return AccessTools.Method(typeof(DropletOfPrecognition), "OnUse");
            yield return AccessTools.Method(typeof(LiquidMemories), "OnUse");
            yield return AccessTools.Method(typeof(ForegoneConclusionPower), nameof(ForegoneConclusionPower.BeforeHandDraw));
            yield return AccessTools.Method(typeof(StratagemPower), nameof(StratagemPower.AfterShuffle));
        }
        private static void Prefix(AbstractModel __instance, out AbstractModel? __state)
        {
            __state = source.Value;
            source.Value = __instance;
        }
        private static void Finalizer(AbstractModel? __state) => source.Value = __state;
    }

    [HarmonyPatch]
    private static class PlayerPlay
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(PlayCardAction), "ExecuteAction");
        [HarmonyBefore(RitsuBridge.PatchId)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var count = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(AccessTools.Method(typeof(CardModel), nameof(CardModel.SpendResources))))
                {
                    yield return new CodeInstruction(OpCodes.Dup).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerPlay), nameof(Route)));
                    count++;
                }
                yield return instruction;
            }
            if (count != 1)
                throw new InvalidOperationException($"Expected one manual-play commitment, found {count}.");
        }
        private static void Route(CardModel card)
        {
            if (CombatCards.UseVanilla(card.Owner)) return;
            // The action has validated play/target; capture equivalence before spending changes costs.
            var eligible = card.Owner.PlayerCombatState!.Hand.Cards.Where(c => c.CanPlay(out _, out _)).ToArray();
            new Choice(eligible, "play").Commit([card]);
        }
    }

    [HarmonyPatch(typeof(ThievingHopper), "ThieveryMove")]
    private static class Theft
    {
        private static void Postfix(ThievingHopper __instance, ref Task __result) => __result = Complete(__result, __instance);
        private static async Task Complete(Task task, ThievingHopper hopper)
        {
            await task;
            theftFinished.GetValue(hopper, _ => new());
        }
    }
}
