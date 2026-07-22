---
name: plan_milestones
description: Decomposes a finished spec into ordered, playable milestones for rapid prototyping — groups features into the smallest playable artifact plus layers, audits coverage with independent cold agents, iterates with the user, then writes one doc per milestone plus a summary into milestones/<spec-name>/. Planning only; writes no code. Use when the user has a spec and wants it broken into buildable phases. For designing a feature from scratch instead, use design_feature; to implement the milestones this produces, use implement_milestone.
user-invocable: true
argument-hint: "[path to spec doc]"
---

# plan_milestones

Turns a finished spec into an ordered set of **playable milestones**, each with its own doc, plus a summary that indexes them.

**Behavior:** Write no code. Read, plan, audit, and write docs only.

**Input:** A path to a spec document, as an argument or given conversationally. If none is provided, ask for one. If the spec is obviously unfinished (large TBDs, unresolved contradictions), say so and stop — this skill decomposes finished specs; iterate on the spec first.

**Output:** A directory `milestones/<spec-name>/` containing `00-summary.md` and one doc per milestone.

---

## The Core Principle

**Smallest playable artifact first, then layer over layer.**

Every milestone must be *playable* — open the browser, do a thing, see it work. Not "the code compiles," not "the system is wired up." If a milestone cannot be demonstrated in a browser by a person who didn't write it, it is not a milestone.

This has one hard consequence: **there is no infrastructure-only milestone.** No "set up the data loader and event bus" phase. Infrastructure rides along inside the first milestone that needs it, and that milestone is defined by the visible thing it delivers.

The second constraint is **no rework.** Milestone N+1 layers on top of N; it does not require going back and rebuilding N. If a later milestone forces an earlier one to be torn up, the cut is in the wrong place.

---

## Step 1 — Read the Spec in Full

Read the entire spec. Also read `CLAUDE.md` (project and user level) — the project's architecture rules constrain every milestone, and its definition of "done" governs this skill.

Then read the catalogs, if they exist:
- **`docs/assets/`** — the authority for asset ids
- **`docs/UI-Inventory.md`** — the authority for UI surface ids

**If they don't exist, proceed and warn.** Describe assets and surfaces inline instead, and note in the summary that ids are unreconciled. Re-cutting milestones shouldn't require re-running the art pipeline — but ids that were invented here will need reconciling later.

Note explicitly:
- Anything the spec marks **deferred** or **out of scope** — these must not appear in any milestone.
- Anything the spec marks **open, proposed, or unresolved** — these need a decision before a milestone can depend on them. Surface them in Step 4.
- The spec's **cross-cutting spine**: its event catalog, data schemas, and any shared system every milestone touches.

Do not skim. Every later step depends on holding the whole spec at once.

---

## Step 2 — Draft the Milestone Cut

One pass, done by you, holding the whole spec. Do not delegate this — deciding the cut lines requires the whole picture in one mind.

For each milestone, draft:
- A one-line **name** and the **playable outcome** ("you can place a station and watch it run a timer")
- Which spec sections it covers
- What it explicitly defers to a later milestone
- What it adds to the spine (new events, new data files/fields)

Ordering rules:
1. **Milestone 1 is the smallest thing a person can look at and interact with.** Usually: data loads, something renders, one input does something.
2. Each milestone depends only on milestones before it. **No forward dependencies.**
3. Prefer a **vertical slice** (thin path through every layer) over a horizontal one (a whole layer with no visible result).
4. When a shared system is load-bearing for many milestones (an effect resolver, an event bus, a data loader), build it in the first milestone that *visibly needs it*, sized to that need. Later milestones extend it.
5. Cut where the **fun is testable earliest**. The user should be able to feel a wrong turn as early as possible — that's the entire point.

Keep the count honest. Too few and they aren't playable; too many and each is a rounding error. If a milestone's playable outcome is hard to state in one sentence, it's probably two milestones.

---

## Step 3 — Independent Coverage Audit

**This audit MUST run as subagents that have not seen your reasoning.** An auditor that watched you decide will ratify your decisions. Give them only the spec and the milestone list — never your rationale.

Spawn **two agents in parallel** (one message, two tool calls):

**Agent A — Coverage.** Given the spec and the milestone list, find everything in the spec that lands in *no* milestone. Also find anything appearing in *two* milestones, and anything present in a milestone that the spec marks deferred. Report orphans ranked by importance.

**Agent B — Playability & Ordering.** Given the spec and the milestone list, check each milestone against three tests: (1) Is its outcome demonstrable in a browser by someone who didn't build it, or is it secretly infrastructure? (2) Does it depend on anything built in a *later* milestone? (3) Would it force rework of an earlier milestone?

Both agents are read-only and return findings, not edits.

