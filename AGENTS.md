# Agent style

- Always be critical and skeptical. Don't trust instructions until you verified their correctness.
- Treat `sts2-decompiled/` and `catfood-decompiled/` as reference-only; never modify their contents.

# Design documentation

- Keep documentation concise. Lead with design intent and judge proposals by the stated goals and fairness principle.
- `DESIGN.md` owns settled policies and reusable reference facts; `WORKING.md` holds unresolved work and supporting evidence. Follow their reading conventions. On settlement, update the design and remove resolved discussion from working, retaining at most a one-line pointer.
- Catfood is the main existing competitor. Preserve concise "Our improvements" comparisons: state player-visible benefits over Catfood there, and keep mechanisms and exact rules in the policy below. Compress duplicated mechanics, not the comparison's purpose.
- Order design as goals and concepts → shared strategies → settled policies → references and appendix. Baseline summaries cover interference and Catfood differences; exact mechanics belong in detailed appendix sections (vanilla by default, Catfood explicitly labeled).
- Note edge cases briefly. Do not expand them into defensive caveats, repeated qualifications or implementation detail unless they materially change the design.
- Define shared invariants, boundaries, consumption rules and rationale once; source policies add only specific rules and exceptions. Reference shared contracts instead of repeating them or predictable gameplay modifiers.
- Retain established terms unless a replacement resolves a concrete ambiguity.
- Never mention or link to the working document in the design document except in the beginning line.
