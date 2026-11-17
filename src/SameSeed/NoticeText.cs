using System.Text.Json;
using MegaCrit.Sts2.Core.Localization;

namespace SameSeed;

internal static class NoticeText
{
    private static readonly Dictionary<string, Dictionary<string, string>> tables = new(StringComparer.Ordinal);

    internal static string Get(string key, params object[] arguments)
    {
        var locale = LocManager.Instance;
        return string.Format(locale.CultureInfo, GetTemplate(locale.Language, key), arguments);
    }

    internal static string GetTemplate(string language, string key) =>
        Table(language).GetValueOrDefault(key) ?? Table("eng")[key];

    private static Dictionary<string, string> Table(string language)
    {
        if (tables.TryGetValue(language, out var table))
            return table;
        using var stream = typeof(NoticeText).Assembly.GetManifestResourceStream($"SameSeed.Localization.{language}.json");
        table = stream == null ? new(StringComparer.Ordinal) : JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        tables.Add(language, table);
        return table;
    }
}
