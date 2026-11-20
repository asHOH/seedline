using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckModifierCheckpoints()
    {
        static void Reject(Action action, string message)
        {
            try
            {
                action();
            }
            catch (InvalidDataException)
            {
                Assert(true, message);
                return;
            }
            throw new InvalidOperationException(message);
        }

        foreach (var (mode, custom, players, modifier) in new[]
        {
            (GameMode.Standard, true, 1, false), (GameMode.Standard, false, 2, false),
            (GameMode.Standard, false, 1, true)
        })
        {
            var invalid = RunState.CreateForNewRun(Enumerable.Range(1, players)
                .Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray(),
                ActModel.GetDefaultList().Select(act => act.ToMutable()).ToList(),
                modifier ? [ModelDb.Modifier<DeprecatedModifier>().ToMutable()] : [], mode, custom, 7, "MODIFIERSCOPE");
            Reject(() => RunCheckpoints.Start(invalid),
                $"startup rejects mode={mode}, custom={custom}, players={players}, live modifier={modifier}");
            Assert(RunCheckpoints.Find(invalid) == null, "rejected startup attaches no Seedline state");
        }

        var run = StartGoldRun();
        try
        {
            var snapshot = RunManager.Instance.ToSave(null);
            var payload = new SerializableModifier
            {
                Id = ModelDb.Modifier<DeprecatedModifier>().Id,
                Props = new SavedProperties { strings = [new("Snapshot", "save-only payload")] }
            };
            snapshot.Modifiers.Add(payload);
            var bytes = RunCheckpoints.Encode(snapshot);
            var store = new MockGodotFileIo("modifier-payload");
            var path = RunSaveManager.GetRunSavePath(1, RunSaveManager.runSaveFileName);
            await CheckpointStore.Write(store, path, snapshot, false, null);
            var loaded = CheckpointStore.Read(store, path, snapshot.SchemaVersion).SaveData!;
            Assert(loaded.Modifiers.Single().Props!.strings!.Single().value == "save-only payload" &&
                RunCheckpoints.Encode(loaded).SequenceEqual(bytes), "serialized modifier payload round-trips unchanged");

            loaded.NumReloads++;
            await CheckpointStore.Write(store, path, loaded, false, null);
            var rewritten = CheckpointStore.Read(store, path, snapshot.SchemaVersion).SaveData!;
            Assert(rewritten.NumReloads == loaded.NumReloads &&
                rewritten.Modifiers.Single().Props!.strings!.Single().value == "save-only payload",
                "reload-count rewrite retains save-only modifier data");

            RunManager.Instance.CleanUp();
            var restored = RunState.FromSerializable(rewritten);
            Reject(() => RunCheckpoints.Restore(restored, rewritten), "restoration rejects a modifier left in the live run");
            Assert(RunCheckpoints.Find(restored) == null, "rejected restoration leaves Seedline state unattached");

            // Simulate a mod consuming its save-only carrier after reconstructing its own state.
            AccessTools.Property(typeof(RunState), nameof(RunState.Modifiers)).SetValue(restored, Array.Empty<ModifierModel>());
            await RunManager.Instance.SetUpSavedSingleplayer(restored, rewritten);
            Assert(SameState(RunCheckpoints.Require(restored), RunCheckpoints.Require(rewritten)) &&
                rewritten.Modifiers.Single().Props!.strings!.Single().value == "save-only payload",
                "restoration accepts a consumed carrier without stripping it from the saved snapshot");

            var captured = RunManager.Instance.ToSave(null);
            var capturedBytes = RunCheckpoints.Encode(captured);
            restored.AddModifierDebug(ModelDb.Modifier<DeprecatedModifier>().ToMutable());
            Reject(() => RunManager.Instance.ToSave(null), "capture rejects modifiers added after startup");
            Assert(capturedBytes.SequenceEqual(RunCheckpoints.Encode(captured)), "failed capture preserves an earlier checkpoint");
        }
        finally
        {
            RunManager.Instance.CleanUp();
        }
    }
}
