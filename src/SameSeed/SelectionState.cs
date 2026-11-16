using System.Text.Json;
using System.Text.Json.Serialization;

namespace SameSeed;

internal sealed class SelectionClock
{
    [JsonRequired] public double Remaining { get; set; }
    [JsonRequired] public long Renewals { get; set; }
    internal SelectionClock Copy() => (SelectionClock)MemberwiseClone();
}

internal sealed class SelectionState
{
    [JsonRequired] public SortedDictionary<string, SelectionClock> Clocks { get; set; } = new(StringComparer.Ordinal);

    internal SelectionClock Get(string seed, RandomAddress scope, string key)
    {
        var address = scope.With(key);
        if (!Clocks.TryGetValue(address.Key, out var clock))
            Clocks.Add(address.Key, clock = new() { Remaining = Roll(seed, address, 0) });
        return clock;
    }

    private static double Roll(string seed, RandomAddress address, long renewal) =>
        -Math.Log(AddressRandom.OpenUnit(AddressRandom.UInt64(seed, address.At(renewal))));

    internal void Renew(string seed, RandomAddress scope, string key)
    {
        var clock = Get(seed, scope, key);
        clock.Renewals = checked(clock.Renewals + 1);
        clock.Remaining = Roll(seed, scope.With(key), clock.Renewals);
    }

    internal string[] Select(string seed, RandomAddress scope, IEnumerable<string> eligible, int count)
    {
        if (count <= 0)
            return [];
        var ordered = eligible.Distinct(StringComparer.Ordinal)
            .Select(key => (Key: key, Clock: Get(seed, scope, key)))
            .OrderBy(p => p.Clock.Remaining).ThenBy(p => p.Key, StringComparer.Ordinal).ToArray();
        count = Math.Min(count, ordered.Length);
        if (count == 0)
            return [];
        var minimum = ordered[count - 1].Clock.Remaining;
        for (var i = 0; i < ordered.Length; i++)
            ordered[i].Clock.Remaining = i < count ? 0 : Math.Max(0, ordered[i].Clock.Remaining - minimum);
        return ordered.Take(count).Select(p => p.Key).ToArray();
    }

    internal SelectionState Copy() => new()
    {
        Clocks = new(Clocks.ToDictionary(p => p.Key, p => p.Value.Copy()), StringComparer.Ordinal)
    };

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("selection");
        JsonSerializer.Serialize(writer, this);
    }

    internal static SelectionState Read(JsonElement element)
    {
        try
        {
            var state = element.Deserialize<SelectionState>() ?? throw new InvalidDataException("Missing selection state.");
            if (state.Clocks == null)
                throw new InvalidDataException("Missing selection clocks.");
            foreach (var (key, clock) in state.Clocks)
            {
                RandomAddress.Parse(key);
                if (clock == null || !double.IsFinite(clock.Remaining) || clock.Remaining < 0 || clock.Renewals < 0)
                    throw new InvalidDataException("Invalid selection clock.");
            }
            return state;
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Invalid SameSeed selection state. Save preserved.", error);
        }
    }
}
