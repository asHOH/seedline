using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace Seedline;

internal sealed class FrozenCardOrder(IComparable[] properties, string? address) : IComparable<FrozenCardOrder>, IComparable
{
    private readonly IComparable[] properties = properties;
    private readonly string? address = address;
    internal int Category => (int)properties[0];
    public int CompareTo(object? other) => CompareTo((FrozenCardOrder?)other);

    public int CompareTo(FrozenCardOrder? other)
    {
        if (ReferenceEquals(this, other))
            return 0;
        if (other == null)
            return 1;
        for (var i = 0; i < Math.Min(properties.Length, other.properties.Length); i++)
        {
            var result = properties[i] is string text ? StringComparer.Ordinal.Compare(text, (string)other.properties[i])
                : properties[i].CompareTo(other.properties[i]);
            if (result != 0)
                return result;
        }
        var length = properties.Length.CompareTo(other.properties.Length);
        return length != 0 ? length : StringComparer.Ordinal.Compare(address, other.address);
    }
}

internal sealed record PermanentCardEquivalence(string Identity, int UpgradeLevel, string? Enchantment, int EnchantmentAmount, int Progress);

internal static partial class CardOrder
{
    internal static readonly IComparer<CardModel> Permanent = Comparer<CardModel>.Create((a, b) => Compare(a, b, CapturePermanent));
    internal static readonly IComparer<CardModel> Identity = Comparer<CardModel>.Create((a, b) => Compare(a, b, CaptureIdentity));

    internal static FrozenCardOrder CapturePermanent(CardModel card) => new(Rank(card), CardOccurrences.Find(card)?.Address);
    private static FrozenCardOrder CaptureIdentity(CardModel card) => new(Rank(card.CanonicalInstance), null);

    internal static PermanentCardEquivalence Equivalence(CardModel card) => new(card.Id.Entry, card.CurrentUpgradeLevel,
        card.Enchantment?.Id.Entry, card.Enchantment?.Amount ?? 0, card switch
        {
            GeneticAlgorithm c => c.IncreasedBlock,
            TheScythe c => c.IncreasedDamage,
            ShowOff c => c.IncreasedAim,
            Dowsing c => c.RoomsEntered,
            Guilty c => c.CombatsSeen,
            _ => 0
        });

    private static readonly HashSet<string> conditionals = Set("BODY_SLAM FEED FIEND_FIRE FORGOTTEN_RITUAL ONE_TWO_PUNCH PACTS_END PRIMAL_FORCE RAGE SECOND_WIND STOKE STOMP TEAR_ASUNDER ANTICIPATE BUBBLE_BUBBLE BULLET_TIME CALCULATED_GAMBLE CORROSIVE_WAVE FINISHER GRAND_FINALE MIRAGE MURDER NIGHTMARE PINPOINT SHADOW_STEP SHADOWMELD THE_HUNT DECISIONS_DECISIONS LUNAR_BLAST MONOLOGUE RADIATE SUPERMASSIVE EIDOLON PULL_FROM_BELOW SACRIFICE TRANSFIGURE BARRAGE FLAK_CANNON HELIX_DRILL MULTI_CAST QUADCAST SIGNAL_BOOST SYNCHRONIZE VOLTAIC BOUNCE CHO_LIMBS CODA FLORESCENCE GROWTH HARMONIC_SHIELD KI_VERSE NAIL QUIETUS UPROOT WREST ANOINTED BEAT_DOWN GOLD_AXE HAND_OF_GREED HIDDEN_GEM IMPATIENCE MIND_BLAST PROLONG RESTLESSNESS CLASH DUAL_WIELD ENLIGHTENMENT ENTRENCH FEEDING_FRENZY STACK JUDGEMENT THE_DEVIL");
    private static readonly HashSet<string> attackingSkills = Set("BLADE_DANCE BOUNCING_FLASK DARKNESS DEADLY_POISON DEATH END_OF_DAYS GOD_BYRD KNIFE_TRAP NO_ESCAPE OBLIVION OUTBREAK SNAKEBITE TEMPEST THE_BOMB THE_MOON ZAP");
    private static readonly string[] curses = "BAD_LUCK ENTHRALLED NORMALITY REGRET DEBT SHAME DOUBT DECAY WRITHE CURSE_OF_THE_BELL GREED INJURY BYRDONIS_EGG DOWSING LANTERN_KEY SPOILS_MAP GUILTY SPORE_MIND POOR_SLEEP ASCENDERS_BANE FOLLY CLUMSY".Split(' ');
    private static readonly string[] statuses = "WITHER BECKON VOID TOXIC INFECTION BURN WOUND SOOT DEBRIS SLIMED SADNESS DAZED FRANTIC_ESCAPE".Split(' ');
    private static readonly string[] enchantments = "GLAM NONE MOMENTUM SOULS_POWER SHARP TEZCATARAS_EMBER VIGOROUS CORRUPTED INSTINCT INKY NIMBLE GOOPY ADROIT SOWN SLITHER PERFECT_FIT STEADY ROYALLY_APPROVED SWIFT".Split(' ');
    private static readonly HashSet<string> trash = Set("CALTROPS CLASH DISTRACTION DUAL_WIELD ENTRENCH HELLO_WORLD OUTMANEUVER REBOUND RIP_AND_TEAR STACK");
    private static readonly HashSet<string> eventA = Set("EXTERMINATE SQUASH ENLIGHTENMENT FEEDING_FRENZY");
    private static readonly Dictionary<Type, bool> drawEffects = new();
    // These draws resolve through powers, outside the card's own methods.
    private static readonly HashSet<string> delayedDraws = Set("DARK_EMBRACE MACHINE_LEARNING TOOLS_OF_THE_TRADE DEMESNE GLOW GUIDING_STAR ITERATION MOTIF PAGESTORM PLOT PREDATOR RELAX THE_STAR TYRANNY VICIOUS WAH_KI");
    private static Dictionary<string, string>? english;

