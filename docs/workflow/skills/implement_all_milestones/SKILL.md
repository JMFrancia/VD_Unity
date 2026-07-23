---
name: implement_all_milestones
description: Runs a whole milestone plan back-to-back unattended — opens with a pre-flight briefing (scope, time/token estimate, predicted stop points, questions to resolve up front), then one fresh subagent per milestone, each building, self-verifying, committing, and journaling before the next begins. Halts the run on any architectural question and resumes from a half-finished milestone without losing work. Use when the user wants to build a plan_milestones plan straight through rather than one milestone at a time. For a single milestone with the user playing it, use implement_milestone.
user-invocable: true
argument-hint: "[N | N-M] (optional — defaults to every remaining milestone)"
---

# implement_all_milestones

Runs a `plan_milestones` plan **straight through**, one milestone at a time, each in its own fresh subagent.

This is the unattended counterpart to `implement_milestone`. The trade it makes is explicit: **automated verification replaces the user playing each milestone.** Automated verification proves a milestone *works*. It cannot prove it's *fun*. The user gets that back at the end, as a reviewable chain of one-commit-per-milestone they can play and rewind.

**Your job as orchestrator is to stay small.** You dispatch, read a short structured return, update the run file, and dispatch the next. You do **not** read milestone docs, source files, or the spec — that is the subagent's context, not yours. If you find yourself reading `Assets/`, you have already failed at the one thing this skill exists to do.

---

## Step 0 — Resolve the Run

Find the plan: `milestones/<spec>/`. If several exist, use the most recently modified and say which.

Resolve the range from the argument:

- **Bare** — every milestone with no ✅ entry in `LOG.md`.
- **`N`** — milestone N through the last one.
- **`N-M`** — that range, inclusive.

Then check `milestones/<spec>/.run/` for a stale journal (`NN-*.journal.md`). A journal on disk means a previous run **aborted mid-milestone**. That milestone is the resume point — say so, and expect its subagent to continue rather than restart.

---

## Step 1 — Pre-Flight Briefing

Before anything builds, the user gets to see what they're authorising. **Do not read the milestone docs yourself to produce this** — that would blow the context budget the whole skill exists to protect. Dispatch **one scout subagent** that reads the plan and returns a compact briefing.

The scout reads: every in-range milestone doc, the spec sections they cite, `LOG.md`, and the mockup/asset readiness for the surfaces they touch. It returns **only** the briefing below — no milestone summaries, no source.

### The briefing

**1. Scope.** One line per milestone: number, name, its playable outcome.

**2. Cost estimate.** A range with its basis stated, not a number pulled from the air. Derive from what the plan actually contains — count of milestones, systems touched, new prefabs and UI surfaces, assets that need generating, whether Core logic (and therefore tests) is involved. Give:

- **Wall-clock**, as a range
- **Token usage**, as a range, noting the dominant driver
- **Confidence**, and what would move it — a plan whose later milestones are thin sketches deserves a wide range and should say so

Never present an estimate as precise. A confident wrong number is worse than an honest range.

**3. Predicted stop points.** Where this run is *likely* to halt, ranked by likelihood, each tied to its tier-3 category. Common ones: a milestone that introduces the first event of a new kind, an SO schema decision the plan leaves open, a UI surface whose chosen mockup may contradict the milestone doc, a milestone whose *Do NOT Build* list looks like it overlaps the one before it. This is a forecast, not a promise — say so.

**4. Questions to resolve up front — if any.** The highest-value part of the briefing. Any tier-3 question that is **already visible in the docs** should be asked **now**, not four hours in. Ambiguities the plan never resolves, two milestone docs that disagree, a spec section cited by three milestones that reads two ways.

Be strict here: only questions that would genuinely halt a subagent. **If there are none, say "none" and move on.** Manufacturing questions to look thorough turns the one interactive gate into ceremony, and the user chose this skill specifically to avoid that.

**5. The trade, stated plainly.** Each milestone is committed on automated verification, without the user playing it first.

**6. The exclusive-tree warning.** For the duration of the run, this working tree and this Unity Editor belong to the run. Say so explicitly:

- Don't run the art pipeline (`style_guide`, `asset_list`, `ui_inventory`, asset generation) against this tree
- Don't edit and save scenes or prefabs in the editor

Both produce changes the run cannot tell from its own, and a milestone that touches the same file gets its commit halted. The single most likely contaminator is not another agent — it's the user saving a scene mid-run.

### Then gate

Ask for confirmation, and answer any questions raised in (4) before dispatching milestone 1 — those answers pass through to the subagents that need them.

**This is the only interactive gate in the whole run.** Everything after it is unattended until something halts.

