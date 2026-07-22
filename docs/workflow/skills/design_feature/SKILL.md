---
name: design_feature
description: Structured planning workflow for a new feature — confirms scope, explores the codebase, resolves edge cases in batches, evaluates approaches and external libraries, and writes a phased implementation plan to plans/<feature-name>.md for implement_phase to consume. Planning only; writes no code. Use when the user wants to design, plan, spec, or scope a new feature before implementation begins. Pass `prototype` (or `-p`) as the first argument for a fast, low-ceremony pass suited to rapid prototyping — prototype mode writes a playable milestone set to milestones/<Feature-Name>/ for implement_milestone instead of a phase plan.
user-invocable: true
argument-hint: "[prototype] [feature description]"
---

# design_feature

Structured planning workflow for new game features. Produces a detailed implementation plan before any code is written.

**Behavior:** Do not write or edit any code files during this skill. Only research (read, search, explore) and plan. Save the final plan to `plans/<feature-name>.md` — **except in prototype mode, which writes a milestone set to `milestones/<Feature-Name>/` instead** (see *Modes* below). (The one exception to the no-code rule: in prototype mode, after the user has approved the plan and explicitly asked to build it — see *Handoff — prototype mode*.)

**Input:** A feature description, provided as an argument or conversationally after invocation.

---

## Modes

**Full (default).** Run every step as written.

**Prototype.** Active when the invocation starts with `prototype`, `--prototype`, or `-p`, or when the user asks for prototype/fast/rough mode in conversation. The rest of the argument is the feature description. Built for rapid iteration where the cost of a wrong guess is a replay, not a production incident.

In prototype mode the goal is **a plan the user can start building in a few minutes**, not an airtight spec. Apply these deltas:

| Step | Prototype behavior |
|---|---|
| 1 — Restate | **Keep.** Short — a few lines, not a spec. Confirm before moving on. |
| 2 — Explore | **Keep, time-boxed.** Enough to find integration points and existing patterns to match. Don't map the whole codebase. |
| 3 — Clarifying questions | **Keep, trimmed.** One round, only questions whose answers actually change the build. Pick a sensible default and state it rather than asking about anything you could reasonably decide yourself. |
| 4 — Edge cases | **Big and obvious only.** One batch, ~3 max — the ones that would break the core loop or make the feature unplayable. No exhaustive failure-state sweep. Skip entirely if nothing meaningful surfaces, and say so. |
| 5A/5C — Approaches | **Keep, compressed.** Two approaches max, a few lines each, one recommendation. Judge on "does it work and match how this project already does things," not on abstraction quality. |
| 5B — Library evaluation | **Skip** unless the feature obviously needs a third-party dependency (physics, pathfinding, a file format). If it does, run the reliability + security checks as written — a bad dependency is expensive at any speed. |
| 6 — Plan doc | **Write a MILESTONE SET, not a phase plan** — `milestones/<Feature-Name>/`, consumed by `implement_milestone`. See the prototype output format below. |
| 7 — Debug & dev tooling | **Skip entirely.** Do not propose debug menus, diagnostics, or inspectors. If the user asks for one later, it's a feature. |
| 8 — Architecture documentation | **Skip entirely.** |
| 9 — Test cases | **Replace with a short "How to verify by playing"** — a handful of concrete things to do and see. No test-case tables, no internal iteration pass. |
| Handoff | **Offer to implement milestone 1 immediately** in the current session — see the prototype handoff below. |

**Architecture standard in prototype mode:** match what the project already does. Read the project `CLAUDE.md` and the surrounding code, and follow their conventions — a project's own hard rules (layer boundaries, event contracts, data-driven config) still apply and are never relaxed by this mode. What relaxes is *added* rigor: no new abstractions, no future-proofing, no generalizing for a second caller that doesn't exist yet.

---

## Step 1 — Restate for Confirmation

Restate the feature as understood — scope, intent, and expected behavior. Present to user and ask for confirmation. If corrections are given, integrate and restate until confirmed.

---

## Step 2 — Explore Existing Codebase

