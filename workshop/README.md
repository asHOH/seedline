# Workshop

Use `artwork/image.png` for the thumbnail, `artwork/gallery/` for gallery exports, and `artwork/source/` for originals. Upload images must be under 1 MiB.

Build the [release archive](../docs/development.md#releases), then run `./scripts/prepare-workshop.ps1` from the repo root for private staging.

With Steam running, upload using `.tools/sts2-mod-uploader/v0.2.0/ModUploader.exe upload -w <workspace>`.

Save generated `mod_id.txt` here. Subsequent preparation uses `-Update`, preserving online text/visibility but resending thumbnail/dependencies.

`-IncludeGallery` synchronizes previews, deleting missing images.

Uploader v0.2.0 writes English only. Change notes are bilingual.

When description text changes, edit `descriptions/english.bbcode` and `descriptions/schinese.bbcode`, then run this standalone command in PowerShell 7 with Steam signed in:

```powershell
./scripts/update-workshop-descriptions.ps1 -GameDir 'D:/Games/Slay the Spire 2'
```

It uploads both descriptions to the existing item without changing binaries, titles, visibility or release notes. It is separate from release publishing and does not compare or skip unchanged text.

Keep local text synchronized with online edits. QQ placeholders are acceptable privately.
