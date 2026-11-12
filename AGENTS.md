# Agent rules

- Base claims about existing mechanics on code or reference evidence.
- `sts2-decompiled/` and `catfood-decompiled/` are reference-only; never modify their contents.

## Design documentation

- Conciseness over precision.
- Test every sentence for reader value: remove obvious, implied or repeated information. Retain distinctions affecting interpretation or use.
- Write for implementers and design reviewers. Keep authoring instructions here; reading guides serve navigation and interpretation.
- Judge tradeoffs by documented goals and priorities; ask about unresolved choices.
- Use established terms; define new names before use, only for distinct, reusable concepts. Mention edge cases.
- Use gameplay wording when several words suffice; otherwise pair code references with explanations.
- In address expressions, place purpose before sharing scope.
- Follow [Document hierarchy](DESIGN.md#document-hierarchy).
- Extract repeated algorithms into mechanisms and cross-source requirements into shared policies; source policies add local rules and exceptions.
- State requirements in policy text; notes explain them. Put intent/scope first, baseline context last, and rationale beside its rule.
- Keep local preservation explanations only when they clarify a subtle implementation requirement.
- Review behavioral changes against the [behavior-preservation principle](DESIGN.md#goals-and-principles). Separate distinct deviations. For non-neutral grades (4B/5), illustrate when and how the strategy changes.
- Policy tables contain only their section's rules; overview/reference tables may summarize.
- Review policy names and necessity; propose terminology changes and retire redundancies.
- Report conflicting policies explicitly; do not silently settle them.
