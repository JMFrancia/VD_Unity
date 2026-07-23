# Milestone 04 — Quests in the Headless Sim

**Playable outcome (tool-side):** `dotnet run --project tools/VoidDay.Balance -- sim`
(and `eval`) runs the **real** quest rules from `Assets/Core`, and the report shows quest
completions and quest reward income per level. Quest scalar knobs (reward amounts, goal
counts, unlock levels) are movable via `eval`/`sweep`/`suggest`.

> **Deliberate deviation from "playable in the editor":** M4/M5 are offline-tool work; the
> user explicitly scoped the balance tool into this feature. Their "play" is running the
> CLI and reading the report, not pressing Play in Unity. This is called out in the summary.

## Goal
Make the balance sim quest-aware so quest rewards stop being invisible income and quests can
be measured and tuned. It comes after the in-game feature (M1–M3) because it consumes M1's
Core quest code; it comes before M5 because you tune-then-author (measure scalar knobs here,
add structural create/reorder/delete write-back in M5).

**Why this is small:** the tool globs `Assets/Core/**/*.cs` into its own compile
(`VoidDay.Balance.csproj`), so M1's pure-Core `QuestLog` runs in the sim for free once it's
constructed and fed data. This milestone is *data mirroring + wiring + metrics + knobs*,
**not** a re-implementation of quest logic. If you find yourself re-writing quest behavior in
the tool, M1 put the logic in the wrong layer — stop and fix M1.

## Build This

**Config schema (`tools/VoidDay.Balance/Schema/BalanceConfig.cs`)**
- Add `List<QuestConfig> Quests` mirroring the `QuestSO` shape (conditions, goal, reward)
  as plain serializable C#, matching how `RecipeConfig`/`UpgradeConfig`/`LevelConfig` mirror
  their SOs.

**Reader (`Unity/EconomyReader.cs`, `Unity/RawAssets.cs`)**
- Add a `QuestRaw` DTO in `RawAssets.cs`; reference the quests list from `GameConfigRaw`
  (mirror how `stationRoster`/`Levels` are read).
- Add `ReadQuests` + `ProjectQuests` in `EconomyReader`, carrying enums **by name** (as the
  reader already does at the enum-mapping seam) so reordering a Core enum can't silently
  reassign kinds.
- Add a `_questGuidById` back-map (mirroring `_recipeGuidById`) so M5's writer can resolve a
  quest id to its `.asset` path.

**Harness (`Sim/CoreHarness.cs`)**
- Construct `QuestLog` from the projected quest configs, exactly where `GameBoot` constructs
  it — `CoreHarness` mirrors `GameBoot.Start()` (keep the mirror annotation / reconcile date
  updated; the `GameBootParityTests` canary guards this). Feed it the same bus + read handles
  + reward sinks the game does. Because `QuestLog` subscribes to the real bus, progress and
  rewards flow automatically; `MetricsCollector` already sees the reward deltas as normal
  `MoneyChanged`/`GemsChanged`/`ResourceChanged`/XP.

**Metrics (`Sim/MetricsCollector.cs`, `Schema/SimResult.cs`)**
- Subscribe to `QuestCompleted`/`QuestCollected`; add counters (quests completed per level,
  quest reward XP/money/resources granted). Surface them on `SimResult.LevelReport` next to
  `OrdersFulfilled`/`JobsCollected`.

**Goal metrics (`Agent/Goal.cs`, `Agent/GoalEvaluator.cs`) + contract (`AGENTS.md`)**
- Add quest-oriented metric(s) the skill can target (e.g. `quest.completionLevel`,
  `quest.rewardShare`). **The skill forbids inventing metrics**, so whatever you add here
  must be documented in `AGENTS.md`'s metric list in the same change.

**Scalar knobs (`Agent/Patch.cs`, `bounds.json`)**
- Extend the path grammar in `Patch.cs` to address quest scalars
  (e.g. `quests/<id>.reward.xp`, `quests/<id>.goal.amount`, `quests/<id>.conditions[0].amount`).
