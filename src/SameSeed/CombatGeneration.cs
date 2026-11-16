using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;

namespace SameSeed;

internal static class CombatGeneration
{
    private static readonly AsyncLocal<Type?> source = new();
    internal static bool IsMetamorphosis => source.Value == typeof(Metamorphosis);
    private static readonly Type[] sources =
    [typeof(Stoke), typeof(CallOfTheVoidPower), typeof(ChoicesParadox), typeof(CalamityPower), typeof(CreativeAiPower),
        typeof(HelloWorldPower), typeof(Jackpot), typeof(BigHat), typeof(BundleOfJoy), typeof(JackOfAllTrades),
        typeof(ManifestAuthority), typeof(Quasar), typeof(SpectrumShiftPower), typeof(OrangeDough), typeof(Toolbox),
        typeof(TheHierophant), typeof(Discovery), typeof(MadScience), typeof(VexingPuzzlebox), typeof(InfernalBlade),
        typeof(Crossbow), typeof(Distraction), typeof(Abundance), typeof(WhiteNoise), typeof(BlotOutTheSky),
        typeof(Metamorphosis), typeof(TheLovers), typeof(Splash), typeof(Ocarina)];
    private static readonly HashSet<Type> freeTurn =
    [typeof(Discovery), typeof(MadScience), typeof(VexingPuzzlebox), typeof(InfernalBlade), typeof(Crossbow),
        typeof(Distraction), typeof(Abundance), typeof(WhiteNoise), typeof(BlotOutTheSky)];

    internal static CardModel Draw(Player player, string group, string poolId, IEnumerable<CardModel> pool, IEnumerable<CardModel> eligible)
    {
        var combat = (CombatState)player.Creature.CombatState!;
        var scope = CombatStateScope.Require(combat);
        var identities = pool.ToArray();
        var entry = scope.Cards.Draw(scope.Seed, scope.For("card generation", group, poolId),
            identities.Select(c => c.Id.Entry), eligible.Select(c => c.Id.Entry));
        var card = combat.CreateCard(identities.First(c => c.Id.Entry == entry.Identity), player);
        CardOccurrences.Create(card, entry.Address);
        return card;
    }

    internal static IEnumerable<CardModel> Pool(Player player, CardPoolModel pool) => CardFactory.FilterForCombat(
        pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint));

    internal static List<CardModel> Generate(Player player, IEnumerable<CardModel> cards, int count, bool distinct, Type generator)
    {
        var eligible = CardFactory.FilterForCombat(cards).Where(c => c.IsAllowedInRun(player.RunState)).ToArray();
        var result = new List<CardModel>();
        if (count <= 0 || (distinct && eligible.Length == 0))
            return result;
        if (eligible.Length == 0)
            throw new InvalidOperationException("No eligible combat card generation.");
        var group = freeTurn.Contains(generator) ? "free this turn" : generator == typeof(Metamorphosis) ? "free this combat" :
            generator == typeof(Splash) ? "Splash" : "ordinary costs";
        IEnumerable<CardModel> pool;
        string poolId;
        if (generator == typeof(Ocarina))
        {
            pool = player.UnlockState.CharacterCardPools.SelectMany(p => Pool(player, p))
                .Where(c => c.Tags.Any(t => t is CardTag.Wah or CardTag.Cho or CardTag.Ki));
            poolId = "melodies";
        }
        else if (generator == typeof(TheLovers) || generator == typeof(Splash))
        {
            var pools = player.UnlockState.CharacterCardPools.ToList();
            if (pools.Count > 1)
                pools.Remove(player.Character.CardPool);
            pool = pools.SelectMany(p => Pool(player, p));
            if (generator == typeof(Splash))
                pool = pool.Where(c => c.Type == CardType.Attack);
            poolId = "other characters";
        }
        else
        {
            var cardPool = eligible[0].Pool;
            pool = Pool(player, cardPool);
            if (generator == typeof(Metamorphosis))
                pool = pool.Where(c => c.Type == CardType.Attack);
            poolId = cardPool.Id.Entry;
        }
        var identities = pool.ToArray();
        for (var i = 0; i < (distinct ? Math.Min(count, eligible.Length) : count); i++)
        {
            var allowed = distinct ? eligible.Where(c => result.All(r => r.Id != c.Id)) : eligible;
            result.Add(Draw(player, group, poolId, identities, allowed));
        }
        return result;
    }

    [HarmonyPatch]
    internal static class Sources
    {
        internal static IEnumerable<MethodBase> TargetMethods() => sources.SelectMany(t => t.GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<AsyncStateMachineAttribute>() != null);
        private static void Prefix(object __instance, out Type? __state)
        {
            __state = source.Value;
            source.Value = __instance.GetType();
        }
        private static void Finalizer(Type? __state) => source.Value = __state;
    }

    [HarmonyPatch]
    private static class Factories
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
        [AccessTools.Method(typeof(CardFactory), nameof(CardFactory.GetDistinctForCombat)),
            AccessTools.Method(typeof(CardFactory), nameof(CardFactory.GetForCombat))];
        private static bool Prefix(Player player, IEnumerable<CardModel> cards, int count, MethodBase __originalMethod,
            ref IEnumerable<CardModel> __result)
        {
            if (source.Value is not { } generator)
                return true;
            __result = Generate(player, cards, count, __originalMethod.Name == nameof(CardFactory.GetDistinctForCombat), generator);
            return false;
        }
    }

    [HarmonyPatch]
    private static class MythicForm
    {
        private static MethodBase TargetMethod() => RewardPatches.AsyncMethod(typeof(MythicFormPower), nameof(MythicFormPower.BeforeHandDraw));
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            instructions = RewardPatches.Replace(instructions, m => m.DeclaringType == typeof(Rng) && m.Name == nameof(Rng.NextItem),
                AccessTools.Method(typeof(MythicForm), nameof(Choose)));
            return RewardPatches.Replace(instructions, m => m.Name == "CreateCard" && !m.IsGenericMethod,
                AccessTools.Method(typeof(MythicForm), nameof(Created)));
        }
        private static CardModel Choose(Rng _, IEnumerable<CardModel> cards)
        {
            var player = CombatManager.Instance.DebugOnlyGetState()!.Players[0];
            return Draw(player, "ordinary costs", "Tarot", TarotCards.All, cards);
        }
        private static CardModel Created(ICombatState _, CardModel card, Player player) => card;
    }
}
