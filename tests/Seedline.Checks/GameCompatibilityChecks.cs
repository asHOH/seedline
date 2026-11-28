namespace Seedline.Checks;

public static partial class Checks
{
    private static void CheckGameCompatibility()
    {
        var warnings = new List<string>();
        Assert(GameCompatibility.Check("v0.114.0", "a7b9a9c0", warnings.Add) == GameCompatibility.ValidatedBuild &&
            warnings.Count == 0, "validated game build starts without a compatibility warning");
        Assert(GameCompatibility.Check("0.114.0", "a7b9a9c0", warnings.Add) == GameCompatibility.ValidatedBuild &&
            warnings.Count == 0, "optional version prefix preserves checkpoint build identity");
        foreach (var (version, commit) in new[]
        {
            ("v0.114.0", "another-commit"), ("v0.114.1", "patch"),
            ("v0.115.0", "next"), ("v0.115.0-beta.1", "preview"), ("v1.0.0", "release")
        })
        {
            warnings.Clear();
            var build = GameCompatibility.Check(version, commit, warnings.Add);
            Assert(build == $"{version}/{commit}" && warnings.Count == 1 && warnings[0].Contains(build),
                $"unvalidated game build {build} is allowed with a warning and its own checkpoint identity");
        }
        foreach (var (version, commit) in new (string?, string?)[]
        {
            ("v0.113.1", "old"), ("v0.114.0-beta.1", "preview"),
            (null, "commit"), ("invalid", "commit"), ("v0.114.0", null), ("v0.114.0", " ")
        })
        {
            warnings.Clear();
            var rejected = false;
            try { GameCompatibility.Check(version, commit, warnings.Add); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && warnings.Count == 0,
                $"older or unidentified game build {version}/{commit} is rejected");
        }
    }
}
