using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;

namespace SameSeed.Checks;

public static partial class Checks
{
    public sealed class RitsuProbeData
    {
        public int Marker { get; set; }
    }

    private static async Task CheckRitsuPrototype()
    {
        var mod = ModManager.GetLoadedMods().Single(m => m.manifest?.id == "STS2-RitsuLib");
        Assert(mod.manifest?.version == "0.6.13" && mod.errors is not { Count: > 0 }, "official RitsuLib v0.6.13 loaded");
        var framework = RitsuCheckpointPrototype.RitsuType("RitsuLibFramework");
        Assert((bool)AccessTools.Property(framework, "IsActive").GetValue(null)! &&
            framework.Assembly.GetName().Version == new Version(0, 6, 13, 0), "RitsuLib runtime initialized at pinned version");

        var registry = RitsuCheckpointPrototype.Call("RunData.RunSavedDataStore", "For", "SameSeed.RitsuPrototype")!;
        var data = AccessTools.Method(registry.GetType(), "Register").MakeGenericMethod(typeof(RitsuProbeData))
            .Invoke(registry, ["run", null, null])!;
        var playerData = AccessTools.Method(registry.GetType(), "RegisterPerPlayer").MakeGenericMethod(typeof(RitsuProbeData))
            .Invoke(registry, ["player", null, null])!;
        var setRun = AccessTools.Method(data.GetType(), "Set");
        var setPlayer = AccessTools.Method(playerData.GetType(), "Set", [typeof(RunState), typeof(ulong), typeof(RitsuProbeData)]);

        RitsuCheckpointPrototype.Install();
        var manager = RunManager.Instance;
        var state = RunState.CreateForNewRun([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
            ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), [], GameMode.Standard, false, 7, "RITSUPROBE");
        manager.SetUpNewSingleplayer(state, true);
        setRun.Invoke(data, [state, new RitsuProbeData { Marker = 101 }]);
        setPlayer.Invoke(playerData, [state, 1UL, new RitsuProbeData { Marker = 11 }]);
        var address = RandomAddress.For("ritsu prototype", "reward");
        RunCheckpoints.Require(state).Random.NextUInt64(address);
        var older = manager.ToSave(null);
        var olderPayload = RitsuCheckpointPrototype.Document(older);

        setRun.Invoke(data, [state, new RitsuProbeData { Marker = 202 }]);
        setPlayer.Invoke(playerData, [state, 1UL, new RitsuProbeData { Marker = 22 }]);
        RunCheckpoints.Require(state).Random.NextUInt64(address);
        var newer = manager.ToSave(null);
        Assert(JsonNode.DeepEquals(olderPayload, RitsuCheckpointPrototype.Document(older)), "RitsuLib capture remains frozen after live state changes");
        manager.CleanUp();

        var store = new MockGodotFileIo("ritsu-prototype");
        var saves = new RunSaveManager(1, store, new MigrationManager(store), false);
        var path = RunSaveManager.GetRunSavePath(1, RunSaveManager.runSaveFileName);
        await saves.SaveRun(older, false);
        var olderJson = store.ReadFile(path)!;
        await saves.SaveRun(newer, false);
        var newerJson = store.ReadFile(path)!;
        Assert(saves.HasRunSave && store.ReadFile(path + ".backup") == olderJson && olderJson != newerJson,
            "standard path holds different complete primary and backup checkpoints");

        // Keep valid JSON and a newer RitsuLib payload while forcing vanilla primary recovery.
        var brokenPrimary = JsonNode.Parse(newerJson)!.AsObject();
        brokenPrimary["players"] = "invalid";
        store.WriteFile(path, brokenPrimary.ToJsonString());
        store.WriteFile(path + ".backup", olderJson);
        var rejected = saves.LoadRunSave();
        Assert(!rejected.Success, "invalid run data rejects continuation instead of falling back");

        // Demonstrate the default hook's reread independently of the strict SameSeed loader.
        var selected = RunCheckpoints.Decode(olderJson, saves.SchemaVersion);
        RitsuCheckpointPrototype.Call("RunData.Patches.RunSavedDataLoadRunSavePatch", "Postfix", saves, new ReadSaveResult<SerializableRun>(selected));
        Assert(JsonNode.DeepEquals(RitsuCheckpointPrototype.Document(selected), JsonNode.Parse(newerJson)!["_ritsulib"]),
            "negative control: default RitsuLib hook replaces backup payload with primary payload");
        RitsuCheckpointPrototype.ProtectLoadedSnapshot = true;
        selected = RunCheckpoints.Decode(olderJson, saves.SchemaVersion);
        RitsuCheckpointPrototype.Call("RunData.Patches.RunSavedDataLoadRunSavePatch", "Postfix", saves, new ReadSaveResult<SerializableRun>(selected));
        RitsuCheckpointPrototype.RestoreSelectedPayload(new ReadSaveResult<SerializableRun>(selected));
        Assert(JsonNode.DeepEquals(olderPayload, RitsuCheckpointPrototype.Document(selected)), "bridge restores selected payload after RitsuLib's primary reread");

        foreach (var primary in new string?[] { newerJson, null, "", "{torn" })
        {
            if (primary == null)
                store.DeleteFile(path);
            else
                store.WriteFile(path, primary);
            store.WriteFile(path + ".backup", olderJson);
            var loaded = saves.LoadRunSave();
            Assert(loaded.Success, $"load succeeds for primary: {PrimaryLabel(primary)}");
            var snapshot = loaded.SaveData!;
            var usePrimary = primary == newerJson;
            Assert(JsonNode.DeepEquals(RitsuCheckpointPrototype.Document(snapshot), JsonNode.Parse(usePrimary ? newerJson : olderJson)!["_ritsulib"]),
                $"selected snapshot retains matching payload: {PrimaryLabel(primary)}");
            var restored = RunState.FromSerializable(snapshot);
            object?[] actualRun = [restored, null];
            var found = (bool)AccessTools.Method(data.GetType(), "TryGet").Invoke(data, actualRun)!;
            var marker = (actualRun[1] as RitsuProbeData)?.Marker;
            Assert(found && marker == (usePrimary ? 202 : 101), $"matching RitsuLib runtime restored: {PrimaryLabel(primary)} (found={found}, marker={marker})");
            Assert(RunCheckpoints.Require(snapshot).Random.Position(address) == (usePrimary ? 2 : 1), "SameSeed state comes from the same checkpoint");
            var reloads = snapshot.NumReloads;
            await manager.SetUpSavedSingleplayer(restored, snapshot);
            var rewritten = SaveManager.Instance.LoadRunSave().SaveData!;
            Assert(rewritten.NumReloads == reloads + 1 && JsonNode.DeepEquals(RitsuCheckpointPrototype.Document(snapshot), RitsuCheckpointPrototype.Document(rewritten)),
                "real reload-count rewrite preserves the selected RitsuLib payload");
            manager.CleanUp();
        }

        Action<JsonObject>[] corruptions =
        [
            root => Slot(root, "run")["schema"] = 999,
            root => Slot(root, "run")["data"]!["Marker"] = "invalid integer",
            root => Slot(root, "player")["players"]!["invalid player ID"] = new JsonObject { ["Marker"] = 99 },
            root => Slot(root, "player")["players"]!["2"] = null
        ];
        string[] labels = ["silent schema rejection", "caught deserialization exception", "silently skipped player ID", "silently skipped player data"];
        for (var index = 0; index < corruptions.Length; index++)
        {
            var invalid = JsonNode.Parse(newerJson)!.AsObject();
            corruptions[index](invalid);
            store.WriteFile(path, invalid.ToJsonString());
            store.WriteFile(path + ".backup", olderJson);
            var primaryBefore = store.ReadFile(path);
            var backupBefore = store.ReadFile(path + ".backup");
            var snapshot = saves.LoadRunSave().SaveData!;
            var restored = RunState.FromSerializable(snapshot);
            if (index < 2)
            {
                object?[] value = [restored, null];
                Assert(!(bool)AccessTools.Method(data.GetType(), "TryGet").Invoke(data, value)!, $"negative control: RitsuLib returns with missing runtime value after {labels[index]}");
                var exported = RitsuCheckpointPrototype.Call("RunData.RunSavedDataRegistry", "BuildDocumentFromRun", restored)!;
                var exportedRoot = (JsonObject)AccessTools.Method(exported.GetType(), "ToRootObject").Invoke(exported, null)!;
                Assert(JsonNode.DeepEquals(invalid["_ritsulib"], exportedRoot["_ritsulib"]), "negative control: exported JSON still matches after failed restoration");
            }
            var globalStore = (ISaveStore)AccessTools.Field(typeof(SaveManager), "_saveStore").GetValue(SaveManager.Instance)!;
            var globalBefore = globalStore.ReadFile(path);
            var globalBackupBefore = globalStore.ReadFile(path + ".backup");
            var reloads = snapshot.NumReloads;
            await ExpectRitsuRejection(() => manager.SetUpSavedSingleplayer(restored, snapshot), labels[index]);
            Assert(snapshot.NumReloads == reloads && !manager.IsInProgress &&
                store.ReadFile(path) == primaryBefore && store.ReadFile(path + ".backup") == backupBefore &&
                globalStore.ReadFile(path) == globalBefore && globalStore.ReadFile(path + ".backup") == globalBackupBefore,
                $"{labels[index]} aborts before rewriting either checkpoint");
        }
    }

    private static string PrimaryLabel(string? value) => value switch { null => "missing", "" => "empty", "{torn" => "broken JSON", _ => "valid" };

    private static JsonObject Slot(JsonObject root, string key) => root["_ritsulib"]!["run_saved_data"]!["SameSeed.RitsuPrototype"]![key]!.AsObject();

    private static async Task ExpectRitsuRejection(Func<Task> action, string label)
    {
        try { await action(); }
        catch (InvalidDataException error)
        {
            Assert(error.Message.Contains("RitsuLib", StringComparison.Ordinal), $"restoration failure explained: {label}");
            return;
        }
        throw new InvalidOperationException($"Expected restoration rejection: {label}");
    }
}
