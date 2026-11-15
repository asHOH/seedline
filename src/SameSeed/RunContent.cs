using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Ftue;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Dread;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Timeline.Epochs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.addons.mega_text;

namespace SameSeed;

internal static class RunContent
{
    internal static bool Covers(StartRunLobby lobby) => lobby.NetService.Type == NetGameType.Singleplayer &&
        !lobby.IsCustom && lobby.GameMode is GameMode.Standard or GameMode.Dread;

    internal static void Grant(RunState run)
    {
        foreach (var player in run.Players)
        {
            RelicRewards.ValidateFullPool(player.Character);
            AccessTools.Field(typeof(Player), "<UnlockState>k__BackingField").SetValue(player, UnlockState.all);
        }
        AccessTools.Property(typeof(RunState), nameof(RunState.UnlockState)).SetValue(run, UnlockState.all);
    }

    internal static string Missing(UnlockState unlocks)
    {
        var groups = new List<string>();
        void Add(string name, IEnumerable<string> items)
        {
            var names = items.Distinct().ToArray();
            if (names.Length > 0)
                groups.Add($"{name}: {string.Join(", ", names)}");
        }
        IEnumerable<CardModel> Cards(UnlockState state) => state.CardPools.SelectMany(p =>
            p.GetUnlockedCards(state, CardMultiplayerConstraint.SingleplayerOnly));
        IEnumerable<AncientEventModel> Ancients(UnlockState state) => state.SharedAncients.Concat(
            ModelDb.ActsByIndex.SelectMany(a => a).SelectMany(a => a.GetUnlockedAncients(state)));
        Add("Cards", Cards(UnlockState.all).Except(Cards(unlocks)).Select(c => c.Title));
        Add("Relics", UnlockState.all.Relics.Except(unlocks.Relics).Select(r => r.Title.GetFormattedText()));
        Add("Potions", UnlockState.all.Potions.Except(unlocks.Potions).Select(p => p.Title.GetFormattedText()));
        Add("Acts", ModelDb.ActsByIndex.SelectMany(a => a).Where(a => !a.IsUnlocked(unlocks)).Select(a => a.Title.GetFormattedText()));
        Add("Ancients", Ancients(UnlockState.all).Except(Ancients(unlocks)).Select(a => a.Title.GetFormattedText()));
        Add("Events", (unlocks.IsEpochRevealed<Event1Epoch>() ? [] : Event1Epoch.Events)
            .Concat(unlocks.IsEpochRevealed<Event2Epoch>() ? [] : Event2Epoch.Events)
            .Concat(unlocks.IsEpochRevealed<Event3Epoch>() ? [] : Event3Epoch.Events).Select(e => e.Title.GetFormattedText()));
        return string.Join("\n\n", groups);
    }

    internal static NGenericPopup Notice(string missing)
    {
        var popup = NGenericPopup.Create() ?? throw new InvalidOperationException("SameSeed notice could not be created.");
        NModalContainer.Instance!.Add(popup);
        var panel = popup.GetNode<NVerticalPopup>("VerticalPopup");
        panel.SetText("SameSeed",
            "Full content is available for this SameSeed run. Your profile unlocks and Ascension limits stay as they are.\n\n" + missing);
        // Keep long missing-content lists readable in the game's existing popup.
        var body = panel.GetNode<MegaRichTextLabel>("Description");
        body.AutoSizeEnabled = false;
        body.ScrollActive = true;
        body.Call("SetFontSize", 24);
        body.FocusMode = Control.FocusModeEnum.All;
        panel.YesButton.FocusMode = Control.FocusModeEnum.All;
        panel.NoButton.FocusMode = Control.FocusModeEnum.All;
        panel.YesButton.FocusNeighborTop = body.GetPath();
        panel.NoButton.FocusNeighborTop = body.GetPath();
        body.FocusNeighborBottom = panel.YesButton.GetPath();
        return popup;
    }
}

[HarmonyPatch(typeof(StartRunLobby), "GetUnlockState")]
internal static class LobbyContentPatch
{
    private static void Postfix(StartRunLobby __instance, ref UnlockState __result)
    {
        if (RunContent.Covers(__instance))
            __result = UnlockState.all;
    }
}

[HarmonyPatch]
internal static class ContentNoticePatch
{
    private static readonly ConditionalWeakTable<StartRunLobby, object> accepted = new();
    private static IEnumerable<MethodBase> TargetMethods() =>
    [
        AccessTools.Method(typeof(NCharacterSelectScreen), "OnEmbarkPressed"),
        AccessTools.Method(typeof(NDreadScreen), "OnEmbarkPressed")
    ];

    internal static bool Prefix(Control __instance, StartRunLobby ____lobby, MethodBase __originalMethod)
    {
        if (!RunContent.Covers(____lobby))
            return true;
        if (NModalContainer.Instance!.OpenModal is { } modal && modal is not NAcceptTutorialsFtue)
            return false;
        if (accepted.TryGetValue(____lobby, out _))
            return true;
        var missing = RunContent.Missing(UnlockState.FromSerializable(____lobby.LocalPlayer.unlockState));
        if (missing.Length == 0)
            return true;
        var notice = RunContent.Notice(missing);
        var buttons = notice.GetNode<NVerticalPopup>("VerticalPopup");
        buttons.InitYesButton(NProceedButton.ProceedLoc, _ =>
        {
            accepted.AddOrUpdate(____lobby, new object());
            // Let the popup close before the game's tutorial prompt or transition opens.
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(__instance) && __instance.IsInsideTree())
                    __originalMethod.Invoke(__instance, [null]);
            }).CallDeferred();
        });
        buttons.InitNoButton(new LocString("main_menu_ui", "GENERIC_POPUP.cancel"), _ => { });
        buttons.YesButton.GrabFocus();
        return false;
    }
}
