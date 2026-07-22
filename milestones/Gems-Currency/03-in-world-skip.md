# Milestone 03 — In-World Skip

**Demonstrable outcome:** queue a long job at a station and close the panel. The in-world radial shows a gem
cost under the seconds. Tap the radial → "Skip for ◆2?" → Confirm → the job completes instantly and its
output sits ready to collect. The same works on a construction site's radial.

## Goal
Make the in-world timers skippable through the same rule and the same popup M2 built. This milestone is
**View wiring plus one `GameBoot` line** — no Core changes, no new events. `TimerRef.Job` and
`TimerRef.Construction` already work; they just have no tap surface yet.

## Build This

- **`View/TimerWidget.cs`** — gains:
  - `[SerializeField] Collider tapCollider`
  - `[SerializeField] TextMeshPro costLabel` (3D TMP, like `secondsLabel` — **not** UGUI)
  - `public TimerRef Timer { get; set; }` — set by whoever owns the widget
  - `public void SetCost(int gems)`
  The widget stays pure presentation: it renders what it is handed and decides nothing. It does **not** ask
  `TimeSkip` for its own cost.
- **`TimerWidget.prefab`** — author the collider sized to the ring, and the cost label beneath the seconds.
  The widget is `SetActive(false)` whenever no timer runs, so the collider is inert for free — no
  enable/disable logic needed.
- **`View/StationStateWidget.cs`** — ⚠️ **required, and easy to miss.** `WorldState` holds a
  `StationStateWidget`, not a `TimerWidget`; the timer is private inside it (`StationStateWidget.cs:15`) and
  only `SetTimerVisible` / `SetTimer` are exposed. Add pass-throughs — `SetTimerRef(TimerRef)` and
  `SetTimerCost(int)` — so `WorldState` can reach the nested widget. `rig.Widget.Timer` does **not** compile.
- **`View/InputRouter.cs`** — check `GetComponentInParent<TimerWidget>()` **before** `QueueSlot` and
  `StationView` in **both** raycast paths:
  - `TryTapStation` (`:88`) → publish `TimerSkipTapped(widget.Timer)`
  - `StationUnder` (`:77`, run on press, feeds the long-press pickup) → return null for a timer hit, or a
    long press on a radial picks the station up for a move.

  The widget sits under the station root, so without this ordering `GetComponentInParent<StationView>`
  claims the hit in both paths.
- **`View/WorldState.cs`** — sets the rig's timer ref to `TimerRef.Job(rig.StationId)` when the rig is built,
  and pushes the cost inside the per-frame poll it already runs (right where it calls `SetTimer`), via the
  new `StationStateWidget` pass-throughs. Needs `TimeSkip` injected via `Init`.
- **`View/ConstructionSiteView.cs`** — sets `timer.Timer = TimerRef.Construction(e.StationId)` on the
  `TimerWidget` it instantiates directly (this one *is* a `TimerWidget`, so no pass-through needed), and
  pushes the cost in its existing `Update` loop. Needs `TimeSkip` injected via `Init`.
- **`GameBoot`** — pass `TimeSkip` into both `Init` calls. ⚠️ `constructionSiteView.Init` is currently called
  at `:114`, **before `OrderBoard` is constructed at `:187`**, and `TimeSkip` depends on `OrderBoard`. Move
  `constructionSiteView.Init` down with the other `Init` calls (smaller change, and matches where every other
  `Init` already lives) — or hoist the `OrderBoard` construction. Pick one deliberately.
- **`StationStateWidget.prefab`** — its nested `TimerWidget` instance must pick up the new collider and cost
  label authored in `TimerWidget.prefab`.

## Do NOT Build This
- **Any `Assets/Core/` change.** `TimeSkip`, `TimerRef` and the three owner hooks are finished. If this
  milestone finds itself editing `Assets/Core/`, something is wrong — stop and flag it. (`GameBoot` under
  `Systems/` *is* touched — that is the composition root, not Core.)
- **A separate skip pill under the ring.** The whole widget is the tap target; that was chosen deliberately
  (fewer authored objects) and the confirm popup is what protects against a misfire.
- **Forcing the job radial visible while the station panel is open.** `WorldState` hides it by design
  (BUG-03). A job skip happens from the closed-panel view. This is intended — do not "fix" it.
- **A skip control inside `StationPanel`.** Considered and cut.
- **New events or a second confirm popup.** M2's serve both surfaces.

## Context
- **New:** nothing.
- **Touched:** `View/TimerWidget.cs`, `View/StationStateWidget.cs`, `View/InputRouter.cs`,
  `View/WorldState.cs`, `View/ConstructionSiteView.cs`, `Systems/Boot/GameBoot.cs`,
  `Assets/Prefabs/**` (`TimerWidget.prefab` and the `StationStateWidget.prefab` instance of it).

## Principles
- **The View renders from state and captures input** (rule 3) — it holds no rule. The cost comes from
  `TimeSkip`; the widget just draws it.
- **One prefab, two callers.** `TimerWidget` already serves jobs and construction sites; the skip affordance
  is authored once and both inherit it. That is the whole reason the shared widget exists.
- **Unity-native authoring** (rule 4): the collider and label are authored in the prefab, not added in code.

## Definition of Done
- A running job's radial shows a gem cost that falls as the timer runs down.
- A construction site's radial shows the same.
- `TimerSkipTapped` carries the correct `TimerRef` for each — verified by inspecting the wiring, since
  pointer injection is unavailable.
- Confirming a job skip completes the job; the output sits ready and is collectable.
- Confirming a construction skip finishes the station, publishes `StationBuilt`, and fires the confetti
  exactly as a natural finish does.
- Tapping a station **body** (not its radial) still collects or opens the panel, and a long press on a radial
  does not pick the station up — the skip never steals a gesture. **Inspection-only** (see How to Test 5).
- Skipping a job while the silo is full completes it into the `storage-full` warning state, not a crash.
- All EditMode tests still pass.

## How to Test
1. Playmode (`runInBackground = true`). Queue a long job, close the panel, screenshot the radial showing
   seconds **and** a gem cost.
2. Publish `TimerSkipTapped(TimerRef.Job("<stationId>"))` → confirm → the job completes and the ready icon
   appears. Screenshot before and after.
3. Place a station, screenshot its construction radial with the cost, then skip it → the station appears and
   the confetti fires.
4. **Verify the tap wiring by inspection**, since pointer injection is unavailable in this project: confirm
   the `TimerWidget` collider is authored and enabled, and that **both** `InputRouter.TryTapStation` and
   `InputRouter.StationUnder` check `TimerWidget` before `QueueSlot` / `StationView`. Say plainly in the log
   that a real tap was not injected.
5. ⚠️ **Do not "verify" tap-stealing by publishing `StationTapped` directly** — that event is what
   `InputRouter` publishes *after* it has already resolved the raycast, so the test bypasses the branch under
   test and can never fail. The "skip never steals a tap" DoD item is an **inspection-only** check (step 4);
   record it as such rather than as a passed runtime test.
6. Fill the silo, then skip a job → it completes into the storage-full warning state.
7. Run the EditMode suite.
