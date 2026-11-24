using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Exceptions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Migrations;

namespace Seedline;

internal static class CheckpointStore
{
    private static readonly Func<MigrationManager, string, string?> repairJson =
        AccessTools.MethodDelegate<Func<MigrationManager, string, string?>>(AccessTools.Method(typeof(MigrationManager), "RepairCommonJsonErrors"));

    internal static async Task Write(ISaveStore store, string path, SerializableRun save, bool synchronous, Action? saved)
    {
        var stage = "encode";
        try
        {
            // Freeze the complete checkpoint before the first await, including reload-count rewrites.
            var bytes = RunCheckpoints.Encode(save);
            stage = "write";
            if (synchronous)
                store.WriteFile(path, bytes);
            else
                await store.WriteFileAsync(path, bytes);
            DecisionTrace.Record("checkpoint-written", () => new { checkpoint = RunCheckpoints.Describe(save) });
            stage = "saved callback (write completed)";
            saved?.Invoke();
        }
        catch (Exception error)
        {
            Log.Error($"Seedline: checkpoint {stage} failed; path={path}; {RunCheckpoints.Describe(save)}\n{error}");
            throw;
        }
    }

    internal static ReadSaveResult<SerializableRun> Read(ISaveStore store, string path, int gameSchema,
        MigrationManager? vanilla = null)
    {
        ModEntry.LogLoadedMods();
        var selectedPath = path;
        var context = "run=unknown";
        var primaryFailure = "";
        try
        {
            var primary = ReadOne(store, path, gameSchema, ref context, vanilla);
            if (primary.Success || primary.Status is not (ReadSaveStatus.FileNotFound or ReadSaveStatus.JsonParseError or ReadSaveStatus.FileEmpty))
                return primary;

            primaryFailure = $"primary={primary.Status} {primary.ErrorMessage}; {context}";
            selectedPath = path + ".backup";
            context = "run=unknown";
            var backup = ReadOne(store, selectedPath, gameSchema, ref context, vanilla);
            if (backup.Success)
            {
                Log.Warn($"Seedline: recovered complete checkpoint from {selectedPath}; {primaryFailure}; {RunCheckpoints.Describe(backup.SaveData!)}.");
                return backup;
            }
            if (primary.Status != ReadSaveStatus.FileNotFound || backup.Status != ReadSaveStatus.FileNotFound)
                Log.Error($"Seedline: checkpoint read failed; files preserved; path={path}; {primaryFailure}; backup={backup.Status} {backup.ErrorMessage}; {context}.");
            return primary.Status == ReadSaveStatus.FileNotFound ? backup : primary;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or SaveException)
        {
            Log.Error($"Seedline: checkpoint load rejected; files preserved; path={selectedPath}; {context}; {primaryFailure}\n{error}");
            return new ReadSaveResult<SerializableRun>(ReadSaveStatus.ValidationFailed, error.Message);
        }
    }

    private static ReadSaveResult<SerializableRun> ReadOne(ISaveStore store, string path, int gameSchema, ref string context,
        MigrationManager? vanilla)
    {
        if (!store.FileExists(path))
            return new ReadSaveResult<SerializableRun>(ReadSaveStatus.FileNotFound);
        var json = store.ReadFile(path);
        if (string.IsNullOrWhiteSpace(json))
            return new ReadSaveResult<SerializableRun>(ReadSaveStatus.FileEmpty);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (vanilla != null && Bypasses(root))
                    return vanilla.LoadSave<SerializableRun>(path);
                root.TryGetProperty("seedline", out var state);
                var floor = root.TryGetProperty("map_point_history", out var history) && history.ValueKind == JsonValueKind.Array
                    ? history.EnumerateArray().Where(act => act.ValueKind == JsonValueKind.Array).Sum(act => act.GetArrayLength()).ToString()
                    : "unknown";
                context = $"run={Field(state, "run_id")}; seed={Field(state, "seed")}; act_index={Field(root, "current_act_index")}; floor={floor}";
            }
            return new ReadSaveResult<SerializableRun>(RunCheckpoints.Decode(root, gameSchema));
        }
        catch (JsonException error)
        {
            if (vanilla != null && repairJson(vanilla, json) is { } repaired)
            {
                // Inspect repaired JSON only to route bypassed modes; never repair a Seedline checkpoint.
                using var document = JsonDocument.Parse(repaired);
                if (Bypasses(document.RootElement))
                    return vanilla.LoadSave<SerializableRun>(path);
            }
            context += $"\n{error}";
            return new ReadSaveResult<SerializableRun>(ReadSaveStatus.JsonParseError, error.Message);
        }
    }

    private static bool Bypasses(JsonElement root) => root.ValueKind == JsonValueKind.Object &&
        !root.TryGetProperty("seedline", out _) && root.TryGetProperty("game_mode", out var mode) &&
        mode.ValueKind == JsonValueKind.String && Enum.TryParse<GameMode>(mode.GetString(), true, out var value) && RunScope.Bypasses(value);

    private static string Field(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.GetRawText() : "unknown";
}
