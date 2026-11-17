# Event selection experiments

Evidence for the [event-selection policy](../../DESIGN.md#ordinary-event-selection) and its [rejected alternatives](../../DESIGN.md#event-selection-alternatives).

| Script | Comparison |
|---|---|
| [within-act-chain.mjs](within-act-chain.mjs) | Retained, refreshed and hybrid condition priorities across three acts, carrying policy-generated visits forward. |
| [within-act-sequences.mjs](within-act-sequences.mjs) | Within-act priority persistence, exact small-case distribution checks, and shared helpers for the chain model. |
| [insertion-coupling.mjs](insertion-coupling.mjs) | Uniformity of sorted random insertion priorities; comparison with weighted merging. |
| [early-overlap.mjs](early-overlap.mjs) | Prefix agreement and overlap across sorted priorities, position priorities, weighted merging and biased fixed spacing. |
| [wrap-comparison.mjs](wrap-comparison.mjs) | Exact exposure differences after count refill and wrapping. |

Run a script from the repository root with Node.js, for example:

```powershell
node research/event-overlap/within-act-chain.mjs
```

Each script validates its assertions and writes an ignored `*-results.json` beside it. Simulations use fixed seeds. Historical pools and prescribed state histories isolate algorithm tradeoffs; current gameplay frequencies require representative recorded routes.
