using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Connection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private sealed class HandshakeProbe(NetGameType type) : IHandshakeHandler
    {
        public NetGameType Type => type;
        internal bool Accepted;
        internal NetErrorInfo? Error;
        internal byte[]? Reply;

        public void SendHandshakeMessage(ulong peerId, PacketWriter writer) => Reply = writer.Buffer[..writer.BytePosition];
        public void HandshakeFailed(ulong peerId, NetErrorInfo info) => Error = info;
        public void HandshakeSucceeded(ulong peerId, PeerVersionInfo versionInfo) => Accepted = true;
    }

    private static void WithVanillaPropertyCache(Action check)
    {
        var harmony = new Harmony(ModEntry.Id);
        var init = AccessTools.Method(typeof(ModelIdSerializationCache), nameof(ModelIdSerializationCache.Init));
        harmony.Unpatch(init, HarmonyPatchType.Transpiler, ModEntry.Id);
        try
        {
            ModelIdSerializationCache.ResetForTest();
            ModelIdSerializationCache.Init();
            check();
        }
        finally
        {
            harmony.CreateClassProcessor(typeof(CardOccurrences.RegisterSavedName)).Patch();
            ModelIdSerializationCache.ResetForTest();
            ModelIdSerializationCache.Init();
        }
    }

    private static byte[] PropertyPacket(IEnumerable<string> names)
    {
        var writer = new PacketWriter();
        foreach (var model in ModelDb.All)
            writer.WriteFullModelId(model.Id);
        writer.Write(new SavedProperties
        {
            ints = names.Select((name, index) => new SavedProperties.SavedProperty<int>(name, index)).ToList()
        });
        return writer.Buffer[..writer.BytePosition];
    }

    private static void CheckMultiplayerCompatibility()
    {
        var local = PeerVersionInfo.LocalDefault();
        Assert(local.gameplayAffectingMods?.Any(m => m.StartsWith(ModEntry.Id + "-", StringComparison.Ordinal)) != true &&
            local.otherMods?.Any(m => m.StartsWith(ModEntry.Id + "-", StringComparison.Ordinal)) == true,
            "Seedline is advertised as optional for multiplayer, not hidden from peers");

        var widths = (ModelIdSerializationCache.CategoryIdBitSize, ModelIdSerializationCache.EntryIdBitSize,
            ModelIdSerializationCache.PropertyIdBitSize, ModelIdSerializationCache.EpochIdBitSize);
        string[] properties = [];
        byte[] ordinaryPacket = [];
        uint vanillaHash = 0;
        WithVanillaPropertyCache(() =>
        {
            vanillaHash = ModelIdSerializationCache.Hash;
            properties = Enumerable.Range(0, ModelIdSerializationCache.MaxPropertyId + 1)
                .Select(ModelIdSerializationCache.GetPropertyNameForNetId).ToArray();
            ordinaryPacket = PropertyPacket(properties);
            Assert(widths == (ModelIdSerializationCache.CategoryIdBitSize, ModelIdSerializationCache.EntryIdBitSize,
                ModelIdSerializationCache.PropertyIdBitSize, ModelIdSerializationCache.EpochIdBitSize),
                "Seedline preserves vanilla network bit widths");
        });
        Assert(local.idDatabaseHash == vanillaHash && ModelIdSerializationCache.Hash == vanillaHash,
            "Seedline preserves the vanilla model/property compatibility hash");
        Assert(properties.Select((name, index) => ModelIdSerializationCache.GetNetIdForPropertyName(name) == index).All(equal => equal) &&
            ModelIdSerializationCache.GetNetIdForPropertyName(CardOccurrences.SavedName) == properties.Length,
            "Seedline appends local metadata without shifting any ordinary property ID");
        Assert(ordinaryPacket.SequenceEqual(PropertyPacket(properties)),
            "every ordinary model and property serializes to identical bytes with and without Seedline metadata registration");

        var remote = local;
        remote.idDatabaseHash = vanillaHash;
        remote.otherMods = local.otherMods?.Where(m => !m.StartsWith(ModEntry.Id + "-", StringComparison.Ordinal)).ToList();
        foreach (var type in new[] { NetGameType.Host, NetGameType.Client })
        {
            CheckHandshake(type, local, remote, true);
            CheckHandshake(type, remote, local, true);
        }
        var incompatible = remote;
        incompatible.idDatabaseHash ^= 1;
        CheckHandshake(NetGameType.Host, local, incompatible, false);
        incompatible = remote;
        incompatible.gameplayAffectingMods = [.. remote.gameplayAffectingMods ?? [], "AnotherGameplayMod-1.0"];
        CheckHandshake(NetGameType.Host, local, incompatible, false);
    }

    private static void CheckHandshake(NetGameType type, PeerVersionInfo local, PeerVersionInfo remote, bool accepted)
    {
        var handler = new HandshakeProbe(type);
        var manager = new HandshakeManager(handler, local, new PacketWriter());
        const ulong peer = 42;
        manager.BeginHandshakeFor(peer);
        try
        {
            var writer = new PacketWriter();
            HandshakeManager.WriteHandshakeMessage(peer, remote, writer);
            var reader = new PacketReader();
            reader.Reset(writer.Buffer[..writer.BytePosition]);
            manager.HandshakeMessageReceived(peer, reader);
            Assert(handler.Accepted == accepted && handler.Error.HasValue != accepted && !manager.IsHandshaking(peer),
                $"{type} handshake {(accepted ? "accepts a mixed Seedline installation" : "still rejects incompatible gameplay or model IDs")}");
            if (accepted)
                Assert(handler.Reply != null, $"{type} writes its ordinary handshake reply");
        }
        finally
        {
            manager.AbortHandshake(peer);
        }
    }

    private static void CheckMultiplayerSavePacket(SerializableRun snapshot)
    {
        var writer = new PacketWriter();
        writer.Write(snapshot);
        var packet = writer.Buffer[..writer.BytePosition];
        WithVanillaPropertyCache(() =>
        {
            var reader = new PacketReader();
            reader.Reset(packet);
            var decoded = reader.Read<SerializableRun>();
            var vanillaWriter = new PacketWriter();
            vanillaWriter.Write(decoded);
            Assert(packet.SequenceEqual(vanillaWriter.Buffer[..vanillaWriter.BytePosition]) &&
                decoded.Players.Count == snapshot.Players.Count,
                "a peer without Seedline decodes and re-encodes the multiplayer save with identical bytes");
        });
    }
}
