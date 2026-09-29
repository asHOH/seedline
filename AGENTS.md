# Agent style

- Be critical of technical claims; verify them against code or reference evidence before relying on them.
- Treat `sts2-decompiled/` and `catfood-decompiled/` as reference-only; never modify their contents.

# Design documentation

- Keep documentation concise.
- Use established terms.
- `DESIGN.md` owns settled policies and reusable reference facts; `WORKING.md` holds unresolved work and supporting evidence. Follow their reading conventions. On settlement, update the design and remove resolved discussion from working, retaining at most a one-line pointer.
- Organize `WORKING.md` by decision unit: split work only when choices can settle independently or require materially different evidence; keep undeveloped backlog compact.
- Catfood is the main existing competitor of this mod.
- Compress duplicated mechanics; exact mechanics belong in detailed appendix sections (vanilla by default, Catfood explicitly labeled).
- Order design as goals and concepts → shared strategies → settled policies → references and appendix.
- Define shared invariants, boundaries, consumption rules and rationale once; source policies add only specific rules and exceptions. Reference shared contracts instead of repeating them or predictable gameplay modifiers.
- Note edge cases briefly. Do not expand them into defensive caveats unless they materially change the design.
- Never mention or link to `WORKING.md` in `DESIGN.md` except in the beginning line.
