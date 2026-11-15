using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;

namespace SameSeed;

internal static class CloudCheckpoints
{
    internal static IEnumerable<Task> Append(IEnumerable<Task> original, CloudSaveStore store, bool upload)
    {
        foreach (var task in original)
            yield return task;
        for (var profile = 1; profile <= 3; profile++)
        {
            var path = RunSaveManager.GetRunSavePath(profile, RunCheckpoints.FileName, true);
            yield return upload ? store.OverwriteCloudWithLocal(path) : store.SyncCloudToLocal(path);
        }
    }
}

[HarmonyPatch(typeof(SaveManager), "EnumerateCloudSyncTasks")]
internal static class CloudDownloadPatch
{
    private static void Postfix(CloudSaveStore cloudStore, ref IEnumerable<Task> __result) =>
        __result = CloudCheckpoints.Append(__result, cloudStore, false);
}

[HarmonyPatch(typeof(SaveManager), "EnumerateOverwriteCloudWithLocalTasks")]
internal static class CloudUploadPatch
{
    private static void Postfix(CloudSaveStore cloudStore, ref IEnumerable<Task> __result) =>
        __result = CloudCheckpoints.Append(__result, cloudStore, true);
}

[HarmonyPatch(typeof(SaveManager), "CleanupStaleCurrentRunSaves")]
internal static class StaleCheckpointPatch
{
    private static readonly Action<SaveManager, int, string> cleanup = AccessTools.MethodDelegate<Action<SaveManager, int, string>>(
        AccessTools.Method(typeof(SaveManager), "CleanupStaleCurrentRunSaveForProfile"));

    private static void Postfix(SaveManager __instance, ISaveStore ____saveStore, MigrationManager ____migrationManager)
    {
        for (var profile = 1; profile <= 3; profile++)
        {
            var path = RunSaveManager.GetRunSavePath(profile, RunCheckpoints.FileName, true);
            if (CheckpointStore.Read(____saveStore, path, ____migrationManager.GetLatestVersion<SerializableRun>()).Success)
                cleanup(__instance, profile, RunCheckpoints.FileName);
        }
    }
}
