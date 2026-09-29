# Agent rules

- Base claims about existing mechanics on code or reference evidence.
- Treat `sts2-decompiled/` and `catfood-decompiled/` as reference-only; never modify their contents.

# Design documentation

- Keep documentation concise, use established terms, and mention edge cases; don't expand on edge cases except when they materially affect the design.
- `DESIGN.md` owns settled policies and reusable reference facts; `WORKING.md` holds unresolved work and supporting evidence. On settlement, update `DESIGN.md` and remove resolved discussion from `WORKING.md`, retaining at most a one-line pointer where remaining work needs it.
- Organize `WORKING.md` by decision unit: split work only when choices can settle independently or require materially different evidence; keep undeveloped backlog compact.
- Keep `DESIGN.md` layered from goals/concepts → shared strategies → settled policies → reference/appendix. Avoid duplicating detailed baseline mechanics; vanilla is the default baseline and Catfood differences are explicitly labeled.
- Define shared invariants, boundaries, consumption rules and rationale once; source policies add only local rules and exceptions. Reference shared contracts instead of repeating them or predictable gameplay modifiers.
- Link from `DESIGN.md` to `WORKING.md` only at explicit unresolved seams; settled behavior must not depend on `WORKING.md`.
