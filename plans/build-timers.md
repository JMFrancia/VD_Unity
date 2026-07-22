# Build Timers — Prototype Plan

## Implementation Status
<!-- implement_phase reads and updates this ledger. -->
| Phase | State | Commit | Notes |
|---|---|---|---|
| 1 — Shared TimerWidget | ✅ DONE | — | TMP essentials imported (LiberationSans SDF). Label is black — white was unreadable on the light ring. `TryGetHeadProgress` gained `secondsRemaining`; both call sites are in `WorldState`. |
| 2 — Core build timer + construction site | ⬜ TODO | — | |
| 3 — Completion celebration | ⬜ TODO | — | |

## What It Is

Placing a station no longer creates it. A **construction site** appears on the farm instead — the station's own
mesh rendered semi-transparent, with a countdown above it. When the timer expires the real, operable station
takes its place, a sound plays, and confetti bursts out of it.

The countdown is rendered by a **shared `TimerWidget`** — one authored prefab and one component used by both the
construction site and the existing in-world job radial, so the game has a single timer visual rather than one
per feature. Jobs gain a seconds readout they don't have today as a side effect.

## How It Works

### The event contract — the pivot the whole design turns on

`StationBuilt` keeps its exact meaning ("this station now exists and is operable") and simply **fires later**.
Everything already listening — `ProgressionSystem` (XP), `UpgradesSystem` (register tracks), `StationRegistry`
(instantiate the real prefab), `BuildMenu` (refresh), `SfxController` (the `StationBuilt` cue), `StationFlattenMask`
(press the grass flat) — therefore lands at completion with **no change to any of those files**. The completion
sound the feature asks for is the `StationBuilt` cue that already exists.

A new `StationConstructionStarted(StationId, StationType, Cell, Duration)` carries the placement moment.

```
drag drop → PlaceRequested → BuildSystem.Place(type, cell, now)
                               ├─ charge money, occupy the cell, count against cap
                               └─ publish StationConstructionStarted   → ConstructionSiteView spawns the placeholder
                                                                       → BuildMenu refreshes
                                                                       → SfxController plays the "thunk"
        ... StationRegistry pumps BuildSystem.Tick(now) each frame ...

  end time reached → BuildSystem completes the site
                       ├─ clear UnderConstruction, JobSystem.Register (now operable)
                       └─ publish StationBuilt  → StationRegistry instantiates the real prefab
                                                → ConstructionSiteView destroys the placeholder, fires confetti
                                                → ProgressionSystem awards XP
                                                → SfxController plays the completion cue
                                                → UpgradesSystem, BuildMenu, StationFlattenMask (unchanged)
```

### Core owns the timing

`BuildSystem` holds the pending sites with absolute end-times (§13, same as `JobSystem`), and `StationRegistry`
pumps `_build.Tick(Time.timeAsDouble)` exactly as `Producer` pumps `_jobs.Tick(now)`. The View never decides when
a station exists — it polls `BuildSystem.TryGetSiteProgress(id, now, out fraction, out secondsRemaining)` to
render, which is the same shape as `JobSystem.TryGetHeadProgress`.

The station enters the grid at **placement**, flagged `UnderConstruction`. That is what makes the cell occupied
and the cap honest immediately. The flag is also what keeps the order pool honest: `GameBoot.Producible()`
iterates `grid.All`, so without it an unfinished Bakery would start attracting cake orders.

`buildSeconds <= 0` completes on the same frame — the same escape hatch `JobSystem` gives instant recipes, so a
designer can restore today's build-instantly behavior per station type from the inspector.

### The construction site is inert

The placeholder carries **no `StationView`**, so `InputRouter` can neither tap nor long-press it: no panel, no
move, no demolish, no cancel, no refund. It is also absent from `StationRegistry.Roots`, so the camera, the
station panel and the grass-flatten mask all ignore it — the grass springs back up only when the real station
lands, which reads correctly as "the site isn't finished".

### The shared TimerWidget

