using Godot;
using HarmonyLib;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Ftue;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Dread;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Timeline;
using MegaCrit.Sts2.Core.Timeline.Epochs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.addons.mega_text;

namespace Seedline.Checks;

public static partial class Checks
{
    private static int contentProceedCount;
    private static void ContentProceedProbe(object? _) => contentProceedCount++;

    private static async Task CheckRunContent()
    {
        var partial = new UnlockState(EpochModel.AllEpochIds.Except([EpochModel.GetId<Fool6Epoch>()]), [], 0);
        // Vanilla records starting relic discovery when constructing a player.
        _ = Player.CreateForNewRun<Fool>(partial, 1);
        var progress = JsonSerializationUtility.ToJson(SaveManager.Instance.Progress.ToSerializable());
        Assert(RunContent.Missing(partial).Contains(ModelDb.Relic<WhittlingKnife>().Title.GetFormattedText()) &&
            RunContent.Missing(UnlockState.all) == "", "notice names missing content and skips fully unlocked pools");
        var previous = TestMode.IsOn;
        TestMode.IsOn = true;
        try
        {
            foreach (var mode in new[] { GameMode.Standard, GameMode.Dread })
            {
                var player = Player.CreateForNewRun<Fool>(partial, 1);
                var run = RunState.CreateForNewRun([player], ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(),
                    [], mode, false, 7, "CONTENTCHECK");
                Assert(!player.UnlockState.IsEpochRevealed<Fool6Epoch>() && !run.UnlockState.IsEpochRevealed<Fool6Epoch>(),
                    $"{mode}: unattached players and runs keep their original unlocks");
                RunManager.Instance.SetUpNewSingleplayer(run, true);
                Assert(player.UnlockState == UnlockState.all && run.UnlockState == UnlockState.all && run.AscensionLevel == 7,
                    $"{mode}: new Seedline run grants full content before initialization and preserves selected Ascension");
                if (mode == GameMode.Dread)
                {
                    RelicRewards.Draw(player, "generic", RelicRarity.Common);
                    RunCheckpoints.Require(run).Random.NextUInt64(RandomAddress.For("content preservation"));
                }
                var snapshot = RunManager.Instance.ToSave(null);
                var bytes = RunCheckpoints.Encode(snapshot);
                var savedState = RunCheckpoints.Require(snapshot);
                RelicRewards.Initialize(player);
                Assert(RunCheckpoints.Require(run).Rewards.RelicsInitialized,
                    $"{mode}: previously locked Fool relics satisfy allocation sizes");
                RunManager.Instance.CleanUp();

                var loaded = RunCheckpoints.Decode(System.Text.Encoding.UTF8.GetString(bytes), snapshot.SchemaVersion);
                var restored = RunState.FromSerializable(loaded);
                var restoredUnlocks = restored.UnlockState;
                var playerUnlocks = restored.Players[0].UnlockState;
                await RunManager.Instance.SetUpSavedSingleplayer(restored, loaded);
                Assert(restored.UnlockState == restoredUnlocks && restored.Players[0].UnlockState == playerUnlocks &&
                    RunContent.Missing(restored.UnlockState) == "" &&
                    restored.AscensionLevel == 7 && SameState(RunCheckpoints.Require(restored), savedState) &&
                    JsonNode.DeepEquals(JsonNode.Parse(bytes)!["seedline"]!["rewards"],
                        JsonNode.Parse(RunCheckpoints.Encode(RunManager.Instance.ToSave(null)))!["seedline"]!["rewards"]),
                    $"{mode}: Continue preserves full unlocks, saved reward and random state");
                RelicRewards.Initialize(restored.Players[0]);
                Assert(RunCheckpoints.Require(restored).Rewards.RelicsInitialized && RunCheckpoints.Encode(snapshot).SequenceEqual(bytes),
                    $"{mode}: resumed full-content save preserves relic supplies and the frozen source snapshot");
                await SaveManager.Instance.SaveRun(null, saveProgress: false);
                var saved = SaveManager.Instance.LoadRunSave().SaveData!;
                Assert(UnlockState.FromSerializable(saved.Players[0].UnlockState).IsEpochRevealed<Fool6Epoch>(),
                    $"{mode}: the next normal checkpoint retains full run content");
                RunManager.Instance.CleanUp();
            }
        }
        finally
        {
            RunManager.Instance.CleanUp();
            TestMode.IsOn = previous;
        }
        Assert(JsonSerializationUtility.ToJson(SaveManager.Instance.Progress.ToSerializable()) == progress &&
            !partial.IsEpochRevealed<Fool6Epoch>() && partial.NumberOfRuns == 0,
            "run grants preserve profile progress, Ascension limits and the source unlock snapshot");

        var screen = new Control();
        NGame.Instance!.AddChild(screen);
        var lobby = new StartRunLobby(GameMode.Standard, false, new NetSingleplayerGameService(), null!, 1);
        lobby.Players.Add(new StartRunLobbyPlayer { id = lobby.NetService.NetId, character = ModelDb.Character<Fool>(), unlockState = partial.ToSerializable() });
        var method = AccessTools.Method(typeof(Checks), nameof(ContentProceedProbe));
        var getUnlocks = AccessTools.Method(typeof(StartRunLobby), "GetUnlockState");
        Assert((UnlockState)getUnlocks.Invoke(lobby, null)! == UnlockState.all, "single-player act selection uses full content before run creation");
        foreach (var mode in new[] { GameMode.Daily, GameMode.Blitz })
        {
            lobby.GameMode = mode;
            Assert(!RunContent.Covers(lobby), $"{mode}: content grant excludes unsupported lobbies");
        }
        lobby.GameMode = GameMode.Standard;
        var custom = new StartRunLobby(GameMode.Standard, true, new NetSingleplayerGameService(), null!, 1);
        Assert(!RunContent.Covers(custom), "custom lobby stays outside the content grant");
        foreach (var type in new[] { typeof(NCharacterSelectScreen), typeof(NDreadScreen) })
            Assert(Harmony.GetPatchInfo(AccessTools.Method(type, "OnEmbarkPressed"))?.Owners.Contains(ModEntry.Id) == true,
                $"{type.Name}: notice intercepts Start before the original handler");
        Assert(!ContentNoticePatch.Prefix(screen, lobby, method), "partial unlocks pause Start for the content notice");
        var panel = ((Node)NModalContainer.Instance!.OpenModal!).GetNode<NVerticalPopup>("VerticalPopup");
        Assert(panel.YesButton.HasFocus() && panel.YesButton.IsYes && panel.NoButton.Visible &&
            panel.GetNode<MegaRichTextLabel>("Description").ScrollActive, "notice defaults to Proceed and exposes Cancel with a scrollable content list");
        Assert(!ContentNoticePatch.Prefix(screen, lobby, method) &&
            ((Node)NModalContainer.Instance.OpenModal!).GetNode<NVerticalPopup>("VerticalPopup") == panel,
            "repeated Start clicks keep one pending notice");
        panel.NoButton.EmitSignal(NClickableControl.SignalName.Released, panel.NoButton);
        Assert(NModalContainer.Instance.OpenModal == null && contentProceedCount == 0 && !lobby.LocalPlayer.isReady,
            "Cancel closes the notice without starting or changing readiness");
        var local = lobby.Players[0];
        local.unlockState = UnlockState.none.ToSerializable();
        lobby.Players[0] = local;
        Assert(!ContentNoticePatch.Prefix(screen, lobby, method), "Start after Cancel asks again");
        panel = ((Node)NModalContainer.Instance.OpenModal!).GetNode<NVerticalPopup>("VerticalPopup");
        await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
        var description = panel.GetNode<MegaRichTextLabel>("Description");
        Assert(ModelDb.AllCharacters.All(character => description.Text.Contains(character.Title.GetFormattedText() + ":")) &&
            description.Text.Contains("Ancients:") &&
            description.GetContentHeight() > description.Size.Y && description.GetVScrollBar().MaxValue > description.GetVScrollBar().Page,
            "a fresh profile's character-grouped content summary retains working scrolling");
        panel.YesButton.EmitSignal(NClickableControl.SignalName.Released, panel.YesButton);
        await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
        Assert(contentProceedCount == 1 && NModalContainer.Instance.OpenModal == null &&
            ContentNoticePatch.Prefix(screen, lobby, method), "Proceed resumes once after closing the notice and allows tutorial reentry");
        Assert(JsonSerializationUtility.ToJson(SaveManager.Instance.Progress.ToSerializable()) == progress,
            "Cancel and Proceed leave permanent progress unchanged");
        var tutorialResumed = false;
        var tutorial = NAcceptTutorialsFtue.Create(null!, () => tutorialResumed = ContentNoticePatch.Prefix(screen, lobby, method))!;
        NModalContainer.Instance.Add(tutorial);
        var tutorialButtons = tutorial.GetNode<NVerticalPopup>("VerticalPopup");
        tutorialButtons.YesButton.EmitSignal(NClickableControl.SignalName.Released, tutorialButtons.YesButton);
        Assert(tutorialResumed && NModalContainer.Instance.OpenModal == null,
            "first-run tutorial callback can resume Start before its own modal closes");
        screen.QueueFree();
        await CheckNoticeLocalization();
    }