    private static int Compare(CardModel? left, CardModel? right, Func<CardModel, FrozenCardOrder> capture)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null || right == null)
            return left == null ? -1 : 1;
        return capture(left).CompareTo(capture(right));
    }

    private static IComparable[] Rank(CardModel card, CombatRankBatch? batch = null, bool includeOrigin = true,
        ICombatState? pileCombat = null)
    {
        var inCombat = batch != null;
        var combat = inCombat ? pileCombat ?? card.CombatState : null;
        var listeners = batch?.Listeners(combat ?? PendingCombat(card), card) ?? [];
        var persistent = Equivalence(card);
        var id = persistent.Identity;
        var replay = card.GetEnchantedReplayCount();
        var strike = card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Strike);
        var defend = card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Defend);
        var category = card.Type is CardType.Curse or CardType.Quest ? 0 : card.Type == CardType.Status ? 1 :
            strike && replay == 0 ? 2 : id == "MIGHTY_WEAPON" ? 3 : defend && replay == 0 ? 4 : id == "MIGHTY_SHIELD" ? 5 :
            card.Rarity == CardRarity.Basic && !strike && !defend ? 6 : conditionals.Contains(id) ? 9 :
            card.Type == CardType.Power ? 10 : card.Type == CardType.Attack || attackingSkills.Contains(id) ? 7 : 8;
        var rarity = Rarity(card);
        var enchantment = card.Enchantment;
        var expired = enchantment?.Status == EnchantmentStatus.Disabled;
        var enchantId = enchantment?.Id.Entry ?? "NONE";
        var neutral = expired || enchantId is "NONE" or "SPIRAL" or "CLONE" or "IMBUED";
        var enchantRank = neutral ? 1d : Array.IndexOf(enchantments, enchantId);
        var momentum = enchantment is Momentum ? (int)AccessTools.Field(typeof(Momentum), "_extraDamage").GetValue(enchantment)! : 0;
        if (enchantment is Momentum && momentum > 0)
            enchantRank = 6.5;
        var neutralTie = enchantId == "NONE" ? 0 : expired ? 1 : enchantId == "SPIRAL" ? 2 : enchantId == "CLONE" ? 3 : 4;
        var lastingEnergy = inCombat ? CombatEnergy(card, listeners, false) : card.EnergyCost.GetWithModifiers(CostModifiers.None);
        var energy = card.EnergyCost.CostsX ? 0.5 : lastingEnergy;
        if (enchantment is Slither)
            energy = 2.5;
        english ??= JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString("res://localization/eng/cards.json"))!;
        var title = english.GetValueOrDefault(id + ".title", id).ToUpperInvariant();
        var special = card is Slimed && replay > 0 ? statuses.Length :
            category == 0 ? Array.IndexOf(curses, id) : category == 1 ? Array.IndexOf(statuses, id) : -1;
        var carried = inCombat ? CarriedValue(card) : card is GeneticAlgorithm or TheScythe or ShowOff ? persistent.Progress : 0;
        var keywords = inCombat ? CombatKeywords(card, listeners) : card.Keywords;
        var restoration = inCombat ? Restoration(card) : 0;
        var temporaryKeywords = inCombat ? TemporaryKeywords(card, keywords, combat) : 0;
        var turnEnergy = inCombat && !card.IsCanonical ? CombatEnergy(card, listeners, true) : lastingEnergy;
        var turnEffects = inCombat && !card.IsCanonical && (temporaryKeywords != 0 || turnEnergy != lastingEnergy ||
            energyModifiers(card.EnergyCost).Any(Turn) || starModifiers(card).Any(m => m.ClearsWhenTurnEnds));
        var rank = new List<IComparable> {category, special, (strike || defend) && replay > 0 ? (replay - 1) / 2d : replay,
            category < 2 ? false : keywords.Contains(CardKeyword.Retain),
            category < 2 ? 0d : card.HasStarCostX ? 0.5 : Math.Max(0, inCombat ? CombatStars(card, listeners, false) : card.BaseStarCost), category < 2 ? false : rarity >= 10,
            category < 2 ? 0d : energy, category < 2 ? false : Draws(card), category < 2 ? 0 : rarity,
            category < 2 ? false : card.Pool is ColorlessCardPool || card.Pool.Id.Entry is "EVENT_CARD_POOL" or "TOKEN_CARD_POOL",
            title, id };
        if (inCombat)
            rank.Add(card.Affliction is not MegaCrit.Sts2.Core.Models.Afflictions.Bound);
        rank.AddRange([carried, enchantRank, persistent.EnchantmentAmount, momentum,
            inCombat ? card.IsUpgraded ? 2 : restoration > 0 ? 1 : 0 : persistent.UpgradeLevel]);
        if (inCombat)
            rank.Add(card.CurrentUpgradeLevel);
        rank.AddRange([card is Guilty ? persistent.Progress : 0, keywords.Contains(CardKeyword.Ethereal), keywords.Contains(CardKeyword.Sly)]);
        if (inCombat)
            rank.AddRange([PendingUse(card, combat), restoration, turnEffects,
                turnEffects ? card.HasStarCostX ? 0.5 : Math.Max(0, CombatStars(card, listeners, true)) : 0d,
                turnEffects ? card.EnergyCost.CostsX ? 0.5 : turnEnergy : 0d,
                temporaryKeywords, enchantment is Slither ? lastingEnergy : 0]);
        rank.AddRange([neutral ? neutralTie : 0, expired ? Array.IndexOf(enchantments, enchantId) : 0]);
        if (inCombat)
        {
            rank.Add(StarModifiers(card));
            rank.Add(EnergyModifiers(card));
            if (includeOrigin)
                rank.Add(card.DeckVersion != null || (pileCombat == null && card.Pile?.Type == PileType.Deck));
        }
        return rank.ToArray();
    }

    private static int Rarity(CardModel card) => card.Id.Entry switch
    {
        "PECK" => 1,
        "TORIC_TOUGHNESS" => 3,
        "DINGY_DAGGER" => card.CurrentUpgradeLevel < 2 ? 6 : 12,
        "SOVEREIGN_BLADE" => 11,
        _ when trash.Contains(card.Id.Entry) => 2,
        _ => card.Rarity switch
        {
            CardRarity.Basic => 0,
            CardRarity.Common => 4,
            CardRarity.Token => 5,
            CardRarity.Uncommon => 7,
            CardRarity.Event => eventA.Contains(card.Id.Entry) ? 8 : 12,
            CardRarity.Rare => 10,
            CardRarity.Ancient => 13,
            _ => 0
        }
    };

    private static bool Draws(CardModel card)
    {
        if (card.Enchantment is Swift { Status: EnchantmentStatus.Normal } || card.Id.Entry == "COMPILE_DRIVER" || delayedDraws.Contains(card.Id.Entry))
            return true;
        var type = card.GetType();
        if (!drawEffects.TryGetValue(type, out var draws))
        {
            draws = type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public).Append(type)
                .SelectMany(t => t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(m => m.GetMethodBody() != null).Any(m => PatchProcessor.GetOriginalInstructions(m)
                    .Any(i => i.operand is MethodInfo method &&
                        (method.DeclaringType == typeof(CardPileCmd) && method.Name == nameof(CardPileCmd.Draw) ||
                         method.DeclaringType == typeof(CardCmd) && method.Name == nameof(CardCmd.DiscardAndDraw))));
            drawEffects.Add(type, draws);
        }
        return draws;
    }
    private static HashSet<string> Set(string ids) => ids.Split(' ').ToHashSet(StringComparer.Ordinal);
}
