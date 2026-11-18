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
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class CombatRouting
{
    internal static bool IsChoicePatch(Type type) => type == typeof(HandChoice) || type == typeof(UpgradeChoice) || type == typeof(PileChoice);
    private static readonly AsyncLocal<AbstractModel?> source = new();
    private static readonly ConditionalWeakTable<ThievingHopper, object> theftFinished = new();

    internal static bool Protected(CardModel card) => card is GeneticAlgorithm or TheScythe or ShowOff ||
        card.Enchantment is Goopy || card.CombatState is CombatState combat && combat.Enemies.Any(c =>
            c.IsAlive && c.Monster is ThievingHopper hopper && !theftFinished.TryGetValue(hopper, out _));

    internal static bool Equivalent(CardModel a, CardModel b) => CardOrder.CombatEquivalent(a, b) &&
        (!Protected(a) && !Protected(b) || ReferenceEquals(a.DeckVersion, b.DeckVersion)) &&
        CombatReferences.PendingEquivalent(a, b);

    internal sealed class Choice
    {
        private readonly CardModel[][] groups;
        private readonly string operation;
        internal Choice(IEnumerable<CardModel> eligible, string operation)
        {
            this.operation = operation;
            var classes = new List<List<CardModel>>();
            foreach (var card in eligible)
            {
                var group = classes.FirstOrDefault(g => Equivalent(g[0], card));
                if (group == null) classes.Add([card]);
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
        "ArrowStab" or "Dredge" or "DropletOfPrecognition" or "ForegoneConclusionPower" or "Graveblast" or
            "Hologram" or "KiChant" or "LiquidMemories" or "LockedIn" or "NeowsFury" or
            "SecretTechnique" or "SecretWeapon" or "SeekerStrike" or "StratagemPower" or "Tutor" or "Wish" => "retrieval",
        "Armaments" => "upgrade",
        "Begone" or "Charge" or "EntropyPower" or "Guards" or "Seance" => "transformation",
        _ => "choice:" + model.Id.Entry
    };

    private static bool Scripted(Player player) => RunManager.Instance.CardSelectorStack.GetSelector(player) is VakuuCardSelector;

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
        private static void Prefix(Player player, Func<CardModel, bool>? filter, AbstractModel source, out Choice? __state) =>
            __state = Scripted(player) ? null : new(player.PlayerCombatState!.Hand.Cards.Where(c => filter?.Invoke(c) ?? true), Operation(source));
        private static void Postfix(Choice? __state, ref Task<IEnumerable<CardModel>> __result) => __result = Complete(__result, __state);
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHandForUpgrade))]
    private static class UpgradeChoice
    {
        private static void Prefix(Player player, out Choice? __state) =>
            __state = Scripted(player) ? null : new(player.PlayerCombatState!.Hand.Cards.Where(c => c.IsUpgradable), "upgrade");
        private static void Postfix(Choice? __state, ref Task<CardModel?> __result) => __result = CompleteOne(__result, __state);
    }

    [HarmonyPatch]
    private static class PileChoice
    {
        private static MethodBase TargetMethod() => typeof(CardSelectCmd).GetMethods().Single(m =>
            m.Name == nameof(CardSelectCmd.FromCombatPile) && m.GetParameters().Length == 5);
        private static void Prefix(CardPile pile, Player player, Func<CardModel, bool>? filter, out Choice? __state) =>
            __state = Scripted(player) ? null : new(pile.Cards.Where(c => filter?.Invoke(c) ?? true), Operation(source.Value
                ?? throw new InvalidOperationException("Missing combat pile choice source.")));
        private static void Postfix(Choice? __state, ref Task<IEnumerable<CardModel>> __result) => __result = Complete(__result, __state);
    }

    // Context follows asynchronous continuations; nested sources restore their caller's context.
    [HarmonyPatch]
    private static class SourceContext
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] cards = [typeof(ArrowStab), typeof(Charge), typeof(Cleanse), typeof(CosmicIndifference), typeof(Dredge),
                typeof(Graveblast), typeof(Headbutt), typeof(Hologram), typeof(KiChant), typeof(LockedIn), typeof(NeowsFury),
                typeof(Recall), typeof(Seance), typeof(SecretTechnique), typeof(SecretWeapon), typeof(SeekerStrike), typeof(Tutor), typeof(Wish)];
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