TextMeshPro ships inside `com.unity.ugui` 2.0.0 on Unity 6 (`Runtime/TMP/Unity.TextMeshPro.asmdef` in the package)
but its Essential Resources are **not yet imported** — Phase 1 imports them once, via the Unity MCP. A **3D**
`TextMeshPro` component renders as a mesh with no canvas, which matters: this project's URP camera setup does not
render world-space canvases (see the note on `StationStateWidget`), which is why the whole in-world rig is meshes.

`QueueSlot`'s slot-0 mini progress bar is deliberately **not** folded in — it is a horizontal fill inside a
half-unit chip, and a ring plus digits does not fit there.

## Data & Config

| Where | What | Why there |
|---|---|---|
| `StationSO.buildSeconds` (float) | Seconds to build one of this type. | Game data → SO (rule 1). Per-type, inspector-tuned. `0` = instant. |
| `StationTypeModel.BuildSeconds` | The Core projection of the above. | Core reads a Model, never an SO (rule 3). Set in `ModelProjector.ProjectType`. |
| `StationModel.UnderConstruction` (bool) | Site placed but not finished. | Rule-relevant state — gates the order pool. |
| `TimerWidget.prefab` | Ring quad + 3D `TextMeshPro`, billboarded. | Presentation → authored prefab (rule 4). Text size/colour/offset live here. |
| `ConstructionSite` material | Semi-transparent tint for the placeholder body. | Authored material in `Assets/Art/Materials`, its own asset (not the green/red placement-ghost materials). |
| `ConstructionSiteView` serialized fields | `constructionMaterial`, `timerTemplate`, timer height, `confettiPrefab`, confetti lifetime. | Static presentation → inspector on the scene component. |
| `SfxCue.StationConstructionStarted` | New cue, **appended** to the enum. | Appended, never inserted — the enum's integer values are what `SfxLibrarySO` serializes. |

No new `ResolveKind`. Build duration goes through no effect today and inventing a seam entry for a
non-existent emitter is exactly the speculative generality YAGNI forbids; the read site is one line to reroute
if a "build speed" upgrade ever lands.

## Gotchas Found While Planning

Discovered by reading the code, not derivable from the plan above. Each one is a real trap.

- **`SfxCue` is a boot-time trap.** `SfxController.Init` **throws** if any enum value has no row in the
  `SfxLibrarySO` asset, and rows are only created by that asset's `OnValidate`. After appending the new cue,
  select the SfxLibrary asset in the inspector to resync its rows — otherwise the game will not boot.
- **TMP Essential Resources live at a hash-suffixed path:**
  `Library/PackageCache/com.unity.ugui@<hash>/Package Resources/TMP Essential Resources.unitypackage`
  (currently `@11343a6274f3`; the hash changes on package re-resolve). There is no `Assets/TextMesh Pro`
  folder and no TMP entry in `ProjectSettings` yet.
- **`StationStateWidget` drives `_Fill` through a `MaterialPropertyBlock`** so every station shares one
  material asset. `TimerWidget` must keep that — assigning `.material` instead silently forks a material
  instance per station.
- **`PlacementController.SpawnGhost` is already the placeholder recipe** — instantiate the station prefab,
  disable all child colliders, destroy the `StationView`, swap `sharedMaterial` on every child renderer.
  Copy the shape, but with a **new** construction material asset, not `ghostValidMaterial` / `ghostInvalidMaterial`.
- **`BuildMenu` already refreshes on `MoneyChanged`**, which fires when the build cost is charged at placement.
  The extra `StationConstructionStarted` subscription only matters for a zero-cost station type.
- **`StationFlattenMask` caps at 32 stations** (`MaxStations`, must match `MAX_FLATTEN_STATIONS` in
  `VertexColorToon.shader`) and reads `StationRegistry.Roots`, which construction sites stay out of.
- **Phase 1 changes a Core signature.** `JobSystem.TryGetHeadProgress` gains an out-param; `WorldState` has two
  call sites — check `StationPanel` and the EditMode tests in `Assets/Tests` for more before changing it.

## Phases

### Phase 1 — Shared TimerWidget (ring + seconds), adopted by the job radial

- Import TMP Essential Resources once (Unity MCP).
- Author `Assets/Prefabs/TimerWidget.prefab`: billboarded root, ring quad on the existing
  `VoidDay/RadialProgress` material, 3D `TextMeshPro` for the seconds.
