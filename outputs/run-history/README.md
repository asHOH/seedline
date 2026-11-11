# Run-history measurement

## Current result

**65 / 33,022 runs (0.1968%)** contain a confirmed multi-Rare offer. Six unresolved runs give a **0.1968–0.2150%** reconstruction range.

Cohort: v0.111.0, single-player, standard A7–10 runs entering the **first Act 3 boss**, including wins, losses and abandons. Sources: elites, Orrery, Lost Coffer, Trial's Nondescript Guilty, and Brain Leech's Share Knowledge. Each run counts once.

Data: [Spire Codex](https://github.com/ptrlrd/spire-codex#api-endpoints), November 10, 2026 snapshot; **397,829 records**. Screening checks content IDs, event choices, starting gold/deck changes, combat card pools and rarity constraints, then deduplicates attempts. Mods qualify when vanilla gameplay is preserved; [Spire Delight's normal-play opt-out](https://steamcommunity.com/sharedfiles/filedetails/?id=3817784207) is allowed. Reconstruction separates offers, fixed-Rare rewards, supplemental cards and logging duplicates.

## Reproduce

Requires Node.js and the local v0.111.0 reference code. From the repository root:

```powershell
New-Item -ItemType Directory -Force .cache/run-history | Out-Null
Invoke-WebRequest https://spire-codex.com/api/exports/runs/manifest -OutFile .cache/run-history/source-manifest.json -ErrorAction Stop
curl.exe --fail --location --output .cache/run-history/runs-v0.111.0.jsonl.gz https://spire-codex.com/exports/runs-v0.111.0.jsonl.gz
node --test outputs/run-history/count.test.cjs
node outputs/run-history/count.cjs
```

Downloads refresh v0.111.0 data in the Git-ignored cache. The counter validates bytes, checksum and record count against the adjacent downloaded manifest, then saves it with the results. A mismatch requires refreshing both downloads.

[Results](results.json) · [Case evidence](findings.json) · [Rejected histories](rejected.json)

The figures above describe the recorded snapshot; reruns update the JSON files. Volunteer-upload bias and undetectable mods constrain generalization.
