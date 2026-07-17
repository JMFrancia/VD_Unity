# Milestone 01 — World & Camera

**Playable outcome:** Open the WebGL build (or press Play) and pan around a rendered farm — a grid on the XZ plane with the pre-placed Field, Silo, and Order Board sitting on it as tinted primitive meshes under the angled ¾ top-down camera.

## Goal
The smallest thing a person can look at and touch. It stands up the boot path — load ScriptableObjects, validate them, project them into `Core/Model` objects, render the starting world — and one input: dragging to pan the camera. Everything later hangs off the data loader, the grid, and the station registry born here, so this milestone is defined by the visible world it produces, not by the plumbing under it.

## Build This
- **Boot data load + validation** (§14, CLAUDE.md *Data loading*): load `GameConfigSO`, every `StationSO`, every `ResourceSO`. Validate once at boot — every required reference assigned, every number in range — and **throw immediately** with asset name + field on failure. No default-fill.
- **SO → `Core/Model` projection** (§14): project the loaded SOs into plain `Core/Model` objects (`GameConfigModel`, `StationModel`, `ResourceModel`). Asset refs (mesh/material) stay on the SO; the model carries only rule-relevant scalars. `Core/` must not `using UnityEngine`.
- **The grid** (§4.1): a fixed lattice on the XZ plane sized from `GameConfigSO` (~20×30), one station per cell, cell `(col,row)` → world position.
- **Station registry** (§4.1): the authoritative map of which cell holds which station. **Build it to support runtime add/remove and occupancy queries from day one** — M4 places stations into this same registry, so it must not assume a fixed roster (see *Gotchas* in the summary).
- **Pre-placed start** (§5.3): instantiate 1 Field, 1 Silo, 1 Order Board from the `GameConfigSO` start block into the registry, each rendered as its per-type primitive mesh tinted by `StationSO.placeholderColor` (§12.6).
- **Camera** (§12.5): orthographic, pitched ~55–60°. **Pan** drags the camera across XZ via pointer raycast-to-XZ-plane delta (world point stays under the finger). Pinch-zoom changes orthographic size. Min/max zoom + pan bounds from `GameConfigSO`; pan clamped to map bounds.
- **Input binds to `Pointer`** (mouse + touch), not `Touchscreen`, so pan is mouse-verifiable in a browser (§12.5 testing note).
- **Billboard rig** (§12.6): the reusable component that rotates world-space UI to face the camera each frame (constant yaw/pitch). Built here so later world-space UI (progress bar, ready icon, hearts) just attaches to it. Nothing uses it yet.
- **Ground + backdrop** (§12.5): the grassy island on the soft-violet void.

## Do NOT Build This
- **The event bus, input intents, any `input:*`/domain events** → M2. Pan/zoom are camera view-state and emit nothing (§15 note; UI-Inventory `world.playfield`).
- **Tapping a station / opening any panel / any job logic** → M2.
- **Station *state* visuals** — working progress bar, ready hop/icon, storage-full tint → ride with the milestone that introduces the state (progress + ready → M2; storage-full → M7). Build only the static station body + the billboard rig here.
- **The `resolve()` value seam** → M2 (it's born where the first tunable is read into a rule).
- **Any HUD** (money, XP bar, buttons) → M2/M3.
- **Runtime placement / build menu** → M4.

## Context
First milestone — nothing exists yet. Adds to the spine:
- **Events added:** none (no bus yet).
- **Data added:** `GameConfigSO` (grid dims, cell size, camera min/max zoom, pan bounds, start block: starting resources + pre-placed stations), `StationSO` (per station: `placeholderColor`, mesh/prefab ref, footprint, `displayName`; other fields authored but unread here), `ResourceSO` (per resource: id, display name — other fields unread here). `Core/Model`: `GameConfigModel`, `StationModel`, `ResourceModel`.
- **Systems touched:** new `Systems/Boot` (load + validate + project), `Systems/Grid` (registry + occupancy), `View/Camera`, `View/StationView` (renders a station body), `View/Billboard`.

## Principles
- **The Core boundary** (CLAUDE.md rule 3): the grid math, registry, and models are candidates for `Core/`; anything touching `Vector3`/meshes/camera is `Systems`/`View`. A grid cell is `(int col, int row)`, never a `Vector3`.
- **Data-driven** (rule 1): grid size, cell size, camera bounds, start roster — all from `GameConfigSO`. No literal `20`, `30`, zoom clamps, or start list in code.
- **Verify Unity APIs against what's installed** (CLAUDE.md): confirm the Input System (`Pointer`), URP orthographic camera, and raycast-to-plane calls against `Packages/manifest.json` versions before writing them — don't call from memory.
- **Fail loud** at the data boundary; assume well-formed data past boot.

## Assets Required
- `mesh.station.field` [placeholder OK — flat quad], `mesh.station.silo` [placeholder OK — cylinder], `mesh.station.orderBoard` [placeholder OK — primitive TBD]
- `mat.station.field`, `mat.station.silo`, `mat.station.orderBoard` [placeholder OK — `placeholderColor` tint]
- `mesh.env.ground` [placeholder OK — tinted Plane], `mesh.env.backdrop` [placeholder OK — skybox/quad], `mat.env.ground`, `mat.env.skybox` [placeholder OK]
- (Henhouse/Pasture/Creamery/Bakery/Workshop meshes are not placed here — deferred to when they're buildable, M4.)

## UI Mockups Required
- `world.playfield` — no chrome; it is the scene. [placeholder layout OK]
- No HUD or panel surfaces this milestone.

## Definition of Done
- The build loads in a browser with no console errors; a malformed SO throws a boot exception naming the asset + field.
- You see a grid-sized grassy island on a violet void, with a Field, a Silo, and an Order Board on distinct cells, each a distinctly-tinted primitive.
- Dragging with the mouse pans the camera; the world point stays under the cursor; panning stops at the map edges.
- (Touch device only: two-finger pinch zooms between the configured min/max.)

## How to Test
1. Open the WebGL build (or press Play).
2. Confirm three tinted primitives sit on the grid in distinct cells, and the camera looks down at ~55–60°.
3. Click-drag on empty ground — the camera pans and the point under the cursor tracks your finger/mouse.
4. Drag toward each edge — panning clamps at the map bounds (you can't scroll into the void forever).
5. (Optional, touch device) pinch to zoom; confirm it stops at the min/max.
6. Temporarily blank a required field on one SO in the inspector and relaunch — confirm a boot exception names that asset and field (then restore it).