Before asking clarifying questions, explore the codebase to understand:
- Integration points and relevant systems
- Reusable code and patterns already in place
- Architectural constraints

This informs better questions in the next step and prevents redundant design.

---

## Step 3 — Clarifying Questions

Ask questions to resolve ambiguity and fully define requirements. Areas to explore:
- UX expectations and behavior details
- Systems impacted and dependencies
- Constraints and data requirements
- Configurability needs

Allow natural back-and-forth. Continue until requirements are clear.

---

## Step 4 — Edge Case Analysis

Identify meaningful edge cases across UX, system conflicts, data integrity, performance, and failure states.

For each edge case, propose a recommended solution with brief alternatives noted.

**Presentation:** Group related edge cases (3-5 per batch). Present each batch with recommended solutions and ask for user input. If a resolution introduces new edge cases, add them to the next batch. Continue until all are resolved.

---

## Step 5 — Implementation Strategy

### 5A — Draft Approaches

Explore multiple viable implementation approaches. Evaluate each against:
- Does it fulfill all requirements?
- Is it modular, testable, and config-driven?
- Does it follow DRY/SOLID without overengineering?
- Are there performance or scalability concerns?
- Does it introduce unnecessary technical debt?

### 5B — External Library Evaluation

*Prototype mode: skipped unless the feature obviously needs a third-party dependency.*

Before presenting approaches to the user, consider whether an external library could meaningfully help implement this feature — avoiding reinventing the wheel for well-solved problems.

**Decision gate:**
- If the feature's needs are straightforward or well-served by existing codebase patterns, note to the user that external libraries were considered and briefly explain why none are needed. Then proceed to 5C.
- If a library could add real value, continue with the evaluation below.

**Candidate research (web search required):**
Search the web for libraries that address the feature's core needs. Only consider candidates that meet **all** of the following reliability criteria:
- Well-documented with clear API references
- Widely used with strong community support
- High GitHub stars and active contributors/branches
- Positively reviewed across multiple sources
- **Not** brand-new or untested — must have a proven track record

**Security & issues check (mandatory for every candidate):**
For each candidate that passes the reliability filter, do additional web research specifically for:
- Bad reviews citing security issues
- Known vulnerabilities (CVEs, security advisories)
- Sources discussing bugs, stability problems, or maintenance concerns
- Any red flags in issue trackers or community discussions

This security/issues check is **not optional** — candidates that have known security issues or unresolved vulnerability reports must be disqualified or have those risks prominently noted.

**If viable candidates exist:** Weigh pros and cons of each, then recommend one based on:
- A) How well it fits the feature's specific needs
- B) The library's reliability and security posture (per criteria above)

Incorporate the recommended library into an implementation approach — either modifying an existing candidate from 5A or creating a new approach built around the library.

**If no candidates pass the quality/security bar:** Note this to the user and proceed without a library recommendation.

### 5C — Present Recommendation

Provide a concise summary of approaches considered (including any library evaluation from 5B), pros/cons, the recommended solution, and why. Ask user to confirm or choose an alternative.

---

## Step 6 — Final Implementation Plan

**Prototype mode does NOT use the structure below** — it writes a milestone set instead. Skip to
*Prototype output* at the end of this step. Full mode uses the structure below.

Write the plan to `plans/<feature-name>.md` using this structure:

