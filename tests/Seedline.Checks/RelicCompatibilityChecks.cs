using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckRelicCompatibility()
    {
        foreach (var (rarity, size, expected) in new[]
        {
            (RelicRarity.Common, 26, new[] { 12, 9, 5 }),
            (RelicRarity.Uncommon, 32, new[] { 14, 11, 7 }),
            (RelicRarity.Rare, 38, new[] { 17, 13, 8 }),
            (RelicRarity.Uncommon, 30, new[] { 13, 11, 6 }),
            (RelicRarity.Common, 27, new[] { 12, 10, 5 }),
            (RelicRarity.Rare, 39, new[] { 17, 14, 8 }),
            (RelicRarity.Common, 1, new[] { 1, 0, 0 }),
            (RelicRarity.Common, 2, new[] { 1, 1, 0 }),
            (RelicRarity.Common, 0, new[] { 0, 0, 0 })
        })
            Assert(RelicRewards.AllocationCounts(rarity, size).SequenceEqual(expected),
                $"{rarity}/{size}: relic allocation preserves vanilla counts and rounds altered pools deterministically");
        foreach (var size in Enumerable.Range(0, 257).Append(int.MaxValue))
        {
            var counts = RelicRewards.AllocationCounts(RelicRarity.Common, size);
            Assert(counts.Sum() == size && counts.Select((count, i) =>
                Math.Abs(count - (double)size * new[] { 9, 7, 4 }[i] / 20) < 1).All(value => value) &&
                RelicRewards.AllocationCounts(RelicRarity.Shop, size).SequenceEqual([0, size, 0]),
                $"{size}: allocation conserves stock within one relic of each target share; Shop stays shop-only");
        }

        RunManager.Instance.CleanUp();
        var initial = StartGoldRun();
        var shared = ModelDb.RelicPool<SharedRelicPool>();
        var character = initial.Players[0].Character.RelicPool;
        var sharedRelics = shared.AllRelics.ToArray();
        var characterRelics = character.AllRelics.ToArray();
        var originalIds = sharedRelics.Concat(characterRelics).Select(r => r.Id).ToHashSet();
        var additions = ModelDb.AllRelics.Where(r => !originalIds.Contains(r.Id) &&
            r.Rarity is RelicRarity.Common or RelicRarity.Uncommon or RelicRarity.Rare or RelicRarity.Shop).ToArray();
        Assert(additions.Length > 0, "expanded-pool fixture includes extra ordinary relic identities");
        var relicField = AccessTools.Field(typeof(RelicPoolModel), "_relics");
        try
        {
            foreach (var (name, sharedPool, characterPool) in new[]
            {
                ("vanilla", sharedRelics, characterRelics),
                ("no character Uncommons", sharedRelics, characterRelics.Where(r => r.Rarity != RelicRarity.Uncommon).ToArray()),
                ("expanded", sharedRelics, characterRelics.Concat(additions).ToArray()),
                ("no Commons", sharedRelics.Where(r => r.Rarity != RelicRarity.Common).ToArray(),
                    characterRelics.Where(r => r.Rarity != RelicRarity.Common).ToArray()),
                ("one Common", sharedRelics.Where(r => r.Rarity == RelicRarity.Common).Take(1).ToArray(), Array.Empty<RelicModel>()),
                ("empty", Array.Empty<RelicModel>(), Array.Empty<RelicModel>())
            })
            {
                string[]? allocation = null;
                foreach (var reverse in new[] { false, true })
                {
                    RunManager.Instance.CleanUp();
                    relicField.SetValue(shared, reverse ? sharedPool.Reverse().ToArray() : sharedPool);
                    relicField.SetValue(character, reverse ? characterPool.Reverse().ToArray() : characterPool);
                    var run = StartGoldRun();
                    var player = run.Players[0];
                    RelicRewards.Initialize(player);
                    var state = RunCheckpoints.Require(run);
                    var pool = RelicRewards.Pool(player).ToArray();
                    var entries = state.Rewards.Supplies.OrderBy(p => p.Key, StringComparer.Ordinal)
                        .SelectMany(p => p.Value.Positions).Select(p => p.Entry!).ToArray();
                    Assert(entries.Select(e => e.Identity).Order().SequenceEqual(pool.Select(r => r.Id.Entry).Order()),
                        $"{name}: initialization allocates every relic exactly once");
                    var signature = entries.Select(e => e.Identity + e.Address).ToArray();
                    Assert(allocation == null || signature.SequenceEqual(allocation),
                        $"{name}: pool enumeration order preserves allocations and entry addresses");
                    allocation = signature;
                    RelicRewards.Initialize(player);
                    Assert(state.Rewards.Supplies.Sum(p => p.Value.Positions.Count) == pool.Length,
                        $"{name}: initialization is idempotent");

                    var snapshot = RunManager.Instance.ToSave(null);
                    var bytes = RunCheckpoints.Encode(snapshot);
                    var expected = new[] { "generic", "shop", "Courier" }
                        .Select(source => RelicRewards.Draw(player, source, RelicRarity.Common).Id).ToArray();
                    var loaded = RunCheckpoints.Decode(Encoding.UTF8.GetString(bytes), snapshot.SchemaVersion);
                    RunManager.Instance.CleanUp();
                    run = RunState.FromSerializable(loaded);
                    await RunManager.Instance.SetUpSavedSingleplayer(run, loaded);
                    player = run.Players[0];
                    Assert(new[] { "generic", "shop", "Courier" }
                        .Select(source => RelicRewards.Draw(player, source, RelicRarity.Common).Id).SequenceEqual(expected),
                        $"{name}: checkpoint reload reproduces all source draws");

                    RunManager.Instance.CleanUp();
                    run = StartGoldRun();
                    player = run.Players[0];
                    var eligible = RelicRewards.Pool(player).Where(r => r.Rarity != RelicRarity.Shop && r.IsAllowed(run))
                        .Select(r => r.Id).OrderBy(id => id.Entry, StringComparer.Ordinal).ToArray();
                    var drawn = Enumerable.Range(0, eligible.Length)
                        .Select(_ => RelicRewards.Draw(player, "generic", RelicRarity.Common).Id)
                        .OrderBy(id => id.Entry, StringComparer.Ordinal).ToArray();
                    Assert(drawn.SequenceEqual(eligible) &&
                        RelicRewards.Draw(player, "generic", RelicRarity.Common) == RelicFactory.FallbackRelic,
                        $"{name}: borrowing and rarity fallback expose all eligible ordinary stock once, then Circlet");
                }
            }
        }
        finally
        {
            RunManager.Instance.CleanUp();
            relicField.SetValue(shared, sharedRelics);
            relicField.SetValue(character, characterRelics);
        }
    }
}