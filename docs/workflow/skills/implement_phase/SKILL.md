---
name: implement_phase
description: Use when the user wants to implement, continue, or resume a phase of an existing multi-phase plan in plans/ — e.g. "implement phase 2", "continue the <feature> plan", "pick up where we left off". Executes exactly one phase (TDD, user verification, commit, plan back-sync) then stops. Not for designing plans (use design_feature) or ad-hoc coding with no plan doc.
user-invocable: true
argument-hint: "[plan-path-or-feature-name] (optional — defaults to the most recently modified plan in plans/)"
---

# implement_phase

Implements **exactly one phase** of a multi-phase plan (the kind `design_feature` produces in `plans/<feature>.md`), then stops. This skill automates the "Multi-Phase Implementation Workflow" from `~/.claude/CLAUDE.md` for a single phase, with full real-time interactivity and strict verify-before-advance discipline.

**Behavior:** Work strictly to the end of the current phase. Never start the next phase. The plan doc is the single source of truth for phase status, as-built deviations, tech debt, open questions, and handoff notes — there is no separate state file.

**Supersedes global CLAUDE.md:** while this skill is active, do NOT produce the copy-paste "Next-Phase Kickoff Prompt" from the global Multi-Phase workflow — Step 6's plan-persisted handoff notes plus the next session's Step 2 framing replace it. All other global workflow rules (TDD order, verify-before-commit, notification sound) still apply.

**Input:** An optional plan path or feature name as an argument. If omitted, continue the most-recently-modified plan in `plans/`.

**Why one phase per session:** Each phase runs here in the main conversation loop, so it can talk to the user in real time. A fresh session per phase gives genuinely isolated context and survives crashes/compaction. **Never implement multiple phases at once** — plans are rarely bulletproof against contact with implementation, and back-syncing each phase before the next begins is what stops a wrong assumption in an early phase from silently poisoning a later one.

---

## Step 0 — Resolve plan & read context

- If invoked **with an argument** (a plan path or feature name), use that plan. Resolve a bare feature name against `plans/**/*.md` (e.g. `closet-mode` → `plans/closet-mode.md`).
- If invoked **bare**, pick the **most-recently-modified** `plans/**/*.md`.
- Read the plan in full, plus its companion docs: any `meta-plan.md` in the same folder, and any `*_Architecture.md` the plan references.
- Read the **status ledger** at the top of the plan (see Step 0A). If none exists, prepare to bootstrap one in Step 0A.
- Check the project `CLAUDE.md` for system-specific conventions (architecture-doc rules, plans path, event rules).

### Step 0A — Status ledger (the canonical phase state)

Phase status lives in a single table at the top of the plan doc:

```markdown
## Implementation Status
| Phase | State | Commit | Notes |
|---|---|---|---|
| 1 — <title> | ✅ DONE | `<sha>` | <as-built notes, test counts> |
| 2 — <title> | 🟡 IN PROGRESS | — | <branch, started> |
| 3 — <title> | ⬜ TODO | — | |
```

- **If the plan has no `## Implementation Status` table:** parse the plan's phase headings (e.g. `### Phase N — Title`) into a table with every phase set to `⬜ TODO`. Present it to the user and get confirmation before writing it into the plan. This is the only place the two plan styles (status-table vs. inline-as-built) are reconciled.
- **Next phase = the first `⬜ TODO` row**, unless a `🟡 IN PROGRESS` row is resumable (left unfinished by a prior session), in which case resume that one.

---

## Step 1 — Confirm plan + phase (mandatory gate)

Always begin real work by confirming with the user, in plain language:

- **Which plan** is targeted (and how it was resolved — explicit argument vs. most-recently-modified).
- **Which phase** is next, **why** (first `⬜ TODO`, or resuming a `🟡 IN PROGRESS`), and a **one-line summary** of that phase.

