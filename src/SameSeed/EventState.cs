using System.Text.Json;
using System.Text.Json.Serialization;

namespace SameSeed;

internal sealed class EventCondition
{
    [JsonRequired] public List<string> Order { get; set; } = [];
    [JsonRequired] public int Position { get; set; }
    [JsonRequired] public int Remaining { get; set; }
    [JsonRequired] public long Roll { get; set; }
    [JsonRequired] public double Priority { get; set; }
    internal EventCondition Copy() => new() { Order = [.. Order], Position = Position, Remaining = Remaining, Roll = Roll, Priority = Priority };
}

internal sealed class EventState
{
    [JsonRequired] public int Act { get; set; } = -1;
    [JsonRequired] public long Ordinal { get; set; }
    [JsonRequired] public SortedDictionary<string, EventCondition> Conditions { get; set; } = new(StringComparer.Ordinal);

    internal void Enter(string seed, int act, IReadOnlyDictionary<string, string[]> initialOrders, HashSet<string> available, HashSet<string> visited)
    {
        foreach (var (key, initial) in initialOrders)
        {
            Conditions.TryGetValue(key, out var previous);
            var survivors = previous?.Order.Where(id => available.Contains(id) && !visited.Contains(id)).ToArray() ?? [];
            var incoming = initial.Where(id => available.Contains(id) && !visited.Contains(id) && !survivors.Contains(id)).ToArray();
            var order = act == 0 ? incoming : Merge(seed, act, key, survivors, incoming);
            Conditions[key] = new() { Order = [.. order], Remaining = order.Length };
        }
        Act = act;
        Ordinal = 0;
        if (act == 0)
            Initialize(seed, resetRolls: true);
    }

    internal static string[] Merge(string seed, int act, string condition, string[] survivors, string[] incoming)
    {
        IEnumerable<(string Id, double Priority)> Assign(string[] ids, string cohort)
        {
            var priorities = Enumerable.Range(0, ids.Length).Select(i => AddressRandom.Unit(AddressRandom.UInt64(seed,
                RandomAddress.For("insertion", condition, cohort).At(act).At(i)))).Order().ToArray();
            return ids.Select((id, i) => (id, priorities[i]));
        }
        return Assign(survivors, "surviving").Concat(Assign(incoming, "incoming"))
            .OrderBy(p => p.Priority).ThenBy(p => p.Id, StringComparer.Ordinal).Select(p => p.Id).ToArray();
    }

    internal string? Select(string seed, HashSet<string> visited, Func<string, bool> conditionPasses, Func<string, bool> eligible)
    {
        Ordinal++;
        if (Act > 0)
            Initialize(seed, resetRolls: true);
        while (true)
        {
            var next = Conditions.Where(p => p.Value.Remaining > 0).OrderBy(p => p.Value.Priority)
                .ThenBy(p => p.Key, StringComparer.Ordinal).FirstOrDefault();
            if (next.Value == null)
            {
                if (!Conditions.Values.SelectMany(c => c.Order).Any(id => !visited.Contains(id) && eligible(id)))
                    return null;
                foreach (var condition in Conditions.Values)
                {
                    condition.Position = 0;
                    condition.Remaining = condition.Order.Count(id => !visited.Contains(id));
                }
                Initialize(seed, resetRolls: false);
                continue;
            }
            var value = next.Value;
            value.Remaining--;
            string? selected = null;
            if (conditionPasses(next.Key))
            {
                while (value.Position < value.Order.Count && visited.Contains(value.Order[value.Position]))
                    value.Position++;
                if (value.Position < value.Order.Count)
                {
                    var id = value.Order[value.Position++];
                    if (eligible(id))
                        selected = id;
                }
            }
            if (value.Remaining > 0 && (selected == null || Act == 0))
                value.Priority += Increment(seed, next.Key, value);
            if (selected != null)
                return selected;
        }
    }

    private void Initialize(string seed, bool resetRolls)
    {
        foreach (var (key, condition) in Conditions)
        {
            if (resetRolls)
                condition.Roll = 0;
            condition.Priority = condition.Remaining > 0 ? Increment(seed, key, condition) : 0;
        }
    }

    private double Increment(string seed, string key, EventCondition condition)
    {
        var address = RandomAddress.For("condition selection", key);
        if (Act > 0)
            address = address.At(Act).At(Ordinal - 1);
        return -Math.Log(AddressRandom.OpenUnit(AddressRandom.UInt64(seed, address.At(condition.Roll++)))) / condition.Remaining;
    }

    internal EventState Copy() => new() { Act = Act, Ordinal = Ordinal, Conditions = new(Conditions.ToDictionary(p => p.Key, p => p.Value.Copy()), StringComparer.Ordinal) };

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("events");
        JsonSerializer.Serialize(writer, this);
    }

    internal static EventState Read(JsonElement element)
    {
        try
        {
            var state = element.Deserialize<EventState>() ?? throw new InvalidDataException("Missing event state.");
            if (state.Act is < -1 or > 2 || state.Ordinal < 0 || state.Conditions == null || state.Conditions.Any(p =>
                string.IsNullOrEmpty(p.Key) || p.Value == null || p.Value.Order == null || p.Value.Order.Any(string.IsNullOrEmpty) ||
                p.Value.Order.Distinct(StringComparer.Ordinal).Count() != p.Value.Order.Count || p.Value.Position < 0 || p.Value.Position > p.Value.Order.Count ||
                p.Value.Remaining < 0 || p.Value.Remaining > p.Value.Order.Count || p.Value.Roll < 0 || !double.IsFinite(p.Value.Priority) || p.Value.Priority < 0))
                throw new InvalidDataException("Invalid event selection state.");
            return state;
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Invalid event selection state. Save preserved.", error);
        }
    }
}
