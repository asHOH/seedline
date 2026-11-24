using System.IO.Compression;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.addons.mega_text;

namespace Seedline.Checks;

public static partial class Checks
{
    private static TaskCompletionSource exportGate = new();
    private static readonly List<string> exportPaths = [];
    private static bool failExport;
    private const string exportFailure = @"Cannot write C:\[alice\[b]report[/b].zip";

    private static NSettingsScreen ExportSettings(bool inRun)
    {
        var screen = SceneHelper.Instantiate<NSettingsScreen>("screens/settings_screen");
        screen.SetIsInRun(inRun);
        NGame.Instance!.AddChild(screen);
        screen.Show();
        screen.GetNode<NSettingsTabManager>("%SettingsTabManager").ResetTabs();
        return screen;
    }

    private static async Task ExportFrame()
    {
        // Capture waits three frames; also allow deferred layout and popup sizing to finish.
        for (var frame = 0; frame < 4; frame++)
            await NGame.Instance!.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static async Task CheckExportMenu()
    {
        var screen = ExportSettings(false);
        try
        {
            await ExportFrame();
            var panel = screen.GetNode<NSettingsPanel>("%GeneralSettings");
            var row = panel.Content.GetNode<Control>("SeedlineDiagnostics");
            var button = row.GetNode<NButton>("ExportButton");
            var original = screen.GetNode<NOpenFeedbackScreenButton>("%FeedbackButton");
            Assert(button.IsEnabled && button.FocusMode == Control.FocusModeEnum.All &&
                button.GetNode<MegaLabel>("Label").Text == NoticeText.Get("ExportDiagnostics") &&
                original.GetParent() == panel.Content.GetNode("SendFeedback"),
                "main-menu settings expose a localized export button without replacing game feedback");
            Assert(original.FocusNeighborBottom == button.GetPath() && button.FocusNeighborTop == original.GetPath() &&
                row.Position.Y >= original.GetParent<Control>().Position.Y + original.GetParent<Control>().Size.Y,
                "export row fits the settings list and participates in controller navigation");
        }
        finally { screen.Free(); }
    }

    private static async Task CheckDiagnosticExport(SerializableRun snapshot)
    {
        var screen = ExportSettings(true);
        var locale = LocManager.Instance;
        var language = locale.Language;
        var patch = new Harmony("Seedline.Checks.Export");
        // Headless viewports have no screenshot. Exercise the real ZIP collector with a fixture
        // image, without opening Explorer during automated checks.
        patch.Patch(AccessTools.Method(typeof(GetLogsConsoleCmd), nameof(GetLogsConsoleCmd.GrabLogs)),
            prefix: new HarmonyMethod(typeof(Checks), nameof(ExportProbe)));
        patch.Patch(AccessTools.Method(typeof(GetLogsConsoleCmd), nameof(GetLogsConsoleCmd.GetBugReportPath)),
            postfix: new HarmonyMethod(typeof(Checks), nameof(ExportPathProbe)));
        try
        {
            await ExportFrame();
            var row = screen.GetNode<NSettingsPanel>("%GeneralSettings").Content.GetNode<Control>("SeedlineDiagnostics");
            var button = row.GetNode<NButton>("ExportButton");
            var localize = AccessTools.Method(typeof(NSettingsScreen), "LocalizeLabels");
            foreach (var code in LocManager.Languages.Append("eng"))
            {
                locale.SetLanguage(code);
                localize.Invoke(screen, null);
                Assert(row.GetNode<MegaRichTextLabel>("Label").Text == NoticeText.GetTemplate(code, "DiagnosticsTitle") &&
                    button.GetNode<MegaLabel>("Label").Text == NoticeText.GetTemplate(code, "ExportDiagnostics") &&
                    screen.GetNode<NSettingsPanel>("%GeneralSettings").Content.GetChildren().Count(n => n.Name == "SeedlineDiagnostics") == 1,
                    $"{code}: export controls follow the selected language without duplicate rows");
                Assert(NoticeText.Get("DiagnosticsSaved", "report.zip").Contains("report.zip") &&
                    NoticeText.Get("DiagnosticsFailed", "failure").Contains("failure"), $"{code}: export status templates format correctly");
            }
            locale.SetLanguage(language);
            localize.Invoke(screen, null);
            var savePath = Path.Combine(ProjectSettings.GlobalizePath(UserDataPathProvider.GetAccountScopedBasePath(null)),
                RunSaveManager.GetRunSavePath(SaveManager.Instance.CurrentProfileId, RunSaveManager.runSaveFileName));
            var before = System.IO.File.ReadAllBytes(savePath);
            System.IO.File.WriteAllBytes(savePath + ".backup", before);
            var logDirectory = Path.Combine(OS.GetUserDataDir(), "logs");
            Directory.CreateDirectory(logDirectory);
            System.IO.File.WriteAllText(Path.Combine(logDirectory, "seedline-export-check.log"), "Seedline export diagnostic fixture");
            var stateBefore = RunCheckpoints.Encode(snapshot);
            DecisionTrace.Record("export-number-fixture", () => new { seed = "76561123456789012", priority = 76561123456789012UL });
            exportGate = new TaskCompletionSource();
            exportPaths.Clear();
            button.EmitSignal(NClickableControl.SignalName.Released, button);
            Assert(!button.IsEnabled && !screen.Visible,
                "one click hides settings for capture and disables the export button");
            await ExportFrame();
            button.EmitSignal(NClickableControl.SignalName.Released, button);
            await ExportFrame();
            Assert(exportPaths.Count == 1 && NModalContainer.Instance!.OpenModal == null && !screen.Visible,
                "export captures without settings and repeated clicks do not start another export");
            exportGate.SetResult();
            await ExportFrame();
            var result = ((Node)NModalContainer.Instance!.OpenModal!).GetNode<NVerticalPopup>("VerticalPopup");
            var description = result.GetNode<MegaRichTextLabel>("Description");
            MegaLabelHelper.ParseBbcode(description.Text);
            Assert(screen.Visible && button.IsEnabled && !result.NoButton.Visible && result.YesButton.HasFocus() &&
                description.GetParsedText() == NoticeText.Get("DiagnosticsSaved", exportPaths[0]),
                "successful export restores settings and displays bracket-containing paths literally");
            using (var zip = ZipFile.OpenRead(exportPaths[0]))
            {
                var entries = zip.Entries.Select(e => e.FullName).ToArray();
                Assert(entries.Contains("logs/seedline-export-check.log") && entries.Contains("release_info.json") && entries.Contains("screenshot.png") &&
                    entries.Any(e => e.EndsWith("/current_run.save")) && entries.Any(e => e.EndsWith("/current_run.save.backup")),
                    "exported ZIP contains game metadata, logs, screenshot and checkpoint pair");
                using var reader = new StreamReader(zip.Entries.Single(e => e.FullName.EndsWith("/current_run.save")).Open());
                Assert(reader.ReadToEnd().Contains("\"seedline\""), "game exporter retains Seedline checkpoint data");
                using var trace = new StreamReader(zip.GetEntry("logs/seedline/decisions.jsonl")!.Open());
                var records = trace.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => System.Text.Json.Nodes.JsonNode.Parse(line)!).ToArray();
                var numeric = records.Single(r => (string?)r["kind"] == "export-number-fixture")["data"]!;
                Assert((string?)numeric["seed"] == "76561123456789012" && (string?)numeric["priority"] == "76561123456789012" &&
                    records.Any(r => (string?)r["kind"] == "checkpoint-written"),
                    "one-click ZIP includes flushed decisions and preserves diagnostic numbers through the real sanitizer");
            }
            Assert(System.IO.File.ReadAllBytes(savePath).SequenceEqual(before) && RunCheckpoints.Encode(snapshot).SequenceEqual(stateBefore),
                "export leaves checkpoint files and captured random state unchanged");
            NModalContainer.Instance.Clear();
            failExport = true;
            button.EmitSignal(NClickableControl.SignalName.Released, button);
            await ExportFrame();
            result = ((Node)NModalContainer.Instance.OpenModal!).GetNode<NVerticalPopup>("VerticalPopup");
            description = result.GetNode<MegaRichTextLabel>("Description");
            MegaLabelHelper.ParseBbcode(description.Text);
            Assert(screen.Visible && button.IsEnabled && description.GetParsedText() == NoticeText.Get("DiagnosticsFailed", exportFailure) &&
                exportPaths[0] != exportPaths[1], "failed exports restore settings and display bracket-containing errors literally");
            NModalContainer.Instance.Clear();
            failExport = false;
            button.EmitSignal(NClickableControl.SignalName.Released, button);
            await ExportFrame();
            Assert(exportPaths.Count == 3 && System.IO.File.Exists(exportPaths[2]) && NModalContainer.Instance.OpenModal != null,
                "export can be retried after failure");
        }
        finally
        {
            failExport = false;
            patch.UnpatchAll(patch.Id);
            NModalContainer.Instance!.Clear();
            locale.SetLanguage(language);
            screen.Free();
        }
    }

    private static bool ExportProbe(string bugReportPath, ref Task __result)
    {
        Assert(!NGame.Instance!.GetNode<NSettingsScreen>("SettingsScreen").Visible,
            "settings overlay is hidden when the game exporter captures its screenshot");
        exportPaths.Add(bugReportPath);
        __result = ExportFixture(bugReportPath);
        return false;
    }

    private static void ExportPathProbe(ref string __result)
    {
        var directory = Path.Combine(Path.GetDirectoryName(__result)!, "[player");
        Directory.CreateDirectory(directory);
        __result = Path.Combine(directory, Path.GetFileName(__result));
    }

    private static async Task ExportFixture(string path)
    {
        if (failExport)
            throw new IOException(exportFailure);
        await exportGate.Task;
        using var image = Image.CreateEmpty(2, 2, false, Image.Format.Rgba8);
        using var stream = new FileStream(path, FileMode.CreateNew);
        GetLogsConsoleCmd.ZipFiles(stream, image.SavePngToBuffer());
    }
}
