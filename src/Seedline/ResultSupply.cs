namespace Seedline;

internal sealed record SupplyEntry(string Identity, string Address);

internal sealed class SupplyPosition
{
    public SupplyEntry? Entry { get; set; }
    public string[]? Eligible { get; set; }
    public int Remaining { get; set; } = -1;
    public SupplyEntry? Carried { get; set; }

    internal SupplyPosition Copy() => new()
    {
        Entry = Entry,
        Eligible = Eligible?.ToArray(),
        Remaining = Remaining,
        Carried = Carried
    };
}

// Filters are frozen identity sets so later eligibility changes cannot rewrite an earlier advancement.
internal sealed class ResultSupply
{
    public List<SupplyPosition> Positions { get; set; } = [];
    public long Generated { get; set; }

    internal ResultSupply Copy() => new() { Generated = Generated, Positions = Positions.Select(p => p.Copy()).ToList() };

    internal SupplyEntry? Draw(HashSet<string> eligible, Func<long, SupplyEntry>? generate = null,
        Func<HashSet<string>, SupplyEntry?>? borrow = null, bool remove = false, int lifetime = -1)
    {
        if (eligible.Count == 0)
            return null;
        for (var i = 0; ; i++)
        {
            if (!Ensure(i, generate))
            {
                var borrowed = borrow?.Invoke(eligible);
                if (borrowed == null)
                    return null;
                Positions.Add(new() { Entry = borrowed });
            }
            Resolve(i, generate, borrow);
            if (Positions[i].Entry is not { } entry || !eligible.Contains(entry.Identity))
                continue;
            if (remove)
                Positions.RemoveAt(i);
            else
                Positions[i] = new()
                {
                    Eligible = eligible.Order(StringComparer.Ordinal).ToArray(),
                    Remaining = lifetime,
                    Carried = lifetime >= 0 ? entry : null
                };
            Compact();
            return entry;
        }
    }

    internal SupplyEntry? Tail(HashSet<string> eligible)
    {
        for (var i = Positions.Count - 1; i >= 0; i--)
        {
            if (Positions[i].Entry is not { } entry || !eligible.Contains(entry.Identity))
                continue;
            Positions.RemoveAt(i);
            return entry;
        }
        return null;
    }

    internal void Remove(string identity)
    {
        foreach (var position in Positions)
        {
            if (position.Entry?.Identity == identity)
                position.Entry = null;
            if (position.Carried?.Identity == identity)
                position.Carried = null;
        }
        Compact();
    }

    private bool Ensure(int index, Func<long, SupplyEntry>? generate)
    {
        if (index < Positions.Count)
            return true;
        if (generate == null)
            return false;
        var entry = generate(Generated);
        Generated = checked(Generated + 1);
        Positions.Add(new() { Entry = entry });
        return true;
    }

    private void Resolve(int index, Func<long, SupplyEntry>? generate, Func<HashSet<string>, SupplyEntry?>? borrow)
    {
        var marker = Positions[index];
        if (marker.Eligible == null)
            return;
        if (marker.Remaining == 0)
        {
            Positions[index] = new() { Entry = marker.Carried };
            return;
        }
        var eligible = marker.Eligible.ToHashSet(StringComparer.Ordinal);
        for (var next = index + 1; ; next++)
        {
            if (!Ensure(next, generate))
            {
                var borrowed = borrow?.Invoke(eligible);
                if (borrowed == null)
                {
                    Positions[index] = new() { Entry = marker.Carried };
                    return;
                }
                Positions.Add(new() { Entry = borrowed });
            }
            Resolve(next, generate, borrow);
            if (Positions[next].Entry is not { } entry || !eligible.Contains(entry.Identity))
                continue;
            Positions[index] = new() { Entry = entry };
            if (marker.Remaining > 0)
                marker.Remaining--;
            Positions[next] = marker;
            return;
        }
    }

    private void Compact() => Positions.RemoveAll(p => p.Entry == null && p.Eligible == null);
}
