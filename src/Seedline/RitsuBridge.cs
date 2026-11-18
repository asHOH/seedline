using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace Seedline;

// shortcut: v0.6.14 run/player slots only; extend when another compatibility target is accepted.
internal static class RitsuBridge
{
    internal const string PatchId = "com.ritsukage.sts2-RitsuLib.framework-core";

    // The first checkpoint operation runs after both mods have had a chance to initialize.
    private static readonly Lazy<Type?> frameworkType = new(() => AccessTools.TypeByName("STS2RitsuLib.RitsuLibFramework"));

    private static JsonObject EmptyDocument() => new() { ["version"] = 1, ["run_saved_data"] = new JsonObject() };

    private static bool Enabled()
    {
        var framework = frameworkType.Value;
        if (framework == null && !ModManager.Mods.Any(m => m.manifest?.id == "STS2-RitsuLib" &&
            m.state is ModLoadState.Loaded or ModLoadState.Failed))
            return false;
        if (framework?.Assembly.GetName().Version != new Version(0, 6, 14, 0) ||
            AccessTools.Property(framework, "IsActive").GetValue(null) is not true)
            throw new InvalidDataException("Seedline requires active RitsuLib v0.6.14 when installed.");
        return true;
    }

    internal static void CheckConfiguration(bool expected)
    {
        if (Enabled() != expected)
            throw new InvalidDataException("RitsuLib setup changed during this run. Restore the original mod setup.");
    }

    internal static JsonObject? Capture(RunState run)
    {
        if (!Enabled())
            return null;
        // Capture explicitly so completeness is independent of RitsuLib's capture hook.
        var document = Call("RunData.RunSavedDataRegistry", "BuildDocumentFromRun", run);
        return document == null ? EmptyDocument() :
            ((JsonObject)AccessTools.Method(document.GetType(), "ToRootObject").Invoke(document, null)!)["_ritsulib"]!.DeepClone().AsObject();
    }

    internal static JsonObject? Read(JsonElement root, SerializableRun save)
    {
        var present = root.TryGetProperty("_ritsulib", out var data);
        CheckConfiguration(present);
        if (!present)
            return null;
        if (data.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Missing or invalid RitsuLib checkpoint payload.");
        var payload = JsonNode.Parse(data.GetRawText())!.AsObject();
        Attach(save, payload);
        return payload;
    }

    internal static void Attach(SerializableRun save, JsonObject? payload)
    {
        if (payload == null)
            return;
        // RitsuLib's load postfix rereads the primary even when Seedline selected its backup.
        var json = new JsonObject { ["_ritsulib"] = payload.DeepClone() }.ToJsonString();
        Call("RunData.RunSavedDataRegistry", "AttachDocumentFromJson", save, json);
        var attached = Call("RunData.RunSavedDataRegistry", "BuildPayloadFromSerializable", save) as string;
        if (!JsonNode.DeepEquals(payload, attached == null ? EmptyDocument() : JsonNode.Parse(attached)!["_ritsulib"]))
            throw new InvalidDataException("RitsuLib failed to attach the complete selected checkpoint.");
    }

    internal static void ValidateRestored(RunState run, JsonObject? payload)
    {
        if (payload == null)
            return;
        var slots = ((Array)Call("RunData.RunSavedDataRegistry", "GetRegisteredSlots")!).Cast<object>().ToArray();
        var bag = Call("RunData.RunSavedDataRuntime", "GetBag", run)!;
        var options = (JsonSerializerOptions)AccessTools.Property(RitsuType("RunData.RunSavedDataJson"), "Options").GetValue(null)!;
        foreach (var (modId, entries) in payload["run_saved_data"]!.AsObject())
            foreach (var (key, entry) in entries!.AsObject())
            {
                var slot = slots.SingleOrDefault(candidate =>
                    string.Equals((string?)Property(candidate, "ModId"), modId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string?)Property(candidate, "Key"), key, StringComparison.OrdinalIgnoreCase));
                if (slot == null || entry?["schema"] is not JsonValue schema || !schema.TryGetValue<int>(out var version) ||
                    version != (int)Property(Property(slot, "Options")!, "SchemaVersion")!)
                    throw new InvalidDataException($"Unsupported RitsuLib slot: {modId}/{key}.");
                var player = Property(slot, "Kind")!.ToString() == "Player";
                object?[] args = [Property(slot, "SlotKey"), null];
                var found = (bool)AccessTools.Method(bag.GetType(), "TryGet").Invoke(bag, args)!;
                // Preserved JSON survives failed imports; compare the actual runtime values.
                if (entry["kind"] is not JsonValue kind || !kind.TryGetValue<string>(out var slotKind) ||
                    slotKind != (player ? "player" : "run") || !found || args[1] == null ||
                    !JsonNode.DeepEquals(entry[player ? "players" : "data"], JsonSerializer.SerializeToNode(args[1], args[1]!.GetType(), options)))
                    throw new InvalidDataException($"RitsuLib failed to restore {modId}/{key}.");
            }
    }

    private static Type RitsuType(string name) => AccessTools.TypeByName("STS2RitsuLib." + name)
        ?? throw new InvalidDataException($"RitsuLib type missing: {name}.");

    private static object? Call(string type, string method, params object?[] args)
    {
        try { return AccessTools.Method(RitsuType(type), method).Invoke(null, args); }
        catch (TargetInvocationException error)
        {
            throw new InvalidDataException($"RitsuLib {method} failed. Checkpoint preserved.", error.InnerException);
        }
    }

    private static object? Property(object value, string name) => AccessTools.Property(value.GetType(), name).GetValue(value);
}
