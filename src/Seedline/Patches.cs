using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;

namespace Seedline;

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), [typeof(SerializableRun), typeof(bool)])]
internal static class SavePatch
{
    // Own the complete bytes before RitsuLib starts its store-injection context.
    [HarmonyPriority(Priority.First), HarmonyBefore(RitsuBridge.PatchId)]
    private static bool Prefix(SerializableRun save, bool isMultiplayer, ISaveStore ____saveStore,
        IProfileIdProvider ____profileIdProvider, bool ____forceSynchronous, Action? ___Saved, ref Task __result)
    {
        if (isMultiplayer || RunScope.Bypasses(save))
            return true;
        var path = RunSaveManager.GetRunSavePath(____profileIdProvider.CurrentProfileId, RunSaveManager.runSaveFileName);
        __result = CheckpointStore.Write(____saveStore, path, save, ____forceSynchronous, ___Saved);
        return false;
    }
}

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadRunSave))]
internal static class LoadPatch
{
    private static bool Prefix(RunSaveManager __instance, ISaveStore ____saveStore,
        IProfileIdProvider ____profileIdProvider, MigrationManager ____migrationManager,
        ref ReadSaveResult<SerializableRun> __result)
    {
        var path = RunSaveManager.GetRunSavePath(____profileIdProvider.CurrentProfileId, RunSaveManager.runSaveFileName);
        __result = CheckpointStore.Read(____saveStore, path, __instance.SchemaVersion, ____migrationManager);
        return false;
    }

    [HarmonyPriority(Priority.Last), HarmonyAfter(RitsuBridge.PatchId)]
    private static void Postfix(ReadSaveResult<SerializableRun> __result)
    {
        if (__result.SaveData is { } save && !RunScope.Bypasses(save))
            RitsuBridge.Attach(save, RunCheckpoints.Require(save).Ritsu);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class NewRunPatch
{
    private static void Postfix(RunState state)
    {
        if (!RunScope.Bypasses(state))
            CardOccurrences.Initialize(state);
    }

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
    [HarmonyPriority(Priority.Last), HarmonyAfter(RitsuBridge.PatchId)]
    private static void Postfix(RunManager __instance, SerializableRun __result) =>
        RunCheckpoints.Capture(__instance.DebugOnlyGetState()!, __result);
}

[HarmonyPatch]
internal static class MultiplayerPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
    [
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer)),
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))
    ];

    private static void Prefix(RunState state) => RunScope.Multiplayer(state);
}
