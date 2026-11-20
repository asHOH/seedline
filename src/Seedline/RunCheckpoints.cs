using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace Seedline;

internal sealed record CheckpointState(Guid RunId, string Seed, RandomState Random, JsonObject? Ritsu = null)
{
    internal RewardState Rewards { get; init; } = new();
    internal SelectionState Selection { get; init; } = new();
    internal EventState Events { get; init; } = new();
    internal CardHistory Cards { get; init; } = new();
    internal CheckpointState Copy() => this with { Random = Random.Copy(), Rewards = Rewards.Copy(), Selection = Selection.Copy(), Events = Events.Copy(), Cards = Cards.Copy(), Ritsu = Ritsu?.DeepClone().AsObject() };
}

internal static class RunCheckpoints
{
    internal const int SchemaVersion = 6;
    private static readonly ConditionalWeakTable<RunState, CheckpointState> runs = new();
    private static readonly ConditionalWeakTable<SerializableRun, CheckpointState> snapshots = new();

    internal static void Start(RunState run)
    {
        ModEntry.CheckOtherMods();
        CheckLiveScope(run);
        RunContent.Grant(run);
        runs.Add(run, new CheckpointState(Guid.NewGuid(), run.Rng.StringSeed, new RandomState(run.Rng.StringSeed), RitsuBridge.Capture(run)));
    }

    internal static void Capture(RunState run, SerializableRun save)
    {
        if (runs.TryGetValue(run, out var state))
        {
            CheckLiveScope(run);
            snapshots.Add(save, state.Copy() with { Ritsu = RitsuBridge.Capture(run) });
        }
    }

    internal static void Restore(RunState run, SerializableRun save)
    {
        ModEntry.CheckOtherMods();
        CheckLiveScope(run);
        var state = Require(save);
        if (run.Rng.StringSeed != state.Seed)
            throw new InvalidDataException("Seedline checkpoint belongs to another run.");
        RitsuBridge.ValidateRestored(run, state.Ritsu);
        runs.Add(run, state.Copy());
    }

    internal static CheckpointState Require(RunState run) => runs.TryGetValue(run, out var state)
        ? state : throw new InvalidOperationException("Seedline state is missing from this run.");

    internal static CheckpointState? Find(RunState run) => runs.TryGetValue(run, out var state) ? state : null;

    internal static CheckpointState Require(SerializableRun save)
    {
        if (!snapshots.TryGetValue(save, out var state))
            throw new InvalidDataException("Seedline state is missing. Start a new run with the mod enabled.");
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
            writer.WriteStartObject("seedline");
            writer.WriteNumber("schema_version", SchemaVersion);
            writer.WriteString("game_build", ModEntry.GameBuild);
            writer.WriteString("run_id", state.RunId);
            writer.WriteString("seed", state.Seed);
            state.Random.Write(writer);
            state.Rewards.Write(writer);
            state.Selection.Write(writer);
            state.Events.Write(writer);
            state.Cards.Write(writer);
            writer.WriteEndObject();
            if (state.Ritsu is { } ritsu)
            {
                writer.WritePropertyName("_ritsulib");
                ritsu.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    internal static SerializableRun Decode(string json, int gameSchema)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("seedline", out var state) || state.ValueKind != JsonValueKind.Object ||
            !state.TryGetProperty("schema_version", out var version) ||
            version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != SchemaVersion ||
            !state.TryGetProperty("game_build", out var build) || build.GetString() != ModEntry.GameBuild ||
            !state.TryGetProperty("run_id", out var id) || !id.TryGetGuid(out var runId) || runId == Guid.Empty ||
            !state.TryGetProperty("seed", out var seed) || seed.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Missing or incompatible Seedline checkpoint metadata. Save preserved.");

        SerializableRun save;
        try
        {
            save = root.Deserialize(JsonSerializationUtility.GetTypeInfo<SerializableRun>())
                ?? throw new InvalidDataException("Seedline checkpoint has no run.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Seedline checkpoint contains invalid run data. Save preserved.", error);
        }
        if (save.SchemaVersion != gameSchema)
            throw new InvalidDataException("Unsupported vanilla checkpoint schema. Save preserved.");
        if (!state.TryGetProperty("random_positions", out var positions) || !state.TryGetProperty("rewards", out var rewards) ||
            !state.TryGetProperty("selection", out var selection) || !state.TryGetProperty("events", out var events) ||
            !state.TryGetProperty("card_history", out var cards))
            throw new InvalidDataException("Missing Seedline random, reward or selection state. Save preserved.");
        var random = RandomState.Read(seed.GetString()!, positions);
        var restored = new CheckpointState(runId, seed.GetString()!, random, RitsuBridge.Read(root, save))
        {
            Rewards = RewardState.Read(rewards),
            Selection = SelectionState.Read(selection),
            Events = EventState.Read(events),
            Cards = CardHistory.Read(cards)
        };
        Validate(save, restored);
        snapshots.Add(save, restored);
        return save;
    }

    private static void Validate(SerializableRun save, CheckpointState state)
    {
        RitsuBridge.CheckConfiguration(state.Ritsu != null);
        if (string.IsNullOrEmpty(state.Seed) || save.SerializableRng?.Seed != state.Seed || save.Players == null ||
            save.Modifiers == null || save.Acts == null || save.SerializableOdds == null ||
            save.SerializableSharedRelicGrabBag == null || save.ExtraFields == null ||
            save.Players.Any(player => player == null) || save.Acts.Any(act => act == null) ||
            save.Modifiers.Any(modifier => modifier?.Id == null) ||
            save.CurrentActIndex < 0 || save.CurrentActIndex >= save.Acts.Count)
            throw new InvalidDataException("Seedline checkpoint is incomplete or belongs to another run.");
        CheckScope(save.GameMode, save.IsCustomGame, save.Players.Count);
        CardOccurrences.Validate(save);
    }

    private static void CheckLiveScope(RunState run)
    {
        CheckScope(run.GameMode, run.IsCustomGame, run.Players.Count);
        // Mods may carry save-only data in serialized modifiers and consume it during reconstruction.
        if (run.Modifiers.Count != 0)
            throw new InvalidDataException("Seedline does not support live run modifiers.");
    }

    private static void CheckScope(GameMode mode, bool custom, int players)
    {
        if (players != 1 || custom || mode is not (GameMode.Standard or GameMode.Dread))
            throw new InvalidDataException("Seedline supports non-custom standard and Dread single-player runs.");
    }
}
