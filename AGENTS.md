# Agent rules

- Base claims about existing mechanics on code or reference evidence.
- `sts2-decompiled/` and `catfood-decompiled/` are reference-only; never modify their contents.

## Design documentation

- Conciseness over precision.
- Use established terms. Introduce a separate name only for a distinct, reusable concept; define before use.
- Mention edge cases, but don't expand on them except when they materially affect the design.
- `DESIGN.md` owns settled policies and reusable reference facts; `WORKING.md` holds unresolved work and supporting evidence. Settled behavior must not depend on `WORKING.md`.
- Split policies or problems into decoupled units if possible. Extract as a shared strategy if two or more policy overlap strongly.
- Organize `WORKING.md` by decision unit: split work only when choices can settle independently or require different evidence.
- Keep `DESIGN.md` layered from goals/concepts → shared strategies → settled policies → reference/appendix. Define existing-mechanics references once, using vanilla by default and labeling Catfood differences.
- Define shared invariants, boundaries, consumption rules and rationale once; source policies add only local rules and exceptions.
- Reference shared contracts instead of repeating them.
- Within settled policies, put intent/scope before operative rules, baseline context last, and rationale beside the rule it explains; use architecture dimensions only where informative.
- Review behavioral changes against the [behavior-preservation principle](DESIGN.md#goals-and-principles). Separate distinct deviations.
- Report conflicting policies explicitly; do not silently settle them.
