# Milestone 06 — Reports & Comparison

**Demonstrable outcome:** run a 30-seed sweep and see five charts — including a level × pressure heatmap that
tells you at a glance where the game jams — then overlay a second config and read the per-level deltas.

## Goal
Make the simulation legible. A per-level text table answers questions you already knew to ask; the pressure
heatmap shows you the ones you didn't. And balancing is inherently comparative — "is this better than what we
had" is the question every edit raises.

## Build This
- **`Sim/SimSweep.cs`** — N seeds via `Parallel.For`, aggregating median / p10 / p90 per metric. Individual
  seed results are retained, not discarded into the aggregate.
- **`POST /api/sim`** returning the aggregate plus per-seed results.
- **Chart.js 4.x vendored** into `wwwroot/vendor/` — no CDN. The p10–p90 band is two line datasets with
  `fill: '-1'`.
- **Five views:**
  1. **Time per level** — median bar + p10–p90 whisker. The headline.
  2. **Time composition** — acting vs waiting per level.
  3. **Money** — entry and exit per level, with band.
  4. **Pressure heatmap** — level × category by seconds lost. *The bottleneck view.*
  5. **Purchase timeline** — the level at which each remedy is first bought.
- **Click any seed** to open its individual run.
- **A/B overlay** — baseline and candidate pickers; charts 1 and 3 overlay both; a per-level delta table with
  direction marked. **Both sides run the same seed set**, so a difference is a real config effect rather than
  seed noise.

## Do NOT Build This
- **Sessions, journals, live views, the skill** → M7.
- **New simulation behaviour.** This milestone visualises M3's output; it does not change what is simulated.
  If a chart wants a metric that doesn't exist, add it to `LevelReport` — don't add a second measurement path.
- **Export/print/PDF.** Screenshot is fine.

## Context
- **New:** `Sim/SimSweep.cs`, chart views, A/B overlay, vendored Chart.js.
- **Extends:** M4's UI. This is why the workbench came first — the charts land in an app that already exists.

## Principles
- **Never present an aggregate without its spread.** A median with no band invites reading noise as signal.
- **The self-comparison is the control.** A/B of a config against an unmodified copy of itself must show
  every delta exactly zero. If it doesn't, the comparison is measuring the tool, not the game.
- **Seeds stay openable.** An aggregate you can't drill into is an aggregate you can't debug.

## Definition of Done
- A 30-seed sweep on `baseline` completes in reasonable time and renders all five views.
- The p10–p90 band is **non-degenerate** — proving seeds genuinely diverge rather than the sweep running the
  same stream 30 times.
- The heatmap highlights a category you can explain from the config.
- Opening a single seed reproduces its M3 CLI output exactly.
- A/B of `baseline` vs a copy of itself shows every delta exactly zero.
- A/B of `baseline` vs halved build costs shows earlier levels sooner and `Capacity` pressure falling.

## How to Test
1. Run a 30-seed sweep on `baseline`. Confirm all five views render and the band is non-degenerate.
2. Read the heatmap and satisfy yourself the dominant category per level is explainable.
3. Open one seed; diff its numbers against `balance sim --seed <n>` from the terminal.
4. Duplicate `baseline` unchanged; A/B them; confirm all deltas are zero.
5. Duplicate as `cheap-fields`, halve every `buildCost`, A/B; confirm the expected direction.
6. Set `refillSeconds` to 600 and `slotCount` to 1; re-run; confirm `OrderRefill` and `Income` climb.
7. Set every `unlockLevel` to 10; re-run; confirm `Unlock` dominates levels 1–9 with `Supply` rising.

**Acceptance cases covered:** QA-10, QA-11, QA-16, and the aggregation half of QA-13.
