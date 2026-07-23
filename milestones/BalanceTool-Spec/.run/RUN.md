# BalanceTool-Spec — Run 2026-07-22

**Range:** 01–07 · **Status:** running
**Estimate:** ~2.5–5 h wall-clock · ~1.6–3.0M tokens · confidence medium
**Dominant driver:** M03 (CoreHarness reconciling real Core against GameBoot).

## Resolved up front
- **QA scope → ALL QA-1…QA-26.** M07's "run QA-1…QA-21" phrasing is stale (predates the gem QA cases). The M07 acceptance replay covers every case through QA-26, including the gem-policy cases QA-22–26. Pass this to the M07 subagent.

## Predicted stop points (forecast)
1. M03 — layer boundary: `CoreHarness` mirroring `GameBoot.Start()` (wiring order, `Producible()` live closure, `-1` sentinel across three timer owners).
2. M03 — parity-canary timing: hashing `GameBoot.cs` last, before confirming in-flight gems/VFX committed.
3. M01 — Core failing to compile standalone under net9.0, or YAML preprocessor hitting an unhandled asset form.
4. M02 — structural insertion (new RecipeSO + generated .meta GUID) with minimal diffs.
5. M07 — doc-vs-spec (QA numbering, now resolved above).

## Tree notes
- This run **never opens the Unity Editor** (verification = `dotnet run` + C# tests). Editor lock relaxed.
- One-way dependency: nothing under `Assets/` learns the tool exists. `git status Assets/` stays clean except assets explicitly written (M01 `.gitignore`, M02 write-targets, M07 export).
- Pre-existing dirty/uncommitted, DO NOT stage: `ProjectSettings/ProjectSettings.asset` (DOTween define), staged rename `plans/balance-tool.md → docs/BalanceTool-Spec.md`, `README.md` (untracked), `milestones/BalanceTool-Spec/03-simulate.md` (modified plan doc), `milestones/Collection-Particles/.run/`, `docs/workflow/skills/preproduction/`.

| # | Milestone | Status | Commit | Notes |
|---|-----------|--------|--------|-------|
| 01 | Read the Economy | running | — | |
| 02 | Write It Back | pending | — | |
| 03 | Simulate | pending | — | |
| 04 | The Workbench | pending | — | |
| 05 | Agent Primitives | pending | — | |
| 06 | Reports & Comparison | pending | — | |
| 07 | Balancing Sessions | pending | — | |
