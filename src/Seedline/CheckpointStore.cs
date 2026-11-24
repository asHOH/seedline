using System.Text.Json;
using MegaCrit.Sts2.Core.Exceptions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;

namespace Seedline;

internal static class CheckpointStore
{
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
            stage = "saved callback (write completed)";
            saved?.Invoke();
        }
        catch (Exception error)
        {
            Log.Error($"Seedline: checkpoint {stage} failed; path={path}; {RunCheckpoints.Describe(save)}\n{error}");
            throw;
        }
    }

    internal static ReadSaveResult<SerializableRun> Read(ISaveStore store, string path, int gameSchema)
    {
        ModEntry.LogLoadedMods();
        var selectedPath = path;
        var context = "run=unknown";
        var primaryFailure = "";
        try
        {
            var primary = ReadOne(store, path, gameSchema, ref context);
            if (primary.Success || primary.Status is not (ReadSaveStatus.FileNotFound or ReadSaveStatus.JsonParseError or ReadSaveStatus.FileEmpty))
                return primary;

            primaryFailure = $"primary={primary.Status} {primary.ErrorMessage}; {context}";
            selectedPath = path + ".backup";
            context = "run=unknown";
            var backup = ReadOne(store, selectedPath, gameSchema, ref context);
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

    private static ReadSaveResult<SerializableRun> ReadOne(ISaveStore store, string path, int gameSchema, ref string context)
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
            context += $"\n{error}";
            return new ReadSaveResult<SerializableRun>(ReadSaveStatus.JsonParseError, error.Message);
        }
    }

    private static string Field(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.GetRawText() : "unknown";
}
