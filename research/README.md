# Research

## Findings

| Study | Finding | Evidence |
|---|---|---|
| [Card rarity](../design/DESIGN.md#rarity-calibration) | v0.111.0 calibration approximates vanilla Uncommon and shop Rare averages. | [A7+](reward-calibration/act3-results.json), [A0–6](reward-calibration/a6-results.json), [sensitivity](reward-calibration/results.json). |
| [Event selection](#event-selection) | Retained versus refreshed priorities trade relative order against mismatch timing. | [Alternatives](../design/DESIGN.md#event-selection-alternatives). |
| [Multi-Rare offers](#multi-rare-offers) | v0.111.0: **65 / 33,022 runs (0.1968%)** confirmed; six unresolved runs widen reconstruction to **0.1968–0.2150%**. | [Results](run-history/results.json), [cases](run-history/findings.json), [rejections](run-history/rejected.json). |
| [Relic Trader](../design/DESIGN.md#relic-trader-alternatives) | With one inventory substitution, the selected assignment maximizes expected outgoing overlap among symmetric rules. | [Assumptions and results](relic-trader/results.json), [checks](relic-trader/validate.cjs). |

## Methods

[Calibration model](reward-calibration/calibrate.cjs) and [environment](reward-calibration/environment.cjs).

Run commands from the repository root.

### [Event selection](../design/DESIGN.md#ordinary-event-selection)

| Script | Comparison |
|---|---|
| [Chain](event-overlap/within-act-chain.mjs) | Retained, refreshed and hybrid condition priorities across three acts, carrying policy-generated visits. |
| [Within-act](event-overlap/within-act-sequences.mjs) | Priority persistence, exact small-case distributions, shared chain helpers. |
| [Insertion](event-overlap/insertion-coupling.mjs) | Sorted random insertion priorities: uniformity versus weighted merging. |
| [Wrapping](event-overlap/wrap-comparison.mjs) | Exact exposure differences after count refill and wrapping. |

```powershell
node research/event-overlap/within-act-chain.mjs
```

Scripts check assertions and write ignored `*-results.json` beside themselves. Simulations use fixed seeds. Historical pools and prescribed state histories isolate algorithm tradeoffs; current gameplay frequencies require representative recorded routes.

### Event workbook

The [workbook](../references/StS2_Events.xlsx) records v0.113.1. Regeneration requires that game build, ILSpyCmd 9.1.0.7988 with its .NET 8 runtime, and Codex's bundled Node runtime (which supplies `@oai/artifact-tool`).

```powershell
ilspycmd -p -o sts2-decompiled '<v0.113.1 game>/data_sts2_windows_x86_64/sts2.dll'
& '<Codex bundled Node executable>' scripts/build-events.mjs
```

The [generator](../scripts/build-events.mjs) uses the workbook as its template and checks event coverage against the extracted source. Review its export in `research/event-workbook/` before replacing `references/StS2_Events.xlsx`. Game sources and previews stay local.

### Multi-Rare offers

Cohort: v0.111.0 standard single-player A7–10 runs reaching the **first Act 3 boss**, including wins, losses and abandons; each counted once. Sources: elites, Orrery, Lost Coffer, Trial's Nondescript Guilty and Brain Leech's Share Knowledge.

[Spire Codex](https://github.com/ptrlrd/spire-codex#api-endpoints) snapshot: November 10, 2026; **397,829 records**. Screening checks content IDs, event choices, starting gold/deck changes, combat card pools and rarity constraints, then deduplicates attempts. Mods preserving vanilla qualify, including [Spire Delight's normal-play opt-out](https://steamcommunity.com/sharedfiles/filedetails/?id=3817784207). Reconstruction separates offers, fixed-Rare rewards, supplemental cards and logging duplicates. Volunteer-upload bias and undetectable mods constrain generalization.

Uses [v0.111.0 card/model metadata](run-history/reference-v0.111.0.json).

```powershell
New-Item -ItemType Directory -Force .cache/run-history | Out-Null
Invoke-WebRequest https://spire-codex.com/api/exports/runs/manifest -OutFile .cache/run-history/source-manifest.json -ErrorAction Stop
curl.exe --fail --location --output .cache/run-history/runs-v0.111.0.jsonl.gz https://spire-codex.com/exports/runs-v0.111.0.jsonl.gz
node --test research/run-history/count.test.cjs
node research/run-history/count.cjs
```

The counter validates dataset size, checksum and record count against the downloaded manifest before replacing results and saved manifest. Refresh both downloads on mismatch.
