using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SameSeed;

internal static class AddressRandom
{
    internal static ulong UInt64(string seed, RandomAddress address) => Sample(seed, address, 0);

    internal static double Unit(ulong value) => (value >> 11) * (1.0 / (1UL << 53));

    internal static double OpenUnit(ulong value) => ((value >> 12) + 0.5) * (1.0 / (1UL << 52));

    internal static int IntInclusive(string seed, RandomAddress address, int minimum, int maximum)
    {
        if (minimum > maximum)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        var width = (ulong)((long)maximum - minimum + 1);
        var threshold = unchecked(0UL - width) % width;
        for (long attempt = 0; ; attempt = checked(attempt + 1))
        {
            var value = Sample(seed, address, attempt);
            if (value >= threshold)
                return (int)(minimum + (long)(value % width));
        }
    }

    private static ulong Sample(string seed, RandomAddress address, long attempt)
    {
        ArgumentException.ThrowIfNullOrEmpty(seed);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), leaveOpen: true);
        // BinaryWriter length-prefixes UTF-8 strings; integers and hash extraction are little-endian.
        writer.Write("SameSeed/random/1");
        writer.Write(seed);
        writer.Write(address.Key);
        writer.Write(attempt);
        writer.Flush();
        return BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(stream.GetBuffer().AsSpan(0, (int)stream.Length)));
    }
}