- Add `bounds.json` entries (min/max) for the quest knobs so `eval`/`sweep`/`suggest` may
  move them. Only allowlisted paths are movable.

## Do NOT Build This
- **Create / reorder / delete quests, and write-back to `.asset` files** → M5. This milestone
  is read + model + scalar-edit-in-config only. Do **not** touch `AssetWriter.cs` here.
- **Re-implementing quest evaluation in the tool** — the shared Core compile runs the real
  `QuestLog`. If it doesn't compile into the tool, the fix is in M1 (a stray `UnityEngine`
  reference in Core), not a mirror here.
- **`balance_game` SKILL.md interview/procedure changes** → M5 (paired with the authoring
  workflow). Adding the *metric* to `AGENTS.md` is in-scope; rewriting the skill is not.
- **New quest goal/condition kinds** — mirror exactly what M1 shipped.

## Context
Builds on M1 (Core quest code + `QuestSO` + `GameConfig.quests`). Independent of M2/M3
(those are View-only and invisible to the sim) — M4 could technically follow M1 directly.
- **Events added:** none (consumes M1 quest events on the shared bus).
- **Data files/fields added:** `QuestConfig` on `BalanceConfig`; `QuestRaw` DTO; quest
  counters on `SimResult.LevelReport`; quest metric(s) in `Goal`/`GoalEvaluator` +
  `AGENTS.md`; quest knob entries in `bounds.json`.
- **Systems touched:** `Schema/BalanceConfig.cs`, `Schema/SimResult.cs`, `Unity/EconomyReader.cs`,
  `Unity/RawAssets.cs`, `Sim/CoreHarness.cs`, `Sim/MetricsCollector.cs`, `Agent/Goal.cs`,
  `Agent/GoalEvaluator.cs`, `Agent/Patch.cs`, `bounds.json`, `AGENTS.md`.

## Principles
- **One-way mirror (project memory):** the tool reads `Assets/` (SO `.asset` files + shared
  Core source); nothing under `Assets/` may learn the tool exists. No game code changes here.
- **The sim doesn't model boot/UI rules** (project memory: balance-config-vs-BootValidator).
  The pill's 20s window, toasts, and menu are View — the sim ignores them and only models the
  economic facts (grant → progress → reward). Note this so nobody expects UI behavior in a run.
- **Enums carried by name, never index**, at the reader seam — matches the existing convention
  and prevents silent kind reassignment.
- **Never invent a metric the skill will reject** — `AGENTS.md` is the contract; add there in
  lockstep.

## Assets Required
None (tool-side).

## UI Mockups Required
None.

## Definition of Done
- `dotnet run --project tools/VoidDay.Balance -- read --json` includes the quests, sourced
  from the SO assets.
- `sim`/`eval` runs without hand-mirroring quest logic, and the per-level report shows quest
  completions and quest reward income (the numbers move when quests actually complete in-sim).
- `eval --session … --path quests/<id>.reward.xp --value … --rationale …` moves the knob,
  re-sims, scores, and journals one line; the reward change is reflected in the report.
- `GameBootParityTests` (the harness↔GameBoot canary) still passes with `QuestLog` added.

## How to Test
1. `dotnet run --project tools/VoidDay.Balance -- read --json` — confirm the quests appear in
   the config with correct conditions/goal/reward.
2. `dotnet run --project tools/VoidDay.Balance -- sim --json` — confirm it runs and the report
   shows quest completions + quest reward income per level (non-zero once a quest completes).
3. Start a session and `eval` a quest reward knob up; confirm the sim re-runs, the report's
   quest reward income rises, and a journal line is appended.
4. `sweep` a quest knob across its bounds; confirm it's free/unjournaled and returns a range.
5. Run the tool's test suite; confirm `GameBootParityTests` passes.
