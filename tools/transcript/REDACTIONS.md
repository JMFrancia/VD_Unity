# What was redacted from the session log, and why

The prompt log in [`docs/ai-transcript/prompts/`](../../docs/ai-transcript/prompts/) is verbatim except where marked `[... redacted]`. This file is the public record of what those markers cover.

The rules themselves live in `tools/transcript/redactions.json`, which is **gitignored on purpose**: a redaction pattern necessarily contains the string it matches, so committing the rules would republish the exact content they remove. This document describes each rule instead.

## What was removed

| Category | Instances | What it covers |
|---|---|---|
| Personal aside | 3 | Two passages written during a low moment early in the project, and one health-related remark. Personal, not project content. The technical argument surrounding them — whether to build an intermediate prototype first — is kept in full. |
| Contact detail | 1 | A personal email address. Publishing it in a public repo invites spam and credential-stuffing. |
| Access-bearing key | 1 | A Figma file key, which grants access to any link-shared file. |
| Proprietary tooling | 2 | A filesystem path to, and a name reference for, separate asset-generation tooling that is deliberately not part of this repo. |
| Profanity | 4 | Softened or elided. In each case the surrounding technical point is preserved — one of them is among the sharpest architectural calls in the log. |

## What was *not* touched

- **No technical content was altered, softened, or added after the fact.** Every architectural argument, correction, complaint about generated output, and change of mind appears as written, typos included.
- **No prompt was removed for making me look bad.** Blunt art direction, frustration with broken tooling, and reversals of my own earlier instructions are all still there — they're the most useful part of the record.
- **Nothing was rewritten silently.** Every removal leaves a visible `[... redacted]` marker.

## Sessions excluded

Two kinds of session are dropped by the generator rather than redacted:

- **Forked duplicates.** Resuming or rewinding a conversation writes a second transcript replaying the same prompts. Two such forks were collapsed, keeping whichever went furthest.
- **The sessions that produced this artifact.** The session that designed the log and its redaction pass, and the later session that refreshed the README and regenerated the log for a whole new day of work — both are self-referential, so publishing them inside the result would be circular.

## Verifying

`build_transcript.py` refuses to run if the rules file is absent, so the log cannot be regenerated unredacted by accident. To confirm the rules did what this document claims, run the generator and grep the output for the redaction marker.
