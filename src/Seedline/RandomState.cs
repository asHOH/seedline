using System.Text.Json;

namespace Seedline;

internal sealed class RandomState(string seed)
{
    private readonly Dictionary<string, long> positions = new(StringComparer.Ordinal);

    internal long Position(RandomAddress address) => positions.GetValueOrDefault(address.Key);

    internal void Commit(RandomAddress address, RandomState preview) => positions[address.Key] = preview.Position(address);

    // Call only when the source policy commits an opportunity; addresses remain zero-based.
    internal long Advance(RandomAddress address)
    {
        var position = Position(address);
        positions[address.Key] = checked(position + 1);
        return position;
    }

    internal ulong NextUInt64(RandomAddress address)
    {
        var value = AddressRandom.UInt64(seed, address.At(Position(address)));
        Advance(address);
        return value;
    }

    internal double NextUnit(RandomAddress address) => AddressRandom.Unit(NextUInt64(address));

    internal double NextOpenUnit(RandomAddress address) => AddressRandom.OpenUnit(NextUInt64(address));

    internal int NextIntInclusive(RandomAddress address, int minimum, int maximum)
    {
        var value = AddressRandom.IntInclusive(seed, address.At(Position(address)), minimum, maximum);
        Advance(address);
        return value;
    }

    internal RandomState Copy()
    {
        var copy = new RandomState(seed);
        foreach (var pair in positions)
            copy.positions.Add(pair.Key, pair.Value);
        return copy;
    }

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("random_positions");
        foreach (var pair in positions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            writer.WriteNumber(pair.Key, pair.Value);
        writer.WriteEndObject();
    }

    internal static RandomState Read(string seed, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Seedline random positions are missing or invalid.");
        var state = new RandomState(seed);
        foreach (var property in element.EnumerateObject())
        {
            var address = RandomAddress.Parse(property.Name);
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt64(out var position) ||
                position < 0 || !state.positions.TryAdd(address.Key, position))
                throw new InvalidDataException("Invalid or duplicate Seedline random position.");
        }
        return state;
    }
}
