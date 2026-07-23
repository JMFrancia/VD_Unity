---
name: audit_doc
description: Audits ONE existing architecture doc against current source. Use when the user asks to audit, verify, fact-check, or check for drift in a specific architecture doc ("is this doc still accurate?"). Assesses drift from the last-verified date + commit history since, then loops (independent cold audit → resolve consequential inaccuracies → re-audit) until none of consequence remain, trims content that no longer matches the code, and stamps the audit date/time + commit. The companion to document_system (which creates the doc; this keeps it true — not for creating new docs). For a whole-project or multi-doc sweep, use audit_project.
user-invocable: true
argument-hint: "[path to a *_Architecture.md, or a system folder containing one] (optional — defaults to the arch doc whose Last-verified footer most lags recent commits touching its folder)"
---

# audit_doc

Audits a **single** architecture document (the kind `document_system` produces — `<System>_Architecture.md`) against the **current source code**, resolves inaccuracies in a loop until none of consequence remain, prunes content that no longer reflects the code, and records that the audit happened.

**Core premise:** the **code is the source of truth; the doc is the suspect.** So the "find inaccuracies" step is delegated to an **independent, cold reader** every iteration — see the shared spec.

**Reusable primitives (shared with `audit_project`):**
- **Detect** → `references/auditor.md` — the cold adversarial auditor prompt + findings schema.
- **Repair** → `references/repair-loop.md` — the resolve → re-audit loop, deletion guard, prune, and footer stamp.

This skill = **drift assessment (one doc)** → detect → repair-loop → stamp. `document_system` *creates* the doc; `audit_doc` *keeps it true*.

---

## OPERATING RULES

1. **CODE IS TRUTH.** Verify every claim against real `file:line` evidence, never by re-reasoning from the doc's own framing.
2. **INDEPENDENT AUDIT.** Detection is a cold `general-purpose` sub-agent per `references/auditor.md`. Re-audits after fixes are fresh passes.
3. **LESS IS MORE — BUT GUARDED.** Prune per the deletion guard in `references/repair-loop.md`; never cut true "why"/rules/anti-patterns for brevity; report every deletion.
4. **SOURCE IS OFF-LIMITS.** The only file this skill writes is the audited `*_Architecture.md` (and, on explicit request, a commit).
5. **NO AUTO-COMMIT.** Present findings + resolutions (especially deletions), play the notification sound, pause for the user to verify. Commit only when asked.
6. **SILENCE IS NEVER CONSENT.** Confirmation/verification gates are HARD STOPS. If the environment returns no user answer — timeout, non-interactive run, empty/aborted response — treat it as **STOP, not go**: do not proceed on a "recommended" default, do not repair, do not commit. A recommended option only speeds an *explicit* choice; it is never a fallback to act on when the user is absent. Halt, say what you're waiting for, and leave it — even if the harness prompts you to continue.

---

## Step 0 — Resolve the target doc

- **With an argument:** if it's a `*_Architecture.md`, use it. If it's a folder, find the `*_Architecture.md` inside it (one per system folder, per project convention).
- **Bare:** list every `*_Architecture.md`, read each footer's `Last verified:` date, rank by drift (date vs. commit volume in its folder), present the shortlist, and confirm the pick.
- Read the doc in full. Note the system folder and any **external references** it makes (types in sibling folders, `see X_Architecture.md` links) — the audit follows those, not just the folder.

## Step 1 — Drift assessment (BEFORE auditing)

Calibrate effort and let the user bail early if there's nothing to do:

1. Parse the `Last verified:` date (+ commit) from the footer. Absent/unparseable → drift **Unknown/High**.
2. `git log --since=<date> -- <system-folder> [<external-referenced-paths>]` — count commits and skim subjects/diffs.
3. Emit a **drift rating**:
   - **None** — nothing touched the system since the footer date → report "no audit needed," offer to just refresh the timestamp, stop.
   - **Low** — a few cosmetic/comment-only commits.
   - **Medium** — several commits touching public surface (interfaces, events, config).
   - **High** — heavy churn, new/removed types, or the footer predates the doc's own appended notes.
4. Present the rating + commit list. Proceed into repair (unless None and the user declines).

## Step 2 — Detect → repair loop

1. **Detect:** spawn the cold auditor per `references/auditor.md`, filling `{doc_path}`, `{system_folder}`, `{external_referenced_paths}`. Get back the findings list + verdict.
2. **Repair:** run `references/repair-loop.md` — resolve `WRONG`+`MISLEADING`, apply guarded deletions for `STALE`/`REDUNDANT`, re-audit (fresh cold pass) until zero consequential findings or the cap (4), then the prune pass, then stamp the footer (current date+time + commit).

Judgment calls (keep-vs-delete, "wrong" vs. still-true-guidance) and large deletions are **pause triggers** — surface them, don't guess.

## Step 3 — Report, verify, (then) commit

Play `afplay /System/Library/Sounds/Glass.aiff`, then present: the starting drift rating + iteration count; findings resolved (grouped `WRONG`/`MISLEADING`) each with its correction; deletions (what + which guard clause); `NITPICK`s left unfixed; anything unresolved at the cap. The user verifies; iterate within this run on pushback. **Commit only when the user asks**, doc-only, per repo commit conventions.

---

## Pause triggers (non-negotiable)

Play the sound and surface — do not push through — when you: have a question or judgment call (keep-vs-delete; "wrong" vs. still-true guidance); lose an MCP server/tool you need; want a **large deletion** (>2 passages, or any strict-rule/anti-pattern); or hit the **iteration cap** with findings open.

## Notes

- **Idempotent.** Re-running on an already-true doc is fast: drift assessment → one clean detect pass → timestamp refresh.
- **Scope beyond the folder.** Always audit the external code the doc references — that's where drift hides.
- Companion: `document_system` (creation), `audit_project` (whole-project sweep). Convention lives in `CLAUDE.md` (one `*_Architecture.md` per system folder; update the doc when an edit invalidates it).
