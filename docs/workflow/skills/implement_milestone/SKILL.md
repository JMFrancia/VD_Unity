---
name: implement_milestone
description: Implements exactly ONE playable milestone from a milestone plan in milestones/<spec>/, then stops — build, verify it runs, user plays it, commit, append to the running log. Built for rapid prototyping (no TDD, no test gate); the fast counterpart to implement_phase. Use when the user wants to build, continue, or resume a milestone from a plan_milestones plan. Not for designing plans (use plan_milestones), for running a whole plan back-to-back (use implement_all_milestones), or for production-grade phased work (use implement_phase).
user-invocable: true
argument-hint: "[milestone-number-or-path] (optional — defaults to the next incomplete milestone)"
---

# implement_milestone

Implements **exactly one milestone** from a `plan_milestones` plan, then stops.

This is the **prototype** counterpart to `implement_phase`. It does not run TDD, does not gate on a test suite, and does not ask permission to move. It gates on one thing: **the milestone is playable and the user has played it.**

**Behavior:** Build strictly to the end of one milestone. Never start the next.

**Input:** A milestone number or doc path. If omitted, take the next incomplete milestone per the log.

**Supersedes the global CLAUDE.md** where they conflict — the project's `CLAUDE.md` governs. Specifically: no TDD, no test gate, no planning ceremony, no Bug Fix Protocol. Retained: fail loud, fail fast at the data boundary, root causes over symptoms, comment hygiene, the notification sound.

---

## Step 0 — Resolve the Milestone

- **With an argument:** resolve a number against `milestones/**/NN-*.md`, or use the path directly.
- **Bare:** read `milestones/<spec>/LOG.md` and take the first milestone with no completion entry. If several milestone plans exist, use the most recently modified.
- **No log yet:** this is milestone 1. Create the log in Step 6.

---

## Step 1 — Confirm (interactive mode only)

State plainly: which milestone, how it was resolved, and its playable outcome in one line.

**Wait for confirmation.** Skip this step entirely in unattended mode (see *Unattended Mode* below).

---

## Step 2 — Read Context

Read, in this order:

