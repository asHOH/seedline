using System.Security.Cryptography;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;

namespace SameSeed;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public const string Id = "SameSeed";
    internal const string GameBuild = "v0.113.0/16ac0fe3";
    internal const string AssemblyHash = "A6CC282C52B96AD2E01D5692B1144B33AA8E3BA4E969582F648ECB13B2F751D7";
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
            return;

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("SameSeed requires Windows.");
        using (var file = File.OpenRead(typeof(RunManager).Assembly.Location))
        {
            if (Convert.ToHexString(SHA256.HashData(file)) != AssemblyHash)
                throw new InvalidOperationException($"SameSeed requires game build {GameBuild}.");
        }

        var harmony = new Harmony(Id);
        try
        {
            harmony.PatchAll(typeof(ModEntry).Assembly);
            initialized = true;
            Log.Info($"{Id}: Combat gold and checkpoints initialized for {GameBuild}.");
        }
        catch
        {
            harmony.UnpatchAll(Id);
            throw;
        }
    }

    internal static void CheckOtherMods()
    {
        // Check at run entry too: Catfood may initialize after this mod.
        if (ModManager.Mods.Any(m => m.state == ModLoadState.Loaded &&
            (IsCatfood(m.manifest?.id) || m.assemblies.Any(a => IsCatfood(a.GetName().Name)))))
            throw new InvalidOperationException("Disable Catfood before starting or continuing a SameSeed run.");
    }

    private static bool IsCatfood(string? name) => name != null &&
        (name.Contains("catfood", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("CFCRacing", StringComparison.OrdinalIgnoreCase));
}
