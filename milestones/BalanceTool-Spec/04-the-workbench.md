# Milestone 04 — The Workbench

**Demonstrable outcome:** `dotnet run`, open a browser, and edit every economy tunable in a real UI — change
a recipe duration and a build cost, save it as a named version, push it to Unity, and see the asset change.

## Goal
Turn the CLI into an app. This is the surface you'll spend the most time in, and the one that makes
hand-tuning pleasant enough to actually do.

## Build This
- **Minimal API:** `GET/PUT /api/config`, `GET/POST/DELETE /api/versions`, `POST /api/sim`, `POST /api/write`.
  The browser is a client of the same contract the CLI uses — no second code path.
- **`wwwroot/`** — Preact + htm vendored as ESM, no build step. Tabs: **Global / Resources / Recipes /
  Stations / Upgrades / Levels / Orders**. Editable typed tables.
- **Add-row** for recipes, level rows and upgrade tiers. Resources and station types are **edit-only**.
- **Effect editor restricted to the six types with teeth**: `StationSpeed`, `StationYield`, `StationCost`,
  `StationQueueDepth`, `XpGain`, `StorageCap`. Offering the others would let someone author an effect that
  silently does nothing.
- **Client-side validation mirroring `BootValidator`**: level thresholds strictly ascending, level 1 has no
  grants, no duplicate ids, all references resolve, `triggerChance` in 0–100.
- **Version management UI** — list, load, save-as, delete. Push-to-Unity showing the change summary first.

## Do NOT Build This
- **Charts of any kind** → M6. This milestone edits; it does not report. A sim can be *triggered* and its
  raw table shown, but no visualisation.
- **A/B comparison** → M6.
- **Session views, journals, agent affordances** → M5/M7.
- **Authentication, multi-user, remote hosting.** It's a localhost dev tool.

## Context
- **New:** `Api/`, `wwwroot/`, version management.
- **Reads:** M1's reader, M2's writer, M3's runner — all already exist. This milestone adds no economy logic.

## Principles
- **The CLI is the contract.** The UI calls the same API the CLI does; no capability exists in one and not
  the other.
- **Validation mirrors the game's boot validation**, so a config that passes here boots there. Divergence
  means you can save something Unity will reject.
- **No build step.** Vendored ESM keeps the tool a `dotnet run` away from working, with no npm tree to rot.

## Definition of Done
- `dotnet run --project tools/VoidDay.Balance` serves the app; the browser loads `baseline`.
- Every field in `BalanceConfig` is editable, correctly typed, and round-trips through save/load.
- Adding a recipe, a level row and an upgrade tier all work and survive a save/load cycle.
- Invalid input (descending thresholds, a grant on level 1, a duplicate id) is caught client-side with a
  clear message before save.
- Save-as creates a new version file; the original is untouched.
- Push-to-Unity shows the change summary, and on confirm the asset changes.

## How to Test
1. Load `baseline`. Walk every tab and confirm the values match `baseline.json`.
2. Edit a recipe duration and a station build cost. Save as `test-tune`. Confirm both files on disk are
   correct and `baseline.json` is byte-identical to before.
3. Add a level row with a Money grant; save; reload; confirm it persisted.
4. Try to author descending XP thresholds and confirm it's refused.
5. Open the effect editor and confirm only the six types with teeth are offered.
6. Push `test-tune` to Unity, read the summary, confirm, and check the inspector.

**Acceptance cases covered:** the editor half of the tool; no new acceptance cases (QA is behavioural).
