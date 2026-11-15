using System.Text.Json;
using Xunit;

namespace SameSeed.Tests;

public sealed class RareTimingTests
{
    [Fact]
    public void PendingRareWaitsThroughBothScarcityOpeningOffers()
    {
        var state = new RareTiming { Threshold = 0.01, Luck = 0.01, Pending = true };
        var random = new RandomState("pending");
        Assert.Empty(state.Simulate(random, 3, 0.0149, true, true));
        Assert.Empty(state.Simulate(random, 3, 0.0149, true, true));
        Assert.True(state.Pending);
        Assert.Equal(0, Assert.Single(state.Simulate(random, 3, 0.0149, true, true)));
        Assert.False(state.Pending);
        Assert.Equal(-0.04, state.Offset, 10);
    }

    [Fact]
    public void FixedBaseOffersPreserveOffsetAndScarcityCapsDelivery()
    {
        var state = new RareTiming { Threshold = 0.000001, Offset = 0.30 };
        var random = new RandomState("base");
        Assert.Single(state.Simulate(random, 5, 0.9, false, true));
        Assert.True(state.Pending);
        Assert.Equal(0.30, state.Offset);
        var snapshot = state.Copy();
        var replay = random.Copy();
        Assert.Equal(state.Simulate(random, 5, 0.03, false, true), snapshot.Simulate(replay, 5, 0.03, false, true));
        Assert.Equal(state.Luck, snapshot.Luck);
        Assert.Equal(state.Threshold, snapshot.Threshold);
    }

    [Fact]
    public void BelowScarcityDeliversAllCrossings()
    {
        var state = new RareTiming { Threshold = 0.000001 };
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, state.Simulate(new RandomState("many"), 5, 0.999999, false, false));
        Assert.False(state.Pending);
    }

    [Fact]
    public void RewardStateRejectsInvalidPendingThresholdAndMissingFields()
    {
        var state = new RewardState();
        state.Rare.Pending = true;
        Assert.Throws<InvalidDataException>(() => RewardState.Read(JsonSerializer.SerializeToElement(state)));
        Assert.Throws<InvalidDataException>(() => RewardState.Read(JsonSerializer.SerializeToElement(new { })));
    }
}
