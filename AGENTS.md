# Agent rules

- Base claims about existing mechanics on code or reference evidence.
- `sts2-decompiled/` and `catfood-decompiled/` are reference-only; never modify their contents.

## Design documentation

- Conciseness over precision. Test every sentence for reader value: remove obvious, implied or repeated information. Keep distinctions affecting interpretation or use.
- Write for implementers and design reviewers. Keep authoring instructions here; reading guides serve navigation and interpretation.
- Use established terms; define new names before use, only for distinct, reusable concepts. Mention edge cases; expand only when material.
- In address expressions, place purpose before sharing scope.
- Follow [Document hierarchy](DESIGN.md#document-hierarchy): goals/concepts → global rules → mechanisms → shared policies → source policies → reference/appendix.
- Extract repeated algorithms into mechanisms and cross-source requirements into shared policies. Define shared invariants, boundaries, consumption rules and rationale once; source policies add local rules and exceptions.
- State requirements in policy text; notes explain them. Put intent/scope first, baseline context last, and rationale beside its rule.
- Define existing mechanics once: vanilla by default, Catfood differences labeled. Prefer baseline links; explain labels only when needed for interpretation.
- Review behavioral changes against the [behavior-preservation principle](DESIGN.md#goals-and-principles). Separate distinct deviations. For non-neutral grades (4B/5), illustrate when and how the strategy changes.
- Policy tables contain only their section's rules; overview/reference tables may summarize.
- Review policy names and necessity; rename, merge, simplify or remove where useful.
- Report conflicting policies explicitly; do not silently settle them.
