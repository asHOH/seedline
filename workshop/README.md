# Workshop

Use `artwork/image.png` for the thumbnail, `artwork/gallery/` for gallery exports, and `artwork/source/` for originals. Upload images must be under 1 MiB.

Build the [release archive](../docs/development.md#releases), then run `./scripts/prepare-workshop.ps1` from the repo root for private staging.

With Steam running, upload using `.tools/sts2-mod-uploader/v0.2.0/ModUploader.exe upload -w <workspace>`.

Save generated `mod_id.txt` here. Subsequent preparation uses `-Update`, preserving online text/visibility but resending thumbnail/dependencies.

`-IncludeGallery` synchronizes previews, deleting missing images.

Uploader v0.2.0 writes English only. Add Chinese through language-aware editing. Change notes are bilingual.

Keep local text synchronized with online edits. QQ placeholders are acceptable privately.
