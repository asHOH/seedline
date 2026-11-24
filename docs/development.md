# Development

Build, test and release Seedline. See [Implementation](implementation.md) for state and integration contracts.

## Build and validate

Install a .NET SDK compatible with [global.json](../global.json). In PowerShell 7, run from the repository root, replacing the example game path with your installation:

```powershell
./scripts/verify.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Integration checks use an isolated game copy, so allow disk space for it.
Later runs reuse game files whose sizes and modification times match. Test mods and save directories are refreshed each run.

- For a portable SDK, add `-Dotnet 'path/to/dotnet.exe'`.
- With RitsuLib, add `-RitsuLibDir '<extracted RitsuLib package>'`, using the [complete package](compatibility.md#ritsulib).
- To deploy after all checks pass, close the game and add `-Deploy`.

The validated package is written to `artifacts/Seedline/`. Close the game, then deploy:

```powershell
./scripts/deploy.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

Follow the [activation steps](../README.md#install).

## Diagnostics

Seedline records versions, run context and checkpoint failures in the game's log. Individual random decisions are not logged, so gameplay reports also need the affected save and reproduction steps.

## Releases

Set matching versions in `src/Seedline/Seedline.json` and `src/Seedline/Seedline.csproj`. Add short release notes in `docs/releases/<version>.md`, then commit the changes.

From a clean working tree, package against the supported game installation. Include RitsuLib to validate both configurations:

```powershell
./scripts/package-release.ps1 -GameDir 'D:/Games/Slay the Spire 2' -RitsuLibDir '<complete RitsuLib package>'
```

Packaging runs `verify.ps1` and creates `artifacts/Seedline-<version>.zip` and its `.sha256` checksum.

For Steam Workshop, follow the [private publication workflow](../workshop/README.md). Page text and artwork sources live in `workshop/`; upload workspaces stay in ignored `artifacts/`.

With GitHub CLI authenticated for repository write access, tag the tested commit and create a draft:

```powershell
$version = (Get-Content -Raw src/Seedline/Seedline.json | ConvertFrom-Json).version
git tag -a "v$version" -m "Seedline $version"
git push origin HEAD
git push origin "v$version"
gh release create "v$version" "artifacts/Seedline-$version.zip" "artifacts/Seedline-$version.zip.sha256" --verify-tag --draft --title "Seedline $version" --notes-file "docs/releases/$version.md"
```

Check the uploaded assets and notes, then publish with `gh release edit "v$version" --draft=false --latest`. Do not reuse a published version for different binaries.
