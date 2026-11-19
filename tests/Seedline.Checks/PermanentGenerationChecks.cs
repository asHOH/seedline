using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckPermanentGenerationPolicies()
    {
        string? expected = null;
        foreach (var waterfall in new[] { false, true })
        {
            RunManager.Instance.CleanUp();
            var run = StartGoldRun();
            var player = run.Players[0];
            await CardCmd.TransformToRandom(player.Deck.Cards.OfType<Bash>().Single(), player.PlayerRng.Transformations, CardPreviewStyle.None);
            var state = RunCheckpoints.Require(run);
            var pool = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
                .Where(c => c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare).ToArray();
            var address = RandomAddress.For("permanent card identity", player.Character.CardPool.Id.Entry);
            var preview = state.Rewards.Copy();
            var entry = preview.Draw(state.Seed, address, pool.Select(c => c.Id.Entry),
                pool.Where(c => c.Rarity == CardRarity.Rare && c.IsAllowedInRun(run)).Select(c => c.Id.Entry));
            var before = player.Deck.Cards.ToArray();
            var vanilla = player.PlayerRng.Rewards.ToSerializable();
            if (waterfall)
            {
                var source = ModelDb.Event<WaterfallOfObviousSecrets>().ToMutable();
                run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, source.Id);
                await source.BeginEvent(player, null, false);
                await (Task)AccessTools.Method(typeof(WaterfallOfObviousSecrets), "Negotiate").Invoke(source, [])!;
            }
            else
                await RelicCmd.Obtain<ArcaneScroll>(player);
            var added = player.Deck.Cards.Except(before).Single();
            Assert(added.Rarity == CardRarity.Rare && RewardEntries.Find(added) == entry &&
                vanilla.Equals(player.PlayerRng.Rewards.ToSerializable()) && state.Rewards.Rare.Threshold == 0,
                "fixed-Rare direct addition follows the transformation supply without consuming reward rarity or vanilla RNG");
            var next = preview.Draw(state.Seed, address, pool.Select(c => c.Id.Entry),
                pool.Where(c => c.Id != added.Id && c.IsAllowedInRun(run)).Select(c => c.Id.Entry));
            var transformed = (await CardCmd.TransformToRandom(added, player.PlayerRng.Transformations, CardPreviewStyle.None)).cardAdded;
            Assert(RewardEntries.Find(transformed) == next && transformed.Id != added.Id &&
                state.Cards.Removed.ContainsKey(entry.Address),
                "transforming the direct grant resumes shared filtered stock and retires its original occurrence");
            var actual = entry + ":" + next + JsonSerializer.Serialize(state.Rewards.Supplies);
            if (expected == null) expected = actual;
            else Assert(actual == expected, "Arcane Scroll and Waterfall share identities, addresses and subsequent transformation supply state");
        }

        foreach (var ascension in new[] { 0, 7 })
            foreach (var method in new[] { "Study", "TouchCore" })
                foreach (var rare in new[] { false, true })
                {
                    RunManager.Instance.CleanUp();
                    var run = StartGoldRun(ascension);
                    var player = run.Players[0];
                    var canonical = ModelDb.Event<InfestedAutomaton>();
                    run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, canonical.Id);
                    run.PushRoom(new EventRoom(canonical));
                    RunManager.Instance.EventSynchronizer.BeginEvent(canonical);
                    var source = RunManager.Instance.EventSynchronizer.GetLocalEvent();
                    var rareChance = ascension == 7 ? 0.0149 : 0.03;
                    var seed = Enumerable.Range(0, 10000).First(i => rare
                        ? new Rng((ulong)i).NextFloat() < rareChance : new Rng((ulong)i).NextFloat() >= rareChance + 0.37);
                    var rng = new Rng((ulong)seed);
                    source.Rng.LoadFromSerializable(rng.ToSerializable());
                    rng.NextFloat();
                    var rewards = player.PlayerRng.Rewards.ToSerializable();
                    var nonCombat = player.PlayerRng.NonCombatGeneration.ToSerializable();
                    var state = RunCheckpoints.Require(run);
                    var before = player.Deck.Cards.ToArray();
                    await (Task)AccessTools.Method(typeof(InfestedAutomaton), method).Invoke(source, [])!;
                    var added = player.Deck.Cards.Except(before).Single();
                    var rarity = rare ? CardRarity.Rare : method == "Study" ? CardRarity.Uncommon : CardRarity.Common;
                    Assert(added.Rarity == rarity && (method == "Study" ? added.Type == CardType.Power :
                        added.EnergyCost.Canonical == 0 && !added.EnergyCost.CostsX) && !CardOccurrences.Info(added).SourceKey,
                        $"A{ascension} Automaton {method}: preserves source restrictions, Common fallback and non-source-key acquisition");
                    Assert(source.Rng.ToSerializable().Equals(rng.ToSerializable()) &&
                        rewards.Equals(player.PlayerRng.Rewards.ToSerializable()) && nonCombat.Equals(player.PlayerRng.NonCombatGeneration.ToSerializable()) &&
                        state.Rewards.Rare.Threshold == 0 && state.Rewards.Rare.Offset == -0.05,
                        $"A{ascension} Automaton {method}: consumes exactly one event rarity roll while preserving player RNG and combat rarity state");
                }
        RunManager.Instance.CleanUp();
    }
}
