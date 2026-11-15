using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace SameSeed.Checks;

// Test-only bridge for v0.6.13; production keeps its existing save integration.
internal static class RitsuCheckpointPrototype
{
    internal const string PatchId = "SameSeed.RitsuPrototype";
    private sealed record Payload(JsonNode Data);
    private static readonly ConditionalWeakTable<SerializableRun, Payload> payloads = new();
    internal static bool ProtectLoadedSnapshot { get; set; }

    internal static Type RitsuType(string name) => AccessTools.TypeByName("STS2RitsuLib." + name)
        ?? throw new InvalidOperationException($"RitsuLib type missing: {name}");

    internal static object? Call(string type, string method, params object?[] args) =>
        AccessTools.Method(RitsuType(type), method).Invoke(null, args);

    internal static JsonNode Document(SerializableRun save) =>
        JsonNode.Parse((string?)Call("RunData.RunSavedDataRegistry", "BuildPayloadFromSerializable", save)
            ?? throw new InvalidDataException("Prototype requires a nonempty RitsuLib payload."))!["_ritsulib"]!;

    internal static void Install()
    {
        var harmony = new Harmony(PatchId);
        var saveMethod = AccessTools.Method(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), [typeof(SerializableRun), typeof(bool)]);
        var loadMethod = AccessTools.Method(typeof(RunSaveManager), nameof(RunSaveManager.LoadRunSave));
        harmony.Unpatch(AccessTools.PropertyGetter(typeof(RunSaveManager), "CurrentRunSavePath"), HarmonyPatchType.Prefix, ModEntry.Id);
        harmony.Unpatch(saveMethod, HarmonyPatchType.Prefix, ModEntry.Id);
        harmony.Unpatch(loadMethod, HarmonyPatchType.Prefix, ModEntry.Id);

        harmony.Patch(saveMethod, prefix: Patch(nameof(Save), Priority.Last));
        harmony.Patch(loadMethod, prefix: Patch(nameof(Load), Priority.First), postfix: Patch(nameof(RestoreSelectedPayload), Priority.Last));
        harmony.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.ToSave)), postfix: Patch(nameof(Capture), Priority.Last));
        harmony.Patch(AccessTools.Method(typeof(RunCheckpoints), nameof(RunCheckpoints.Decode)), postfix: Patch(nameof(Attach), Priority.Last));
        harmony.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer)), prefix: Patch(nameof(ValidateRestored), Priority.First));
    }

    private static HarmonyMethod Patch(string name, int priority) =>
        new(AccessTools.Method(typeof(RitsuCheckpointPrototype), name)) { priority = priority };

    private static void Capture(SerializableRun __result) => payloads.Add(__result, new(Document(__result)));

    private static void Attach(string json, SerializableRun __result)
    {
        var data = JsonNode.Parse(json)?["_ritsulib"]
            ?? throw new InvalidDataException("Prototype checkpoint is missing its RitsuLib payload.");
        Call("RunData.RunSavedDataRegistry", "AttachDocumentFromJson", __result, json);
        if (!JsonNode.DeepEquals(data, Document(__result)))
            throw new InvalidDataException("RitsuLib failed to attach the selected checkpoint.");
        payloads.Add(__result, new(data));
    }

    internal static void RestoreSelectedPayload(ReadSaveResult<SerializableRun> __result)
    {
        if (!ProtectLoadedSnapshot || __result.SaveData is not { } save || !payloads.TryGetValue(save, out var payload))
            return;
        // RitsuLib's earlier postfix rereads the primary even when the game selected a backup.
        var json = new JsonObject { ["_ritsulib"] = payload.Data.DeepClone() }.ToJsonString();
        Call("RunData.RunSavedDataRegistry", "AttachDocumentFromJson", save, json);
        if (!JsonNode.DeepEquals(payload.Data, Document(save)))
            throw new InvalidDataException("RitsuLib failed to attach the selected checkpoint.");
    }

    private static bool Load(RunSaveManager __instance, ISaveStore ____saveStore,
        IProfileIdProvider ____profileIdProvider, ref ReadSaveResult<SerializableRun> __result)
    {
        var path = RunSaveManager.GetRunSavePath(____profileIdProvider.CurrentProfileId, RunSaveManager.runSaveFileName);
        __result = CheckpointStore.Read(____saveStore, path, __instance.SchemaVersion);
        return false;
    }

    private static bool Save(SerializableRun save, bool isMultiplayer, ISaveStore ____saveStore,
        IProfileIdProvider ____profileIdProvider, bool ____forceSynchronous, Action? ___Saved, ref Task __result)
    {
        if (isMultiplayer)
            throw new InvalidOperationException("Prototype covers single-player.");
        var path = RunSaveManager.GetRunSavePath(____profileIdProvider.CurrentProfileId, RunSaveManager.runSaveFileName);
        __result = Write(____saveStore, path, save, ____forceSynchronous, ___Saved);
        return false;
    }

    private static async Task Write(ISaveStore store, string path, SerializableRun save, bool synchronous, Action? saved)
    {
        var expected = payloads.GetValue(save, _ => throw new InvalidDataException("Uncaptured prototype checkpoint.")).Data;
        var bytes = (byte[])Call("RunData.RunSavedDataRegistry", "InjectIntoUtf8Json", RunCheckpoints.Encode(save), save)!;
        if (!JsonNode.DeepEquals(expected, JsonNode.Parse(bytes)?["_ritsulib"]))
            throw new InvalidDataException("RitsuLib injection changed or omitted the captured payload.");
        if (synchronous)
            store.WriteFile(path, bytes);
        else
            await store.WriteFileAsync(path, bytes);
        saved?.Invoke();
    }

    private static void ValidateRestored(RunState state, SerializableRun save)
    {
        var document = payloads.GetValue(save, _ => throw new InvalidDataException("Uncaptured prototype checkpoint.")).Data;
        var slots = ((Array)Call("RunData.RunSavedDataRegistry", "GetRegisteredSlots")!).Cast<object>().ToArray();
        var bag = Call("RunData.RunSavedDataRuntime", "GetBag", state)!;
        var options = (JsonSerializerOptions)AccessTools.Property(RitsuType("RunData.RunSavedDataJson"), "Options").GetValue(null)!;
        foreach (var (modId, entries) in document["run_saved_data"]!.AsObject())
            foreach (var (key, entry) in entries!.AsObject())
            {
                var slot = slots.SingleOrDefault(candidate =>
                    string.Equals((string?)Property(candidate, "ModId"), modId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string?)Property(candidate, "Key"), key, StringComparison.OrdinalIgnoreCase));
                if (slot == null || entry!["schema"]!.GetValue<int>() != (int)Property(Property(slot, "Options")!, "SchemaVersion")!)
                    throw new InvalidDataException($"Unsupported RitsuLib slot: {modId}/{key}.");
                object?[] args = [Property(slot, "SlotKey"), null];
                var found = (bool)AccessTools.Method(bag.GetType(), "TryGet").Invoke(bag, args)!;
                var field = Property(slot, "Kind")!.ToString() == "Player" ? "players" : "data";
                // Raw documents survive failed imports; inspect the actual runtime bag instead.
                if (!found || args[1] == null || !JsonNode.DeepEquals(entry[field], JsonSerializer.SerializeToNode(args[1], args[1]!.GetType(), options)))
                    throw new InvalidDataException($"RitsuLib failed to restore {modId}/{key}.");
            }
    }

    private static object? Property(object value, string name) => AccessTools.Property(value.GetType(), name).GetValue(value);
}
