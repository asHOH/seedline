using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace SameSeed;

// Stage 1 records provenance only; Foundation adds gameplay state to this snapshot.
internal sealed record CheckpointState(Guid RunId, string Seed);

internal static class RunCheckpoints
{
    internal const string FileName = "same_seed_run.save";
    internal const int SchemaVersion = 1;
    private static readonly ConditionalWeakTable<RunState, CheckpointState> runs = new();
    private static readonly ConditionalWeakTable<SerializableRun, CheckpointState> snapshots = new();

    internal static void Start(RunState run)
    {
        ModEntry.CheckOtherMods();
        CheckScope(run.GameMode, run.IsCustomGame, run.Players.Count, run.Modifiers.Count);
        runs.Add(run, new CheckpointState(Guid.NewGuid(), run.Rng.StringSeed));
    }

    internal static void Capture(RunState run, SerializableRun save)
    {
        if (runs.TryGetValue(run, out var state))
            snapshots.Add(save, state);
    }

    internal static void Restore(RunState run, SerializableRun save)
    {
        ModEntry.CheckOtherMods();
        var state = Require(save);
        if (run.Rng.StringSeed != state.Seed)
            throw new InvalidDataException("SameSeed checkpoint belongs to another run.");
        runs.Add(run, state);
    }

    internal static CheckpointState Require(SerializableRun save)
    {
        if (!snapshots.TryGetValue(save, out var state))
            throw new InvalidDataException("SameSeed state is missing. Start a new run with the mod enabled.");
        Validate(save, state);
        return state;
    }

    internal static byte[] Encode(SerializableRun save)
    {
        var state = Require(save);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in JsonSerializer.SerializeToElement(save, JsonSerializationUtility.GetTypeInfo<SerializableRun>()).EnumerateObject())
                property.WriteTo(writer);
            writer.WriteStartObject("same_seed");
            writer.WriteNumber("schema_version", SchemaVersion);
            writer.WriteString("game_build", ModEntry.GameBuild);
            writer.WriteString("run_id", state.RunId);
            writer.WriteString("seed", state.Seed);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    internal static SerializableRun Decode(string json, int gameSchema)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("same_seed", out var state) || state.ValueKind != JsonValueKind.Object ||
            !state.TryGetProperty("schema_version", out var version) ||
            !version.TryGetInt32(out var number) || number != SchemaVersion ||
            !state.TryGetProperty("game_build", out var build) || build.GetString() != ModEntry.GameBuild ||
            !state.TryGetProperty("run_id", out var id) || !id.TryGetGuid(out var runId) || runId == Guid.Empty ||
            !state.TryGetProperty("seed", out var seed) || seed.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Missing or incompatible SameSeed checkpoint metadata. Save preserved.");

        SerializableRun save;
        try
        {
            save = root.Deserialize(JsonSerializationUtility.GetTypeInfo<SerializableRun>())
                ?? throw new InvalidDataException("SameSeed checkpoint has no run.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("SameSeed checkpoint contains invalid run data. Save preserved.", error);
        }
        if (save.SchemaVersion != gameSchema)
            throw new InvalidDataException("Unsupported vanilla checkpoint schema. Save preserved.");
        var restored = new CheckpointState(runId, seed.GetString()!);
        Validate(save, restored);
        snapshots.Add(save, restored);
        return save;
    }

    private static void Validate(SerializableRun save, CheckpointState state)
    {
        if (string.IsNullOrEmpty(state.Seed) || save.SerializableRng?.Seed != state.Seed || save.Players == null ||
            save.Modifiers == null || save.Acts == null || save.SerializableOdds == null ||
            save.SerializableSharedRelicGrabBag == null || save.ExtraFields == null ||
            save.Players.Any(player => player == null) || save.Acts.Any(act => act == null) ||
            save.CurrentActIndex < 0 || save.CurrentActIndex >= save.Acts.Count)
            throw new InvalidDataException("SameSeed checkpoint is incomplete or belongs to another run.");
        CheckScope(save.GameMode, save.IsCustomGame, save.Players.Count, save.Modifiers.Count);
    }

    private static void CheckScope(GameMode mode, bool custom, int players, int modifiers)
    {
        if (players != 1 || custom || modifiers != 0 || mode is not (GameMode.Standard or GameMode.Dread))
            throw new InvalidDataException("SameSeed supports standard and Dread single-player runs without custom modifiers.");
    }
}