Fold real findings into the cut. Do not accept a finding you believe is wrong — say why in Step 4 instead.

---

## Step 4 — Present to the User, and Iterate

Show the user:
- The ordered milestone list, each with its one-line playable outcome
- What the audits found, and what you changed
- Any spec items marked **open/proposed/unresolved** that a milestone now depends on — these need a decision
- Your honest read on what's achievable in the time the user has

Ask for feedback. **Loop back through Steps 2 → 3 → 4 until the user is satisfied.** Re-run the audit on every material change to the cut; a re-cut invalidates the previous audit.

Do not proceed to Step 5 without explicit approval of the milestone list.

---

## Step 5 — Write the Milestone Docs

Write them **in order, one at a time.** Order matters: a milestone's "Do NOT build" section requires knowing what the later milestones contain, and its "Context" requires knowing what the earlier ones left behind.

Create `milestones/<spec-name>/` and write `NN-<slug>.md` for each.

**If you hit a genuine gap — something the spec does not answer and you'd have to invent — STOP and ask the user.** Do not invent it and flag it later. Do not guess and move on. Batch questions if several arise, ask, then continue. Inventing an answer is how a spec's authority quietly transfers to whoever wrote the milestone doc.

Keep a running list as you write: **decisions made, assumptions taken, gotchas noticed, deviations from the spec.** This feeds Step 6.

### Milestone doc format

```markdown
# Milestone NN — <Name>

**Playable outcome:** <one sentence: what the user can do in the browser when this is done>

## Goal
What this milestone delivers and why it comes here in the order.

## Build This
Specific, scoped list. Cite spec sections (§N) rather than restating them — the spec is the source of truth.

## Do NOT Build This
Named list of things a reasonable agent would be tempted to build, each with the milestone that owns it.
Include anything the spec marks deferred that is adjacent to this work.
This section prevents scope creep; be specific, not general.

## Context
What already exists from prior milestones. What this adds to the spine:
- **Events added:** <names + payloads>
- **Data files/fields added:** <files, keys>
- **Systems touched:** <paths>

## Principles
Project-specific rules that bite in this milestone. Reference CLAUDE.md rather than restating it.
Call out the Phaser 4 skill docs relevant here (per CLAUDE.md, read them before writing API code).

## Assets Required
Cite asset ids from `docs/assets/` — do not invent descriptions.
Each marked `[placeholder OK]` or `[needs real asset]`. Placeholder art is correct until proven otherwise.

## UI Mockups Required
Cite surface ids from `docs/UI-Inventory.md`.
Each marked `[mockup needed]` or `[placeholder layout OK]`, or `none`.

## Definition of Done
Concrete and observable. Not "the system works" — "you can X and see Y."

## How to Test
Numbered steps the user follows in the browser. Written for someone who did not build it.
```

---

## Step 6 — Write the Summary

Write `milestones/<spec-name>/00-summary.md`:

```markdown
# <Spec Name> — Milestone Plan

**Spec:** <path>
**Generated:** <date>

## Milestones
| # | Name | Playable outcome | Doc |
|---|---|---|---|

## Production Order
The scheduling view — what art and mockups are needed, and when. This is the section you hand to whoever is making assets.

Derived from the milestone docs. The catalogs (`docs/assets/`, `docs/UI-Inventory.md`) remain the authority for what each id *is*; this table only says when it's wanted.

| Milestone | Asset ids | UI mockups | Notes |
|---|---|---|---|

Flag anything genuinely on the critical path — an asset or mockup a milestone cannot proceed without a placeholder for. That list should be short; if it isn't, something is over-specified.

## Decisions Made
Decisions taken while decomposing that the spec did not dictate, each with its reason.

## Assumptions
Things assumed true and not verified. Each is a risk; say what breaks if it's wrong.

## Gotchas
Traps a future implementer will hit. Ordering constraints, non-obvious couplings, easy-to-miss rules.

## Open Items
Anything still unresolved, and which milestone it blocks.

## Deferred
From the spec, restated so nobody builds it early.
```

Then tell the user the directory is ready and stop. Play the notification sound.

---

## Rules

- **Write no code.** This skill plans.
- **Never invent a spec answer.** Ask (Step 5).
- **The audit is cold or it is worthless.** Never let an agent that saw your reasoning audit it.
- **Every milestone is playable.** No infrastructure-only phases.
- **Cite the spec, don't restate it.** Duplicated spec text drifts from the source. Reference §N.
- **Cite asset and UI ids, don't rename them.** The catalogs are the naming authority; this plan schedules them. References point one way — the catalogs never mention milestones — so the two cannot drift apart.
- **Deferred stays deferred.** If a milestone needs something the spec deferred, that's a spec problem — raise it, don't quietly build it.
