using System.Text.Json;
using Xunit;

namespace Seedline.Tests;

public sealed class SelectionTests
{
    private static readonly RandomAddress scope = RandomAddress.For("selection test");

    [Fact]
    public void BatchUsesOneSnapshotAndLeavesInactiveClocksAlone()
    {
        var state = new SelectionState();
        foreach (var (key, value) in new[] { ("A", 0.5), ("B", 0.6), ("C", 0.7), ("D", 0.8) })
            state.Get("seed", scope, key).Remaining = value;
        var reversed = state.Copy();
        Assert.Equal(["A", "C"], state.Select("seed", scope, ["C", "A", "D"], 2));
        Assert.Equal(["A", "C"], reversed.Select("seed", scope, ["D", "A", "C"], 2));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(reversed));
        Assert.Equal(0, state.Get("seed", scope, "A").Remaining);
        Assert.Equal(0.6, state.Get("seed", scope, "B").Remaining);
        Assert.Equal(0.1, state.Get("seed", scope, "D").Remaining, 12);
    }

    [Fact]
    public void RenewalAndReloadPreserveIndependentOccurrenceHistory()
    {
        var state = new SelectionState();
        state.Select("seed", scope, ["A", "B", "C"], 1);
        var frozen = state.Copy();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(state));
        var loaded = SelectionState.Read(json.RootElement);
        state.Renew("seed", scope, "A");
        loaded.Renew("seed", scope, "A");
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(loaded));
        Assert.Equal(0, frozen.Get("seed", scope, "A").Renewals);
        Assert.Equal(1, state.Get("seed", scope, "A").Renewals);
        Assert.NotEqual(state.Get("seed", scope, "A").Remaining, state.Get("seed", scope.With("other"), "A").Remaining);
    }

    [Fact]
    public void BatchMatchesSequentialCountdownAcrossTiesAndEligibilitySets()
    {
        string[] keys = ["A", "B", "C", "D"];
        // Exhaust every assignment of clocks 0, 1 and 2, including ties and consumed clocks.
        for (var assignment = 0; assignment < 81; assignment++)
        {
            var initial = new SelectionState();
            var values = assignment;
            foreach (var key in keys)
            {
                initial.Get("seed", scope, key).Remaining = values % 3;
                values /= 3;
            }
            for (var mask = 0; mask < 16; mask++)
            {
                for (var count = 0; count <= keys.Length; count++)
                {
                    var eligible = keys.Where((_, index) => (mask & (1 << index)) != 0).ToList();
                    var batch = initial.Copy();
                    var sequential = initial.Copy();
                    var selected = batch.Select("seed", scope, eligible.AsEnumerable().Reverse(), count);
                    List<string> expected = [];
                    while (expected.Count < count && eligible.Count > 0)
                    {
                        var winner = sequential.Select("seed", scope, eligible, 1)[0];
                        expected.Add(winner);
                        eligible.Remove(winner);
                    }
                    Assert.Equal(expected, selected);
                    Assert.Equal(JsonSerializer.Serialize(sequential), JsonSerializer.Serialize(batch));
                }
            }
        }
    }

    [Fact]
    public void SharedPrioritiesPreserveNestedAgreementAndCopyWeight()
    {
        var matches = 0;
        var duplicateWins = 0;
        foreach (var rank in ThreeCardRanks())
        {
            var initial = RankedState(rank);
            var full = initial.Copy().Select("seed", scope, ["A", "B", "C"], 1)[0];
            var subset = initial.Copy().Select("seed", scope, ["A", "B"], 1)[0];
            matches += full == subset ? 1 : 0;
            duplicateWins += full != "C" ? 1 : 0;
        }
        Assert.Equal(4, matches);
        Assert.Equal(4, duplicateWins);
    }

    [Fact]
    public void IndependentRoutingPreservesOneStepIdentityProbabilities()
    {
        int[] fixedCounts = new int[3], routedCounts = new int[3];
        foreach (var downstreamRank in ThreeCardRanks())
        {
            var target = RankedState(downstreamRank).Select("seed", scope, ["A", "B", "C"], 1)[0];
            // A/B are equivalent Strikes; C is a Defend. Routing upgrades one Strike.
            int Outcome(string upgraded) => target == "C" ? 0 : target == upgraded ? 1 : 2;
            fixedCounts[Outcome("A")]++;
            foreach (var routingRank in ThreeCardRanks())
            {
                var upgraded = RankedState(routingRank).Select("seed", scope, ["A", "B"], 1)[0];
                routedCounts[Outcome(upgraded)]++;
            }
        }
        Assert.Equal([2, 2, 2], fixedCounts);
        Assert.Equal(fixedCounts.Select(count => count * 6), routedCounts);
    }

    private static IEnumerable<string[]> ThreeCardRanks()
    {
        string[] keys = ["A", "B", "C"];
        return from first in keys
               from second in keys
               where first != second
               select new[] { first, second, keys.Single(key => key != first && key != second) };
    }

    private static SelectionState RankedState(string[] rank)
    {
        var state = new SelectionState();
        for (var index = 0; index < rank.Length; index++)
            state.Get("seed", scope, rank[index]).Remaining = index + 1;
        return state;
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Clocks\":null}")]
    [InlineData("{\"Clocks\":{\"bad\":{\"Remaining\":1,\"Renewals\":0}}}")]
    [InlineData("{\"Clocks\":{\"[\\\"valid\\\"]\":{\"Remaining\":-1,\"Renewals\":0}}}")]
    [InlineData("{\"Clocks\":{\"[\\\"valid\\\"]\":{\"Remaining\":0,\"Renewals\":-1}}}")]
    public void InvalidSavedClocksAreRejected(string text)
    {
        using var json = JsonDocument.Parse(text);
        Assert.Throws<InvalidDataException>(() => SelectionState.Read(json.RootElement));
    }
}
