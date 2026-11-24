using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Seedline.Tests;

public sealed class DecisionRecorderTests : IDisposable
{
    private readonly string directory = Path.Combine(AppContext.BaseDirectory, "trace-tests", Guid.NewGuid().ToString("N"));

    private static string ReadText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void FlushesRotatesAndAppendsAcrossRestarts()
    {
        const int limit = 512;
        using (var writer = new DecisionRecorder(() => directory, e => throw e, limit))
            for (var i = 0; i < 15; i++)
            {
                writer.Write(() => new { index = i, text = new string('a', 100) });
                Assert.Contains($"\"index\":\"{i}\"", ReadText(Path.Combine(directory, "decisions.jsonl")));
            }
        Assert.Equal(2, Directory.GetFiles(directory).Length);
        Assert.All(Directory.GetFiles(directory), path => Assert.InRange(new FileInfo(path).Length, 1, limit));
        var retained = File.ReadAllText(Path.Combine(directory, "decisions.jsonl"));
        using (var writer = new DecisionRecorder(() => directory, e => throw e, limit))
            writer.Write(() => new { index = 15 });
        Assert.StartsWith(retained, File.ReadAllText(Path.Combine(directory, "decisions.jsonl")));
        foreach (var path in Directory.GetFiles(directory))
            foreach (var line in File.ReadLines(path))
                using (JsonDocument.Parse(line)) { }
    }

    [Fact]
    public void DiagnosticNumbersSurviveTextSanitization()
    {
        using var writer = new DecisionRecorder(() => directory, e => throw e);
        writer.Write(() => new { seed = "76561123456789012", value = 76561123456789012UL, fraction = 0.76561123456789012, address = "[\"value\",76561123456789012]" });
        var text = ReadText(Path.Combine(directory, "decisions.jsonl"));
        Assert.DoesNotMatch(new Regex(@"\b76561\d{12}\b"), text);
        using var json = JsonDocument.Parse(text);
        Assert.Equal("76561123456789012", json.RootElement.GetProperty("seed").GetString());
        Assert.Equal("76561123456789012", json.RootElement.GetProperty("value").GetString());
        Assert.Equal("[\"value\",76561123456789012]", json.RootElement.GetProperty("address").GetString());
    }

    [Fact]
    public void FailureStopsOnceWithoutEvaluatingLaterRecords()
    {
        Directory.CreateDirectory(directory);
        var blocked = Path.Combine(directory, "file-not-directory");
        File.WriteAllText(blocked, "occupied");
        var warnings = 0;
        using var writer = new DecisionRecorder(() => blocked, _ => warnings++);
        writer.Write(() => new { first = true });
        writer.Write(() => throw new InvalidOperationException("must not evaluate"));
        Assert.Equal(1, warnings);
        Assert.Equal("occupied", File.ReadAllText(blocked));
    }

    [Fact]
    public void SerializationFailureCannotEscapeIntoGameplay()
    {
        var warnings = 0;
        using var writer = new DecisionRecorder(() => directory, _ => warnings++);
        writer.Write(() => throw new InvalidOperationException("diagnostic projection"));
        writer.Write(() => new { after = true });
        Assert.Equal(1, warnings);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void InterruptedAndOversizedRecordsDoNotHideFollowingRecords()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "decisions.jsonl");
        File.WriteAllText(path, "{\"interrupted\":");
        using var writer = new DecisionRecorder(() => directory, e => throw e, 256);
        writer.Write(() => new { next = true });
        var lines = ReadText(path).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using (var json = JsonDocument.Parse(lines[1]))
            Assert.True(json.RootElement.GetProperty("next").GetBoolean());
        writer.Write(() => new { huge = new string('x', 1000) });
        Assert.Contains("record-too-large", ReadText(path));
        Assert.InRange(new FileInfo(path).Length, 1, 256);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
