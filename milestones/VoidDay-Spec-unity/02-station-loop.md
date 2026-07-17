# Milestone 02 — Station Loop

**Playable outcome:** Tap the Field to open its panel, queue a recipe, watch the timer run, tap the station to collect the output and see your resource counts rise — and watch a full 3-deep queue stop advancing until you collect.

## Goal
The heart of the game as a vertical slice. This is where the **event bus** is born (the first real need — input intents in, domain events out), where the generic **Producer** drives a recipe on a timer, and where the core friction — **stations block on uncollected output** — first bites. Get this feeling right and the rest is layering; get it wrong and no later milestone saves it.

## Build This
- **The event bus** (§15, CLAUDE.md rule 2): plain C# in `Core/Events`. Emitters describe what happened; listeners decide what to do. Systems talk only through it.
- **Input intents** (§15): the View publishes `input:stationTapped`, `input:jobQueueRequested`, `input:jobCancelRequested`; it never acts on them.
- **Generic Producer** (§4.1, CLAUDE.md): one `Producer` system driven by `StationModel` + `RecipeModel`. No per-station subclass.
- **Recipes** (§5.2): projected `RecipeModel` (inputs, outputs, optional timer, station type). Field recipes incl. both **Fallow** (0→1, very slow). Timer ≤0 ⇒ instant.
- **Job rules** (§4.4): inputs consumed at queue time (can't queue what you can't afford); queue depth base **3** read from `StationSO` via the seam (see below); cancel = full refund if not started, none once running; **blocking** — a completed job's output sits at the station and the next queued job does not start until collected.
- **Tap-resolution** (§4.4/§4.5, user-confirmed): implement a **generic `IsCollectionPossible(station)` predicate**. Tap → if the predicate is true, collect; else open `panel.station`. Do **not** branch on "ready" specifically — M7 adds "storage-full" as another reason the predicate returns false, and it must slot in without editing this logic.
- **Resource pool** (§4.2, §5.1): a global pool of resource counts in Core. Starting 1 wheat, 1 corn (§5.3). Emit `resource:changed` on every delta.
- **The `resolve(value, type, context)` seam** (architectural decision, user-approved): every tunable read into a rule — recipe duration, output quantity, input cost, queue depth base — goes through a `resolve()` call that is a **no-op passthrough** now. M5 gives it teeth. This is the seam that makes M5 pure extension.
- **Absolute-timestamp timers** (§13): store job timers as absolute timestamps, not frame-delta accumulation, so offline progress is a later change and not a rewrite.
- **Boot events** (§15): now that the bus exists, emit `data:loaded` / `game:started` at startup.
- **`panel.station`** (partial — recipe rows + queue): recipe rows as `pattern.purchaseRow` (inputs=cost, output, timer, `Queue`); job-queue display with per-job progress + cancel. **No collect button** (collection is a map tap).
- **World-space job state** (§12.6): `world.progressBar` (billboarded, attaches to M1's rig) while running; `world.readyIcon` (floating icon + hop/bounce) when output waits.
- **HUD money counter + `popup.totalResources`** for resource visibility. The counter shows `0` (no cash source yet); it is the tap-target that opens the total-resources popup.
- **Debug menu** (§12.7): `hud.debugButton` + `menu.debug` with **add resources** and **reset**. Grows in later milestones.

## Do NOT Build This
- **Orders / cash / XP** → M3. Money counter shows 0; no earning.
- **Building, demolishing, moving, the build menu** → M4. Only the pre-placed Field is used.
- **Any Effect resolution** → M5. The `resolve()` seam is a passthrough; do not build the resolver, schema, or stacking math.
- **Storage caps / storage-full state** → M7. Resources are **uncapped** here; collection always succeeds. (The predicate is generic so M7 adds the cap check without rework.)
- **Station upgrades, the upgrade rows in the panel** → M5.
- **VoidPet assignment slot in the panel** → M9 (hidden until a pet is owned).
- **Level/XP bar HUD** → M3.

## Context
Builds on M1 (grid, registry, station render, camera). Adds to the spine:
- **Events added:** `data:loaded`, `game:started`; input intents `input:stationTapped`, `input:jobQueueRequested {stationId, recipeId}`, `input:jobCancelRequested {stationId, jobIndex}`; `job:queued`, `job:started`, `job:completed {stationId, outputs}`, `job:collected {stationId, outputs, byPet?}`, `job:cancelled`; `resource:changed {resource, delta, total}`; `station:blocked {stationId, reason}`.
- **Data added:** `RecipeSO` → `RecipeModel` (inputs, outputs, optional timer, station type); `StationSO` gains `queueDepth` base + recipe refs (read); `ResourceSO` fields (base value etc. authored; counts live in Core pool).
- **Systems touched:** new `Core/Events` (bus), `Core/Rules` (job/recipe resolution), `Systems/Producer`, `Systems/ResourcePool`; `View/StationPanel`, `View/WorldState` (progress bar, ready icon), `View/HUD` (money, debug), `View/InputRouter`.

## Principles
- **Event-driven** (rule 2): the Producer emits `job:completed`; it never calls the View. The ready icon *listens*. No system holds a reference to another.
- **The Core boundary** (rule 3): job timing, blocking, consumption, the `IsCollectionPossible` predicate — all `Core/`. `Update()` only syncs the View to Core state (CLAUDE.md).
- **Data-driven** (rule 1): timers, costs, outputs, queue depth — all from SOs via `resolve()`. The only literals are structural (queue index, 0/1 identities).
- **Fail loud** (CLAUDE.md *Errors*): a tap on a station id with no registry entry throws, not returns.
- **Verify Unity APIs**: Input System `Pointer` tap, world-space Canvas / billboard, `Time` for timestamps — check against installed versions.

## Assets Required
- `mesh.station.field`, `mat.station.field` [placeholder OK — from M1]
- `icon.resource.wheat`, `icon.resource.corn` [placeholder OK — flat symbols; needed in recipe rows + total-resources popup] (other resource icons arrive as their recipes do)
- `icon.money` [placeholder OK — gold disc], `icon.hud.debug` [placeholder OK — UGUI default], `icon.ready` [placeholder OK — "!" / output icon]
- `ui.panel`, `ui.button`, `ui.card`, `ui.bar.progress` [placeholder OK — UGUI defaults / quads]
- **SFX** [placeholder OK — silent until authored]: `sfx.job.queue`, `sfx.job.complete`, `sfx.job.collect` (the hero cue), `sfx.job.cancel`, `sfx.ui.tap`, `sfx.ui.open`, `sfx.ui.close`
- **VFX** [placeholder OK — tween/tint]: `vfx.collectPop`, `vfx.readySparkle`

## UI Mockups Required
- `panel.station` (recipe rows + queue display only — no upgrades/pet slot yet) — [mockup needed]; follows `pattern.purchaseRow`.
- `hud.money`, `hud.debugButton`, `menu.debug`, `popup.totalResources` — [placeholder layout OK]
- `world.progressBar`, `world.readyIcon` — [mockup needed]

## Definition of Done
- Tapping the Field opens its panel; queuing `1 wheat → 2 wheat` consumes 1 wheat immediately and starts a timer with a visible progress bar.
- On completion, a ready icon hops above the station; the next queued job does **not** start until you tap to collect.
- Tapping the ready station collects (counts rise, ready icon clears); tapping an idle/running station opens the panel.
- Queue three jobs; confirm only the head runs and the rest wait behind the uncollected output.
- Tapping the money counter opens the total-resources popup showing wheat/corn counts; money reads 0.
- Debug → add resources bumps the pool; debug → reset returns to the start state.

## How to Test
1. Launch; tap the Field → panel opens with recipe rows and an (empty) queue.
2. Queue `1 wheat → 2 wheat`. Confirm 1 wheat is deducted at once and a progress bar fills.
3. Let it finish → a ready icon hops. Tap the station → wheat count rises by 2, icon clears.
4. Queue `1 corn → 2 corn` three times (use debug → add resources for corn). Confirm the queue shows three entries, only the head runs, and nothing advances until you collect.
5. Cancel a not-yet-started queued job → full refund; let one start, cancel it → no refund.
6. Queue a Fallow recipe (0 → 1 wheat) from empty → confirm it runs (very slowly) and needs no input.
7. Tap the money counter → total-resources popup lists every resource with its count.
8. Debug → reset → back to 1 wheat, 1 corn, empty queue.
