using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SameSeed.Checks;

public static partial class Checks
{
    private static async Task CheckCombatStateFoundation()
    {
        var run = StartGoldRun();
        var player = run.Players[0];
        var state = RunCheckpoints.Require(run);
        var start = RunManager.Instance.ToSave(null);
        var startBytes = RunCheckpoints.Encode(start);
        var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
        var counter = RandomAddress.For("combat", RoomType.Monster.ToString()).At(0);
        Assert(state.Random.Position(counter) == 0, "combat construction preserves the next opportunity");
        var first = CombatStateScope.Begin(combat);
        Assert(ReferenceEquals(first, CombatStateScope.Begin(combat)) && state.Random.Position(counter) == 1,
            "combat entry commits one act-local category ordinal, once per combat");
        var roll = first.Random.NextUInt64(first.For("check"));
        var elite = CombatStateScope.Begin(new(ModelDb.Encounter<BygoneEffigyElite>().ToMutable(), run));
        Assert(elite.Address != first.Address && state.Random.Position(counter) == 1,
            "elite combat has an independent category counter");
        run.PushRoom(new EventRoom(ModelDb.Event<BattlewornDummy>()));
        var battle1 = CombatStateScope.Begin(new(ModelDb.Encounter<BattlewornDummyEventV1Encounter>().ToMutable(), run));
        var battle2 = CombatStateScope.Begin(new(ModelDb.Encounter<BattlewornDummyEventV2Encounter>().ToMutable(), run));
        Assert(battle1.Address != battle2.Address && state.Random.Position(counter) == 1 &&
            state.Random.Position(RandomAddress.For("event combat", "BATTLEWORN_DUMMY")) == 2,
            "event battles use event-local ordinals without consuming normal combats");
        run.PopCurrentRoom();
        run.CurrentActIndex = 1;
        var act2 = CombatStateScope.Begin(new(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run));
        Assert(act2.Address != first.Address && state.Random.Position(RandomAddress.For("combat", RoomType.Monster.ToString()).At(1)) == 1,
            "new acts have separate combat opportunities");
        RunManager.Instance.CleanUp();
        var restored = RunCheckpoints.Decode(Encoding.UTF8.GetString(startBytes), start.SchemaVersion);
        run = RunState.FromSerializable(restored);
        await RunManager.Instance.SetUpSavedSingleplayer(run, restored);
        player = run.Players[0];
        state = RunCheckpoints.Require(run);
        var replay = CombatStateScope.Begin(new(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run));
        Assert(replay.Address == first.Address && replay.Random.NextUInt64(replay.For("check")) == roll,
            "pre-combat reload reproduces combat address and local randomness");
        Assert(startBytes.SequenceEqual(RunCheckpoints.Encode(start)), "combat consumption leaves its checkpoint frozen");

        var opening = player.Deck.Cards.Select(CardOccurrences.Info).ToArray();
        Assert(opening.All(c => c.SourceKey && c.Acquisition != null) && opening.Select(c => c.Acquisition).Distinct().Count() == 1,
            "starting deck is one acquisition batch with source keys");
        var a = run.CreateCard<Anger>(player);
        var b = run.CreateCard<Bash>(player);
        await CardPileCmd.Add(new CardModel[] { b, a }, PileType.Deck);
        var aInfo = CardOccurrences.Info(a);
        Assert(aInfo.Acquisition == CardOccurrences.Info(b).Acquisition && aInfo.Acquisition > opening[0].Acquisition && !aInfo.SourceKey,
            "new permanent grants share one committed acquisition batch");
        var copy = run.CloneCard(a);
        await CardPileCmd.Add(copy, PileType.Deck);
        Assert(CardOccurrences.Info(copy).Address != aInfo.Address && CardOccurrences.Info(copy).Acquisition > aInfo.Acquisition,
            "permanent copies start fresh occurrences and acquisition batches");
        CardCmd.Enchant<Clone>(a, 1);
        CardCmd.Enchant<Clone>(b, 1);
        var beforeClones = player.Deck.Cards.ToArray();
        await new CloneRestSiteOption(player).OnSelect();
        var clones = player.Deck.Cards.Except(beforeClones).ToArray();
        Assert(clones.Length == 2 && clones.Select(c => CardOccurrences.Info(c).Acquisition).Distinct().Count() == 1 &&
            clones.All(c => !CardOccurrences.Info(c).SourceKey),
            "source-level copy batches retain one acquisition ordinal across sequential additions");
        var beforeBundle = player.Deck.Cards.ToArray();
        var scrolls = (ScrollBoxes)ModelDb.Relic<ScrollBoxes>().ToMutable();
        player.AddRelicInternal(scrolls);
        await scrolls.AfterObtained();
        var bundle = player.Deck.Cards.Except(beforeBundle).ToArray();
        Assert(bundle.Length == 3 && bundle.Select(c => CardOccurrences.Info(c).Acquisition).Distinct().Count() == 1,
            "Scroll Boxes acquires the chosen bundle in one batch across sequential additions");
        CardCmd.Upgrade(a);
        await CardPileCmd.RemoveFromDeck(a, false);
        var frozen = state.Cards.Removed[aInfo.Address];
        a.DowngradeInternal();
        var frozenCard = JsonSerializer.Deserialize(frozen, JsonSerializationUtility.GetTypeInfo<SerializableCard>())!;
        Assert(frozenCard.CurrentUpgradeLevel == 1 &&
            CardOccurrences.Read(frozenCard.Props!.strings!
                .Single(p => p.name == CardOccurrences.SavedName).value).Acquisition == aInfo.Acquisition,
            "removal freezes card properties and retains original acquisition metadata");
        var removedSave = RunManager.Instance.ToSave(null);
        var removedBytes = RunCheckpoints.Encode(removedSave);
        run.AddCard(a, player);
        await CardPileCmd.Add(a, PileType.Deck);
        Assert(!state.Cards.Removed.ContainsKey(aInfo.Address) && CardOccurrences.Info(a).Acquisition == aInfo.Acquisition &&
            removedBytes.SequenceEqual(RunCheckpoints.Encode(removedSave)),
            "returned cards retain their lifetime and clear the live retained entry without mutating checkpoints");
        var tooth = (PaelsTooth)ModelDb.Relic<PaelsTooth>().ToMutable();
        player.AddRelicInternal(tooth);
        tooth.DebugAddCard(b.ToSerializable());
        await CardPileCmd.RemoveFromDeck(b, false);
        Assert(!state.Cards.Removed.ContainsKey(CardOccurrences.Info(b).Address),
            "Tooth storage preserves the occurrence without a permanent-removal entry");
        var transformed = player.Deck.Cards.OfType<StrikeIronclad>().First();
        var transformedInfo = CardOccurrences.Info(transformed);
        var replacement = (await CardCmd.TransformToRandom(transformed, player.PlayerRng.Transformations)).cardAdded;
        Assert(state.Cards.Removed.ContainsKey(transformedInfo.Address) && CardOccurrences.Info(replacement).SourceKey &&
            CardOccurrences.Info(replacement).Acquisition > transformedInfo.Acquisition,
            "permanent transformations retain the retired input and acquire a fresh source-addressed result");
        RunManager.Instance.CleanUp();
        restored = RunCheckpoints.Decode(Encoding.UTF8.GetString(removedBytes), removedSave.SchemaVersion);
        run = RunState.FromSerializable(restored);
        await RunManager.Instance.SetUpSavedSingleplayer(run, restored);
        Assert(RunCheckpoints.Require(run).Cards.Removed[aInfo.Address] == frozen,
            "removed-card history survives full checkpoint restoration");
        RunManager.Instance.CleanUp();
    }
}
