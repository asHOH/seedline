using System.Text.Json;
using Xunit;

namespace SameSeed.Tests;

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