    private static async Task CheckNoticeLocalization()
    {
        var locale = LocManager.Instance;
        var originalLanguage = locale.Language;
        var cards = ModelDb.AllCards.OrderBy(c => c, CardOrder.Identity).Select(c => c.Id).ToArray();
        var partial = new UnlockState(EpochModel.AllEpochIds.Except([EpochModel.GetId<Fool6Epoch>()]), [], 0);
        using var englishStream = typeof(NoticeText).Assembly.GetManifestResourceStream("Seedline.Localization.eng.json")!;
        var english = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(englishStream)!;
        Assert(NoticeText.GetTemplate("untranslated", "Title") == english["Title"], "an untranslated locale falls back to English");
        try
        {
            foreach (var language in LocManager.Languages.Append("eng"))
            {
                using var stream = typeof(NoticeText).Assembly.GetManifestResourceStream($"Seedline.Localization.{language}.json")!;
                var translation = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
                Assert(english.Keys.ToHashSet().SetEquals(translation.Keys) && translation.Values.All(value => !string.IsNullOrEmpty(value)),
                    $"{language}: packaged translations cover every notice key");
                locale.SetLanguage(language);
                var missing = RunContent.Missing(UnlockState.none);
                var popup = RunContent.Notice(missing);
                var panel = popup.GetNode<NVerticalPopup>("VerticalPopup");
                await NGame.Instance!.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
                Assert(panel.GetNode<MegaLabel>("Header").Text == translation["Title"] &&
                    panel.GetNode<MegaRichTextLabel>("Description").Text == string.Format(locale.CultureInfo, translation["Body"], missing) &&
                    missing.Contains(translation["Ancients"]) &&
                    new[] { 1, 2, 5, 12 }.All(count => NoticeText.Get("More", count) == string.Format(locale.CultureInfo, translation["More"], count)),
                    $"{language}: notice title, body, headings and counts follow the selected locale");
                Assert(RunContent.Missing(partial).Contains(ModelDb.Relic<WhittlingKnife>().Title.GetFormattedText()) &&
                    RunContent.Missing(UnlockState.all) == "",
                    $"{language}: content names use game translations and full unlocks still skip the notice");
                Assert(cards.SequenceEqual(ModelDb.AllCards.OrderBy(c => c, CardOrder.Identity).Select(c => c.Id)),
                    $"{language}: notice localization preserves gameplay card ordering");
                NModalContainer.Instance!.Clear();
            }
        }
        finally
        {
            NModalContainer.Instance!.Clear();
            locale.SetLanguage(originalLanguage);
        }
    }
}
