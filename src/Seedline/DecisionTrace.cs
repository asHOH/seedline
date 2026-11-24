using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Seedline;

internal static class DecisionTrace
{
    private static DecisionRecorder? recorder;
    private static Func<object>? context;
    private static readonly string session = Guid.NewGuid().ToString("N");
    private static long sequence;

    internal static void Initialize(Func<string> directory, Func<object> getContext, Action<Exception> failed)
    {
        context = getContext;
        recorder = new(directory, failed);
    }

    internal static void Record(string kind, Func<object> data) => recorder?.Write(() => new
    {
        format = 1,
        session,
        sequence = ++sequence,
        utc = DateTime.UtcNow,
        kind,
        context = context!(),
        data = data()
    });
}

// Each record is flushed to the OS. No worker, queue, or gameplay state is retained.
internal sealed class DecisionRecorder(Func<string> directory, Action<Exception> failed, int limit = 4 * 1024 * 1024) : IDisposable
{
    private static readonly JsonSerializerOptions options = new() { NumberHandling = JsonNumberHandling.WriteAsString };
    private static readonly Regex steamLikeNumber = new(@"\b76561\d{12}\b", RegexOptions.CultureInvariant);
    private readonly object gate = new();
    private FileStream? stream;
    private bool stopped;

    internal void Write(Func<object> record)
    {
        lock (gate)
        {
            if (stopped)
                return;
            try
            {
                var json = JsonSerializer.Serialize(record(), options);
                // These are diagnostic values, never account IDs. JSON escaping keeps seeds and
                // numeric values intact through the game's Steam-ID sanitizer.
                json = steamLikeNumber.Replace(json, m => "\\u0037" + m.Value[1..]);
                var bytes = Encoding.UTF8.GetBytes(json + "\n");
                if (bytes.Length > limit)
                    bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { kind = "record-too-large", bytes = bytes.Length }) + "\n");
                var folder = directory();
                var path = Path.Combine(folder, "decisions.jsonl");
                if (stream == null)
                {
                    Directory.CreateDirectory(folder);
                    stream = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                    if (stream.Length > 0)
                    {
                        stream.Seek(-1, SeekOrigin.End);
                        if (stream.ReadByte() != '\n' && stream.Length < limit)
                            stream.WriteByte((byte)'\n'); // Separate an interrupted record from the next session.
                    }
                    stream.Seek(0, SeekOrigin.End);
                }
                if (stream.Length + bytes.Length > limit)
                {
                    stream.Dispose();
                    stream = null;
                    File.Move(path, Path.Combine(folder, "decisions.previous.jsonl"), overwrite: true);
                    stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
                }
                stream.Write(bytes);
                stream.Flush();
            }
            catch (Exception error)
            {
                stopped = true;
                try { stream?.Dispose(); }
                catch (IOException) { }
                stream = null;
                try { failed(error); }
                catch (Exception) { } // Diagnostics, including their failure warning, cannot stop gameplay.
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            stopped = true;
            stream?.Dispose();
            stream = null;
        }
    }
}
