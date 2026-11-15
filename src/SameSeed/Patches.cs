using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace SameSeed;

[HarmonyPatch(typeof(RunSaveManager), "get_CurrentRunSavePath")]
internal static class SavePathPatch
{
    private static bool Prefix(IProfileIdProvider ____profileIdProvider, ref string __result)
    {
        __result = RunSaveManager.GetRunSavePath(____profileIdProvider.CurrentProfileId, RunCheckpoints.FileName, true);
        return false;
    }
}

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), [typeof(SerializableRun), typeof(bool)])]
internal static class SavePatch
{
    private static bool Prefix(SerializableRun save, bool isMultiplayer, ISaveStore ____saveStore,
        IProfileIdProvider ____profileIdProvider, bool ____forceSynchronous, Action? ___Saved, ref Task __result)
    {
        if (isMultiplayer)
            throw new InvalidOperationException("SameSeed requires single-player.");
        var path = RunSaveManager.GetRunSavePath(____profileIdProvider.CurrentProfileId, RunCheckpoints.FileName, true);
        __result = CheckpointStore.Write(____saveStore, path, save, ____forceSynchronous, ___Saved);
        return false;
    }
}

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadRunSave))]
internal static class LoadPatch
{
    private static bool Prefix(RunSaveManager __instance, ISaveStore ____saveStore,
        IProfileIdProvider ____profileIdProvider, ref ReadSaveResult<SerializableRun> __result)
    {
        var path = RunSaveManager.GetRunSavePath(____profileIdProvider.CurrentProfileId, RunCheckpoints.FileName, true);
        __result = CheckpointStore.Read(____saveStore, path, __instance.SchemaVersion);
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class NewRunPatch
{
    private static void Prefix(RunManager __instance, RunState state)
    {
        if (__instance.IsInProgress)
            throw new InvalidOperationException("A run is already active.");
        RunCheckpoints.Start(state);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer))]
internal static class RestoreRunPatch
{
    private static void Prefix(RunManager __instance, RunState state, SerializableRun save)
    {
        if (__instance.IsInProgress)
            throw new InvalidOperationException("A run is already active.");
        RunCheckpoints.Restore(state, save);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class CapturePatch
{
    private static void Postfix(RunManager __instance, SerializableRun __result)
    {
        if (__instance.DebugOnlyGetState() is { } state)
            RunCheckpoints.Capture(state, __result);
    }
}

[HarmonyPatch]
internal static class MultiplayerPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
    [
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer)),
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))
    ];

    private static void Prefix() => throw new InvalidOperationException("Disable SameSeed to play multiplayer.");
}
