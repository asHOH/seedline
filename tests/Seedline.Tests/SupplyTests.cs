using Xunit;

namespace Seedline.Tests;

public sealed class SupplyTests
{
    [Fact]
    public void UnfilteredInfiniteDrawsConsumePositionsWithoutPendingAdvancement()
    {
        var state = new RewardState();
        var address = RandomAddress.For("unfiltered");
        for (var i = 0; i < 1000; i++)
            Assert.Equal(address.At(i).Key, state.Draw("seed", address, ["A", "B"], ["A", "B"]).Address);
        Assert.Empty(state.Supply(address).Positions);
        Assert.Equal(1000, state.Supply(address).Generated);
    }

    private static ResultSupply Supply(params string[] identities) => new()
    {
        Positions = identities.Select((id, i) => new SupplyPosition
        {
            Entry = new(id, RandomAddress.For("test supply").At(i).Key)
        }).ToList()
    };

    [Fact]
    public void FilteredAdvancementPreservesIneligiblePositionsAndOriginalAddresses()
    {
        var supply = Supply("X", "A", "Y", "B", "Z", "C");
        Assert.Equal("A", supply.Draw(["A", "B", "C"])!.Identity);
        Assert.Equal("X", supply.Draw(["X", "A", "Y", "B", "Z", "C"], remove: true)!.Identity);
        var moved = supply.Draw(["X", "A", "Y", "B", "Z", "C"], remove: true)!;
        Assert.Equal("B", moved.Identity);
        Assert.Equal(RandomAddress.For("test supply").At(3).Key, moved.Address);
        Assert.Equal("Y", supply.Draw(["Y", "Z", "C"], remove: true)!.Identity);
    }

    [Fact]
    public void CollidingFiltersMatchEagerAdvancement()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var random = new Random(seed);
            var ids = Enumerable.Range(0, 24).Select(i => (i % 4).ToString()).ToArray();
            var supply = Supply(ids);
            var eager = supply.Positions.Select(p => p.Entry!).ToList();
            for (var draw = 0; draw < ids.Length; draw++)
            {
                var eligible = Enumerable.Range(0, 4).Where(_ => random.Next(2) == 0).Select(i => i.ToString()).ToHashSet();
                var positions = eager.Select((entry, index) => (entry, index)).Where(x => eligible.Contains(x.entry.Identity)).ToArray();
                var actual = supply.Draw(eligible);
                Assert.Equal(positions.FirstOrDefault().entry, actual);
                if (positions.Length == 0)
                    continue;
                for (var i = 1; i < positions.Length; i++)
                    eager[positions[i - 1].index] = positions[i].entry;
                eager.RemoveAt(positions[^1].index);
            }
        }
    }

    [Fact]
    public void ChestChoiceChangesOnlyTheExpiryResultAndCopiesAreIndependent()
    {
        var skipped = Supply("A", "K", "B", "C");
        skipped.Draw(["A", "B", "C"], lifetime: 1);
        var acquired = skipped.Copy();
        acquired.Remove("A");
        HashSet<string> all = ["A", "K", "B", "C"];
        Assert.Equal("B", skipped.Draw(all, remove: true)!.Identity);
        Assert.Equal("B", acquired.Draw(all, remove: true)!.Identity);
        Assert.Equal("K", skipped.Draw(all, remove: true)!.Identity);
        Assert.Equal("K", acquired.Draw(all, remove: true)!.Identity);
        Assert.Equal("A", skipped.Draw(all, remove: true)!.Identity);
        Assert.Equal("C", acquired.Draw(all, remove: true)!.Identity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChestMarkerBorrowsDonorTailsUntilItsLifetimeExpires(bool acquired)
    {
        var supply = Supply("A", "K");
        var offered = supply.Draw(["A", "B", "C"], lifetime: 2);
        if (acquired)
            supply.Remove("A");
        var donorC = new SupplyEntry("C", RandomAddress.For("donor").At(0).Key);
        var donorB = new SupplyEntry("B", RandomAddress.For("donor").At(3).Key);
        var donorD = new SupplyEntry("D", RandomAddress.For("donor").At(2).Key);
        var donorMarker = new SupplyPosition { Eligible = ["B", "C"] };
        var donor = new ResultSupply
        {
            Positions = [new() { Entry = donorC }, donorMarker, new() { Entry = donorD }, new() { Entry = donorB }]
        };
        SupplyEntry? Draw() => supply.Draw(["A", "B", "C", "K"], borrow: donor.Tail, remove: true);

        Assert.Equal(donorB, Draw());
        Assert.Equal(1, supply.Positions.Single(p => p.Eligible != null).Remaining);
        Assert.Equal("K", Draw()!.Identity);
        Assert.Equal(donorC, Draw());
        Assert.Equal(0, supply.Positions.Single(p => p.Eligible != null).Remaining);
        Assert.Equal(acquired ? null : offered, Draw());
        Assert.Null(Draw());
        Assert.Collection(donor.Positions,
            p => Assert.Same(donorMarker, p), p => Assert.Equal(donorD, p.Entry));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChestMarkerExpiresWhenNeitherLocalNorDonorEntriesCanAdvanceIt(bool acquired)
    {
        var supply = Supply("A", "K");
        var offered = supply.Draw(["A", "B"], lifetime: 5);
        if (acquired)
            supply.Remove("A");
        var donor = Supply("D");
        SupplyEntry? Draw() => supply.Draw(["A", "B", "K"], borrow: donor.Tail, remove: true);

        if (!acquired)
            Assert.Equal(offered, Draw());
        Assert.Equal("K", Draw()!.Identity);
        Assert.Null(Draw());
        Assert.Empty(supply.Positions);
        Assert.Equal("D", Assert.Single(donor.Positions).Entry!.Identity);
    }

    [Fact]
    public void TailSkipsMarkersAndInfiniteEntriesKeepTheirGenerationAddresses()
    {
        var supply = Supply("A", "B", "C");
        supply.Draw(["C"]);
        Assert.Equal("B", supply.Tail(["A", "B", "C"])!.Identity);
        var infinite = new ResultSupply();
        SupplyEntry Generate(long i) => new(i % 2 == 0 ? "A" : "B", RandomAddress.For("infinite").At(i).Key);
        Assert.Equal(Generate(1), infinite.Draw(["B"], Generate));
        Assert.Equal(Generate(3), infinite.Draw(["B"], Generate));
        Assert.Equal(Generate(0), infinite.Draw(["A", "B"], Generate, remove: true));
        Assert.Equal(Generate(5), infinite.Draw(["A", "B"], Generate, remove: true));
    }
}
