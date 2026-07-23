# Milestone 05 — Quest Authoring in the Tool + `balance_game` Skill

**Playable outcome:** In a balance session you can **create**, **reorder**, and **delete**
quests; `write --apply` writes the new/changed `QuestSO` `.asset` files back into the Unity
project (and Unity opens/plays them); and the `balance_game` skill knows how to interview
for, tune, author, and export quests.

> **Deliberate deviation from "playable in the editor":** like M4, this is offline-tool work.
> Its "play" is running the CLI authoring flow and then confirming the result *is* playable
> in Unity (open the project, the created quest works). Called out in the summary.

## Goal
Closes the user's explicit requirement that the headless tool can **create/edit/reorder**
quests and the `balance_game` skill has what it needs to do quest balance work. It comes last
because it's the highest-risk tool work — new structural write paths in `AssetWriter`, which
today refuses list add/remove/reorder everywhere except recipes — and it builds on M4's
reader/back-map/knobs.

## Build This

**Structural config ops (`Agent/Patch.cs` or a dedicated verb)**
- Today `patch` supports only `op:"set"`, and lists are positionally addressed with no
  add/remove/move. Add the ability to **create** a quest (append a `QuestConfig`), **delete**
  a quest, and **reorder** the `Quests` list. Follow the existing verb/op conventions —
  either new ops (`insert`/`remove`/`move`) or a scoped `quest` sub-verb — but do not invent
  a grammar that fights AGENTS.md. Document whatever you add in `AGENTS.md`.

**Write-back (`Unity/AssetWriter.cs`)**
- Add surgical quest write paths, using the two existing structural templates as the model:
  - **Create** a new `Quest_<Name>.asset` (+ `.meta`) and wire it into `GameConfig.asset`'s
    `quests` list — mirror `InsertRecipe` (which writes a new recipe `.asset` + `.meta` and
    wires it into the `StationSO`).
  - **Reorder / delete** — edit the `quests` reference list in `GameConfig.asset`; for a
    structural rewrite of a block, mirror the grant-block regeneration path
    (`BuildGrantsBlock`/`ApplyGrantRewrites`) which regenerates a list byte-for-byte.
  - **Scalar edits** to existing quests — line-addressable single-scalar replacement, per the
    writer's "never reserialize; a one-field change is a one-line diff" rule.
- Preserve the writer's loud-refusal discipline: anything it genuinely can't do safely throws
  `WriteRefusedException` rather than corrupting an asset.
- Resolve quest id → `.asset` path via the `_questGuidById` back-map added in M4.

**`balance_game` skill (`.claude/skills/balance_game/SKILL.md`) + `AGENTS.md`**
- **Step 1 interview:** add quest-shaped questions (quest pacing, reward richness, whether
  quests gate progression) that map to the M4 quest metric(s).
- **Steps 2–3 iteration:** document the quest knobs (from M4's `bounds.json`) and the new
  create/reorder/delete ops so `suggest`/`sweep`/`eval` can move them.
- **Step 4 export:** the gated `write --apply` now includes quest write-back; keep the
  explicit-approval gate (a "no" writes nothing).
- Keep `AGENTS.md` (the authoritative contract the skill defers to) in lockstep — every new
  verb/op/metric/path/bound documented there, or the skill won't use it.

## Do NOT Build This
- **New in-game quest behavior** → owned by M1–M3. This milestone changes only the tool and
  the skill; `Assets/` game code is untouched except as the *target* of `write --apply`
  (which writes SO `.asset` data the same way a designer's inspector edit would).
- **A GUI for quest authoring** — the CLI + skill flow is the surface; no new workbench UI
  beyond what naturally falls out (out of scope unless the user asks).
- **Loosening the writer's safety rules** — new paths must still be surgical and refuse loudly
  on anything unsafe. Do not switch to full reserialization.
- **Cross-session quest persistence in the game** — still out of scope (no save system).

## Context
Builds on M4 (schema, reader, `_questGuidById` back-map, knobs, metrics) and M1 (`QuestSO`
asset shape the writer must produce). Final milestone.
- **Events added:** none.
- **Data files/fields added:** new write paths + structural ops; quest verbs/ops/metrics/
  bounds documented in `AGENTS.md`; quest workflow in `SKILL.md`. The *output* is new
  `Assets/Data/SO/Quest_*.asset` files created by the tool.
- **Systems touched:** `Agent/Patch.cs` (+ possibly a new verb in `Cli/Program.cs`),
  `Unity/AssetWriter.cs`, `AGENTS.md`, `.claude/skills/balance_game/SKILL.md`, and
  `bounds.json` if new knobs are exposed.

## Principles
- **One-way mirror + write-back discipline (project memory):** the tool writes `.asset`
  *files*; nothing under `Assets/` references the tool. The writer never reserializes — a
  change is a minimal diff — and refuses loudly rather than guessing.
- **Always playtest in Unity after a balance export (project memory):** the sim doesn't model
  boot/UI rules, so a tool-created quest must be opened in Unity and played to confirm
  `BootValidator` accepts it and the menu/pill/toast behave. This is the M5 acceptance gate.
- **AGENTS.md is the contract; the skill defers to it** — add capabilities there in lockstep
  or the skill can't use them (it's forbidden from inventing verbs/metrics).

## Assets Required
None (the tool *creates* quest `.asset` files as output; no pre-made art needed).

## UI Mockups Required
None.

## Definition of Done
- In a session you can create a new quest, reorder the quest list, and delete a quest via the
  CLI, and re-sim to see the effect.
- `write --apply` (after explicit approval) creates/updates the `Quest_*.asset` files and the
  `GameConfig.asset` `quests` list with minimal, correct diffs; refuses loudly on anything
  unsafe.
- Opening the Unity project after an export: the created quest passes `BootValidator`, appears
  in the menu, and is completable/collectable in Play.
- The `balance_game` skill, followed end-to-end, can interview for quest goals, move quest
  knobs, author a quest, and gate the export — using only documented `AGENTS.md` capabilities.

## How to Test
1. Start a balance session. Create a new quest via the CLI; `sim` and confirm it appears and
   can complete in-sim.
2. Reorder the quest list and delete a quest; confirm the config reflects both and re-sims.
3. `write --apply` and approve. Inspect the `git diff` — confirm the new `Quest_*.asset`
   (+ `.meta`), the `GameConfig.asset` `quests` edits, and any scalar changes are minimal and
   correct.
4. Open Unity, press Play — confirm the tool-created quest validates at boot, shows in the
   menu, and is collectable (M1–M3 behavior intact).
5. Run the `balance_game` skill against a quest-pacing goal end-to-end; confirm it uses only
   documented metrics/knobs/verbs and gates the export on your approval.
