# Agent rules

- Base claims about existing mechanics on code or reference evidence.
- Treat `sts2-decompiled/` and `catfood-decompiled/` as reference-only; never modify their contents.

## Design documentation

- Keep documentation concise, use established terms, and mention edge cases; don't expand on edge cases except when they materially affect the design.
- `DESIGN.md` owns settled policies and reusable reference facts; `WORKING.md` holds unresolved work and supporting evidence. On settlement, update `DESIGN.md` and remove resolved discussion from `WORKING.md`, retaining at most a one-line pointer where remaining work needs it.
- Organize `WORKING.md` by decision unit: split work only when choices can settle independently or require materially different evidence; keep undeveloped backlog compact and omit empty scaffolding.
- Keep `DESIGN.md` layered from goals/concepts → shared strategies → settled policies → reference/appendix. Define existing-mechanics references once, using vanilla by default and explicitly labeling Catfood differences.
- Define shared invariants, boundaries, consumption rules and rationale once; source policies add only local rules and exceptions. Reference shared contracts instead of repeating them or predictable gameplay modifiers.
- Within settled policies, put intent/scope before operative rules, baseline context last, and rationale beside the rule it explains; use architecture dimensions only where informative.
- Link from `DESIGN.md` to `WORKING.md` only at explicit unresolved seams; settled behavior must not depend on `WORKING.md`.
- Review all behavioral changes against the [behavior-preservation principle](DESIGN.md#goals-and-principles). Use the [vanilla-deviation convention](DESIGN.md#vanilla-deviations) for impact notes; do not repeat rationale in those notes.
- Report conflicting policies explicitly; do not silently settle them.
