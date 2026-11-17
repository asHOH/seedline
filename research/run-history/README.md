# Run-history measurement

## Result

**65 / 33,022 runs (0.1968%)** contain a confirmed multi-Rare offer. Six unresolved runs give a **0.1968–0.2150%** reconstruction range.

Cohort: v0.111.0, single-player, standard A7–10 runs entering the **first Act 3 boss**, including wins, losses and abandons. Sources: elites, Orrery, Lost Coffer, Trial's Nondescript Guilty, and Brain Leech's Share Knowledge. Each run counts once.

Data: [Spire Codex](https://github.com/ptrlrd/spire-codex#api-endpoints), November 10, 2026 snapshot; **397,829 records**. Screening checks content IDs, event choices, starting gold/deck changes, combat card pools and rarity constraints, then deduplicates attempts. Mods qualify when vanilla gameplay is preserved; [Spire Delight's normal-play opt-out](https://steamcommunity.com/sharedfiles/filedetails/?id=3817784207) is allowed. Reconstruction separates offers, fixed-Rare rewards, supplemental cards and logging duplicates. Volunteer-upload bias and undetectable mods constrain generalization.

## Reproduce

The counter uses [v0.111.0 card and model metadata](reference-v0.111.0.json). With Node.js, run from the repository root:

```powershell
New-Item -ItemType Directory -Force .cache/run-history | Out-Null
Invoke-WebRequest https://spire-codex.com/api/exports/runs/manifest -OutFile .cache/run-history/source-manifest.json -ErrorAction Stop
curl.exe --fail --location --output .cache/run-history/runs-v0.111.0.jsonl.gz https://spire-codex.com/exports/runs-v0.111.0.jsonl.gz
node --test research/run-history/count.test.cjs
node research/run-history/count.cjs
```

The counter validates dataset size, checksum and record count against the downloaded manifest, then replaces the results and saved manifest. Refresh both downloads on mismatch.

[Results](results.json) · [Case evidence](findings.json) · [Rejected histories](rejected.json)
