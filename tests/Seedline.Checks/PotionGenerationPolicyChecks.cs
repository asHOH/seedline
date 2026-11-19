using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckPotionGenerationPolicy()
    {
        foreach (var seed in new[] { "POTIONPOLICY0", "POTIONPOLICY1" })
        {
            var run = StartGoldRun(seed: seed);
            var player = run.Players[0];
            var state = RunCheckpoints.Require(run);
            var context = new ThrowingPlayerChoiceContext();
            var ordinals = new Dictionary<ModelId, long>();
            CardModel[] Pool(CardPoolModel pool) => CardFactory.FilterForCombat(
                pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
                .Where(c => c.IsAllowedInRun(player.RunState)).ToArray();
            var own = Pool(player.Character.CardPool);
            var colorless = Pool(ModelDb.CardPool<ColorlessCardPool>());
            var melodies = player.UnlockState.CharacterCardPools.SelectMany(Pool)
                .Where(c => c.Tags.Any(t => t is CardTag.Wah or CardTag.Cho or CardTag.Ki)).ToArray();
            var choice = new PotionChoice();
            player.AddRelicInternal(ModelDb.Relic<PrismaticGem>().ToMutable());
            player.AddRelicInternal(ModelDb.Relic<DingyRug>().ToMutable());

            SupplyEntry[] Expected(PotionModel potion, long ordinal)
            {
                var pool = potion switch
                {
                    AttackPotion => own.Where(c => c.Type == CardType.Attack).ToArray(),
                    SkillPotion => own.Where(c => c.Type == CardType.Skill).ToArray(),
                    PowerPotion => own.Where(c => c.Type == CardType.Power).ToArray(),
                    OrobicAcid => own,
                    BottledSong => melodies,
                    _ => colorless
                };
                var address = RandomAddress.For("potion card generation", potion.Id.Entry).At(ordinal);
                var identities = pool.Select(c => c.Id.Entry).Distinct().Order(StringComparer.Ordinal).ToArray();
                var sequence = Enumerable.Range(0, 256).Select(i => new SupplyEntry(
                    identities[AddressRandom.IntInclusive(state.Seed, address.At(i), 0, identities.Length - 1)], address.At(i).Key)).ToList();
                CardType[] types = [CardType.Attack, CardType.Skill, CardType.Power];
                CardTag[] tags = [CardTag.Wah, CardTag.Cho, CardTag.Ki];
                var result = new List<SupplyEntry>();
                for (var draw = 0; draw < 3; draw++)
                {
                    var eligible = pool.Where(c => potion is OrobicAcid ? c.Type == types[draw] : potion is BottledSong ?
                        c.Tags.Contains(tags[draw]) : result.All(e => e.Identity != c.Id.Entry)).Select(c => c.Id.Entry).ToHashSet();
                    // Resolve all filtered shifts eagerly, without RewardState or ResultSupply.
                    var positions = Enumerable.Range(0, sequence.Count).Where(i => eligible.Contains(sequence[i].Identity)).ToArray();
                    if (positions.Length < 4 || positions[0] >= 64) throw new InvalidOperationException("Insufficient potion oracle tail.");
                    result.Add(sequence[positions[0]]);
                    for (var i = 1; i < positions.Length; i++) sequence[positions[i - 1]] = sequence[positions[i]];
                    sequence.RemoveAt(positions[^1]);
                }
                return result.ToArray();
            }

            for (var battle = 0; battle < 2; battle++)
            {
                var combat = CombatFixture.CreateActiveCombat(run);
                run.AppendToMapPointHistory(MapPointType.Monster, RoomType.Monster, combat.Encounter!.Id);
                var pcs = player.PlayerCombatState!;
                var vanilla = run.Rng.CombatCardGeneration.ToSerializable();
                PotionModel[] potions = [ModelDb.Potion<AttackPotion>(), ModelDb.Potion<SkillPotion>(), ModelDb.Potion<PowerPotion>(),
                    ModelDb.Potion<ColorlessPotion>(), ModelDb.Potion<CosmicConcoction>(), ModelDb.Potion<OrobicAcid>(), ModelDb.Potion<BottledSong>()];
                if (battle == 1) Array.Reverse(potions);

                async Task<CardModel[]> Use(PotionModel canonical, bool skip, bool overflow)
                {
                    foreach (var card in pcs.AllCards.ToArray()) card.RemoveFromCurrentPile();
                    if (overflow)
                        await CardPileCmd.AddGeneratedCardsToCombat(Enumerable.Range(0, CardPile.MaxCardsInHand)
                            .Select(_ => combat.CreateCard<Wound>(player)), PileType.Hand, player);
                    var potion = canonical.ToMutable();
                    var ordinal = ordinals.GetValueOrDefault(potion.Id);
                    var expected = Expected(potion, ordinal);
                    player.AddPotionInternal(potion);
                    choice.Options = [];
                    choice.Skip = skip;
                    var history = CombatManager.Instance.History.Entries.Count();
                    using (CardSelectorStack.SetTestSelector(choice)) await potion.OnUseWrapper(context, player.Creature);
                    ordinals[potion.Id] = ordinal + 1;
                    var generated = CombatManager.Instance.History.Entries.Skip(history).OfType<CardGeneratedEntry>().Select(e => e.Card).ToArray();
                    var offered = choice.Options.Length > 0;
                    var cards = offered ? choice.Options : generated;
                    Assert(cards.Select(c => new SupplyEntry(c.Id.Entry, CardOccurrences.Info(c).Address)).SequenceEqual(expected),
                        $"{seed}, battle {battle + 1}: {potion.Id.Entry} effect {ordinal} matches exact filtered identities and entry addresses");
                    Assert(PotionEffects.EffectOrdinal(potion) == ordinal && state.Random.Position(PotionEffects.Counter(potion.Id.Entry)) == ordinal + 1 &&
                        !player.Potions.Contains(potion) && potion.HasBeenRemovedFromState,
                        "the actual potion-use wrapper commits exactly one type-local opportunity");
                    Assert(generated.Length == (offered ? skip ? 0 : 1 : 3) && generated.All(c => c.Pile?.Type == (overflow ? PileType.Discard : PileType.Hand)),
                        "potion sources preserve choice counts, skipped offers and full-hand discard redirection");
                    Assert(cards.All(c => c.IsUpgraded == (potion is CosmicConcoction)) &&
                        (potion is CosmicConcoction || generated.All(c => c.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0)),
                        "potions preserve Cosmic upgrades and free-this-turn chosen or direct grants");
                    Assert(potion is OrobicAcid or BottledSong || cards.Select(c => c.Id).Distinct().Count() == 3,
                        "distinct potion batches retain all three alternatives without duplicate identities");
                    return cards;
                }

                foreach (var potion in potions)
                {
                    // Discard and reward skips consume the same type's next batch before use.
                    if (battle == 1)
                    {
                        var discarded = potion.ToMutable();
                        player.AddPotionInternal(discarded);
                        await PotionCmd.Discard(discarded);
                        ordinals[potion.Id]++;
                        new PotionReward(potion.ToMutable(), player).OnSkipped();
                        ordinals[potion.Id]++;
                    }
                    await Use(potion, skip: battle == 0 && potion is AttackPotion or ColorlessPotion, overflow: battle == 1);
                }
                if (battle == 1)
                {
                    var song = ModelDb.Potion<BottledSong>();
                    var repeatedOrdinal = ordinals[song.Id];
                    var limit = repeatedOrdinal + 64;
                    while (repeatedOrdinal < limit && Expected(song, repeatedOrdinal).Select(e => e.Identity).Distinct().Count() == 3)
                        repeatedOrdinal++;
                    Assert(repeatedOrdinal < limit, "Bottled Song fixture finds a multi-tag repeat in its addressed batches");
                    while (ordinals[song.Id] < repeatedOrdinal)
                    {
                        new PotionReward(song.ToMutable(), player).OnSkipped();
                        ordinals[song.Id]++;
                    }
                    var repeated = await Use(song, skip: false, overflow: false);
                    Assert(repeated.Select(c => c.Id).Distinct().Count() < 3 &&
                        repeated.Select(c => CardOccurrences.Info(c).Address).Distinct().Count() == 3 &&
                        repeated[0].Tags.Contains(CardTag.Wah) && repeated[1].Tags.Contains(CardTag.Cho) && repeated[2].Tags.Contains(CardTag.Ki),
                        "Bottled Song permits repeated multi-tag identities across Wah, Cho, Ki while retaining distinct occurrence entries");
                }
                Assert(vanilla.Equals(run.Rng.CombatCardGeneration.ToSerializable()), "all seven potion sources bypass vanilla card-generation RNG");
                CombatManager.Instance.Reset(true);
            }
            RunManager.Instance.CleanUp();
        }
    }
}
