# Milestone 07 — Balancing Sessions

**Demonstrable outcome:** type `/balance_game`, talk through what you want the game's balance to feel like,
watch the agent iterate in the browser while you interject, read a report explaining what it did and why,
and approve the export to Unity for playtesting.

## Goal
The payoff. Everything before this built capability; this builds the *workflow*:

1. Converse about goals → `goal.json`
2. Change values → patches against a working config
3. Simulate, read results, iterate — conversing throughout — until the goal is met or shown unreachable
4. Explain what was done and how, then export to Unity

## Build This
- **`Agent/Session.cs`** — session directories and the `session start` / `session status` / `session report`
  verbs:
  ```
  sessions/2026-07-22-capacity-pacing/
    goal.json  config.start.json  config.current.json  journal.jsonl  report.md
  ```
- **`journal.jsonl` gains a required `rationale`** per iteration — the one thing the agent must supply, and
  what lets the report explain *why* rather than only *what*.
- **`session report`** generates `report.md` **from the journal**: goal, starting values, every iteration with
  its rationale, final loss breakdown, and the exact diff exported to Unity.
- **`.claude/skills/balance_game/SKILL.md`** — the four-step workflow. Encodes:
  - the goal interview (step 1) and how to turn answers into a `goal.json`
  - the iteration loop: `eval` → `report` → `suggest` → `sweep` → `patch` → `eval`
  - **the autonomy boundary**: `eval`/`sim`/`sweep`/`suggest`/`patch` free; `write --apply` requires the
    user's explicit approval with the change summary shown first
  - **the infeasibility stopping rule**: sweep every knob `suggest` implicates to both bounds; if loss stays
    above target across all of them, declare unreachable **and present the sweep data as the argument**
  - terminal highlights at completion, with `report.md` as the durable record
- **Live session view** — the browser polls the active session directory and re-renders the loss curve,
  pressure heatmap and per-level times as iterations land.
- **Docs:** `tools/VoidDay.Balance/README.md` (ledger semantics, optimality dial, asset round trip).
- **Spec amendment:** `docs/VoidDay-Spec-unity.md` §9 currently forbids this tool ("no separate tool, no write
  endpoint"). Replace with: the inspector remains the authoring surface and runtime source of truth; an
  external balance tool reads and writes those assets offline. **Check §16 for a repeat of the claim.**
  Docs-only — no code dependency is created and the agnosticism rule holds.

## Do NOT Build This
- **An LLM inside the app.** No chat panel, no API keys, no tool-calling loop. Conversation lives in the
  harness, where it is native and free.
- **An MCP server.** Deferred deliberately — a second interface to keep in sync with the CLI for a modest
  gain inside Claude Code. Revisit if the tool needs driving from elsewhere.
- **Automated search.** Still no `optimize` verb; the agent runs the loop.

## Context
- **New:** `Agent/Session.cs`, `.claude/skills/balance_game/`, live session view, README.
- **Touched in the Unity project:** `docs/VoidDay-Spec-unity.md` §9/§16 — documentation only.
- **Final milestone:** run the full acceptance suite (QA-1 … QA-21) from the spec here.

## Principles
- **★ The report is generated, never narrated.** A long run exhausts a context window; an agent summarising
  from a compacted context produces a plausible, tidier story than what happened, with no signal it is doing
  so. Any claim in a report not traceable to a journal line is a bug in the generator.
- **"No" must be honoured.** Declining the export leaves `Assets/` untouched. Test this explicitly.
- **Infeasibility is argued, not asserted.** Without an explicit stopping rule an agent either gives up early
  or grinds forever.

## Definition of Done
- `/balance_game` runs end to end: goal interview → `goal.json` matching what was agreed → iteration loop →
  report → gated export.
- The browser updates live as iterations land.
- The agent stops and asks before any `.asset` write, showing the full change summary. Declining leaves
  `git diff` on `Assets/` empty; approving writes exactly what was summarised.
- `report.md` claims are all traceable to `journal.jsonl` lines — verified on a 25+ iteration run.
- No `profile/*` path appears anywhere in a session journal.
- An impossible goal terminates in reasonable time with sweep data as evidence.
- Spec §9 (and §16 if applicable) amended.
- **The full acceptance suite QA-1 … QA-21 passes.**

## How to Test
1. Run `/balance_game`. Agree a goal in conversation; check `goal.json` against what you said.
2. Watch the browser while it iterates. Ask it a question mid-run and confirm it answers without losing state.
3. When it proposes export, **decline**. Confirm `git diff` on `Assets/` is empty.
4. Ask it to proceed; read the change summary; approve; confirm the assets match.
5. Open `report.md`, pick five claims, trace each to a journal line.
6. Run the same commands by hand and confirm you get the numbers the agent reported.
7. Give it an impossible goal (every level under 30s *and* money at level 10 above 50,000); confirm it stops
   with evidence rather than grinding.
8. Walk the full acceptance suite from `docs/BalanceTool-Spec.md`.

**Acceptance cases covered:** QA-15, QA-19, QA-20, QA-21 — plus a full pass of QA-1 … QA-21.
