using System.Text;
using System.Text.Json.Nodes;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;

namespace Seedline.Checks;

[ModInitializer(nameof(Initialize))]
public static partial class Checks
{
    private static readonly List<string> passed = [];

    public static void Initialize()
    {
        if (System.Environment.GetEnvironmentVariable("SEEDLINE_CHECK_ROOT") is not { } root ||
            !Path.GetFullPath(OS.GetUserDataDir()).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Checks require an isolated user-data directory.");
        _ = Run(root);
    }

    private static async Task Run(string root)
    {
        try
        {
            await (NGame.Instance ?? throw new InvalidOperationException("Game has not started.")).GameStartupComplete;
            CheckEnvironmentDiagnostics();
            await CheckExportMenu();
            if (System.Environment.GetEnvironmentVariable("SEEDLINE_RITSU_CHECKS") == "1")
                await CheckRitsu();
            Assert(ModManager.GetLoadedMods().Any(m => m.manifest?.id == ModEntry.Id && m.errors is not { Count: > 0 }), "built-in loader and explicit initializer");
            Assert(Harmony.GetPatchInfo(AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer)))?.Owners.Contains(ModEntry.Id) == true &&
                Harmony.GetPatchInfo(AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer)))?.Owners.Contains(ModEntry.Id) == true, "both multiplayer bypass entry points installed");

            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var state = RunState.CreateForNewRun([player], ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), [], GameMode.Standard, false, 7, "CHECKPOINTCHECK");
            var manager = RunManager.Instance;
            manager.SetUpNewSingleplayer(state, true);
            var address = RandomAddress.For("foundation check", "reward");
            var live = RunCheckpoints.Require(state).Random;
            var openingRoll = live.NextUInt64(address);
            Assert(AddressRandom.UInt64("SEEDLINE", RandomAddress.For("gold reward", "normal").At(0)) == 5879364479044975606UL,
                "standalone and combined configurations match the fixed random vector");
            var snapshot = manager.ToSave(null);
            var provenance = RunCheckpoints.Require(snapshot);
            var schema = snapshot.SchemaVersion;
            var original = RunCheckpoints.Encode(snapshot);
            Assert(JsonNode.Parse(original)!["seedline"]!["schema_version"]!.GetValue<int>() == RunCheckpoints.SchemaVersion,
                "checkpoint records the current Seedline schema");
            var release = JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "release_info.json")))!;
            Assert(JsonNode.Parse(original)!["seedline"]!["game_build"]!.GetValue<string>() == $"{release["version"]}/{release["commit"]}",
                "checkpoint records the loaded game build");
            Assert(provenance.Seed == "CHECKPOINTCHECK" && provenance.RunId != Guid.Empty, "new run receives provenance before initialization");

            await SaveManager.Instance.SaveRun(null, saveProgress: false);
            await CheckDiagnosticExport(snapshot);
            var regeneratedRoll = live.NextUInt64(address);
            Assert(RunCheckpoints.Encode(snapshot).SequenceEqual(original), "capture freezes random state before later generation");
            var loaded = SaveManager.Instance.LoadRunSave();
            Assert(loaded.Success && loaded.SaveData != null, "real Godot file checkpoint write/read");
            manager.CleanUp();
            var restored = RunState.FromSerializable(loaded.SaveData!);
            await manager.SetUpSavedSingleplayer(restored, loaded.SaveData!);
            Assert(SameState(RunCheckpoints.Require(manager.ToSave(null)), provenance), "live run restoration retains state");
            var rewritten = SaveManager.Instance.LoadRunSave().SaveData!;
            Assert(rewritten.NumReloads == snapshot.NumReloads + 1 && SameState(RunCheckpoints.Require(rewritten), provenance), "real reload-count rewrite keeps checkpoint state");
            var restoredRandom = RunCheckpoints.Require(restored).Random;
            Assert(restoredRandom.NextUInt64(address) == regeneratedRoll && restoredRandom.Position(address) == 2,
                "pre-generation checkpoint replays one draw without double consumption");
            Assert(SameState(RunCheckpoints.Require(loaded.SaveData!), provenance), "restored run cannot mutate its loaded snapshot");
            await SaveManager.Instance.SaveRun(null, saveProgress: false);
            var committed = SaveManager.Instance.LoadRunSave().SaveData!;
            var followingRoll = restoredRandom.NextUInt64(address);
            manager.CleanUp();
            var continued = RunState.FromSerializable(committed);
            await manager.SetUpSavedSingleplayer(continued, committed);
            var continuedRandom = RunCheckpoints.Require(continued).Random;
            Assert(continuedRandom.Position(address) == 2 && continuedRandom.NextUInt64(address) == followingRoll,
                "post-generation checkpoint continues from the next draw");
            Assert(RunCheckpoints.Require(committed).Random.Position(address) == 2 &&
                RunCheckpoints.Require(SaveManager.Instance.LoadRunSave().SaveData!).Random.Position(address) == 2,
                "reload rewrite preserves frozen positions after live advancement");
            manager.CleanUp();

            await CheckStores(snapshot, schema, original, provenance);
            CheckRejections(snapshot, schema, original);
            await CheckCloud(snapshot, schema);
            await CheckDisk(snapshot, schema);
            await CheckDiagnostics(snapshot, schema);

            var fresh = RunState.CreateForNewRun([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), [], GameMode.Standard, false, 7, "CHECKPOINTCHECK");
            manager.SetUpNewSingleplayer(fresh, true);
            Assert(RunCheckpoints.Require(manager.ToSave(null)).RunId != provenance.RunId, "same-seed new runs have independent provenance");
            Assert(RunCheckpoints.Require(fresh).Random.NextUInt64(address) == openingRoll && live.Position(address) == 2,
                "same-seed runs reproduce rolls independently of UUID and other run consumption");
            manager.CleanUp();
            await CheckModifierCheckpoints();
            await CheckGameplayRewards();
            CheckDecisionEvidence();
            CheckDecisionRecordingCost();
            await System.IO.File.WriteAllTextAsync(Path.Combine(root, "result.json"), System.Text.Json.JsonSerializer.Serialize(new { success = true, checks = passed }));
            ((SceneTree)Engine.GetMainLoop()).Quit();
        }
        catch (Exception error)
        {
            await System.IO.File.WriteAllTextAsync(Path.Combine(root, "result.json"), System.Text.Json.JsonSerializer.Serialize(new { success = false, checks = passed, error = error.ToString() }));
            ((SceneTree)Engine.GetMainLoop()).Quit(1);
        }
    }

    private static async Task CheckStores(SerializableRun snapshot, int schema, byte[] original, CheckpointState provenance)
    {
        foreach (var synchronous in new[] { false, true })
        {
            var store = new MockGodotFileIo("checks");
            var profiles = new StaticProfileIdProvider(1);
            var manager = new RunSaveManager(store, new MigrationManager(store), profiles, synchronous);
            var path = RunSaveManager.GetRunSavePath(1, RunSaveManager.runSaveFileName);
            var legacyPath = RunSaveManager.GetRunSavePath(1, "seedline_run.save", true);
            store.WriteFile(legacyPath, "development sentinel");
            var ordinary = JsonSerializationUtility.ToJson(snapshot);
            store.WriteFile(path, ordinary);
            store.WriteFile(path + ".backup", "ordinary backup sentinel");
            Assert(manager.LoadRunSave().Status == ReadSaveStatus.ValidationFailed && store.ReadFile(path) == ordinary &&
                store.ReadFile(path + ".backup") == "ordinary backup sentinel", "ordinary modded save rejected with both files preserved");
            var savedEvents = 0;
            manager.Saved += () => savedEvents++;
            await manager.SaveRun(snapshot, false);
            Assert(manager.HasRunSave && savedEvents == 1, $"{synchronous}: save discovery and notification");
            var loaded = manager.LoadRunSave().SaveData!;
            Assert(SameState(RunCheckpoints.Require(loaded), provenance) && RunCheckpoints.Encode(loaded).SequenceEqual(original), $"{synchronous}: complete snapshot round-trip");
            loaded.NumReloads++;
            await manager.SaveRun(loaded, false);
            Assert(SameState(RunCheckpoints.Require(manager.LoadRunSave().SaveData!), provenance), $"{synchronous}: rewrite without active run");
            Assert(store.ReadFile(path + ".backup") == Encoding.UTF8.GetString(original), $"{synchronous}: backup contains both halves");
            store.WriteFile(path, "{torn");
            Assert(manager.LoadRunSave().SaveData!.NumReloads == snapshot.NumReloads + 1, $"{synchronous}: torn primary recovers paired backup");
            store.DeleteFile(path);
            Assert(manager.HasRunSave && manager.LoadRunSave().Success, $"{synchronous}: backup-only discovery and recovery");
            var otherProfile = new RunSaveManager(2, store, new MigrationManager(store), synchronous);
            Assert(!otherProfile.HasRunSave, $"{synchronous}: profile isolation");
            store.ShouldFailWrites = true;
            await ExpectFailure(() => manager.SaveRun(loaded, false));
            Assert(savedEvents == 2 && store.FileExists(path + ".backup"), $"{synchronous}: failed write preserves checkpoint and suppresses Saved");
            store.ShouldFailWrites = false;
            manager.DeleteCurrentRun();
            Assert(!manager.HasRunSave && store.ReadFile(legacyPath) == "development sentinel", $"{synchronous}: completion/abandon deletes standard checkpoints and preserves development saves");
        }
        snapshot.PreFinishedRoom = new SerializableRoom
        {
            RoomType = RoomType.Monster,
            ParentEventId = ModelDb.Event<BattlewornDummy>().Id,
            IsPreFinished = true,
            ShouldResumeParentEvent = true,
            EncounterState = new Dictionary<string, string> { ["test_payload"] = "preserved" }
        };
        var parent = RunCheckpoints.Decode(Encoding.UTF8.GetString(RunCheckpoints.Encode(snapshot)), schema).PreFinishedRoom!;
        Assert(parent.ParentEventId == snapshot.PreFinishedRoom.ParentEventId && parent.IsPreFinished && parent.EncounterState["test_payload"] == "preserved", "parent-event reconstruction payload round-trip");
        snapshot.PreFinishedRoom = null;
    }

    private static void CheckRejections(SerializableRun snapshot, int schema, byte[] original)
    {
        var path = RunSaveManager.GetRunSavePath(1, RunSaveManager.runSaveFileName);
        var json = Encoding.UTF8.GetString(original);
        Action<JsonObject>[] mutations =
        [
            root => { if (!root.Remove("_ritsulib")) root["_ritsulib"] = new JsonObject { ["version"] = 1, ["run_saved_data"] = new JsonObject() }; },
            root => root.Remove("seedline"),
            root => root["seedline"]!["schema_version"] = RunCheckpoints.SchemaVersion + 1,
            root => root["seedline"]!["schema_version"] = RunCheckpoints.SchemaVersion - 1,
            root => root["seedline"]!.AsObject().Remove("schema_version"),
            root => root["seedline"]!["schema_version"] = "invalid",
            root => root["seedline"]!.AsObject().Remove("rewards"),
            root => root["seedline"]!.AsObject().Remove("random_positions"),
            root => root["seedline"]!.AsObject().Remove("selection"),
            root => root["seedline"]!["selection"] = null,
            root => root["seedline"]!["selection"]!["Clocks"] = null,
            root => root["seedline"]!.AsObject().Remove("events"),
            root => root["seedline"]!["events"] = null,
            root => root["seedline"]!["events"]!["Conditions"] = null,
            root => root["seedline"]!["random_positions"] = null,
            root => root["seedline"]!["random_positions"] = new JsonArray(),
            root => root["seedline"]!["random_positions"] = new JsonObject { ["invalid address"] = 1 },
            root => root["seedline"]!["random_positions"] = new JsonObject { [RandomAddress.For("counter").Key] = -1 },
            root => root["seedline"]!["random_positions"] = new JsonObject { [RandomAddress.For("counter").Key] = "1" },
            root => root["seedline"]!["game_build"] = "another build",
            root => root["seedline"]!["game_build"] = "v0.113.0/16ac0fe3",
            root => root["seedline"]!["run_id"] = Guid.Empty.ToString(),
            root => root["seedline"]!["seed"] = "another seed",
            root => root["schema_version"] = schema + 1,
            root => root["modifiers"] = null,
            root => root["modifiers"] = new JsonArray((JsonNode?)null),
            root => root["modifiers"] = new JsonArray(new JsonObject { ["id"] = null }),
            root => root["is_custom_game"] = true,
            root => root["rng"] = null,
            root => root["players"] = new JsonArray((JsonNode?)null),
            root => root["acts"] = null,
            root => root["acts"] = new JsonArray((JsonNode?)null),
            root => root["acts"] = new JsonArray(),
            root => root.Remove("acts"),
            root => root["current_act_index"] = -1,
            root => root["current_act_index"] = root["acts"]!.AsArray().Count
        ];
        foreach (var mutate in mutations)
        {
            var root = JsonNode.Parse(json)!.AsObject();
            mutate(root);
            var invalid = root.ToJsonString();
            var store = new MockGodotFileIo("reject");
            store.WriteFile(path, json);
            store.WriteFile(path, invalid);
            var result = CheckpointStore.Read(store, path, schema);
            Assert(result.Status == ReadSaveStatus.ValidationFailed && store.ReadFile(path) == invalid && store.ReadFile(path + ".backup") == json,
                $"reject incompatible/missing state without fallback or modification ({passed.Count})");
        }
        var vanilla = JsonSerializationUtility.ToJson(snapshot);
        try
        {
            RunCheckpoints.Encode(JsonSerializationUtility.FromJson<SerializableRun>(vanilla).SaveData!);
            throw new Exception("Unbound vanilla save was accepted.");
        }
        catch (InvalidDataException)
        {
            Assert(true, "existing vanilla run cannot acquire mod provenance");
        }
        var mods = (List<Mod>)AccessTools.Field(typeof(ModManager), "_mods").GetValue(null)!;
        var catfood = new Mod { path = "test", state = ModLoadState.Loaded, manifest = new ModManifest { id = "CFCRacingMod" } };
        mods.Add(catfood);
        try
        {
            var fresh = RunState.CreateForNewRun([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(), [], GameMode.Standard, false, 7, "CFCRACINGCHECK");
            RunCheckpoints.Start(fresh);
            Assert(RunCheckpoints.Require(fresh).Seed == "CFCRACINGCHECK", "CFCRacing presence does not block new runs");
            var store = new MockGodotFileIo("cfcracing");
            store.WriteFile(path, json);
            var loaded = CheckpointStore.Read(store, path, schema);
            Assert(loaded.Success, "CFCRacing presence does not block checkpoint reads");
            var restored = RunState.FromSerializable(loaded.SaveData!);
            RunCheckpoints.Restore(restored, loaded.SaveData!);
            Assert(SameState(RunCheckpoints.Require(restored), RunCheckpoints.Require(snapshot)),
                "CFCRacing presence does not block checkpoint restoration");
        }
        finally { mods.Remove(catfood); }

        foreach (var mode in new[] { GameMode.Blitz, GameMode.Daily })
        {
            snapshot.GameMode = mode;
            try
            {
                RunCheckpoints.Encode(snapshot);
                throw new Exception("Unsupported mode was accepted.");
            }
            catch (InvalidDataException) { Assert(true, $"{mode} rejected"); }
        }
        snapshot.GameMode = GameMode.Standard;
    }

    private static bool SameState(CheckpointState left, CheckpointState right) =>
        left.RunId == right.RunId && left.Seed == right.Seed && RandomJson(left.Random) == RandomJson(right.Random);

    private static string RandomJson(RandomState state)
    {
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            state.Write(writer);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static async Task CheckCloud(SerializableRun snapshot, int schema)
    {
        var local = new MockGodotFileIo("local") { getCurrentTime = () => DateTimeOffset.UtcNow };
        var remote = new MockCloudGodotFileIo("remote") { getCurrentTime = () => DateTimeOffset.UtcNow };
        var store = new CloudSaveStore(local, remote);
        var path = RunSaveManager.GetRunSavePath(1, RunSaveManager.runSaveFileName);
        for (var profile = 1; profile <= 3; profile++)
            await CheckpointStore.Write(store, RunSaveManager.GetRunSavePath(profile, RunSaveManager.runSaveFileName), snapshot, false, null);
        Assert(local.ReadFile(path) == remote.ReadFile(path), "cloud receives one combined snapshot");
        remote.ShouldFailWrites = true;
        snapshot.NumReloads++;
        await CheckpointStore.Write(store, path, snapshot, false, null);
        Assert(CheckpointStore.Read(store, path, schema).SaveData!.NumReloads == snapshot.NumReloads &&
            local.ReadFile(path) != remote.ReadFile(path), "cloud failure preserves newer local snapshot");
        remote.ShouldFailWrites = false;
        await new SaveManager(store).OverwriteCloudWithLocal();
        Assert(local.ReadFile(path) == remote.ReadFile(path), "SaveManager cloud retry includes mod checkpoint");
        var downloaded = new MockGodotFileIo("download") { getCurrentTime = () => DateTimeOffset.UtcNow };
        var downloadStore = new CloudSaveStore(downloaded, remote);
        var downloadManager = new SaveManager(downloadStore);
        await downloadManager.SyncCloudToLocal();
        for (var profile = 1; profile <= 3; profile++)
        {
            var profilePath = RunSaveManager.GetRunSavePath(profile, RunSaveManager.runSaveFileName);
            Assert(CheckpointStore.Read(downloadStore, profilePath, schema).Success && downloaded.ReadFile(profilePath) == remote.ReadFile(profilePath),
                $"SaveManager startup downloads profile {profile} checkpoint");
        }
        downloaded.WriteFile(path, "{torn");
        remote.DeleteFile(path);
        await downloadManager.SyncCloudToLocal();
        Assert(!downloaded.FileExists(path) && !downloaded.FileExists(path + ".backup"), "vanilla cloud-authoritative deletion includes primary and backup");
        downloaded.WriteFile(path + ".backup", System.Text.Encoding.UTF8.GetString(RunCheckpoints.Encode(snapshot)));
        await downloadManager.SyncCloudToLocal();
        Assert(downloaded.FileExists(path + ".backup"), "cloud synchronization preserves a lone local backup");

        var secondPath = RunSaveManager.GetRunSavePath(2, RunSaveManager.runSaveFileName);
        var historyPath = RunHistorySaveManager.GetHistoryPath(2) + $"/{snapshot.StartTime}.run";
        downloaded.WriteFile(historyPath, "{}");
        AccessTools.Method(typeof(SaveManager), "CleanupStaleCurrentRunSaves").Invoke(downloadManager, null);
        Assert(!downloaded.FileExists(secondPath), "completed mod run is removed by history-based cleanup");

        var thirdPath = RunSaveManager.GetRunSavePath(3, RunSaveManager.runSaveFileName);
        var invalid = JsonNode.Parse(downloaded.ReadFile(thirdPath)!)!.AsObject();
        invalid.Remove("seedline");
        downloaded.WriteFile(thirdPath, invalid.ToJsonString());
        downloaded.WriteFile(RunHistorySaveManager.GetHistoryPath(3) + $"/{snapshot.StartTime}.run", "{}");
        AccessTools.Method(typeof(SaveManager), "CleanupStaleCurrentRunSaves").Invoke(downloadManager, null);
        Assert(!downloaded.FileExists(thirdPath), "game cleanup removes completed ordinary saves through the standard path");
    }

    private static async Task CheckDisk(SerializableRun snapshot, int schema)
    {
        const string directory = "user://checkpoint-disk";
        var store = new GodotFileIo(directory);
        const string path = "current_run.save";
        await CheckpointStore.Write(store, path, snapshot, false, null);
        var first = store.ReadFile(path);
        snapshot.NumReloads++;
        await CheckpointStore.Write(store, path, snapshot, true, null);
        Assert(store.ReadFile(path + ".backup") == first, "real atomic file writer backs up complete envelope");
        System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(directory + "/" + path), "{torn");
        var recovered = CheckpointStore.Read(store, path, schema).SaveData!;
        await CheckpointStore.Write(store, path, recovered, false, null);
        Assert(store.ReadFile(path + ".backup") == first && CheckpointStore.Read(store, path, schema).Success,
            "recovery rewrite retains valid backup across torn primary");
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition)
            throw new InvalidOperationException(label);
        passed.Add(label);
        GD.Print("PASS: " + label);
    }

    private static async Task ExpectFailure(Func<Task> action)
    {
        try { await action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected write failure.");
    }
}
