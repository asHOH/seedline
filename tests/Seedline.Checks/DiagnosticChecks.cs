using System.Text;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;

namespace Seedline.Checks;

public static partial class Checks
{
    private static void CheckEnvironmentDiagnostics()
    {
        var messages = new List<string>();
        void Observe(LogLevel level, string text, int skip)
        {
            if (text.StartsWith("Seedline: loaded mods:"))
                messages.Add(text);
        }
        // Simulate the first load in a fresh session, independently of menu save probes.
        HarmonyLib.AccessTools.Field(typeof(ModEntry), "loggedMods").SetValue(null, false);
        Log.LogCallback += Observe;
        try
        {
            var store = new MockGodotFileIo("environment");
            store.WriteFile("current_run.save", "{}");
            Assert(CheckpointStore.Read(store, "current_run.save", 21).Status == ReadSaveStatus.ValidationFailed,
                "first checkpoint load rejects missing Seedline data");
            CheckpointStore.Read(store, "current_run.save", 21);
            Assert(messages.Count == 1 && messages[0].Contains("Seedline@") && messages[0].Contains("Seedline.Checks@"),
                "failed first load lists loaded mod versions once, after mod initialization");
        }
        finally { Log.LogCallback -= Observe; }
    }

    private static async Task CheckDiagnostics(SerializableRun snapshot, int schema)
    {
        const string path = "current_run.save";
        var bytes = RunCheckpoints.Encode(snapshot);
        var json = Encoding.UTF8.GetString(bytes);
        var runId = RunCheckpoints.Require(snapshot).RunId.ToString();
        var messages = new List<(LogLevel Level, string Text)>();
        void Observe(LogLevel level, string text, int skip)
        {
            if (text.StartsWith("Seedline:"))
                messages.Add((level, text));
        }
        void Single(LogLevel level, params string[] fragments)
        {
            Assert(messages.Count == 1 && messages[0].Level == level && fragments.All(messages[0].Text.Contains),
                $"diagnostics emit one contextual {level} for {fragments[0]}");
            messages.Clear();
        }
        Log.LogCallback += Observe;
        try
        {
            var store = new MockGodotFileIo("diagnostics");
            Assert(CheckpointStore.Read(store, path, schema).Status == ReadSaveStatus.FileNotFound && messages.Count == 0,
                "absent primary and backup are silent");
            await CheckpointStore.Write(store, path, snapshot, false, null);
            Assert(CheckpointStore.Read(store, path, schema).Success && messages.Count == 0 && store.ReadFile(path) == json,
                "ordinary checkpoint reads and writes stay quiet and preserve bytes");

            store.WriteFile(path, "{torn");
            Assert(CheckpointStore.Read(store, path, schema).Success, "diagnostic recovery succeeds");
            Single(LogLevel.Warn, "recovered complete checkpoint", path + ".backup", "primary=JsonParseError", "JsonReaderException", runId);
            store.DeleteFile(path);
            Assert(CheckpointStore.Read(store, path, schema).Success, "diagnostic backup-only recovery succeeds");
            Single(LogLevel.Warn, "recovered complete checkpoint", "primary=FileNotFound", runId);
            store.WriteFile(path, " ");
            Assert(CheckpointStore.Read(store, path, schema).Success, "diagnostic empty-primary recovery succeeds");
            Single(LogLevel.Warn, "recovered complete checkpoint", "primary=FileEmpty", runId);
            store.WriteFile(path, "{torn");
            store.WriteFile(path + ".backup", "{torn backup");
            var unreadable = CheckpointStore.Read(store, path, schema);
            Assert(unreadable.Status == ReadSaveStatus.JsonParseError && !unreadable.ErrorMessage!.Contains("ReadOne"),
                "malformed checkpoint retains concise caller-facing error");
            Single(LogLevel.Error, "checkpoint read failed", "primary=JsonParseError", "backup=JsonParseError", "ReadOne");

            (Action<JsonObject> Mutate, string Detail)[] invalidCases =
            [
                (root => root["seedline"]!["schema_version"] = RunCheckpoints.SchemaVersion + 1, $"expected={RunCheckpoints.SchemaVersion}, actual={RunCheckpoints.SchemaVersion + 1}"),
                (root => root["seedline"]!["game_build"] = "other", $"expected={ModEntry.GameBuild}, actual=\"other\""),
                (root => root["seedline"]!.AsObject().Remove("card_history"), "Missing checkpoint field card_history"),
                (root => root["schema_version"] = schema + 1, $"expected={schema}, actual={schema + 1}"),
                (root => root["current_act_index"] = -1, "current_act_index out of range"),
                (root => root["ascension"] = "invalid", "System.Text.Json.JsonException")
            ];
            foreach (var (mutate, detail) in invalidCases)
            {
                var invalid = JsonNode.Parse(json)!.AsObject();
                mutate(invalid);
                var invalidJson = invalid.ToJsonString();
                store.WriteFile(path, json);
                store.WriteFile(path, invalidJson);
                Assert(CheckpointStore.Read(store, path, schema).Status == ReadSaveStatus.ValidationFailed &&
                    store.ReadFile(path) == invalidJson && store.ReadFile(path + ".backup") == json,
                    "diagnostic rejection preserves both files without fallback");
                Single(LogLevel.Error, "checkpoint load rejected", path, runId, "seed=", "floor=", detail, "RunCheckpoints.Decode");
            }
            store.UnreadablePaths.Add(path);
            Assert(CheckpointStore.Read(store, path, schema).Status == ReadSaveStatus.ValidationFailed, "read IO failure retains status");
            Single(LogLevel.Error, "checkpoint load rejected", path, "System.IO.IOException", "MockGodotFileIo.ReadFile");
            store.UnreadablePaths.Clear();
            store.DeleteFile(path);
            store.UnreadablePaths.Add(path + ".backup");
            Assert(CheckpointStore.Read(store, path, schema).Status == ReadSaveStatus.ValidationFailed, "backup IO failure retains status");
            Single(LogLevel.Error, "checkpoint load rejected", "path=" + path + ".backup", "primary=FileNotFound", "System.IO.IOException");

            foreach (var synchronous in new[] { true, false })
            {
                var failing = new MockGodotFileIo("failed") { ShouldFailWrites = true };
                var called = false;
                await ExpectFailure(() => CheckpointStore.Write(failing, path, snapshot, synchronous, () => called = true));
                Assert(!called, "write failure does not invoke saved callback");
                Single(LogLevel.Error, "checkpoint write failed", runId, "seed=", "floor=", "MockGodotFileIo.WriteFile");
            }
            var delayed = new DelayedWriteFailure();
            var pending = CheckpointStore.Write(delayed, path, snapshot, false, null);
            Assert(!pending.IsCompleted && messages.Count == 0, "pending write emits no success or failure");
            var failure = new InvalidOperationException("async write failure", new IOException("inner IO failure"));
            delayed.Completion.SetException(failure);
            try { await pending; throw new Exception("Write failure was swallowed."); }
            catch (InvalidOperationException error) { Assert(ReferenceEquals(error, failure), "async write exception identity is preserved"); }
            Single(LogLevel.Error, "checkpoint write failed", "async write failure", "inner IO failure", "CheckpointStore.Write");

            store = new MockGodotFileIo("callback");
            await ExpectFailure(() => CheckpointStore.Write(store, path, snapshot, true, FailSavedCallback));
            Assert(store.ReadFile(path) == json, "callback failure occurs after checkpoint write");
            Single(LogLevel.Error, "saved callback (write completed) failed", "inner callback failure", nameof(FailSavedCallback));
            var unbound = JsonSerializationUtility.FromJson<SerializableRun>(json).SaveData!;
            try { await CheckpointStore.Write(store, path, unbound, false, null); throw new Exception("Unbound save accepted."); }
            catch (InvalidDataException) { }
            Single(LogLevel.Error, "checkpoint encode failed", "run=unknown", "Seedline state is missing");
            Assert(store.ReadFile(path) == json && RunCheckpoints.Encode(snapshot).SequenceEqual(bytes),
                "diagnostics leave checkpoint bytes and random positions unchanged");

            var fresh = RunState.CreateForNewRun([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), [], GameMode.Standard, false, 7, "DIAGNOSTICS");
            RunCheckpoints.Start(fresh);
            Single(LogLevel.Info, "run state initialized", "seed=DIAGNOSTICS", "character=", "ascension=7", "mode=Standard");
            var restored = RunState.FromSerializable(snapshot);
            RunCheckpoints.Restore(restored, snapshot);
            Single(LogLevel.Info, "run state restored", runId, "reloads=");
            try { RunCheckpoints.Restore(fresh, snapshot); throw new Exception("Mismatched run accepted."); }
            catch (InvalidDataException) { }
            Single(LogLevel.Error, "checkpoint restore failed", runId, "seed mismatch", "live=DIAGNOSTICS");
        }
        finally { Log.LogCallback -= Observe; }
    }

    private static void FailSavedCallback() => throw new InvalidOperationException("callback failure", new IOException("inner callback failure"));

    private sealed class DelayedWriteFailure() : MockGodotFileIo("delayed"), ISaveStore
    {
        internal TaskCompletionSource Completion { get; } = new();
        Task ISaveStore.WriteFileAsync(string path, byte[] content) => Completion.Task;
    }
}
