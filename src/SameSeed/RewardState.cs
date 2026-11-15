using System.Text.Json;
using System.Text.Json.Serialization;

namespace SameSeed;

internal sealed class RewardState
{
    [JsonRequired] public SortedDictionary<string, ResultSupply> Supplies { get; set; } = new(StringComparer.Ordinal);
    [JsonRequired] public bool RelicsInitialized { get; set; }
    [JsonRequired] public HashSet<string> ChestOffers { get; set; } = new(StringComparer.Ordinal);
    [JsonRequired] public HashSet<string> RetiredRelics { get; set; } = new(StringComparer.Ordinal);
    [JsonRequired] public RareTiming Rare { get; set; } = new();

    internal ResultSupply Supply(RandomAddress address)
    {
        if (!Supplies.TryGetValue(address.Key, out var supply))
            Supplies.Add(address.Key, supply = new());
        return supply;
    }

    internal SupplyEntry Draw(string seed, RandomAddress address, IEnumerable<string> pool, IEnumerable<string> eligible)
    {
        var identities = pool.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var allowed = eligible.ToHashSet(StringComparer.Ordinal);
        allowed.IntersectWith(identities);
        if (allowed.Count == 0)
            throw new InvalidOperationException($"Empty eligible supply: {address.Key}");
        return Supply(address).Draw(allowed, index => new(
            identities[AddressRandom.IntInclusive(seed, address.At(index), 0, identities.Length - 1)],
            address.At(index).Key), remove: allowed.SetEquals(identities))!;
    }

    internal RewardState Copy() => new()
    {
        Supplies = new(Supplies.ToDictionary(p => p.Key, p => p.Value.Copy()), StringComparer.Ordinal),
        RelicsInitialized = RelicsInitialized,
        ChestOffers = new(ChestOffers, StringComparer.Ordinal),
        RetiredRelics = new(RetiredRelics, StringComparer.Ordinal),
        Rare = Rare.Copy()
    };

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("rewards");
        JsonSerializer.Serialize(writer, this);
    }

    internal static RewardState Read(JsonElement element)
    {
        try
        {
            var result = element.Deserialize<RewardState>() ?? throw new InvalidDataException("Missing reward state.");
            result.Validate();
            return result;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or NullReferenceException)
        {
            throw new InvalidDataException("Invalid SameSeed reward state. Save preserved.", error);
        }
    }

    private void Validate()
    {
        if (Supplies == null || ChestOffers == null || RetiredRelics == null || Rare == null ||
            ChestOffers.Concat(RetiredRelics).Any(string.IsNullOrEmpty))
            throw new InvalidDataException("Missing reward state.");
        foreach (var (key, supply) in Supplies)
        {
            RandomAddress.Parse(key);
            if (supply == null || supply.Generated < 0 || supply.Positions == null)
                throw new InvalidDataException("Invalid reward supply.");
            foreach (var position in supply.Positions)
            {
                if (position == null || position.Remaining < -1 ||
                    (position.Entry != null && position.Eligible != null) ||
                    (position.Eligible == null && (position.Carried != null || position.Remaining != -1)) ||
                    (position.Eligible != null && (position.Eligible.Length == 0 || position.Eligible.Any(string.IsNullOrEmpty))))
                    throw new InvalidDataException("Invalid reward supply position.");
                foreach (var entry in new[] { position.Entry, position.Carried }.OfType<SupplyEntry>())
                {
                    if (string.IsNullOrEmpty(entry.Identity))
                        throw new InvalidDataException("Invalid reward identity.");
                    RandomAddress.Parse(entry.Address);
                }
            }
        }
        Rare.Validate();
    }
}

internal sealed class RareTiming
{
    [JsonRequired] public double Luck { get; set; }
    [JsonRequired] public double Offset { get; set; } = -0.05;
    [JsonRequired] public double Threshold { get; set; }
    [JsonRequired] public bool Pending { get; set; }
    [JsonRequired] public int Act { get; set; }
    [JsonRequired] public long NonRareTriples { get; set; }

    internal RareTiming Copy() => (RareTiming)MemberwiseClone();

    internal void Validate()
    {
        if (!double.IsFinite(Luck) || Luck < 0 || !double.IsFinite(Offset) || Offset < -0.05 || Offset > 0.40 ||
            !double.IsFinite(Threshold) || Threshold < 0 || Act < 0 || NonRareTriples < 0 ||
            (Pending && (Threshold == 0 || Luck < Threshold)))
            throw new InvalidDataException("Invalid card rarity state.");
    }

    internal List<int> Simulate(RandomState random, int slots, double baseChance, bool combat, bool scarcity)
    {
        if (Threshold == 0)
            NextThreshold(random);
        var rareSlots = new List<int>();
        var offset = Offset;
        var eligible = false;
        for (var i = 0; i < slots; i++)
        {
            eligible |= !combat || baseChance + offset > 0;
            if (combat)
                offset = Math.Min(0.40, offset + (scarcity ? 0.005 : 0.01));
        }
        for (var i = 0; i < slots; i++)
        {
            var deliverPending = i == 0 && Pending && eligible;
            if (!deliverPending)
            {
                var chance = Math.Max(0, baseChance + (combat ? Offset : 0));
                Luck += -Math.Log(1 - chance);
            }
            if (deliverPending || (Luck >= Threshold && (!scarcity || rareSlots.Count == 0) && !Pending))
            {
                rareSlots.Add(i);
                Luck = 0;
                Pending = false;
                NextThreshold(random);
                if (combat)
                    Offset = -0.05;
            }
            else
            {
                if (Luck >= Threshold)
                    Pending = true;
                if (combat)
                    Offset = Math.Min(0.40, Offset + (scarcity ? 0.005 : 0.01));
            }
        }
        return rareSlots;
    }

    private void NextThreshold(RandomState random) => Threshold = -Math.Log(random.NextOpenUnit(RandomAddress.For("Rare threshold")));
}
