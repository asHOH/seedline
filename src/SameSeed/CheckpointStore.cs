using System.Text.Json;
using MegaCrit.Sts2.Core.Exceptions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;

namespace SameSeed;

internal static class CheckpointStore
{
    internal static async Task Write(ISaveStore store, string path, SerializableRun save, bool synchronous, Action? saved)
    {
        // Freeze the complete checkpoint before the first await, including reload-count rewrites.
        var bytes = RunCheckpoints.Encode(save);
        if (synchronous)
            store.WriteFile(path, bytes);
        else
            await store.WriteFileAsync(path, bytes);
        saved?.Invoke();
    }

    internal static ReadSaveResult<SerializableRun> Read(ISaveStore store, string path, int gameSchema)
    {
        try
        {
            ModEntry.CheckOtherMods();
            var primary = ReadOne(store, path, gameSchema);
            if (primary.Success || primary.Status is not (ReadSaveStatus.FileNotFound or ReadSaveStatus.JsonParseError or ReadSaveStatus.FileEmpty))
                return primary;

            var backup = ReadOne(store, path + ".backup", gameSchema);
            if (backup.Success)
            {
                Log.Warn($"Seedline: recovered complete checkpoint from {path}.backup.");
                return backup;
            }
            return primary.Status == ReadSaveStatus.FileNotFound ? backup : primary;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or SaveException)
        {
            Log.Error($"Seedline: checkpoint load rejected; files preserved. {error.Message}");
            return new ReadSaveResult<SerializableRun>(ReadSaveStatus.ValidationFailed, error.Message);
        }
    }

    private static ReadSaveResult<SerializableRun> ReadOne(ISaveStore store, string path, int gameSchema)
    {
        if (!store.FileExists(path))
            return new ReadSaveResult<SerializableRun>(ReadSaveStatus.FileNotFound);
        var json = store.ReadFile(path);
        if (string.IsNullOrWhiteSpace(json))
            return new ReadSaveResult<SerializableRun>(ReadSaveStatus.FileEmpty);
        try
        {
            return new ReadSaveResult<SerializableRun>(RunCheckpoints.Decode(json, gameSchema));
        }
        catch (JsonException error)
        {
            return new ReadSaveResult<SerializableRun>(ReadSaveStatus.JsonParseError, error.Message);
        }
    }
}
