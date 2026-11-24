using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Logging;
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
    internal const int SchemaVersion = 1;
    private static readonly ConditionalWeakTable<RunState, CheckpointState> runs = new();
    private static readonly ConditionalWeakTable<SerializableRun, CheckpointState> snapshots = new();

    internal static void Start(RunState run)
    {
        ModEntry.LogLoadedMods();
        CheckLiveScope(run);
        RunContent.Grant(run);
        runs.Add(run, new CheckpointState(Guid.NewGuid(), run.Rng.StringSeed, new RandomState(run.Rng.StringSeed), RitsuBridge.Capture(run)));
        Log.Info($"Seedline: run state initialized; {Describe(run)}.");
        DecisionTrace.Record("run-start", () => new { run = Find(run)!.RunId, seed = run.Rng.StringSeed });
    }

    internal static void Capture(RunState run, SerializableRun save)
    {
        if (runs.TryGetValue(run, out var state))
        {
            try
            {
                CheckLiveScope(run);
                snapshots.Add(save, state.Copy() with { Ritsu = RitsuBridge.Capture(run) });
                DecisionTrace.Record("checkpoint-capture", () => new { run = state.RunId, seed = state.Seed, floor = run.TotalFloor, reloads = save.NumReloads });
            }
            catch (Exception error)
            {
                Log.Error($"Seedline: checkpoint capture failed; {Describe(run)}\n{error}");
                throw;
            }
        }
    }

    internal static void Restore(RunState run, SerializableRun save)
    {
        ModEntry.LogLoadedMods();
        try
        {
            CheckLiveScope(run);
            var state = Require(save);
            if (run.Rng.StringSeed != state.Seed)
                throw new InvalidDataException($"Seedline checkpoint seed mismatch: saved={state.Seed}, live={run.Rng.StringSeed}.");
            RitsuBridge.ValidateRestored(run, state.Ritsu);
            runs.Add(run, state.Copy());
        }
        catch (Exception error)
        {
            Log.Error($"Seedline: checkpoint restore failed; {Describe(save)}\n{error}");
            throw;
        }
        Log.Info($"Seedline: run state restored; {Describe(run)}; reloads={save.NumReloads}.");
        DecisionTrace.Record("checkpoint-restore", () => new { run = Find(run)!.RunId, seed = run.Rng.StringSeed, floor = run.TotalFloor, reloads = save.NumReloads });
    }

    internal static CheckpointState Require(RunState run) => runs.TryGetValue(run, out var state)
        ? state : throw new InvalidOperationException("Seedline state is missing from this run.");

    internal static CheckpointState? Find(RunState run) => runs.TryGetValue(run, out var state) ? state : null;

    private static string Describe(RunState run) =>
        $"run={Find(run)?.RunId}; seed={run.Rng.StringSeed}; act={run.CurrentActIndex + 1}; floor={run.TotalFloor}; character={string.Join(",", run.Players.Select(p => p.Character.Id))}; ascension={run.AscensionLevel}; mode={run.GameMode}";

    internal static string Describe(SerializableRun save) =>
        $"run={(snapshots.TryGetValue(save, out var state) ? state.RunId.ToString() : "unknown")}; seed={save.SerializableRng?.Seed}; act_index={save.CurrentActIndex}; floor={save.MapPointHistory?.Sum(act => act?.Count ?? 0)}; reloads={save.NumReloads}";

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
        return Decode(document.RootElement, gameSchema);
    }

    internal static SerializableRun Decode(JsonElement root, int gameSchema)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Checkpoint root must be an object.");
        var state = RequiredField(root, "seedline");
        if (state.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Checkpoint field seedline must be an object.");
        var version = RequiredField(state, "schema_version");
        if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != SchemaVersion)
            throw new InvalidDataException($"Seedline checkpoint schema mismatch: expected={SchemaVersion}, actual={version.GetRawText()}.");
        var build = RequiredField(state, "game_build");
        if (build.ValueKind != JsonValueKind.String || build.GetString() != ModEntry.GameBuild)
            throw new InvalidDataException($"Seedline checkpoint game build mismatch: expected={ModEntry.GameBuild}, actual={build.GetRawText()}.");
        var id = RequiredField(state, "run_id");
        if (id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var runId) || runId == Guid.Empty)
            throw new InvalidDataException($"Seedline checkpoint run_id must be a nonempty GUID; actual={id.GetRawText()}.");
        var seed = RequiredField(state, "seed");
        if (seed.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Seedline checkpoint seed must be a string.");

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
            throw new InvalidDataException($"Vanilla checkpoint schema mismatch: expected={gameSchema}, actual={save.SchemaVersion}.");
        var positions = RequiredField(state, "random_positions");
        var rewards = RequiredField(state, "rewards");
        var selection = RequiredField(state, "selection");
        var events = RequiredField(state, "events");
        var cards = RequiredField(state, "card_history");
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

    private static JsonElement RequiredField(JsonElement parent, string name) => parent.TryGetProperty(name, out var value)
        ? value : throw new InvalidDataException($"Missing checkpoint field {name}.");

    private static void Validate(SerializableRun save, CheckpointState state)
    {
        RitsuBridge.CheckConfiguration(state.Ritsu != null);
        if (string.IsNullOrEmpty(state.Seed) || save.SerializableRng?.Seed != state.Seed)
            throw new InvalidDataException($"Checkpoint seed mismatch: seedline={state.Seed}, vanilla={save.SerializableRng?.Seed ?? "missing"}.");
        if (save.Players == null || save.Players.Any(player => player == null))
            throw new InvalidDataException("Checkpoint players is missing or contains null entries.");
        if (save.Modifiers == null || save.Modifiers.Any(modifier => modifier?.Id == null))
            throw new InvalidDataException("Checkpoint modifiers is missing or contains entries without an ID.");
        if (save.Acts == null || save.Acts.Any(act => act == null))
            throw new InvalidDataException("Checkpoint acts is missing or contains null entries.");
        if (save.SerializableOdds == null)
            throw new InvalidDataException("Checkpoint odds is missing.");
        if (save.SerializableSharedRelicGrabBag == null)
            throw new InvalidDataException("Checkpoint shared_relic_grab_bag is missing.");
        if (save.ExtraFields == null)
            throw new InvalidDataException("Checkpoint extra_fields is missing.");
        if (save.CurrentActIndex < 0 || save.CurrentActIndex >= save.Acts.Count)
            throw new InvalidDataException($"Checkpoint current_act_index out of range: actual={save.CurrentActIndex}, act_count={save.Acts.Count}.");
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
