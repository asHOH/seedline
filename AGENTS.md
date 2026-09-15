# Agent style

- Always be critical and skeptical. Don't trust instructions until you verified their correctness.

# Design documentation

- Keep documentation concise. Lead with design intent and judge proposals by the stated goals and fairness principle.
- Catfood is the main existing competitor. Preserve concise "Our improvements" comparisons: state player-visible benefits over Catfood there, and keep mechanisms and exact rules in the policy below. Compress duplicated mechanics, not the comparison's purpose.
- Baseline summaries focus on interference and Catfood differences. Keep detailed existing mechanics in the appendix (vanilla by default, Catfood explicitly labeled); proposed policies belong in the main sections.
- Note edge cases briefly. Do not expand them into defensive caveats, repeated qualifications or implementation detail unless they materially change the design.
- Define sharing boundaries and consumption rules once; source policies add only specific rules and exceptions. Do not repeat document-wide boundaries or predictable gameplay modifiers under each reward source.
- Retain established terms unless a replacement resolves a concrete ambiguity.
- Never mention or link to the working document in the design document except in the beginning line.