```markdown
# <Feature Name> — Implementation Plan

## Implementation Status
<!-- Canonical phase state — implement_phase reads and updates this ledger. One row per phase, all ⬜ TODO at plan creation. -->
| Phase | State | Commit | Notes |
|---|---|---|---|
| 1 — <title> | ⬜ TODO | — | |
| 2 — <title> | ⬜ TODO | — | |

## Overview
Brief description of the feature and its purpose.

## Architecture
How it fits into the existing system. New modules, data flow, integration points.

## Data Structures
New types, interfaces, config entries.

## Implementation Phases
Ordered phases with specific deliverables per phase. Each phase should be independently testable.

**User verification checkpoints:** EVERY phase ends with a verification checkpoint — including purely internal phases (utilities, types, tests). At each checkpoint the implementer MUST pause, play the notification sound, present exactly what to verify, and wait for the user to confirm before committing that phase and handing off a fresh-context kickoff prompt for the next. No phase proceeds without the user verifying first. (User-facing phases will have visible behavior to check; internal phases verify via tests/review — but the pause happens either way.)

## Systems Affected
Existing files/modules that need changes and why.

## Config
New constants or tunable values.

## Testing Strategy
What to test, edge cases to cover, how to verify.

## Risks & Open Questions
Anything unresolved or worth monitoring.

## Manual QA Test Cases
<!-- Added during Step 9 -->

## Implementation Complete — QA Checklist
**IMPORTANT: When implementation is finished, the implementer MUST display the Manual QA Test Cases section above to the user as a checklist for verification. Do not skip this step.**
```

Present the plan to the user. Revise if edits are requested.

### Prototype output — a milestone set, not a phase plan

**Prototype mode does not write `plans/<feature-name>.md`.** It writes a milestone set to
`milestones/<Feature-Name>/`, in exactly the format `plan_milestones` produces and
`implement_milestone` consumes:

- `00-summary.md` — the architecture in one page, the milestone table, Production Order, Decisions
  Made, Assumptions, **Gotchas**, Open Items, Deferred, Testing.
- `NN-<slug>.md` per milestone — Demonstrable outcome, Goal, Build This, **Do NOT Build This**,
  Context, Principles, Assets Required, UI Mockups Required, Definition of Done, How to Test.
- `LOG.md` — seeded with a "Before Milestone 01 — context carried in from design" section holding
  everything verified while designing, so a cold start doesn't re-derive it. Context is carried
  forward in this file; do **not** write a copy-paste kickoff prompt.

**Read `plan_milestones`' SKILL.md for the full doc format before writing** — that skill is the
authority on the shape, and the two must not drift.

The prototype deltas still apply to the *process*: a light restatement, a time-boxed explore, one
trimmed question round, big-and-obvious edge cases only, two compressed approaches. What does not
relax is the milestone-doc format itself — a thin `Do NOT Build This` or a missing `Gotchas`
section is how a cold implementer goes wrong.

Milestone rules inherited from `plan_milestones`, non-negotiable even here:
- **Every milestone is playable** — press Play and see the new thing. No infrastructure-only
  milestone; infrastructure rides inside the first milestone that visibly needs it.
- **No forward dependencies and no rework** — milestone N+1 layers on N.
- Keep the count low — 2–4 is typical for a single feature.

Also drop a short design record at `plans/<feature-name>.md` if the design pass produced reasoning
worth keeping (the approach comparison, the rejected options). The milestone docs supersede it
wherever they disagree, and `00-summary.md` should say so.

