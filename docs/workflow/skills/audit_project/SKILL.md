---
name: audit_project
description: Whole-project architecture-doc audit. Use when the user wants to audit all or multiple architecture docs, or asks for a doc audit without naming a specific one; for a single named doc, use audit_doc instead. Fans out an independent cold auditor across EVERY *_Architecture.md in the project (in parallel), returns one combined inaccuracy list sorted by severity and grouped by doc, then — on approval — runs the same repair loop across all selected docs. The multi-doc orchestrator built on the same detect + repair primitives as audit_doc.
user-invocable: true
argument-hint: "[optional root path or glob to scope which *_Architecture.md docs are swept; defaults to all under the repo]"
---

# audit_project

Audits **every** architecture document in the project against current source, produces **one severity-ranked combined findings list**, and then (on approval) repairs across all selected docs in a loop. It's the fan-out sibling of `audit_doc`: same two primitives, applied to N docs.

- **Detect** → `../audit_doc/references/auditor.md` (shared cold-auditor prompt + findings schema).
- **Repair** → `../audit_doc/references/repair-loop.md` (per-doc resolve → re-audit loop, deletion guard, prune, stamp).

The value over running `audit_doc` N times: **detection fans out in parallel**, and you get a **global triage view** (all `WRONG`s across all docs, ranked) *before* any file is touched — so you fix what matters project-wide and skip noise.

---

## OPERATING RULES

Inherits all of `audit_doc`'s rules — **code is truth**, **independent cold audit**, **less-is-more but guarded** (report every deletion), **source off-limits**, **no auto-commit**, **silence is never consent**. Two additions for the multi-doc shape:

1. **DETECT-ALL BEFORE REPAIR-ANY.** Complete the full fan-out and present the combined list before editing a single doc. No repair happens until the user approves the triage.
2. **NO SILENT CAPS.** If the sweep bounds coverage for any reason (doc skipped, sub-agent died, sampling), say so explicitly in the report — a partial sweep must never read as "all clean."
3. **SILENCE IS NEVER CONSENT.** Every confirmation/triage gate below is a HARD STOP. If the environment returns no user answer — a timeout, a non-interactive run, an empty/aborted response — treat it as **STOP, not go**: do NOT fan out detection, do NOT repair, do NOT "proceed with the recommended option." A recommended default exists only to make an *explicit* choice faster; it is never a fallback to act on when the user is absent. Halt, state what you're waiting for, and leave the work until the user returns. This applies even if the harness prompts you to continue.

---

## Step 0 — Enumerate & pre-rank

- Find every `*_Architecture.md` under the repo (scoped by the argument if given).
- For each, read the `Last verified:` footer date and `git log --since=<date> -- <its-folder>` to get a **drift rating** (None/Low/Medium/High), exactly as `audit_doc` Step 1.
- Present the inventory as a table (doc · last-verified · commits-since · drift), highest-drift first. **Confirm the set to sweep — HARD STOP** (the user may drop None/Low docs to save tokens). No detection fans out until the user answers; if no answer comes back (timeout / non-interactive), halt per Operating Rule 3 rather than defaulting to a "recommended" set.

## Step 1 — Fan-out detection (parallel, read-only)

- For each doc in the confirmed set, spawn a **cold `general-purpose` sub-agent** per `../audit_doc/references/auditor.md` (fill `{doc_path}`, `{system_folder}`, `{external_referenced_paths}`). **Launch them in parallel** (multiple Agent tool calls in one message) — detection is independent and read-only, so it parallelizes cleanly.
- Each auditor returns its findings list + verdict. Tag every finding with its `doc`.

> **Scale option (opt-in):** for a large sweep (many docs), this fan-out + aggregation is a natural **Workflow** (`parallel()` detect → aggregate → `pipeline()` repair). Only use the Workflow tool if the user has explicitly opted into multi-agent orchestration; otherwise use parallel Agent calls as above.

## Step 2 — Aggregate & present the combined list

- Merge all findings into **one list, sorted by severity** (`WRONG` → `MISLEADING` → `STALE`/`REDUNDANT` → `NITPICK`), grouped by doc within each severity band (or a per-doc rollup with counts — whichever reads clearest for the volume).
- Lead with a **summary**: per-doc consequential counts, project total, and which docs are clean.
- Play `afplay /System/Library/Sounds/Glass.aiff` and present. **HARD STOP for the user to triage** — they choose which docs / severities to repair (e.g. "all WRONGs everywhere," "everything in DesignMode," "skip nitpicks"). No repair begins until they answer; if no answer comes back, halt per Operating Rule 3 (never auto-repair on a default). Detection findings are read-only and safe to leave sitting.

## Step 3 — Repair across selected docs

For each doc the user selected, run `../audit_doc/references/repair-loop.md` (guarded deletions, re-audit until converged or cap 4, prune, stamp footer) **with the loop's "consequential set" narrowed to the severities the user approved in triage** (default: `WRONG`+`MISLEADING`). Convergence = a fresh audit pass returns zero findings *in the approved set*; findings outside it are logged in the final report, not fixed and not loop-blocking — otherwise a partial-severity approval could never converge and would burn the full iteration cap on every doc. Because detection already ran, feed each doc's existing findings straight into the loop's resolve step; the loop's own re-audit confirms convergence.

Docs in the confirmed sweep whose audit came back with **zero consequential findings** skip the repair loop but still get their `Last verified:` footer refreshed per `repair-loop.md` — a clean audit is still a verification, and a stale footer would mis-rank the doc in the next sweep's pre-rank. Offer this at the triage gate.

- Process docs one at a time (repair writes files; keep it reviewable). Surface judgment calls and large deletions per the guard.
- If any doc hits the anti-thrash cap with findings open, report it and move on rather than blocking the batch.

## Step 4 — Combined report, verify, (then) commit

Play the sound, then present a **project-level report**: per doc — starting drift, iterations to converge, findings resolved (`WRONG`/`MISLEADING`), deletions (what + guard clause), unfixed nitpicks, anything left at the cap; plus any docs skipped and why. The user verifies. **Commit only when asked** — offer either one commit for the whole sweep or per-doc commits, following repo commit conventions.

---

## Pause triggers (non-negotiable)

Same as `audit_doc`, plus: after the combined-list presentation (Step 2) **always** wait for triage before repairing; and if the fan-out loses a sub-agent / can't cover a doc, surface the gap rather than silently narrowing scope.

## Notes

- **Detection is cheap-ish and parallel; repair is the expensive, sequential part.** Triaging first means you only pay repair cost on docs that matter.
- **Same primitives, single source of truth.** Any change to how auditing or repair works goes in `../audit_doc/references/*` and both skills track it — this skill adds only the fan-out + aggregation.
- Companions: `audit_doc` (single doc), `document_system` (creation).
