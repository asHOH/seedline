using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static JsonNode[] DecisionRecords(string kind)
    {
        var directory = Path.Combine(OS.GetUserDataDir(), "logs", "seedline");
        static string[] ReadLines(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
        return new[] { "decisions.previous.jsonl", "decisions.jsonl" }.Select(n => Path.Combine(directory, n))
            .Where(System.IO.File.Exists).SelectMany(ReadLines)
            .Select(line => JsonNode.Parse(line)!).Where(r => (string?)r["kind"] == kind).ToArray();
    }

    private static void CheckDecisionEvidence()
    {
        const string seed = "decision-evidence";
        var address = RandomAddress.For("diagnostic acceptance");
        SelectionState Initial()
        {
            var state = new SelectionState();
            state.Get(seed, address, "A").Remaining = 0.5;
            state.Get(seed, address, "B").Remaining = 0.8;
            state.Get(seed, address, "C").Remaining = 0.9;
            return state;
        }
        // Three realistic causes of a changed result: consumption, changed eligibility, and
        // incorrect advancement. The exported clocks must distinguish them without a live run.
        var normal = Initial();
        normal.Select(seed, address, ["A", "B", "C"], 1);
        normal.Select(seed, address, ["B", "C"], 1);
        Initial().Select(seed, address, ["B", "C"], 1);
        var incorrect = Initial();
        incorrect.Clocks[address.With("B").Key].Remaining = 0.1;
        incorrect.Select(seed, address, ["A", "B", "C"], 1);
        // A restored checkpoint repeats its original clocks, not the later live state.
        Initial().Select(seed, address, ["A", "B", "C"], 1);
        using var output = new MemoryStream();
        GetLogsConsoleCmd.ZipFiles(output, []);
        output.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("logs/seedline/decisions.jsonl")!.Open());
        var records = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonNode.Parse(line)!).Where(r => (string?)r["kind"] == "selection" && (string?)r["data"]?["seed"] == seed)
            .Select(r => r["data"]!).ToArray();
        Assert(records.Length == 5 && records[0]["candidates"]!.AsArray().Count == 3 &&
            (string?)records[1]["candidates"]![0]!["before"] == "0.30000000000000004" &&
            (string?)records[2]["candidates"]![0]!["before"] == "0.8" &&
            (string?)records[3]["candidates"]![0]!["key"] == "B" &&
            (string?)records[3]["candidates"]![0]!["before"] == "0.1" &&
            records[0].ToJsonString() == records[4].ToJsonString(),
            "exported evidence distinguishes consumption, eligibility changes, incorrect clocks and checkpoint replay");
    }

    private static bool measuringTrace;
    private static readonly List<(double Ms, long Bytes)> traceSamples = [];

    private static void BeginTraceTiming(out (long Start, long Bytes) __state) =>
        __state = measuringTrace ? (Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread()) : default;

    private static void EndTraceTiming((long Start, long Bytes) __state)
    {
        if (__state.Start != 0)
            traceSamples.Add((Stopwatch.GetElapsedTime(__state.Start).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - __state.Bytes));
    }

    private static void CheckDecisionRecordingCost()
    {
        var patch = new Harmony("Seedline.Checks.TraceCost");
        var warnings = new List<string>();
        void Warning(LogLevel level, string text, int skip)
        {
            if (text.Contains("randomness diagnostics stopped")) warnings.Add(text);
        }
        Log.LogCallback += Warning;
        patch.Patch(AccessTools.Method(typeof(DecisionRecorder), nameof(DecisionRecorder.Write)),
            prefix: new HarmonyMethod(typeof(Checks), nameof(BeginTraceTiming)),
            postfix: new HarmonyMethod(typeof(Checks), nameof(EndTraceTiming)));
        try
        {
            foreach (var size in new[] { 30, 2000 })
            {
                var elapsed = 0.0;
                long allocated = 0;
                for (var iteration = 0; iteration < 5; iteration++)
                {
                    var run = StartGoldRun(seed: "TRACE-COST");
                    var player = run.Players[0];
                    while (player.Deck.Cards.Count < size)
                    {
                        var card = run.CreateCard<StrikeIronclad>(player);
                        CardOccurrences.Create(card, RandomAddress.For("cost fixture").At(player.Deck.Cards.Count).Key);
                        player.Deck.AddInternal(card, silent: true);
                    }
                    var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
                    combat.AddPlayer(player);
                    traceSamples.Clear();
                    measuringTrace = true;
                    try { CombatManager.Instance.SetUpCombat(combat); }
                    finally { measuringTrace = false; }
                    var record = DecisionRecords("shuffle").Last();
                    Assert(warnings.Count == 0 && traceSamples.Count >= 2 &&
                        (string?)record["context"]!["run"] == RunCheckpoints.Require(run).RunId.ToString() &&
                        record["data"]!["order"]!.AsArray().Count == size,
                        $"{size}-card real shuffle writes complete context and every card without recorder failure");
                    if (iteration > 0)
                    {
                        elapsed += traceSamples.Sum(s => s.Ms);
                        allocated += traceSamples.Sum(s => s.Bytes);
                    }
                    RunManager.Instance.CleanUp();
                }
                GD.Print($"Live combat-entry recording cost: cards={size}, ms={elapsed / 4:F3}, allocated={allocated / 4}");
            }
            Assert(Directory.GetFiles(Path.Combine(OS.GetUserDataDir(), "logs", "seedline")).All(p => new FileInfo(p).Length <= 4 * 1024 * 1024),
                "real large-deck records retain bounded files");
        }
        finally
        {
            measuringTrace = false;
            patch.UnpatchAll(patch.Id);
            Log.LogCallback -= Warning;
            RunManager.Instance.CleanUp();
        }
    }
}