---

## Step 2 — Prepare the Run Directory

```
milestones/<spec>/
  LOG.md                     # durable. One entry per completed milestone.
  .run/
    RUN.md                   # this run's state
    NN-<name>.journal.md      # live scratch for the in-flight milestone only
```

Create `.run/RUN.md`:

```markdown
# <Spec Name> — Run <date>

**Range:** NN–MM · **Status:** running
**Estimate:** <wall-clock range> · <token range> · confidence <low|med|high>

## Resolved up front
- <question> → <the user's answer>

| # | Milestone | Status | Commit | Notes |
|---|-----------|--------|--------|-------|
| 01 | <name> | pending | — | |
```

**Resolved up front** is load-bearing, not a record. Every answer the user gave at the gate is passed into the subagents that need it — otherwise a question answered at 9am gets asked again by a cold agent at noon.

Neither `RUN.md` nor the journals are ever staged into a milestone commit — they are run artifacts, not project history. `LOG.md` is the opposite: it *is* history, and it ships **inside** the commit it describes.

That split makes `RUN.md` the **sha ledger.** A log entry cannot name the commit that contains it, so `LOG.md` records what happened and `RUN.md` records where it landed. Keep the commit column filled — the end-of-run report and any rewind depend on it. `RUN.md` is uncommitted and therefore mortal, so subagents also put the milestone number in the commit message; `git log --grep` is the durable fallback once a run's artifacts are gone.

`LOG.md` is also the one file **every** milestone touches, so it is exempt from the contamination check: subagents re-read it from disk immediately before appending, append only, and never halt on finding it dirty.

`RUN.md` existing with **Status: running** *is* the run lock. If you find one when starting a fresh run, another run is either live or died mid-flight — say which milestone it was on and ask, rather than starting a second run against the same tree.

**★ The lock is tree-wide, not plan-wide.** Before starting, check **every** `milestones/*/.run/RUN.md`, not just this plan's. Two runs on different plans still share one git index, and `git add` + `git commit` are not atomic across processes — the other run's staged files land in your commit, or yours in theirs. Disjoint file sets do not help; the index is the shared resource.

If a run is live on another plan, **stop and say so.** The only safe way to run two plans at once is a second `git worktree` with its own index, and that is viable *only* for work that never opens the Unity Editor (a `tools/` .NET project, say — a worktree gets no `Library/`, and the Editor MCP binds to one instance). Offer that; never assume it.

---

## Step 3 — The Loop

For each milestone in range, **strictly in order, one at a time**:

1. Mark it `running` in `RUN.md`.
2. Dispatch **one fresh subagent** (see *The Subagent Contract*).
3. Read its structured return. Do not read anything else it touched.
4. Update `RUN.md` with the outcome.
5. Act on the status:
   - **`complete`** → next milestone.
   - **`blocked`** → **stop the entire run.** Go to Step 4.
   - **`broken`** → dispatch **one** fresh retry subagent, pointed at the same milestone and its journal. If that one also returns `broken`, stop the run and go to Step 4.
   - **`contaminated`** → **stop the entire run immediately. Do not retry.** A file this milestone edited was already modified by something outside the run. The work is built and probably fine — it just cannot be committed as an isolated milestone, which is the one guarantee this skill sells. A retry would only reproduce the same collision. Go to Step 4.

**Never run two milestones concurrently.** They stack on each other, and they share one Unity Editor — a second agent driving playmode while the first is mid-verify corrupts both.

**Never skip a milestone and continue past it.** Ordering is load-bearing; milestone N+1 assumes N landed.

### Halt budget

If the run halts **three or more times**, stop escalating individual questions and report the pattern instead: three halts means the *plan* is underspecified, not that the milestones are hard. Recommend a return to `plan_milestones` rather than another resume.

---

## Step 4 — On Halt

Play `afplay /System/Library/Sounds/Glass.aiff`. Then give the user:

- **Which milestone halted**, and whether it was a question, a failure, or contamination
- **The blocking question verbatim.** Do not paraphrase it, and do not answer it yourself — that is the entire point of a halt.
- **What state the tree is in** — which milestones are committed, and what uncommitted work the aborted milestone left behind
- **What's left** in the range

