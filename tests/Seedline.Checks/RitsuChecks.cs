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

namespace Seedline.Checks;

public static partial class Checks
{
    private static Type RitsuType(string name) => AccessTools.TypeByName("STS2RitsuLib." + name)
        ?? throw new InvalidOperationException($"RitsuLib type missing: {name}");

    private static object? CallRitsu(string type, string method, params object?[] args) =>
        AccessTools.Method(RitsuType(type), method).Invoke(null, args);

    private static JsonNode RitsuDocument(SerializableRun save) =>
        JsonNode.Parse((string?)CallRitsu("RunData.RunSavedDataRegistry", "BuildPayloadFromSerializable", save)
            ?? "{\"_ritsulib\":{\"version\":1,\"run_saved_data\":{}}}")!["_ritsulib"]!;

    public sealed class RitsuProbeData
    {
        public static bool FailCapture { get; set; }
        private int marker;
        public int Marker
        {
            get => FailCapture ? throw new InvalidOperationException("Injected capture failure.") : marker;
            set => marker = value;
        }
    }

    private static async Task CheckRitsu()
    {
        var mod = ModManager.GetLoadedMods().Single(m => m.manifest?.id == "STS2-RitsuLib");
        Assert(mod.manifest?.version == "0.6.14" && mod.errors is not { Count: > 0 }, "official RitsuLib v0.6.14 loaded");
        var framework = RitsuType("RitsuLibFramework");
        Assert((bool)AccessTools.Property(framework, "IsActive").GetValue(null)! &&
            framework.Assembly.GetName().Version == new Version(0, 6, 14, 0), "RitsuLib runtime initialized at pinned version");
        var order = ModManager.GetLoadedMods().Select(m => m.manifest?.id).ToList();
        Assert((order.IndexOf("STS2-RitsuLib") < order.IndexOf(ModEntry.Id)) ==
            (System.Environment.GetEnvironmentVariable("SEEDLINE_RITSU_FIRST") == "1"), "requested initializer order exercised");

        var monitor = new Harmony("Seedline.Checks.RitsuWrites");
        foreach (var storeType in new[] { typeof(MockGodotFileIo), typeof(GodotFileIo), typeof(CloudSaveStore) })
            foreach (var method in new[] { "WriteFile", "WriteFileAsync" })
                monitor.Patch(AccessTools.Method(storeType, method, [typeof(string), typeof(byte[])]),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(Checks), nameof(CheckWriteContext))) { priority = Priority.Last });

        var registry = CallRitsu("RunData.RunSavedDataStore", "For", "Seedline.Checks")!;
        var data = AccessTools.Method(registry.GetType(), "Register").MakeGenericMethod(typeof(RitsuProbeData))
            .Invoke(registry, ["run", null, null])!;
        var playerData = AccessTools.Method(registry.GetType(), "RegisterPerPlayer").MakeGenericMethod(typeof(RitsuProbeData))
            .Invoke(registry, ["player", null, null])!;
        var setRun = AccessTools.Method(data.GetType(), "Set");
        var setPlayer = AccessTools.Method(playerData.GetType(), "Set", [typeof(RunState), typeof(ulong), typeof(RitsuProbeData)]);


        var manager = RunManager.Instance;
        var state = RunState.CreateForNewRun([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
            ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), [], GameMode.Standard, false, 7, "RITSUPROBE");
        manager.SetUpNewSingleplayer(state, true);
        var empty = manager.ToSave(null);
        var emptyJson = System.Text.Encoding.UTF8.GetString(RunCheckpoints.Encode(empty));
        Assert(JsonNode.Parse(emptyJson)!["_ritsulib"]!["run_saved_data"]!.AsObject().Count == 0,
            "RitsuLib with no data captures an explicit empty envelope");
        var emptySave = RunCheckpoints.Decode(emptyJson, empty.SchemaVersion);
        RitsuBridge.ValidateRestored(RunState.FromSerializable(emptySave), RunCheckpoints.Require(emptySave).Ritsu);
        Assert(true, "empty RitsuLib checkpoint restores successfully");
        setRun.Invoke(data, [state, new RitsuProbeData { Marker = 101 }]);
        setPlayer.Invoke(playerData, [state, 1UL, new RitsuProbeData { Marker = 11 }]);
        var address = RandomAddress.For("ritsu check", "reward");
        RunCheckpoints.Require(state).Random.NextUInt64(address);
        var older = manager.ToSave(null);
        var olderPayload = RitsuDocument(older);

        setRun.Invoke(data, [state, new RitsuProbeData { Marker = 202 }]);
        setPlayer.Invoke(playerData, [state, 1UL, new RitsuProbeData { Marker = 22 }]);
        RunCheckpoints.Require(state).Random.NextUInt64(address);
        var newer = manager.ToSave(null);
        Assert(JsonNode.DeepEquals(olderPayload, RitsuDocument(older)), "RitsuLib capture remains frozen after live state changes");
        manager.CleanUp();

        var store = new MockGodotFileIo("ritsu-check");
        var saves = new RunSaveManager(1, store, new MigrationManager(store), false);
        var path = RunSaveManager.GetRunSavePath(1, RunSaveManager.runSaveFileName);
        await saves.SaveRun(older, false);
        var olderJson = store.ReadFile(path)!;
        await saves.SaveRun(newer, false);
        var newerJson = store.ReadFile(path)!;
        Assert(saves.HasRunSave && store.ReadFile(path + ".backup") == olderJson && olderJson != newerJson,
            "standard path holds different complete primary and backup checkpoints");
        store.ShouldFailWrites = true;
        await ExpectFailure(() => saves.SaveRun(older, false));
        store.ShouldFailWrites = false;
        Assert(store.ReadFile(path) == newerJson && store.ReadFile(path + ".backup") == olderJson,
            "combined write failure preserves both complete files");
        await saves.SaveRun(newer, false);

        foreach (var payload in new JsonNode?[] { null, new JsonObject { ["version"] = 2, ["run_saved_data"] = new JsonObject() },
            new JsonObject { ["version"] = 1, ["run_saved_data"] = new JsonObject { ["malformed"] = 42 } } })
        {
            var invalidPayload = JsonNode.Parse(newerJson)!.AsObject();
            if (payload == null)
                invalidPayload.Remove("_ritsulib");
            else
                invalidPayload["_ritsulib"] = payload;
            var invalidJson = invalidPayload.ToJsonString();
            store.WriteFile(path, invalidJson);
            store.WriteFile(path + ".backup", olderJson);
            Assert(saves.LoadRunSave().Status == ReadSaveStatus.ValidationFailed && store.ReadFile(path) == invalidJson &&
                store.ReadFile(path + ".backup") == olderJson, "missing or incomplete RitsuLib payload rejects without partial recovery");
        }

        // Keep valid JSON and a newer RitsuLib payload while forcing vanilla primary recovery.
        var brokenPrimary = JsonNode.Parse(newerJson)!.AsObject();
        brokenPrimary["players"] = "invalid";
        store.WriteFile(path, brokenPrimary.ToJsonString());
        store.WriteFile(path + ".backup", olderJson);
        var rejected = saves.LoadRunSave();
        Assert(!rejected.Success, "invalid run data rejects continuation instead of falling back");

        // Demonstrate the default hook's reread independently of the strict Seedline loader.
        var selected = RunCheckpoints.Decode(olderJson, saves.SchemaVersion);
        CallRitsu("RunData.Patches.RunSavedDataLoadRunSavePatch", "Postfix", saves, new ReadSaveResult<SerializableRun>(selected));
        Assert(JsonNode.DeepEquals(RitsuDocument(selected), JsonNode.Parse(newerJson)!["_ritsulib"]),
            "negative control: default RitsuLib hook replaces backup payload with primary payload");
        await saves.SaveRun(selected, false);
        Assert(JsonNode.DeepEquals(JsonNode.Parse(store.ReadFile(path)!)!["_ritsulib"], olderPayload),
            "writes use the frozen payload even if RitsuLib's attached document changes");

        selected = RunCheckpoints.Decode(olderJson, saves.SchemaVersion);
        CallRitsu("RunData.Patches.RunSavedDataLoadRunSavePatch", "Postfix", saves, new ReadSaveResult<SerializableRun>(selected));
        RitsuBridge.Attach(selected, RunCheckpoints.Require(selected).Ritsu);
        Assert(JsonNode.DeepEquals(olderPayload, RitsuDocument(selected)), "bridge restores selected payload after RitsuLib's primary reread");

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
            Assert(JsonNode.DeepEquals(RitsuDocument(snapshot), JsonNode.Parse(usePrimary ? newerJson : olderJson)!["_ritsulib"]),
                $"selected snapshot retains matching payload: {PrimaryLabel(primary)}");
            var restored = RunState.FromSerializable(snapshot);
            object?[] actualRun = [restored, null];
            var found = (bool)AccessTools.Method(data.GetType(), "TryGet").Invoke(data, actualRun)!;
            var marker = (actualRun[1] as RitsuProbeData)?.Marker;
            Assert(found && marker == (usePrimary ? 202 : 101), $"matching RitsuLib runtime restored: {PrimaryLabel(primary)} (found={found}, marker={marker})");
            Assert(RunCheckpoints.Require(snapshot).Random.Position(address) == (usePrimary ? 2 : 1), "Seedline state comes from the same checkpoint");
            var reloads = snapshot.NumReloads;
            await manager.SetUpSavedSingleplayer(restored, snapshot);
            var rewritten = SaveManager.Instance.LoadRunSave().SaveData!;
            Assert(rewritten.NumReloads == reloads + 1 && JsonNode.DeepEquals(RitsuDocument(snapshot), RitsuDocument(rewritten)),
                "real reload-count rewrite preserves the selected RitsuLib payload");
            manager.CleanUp();
        }

        Action<JsonObject>[] corruptions =
        [
            root => Slot(root, "run")["schema"] = 999,
            root => Slot(root, "run")["data"]!["Marker"] = "invalid integer",
            root => Slot(root, "player")["players"]!["invalid player ID"] = new JsonObject { ["Marker"] = 99 },
            root => Slot(root, "player")["players"]!["2"] = null,
            root => Slot(root, "run")["kind"] = "player",
            root => Slot(root, "run")["kind"] = 42
        ];
        string[] labels = ["silent schema rejection", "caught deserialization exception", "silently skipped player ID", "silently skipped player data", "wrong slot kind", "invalid slot kind"];
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
                var exported = CallRitsu("RunData.RunSavedDataRegistry", "BuildDocumentFromRun", restored)!;
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

        var good = RunCheckpoints.Decode(newerJson, newer.SchemaVersion);
        var goodRun = RunState.FromSerializable(good);
        await manager.SetUpSavedSingleplayer(goodRun, good);
        setRun.Invoke(data, [goodRun, new RitsuProbeData { Marker = 303 }]);
        var disk = (ISaveStore)AccessTools.Field(typeof(SaveManager), "_saveStore").GetValue(SaveManager.Instance)!;
        var before = disk.ReadFile(path);
        var backup = disk.ReadFile(path + ".backup");
        RitsuProbeData.FailCapture = true;
        var failed = false;
        var diskManager = (RunSaveManager)AccessTools.Field(typeof(SaveManager), "_runSaveManager").GetValue(SaveManager.Instance)!;
        try { await diskManager.SaveRun(null); }
        catch (Exception error) when (error.ToString().Contains("Injected capture failure", StringComparison.Ordinal)) { failed = true; }
        finally { RitsuProbeData.FailCapture = false; }
        Assert(failed && disk.ReadFile(path) == before && disk.ReadFile(path + ".backup") == backup,
            "capture failure aborts before changing either checkpoint");
        await SaveManager.Instance.SaveRun(null, saveProgress: false);
        Assert(SaveManager.Instance.LoadRunSave().Success, "combined saving resumes after capture failure without leaked injection context");
        manager.CleanUp();
        Assert(observedWrites > 0, "real store hooks observe complete bytes without a RitsuLib injection context");
        monitor.UnpatchAll(monitor.Id);
    }

    private static int observedWrites;

    private static void CheckWriteContext(string path, byte[] bytes)
    {
        if (!path.EndsWith("/current_run.save", StringComparison.Ordinal))
            return;
        try { if (JsonNode.Parse(bytes)?["seedline"] == null) return; }
        catch (System.Text.Json.JsonException) { return; }
        var context = AccessTools.Field(RitsuType("RunData.Patches.RunSavedDataPatchHelpers"), "ActiveSaveRunCaptures").GetValue(null)!;
        var stack = AccessTools.Property(context.GetType(), "Value").GetValue(context);
        if (stack != null && (int)AccessTools.Property(stack.GetType(), "Count").GetValue(stack)! != 0)
            throw new InvalidOperationException("RitsuLib injection context escaped the replacing save prefix.");
        observedWrites++;
    }

    private static string PrimaryLabel(string? value) => value switch { null => "missing", "" => "empty", "{torn" => "broken JSON", _ => "valid" };

    private static JsonObject Slot(JsonObject root, string key) => root["_ritsulib"]!["run_saved_data"]!["Seedline.Checks"]![key]!.AsObject();

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
