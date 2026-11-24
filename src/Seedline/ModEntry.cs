using System.Security.Cryptography;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public const string Id = "Seedline";
    internal static string GameBuild { get; private set; } = "";
    private static bool loggedMods;

    internal static void LogLoadedMods()
    {
        if (loggedMods || ModManager.State != ModManagerState.Initialized)
            return;
        Log.Info($"Seedline: loaded mods: {string.Join(", ", ModManager.GetLoadedMods().Select(m => $"{m.manifest?.id}@{m.manifest?.version}"))}.");
        loggedMods = true;
    }

    public static void Initialize()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Seedline requires Windows.");
        using (var file = File.OpenRead(typeof(RunManager).Assembly.Location))
        {
            GameBuild = Convert.ToHexString(SHA256.HashData(file)) switch
            {
                "62ECBE91093E1FA862DE4329AAC865209DCB297804CAF0F0520D92D8C0B4A9C3" => "v0.113.1/d09d48e5",
                _ => throw new InvalidOperationException("Seedline requires v0.113.1 build d09d48e5.")
            };
        }

        var harmony = new Harmony(Id);
        try
        {
            // Install choice entry points before rewritten callers can inline their original async stubs.
            foreach (var patch in typeof(ModEntry).Assembly.GetTypes().Where(t => t.IsDefined(typeof(HarmonyPatch), false))
                .OrderByDescending(CombatRouting.IsChoicePatch))
                harmony.CreateClassProcessor(patch).Patch();
            DecisionTrace.Initialize(() => Path.Combine(Godot.OS.GetUserDataDir(), "logs", "seedline"), () =>
            {
                var run = RunManager.Instance.DebugOnlyGetState();
                var combat = MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState();
                return new
                {
                    build = GameBuild,
                    version = typeof(ModEntry).Assembly.GetName().Version?.ToString(),
                    run = run == null ? null : RunCheckpoints.Find(run)?.RunId.ToString(),
                    seed = run?.Rng.StringSeed,
                    act = run?.CurrentActIndex + 1,
                    floor = run?.TotalFloor,
                    round = combat?.RunState == run ? combat?.RoundNumber : null
                };
            }, error => Log.Warn($"Seedline: randomness diagnostics stopped for this session. {error}"));
            Log.Info($"Seedline: version={typeof(ModEntry).Assembly.GetName().Version}; Reward supplies and checkpoints initialized for {GameBuild}; checkpoint schema={RunCheckpoints.SchemaVersion}.");
        }
        catch
        {
            harmony.UnpatchAll(Id);
            throw;
        }
    }
}
