# Agent rules

- Read and fully understand the design document first.
- Base claims about existing mechanics on code or reference evidence.
- `sts2-decompiled/` and `catfood-decompiled/` are reference-only; never modify their contents.

## Design documentation

- Prefer concise rules over exhaustive qualifications; leave obvious implementation details.
- Test every sentence for reader value: remove obvious, implied or repeated information; retain distinctions affecting interpretation or use.
- Write for implementers and design reviewers. Keep authoring instructions here; reading guides serve navigation and interpretation.
- Follow [Document hierarchy](DESIGN.md#document-hierarchy).
- Prefer established terms; define new names before use, only for distinct, reusable concepts. Include edge cases affecting implementation or interpretation; name exceptional sources directly.
- Judge tradeoffs by documented goals and priorities; ask about unresolved choices.
- Prefer brief gameplay wording; pair necessary code references with explanations.
- In address expressions, place purpose before sharing scope.
- Extract repeated algorithms into mechanisms and cross-source requirements into shared policies; source policies add local rules and exceptions.
- Put requirements in policies, explanations in notes, intent/scope first, baseline last, and rationale beside rules.
- Rely on shared rules and preservation defaults; repeat only to clarify subtle implementation requirements.
- Assess deviations against [behavior preservation](DESIGN.md#goals-and-principles). Separate distinct deviations; illustrate when and how strategy changes for grades 4B/5.
- Keep policy tables section-local; fold short qualifications into relevant rows. Overview/reference tables may summarize.
- Review policy names and necessity; propose terminology changes and retire redundancies.
- Report policy conflicts explicitly.

## Implementation documentation

- Keep [IMPLEMENTATION.md](IMPLEMENTATION.md) under 350 words.
