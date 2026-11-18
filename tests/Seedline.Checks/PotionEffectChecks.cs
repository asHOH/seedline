using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckPotionEffects()
    {
        var run = StartGoldRun();
        var player = run.Players[0];
        run.AppendToMapPointHistory(MapPointType.Monster, RoomType.Monster, ModelDb.Encounter<AxebotsNormal>().Id);
        var state = RunCheckpoints.Require(run);
        var attack = PotionEffects.Counter(ModelDb.GetId<AttackPotion>().Entry);
        var skill = PotionEffects.Counter(ModelDb.GetId<SkillPotion>().Entry);
        var oil = PotionEffects.Counter(ModelDb.GetId<SneckoOil>().Entry);
        var potion = ModelDb.Potion<AttackPotion>().ToMutable();
        player.AddPotionInternal(potion);
        Assert(state.Random.Position(attack) == 0, "potion acquisition preserves the effect ordinal");
        potion.RemoveBeforeUse();
        Assert(state.Random.Position(attack) == 1 && PotionEffects.EffectOrdinal(potion) == 0,
            "committed potion use advances once before effects, including effects that make no rolls");
        var canceled = ModelDb.Potion<AttackPotion>().ToMutable();
        player.AddPotionInternal(canceled);
        canceled.AfterUsageCanceled();
        Assert(state.Random.Position(attack) == 1, "canceled potion use preserves effect progression");
        await PotionCmd.Discard(canceled);
        Assert(state.Random.Position(attack) == 2, "discard advances the covered type once");
        var skipped = new PotionReward(ModelDb.Potion<AttackPotion>().ToMutable(), player);
        skipped.OnSkipped();
        skipped.OnSkipped();
        Assert(state.Random.Position(attack) == 3, "each skipped potion reward advances once despite repeated skip notification");
        var claimed = new PotionReward(ModelDb.Potion<AttackPotion>().ToMutable(), player);
        await claimed.SelectUnsynchronized();
        claimed.OnSkipped();
        Assert(state.Random.Position(attack) == 3, "claimed potion rewards preserve effect progression");

        var cauldron = ModelDb.Relic<Cauldron>().ToMutable();
        player.AddRelicInternal(cauldron);
        var rewards = (List<Reward>)AccessTools.Method(typeof(Cauldron), "GenerateRewards").Invoke(cauldron, null)!;
        PotionModel[] contents = [ModelDb.Potion<AttackPotion>().ToMutable(), ModelDb.Potion<AttackPotion>().ToMutable(),
            ModelDb.Potion<SkillPotion>().ToMutable(), ModelDb.Potion<SkillPotion>().ToMutable(), ModelDb.Potion<SneckoOil>().ToMutable()];
        for (var i = 0; i < contents.Length; i++)
            AccessTools.Property(typeof(PotionReward), nameof(PotionReward.Potion)).SetValue(rewards[i], contents[i]);
        var previous = RewardsSet.testSelector;
        try
        {
            RewardsSet.testSelector = async set =>
            {
                await PotionCmd.Discard(claimed.Potion!);
                await RunManager.Instance.RewardsSetSynchronizer.SelectLocalReward(set.Rewards[0]);
                await PotionCmd.Discard(contents[0]);
                await RunManager.Instance.RewardsSetSynchronizer.SelectLocalReward(set.Rewards[2]);
                Assert(state.Random.Position(attack) == 3 && state.Random.Position(skill) == 0,
                    "Cauldron exemptions cover earlier inventory copies and acquired copies without advancing types");
                RunManager.Instance.RewardsSetSynchronizer.SkipLocalRewardsSet();
            };
            await new RewardsSet(player).WithCustomRewards(rewards).Offer();
        }
        finally { RewardsSet.testSelector = previous; }
        Assert(state.Random.Position(attack) == 4 && state.Random.Position(skill) == 0 && state.Random.Position(oil) == 0,
            "Cauldron skips consume remaining type exemptions and advance after they run out");
        var save = RunManager.Instance.ToSave(null);
        var encoded = RunCheckpoints.Encode(save);
        await Hook.BeforeRoomEntered(run, new RestSiteRoom());
        await PotionCmd.Discard(contents[2]);
        Assert(state.Random.Position(skill) == 1,
            $"next-room entry expires unused Cauldron exemptions (skill ordinal {state.Random.Position(skill)})");
        Assert(RunCheckpoints.Require(save).Random.Position(skill) == 0,
            "potion actions preserve the captured effect counters");
        var decoded = RunCheckpoints.Decode(Encoding.UTF8.GetString(encoded), save.SchemaVersion);
        RunManager.Instance.CleanUp();
        run = RunState.FromSerializable(decoded);
        await RunManager.Instance.SetUpSavedSingleplayer(run, decoded);
        var restoredSkill = run.Players[0].Potions.Single(p => p is SkillPotion);
        await PotionCmd.Discard(restoredSkill);
        Assert(RunCheckpoints.Require(run).Random.Position(skill) == 0,
            "checkpoint restoration retains the remaining Cauldron exemption and type ordinals");
        RunManager.Instance.CleanUp();
        await CheckPotionGeneration();
    }

    private sealed class PotionChoice : ICardSelector
    {
        internal CardModel[] Options = [];
        internal bool Skip;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            Options = options.ToArray();
            return Task.FromResult<IEnumerable<CardModel>>(Skip ? [] : Options.Take(1));
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) => throw new InvalidOperationException("Unexpected card reward.");
    }

    private static async Task CheckPotionGeneration()
    {
        string? expected = null;
        for (var branch = 0; branch < 2; branch++)
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
            if (branch == 1)
                RunCheckpoints.Require(run).Random.Advance(RandomAddress.For("combat", combat.Encounter!.RoomType.ToString()).At(run.CurrentActIndex));
            combat.AddPlayer(player);
            combat.Encounter!.GenerateMonstersWithSlots(run);
            foreach (var (monster, slot) in combat.Encounter.MonstersWithSlots)
                combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
            CombatManager.Instance.SetUpCombat(combat);
            var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
            AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);
            var scope = CombatStateScope.Require(combat);
            var choice = new PotionChoice();
            var signature = new List<string>();
            PotionModel[] potions = [ModelDb.Potion<AttackPotion>().ToMutable(), ModelDb.Potion<SkillPotion>().ToMutable(),
                ModelDb.Potion<PowerPotion>().ToMutable(), ModelDb.Potion<ColorlessPotion>().ToMutable(),
                ModelDb.Potion<CosmicConcoction>().ToMutable(), ModelDb.Potion<OrobicAcid>().ToMutable(),
                ModelDb.Potion<BottledSong>().ToMutable()];
            using (CardSelectorStack.SetTestSelector(choice))
                foreach (var potion in potions)
                {
                    if (branch == 1)
                    {
                        CombatGeneration.Generate(player, CombatGeneration.Pool(player, player.Character.CardPool), 3, false, typeof(Stoke));
                        var oil = ModelDb.Potion<SneckoOil>().ToMutable();
                        player.AddPotionInternal(oil);
                        await PotionCmd.Discard(oil);
                    }
                    var vanilla = run.Rng.CombatCardGeneration.ToSerializable();
                    var before = player.PlayerCombatState!.AllCards.ToArray();
                    player.AddPotionInternal(potion);
                    potion.RemoveBeforeUse();
                    choice.Options = [];
                    await (Task)AccessTools.Method(potion.GetType(), "OnUse").Invoke(potion,
                        [new ThrowingPlayerChoiceContext(), player.Creature])!;
                    var added = player.PlayerCombatState.AllCards.Except(before).Where(c => c.Pile != null).ToArray();
                    var batch = choice.Options.Length > 0 ? choice.Options : added;
                    var address = RandomAddress.For("potion card generation", potion.Id.Entry).At(0);
                    Assert(batch.Length == 3 && batch.All(c => CardOccurrences.Info(c).Address.StartsWith(address.Key[..^1] + ",", StringComparison.Ordinal)) &&
                        batch.Select(c => CardOccurrences.Info(c).Address).Distinct().Count() == 3,
                        $"{potion.Id.Entry} actual source creates three independently addressed entries in its first effect batch");
                    Assert(vanilla.Equals(run.Rng.CombatCardGeneration.ToSerializable()) && PotionEffects.EffectOrdinal(potion) == 0 &&
                        RunCheckpoints.Require(run).Random.Position(PotionEffects.Counter(potion.Id.Entry)) == 1,
                        $"{potion.Id.Entry} commits one type opportunity and preserves vanilla card-generation RNG");
                    if (choice.Options.Length > 0)
                    {
                        Assert(batch.Select(c => c.Id).Distinct().Count() == 3 && added.Length == 1 && added[0] == batch[0] &&
                            added[0].EnergyCost.GetWithModifiers(CostModifiers.All) == 0,
                            $"{potion.Id.Entry} preserves distinct offers, player choice, and the chosen card's free cost");
                        var type = potion is AttackPotion ? CardType.Attack : potion is SkillPotion ? CardType.Skill : CardType.Power;
                        Assert(potion is ColorlessPotion ? batch.All(c => c.Pool is ColorlessCardPool) : batch.All(c => c.Type == type),
                            $"{potion.Id.Entry} preserves its identity pool and type filter");
                    }
                    else if (potion is CosmicConcoction)
                    {
                        Assert(added.All(c => c.IsUpgraded && c.Pool is ColorlessCardPool) && added.Select(c => c.Id).Distinct().Count() == 3,
                            "Cosmic Concoction preserves three distinct upgraded colorless grants");
                        var keys = CombatShuffle.Keys(added.Reverse(), 0, false);
                        var ranked = added.OrderBy(c => c, CardOrder.Combat).ToArray();
                        Assert(added.Select(c => CombatCards.Info(c).CreationBatch).Distinct().Count() == 1 &&
                            ranked.Select(c => keys[c]).SequenceEqual(Enumerable.Range(0, ranked.Length)
                                .Select(i => RandomAddress.For("shuffle key", "second").At(i).Key)),
                            "Cosmic Concoction's sequential admissions share a creation batch ranked in processing order");
                    }
                    else
                    {
                        var pool = potion is OrobicAcid ? CombatGeneration.Pool(player, player.Character.CardPool).ToArray() :
                            player.UnlockState.CharacterCardPools.SelectMany(p => CombatGeneration.Pool(player, p))
                                .Where(c => c.Tags.Any(t => t is CardTag.Wah or CardTag.Cho or CardTag.Ki)).ToArray();
                        CardType[] cardTypes = [CardType.Attack, CardType.Skill, CardType.Power];
                        CardTag[] tags = [CardTag.Wah, CardTag.Cho, CardTag.Ki];
                        var supply = new RewardState();
                        var entries = Enumerable.Range(0, 3).Select(i => supply.Draw(scope.Seed, address,
                            pool.Select(c => c.Id.Entry), pool.Where(c => potion is OrobicAcid ? c.Type == cardTypes[i] : c.Tags.Contains(tags[i]))
                                .Select(c => c.Id.Entry))).ToArray();
                        Assert(added.Select(c => CardOccurrences.Info(c).Address).SequenceEqual(entries.Select(e => e.Address)) &&
                            added.All(c => c.EnergyCost.GetWithModifiers(CostModifiers.All) == 0),
                            $"{potion.Id.Entry} shares one filtered supply across ordered subdraws and preserves free costs");
                    }
                    signature.Add(string.Join("|", batch.Select(c => c.Id + ":" + CardOccurrences.Info(c).Address)));
                    foreach (var card in added)
                        card.Pile!.RemoveInternal(card);
                }
            var actual = string.Join(";", signature);
            expected ??= actual;
            Assert(actual == expected, "potion batches replay across different combat opportunities despite unrelated generation and other potion types");
            var skipped = ModelDb.Potion<AttackPotion>().ToMutable();
            player.AddPotionInternal(skipped);
            skipped.RemoveBeforeUse();
            choice.Skip = true;
            using (CardSelectorStack.SetTestSelector(choice))
                await (Task)AccessTools.Method(typeof(AttackPotion), "OnUse").Invoke(skipped,
                    [new ThrowingPlayerChoiceContext(), player.Creature])!;
            Assert(PotionEffects.EffectOrdinal(skipped) == 1 && choice.Options.Length == 3 &&
                choice.Options.All(c => c.Pile == null) && RunCheckpoints.Require(run).Random.Position(PotionEffects.Counter(skipped.Id.Entry)) == 2,
                "declining a generated-card offer retains its consumed potion ordinal and leaves all options unclaimed");
            RunManager.Instance.CleanUp();
        }
    }
}
