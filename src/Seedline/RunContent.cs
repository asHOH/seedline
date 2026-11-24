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
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.Dread;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Timeline.Epochs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.addons.mega_text;

namespace Seedline;

internal static class RunContent
{
    internal static bool Covers(StartRunLobby lobby) => lobby.NetService.Type == NetGameType.Singleplayer &&
        lobby.GameMode is GameMode.Standard or GameMode.Dread && lobby.Modifiers.All(m => RunScope.Supports(m.Id));

    internal static void Grant(RunState run)
    {
        foreach (var player in run.Players)
            AccessTools.Field(typeof(Player), "<UnlockState>k__BackingField").SetValue(player, UnlockState.all);
        AccessTools.Property(typeof(RunState), nameof(RunState.UnlockState)).SetValue(run, UnlockState.all);
    }

    internal static string Missing(UnlockState unlocks)
    {
        var groups = new List<string>();
        void Add(string name, IEnumerable<string> items, int limit = int.MaxValue)
        {
            var names = items.Distinct().ToArray();
            if (names.Length > 0)
                groups.Add(NoticeText.Get("Group", name, string.Join(NoticeText.Get("ListSeparator"), names.Take(limit)),
                    names.Length > limit ? NoticeText.Get("More", names.Length - limit) : ""));
        }
        IEnumerable<CardModel> Cards(UnlockState state) => state.CardPools.SelectMany(p =>
            p.GetUnlockedCards(state, CardMultiplayerConstraint.SingleplayerOnly));
        IEnumerable<AncientEventModel> Ancients(UnlockState state) => state.SharedAncients.Concat(
            ModelDb.ActsByIndex.SelectMany(a => a).SelectMany(a => a.GetUnlockedAncients(state)));
        var characters = ModelDb.AllCharacters.ToArray();
        var cards = Cards(UnlockState.all).Except(Cards(unlocks)).ToArray();
        var relics = UnlockState.all.Relics.Except(unlocks.Relics).ToArray();
        var potions = UnlockState.all.Potions.Except(unlocks.Potions).ToArray();
        foreach (var character in characters)
            Add(character.Title.GetFormattedText(),
                cards.Where(c => c.Pool == character.CardPool).Select(c => c.Title)
                    .Concat(relics.Where(r => r.Pool == character.RelicPool).Select(r => r.Title.GetFormattedText()))
                    .Concat(potions.Where(p => p.Pool == character.PotionPool).Select(p => p.Title.GetFormattedText())), 5);
        Add(NoticeText.Get("ColorlessCards"), cards.Where(c => characters.All(character => c.Pool != character.CardPool)).Select(c => c.Title), 5);
        Add(NoticeText.Get("SharedRelics"), relics.Where(r => characters.All(character => r.Pool != character.RelicPool)).Select(r => r.Title.GetFormattedText()), 5);
        Add(NoticeText.Get("SharedPotions"), potions.Where(p => characters.All(character => p.Pool != character.PotionPool)).Select(p => p.Title.GetFormattedText()), 5);
        Add(NoticeText.Get("Acts"), ModelDb.ActsByIndex.SelectMany(a => a).Where(a => !a.IsUnlocked(unlocks)).Select(a => a.Title.GetFormattedText()));
        Add(NoticeText.Get("Ancients"), Ancients(UnlockState.all).Except(Ancients(unlocks)).Select(a => a.Title.GetFormattedText()));
        Add(NoticeText.Get("Events"), (unlocks.IsEpochRevealed<Event1Epoch>() ? [] : Event1Epoch.Events)
            .Concat(unlocks.IsEpochRevealed<Event2Epoch>() ? [] : Event2Epoch.Events)
            .Concat(unlocks.IsEpochRevealed<Event3Epoch>() ? [] : Event3Epoch.Events).Select(e => e.Title.GetFormattedText()));
        return string.Join("\n\n", groups);
    }

    internal static NGenericPopup Notice(string missing)
    {
        var popup = NGenericPopup.Create() ?? throw new InvalidOperationException("Seedline notice could not be created.");
        NModalContainer.Instance!.Add(popup);
        var panel = popup.GetNode<NVerticalPopup>("VerticalPopup");
        panel.SetText(NoticeText.Get("Title"), NoticeText.Get("Body", missing));
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
        AccessTools.Method(typeof(NDreadScreen), "OnEmbarkPressed"),
        AccessTools.Method(typeof(NCustomRunScreen), "OnEmbarkPressed")
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