- `View/TimerWidget.cs` — `Show(bool)`, `SetProgress(float fraction, float secondsRemaining)`. Formats `14s`
  under a minute, `1:23` above.
- Extend `JobSystem.TryGetHeadProgress` with a `secondsRemaining` out-param (Core already owns the end time;
  the View must not re-derive it).
- Swap `StationStateWidget`'s `radialRoot`/`radialRenderer` for a nested `TimerWidget`; `WorldState` passes the
  remaining seconds through.

**Verify:** queue a job — the ring above the station now counts down in seconds as it fills, and still hides
when that station's panel is open.

### Phase 2 — Core build timer + the construction site

- `StationSO.buildSeconds` → `StationTypeModel` → `BootValidator` (`>= 0`).
- `StationModel.UnderConstruction`.
- `BuildSystem`: `Place(type, cell, now)` charges + occupies + publishes `StationConstructionStarted`;
  `Tick(now)` completes due sites (clear flag, `JobSystem.Register`, publish `StationBuilt`);
  `TryGetSiteProgress(...)`. `buildSeconds <= 0` completes inline.
- `StationRegistry` pumps `_build.Tick(Time.timeAsDouble)` and passes `now` into `Place`.
- `GameBoot.Producible()` skips `UnderConstruction` stations.
- `BuildMenu` also refreshes on `StationConstructionStarted` (the cap count changes at placement).
- New `View/ConstructionSiteView.cs` — spawns the translucent placeholder (station prefab, colliders disabled,
  `StationView` stripped, renderers swapped to the construction material) with a `TimerWidget` above it; polls
  Core each frame; destroys it on `StationBuilt`. Wired in `GameBoot`.
- EditMode test in `Assets/Tests` covering the Core rules: money charged at placement, the site counts against
  the cap, the station is **not** job-registered until `Tick` completes it, and `buildSeconds = 0` completes
  on the same frame.

**Verify:** place a station — a see-through copy appears with a counting-down ring, the money is gone and the
build-menu cap badge already ticked up. It can't be tapped, opened, or dragged. When the timer expires the real
station appears, is tappable, and the grass presses flat under it.

### Phase 3 — Completion celebration

- `SfxCue.StationConstructionStarted` appended + mapped in `SfxController` (the placement thunk). Completion
  already has a voice: the existing `StationBuilt` cue, which now fires at the right moment.
- `confettiPrefab` on `ConstructionSiteView`, instantiated at the finished station on `StationBuilt` and
  destroyed after its serialized lifetime. **User supplies the particle asset.**

**Verify:** a finishing station pops confetti and plays its completion sound; placing one plays a distinct
placement sound.

## Decisions Made

- **Money is charged at placement**, and the site counts against the per-type cap immediately — otherwise you
  could queue five Bakeries you can't afford.
- **No cancel and no refund** on a site under construction. It is inert by construction (no `StationView`), so
  there is no gesture that could ask for one.
- **XP arrives at completion**, free, because `ProgressionSystem` listens to `StationBuilt`.
- **Grass flattens at completion**, not at placement — the site is not in `StationRegistry.Roots`.
- **Session-local timing.** `Time.timeAsDouble`, like every other timer in the project. There is no save system,
  so offline progress is not a question yet.
- **Pre-placed scene stations are unaffected** — `RegisterPreplaced` never constructs.
- **`QueueSlot`'s mini progress bar stays as it is** — wrong shape for a ring-and-digits widget.

## How to Verify by Playing

1. Open the build menu, drag a station onto a free cell. A see-through copy appears with a ring counting down
   in seconds. Money is deducted immediately and the cap badge in the build menu ticks up.
2. Tap the site, long-press it, drag it. Nothing happens — no panel, no pickup.
3. Wait for the timer. The solid station appears, confetti fires, the completion sound plays, and the grass
   presses flat beneath it.
4. Tap the new station — its panel opens and it can queue jobs.
5. Queue a job on any station: the same ring-and-seconds widget counts the job down.
6. Set a station's `buildSeconds` to `0` in the inspector and place one — it appears instantly, as before.
7. With a station still under construction, watch the order board: it should not offer goods only that
   unfinished station could make.
