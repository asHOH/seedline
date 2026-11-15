using System.Globalization;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SameSeed.Tests;

public sealed class RandomTests
{
    private static readonly RandomAddress gold = RandomAddress.For("gold reward", "normal");

    [Theory]
    [InlineData("SAMESEED", 0, 6329520721204281019UL)]
    [InlineData("SAMESEED", 1, 14946665980611961296UL)]
    [InlineData("ANOTHER", 0, 13665997746676760749UL)]
    public void DerivationMatchesIndependentSha256Vectors(string seed, long ordinal, ulong expected) =>
        Assert.Equal(expected, AddressRandom.UInt64(seed, gold.At(ordinal)));

    [Fact]
    public void AddressesSeparateTypesBoundariesAndScopes()
    {
        RandomAddress[] addresses =
        [
            RandomAddress.For("ab", "c"), RandomAddress.For("a", "bc"),
            RandomAddress.For("a/b", "c"), RandomAddress.For("a", "b/c"),
            gold.At(1), gold.With("1"), gold.At(1).At(2), gold.At(2).At(1),
            RandomAddress.For("price", "normal").At(1), RandomAddress.For("gold reward", "elite").At(1),
            RandomAddress.For("奖励", "a\"b", "c\\d", "\n").At(long.MaxValue)
        ];
        Assert.Equal(addresses.Length, addresses.Distinct().Count());
        Assert.Equal(addresses.Length, addresses.Select(a => AddressRandom.UInt64("SEED", a)).Distinct().Count());
        foreach (var address in addresses)
            Assert.Equal(address, RandomAddress.Parse(address.Key));
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    [InlineData("fr-FR")]
    public void AddressesAndRollsAreCultureIndependent(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal("[\"gold reward\",\"normal\",1234]", gold.At(1234).Key);
            Assert.Equal(6329520721204281019UL, AddressRandom.UInt64("SAMESEED", gold.At(0)));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void UnitIntervalsRespectTheirEndpoints()
    {
        Assert.Equal(0, AddressRandom.Unit(0));
        Assert.Equal(1 - Math.Pow(2, -53), AddressRandom.Unit(ulong.MaxValue));
        Assert.Equal(Math.Pow(2, -53), AddressRandom.OpenUnit(0));
        Assert.Equal(1 - Math.Pow(2, -53), AddressRandom.OpenUnit(ulong.MaxValue));
    }

    [Fact]
    public void IntegerSamplingCoversInclusiveBoundsAndFullSignedRange()
    {
        var seen = new HashSet<int>();
        for (var i = 0; i < 1024; i++)
        {
            var value = AddressRandom.IntInclusive("SEED", gold.At(i), 7, 15);
            Assert.InRange(value, 7, 15);
            seen.Add(value);
            var full = AddressRandom.IntInclusive("SEED", gold.At(i), int.MinValue, int.MaxValue);
            var bits = AddressRandom.UInt64("SEED", gold.At(i));
            Assert.Equal((int)(int.MinValue + (long)(bits % (1UL << 32))), full);
        }
        Assert.Equal(Enumerable.Range(7, 9), seen.Order());
        Assert.Equal(int.MaxValue, AddressRandom.IntInclusive("SEED", gold, int.MaxValue, int.MaxValue));
    }

    [Fact]
    public void UnrelatedConsumptionAndPreviewLeaveSequenceUnchanged()
    {
        var left = new RandomState("SEED");
        var right = new RandomState("SEED");
        for (var i = 0; i < 40; i++)
        {
            right.NextUnit(RandomAddress.For("shop", "cards"));
            right.Advance(RandomAddress.For("combat", "act").At(2));
            var preview = AddressRandom.UInt64("SEED", gold.At(i));
            Assert.Equal(i, left.Position(gold));
            Assert.Equal(preview, left.NextUInt64(gold));
            Assert.Equal(preview, right.NextUInt64(gold));
        }
    }

    [Fact]
    public void FailedDrawAndCounterOverflowPreserveState()
    {
        var state = new RandomState("SEED");
        Assert.Throws<ArgumentOutOfRangeException>(() => state.NextIntInclusive(gold, 8, 7));
        Assert.Equal(0, state.Position(gold));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, long> { [gold.Key] = long.MaxValue }));
        var exhausted = RandomState.Read("SEED", json.RootElement);
        Assert.Throws<OverflowException>(() => exhausted.NextUInt64(gold));
        Assert.Equal(long.MaxValue, exhausted.Position(gold));
    }

    [Fact]
    public void SnapshotCopiesReplayFromCheckpointAndContinueIndependently()
    {
        var state = new RandomState("SEED");
        state.NextUInt64(gold);
        var before = state.Copy();
        var generated = state.NextUInt64(gold);
        var after = state.Copy();
        var following = state.NextUInt64(gold);
        Assert.Equal(generated, before.NextUInt64(gold));
        Assert.Equal(following, before.NextUInt64(gold));
        Assert.Equal(following, after.NextUInt64(gold));
        state.Advance(gold);
        Assert.Equal(3, after.Position(gold));
    }

    [Fact]
    public void SerializationIsOrderedAndRestoresAllStreams()
    {
        var first = new RandomState("SEED");
        var second = new RandomState("SEED");
        var other = RandomAddress.For("another").At(1);
        first.NextUnit(gold);
        first.NextOpenUnit(other);
        second.NextOpenUnit(other);
        second.NextUnit(gold);
        Assert.Equal(Encode(first), Encode(second));
        using var document = JsonDocument.Parse(Encode(first));
        var restored = RandomState.Read("SEED", document.RootElement.GetProperty("random_positions"));
        Assert.Equal(first.NextUInt64(gold), restored.NextUInt64(gold));
        Assert.Equal(first.NextUInt64(other), restored.NextUInt64(other));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"bad\":0}")]
    [InlineData("{\"[\\\"p\\\"]\":-1}")]
    [InlineData("{\"[\\\"p\\\"]\":1.5}")]
    [InlineData("{\"[\\\"p\\\"]\":9223372036854775808}")]
    [InlineData("{\"[\\\"p\\\"]\":null}")]
    [InlineData("{\"[\\\"p\\\"]\":\"1\"}")]
    [InlineData("{\"[\\\"p\\\"]\":0,\"[\\\"p\\\"]\":1}")]
    public void CorruptStateIsRejected(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => RandomState.Read("SEED", document.RootElement));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[null]")]
    [InlineData("[\"\"]")]
    [InlineData("[1]")]
    [InlineData("[\"p\",-1]")]
    [InlineData("[\"p\",1.5]")]
    [InlineData("[\"p\",{}]")]
    [InlineData("[\"p\",true]")]
    [InlineData("[\"p\", 1]")]
    public void InvalidAddressesAreRejected(string address) =>
        Assert.Throws<InvalidDataException>(() => RandomAddress.Parse(address));

    [Fact]
    public void InvalidIdsCannotAliasValidIds()
    {
        Assert.Throws<ArgumentException>(() => RandomAddress.For(""));
        Assert.Throws<EncoderFallbackException>(() => RandomAddress.For("\ud800"));
        Assert.Throws<ArgumentOutOfRangeException>(() => gold.At(-1));
    }

    private static string Encode(RandomState state)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            state.Write(writer);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