## How to Verify by Playing
Concrete things to do and what should happen.
```

Present the plan to the user. Revise if edits are requested.

---

## Step 7 — Debug & Dev Tooling

*Prototype mode: skip this entire step — do not propose debug menus or diagnostics.*

### 7A — Audit Existing Dev Tools

Explore the codebase for any existing debug menus, dev tools, or developer-only UI (look for `import.meta.env.DEV` guards, debug panels, dev recipes, etc.).

If no debug infrastructure exists, offer to create a debug menu that is **never shown in production builds** (gated behind `import.meta.env.DEV` or equivalent build-time flag). If the user agrees, creating the debug menu infrastructure becomes **Phase 0** of the implementation plan (before any feature work). If the user declines, skip the rest of Step 7 only — Step 8 (Architecture Documentation) still runs, and Step 9 falls back to manual QA test cases.

### 7B — Propose Debug Tools for This Feature

Suggest new debug tools that would allow a designer to test and debug this feature efficiently. Aim for the minimum set needed — the debug menu should not be massive, just enough to:
- Simulate the scenarios the feature introduces
- Inspect internal state relevant to the feature
- Reset feature-specific state without a full account wipe

### 7C — Iterate on Suggestions

Present the proposed debug tools to the user. Iterate at least once on the initial suggestions — ask if any are unnecessary, if any are missing, or if any should work differently. Refine until the user agrees on the final set.

### 7D — Integrate into Build Plan

Once agreed, add the debug tools to the implementation plan at sensible locations — a debug tool should be available as soon as its corresponding functionality is implemented (e.g., a "view snapshot" tool ships with the snapshot service, not at the end).

Update the plan file `plans/<feature-name>.md` to include the debug tooling within the relevant phases.

---

## Step 8 — Architecture Documentation

*Prototype mode: skip this entire step.*

Decide whether the feature warrants an architecture doc or an update to an existing one.

**Trigger conditions** — recommend a new doc when the feature introduces any of:
- A new top-level system folder under `Assets/Scripts/<NewSystem>/` with multiple collaborating files
- A non-trivial state machine, lifecycle, or sequencing contract that future readers won't recover from a single file
- A new pub-sub event surface (a folder of events emitted by the system)
- A persistence or save-system integration

**Trigger conditions for updating an existing doc** — when the feature changes:
- The state machine, sub-states, or transitions of a documented system
- The contracts (interfaces, event payloads, lifecycle hooks) of a documented system
- Strict rules / anti-patterns that the existing doc enumerates

**Action:**
- If a new doc is warranted, add a final implementation phase to the plan: "Run the `document_system` skill against `Assets/Scripts/<System>/` and save to `Assets/Scripts/<System>/<System>_Architecture.md`." Include the stale-detection footer convention (see `Assets/Scripts/GameState/GameState_Architecture.md` for the format).
- If an existing doc needs updates, add the doc path to the plan's **Systems Affected** section so the implementer touches it as part of the feature.
- If neither applies, note that briefly to the user and proceed.

Present the recommendation to the user before continuing to Step 9.

---

## Step 9 — Test Cases

*Prototype mode: replace this step with the "How to verify by playing" list in the prototype plan template — a handful of concrete things to do and see. No test-case tables, no internal iteration pass.*

Write a concrete set of test cases that use the debug tools to verify the feature works correctly and to help find bugs. If no debug tools were agreed in Step 7, write manual QA test cases using normal app flows instead. Each test case should include:
- **What to do** (step-by-step using debug tools)
- **What to expect** (expected outcome)
- **What it tests** (which requirement or edge case)

**Internal iteration:** Before presenting to the user, review the test cases yourself. Check for:
- Gaps: are all edge cases from Step 4 covered?
- Redundancy: are any test cases testing the same thing?
- Clarity: could a designer follow these steps without ambiguity?
Revise internally, then present the refined set to the user.

Present the test cases to the user for approval. Allow the user to add, remove, or modify test cases.

Once approved, add the test cases to the implementation plan with instructions to display them to the user once implementation is complete (as a QA checklist).

---

### Handoff to Implementation

Once the user confirms the full plan (implementation + debug tools + test cases):
1. Verify the plan file is saved to `plans/<feature-name>.md`.
2. Tell the user the plan is complete and offer: "Ready to clear context and start implementing from the plan?"
3. If yes — the user will `/clear`, then invoke `implement_phase` in the fresh session (bare to pick up the most recent plan, or with the plan path). Each phase runs via `implement_phase`, one phase per session.

### Handoff — prototype mode

Once the user confirms the plan:
1. Verify the milestone set is saved to `milestones/<Feature-Name>/` (`00-summary.md`, one doc per
   milestone, `LOG.md`).
2. Play the notification sound.
3. Offer to start immediately: **"Want me to build Milestone 1 now, or clear context and run `implement_milestone`?"**
4. **If the user says build it now:** stop being a planning skill and implement Milestone 1 directly in this session — the no-code rule at the top of this file is lifted at this point, and only for prototype mode. Build only Milestone 1, then stop and let the user play it. Do not roll into Milestone 2 unprompted.
5. **If the user prefers a fresh context:** hand off to `implement_milestone`. It starts bare — it picks up the most recent milestone set and reads `LOG.md` for context. Do not write a kickoff prompt.

Keep the offer to one line. Don't re-summarize the plan the user just approved.
