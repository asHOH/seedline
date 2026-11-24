using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.addons.mega_text;

namespace Seedline;

[HarmonyPatch(typeof(NSettingsScreen), "LocalizeLabels")]
internal static class DiagnosticExport
{
    private static bool exporting;

    private static void Postfix(NSettingsScreen __instance)
    {
        var panel = __instance.GetNode<NSettingsPanel>("%GeneralSettings");
        var row = panel.Content.GetNodeOrNull<Control>("SeedlineDiagnostics");
        if (row == null)
        {
            var source = panel.Content.GetNode<Control>("SendFeedback");
            row = (Control)source.Duplicate((int)(Node.DuplicateFlags.Scripts | Node.DuplicateFlags.Groups));
            row.Name = "SeedlineDiagnostics";
            row.UniqueNameInOwner = false;
            foreach (var child in row.FindChildren("*", owned: false))
                child.UniqueNameInOwner = false;
            var button = row.FindChildren("*", owned: false).OfType<NOpenFeedbackScreenButton>().Single();
            button.Name = "ExportButton";
            panel.Content.AddChild(row);
            panel.Content.MoveChild(row, source.GetIndex() + 1);
            button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(pressed => TaskHelper.RunSafely(Export(__instance, pressed))));
            panel.CallDeferred(NSettingsPanel.MethodName.RefreshSize);
            panel.CallDeferred(NSettingsPanel.MethodName.UpdateNavigation);
        }
        row.GetNode<MegaRichTextLabel>("Label").Text = NoticeText.Get("DiagnosticsTitle");
        row.GetNode<NButton>("ExportButton").GetNode<MegaLabel>("Label").SetTextAutoSize(NoticeText.Get("ExportDiagnostics"));
    }

    internal static async Task Export(NSettingsScreen screen, NButton button)
    {
        if (exporting)
            return;
        exporting = true;
        button.Disable();
        string message;
        try
        {
            // Match vanilla feedback capture: show the game, not the settings overlay.
            screen.Visible = false;
            NGame.Instance!.MainMenu?.DisableBackstopInstantly();
            NCapstoneContainer.Instance?.DisableBackstopInstantly();
            NRun.Instance?.GlobalUi.RelicInventory.ShowImmediately();
            NRun.Instance?.GlobalUi.MultiplayerPlayerContainer.ShowImmediately();
            await screen.AwaitProcessFrame();
            await screen.AwaitProcessFrame();
            await screen.AwaitProcessFrame();
            var path = GetLogsConsoleCmd.GetBugReportPath("-Seedline-" + Guid.NewGuid().ToString("N"));
            await GetLogsConsoleCmd.GrabLogs(path);
            message = NoticeText.Get("DiagnosticsSaved", path);
        }
        catch (Exception error)
        {
            Log.Error($"Seedline: diagnostic export failed.\n{error}");
            message = NoticeText.Get("DiagnosticsFailed", error.Message);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(screen))
                screen.Visible = true;
            NRun.Instance?.GlobalUi.RelicInventory.HideImmediately();
            NRun.Instance?.GlobalUi.MultiplayerPlayerContainer.HideImmediately();
            NGame.Instance!.MainMenu?.EnableBackstopInstantly();
            NCapstoneContainer.Instance?.EnableBackstopInstantly();
            exporting = false;
            if (GodotObject.IsInstanceValid(button))
                button.Enable();
        }
        var popup = NGenericPopup.Create()!;
        NModalContainer.Instance!.Add(popup);
        var result = popup.GetNode<NVerticalPopup>("VerticalPopup");
        result.SetText(NoticeText.Get("DiagnosticsTitle"), message.EscapeBbcodeTags());
        result.InitYesButton(new LocString("main_menu_ui", "GENERIC_POPUP.ok"), _ => { });
        result.HideNoButton();
        result.YesButton.FocusMode = Control.FocusModeEnum.All;
        result.YesButton.GrabFocus();
    }
}