**On `contaminated`,** give the user the contested file, what the milestone changed in it, and what was already changed in it. Then offer the three real options and let them choose: commit the file whole (accepting the foreign change into this milestone's commit), revert the foreign change and commit clean, or resolve the file by hand and tell you to resume. **Do not pick for them, and never attempt to split the file yourself.**

The uncommitted work stays where it is. Do not clean it up, stash it, or revert it — the journal describes it and the resume agent will continue from it.

When the user answers, resume by re-entering the loop at the halted milestone. Pass their answer through to the fresh subagent along with the journal path.

---

## Step 5 — End-of-Run Report

When the range completes, play the sound and report:

- **Milestone-by-milestone:** name, commit sha, one line on what landed
- **The flag list** — every tier-2 decision the subagents made that later work will build on. This is the most valuable part of the report; put it where the user cannot miss it. Each entry: what was decided, why, and what it would cost to change now.
- **Evidence** captured during verification, in order — state reads, and screenshots where they were obtainable. For Unity milestones, note that `screenshot-game-view` can return frame 1 forever; a subagent that verified by state read rather than by image has done it *right*, and the report should say so rather than apologising for missing pictures
- **What automated verification could not prove:** feel, pacing, difficulty, whether it's fun
- **Tech debt and assumptions** accumulated across the run, from `LOG.md`
- **Estimate vs actual** — what the pre-flight predicted for time, tokens, and stop points, against what happened. Where it was wrong, say why. This is how the next run's estimate gets better; without it the briefing stays guesswork forever.

Then say plainly: **play the build now.** Every milestone is its own commit, so any single one can be reverted without unpicking the rest.

Finally, mark `RUN.md` complete and delete any leftover journals.

---

## Fork Triage — the contract every subagent follows

The project's `CLAUDE.md` already draws this line: *surface a plan only when a decision is genuinely architectural — the event contract, the SO schema, or the layer boundaries.* Everything below is that rule, made operational. Sort by **reversibility × blast radius**, not by how interesting the decision feels.

| Tier | When | Action |
|---|---|---|
| **1 — Decide & log** | Local, reversible, dies inside this milestone: a placeholder number, a layout guess, a name, ugly-but-working | Decide. One journal line. Surfaces as *Assumptions* in `LOG.md`. Never halts. |
| **2 — Decide, log & FLAG** | Reversible, but later milestones will inherit it: the shape of a new SO field, a state machine's states, which system owns a piece of state | Decide, and mark the journal line `FLAG:`. It rises into the end-of-run review list. Never halts. |
| **3 — Halt** | The **event contract**, the **SO schema**, a **layer boundary**; the milestone doc contradicts the spec or itself; a deviation from the milestone doc; scope materially bigger than the cut implies | Abort upward with the question. Never guessed, in any mode. |

Tier 2 is the one that earns its keep. Most real forks land there, and without it they either stall the run or vanish silently.

---

## The Subagent Contract

Dispatch each milestone with a prompt that carries **only what a cold agent needs to find its own context** — never a summary of the milestone, which is the subagent's job to read:

- Invoke `implement_milestone` with the milestone number and `--unattended`
- The plan directory path
- The journal path to write to — and, on a resume, to read first
- The fork-triage table above
- **Any *Resolved up front* answers from `RUN.md` that bear on this milestone**
- The user's answer, if this is a resume after a halt

The subagent writes its journal **as it works**, not at the end. An agent that aborts at 70% never reaches a write-up step, so a journal written only at the end is worthless for exactly the case it exists to serve. One appended line per meaningful unit: SO field added, prefab authored, system wired, verification attempted and its result.

It returns **this and nothing more** — the return value is your entire context cost per milestone, so it stays small:

```json
{
  "milestone": "03 — Station Build Timer",
  "status": "complete | blocked | broken | contaminated",
  "commit": "<sha or null>",
  "built": "one line",
  "flags": ["tier-2 decisions later milestones inherit"],
  "blocker": "the verbatim question, or null",
  "journal": "milestones/<spec>/.run/03-....journal.md"
}
```

---

## Rules

- **Sequential. Always.** One Unity Editor, and milestones stack.
- **Never skip forward past a halt.** Answer, then resume in place.
- **Never answer a subagent's question yourself.** You have less context than it does — you deliberately did not read the docs.
- **Aborted work stays on disk.** Dirty tree plus journal is the resume state. Do not clean, stash, or revert it.
- **Commit per milestone, never bundled, staged by name.** Rewindability is the only compensation for the user not playing each one — a commit that swept in a foreign change cannot be reverted cleanly, so it isn't worth having.
- **The run owns the tree and the editor.** Never isolate a milestone in a `git worktree` when it needs the Editor: a second worktree gets no `Library/` (full reimport) and the Editor MCP binds to one project instance, so playmode verification is impossible there. A plan that never opens Unity is the one exception, and it applies to whole runs — not to individual milestones within a run.
- **One run per working tree.** Check every `milestones/*/.run/RUN.md` before starting, not just this plan's.
- **Stay small.** No milestone docs, no source, no spec. `RUN.md` and structured returns.