1. **`milestones/<spec>/LOG.md`** — what's already built, what deviated, what debt exists, what was assumed. This is your memory of sessions you weren't present for. Read it first; it changes how you read everything else.
2. **The milestone doc** — the authority on scope. Its *Do NOT Build* list is binding.
3. **The spec sections it cites** — the milestone doc cites (§N) rather than restating, so follow the references.
4. **`CLAUDE.md`** — project and user level.
5. **The installed package versions, for any unfamiliar API.** Non-negotiable per `CLAUDE.md`: Unity 6.3 and the current URP / Input System packages are recent enough that recall is unreliable, and the failure mode is a confident call to an API that was renamed or removed. Check `Packages/manifest.json` for the actual version and verify against the installed source rather than memory. If a call can't be confirmed, say so rather than guessing. Legacy `Input.GetMouseButton` etc. do **not** work here.
6. **The mockup + asset readiness for every surface/asset this milestone touches.** The milestone doc's `[mockup needed]` / `[placeholder OK]` / `[asset needed]` markers are a **planning-time snapshot and go stale** — a mockup or asset that landed *after* the plan was written will not be reflected there. Before building any UI surface or wiring any asset, cross-check what is **actually ready now**:
   - The **UI-mockup manifest** (e.g. `docs/UI-Mockups.md`) — for each surface this milestone builds, does a mockup/Figma frame exist? Which variant is **CHOSEN** (a manifest may record a design decision that changes the surface's *structure*, not just its skin)? Read the chosen frame's look/layout (via the Figma MCP or the export) and the behavior contract in `docs/UI-Inventory.md`.
   - The **asset docs / project asset folders** (e.g. `docs/assets/`, `Assets/`) — has a real mesh/sprite/audio asset replaced a placeholder the milestone assumed?
   - **Ready beats the doc.** If a chosen mockup or real asset exists, build against it — do **not** hand-roll a placeholder layout or fake a mockup because the milestone doc's marker said "needed." If the chosen mockup **contradicts** the milestone doc's described layout/scope, that is a Step-3 pause trigger (docs contradict) — surface it, don't silently pick one.

---

## Step 3 — Build

**Before your first edit, take a `git status --porcelain` snapshot** and record it in your journal (or note it, in interactive mode). That snapshot is the "not mine" list — it is what makes a precisely-scoped commit possible in Step 6, and it cannot be reconstructed after the fact.

Work to the end of this milestone only. Follow the project's two non-negotiable rules:

- **Data-driven.** Every tunable lives in the inspector — game data in ScriptableObjects, static presentation in the authored prefab or scene that owns it. If a value has no home in an SO, add the field first, then read it. Never inline with a "TODO: move to config."
- **Event-driven.** Systems talk through the bus. `Core/` never `using UnityEngine`. View captures input and emits intents; systems decide.
- **Unity-native authoring.** Scenes and prefabs are authored through the editor (via the Unity MCP), not generated in code. No runtime UI construction, no `new GameObject(...)` hierarchies. `GameBoot` stays a slim composition root.

Use the milestone doc's *Context* section for the events and data fields this milestone adds to the spine — those names are already reconciled across milestones. **Do not invent an event name that the plan already specifies.**

Move fast. No abstraction until the third occurrence. No error handling for impossible states. Placeholder art is correct.

### Pause triggers

**Stop, play `afplay /System/Library/Sounds/Glass.aiff`, and ask** when:

- **You have a question the milestone doc and spec don't answer.** Do not invent the answer. Inventing is how a spec's authority quietly transfers to whoever wrote the code.
- **The milestone doc contradicts the spec**, or two spec sections contradict each other.
- **You want to deviate from the milestone doc** — get consent first.
- **You'd have to break a data-driven or event-driven rule** to proceed. That's architectural, and architectural decisions are the one thing the project says to surface.
- **The work turns out to be materially bigger than the milestone implies**, suggesting the cut was wrong.

### Decide, but flag it

Between "just decide" and "stop and ask" sits the decision that is reversible *now* but that later milestones will inherit — the shape of a new SO field, a state machine's states, which system owns a piece of state. **Decide and keep moving**, but call it out explicitly in Step 5 and record it in the log: what you chose, why, and what changing it later would cost.

Most real forks land here. Treating them as pause triggers stalls the work; treating them as tier-1 trivia buries a decision the user should have seen.

### Not a pause trigger

- **Tech debt.** Log it (Step 6), don't gate on it. The project optimises for speed; a debt item recorded is enough.
- **Placeholder values.** Invent them into the SO and move on — that's what the data layer is for.
- **Ugly-but-working.** Ship it. Primitives and untextured meshes are correct until proven otherwise.

---

## Step 4 — Verify It Actually Runs

**Before handing it to the user, confirm it works yourself.** Refresh the AssetDatabase and confirm it compiles. Then enter playmode via the Unity MCP and drive it — walk the milestone doc's *How to Test* steps and confirm the *Definition of Done*.

**★ Do not trust `screenshot-game-view`.** When the editor cannot be brought to the foreground, the player loop freezes at `frameCount = 1` and the Game View returns **frame 1 forever** — it will cheerfully show you a stale HUD while you believe it is live, which is how unverified work gets reported as verified. `Application.runInBackground = true` reads `True` inside playmode and **does not fix this.**

Verify by reading state, not by looking:

- **Read component state via `script-execute`** — this is the source of truth, and on its own it is enough to confirm most *Definition of Done* items.
- **Only if you need a rendered image:** flip the overlay canvases to `ScreenSpaceCamera` in playmode and use `screenshot-camera`, which renders on demand. The change is playmode-only and discarded on exit.

Check the console for errors. A milestone that throws on load is not done.

**Report the truth.** If something doesn't work, say so with the output. Never present unverified work as verified.

---

## Step 5 — The User Plays It

Play the notification sound. Give the user:

- **What was built** — deliverables, files touched
- **How to test it** — the numbered steps from the milestone doc
- **What you verified yourself**, and what you couldn't
- **Deviations** from the milestone doc, and why
- **Tech debt** taken, and why
- **Assumptions** made, and what breaks if they're wrong

**The user plays it. This is the gate.** A milestone is done when you can press Play and see the new thing work — not when the code compiles.

If they have fixes, **iterate within this milestone.** Re-verify, re-present. Do not commit or advance until they verify.

---

## Step 6 — Commit and Log

**Only after the user verifies.**

Three steps, **in this order.** The log goes *into* the milestone's commit — a milestone whose commit lands without its log entry leaves the next cold agent reading a plan that claims work is unbuilt.

1. **Decide the staging set.** Do not commit yet.

   **Stage explicit paths — never `git add -A` or `git add .`.** Another session may be running the art pipeline against the same working tree (`style_guide`, `asset_list`, `ui_inventory` all write to `docs/`), or the user may have saved a scene in the editor. A blanket stage sweeps that into this milestone's commit and destroys the one thing the per-milestone commit buys you: the ability to rewind exactly this milestone and nothing else.

   Commit by this procedure:

   - **The "not mine" list.** You took a `git status --porcelain` snapshot before your first edit (Step 3). Everything dirty in that snapshot belongs to someone else.
   - **The "mine" list.** The paths your journal recorded touching. Stage exactly these, by name.
   - **Leave everything else alone.** Dirty paths you didn't touch, and untracked files you didn't create, stay uncommitted. They are not yours to tidy.
   - **Overlap is contamination.** A path on *both* lists — already dirty when you started, and edited by you — cannot be safely split. `git add -p` is interactive and unavailable, and hunk-splitting a `.unity` or `.prefab` YAML file corrupts scenes.

     **Do not commit. Halt.** Report the path, what you changed in it, and what was already changed in it. Splitting the two is a human decision, and getting it wrong silently is far more expensive than stopping.

     **`LOG.md` is exempt from this check** — see step 2.

2. **Append to `milestones/<spec>/LOG.md`**, creating it if absent. This is the *last* file you write and it is handled differently from everything else, because it is the one file every milestone touches:

   - **Re-read it from disk immediately before appending.** Not the copy you read in Step 2 — hours of building have passed, and on a resumed or repaired run another agent may have appended since. Appending to a stale copy silently deletes their entry.
   - **Append only.** Never rewrite, reorder, or reflow existing entries. Your entry goes at the end.
   - **If an entry for this milestone already exists** (a resumed run that got further than its journal suggests), replace *that entry in place* rather than appending a second one.
   - **A dirty `LOG.md` is not contamination.** It is the expected state. Append and stage it; never halt on it.

   The entry:

```markdown
# <Spec Name> — Implementation Log

Running record across milestones. Read this first when picking up cold.

---

## Milestone NN — <Name>
**Status:** ✅ Complete · **Date:** <date>

**Built:** What actually landed.

**Deviations from the plan:** What differs from the milestone doc, and *why*. Nothing to say here is a valid entry — say "none."

**Tech debt:** What was deferred, and what it will cost.

**Assumptions:** What was assumed and not verified. Each is a risk; say what breaks if wrong.

**Gotchas for later milestones:** What the next implementer needs to know that isn't in any doc.
```

   **The entry carries no commit sha.** It can't — the entry ships *inside* the commit it would name. The milestone number in the commit message is the link, and the sha lives in `RUN.md` and the orchestrator's report, which are not committed. Each artifact does one job.

3. **Commit** the staged paths and `LOG.md` together, as one commit. Never bundle milestones. Follow the repo's commit conventions (`git log`), and put the milestone number in the message.

The log is the **memory across cold sessions.** Write it for someone who wasn't here — because next time, nobody was.

---

## Step 7 — Stop

Tell the user the milestone is complete and committed, and name the next one.

**Do not auto-advance.** Each milestone is a chance to feel a wrong turn early — that's the entire point of cutting them this way.

---

## Unattended Mode

Used by the `implement_all_milestones` orchestrator, which runs milestones back-to-back in fresh subagents. When invoked with `--unattended`:

- **Skip Step 1.** No confirmation.
- **Journal as you work, not at the end.** The orchestrator gives you a journal path. Append one line per meaningful unit — SO field added, prefab authored, system wired, verification attempted and its result. Prefix inherited decisions with `FLAG:`. An agent that aborts at 70% never reaches a write-up step, so a journal written only at the end is worthless for exactly the case it exists to serve.
- **On resume, read the journal first.** A journal already at your path means a previous agent aborted mid-milestone and its work is still in the tree. Read it, read `git status` and `git diff --stat`, and **continue** — do not restart, and do not revert what's there.
- **Every pause trigger becomes an abort.** A subagent cannot talk to the user, so it stops and returns the blocking question upward, verbatim. **Leave the work in place** — dirty tree plus journal is the resume state. Never guess an answer, in any mode.
- **The "decide, but flag it" tier does not abort.** Decide, journal it with `FLAG:`, keep building. Aborting on every inherited decision would halt the run constantly and defeat the point.
- **Step 5's user gate becomes Step 4's automated verification.** Commit on a clean automated verify — compiles, playmode runs, *How to Test* walked, game view screenshotted, console clean. Automated verification proves it *works*; it cannot prove it's *fun*. That's the trade the user makes by choosing the orchestrator.
- **Step 6 still runs**, and `LOG.md` is committed with the milestone. Then delete your journal — a journal left on disk *means* "this milestone is half-done."
- **Return only the structured result** the orchestrator asked for. Its context budget per milestone is that object; prose spent there is context stolen from the rest of the run.

---

## Rules

- **One milestone. Then stop.**
- **The user plays it before you commit.** Interactive mode has no exception to this.
- **Commit only what you touched.** Stage by name against your journal, never `-A`. A contaminated file halts the commit — never split it yourself.
- **Never invent an answer.** Ask, or abort upward.
- **The *Do NOT Build* list is binding.** It exists because a later milestone owns that work.
- **Verify unfamiliar APIs against the installed packages.** Unity 6.3 / URP / Input System are newer than recall; a confident call to a removed API is the failure mode.
- **Author in the editor.** Scenes, prefabs, materials, and serialized references go through the Unity MCP — not code that builds hierarchies at runtime.
- **Log for a stranger.** Next session, you are one.
