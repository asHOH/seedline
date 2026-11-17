using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Seedline;

internal sealed record RandomAddress
{
    internal string Key { get; }

    private RandomAddress(string key) => Key = key;

    internal static RandomAddress For(string purpose, params string[] scope)
    {
        var address = new RandomAddress("[" + EncodeId(purpose) + "]");
        foreach (var id in scope)
            address = address.With(id);
        return address;
    }

    internal RandomAddress With(string id) => new(Key[..^1] + "," + EncodeId(id) + "]");

    internal RandomAddress At(long ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        return new(Key[..^1] + "," + ordinal.ToString(CultureInfo.InvariantCulture) + "]");
    }

    internal static RandomAddress Parse(string key)
    {
        try
        {
            using var document = JsonDocument.Parse(key);
            var parts = document.RootElement;
            if (parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0)
                throw new InvalidDataException("Invalid random address.");
            var address = For(parts[0].GetString()!);
            foreach (var part in parts.EnumerateArray().Skip(1))
                address = part.ValueKind == JsonValueKind.String ? address.With(part.GetString()!) : address.At(part.GetInt64());
            if (address.Key != key)
                throw new InvalidDataException("Noncanonical random address.");
            return address;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        {
            throw new InvalidDataException("Invalid random address.", error);
        }
    }

    private static string EncodeId(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        // Reject invalid Unicode before JSON encoding could replace it and alias another ID.
        _ = new UTF8Encoding(false, true).GetByteCount(id);
        return JsonSerializer.Serialize(id);
    }
}
