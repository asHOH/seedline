using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Replay;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCombatSerialization()
    {
        var run = StartGoldRun();
        var manager = RunManager.Instance;
        var player = run.Players[0];
        var storedCard = player.Deck.Cards[0];
        var tooth = (PaelsTooth)ModelDb.Relic<PaelsTooth>().ToMutable();
        player.AddRelicInternal(tooth);
        tooth.DebugAddCard(storedCard.ToSerializable());
        await CardPileCmd.RemoveFromDeck(storedCard);
        var snapshot = manager.ToSave(null);
        var checkpoint = RunCheckpoints.Encode(snapshot);

        // Test mode disables these by default, hiding failures in the binary serialization path.
        manager.ChecksumTracker.IsEnabled = true;
        manager.CombatReplayWriter.IsEnabled = true;
        manager.CombatReplayWriter.RecordInitialState(snapshot);
        var encounter = ModelDb.Encounter<AxebotsNormal>().ToMutable();
        var combat = new CombatState(encounter, run);
        combat.AddPlayer(player);
        encounter.GenerateMonstersWithSlots(run);
        foreach (var (monster, slot) in encounter.MonstersWithSlots)
            combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
        CombatManager.Instance.SetUpCombat(combat);
        CombatFixture.SetInProgress(true);
        manager.ActionExecutor.Pause();
        await CombatFixture.StartTurn();
        Assert(manager.ChecksumTracker.NextId > 0 && player.PlayerCombatState!.Hand.Cards.Count == 5 &&
            player.PlayerCombatState.Phase == PlayerTurnPhase.Play,
            "opening turn completes with combat checksums enabled and occurrence-bearing cards in hand");

        var fullState = NetFullCombatState.FromRun(run, null);
        var writer = new PacketWriter();
        writer.Write(fullState);
        var reader = new PacketReader();
        reader.Reset(writer.Buffer);
        var decoded = reader.Read<NetFullCombatState>();
        var savedCards = decoded.Players.Single().piles.SelectMany(p => p.cards).Select(c => c.card).ToArray();
        Assert(savedCards.Length == player.Deck.Cards.Count && savedCards.All(c =>
            CardOccurrences.Find(CardModel.FromSerializable(c)) is { } info &&
            player.Deck.Cards.Any(original => CardOccurrences.Info(original).Address == info.Address &&
                CardOccurrences.Info(original).Key == info.Key)),
            "binary combat-state round-trip preserves every card occurrence and key");
        var first = manager.ChecksumTracker.GenerateChecksum("serialization regression", null);
        var second = manager.ChecksumTracker.GenerateChecksum("serialization regression", null);
        Assert(first.checksum == second.checksum, "unchanged combat state produces the same checksum");

        var replayWriter = manager.CombatReplayWriter;
        var replayPath = ProjectSettings.GlobalizePath(SaveManager.Instance.GetProfileScopedPath("replays/latest.mcr"));
        if (System.IO.File.Exists(replayPath))
            System.IO.File.Delete(replayPath);
        manager.CleanUp();
        Assert(!manager.IsInProgress && !replayWriter.IsRecordingReplay && System.IO.File.Exists(replayPath),
            "run cleanup saves the active replay and completes after combat");
        reader.Reset(System.IO.File.ReadAllBytes(replayPath));
        var replay = reader.Read<CombatReplay>();
        CardOccurrences.Validate(replay.serializableRun);
        Assert(replay.modelIdHash == ModelIdSerializationCache.Hash && replay.checksumData.Count >= 3 &&
            replay.serializableRun.Players[0].Deck.Count == snapshot.Players[0].Deck.Count &&
            replay.serializableRun.Players[0].Relics.Single(r => r.Id?.Entry == "PAELS_TOOTH").Props!.cardArrays!
                .SelectMany(p => p.value).Single().Props!.strings!.Single(p => p.name == CardOccurrences.SavedName).value ==
            tooth.SerializableCards.Single().Props!.strings!.Single(p => p.name == CardOccurrences.SavedName).value,
            "saved replay decodes with combat checksums, deck occurrences and Pael's Tooth stored metadata");
        Assert(checkpoint.SequenceEqual(RunCheckpoints.Encode(snapshot)),
            "combat checksum and replay serialization leave the captured checkpoint unchanged");
    }
}
