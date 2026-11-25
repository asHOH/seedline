# Agent rules

- English is authoritative for all documents except Workshop pages, where Chinese is authoritative.

## Design decisions

- Read and fully understand the [design document](design/DESIGN.md) first. Use its goals and priorities to judge tradeoffs and ask about unresolved design choices.
- Base claims about existing mechanics on code or reference evidence.
- When a verified mechanic gives a rule a surprising consequence, check it against the design intent and trace what else the rule governs. If several gameplay outcomes remain plausible, show the smallest example that distinguishes them and ask which is intended before proposing an edit.
- Assess deviations against [behavior preservation](design/DESIGN.md#goals-and-principles) and report policy conflicts.

## Implementation constraints

- `sts2-decompiled/` and `catfood-decompiled/` are reference-only; never modify their contents.
- Exclude compatibility work for older development saves.
- Avoid redundant defensive checks and over-engineering; keep validation at real input boundaries.

## Design document structure

- Follow [Document hierarchy](design/DESIGN.md#document-hierarchy). Define common behavior once, rely on preservation defaults, and state local exceptions explicitly; repeat only to clarify subtle implementation requirements.
- Keep authoring instructions here; reading guides serve navigation and interpretation.
- Put requirements in policies, explanations in notes, intent/scope first, baseline last, and rationale beside rules.
- Keep policy tables section-local; fold short qualifications into relevant rows. Overview/reference tables may summarize.

## Design writing

- Write for implementers and design reviewers. Prefer brief gameplay wording; pair necessary code references with explanations. Never refer to git-untracked files.
- Test every sentence for reader value. Remove obvious, implied or repeated information; retain distinctions affecting interpretation or use.
- Prefer established terms; define new names before use, only for distinct, reusable concepts. Review terminology and policies for necessity, propose clearer names, and retire redundancies.
- Include edge cases affecting implementation or interpretation; name exceptional sources directly.
- In address expressions, place purpose before sharing scope.
