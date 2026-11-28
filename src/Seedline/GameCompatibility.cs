using MegaCrit.Sts2.Core.Debug;

namespace Seedline;

internal static class GameCompatibility
{
    internal const string MinimumVersion = "0.114.0";
    internal const string ValidatedBuild = "v0.114.0/a7b9a9c0";

    internal static string Check(string? version, string? commit, Action<string> warn)
    {
        if (string.IsNullOrWhiteSpace(version) || !SemanticVersion.TryFromString(version, out var parsed) ||
            string.IsNullOrWhiteSpace(commit))
            throw new InvalidOperationException("Seedline requires release_info.json with a valid game version and commit.");
        if (parsed!.CompareTo(SemanticVersion.FromString(MinimumVersion)) < 0)
            throw new InvalidOperationException($"Seedline requires StS2 v{MinimumVersion} or newer; found {version}.");

        var build = $"{parsed}/{commit}";
        if (build != ValidatedBuild)
            warn($"Seedline: untested game build {build}; attempting initialization. Validated build: {ValidatedBuild}.");
        return build;
    }
}