**Concurrency warning:** If another phase on the **same git branch** is marked `🟡 IN PROGRESS`, warn the user — it may indicate a concurrent session or a dirty branch. (Simultaneous sessions are supported only across *different* plans; this skill does not auto-branch — branch/worktree management is the user's.)

**Do not proceed until the user confirms.**

---

## Step 2 — Orient via a kickoff framing (do NOT regurgitate the phase)

Re-derive, for this session, the same kind of Next-Phase Kickoff Prompt the manual workflow produces — consuming the **handoff notes** the previous phase persisted (in the status table's Notes column and/or a "Handoff to next phase" note in the plan). Follow the kickoff rules from `~/.claude/CLAUDE.md` exactly:

1. **MUST NOT regurgitate, quote, or otherwise represent the next phase's specifics.** Point at the plan doc (and companion/arch docs) and state exactly which phase to start.
2. **MAY include cross-phase context notes** that aid this phase: prior-phase as-built deviations (and *why*), as-built realities that diverge from the plan, and the user's standing preferences.
3. **Format in Markdown** with LLM-digestion best practices: clear headed sections and tight bullets.

For **phase 1** (no prior handoff exists), synthesize this framing fresh from the plan.

Then mark the phase **`🟡 IN PROGRESS`** in the ledger, recording the current branch.

---

## Step 3 — Implement the phase (TDD)

Follow the test-first workflow from `~/.claude/CLAUDE.md`:

1. **Write tests first** (they should fail initially).
2. **Verify tests fail** (proves the tests are valid).
3. **Implement** the phase to make tests pass.
4. **Run the full suite** — new and existing tests green.

Work strictly to the **end of this phase only**. Do not begin the next phase.

### Pause triggers (non-negotiable)

Whenever any of the following occurs, **pause, play the notification sound (`afplay /System/Library/Sounds/Glass.aiff`), and surface it to the user** — do not push through:

- You **have a question**.
- You **lose an MCP server you need** (e.g. the Unity Editor is offline or needs to be brought into focus).
- You are **verifying an assumption** (state the assumption and how you'd confirm it; wait for confirmation).
- You **want to make a change to the plan** (get consent before deviating).
- You are **adding tech debt** — deferring a decision or feature for later. This requires **explicit user consent**; record the rationale once granted.

---

## Step 4 — Completion report

When the phase is complete, **play `afplay /System/Library/Sounds/Glass.aiff`**, then present a structured report:

- **What was done** — deliverables and files touched.
- **Test / verification results** — the **actual** run output (EditMode/PlayMode counts, pass/fail), so the user verifies against green rather than against prose.
- **Assumptions confirmed** — and how each was confirmed.
- **Decisions made and why.**
- **New tech debt** — only items the user consented to during Step 3, each with its rationale.
- **Manual QA checklist** for this phase — pull from the plan's `Manual QA Test Cases` section if present.

The user verifies. **If they have corrections or questions, iterate within this same phase** — make fixes, re-run checks, re-present the report. **Do not commit or advance until the user verifies.**

---

## Step 5 — Commit (this phase only)

Only **after the user verifies**, commit **this phase's changes only** — never multiple phases together. Follow the repo's commit-message conventions (see recent `git log`).

---

## Step 6 — Back-sync the plan

Reconcile the plan doc with what was actually built:

- **Status ledger:** flip this phase's row to `✅ DONE`; record the **commit SHA**, **test counts**, and a short **as-built note** in the Notes column.
- **Inline as-built:** for any divergence from the plan, write a `> **As-built (Phase N):**` blockquote at the phase, explaining *what* changed and *why* (matching the `plans/closet-mode.md` convention).
- **Append** any new **tech debt**, **open questions / deferred-for-later** items, and **handoff notes for the next phase** into the plan, so the next session's Step 2 can consume them.
- **Architecture docs:** if this phase invalidated anything in a `*_Architecture.md` (state machine, contracts, event payloads, strict rules), update that doc as part of this change (per project `CLAUDE.md`).

---

## Step 7 — Remember & hand off

- The plan doc now records the last completed phase (it is the sole state — nothing else to update).
- Tell the user this phase is **complete and committed**.
- Ask them to **close this session, start a fresh one, and call `implement_phase` again** — bare to continue the most-recent plan, or with the plan argument to target a specific one.

**Do not auto-advance to the next phase.** Stop here.
