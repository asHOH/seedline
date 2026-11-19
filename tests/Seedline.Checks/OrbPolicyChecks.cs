using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckOrbPolicy()
    {
        ModelId[] orbIds = [ModelDb.GetId<LightningOrb>(), ModelDb.GetId<FrostOrb>(), ModelDb.GetId<DarkOrb>(),
            ModelDb.GetId<PlasmaOrb>(), ModelDb.GetId<GlassOrb>()];
        foreach (var character in new CharacterModel[] { ModelDb.Character<Defect>(), ModelDb.Character<Ironclad>() })
        {
            var player = Player.CreateForNewRun(character, UnlockState.all, 1);
            var run = RunState.CreateForNewRun([player], ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(),
                [], GameMode.Standard, false, 7, "ORBPOLICY");
            RunManager.Instance.SetUpNewSingleplayer(run, true);
            LocalContext.NetId = RunManager.Instance.NetService.NetId;
            for (var battle = 0; battle < 2; battle++)
            {
                var combat = CombatFixture.CreateActiveCombat(run);
                foreach (var enemy in combat.Enemies) await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000);
                var scope = CombatStateScope.Require(combat);
                var address = RandomAddress.For("orb type").With(scope.Address.Key);
                var context = new ThrowingPlayerChoiceContext();
                var queue = player.PlayerCombatState!.OrbQueue;
                var vanilla = run.Rng.CombatOrbGeneration.ToSerializable();
                var ordinal = 0;
                async Task Check(int count, Func<Task> action, string source)
                {
                    var history = CombatManager.Instance.History.Entries.Count();
                    var previous = queue.Orbs.ToList();
                    await action();
                    var channeled = CombatManager.Instance.History.Entries.Skip(history).OfType<OrbChanneledEntry>().Select(e => e.Orb).ToArray();
                    var expected = Enumerable.Range(ordinal, count).Select(i =>
                        orbIds[AddressRandom.IntInclusive(scope.Seed, address.At(i), 0, orbIds.Length - 1)]).ToArray();
                    ordinal += count;
                    foreach (var orb in channeled)
                    {
                        if (previous.Count >= queue.Capacity) previous.RemoveAt(0);
                        previous.Add(orb);
                    }
                    Assert(channeled.Select(o => o.Id).SequenceEqual(expected) && scope.Random.Position(address) == ordinal &&
                        queue.Orbs.SequenceEqual(previous),
                        $"{character.Id.Entry}, battle {battle + 1}: {source} preserves exact successful orb identities, queue order and consumption");
                }
                async Task Play<T>(bool upgraded = false) where T : CardModel
                {
                    var card = combat.CreateCard<T>(player);
                    if (upgraded) CardCmd.Upgrade(card);
                    player.PlayerCombatState.PlayPile.AddInternal(card);
                    await (Task)AccessTools.Method(typeof(T), "OnPlay").Invoke(card,
                        [context, new CardPlay
                        {
                            Card = card, Player = player, Target = null, ResultPile = PileType.Discard,
                            Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                            IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
                        }])!;
                    card.RemoveFromCurrentPile();
                }
                await Check(2, () => Play<Chaos>(true), "upgraded Chaos");
                var oldCapacity = queue.Capacity;
                await Check(4, () => Play<TheMagician>(true), "upgraded Magician shares Chaos's sequence");
                Assert(queue.Capacity == oldCapacity + 4, "The Magician preserves its upgraded slot grant before channeling");
                await PowerCmd.Apply<TrashToTreasurePower>(context, player.Creature, 2, player.Creature, null);
                await Check(2, async () => await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<Dazed>(player), PileType.Discard, player),
                    "actual generated-status hook channels once per Trash to Treasure stack");
                await Check(0, async () => await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<Anger>(player), PileType.Hand, player),
                    "non-status generation");
                await Check(0, async () => await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<Dazed>(player), PileType.Discard, null),
                    "status generation without the owning player as creator");
                await OrbCmd.Channel<FrostOrb>(context, player);
                Assert(scope.Random.Position(address) == ordinal, "fixed channeling leaves the shared random-orb ordinal unchanged");
                OrbCmd.RemoveSlots(player, queue.Capacity - 1);
                var evoked = queue.Orbs.Single();
                await Check(1, () => Play<Chaos>(), "full one-slot queue evokes before admitting the next random orb");
                Assert(evoked.HasBeenRemovedFromState, "full-queue replacement retains the vanilla evoke/removal lifecycle");
                OrbCmd.RemoveSlots(player, queue.Capacity);
                var succeeds = character is Defect ? 0 : 1;
                await Check(succeeds, () => Play<Chaos>(), "zero-slot channel attempt");
                Assert(queue.Capacity == succeeds, "only characters without base orb slots automatically gain a slot");
                if (character is Defect)
                {
                    await Check(0, () => Play<Chaos>(true), "repeated failed channel attempts");
                    await OrbCmd.AddSlots(player, 1);
                }
                await Check(1, () => Play<Chaos>(), "successful channel after slot recovery");
                CombatFixture.SetInProgress(false);
                await Check(0, () => Play<Chaos>(), "ended combat");
                Assert(vanilla.Equals(run.Rng.CombatOrbGeneration.ToSerializable()), "all orb sources preserve vanilla orb RNG");
                CombatManager.Instance.Reset(true);
            }
            RunManager.Instance.CleanUp();
        }
    }
}
