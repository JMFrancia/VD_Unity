# VoidDay (Unity) — Implementation Log

Running record across milestones. Read this first when picking up cold.

---

## Milestone 01 — World & Camera
**Status:** ✅ Complete · **Commit:** `d96dc10` · **Date:** 2026-07-17

**Built:**
- **Core layer** (`Assets/Core/`, pure C#): `GridCoord`, `ResourceModel`, `StationModel`, `GameConfigModel`, and `StationGrid` (registry with runtime add/remove + occupancy). A `VoidDay.Core.asmdef` with `"noEngineReferences": true` **mechanically enforces the Core boundary** — Core physically cannot `using UnityEngine`. Assembly-CSharp (Data/Systems/View) auto-references it.
- **Data layer** (`Assets/Data/`): `GameConfigSO`, `StationSO`, `ResourceSO` with `[CreateAssetMenu]`. Placeholder art (primitive type, scale, euler, y-offset, tint) is fully SO-driven; a real prefab (`StationSO.prefab`) wins over the primitive with zero code change (§12.8).
- **Systems/Boot**: `GameBoot` (composition root — validate → project → build grid → render world), `BootValidator` (fail-loud, names asset + field), `ModelProjector` (SO→Model), `GridProjection` (cell→world Vector3, Systems-side so Core stays Unity-free).
- **View**: `CameraController` (orthographic ¾ pitch, pointer raycast-to-XZ pan with world-point-under-cursor, zoom via touch pinch **and** desktop scroll/keys), `StationView` (prefab-or-primitive body), `Billboard` (rig built, nothing attached yet, per plan).
- **Assets**: `Assets/Data/SO/` — `GameConfig`, `Resource_Wheat` (sellable=false), `Resource_Corn`, `Station_Field/Silo/OrderBoard`. Scene `Assets/Scenes/Farm.unity` (Main Camera + Directional Light + GameBoot; ground + stations spawned at runtime from data).

**Verified:** boots with no console errors; world renders (grass ground + 3 distinctly-tinted primitives on distinct cells under the tilted camera); fail-loud proven by blanking `wheat.id` → `[Boot validation] Resource_Wheat.id must not be empty`, then restored. Pan + zoom *feel* verified by the user in Play (not scriptable via MCP — no synthetic input injection).

**Deviations from the plan:**
- **Field placeholder is a flat Cube, not a Quad.** A Unity Quad is single-sided and would backface-cull / z-fight under the top-down camera; a flat cube reads identically and is robust. Still a per-SO `PrimitiveType`.
- **Ground tinted `#9AD070`** (StyleGuide "lighter grass") instead of `#7DBE5A`, because the StyleGuide assigns the *Field* that same `#7DBE5A` — identical tints make the field invisible against the ground. Both are data; retune freely.
- **Environment colors live on `GameConfigSO`** (`groundColor`/`backdropColor`) rather than separate `mat.env.*` assets — keeps boot fully code-driven.
- **Added desktop zoom (mouse scroll + `-`/`=` keys)** beyond the plan's touch-only pinch. Reason: DoD #4 (pinch) is not reproducible on the WebGL-desktop verification gate (§12.5 note); scroll/keys make zoom testable there. Speeds are data (`scrollZoomStep`, `keyZoomSpeed`). CLAUDE.md's "no keyboard" is about *gameplay* — this is a test affordance, commented as such. User-approved.
- **Removed the 4 MCP extension packages** (cinemachine/particlesystem/timeline/splines) from `manifest.json` to fix a compile-blocking version skew (see Gotchas). User-approved.

**Tech debt:**
- `BootValidator` validates the SOs *referenced* by the config (all pre-placed stations + starting resources), not a `Resources.LoadAll` sweep of every SO in the project. For M1 referenced == all. If a later milestone authors SOs that aren't referenced at boot, add a load path then.
- `LitMaterial(Color)` helper is duplicated in `GameBoot` and `StationView` (2 occurrences — under the rule-of-three, left as copy). Third use → extract.
- `ResourceModel` carries only `Id`/`DisplayName`; `baseValue`/`sellable` are on the SO but not yet projected (M3 order pricing/generation will add them to the model).

**Assumptions:**
- **Project Player setting "Active Input Handling" includes the Input System package** (Pointer/Mouse/Keyboard). If it's set to legacy-only, `Pointer.current` etc. return null and pan/zoom silently do nothing. (CLAUDE.md states the project uses the new Input System.)
- **URP is configured in Graphics/Quality settings** (a URP asset assigned). If not, `Shader.Find("Universal Render Pipeline/Lit")` materials render wrong. Verified visually — they render.
- **Grid is centered on the world origin** and the camera focus starts at origin. M4 placement math must use the same `GridProjection.CellToWorld` convention.

**Gotchas for later milestones:**
- **No event bus yet** (M2 builds `Core/Events` + the bus). M1 systems don't emit anything; pan/zoom are pure view-state per the plan.
- **No `resolve()` value seam yet** — M2 introduces it where the first tunable is read into a rule.
- **`StationModel.Id` is a per-instance id** formatted `"{stationType}#{n}"` (e.g. `field#0`), assigned at spawn by `GameBoot`. `StationType` is the SO's type key. Events like `input:stationTapped {stationId}` will use the instance Id.
- **The grid registry (`StationGrid`) already supports runtime add/remove + occupancy** — M4 places into this same instance; do not build a parallel registry.
- **MCP package is pinned to `com.ivanmurzak.unity.mcp` 0.82.3.** The 4 extension packages required 0.84.3, whose source overrides a `CredentialProvider` member absent from the installed `McpPlugin.Common` 6.10.0 DLLs → `CS0115` → all compilation halts. Do **not** re-add those extensions without upgrading the NuGet DLLs to a 0.84.3-compatible version first.
- **Editor-scripting gotcha (asset refs):** assigning an asset reference to a scene component via `SerializedObject` *in the same script that just created the asset* did not serialize (`config: {fileID: 0}`); assigning after the asset is registered (`AssetDatabase.LoadAssetAtPath`) worked. Wire asset→scene refs in a second step / after a refresh.

---

## Milestone 02 — Station Loop
**Status:** ✅ Complete (logic verified; **UI redo done** — see addendum) · **Commit:** `d87eb8e` (loop) + UI-redo commit · **Date:** 2026-07-17

**Built:**
- **Event bus** (`Core/Events/`): `EventBus` (type-keyed pub/sub, plain C#) + `GameEvents` (M2 slice of §15). Systems talk only through it now.
- **Generic Producer** (`Core/Rules/JobSystem.cs`): one Core state machine drives every station's queue. Owns queue-time input consumption (§4.4), the **blocking** rule (a completed head sits `Complete` and stalls the queue until collected), cancel refunds (full if `Queued`, none once `Running`), and the generic **`IsCollectionPossible`** predicate. Timers are **absolute timestamps** (§13) — `double now` passed in from the System.
- **`resolve()` value seam** (`Core/Rules/ValueResolver.cs`): every tunable (duration, output qty, input cost, queue depth) reads through `Resolve(value, ResolveKind, ctx)`. Pure passthrough now; **M5 gives it teeth** without touching call sites. `ResolveKind` is a local enum, deliberately *not* §3.1's `EffectType` (M2 must not depend on the effect schema).
- **`ResourcePool` + `Wallet`** (Core): global resource counts (uncapped in M2) + money (0, no source until M3). Both emit their `*:changed` event on every delta. `RecipeCatalog` indexes recipes by id + station type.
- **Data**: `RecipeSO` (inputs/outputs as `Ingredient{ResourceSO,amount}`, `duration ≤0 = instant`); `StationSO` gains `queueDepth` + `recipes[]`. Four Field recipes authored (`Recipe_Field_*`): wheat/corn grow (5s), two Fallows (30s, no input). Boot validator extended.
- **Systems**: `Producer` (MonoBehaviour) pumps `Tick(Time.timeAsDouble)` + translates `input:*` intents to Core calls; the **tap-resolution branch lives here** as the single authority. `GameBoot` rewired as composition root (builds bus + economy core, registers stations, creates EventSystem + Systems + Views, seeds pool, emits `data:loaded`/`game:started`).
- **View**: `StationPanel` (recipe rows + live queue), `WorldState` (billboarded progress bar + hopping ready icon, polls Core each frame), `Hud` (money counter → total-resources popup, debug menu: +5 per resource, Reset), `InputRouter` (tap→`input:stationTapped`), `UiFactory` (UGUI helpers, legacy `Text`). All placeholder UGUI.

**Verified:** every DoD behavior passed, driven through the real event bus in Play (reflection to publish the same intents a tap/click produces): queue-time consumption (wheat 1→0), timed completion, **blocking** (`[Complete Queued Queued]` — 2nd never starts), collection (+2, ready clears), cancel refunds (queued +1 / running none), Fallow (no input consumed, runs), reset (→1 wheat/1 corn/empty). Panel, progress bar, ready-hop, totals popup all render (screenshots). No console exceptions.
- **NOT verified (MCP can't inject synthetic pointer input):** the literal pointer→raycast→`StationTag` hit. Everything downstream of the raycast is proven; the physical click needs a human (same gap as M1 pan/zoom). User played it and confirmed it works.

**Deviations from the plan:**
- **UI was the WRONG panel.station model at ship — since REDONE against ALT `42:2` (addendum below).** M2 shipped the HayDay-simple single-panel fallback (Figma `5:2`); the chosen design is the ALT / Full HayDay (`42:2`): recipe selection as a floating popup by the building, **job queue as slots under the building (not a panel)**. The logic layer is model-agnostic and stood unchanged; **only the View was reworked** in the follow-up. User verified in Play.
- **Added `StationPanelRequested`** (a View-routing event not in §15): the tap-resolution branch (`Producer`) either calls `Collect` or emits this; the panel just listens. Keeps the predicate evaluated once, avoids a double-trigger. Justified vs §15's "no `ui:*`" note because it's a routed intent, not a "showX" directive.
- **Added minimal `debug:*` intents** (`DebugAddResourceRequested`, `DebugResetRequested`) — §15 leaves debug wiring open (open item); each routes through a natural domain effect (add→`resource:changed`, reset→`game:reset`).
- **Legacy UGUI `Text` + builtin `LegacyRuntime.ttf`**, not TMP — TMP essentials aren't imported; keeps UI fully code-driven. Will revisit in the UI redo (mockups spec Nunito/Fredoka via a theme SO).

**Tech debt:**
- **★ UI REDO — DONE (2026-07-17).** `panel.station` rebuilt against ALT `42:2` — see the *UI redo* addendum at the end of this entry. Floating recipe popup + under-building queue slots shipped; `UI-Inventory.md` reconciled (`world.queueSlots`).
- **Totals-popup ✕ button oversized — FIXED** in the UI redo (now a properly-sized circular close via `UiFactory.CircleButton`).
- **Silo / Order Board open an empty panel** if tapped (registered but no recipes). Harmless; they get real panels in M3/M7. *(In the redo, the popup title/tiles just render empty for them — still harmless.)*
- **`LitMaterial(Color)` helper now in 3 places** (`GameBoot`, `StationView`, and `WorldState` uses an Unlit variant) — at rule-of-three; extract a `View/Materials` helper next time one is touched.

**Assumptions:**
- **`Time.timeAsDouble` is the session clock.** Absolute-timestamp storage means real offline progress is a later swap of the clock source (§13), not a rewrite. Monotonic within a session; fine.
- **Uncapped resources** (M2). `IsCollectionPossible` is generic so M7 adds `&& storage-has-room` as another false-reason with **no change to the tap branch** — verify that stays true when M7 lands.
- **Panels are modal-ish / one at a time** — only one station panel open; tapping the money counter toggles the totals popup independently. If the ALT redo makes recipe selection a non-modal floating popup, input-capture (the `IsOverUi` guard in `InputRouter`) may need revisiting.

**Gotchas for later milestones:**
- **★ Playmode FREEZES in the background during MCP verification** — `Time.timeAsDouble` stays `0`, `Update()` never runs, jobs never tick, Game View screenshots go stale. Fix: `Application.runInBackground = true` at runtime (I also set `PlayerSettings.runInBackground`, but it did not serialize to `ProjectSettings.asset` — didn't `SaveAssets`, so it may not persist). **For any timed/animated verification, set `Application.runInBackground = true` first, then let real seconds pass between MCP polls.** Screenshots also need the editor to repaint (`InternalEditorUtility.RepaintAllViews()`), or just poll state via `script-execute` + logs.
- **Can't inject synthetic pointer input via MCP.** Verify input-driven behavior by publishing the underlying `input:*` intent onto the bus (reach it via reflection: `GameBoot._bus`), not by faking a click. Reserve the literal click for the human gate.
- **The bus + economy core are constructed in `GameBoot` and handed to Systems/Views via `Init(...)`.** That's composition-root wiring, not a system-to-system reference. New Systems/Views follow the same pattern — subscribe in `Init`, unsubscribe in `OnDestroy`.
- **Core emits directly** (it holds the bus), so Systems do **not** "republish" core events — they only pump `Tick` and translate intents. Don't add a republish layer.
- **`ResolveKind` (M2, local) vs `EffectType` (§3.1, M5)** are intentionally separate. M5's resolver maps `ResolveKind` sites to `EffectType` effects; do not merge the enums or make M2 import the effect schema.
- **Recipe ids are `field.wheatGrow` / `field.cornGrow` / `field.fallowWheat` / `field.fallowCorn`.** `input:jobQueueRequested {stationId, recipeId}` uses these.

### Addendum — panel.station UI redo (ALT `42:2`), 2026-07-17
**View-only rework; Core/Systems untouched.** The M2 economy loop is model-agnostic and was reused verbatim — same intents (`input:stationTapped` / `input:jobQueueRequested` / `input:jobCancelRequested`) and domain events. Verified through the real bus (reflection, per the input-injection gotcha): queue → run → block → collect (+2) → cancel refund (+1); slot-tap raycast resolves to the right `QueueSlotTag`; no console errors. **User played and verified.**

**Built:**
- **`StationPanel` → floating recipe popup** near the building (screen-space, tracks the building's screen point): recipe icon tiles → selected recipe's `have/need` + timer detail → one `Queue` action. The bottom-anchored recipe **and** queue list are gone.
- **`WorldState` → under-building queue slots** (`world.queueSlots`, new in-world element): filled / running (mini fill) / empty, shown only while the queue is non-empty. **Ready icon recoloured amber → void-accent violet** (`#8B5CF6`, StyleGuide "ready"). Above-building progress bar unchanged except colour-from-theme.
- **`InputRouter`** — tap on a filled slot → `input:jobCancelRequested` (checks `QueueSlotTag` before `StationTag`); empty-slot colliders are disabled so their taps miss.
- **`Hud`** — warm-cream totals popup (`27:2`) with a correctly-sized circular ✕; dark debug menu (`22:2`) exposing only the *working* M2 cheats (+wheat/+corn, reset) — no inert buttons for unbuilt features.
- **`UiThemeSO` (new SO, `Assets/Data/SO/UiTheme.asset`)** — every colour / type size / radius / font role, seeded from StyleGuide + the mockups. `UiFactory` reworked to be theme-driven with a runtime rounded-rect 9-slice sprite generator (chunky rounded chrome, no imported atlas).

**Deviations / decisions:**
- **`GameBoot` gained a `[SerializeField] UiThemeSO uiTheme`** (fail-loud if unassigned) + calls `UiFactory.SetTheme` before building any UI, and threads the theme into the three view `Init(...)`s. This is composition-root **wiring**, not game logic — the layer boundary holds.
- **Recipe tile labels are View-derived** (output resource name; `"Fallow "` prefix for input-less recipes) because `RecipeModel` carries no display name and Core was out of scope. A proper `RecipeSO.displayName` (Data + projection) is a small later pass.
- **Resource icon chips use one uniform placeholder colour** (`theme.resourceChip`); real per-resource icons drop into SO slots later (placeholder policy). Recipe tiles/slots are colour-uniform, differentiated by label.
- **Queue-slot visibility:** the row shows only when the queue is non-empty (keeps idle stations clean) rather than always-on empty boxes. Still an `world.station`-attached world element, driven off Core queue state.

**Gotchas for later:**
- **Queue slots ride a NON-billboarded row anchored in front of the building (`−Z`, lifted `+Y`), with each slot billboarded individually.** First attempt billboarded the shared anchor, which sent the local `−Y` slot offset *under the ground plane* → occluded/invisible. If you touch slot placement, keep them above `y=0`.
- **Slot colliders enable/disable per frame with fill state** — a raycast test that publishes queue events and raycasts in the *same* synchronous call sees stale collider state (enable happens in the next `Update`). Let a frame pass before asserting.
- **`UiFactory` is theme-driven via a static `SetTheme` set once at boot** — new UI reads `UiFactory.Theme`; don't build UI before `GameBoot` sets it.
- **`docs/UI-Inventory.md` reconciled:** added `world.queueSlots`; `panel.station` contents #4 (upgrades) + #5 (pet slot) remain **TBD/deferred** (not in this surface).

### Architecture pivot — editor-authored scenes + prefabs, 2026-07-17
**User decision: the code-generated-scene approach is rejected wholesale.** The project now follows standard Unity practice; CLAUDE.md rule 4 encodes it. Pre-pivot state is checkpointed at `4b8d12d` (one revert rewinds the whole pivot). Decisions confirmed with the user: pure-C# Core stays, C# event bus stays, level layout lives in the scene.

**Built:**
- **`Farm.unity` is a real authored scene** — ground plane, camera (+`CameraController`), light, `EventSystem`, two canvases (`StationPopupCanvas` sort 10, `HudCanvas` sort 20) with fully authored panels, three station prefab instances (`Field`, `Silo`, `OrderBoard`) under `Stations/`, a `Systems` object (`Producer`/`InputRouter`/`WorldState`), and `GameBoot` with every reference wired in the inspector.
- **Prefabs (`Assets/Prefabs/`)** — `Stations/Station_{Field,Silo,OrderBoard}` (authored body + `StationView` with its `StationSO`); `World/StationStateWidget` (billboarded bar + ready icon + QueueRow anchor) and `World/QueueSlot`; `UI/{RecipeTile,IngredientRow,ResourceRow,CheatButton}` list-item templates.
- **Art assets (`Assets/Art/`)** — 9 URP materials (station tints, ground, progress/ready/slot/chip) and 10 rounded-rect 9-slice sprite PNGs (the old runtime sprite generator, baked to real assets).
- **`UiFactory` deleted.** Panels are authored; components hold `[SerializeField]` refs to their texts/buttons/templates. Dynamic-count content (tiles, ingredient/resource rows, cheat buttons, queue slots) runtime-instantiates authored templates.
- **`GameBoot` → slim composition root** (~half its old size): validates SOs, builds the core (bus/pool/wallet/catalog/jobs), discovers scene-placed `StationView`s (GameObject name = Core instance id, uniqueness validated), injects services via `Init(...)`. Creates no GameObjects.
- **SO cleanup** — `StationSO` lost its placeholder-art fields (visuals live in prefabs now); `GameConfigSO` lost `prePlacedStations` + environment colors (scene/material-owned); `UiThemeSO` slimmed to runtime *state* colors only (ink/warning/accent/accentText/lockedBg/lockedText) — static chrome is baked into prefabs. `StationTag`/`QueueSlotTag` folded into `StationView`/`QueueSlot`; `GridProjection` deleted (placement is transform-authored).

**Verified in play mode** (bus-injection per the input gotcha): boot clean, panel opens with 4 field recipes + selection + have/need ✓, queue slots fill with running mini-bar, ready icon hops on completion, totals popup + debug menu render from templates. No console errors.

**Gotchas for later:**
- **Rule 1 split (CLAUDE.md):** game data → SOs; static presentation → the owning prefab/scene. Don't re-add chrome fields to `UiThemeSO`.
- **Station instance ids are scene GameObject names** (`Field`, `Silo`, `OrderBoard`). Rename in scene = rename the Core id. Boot throws on duplicates/missing SO.
- **`Application.runInBackground = true` needed again this session** for MCP playmode verification (same background-freeze gotcha as M2; it does not persist).
- **Editor-scripted authoring is one-shot scaffolding** — the prefabs/scene are now the source of truth; edit them in the editor (or via MCP gameobject/prefab tools), never by re-running a builder script.

---

## Milestone 03 — Order Board
**Status:** ✅ Complete · **Commit:** `<this commit>` · **Date:** 2026-07-20

**Built:**
- **Core** (pure C#, boundary held): `OrderModel`, `OrderConfigModel`, `XpConfigModel`; `OrderPricing` (payout = Σ(qty×baseValue)×mult, floored at 1), `OrderGeneration` (`System.Random` injected, sellable∩producible candidate pool, tier-weighted by level, no repeated good on a card), `OrderBoard` (slots, absolute-timestamp refill timers, fulfill/skip, orders never expire), `Progression` (level starts at 1 and **never increments** — M8 owns that — plus `xpTotal`). `ResourceModel` gained `BaseValue`/`Sellable`/`Tier`. `ValueResolver` gained `OrderPayout`/`OrderSlots`/`XpGain` kinds (passthrough; M5/M6 give teeth).
- **Data:** `OrderConfigSO` + `XpConfigSO` (assets authored, wired into `GameConfig`), `ResourceSO.tier`. Corn `baseValue` 2→3, tier 1; wheat already `sellable=false` from M1. `BootValidator` extended.
- **Systems:** `OrderBoardSystem` (pumps `Tick`, routes fulfill/skip + debug add-money), `ProgressionSystem` (awards XP off `job:collected` flat + `order:fulfilled` carried), `GameBoot` builds the order/progression graph (producible set derived from recipe outputs).
- **View:** `OrderBoardPanel` (self-selects on the Order Board station type off the shared `StationPanelRequested`) + `OrderCard`/`OrderGoodChip` prefabs, authored against Figma `14:2`. HUD gained a `+$100` cheat and a debug `Lv N · X XP` readout (no level/XP HUD — that's M8).
- **Tests:** `Assets/Tests/` (new EditMode asmdef) — 20 tests, all green (pricing, wheat exclusion, level scaling, refill window, never-expire, seed replay).

**Verified:** 20/20 EditMode pass. Play-mode via bus injection: 3 corn slots, tap opens board, Fill disabled at 1-vs-3 corn, fulfill paid +108/+14 & consumed goods, skip free, job-collect +2 XP with level frozen at 1, `+$100` worked, panels are one-at-a-time. Screenshots confirm render. **User play-tested and approved.** Literal finger-tap→raycast still human-only (MCP can't inject pointer input).

**Deviations from the plan:**
- **Dropped the planned `OrderBoardPanelRequested` event.** Both panels self-select off the existing `StationPanelRequested` by station type — one fewer event.
- **Systems named `OrderBoardSystem`/`ProgressionSystem`** (plan said `OrderBoard`/`Progression`) to avoid colliding with the Core classes they drive.
- **`StationPanel` now closes for non-producers** (was: opened an empty panel in M2). Consequence: tapping the Silo opens nothing until M7 builds `panel.silo` — intended (no inert UI), user-confirmed.

**Tech debt:**
- `StationPanel`'s `NoRecipes` row is now dead (non-producers never open it) — left in the prefab, harmless.
- Goods chips use one placeholder tan tile + text label; a parallel session is generating real resource icons (`ResourceSO.icon` field landed from that work; not yet wired here).
- External balance app can run the Core rules but can't read `.asset` files — needs a small SO→JSON export when wanted.

**Assumptions:**
- **Order seed fixed at 12345** on `GameBoot` (0 = random) — reproducible orders for testing.
- **Tier weighting is unit-tested only** — corn is the sole sellable good in-scene, so every live order is corn until M4 adds producers.
- `OrderConfigSO` numbers (60s refill, ×12 cash, ×1.5 XP) are first-guess placeholders — tune in the inspector.

**Gotchas for later milestones:**
- **Order-slot count reads through the seam** (`OrderBoard.SlotCount` via `ResolveKind.OrderSlots`); the board grows the moment the resolved value rises (M6 order.slots / M8 level-raise) with no rewrite — `Tick` already appends empty refilling slots up to it.
- **Editor mouse-death gotcha:** the Device Simulator disables the Mouse device → whole editor untappable. Fixed permanently by `Assets/Editor/EditorMouseGuard.cs` (editor-only). If input dies, check the guard exists before hunting a code regression.

---

## Milestone 04 — Build & Manage Stations
**Status:** ✅ Complete · **Commit:** `<this commit>` · **Date:** 2026-07-20

**Built:**
- **Core** (pure C#, boundary held): `BuildSystem` (place/move/demolish rules — per-type cap, grid occupancy, wallet charge + 50% demolish refund, job-queue register/unregister), `StationTypeModel`, `JobSystem.Unregister`. `StationGrid` is now actually populated (pre-placed stations register into it at boot — M1 built it but nothing filled it). New events: intents `PlaceRequested`/`MoveRequested`/`DebugDemolishLastRequested`; facts `StationBuilt`/`StationMoved`/`StationDemolished`; dormant `UnlockGranted` (listened, fired by M8). Cost + cap read through new `ResolveKind.BuildCost`/`StationCap` seams (passthrough; M6/M8 give teeth). `Progression.StartingLevel` const.
- **Systems:** `GridProjection` (cell↔world, reintroduced after the pivot deleted it — origin-centered, half-cell centers), `StationRegistry` (the station-lifecycle system: translates place/move/demolish intents → BuildSystem, and instantiates/moves/destroys the type's authored prefab from the Core facts; owns the shared live `roots` map). `GameBoot` rewired (builds grid + BuildSystem, registers pre-placed via `RegisterPreplaced`, injects the shared roots). Build XP in `ProgressionSystem` (off `StationBuilt`).
- **View:** `BuildMenu` + `BuildMenuEntry` (roster tray, 4 states available/locked/cap/can't-afford, drag-to-place), `PlacementController` (ghost = prefab mesh tinted green/red, snap, validity preview, emits place/move), `InputRouter` long-press pickup, `CameraController` pan-suppress during a ghost drag. `WorldState` reconciles its per-station rigs against the live roster each frame. Hud debug "Demolish Last".
- **Data/assets:** `StationSO` +buildCost/cap/unlockLevel/placeholderColor/prefab; `GameConfigSO` +stationRoster(8)/refundPercent; `XpConfigSO` +perStationBuilt. New: Workshop SO+prefab+material, 4 locked-type SOs (Henhouse/Pasture/Creamery/Bakery, SO-only), 2 ghost materials, authored build menu (button + tray + `BuildMenuEntry` prefab).

**Verified:** 20/20 EditMode tests pass. Play-mode via bus injection: place (money −cost, +build XP, prefab snapped to cell), demolish (+50% refund), move (free, relocated), cap enforced (3rd Field throws); build menu renders all 4 state markers. User playtested.

**Playtest bug-fix pass (folded into this commit):**
- **BUG-01 — camera panned over UI.** `CameraController` read the raw pointer and panned on any press, ignoring UI (it's not in the UGUI raycast pipeline, so the UI's raycast-blocking alone can't stop it). Added the `!IsOverUi()` guard `InputRouter` already uses. *(Pointer drag/long-press not MCP-testable — user confirms.)*
- **BUG-02 — build button too high.** `BuildMenu` repositions it: bottom-left when closed, above the tray when open (serialized closed/open positions).
- **BUG-03 — XP leaked pre-M8.** Order-card `+XP` and the HUD debug `Lv/XP` readout deactivated until the level/XP milestone. XP still accrues invisibly.
- **BUG-04 — menus didn't coordinate.** New `ExclusiveUiOpened` event: build menu / station panel / order board / debug menu publish on open and close on another's open. Stacking popups (totals, confirmations) unaffected.

**Deviations from the plan:**
- **Move = long-press → drag-drop** (user decision), not the mockup's tap-to-confirm.
- **Demolish = debug-only** (user decision) — Core has the full 50%-refund path; the player-facing gesture is deferred. The debug button demolishes the last-built station.
- **Locked types = SO + tinted-primitive thumbnail only** (user decision) — no prefabs until M8 makes them placeable (`BootValidator` requires a prefab only for `unlockLevel == StartingLevel` types).
- **One `StationRegistry` handles both intent→Core and Core→GameObject**, collapsing the plan's separate `BuildPlacement` + `StationRegistry` (KISS).
- **Ghost is the real prefab mesh tinted green/red, opaque** (not translucent) — placeholder, upgradeable.
- **Fixed a latent M3 scene bug:** the three UI canvases (HudCanvas/StationPopupCanvas/OrderBoardCanvas) were saved `m_IsActive: 0`, so the HUD was invisible on a fresh play. Set active in `Farm.unity` directly (a scripted SetActive kept reverting on domain reload).

**Tech debt:**
- **Producible order-pool set is frozen at boot** (from placed stations' recipe outputs). Building a new-good producer type at runtime won't widen the order pool until a later milestone refreshes it. Not hit by M4 (only Field/Workshop placeable; neither adds a good).
- **Demolishing a busy station drops its queued jobs' consumed inputs** (no refund) — M4 only demolishes fresh empty stations.
- **`Hud._progression` is now unused** (its XP readout is hidden) — kept for M8's real XP HUD.
- **Order-card XP hidden by deactivating the GameObject** — M8 reactivates + wires the real XP display.

**Assumptions:**
- **Build costs / unlock levels / caps are first-guess placeholders** (Field 50/cap2, Workshop 150, Silo 300, OrderBoard 250; Henhouse Lv3, Pasture Lv4, Creamery Lv5, Bakery Lv7) — all `StationSO`, tune freely.
- **Grid convention: origin-centered, cell centers on half-integer world coords** (cellSize 1). `GridProjection` must match how pre-placed stations were authored — verified against the scene (cell (9,15) → (−0.5,0,0.5)).

**Gotchas for later milestones:**
- **`StationRegistry` owns the shared `roots` dict**; CameraController/StationPanel/WorldState read it live. A runtime station becomes visible everywhere by being added to that one dict — don't cache a snapshot.
- **Runtime-placed station ids are `{type}#{n}`** (`workshop#0`); pre-placed use the scene GameObject name (`Field`, `Silo`, `OrderBoard`). Cap counting is by StationType across both.
- **Canvases must stay `m_IsActive: 1`** in Farm.unity — nothing activates them at runtime. If the HUD vanishes on play, check the canvas active flags first.
- **The MCP can't assign a `Transform`-typed serialized field** (its converter modifies-in-place instead of assigning). `GameBoot.stationsParent` is a `GameObject` for this reason; use `.transform` in code.
- **Editor scene-save didn't reliably persist a scripted `SetActive` through a domain reload** — for durable scene-state flags, edit the `.unity` YAML directly + `scene-open` to reload.
- **Camera pan is not in the UGUI raycast pipeline** — it reads the raw pointer + a math-plane ray, so UI must be excluded via `IsPointerOverGameObject()`, not by raycast-blocking. Any new raw-pointer world gesture needs the same guard.

---

## Milestone 05 — Station Upgrades & the Effect System
**Status:** ✅ Complete · **Commit:** `<this commit>` · **Date:** 2026-07-20

**Built:**
- **The Effect system spine (§3), pure C# in Core.** `Core/Model/Effect.cs` — `Effect`/`Trait`/`EffectValue`/`Condition` + the **full** enum vocabularies (`EffectOp`, `EffectType`, `TriggerType`, `ConditionType`); only the passive own-station subset resolves this milestone, the rest is authored-once vocabulary. `Core/Rules/EffectResolver.cs` — the §3.5 stacking math (`(base+ΣFlat)·(1+ΣPct/100)·ΠMult`), type-agnostic. `Core/Rules/TraitDescription.cs` — the §3.6 procedural sentence generator (the three reference cases pass).
- **The `resolve()` seam got its teeth.** `ValueResolver` now folds real effects into `station.speed/yield/cost/queueDepth` + `xp.gain`; **speed is the one inversion** (resolve the speed factor vs 1.0, then `duration / factor` — additive stacking, not compounding). `station.cost` + `xp.gain` are wired even though no M5 upgrade emits them (so M9 Thrifty / cost-affinity actually apply). Call sites (JobSystem/Progression/BuildSystem/OrderBoard) were untouched — pure forward extension, exactly what decision #2 bought.
- **`UpgradeSystem` (Core)** — per-station-instance tiered purchases (charges wallet, bumps tier, emits `effects:recalculated`) AND the M5 `IEffectSource` (own-station passive effects only). `Systems/UpgradesSystem` MonoBehaviour translates `input:upgradePurchaseRequested` + keeps registration synced to `station:built/demolished/game:reset`.
- **Data:** `UpgradeSO` (tiered, per-tier cost + `Effect[]` authored in the inspector — the one model crossing into Core unprojected, §14) + `StationSO.upgrades`. Three Field tracks authored (`Upgrade_Field_Speed` 3× +25% speed, `_Queue` 2× +1 depth, `_Yield` 2× +1 yield). `BootValidator` validates upgrades + normalises effects (triggerChance 0→100, range required for local/pet types).
- **UI (built to an approved Figma mockup first — see deviations):** `panel.station` gained a **Recipes | Upgrades** tab (variant B: Field title + ✕, tabs beneath). `UpgradeRow` prefab = `pattern.purchaseRow` (procedural description + tier + cost + Buy), states available/can't-afford/maxed. `View/UpgradeRow.cs` + reworked `StationPanel`.
- **Tests:** `Assets/Tests/EffectSystemTests.cs` — 16 tests (resolver stacking incl. "two +25% = +50% not ×1.5625", speed inversion, own-station scope, purchase rules, the three §3.6 sentences). Full suite 36/36 green.

**Playtest bug-fix pass (folded into this commit):**
- **BUG-01 — upgrades not shown on the panel.** `StationPanel` displayed *base* recipe time/output, so a bought speed/yield upgrade never appeared (the job WAS faster — 30s→24s — the label just lied). Added `JobSystem.ResolvedDuration/ResolvedOutput` Core queries (mirror the `QueueDepth` precedent); the panel reads those for the open station. Fixed the yield display in the same pass.
- **BUG-02 — queue-depth upgrade didn't add a slot.** Functional cap was already correct (you *could* queue a 4th job); only the in-world slot row was frozen — `WorldState` built the slots once at station spawn and never rebuilt. Now it polls resolved `QueueDepth` each frame and rebuilds on mismatch (self-heals on purchase and on reset; no new deps).

**Deviations from the plan:**
- **The milestone doc said "upgrade rows in `panel.station`", but the chosen mockup (`42:2`) had deliberately removed upgrades from that surface and left the opener "TBD".** Surfaced the contradiction; user chose **a Recipes|Upgrades tab in the popup**. Per the user's standing preference, the tab layout was **mocked up in Figma and approved before any Unity authoring** (frames `53:2` variant A / `56:2` variant B — B chosen). This is the durable rule now: new/changed UI → Figma mock + approval → then author.
- **`input:upgradePurchaseRequested` payload is `{stationId, upgradeId}`** (§15 lists only `{upgradeId}`) because station upgrades are per-instance (§3.2 "own station").
- **Description generator uses ASCII `-` and `×`** (spec text shows a typographic `−`). Trivial; tests assert the ASCII form.
- **`Progression.AwardXp` already routed through the seam since M3**, so the `xp.gain` "wiring" was a no-op edit — the site was pre-threaded. Good.

**Tech debt:**
- **Maxed upgrade row stays white**; the approved mockup greyed it slightly. Cosmetic, left (state reads clearly from "Lv N · Maxed" + no button). Would need a rowBackground ref + theme color.
- **Long descriptions wrap to two lines** in the narrow popup ("+1 queue depth at its / station."). Readable; widen the popup or shorten the phrasing if it bugs.
- **`local.*/global.*/order.*/build.cost/storage.cap/egg.chance/pet.*` are defined but not resolved** (per the plan's incremental schedule — M6/M7/M9/M10). `WithinRangeStation`/`pet.effectStrength` are declared-but-unused vocabulary (summary Open Items).

**Assumptions:**
- **Station upgrades are per-instance** (§3.2 "own station") — a track bought on `Field#0` does not affect `Field#1`. If design wants per-type, the registry keys + payload change.
- **Upgrade costs/tiers are first-guess placeholders** (speed 50/120/250, queue 80/200, yield 100/260) — all on the `UpgradeSO` assets, tune in the inspector.

**Gotchas for later milestones:**
- **All reflection lives in the MCP verification harness (`script-execute`), NOT the game.** Shipping code is reflection-free; the bus dispatches via a typed `Dictionary<Type,Delegate>` cast-and-call. Don't mistake the test-driving scripts for game code.
- **Speed inverts in the seam** (`ResolveKind.RecipeDuration` → `duration / speedFactor`). Every other kind applies the resolved value directly. A new speed-like "faster = smaller" stat needs the same inversion; don't add a `Local/GlobalSpeed` resolve without deciding how it composes with `StationSpeed`.
- **`WorldState` now polls resolved `QueueDepth` every frame and rebuilds the slot row on change** — don't reintroduce a cached slot count. Any value the View shows that an effect can change must be *read live*, not captured at build (the M5 display bugs were exactly this: base vs resolved).
- **The panel shows RESOLVED time/output** via `JobSystem.ResolvedDuration/ResolvedOutput`. New per-station displays should add a Core query, not resolve in the View (rules stay in Core).
- **`UpgradeSystem.Collect` is own-station only** (`ctx.StationId == null` returns nothing). M6's global/universal source is a *second* `IEffectSource`; add a composite rather than overloading this one.
- **`UpgradeSO` tiers are ADDITIVE** — authoring three "+25%" tiers stacks to +75% via the resolver. Do NOT author tier N as the cumulative total.
- **The `UpgradeRow` prefab + the popup tab surgery were authored via one-shot editor scripts** (as the other UI prefabs were); they are now source-of-truth — edit in the editor / via MCP, don't re-run the builders.

---

## Milestone 07 — Storage & Silo
**Status:** ✅ Complete · **Commit:** `263c105` · **Date:** 2026-07-21

> **M6 was SKIPPED** (user decision, this session). The plan's order was M6 → M7; we went M5 → M7. See
> *Deviations* for what M6 owed to later milestones and how much of that debt is now paid.

**Built:**
- **Storage is ONE SHARED POOL, Hay Day's silo model** — *not* the spec's per-resource caps. `ResourcePool`
  gained a single `Capacity` (base from `GameConfigSO.startingStorageCapacity`, resolved through the effect
  seam), `TotalStored` summing every good, and `HasRoomFor` / `HasRoomForAll`. 40 wheat + 10 corn fill a
  50-pool exactly as 50 wheat would, so hoarding any one good squeezes every other.
- **Capacity gates COLLECTION, never addition.** `ResourcePool.Add` stays dumb and never clamps (§4.4 forbids
  destroying output). `JobSystem.IsCollectionPossible` gained storage as a second false-reason and
  `IsStorageBlocked` distinguishes "ready, tap me" from "refused, go free space". **The tap-resolution branch
  did not move** — exactly what M2's generic predicate was built for.
- **`EffectScopes` (`Core/Model/Effect.cs`) — the M6 carry-forward slice.** Reach is now a property of the
  effect TYPE (OwnStation / Global / LocalRange / PetRange), so `UpgradeSystem.Collect` resolves both scopes
  through one mechanism instead of a branch per type. Exhaustive switch: a new `EffectType` throws rather than
  defaulting to a scope nobody chose. This is what lets a Silo-sold `storage.cap` reach a station-less capacity
  read, and a Workshop-sold `global.speed` reach every station's job timer.
- **`global.speed/cost/yield` now fold into the SAME additive pool as their `station.*` counterpart** —
  `+25%` station and `+25%` global read as `+50%`, not `×1.5625` (§3.5). Reach never earns its own separate
  multiplication. `ValueResolver.Applied` gained a two-type overload for this.
- **`panel.silo`** (`View/SiloPanel.cs` + authored `SiloCanvas`): capacity bar, a STORED contents list (goods
  held, answering "what is taking up my room"), and one tiered EXPAND row reusing M5's `UpgradeRow`. Self-selects
  on station type off the shared `StationPanelRequested`, exactly as `OrderBoardPanel` does.
- **`world.storageFull`**: a still (deliberately non-hopping) warning triangle on `StationStateWidget`, using the
  real `Assets/Art/UI/Icons/storagefull.png` the asset pipeline had already produced.
- **Data:** `GameConfigSO.startingStorageCapacity` (30), `Upgrade_Silo_Cap` (3 tiers, uniform +25, $120/$300/$700)
  on `Station_Silo.upgrades`. `BootValidator` requires capacity > 0.
- **Tests:** `Assets/Tests/StorageTests.cs` — 18 tests. Full suite **55/55 green**.

**Deviations from the plan:**
- **★ The spec's §7 storage model was REPLACED, by user decision.** §7 says "each resource has its own cap" and
  M7's *Do NOT Build* list explicitly named per-resource cap upgrades as out of scope. The user first asked for
  per-resource caps *with per-resource upgrades* (the inverse of the spec), then — after comparing against how
  Hay Day actually works — settled on **Hay Day's shared pool**. Recorded in `docs/UI-Mockups.md`.
  **`docs/VoidDay-Spec-unity.md` §7 is now STALE and still describes per-resource caps.** Fix it before anyone
  treats it as authoritative.
- **★ M6 skipped.** Confirmed by reading M7's Context (it depends on M1/M2/M5, not M6) and by the seams: the
  M6 sites (`OrderPayout` / `OrderSlots` / `BuildCost`) are still passthrough at `ValueResolver`, so nothing
  broke — decision #2's whole purpose. **The only hard M6 dependency was M11** (Dopamine Rain *is*
  `global.speed +25%`), and that debt is now **paid**: the scope mechanism plus the global-speed fold both
  landed here, verified by `GlobalEffect_ReachesEveryStationsResolve`. M11 needs no M6 work.
- **`panel.silo` was redesigned and re-mocked.** Frame `19:2` (per-resource) is superseded by **`65:2`
  "panel.silo v2 (shared pool)"**, built in Figma and approved before any Unity authoring, per the standing
  preference. `19:2` is kept and marked superseded in the manifest.
- **Money, not Hay Day's expansion materials.** A true copy needs new material resources, a crate drop source,
  a material inventory, and a non-money purchase path — its own milestone. Deliberately not attempted.
- **One pool, not Hay Day's Silo + Barn.** The Barn slot is already the Workshop (R3 #12), and wheat + corn is
  nothing to split. Revisit when products exist (M8+).
- **`StorageFull` carries the resource that was turned away** — informative for a toast, *not* a per-resource
  cap. Do not read it as one.
- **Committed inside a combined commit.** A parallel audio session had edited the same shared files
  (`GameEvents.cs`, `GameBoot.cs`, `UpgradeSystem.cs`, `SiloPanel.cs`, several Views), so M7 and the SFX cue
  system could not be split without a non-compiling commit. `263c105` contains both.

**Tech debt:**
- **"54 / 55" does not say "full"** even when a 2-unit harvest is refused. The panel reports the literal number;
  the station reports its own truth. Honest but possibly confusing — switching the panel's warning to "some
  station is blocked" is a small change if it reads wrong in play.
- **Dead space below the EXPAND row.** The panel is sized for a roster that grows; two goods leave it looking empty.
- **`StorageFull` fires only at job completion.** A station that completes with room, and *later* becomes blocked
  because the pool filled from elsewhere, never emits the event. The View polls the predicate so the visual is
  always right; only one-shot reactions (SFX/toasts) miss that case.
- **Spec §7 not updated** (above). Highest-value item here.

**Assumptions:**
- **Capacity 30 and the +25/$120/$300/$700 tiers are first-guess placeholders** — `GameConfigSO` and
  `Upgrade_Silo_Cap`, tune in the inspector.
- **Every good counts 1 toward capacity.** No per-good weight. If a late good should occupy more room, that's a
  new field, not a rework.
- **The literal finger-tap on the Silo was not machine-verified** (MCP still can't inject pointer input — same
  gap every milestone has had). Everything downstream of the raycast was driven through the real bus. User played it.

**Gotchas for later milestones:**
- **★ `UpgradeSystem.Collect` is NO LONGER own-station only** — the M5 log's note about adding a "second
  IEffectSource" for global reach is now **obsolete**. Do not add a composite source; add the effect type to
  `EffectScopes.Of` and it resolves through the existing path.
- **`EffectScopes.Of` is exhaustive and THROWS on an unmapped type.** Adding an `EffectType` without giving it a
  scope fails loudly at first resolve. That is deliberate — map it.
- **Reach types share one pool per stat.** `local.*` (M10) must join `station.*`/`global.*` in the *same*
  `Applied(...)` call, not get its own multiplication, or §3.5's additive rule silently breaks.
- **`ResourcePool.Add` never clamps, by design.** Debug cheats and refunds can push you over capacity; that just
  means blocked-until-spent. Do not "fix" this with a clamp — it would destroy output, which §4.4 forbids.
- **`Effect.resource` is unused for `StorageCap`.** One pool means a resource-narrowed cap effect is meaningless.
  The narrowing still works for `*.cost`/`*.yield` per §3.2.
- **Panel roots are authored INACTIVE in `Farm.unity`** (`OrderBoardCanvas/Panel`, `SiloCanvas/Panel`), matching
  `RecipePopup`/`TotalsPopup`/`DebugMenu`. The *Canvas* stays active (M4's gotcha still holds) — only the child
  panel is off, so it no longer blocks the Game view in edit mode. `Init` already deactivated it at runtime, so
  this changed nothing at play. Edit the `.unity` YAML directly for such flags; a scripted `SetActive` does not
  survive a domain reload.
- **The `SiloCanvas` hierarchy was authored by a one-shot editor script** (as every other UI surface was). It is
  now source-of-truth — edit it in the editor / via MCP, do not re-run the builder.

---

## Milestone 08 — Levels & Unlocks
**Status:** ✅ Complete · **Commit:** `f09e6c1` · **Date:** 2026-07-21

> **M6 remains SKIPPED** (user decision, confirmed again this session). The plan's order was M7 → M8 with
> M6 before both; we have now gone M5 → M7 → M8. See *Deviations* for what M8 paid off M6's behalf.

**Built:**
- **`LevelCurve` (`Core/Rules`)** — the explicit XP→level table (20 levels, no formula, §9). It also owns the
  two numbers `hud.levelXp` renders (`XpIntoLevel` / `XpSpanOfLevel`), because §12.1 is explicit that the View
  *reads* the threshold rather than computing it.
- **`LevelGrants` (`Core/Rules`)** — a dumb accumulator of standing bonuses keyed by `(kind, targetId)`. An
  **empty targetId means "every station type"**, which is how one grant deepens every queue at once.
- **`Progression` now increments.** `AwardXp` → `AdvanceLevels()` → one loop over the level's grant list.
  **One `level:up` fires PER LEVEL CROSSED** — a 3-threshold grant publishes three payloads, so nothing
  downstream has to reconstruct the steps from a jump.
- **★ Gates have exactly ONE home.** A station type's gate is `StationSO.unlockLevel`; an upgrade track's is
  the new `UpgradeSO.unlockLevel`. The level asset never restates them — `ModelProjector.ProjectLevelGates`
  derives them from the roster and `Progression` announces whichever open at the level just reached.
  **Boot validation rejects a `StationType`/`Upgrade` grant authored on a level**, naming the real home.
- **★ The seams were activated WITHOUT touching a read site.** `ValueResolver` gained a *second contributor*
  (`SetGrantSource`) beside the effect source: **a level grant moves the BASE, the effect stack then applies
  on top** (`base + grant`, then Pct/Mult). `ResolveContext` gained **`StationType`** — a cap belongs to a
  *type* and the cap read has no instance, so `StationId` could not carry it. `BuildSystem.Cap` and
  `JobSystem.QueueDepth` each pass it; that is the whole diff at the call sites.
- **`hud.levelXp`** (`View/LevelXpHud.cs` + authored `HudCanvas/LevelXpPill`): badge + XP bar, top-center,
  built to sheet `37:2`. Bar chases its target (`MoveTowards`) instead of snapping; badge does a sine pop on
  level-up. The bar is reset to 0 on level-up so the new level starts from empty.
- **`popup.levelUp`** (`View/LevelUpPopup.cs` + authored `LevelUpCanvas` + `UnlockRow.prefab`), built to
  `24:2`. **Level-ups QUEUE** — each crossed level gets its own screen, "Nice!" advances, the queue drains to
  a close. Publishes `ExclusiveUiOpened("levelUp")` on the *first* screen only; deliberately does **not**
  listen (a celebration is modal, nothing dismisses it but its own button).
- **`UpgradeRow.BindLocked`** — a level-gated track renders visible-but-locked ("Unlocks at level N"), wired
  identically in `StationPanel` and `SiloPanel`. `UpgradeSystem.Purchase` fails loud on a locked track.
- **Debug (§12.7):** a **Level Up** button granting exactly `Progression.XpToNextLevel`. Core owns what
  "enough" is; at the cap the debt is 0 and the award is a no-op, so the cap needs no special case.
  The old "XP has no HUD until M8" readout is now **live** (`L5 · 0/100 xp`) — bar + numbers together.
- **Data:** `Levels.asset` (20 levels, thresholds 0…7000, grants = queue depth / order slots / station caps /
  money), `GameConfigSO.levels`, `UpgradeSO.displayName` + `unlockLevel`, `SfxCue.LevelUp`.
- **Tests:** `Assets/Tests/LevelTests.cs` — 16 tests (crossing, multi-crossing, cap-of-curve, grant
  application per kind, derived gates, reset, and grant+upgrade stacking). Full suite **71/71 green**.

**Deviations from the plan:**
- **★ The reward is MONEY, not eggs.** M8's *Do NOT Build* forbids egg rewards; the mockup's "1 Egg" reward
  beat is therefore filled with a coin + `$N`. Boot validation caps a level at **one** Money grant, matching
  the popup's single-reward block. When M9 lands, eggs join `LevelEntryKind` as a second reward kind.
- **`level:up` carries STRUCTURED entries, not sentences.** §15 says `{level, unlocks, rewards}`; the payload
  is `LevelEntry {Kind, Id, Label, Amount}` and the wording lives as serialized copy on the popup
  (`stationFormat`, `capFormat`, `queueAllFormat`, …). Core supplies facts; the View supplies English.
- **The popup card is a FLOW COLUMN, not the mockup's fixed offsets.** `24:2` positions everything absolutely
  and assumes two unlock rows; three rows collided with the REWARD block. The card is now a
  `VerticalLayoutGroup` + `ContentSizeFitter`, so it grows with content and a level that unlocks nothing
  simply has a shorter card. Visual order and measurements otherwise match the frame.
- **M6's debt, revisited.** M8 needed `order.slots` to actually move, which M6 was to have wired. It moves
  **by level grant**, not by effect — so `ResolveKind.OrderSlots` and `ResolveKind.StationCap` are now
  *grant-only* reads and their **effect half is still passthrough**. M6, if built, adds
  `Applied(..., EffectType.OrderSlots, ctx)` around the existing expression. `BuildCost` and `OrderPayout`
  remain fully passthrough — untouched by this milestone.
- **`EffectsRecalculated` is published on level-up and on reset.** Grants are not Effects, but they change
  *resolved values*, and that event is already the "re-read what you resolved" signal every panel listens to.
  Publishing it avoided adding a `LevelUp` subscription to four Views. Its doc comment was updated to say so.

**Tech debt:**
- **The badge "glow" is a tinted `UI/Skin/Knob` circle** and reads as a grey halo rather than the mockup's
  purple bloom. Needs a real soft-glow sprite (`vfx.levelUp` is still unbuilt too).
- **`sfx.level.up` has no clip** — the cue exists and fires, silently. `SfxCue.LevelUp` was **appended at the
  end of the enum on purpose**: the library serializes cue *integers*, so inserting mid-enum would silently
  reassign every clip below it.
- **Tapping debug Level Up closes the debug menu** (the popup takes UI exclusivity). Repeat cheating means
  reopening the menu each time. User saw it and accepted it.
- **Levels 11–20 are unreachable in practice** at current XP rates and were authored by extrapolation, not by
  play. Treat their thresholds and grants as untested placeholders.
- **`Progression.AwardXp` still resolves XP through `ResolveKind.XpGain`,** so an `xp.gain` effect would make
  the debug cheat's "exactly enough" inexact (it would overshoot). Harmless — nothing authors `xp.gain` yet.

**Assumptions:**
- **The whole curve is a first-guess placeholder** — thresholds (0/20/50/100/175/…/7000), the grant schedule,
  and the money rewards. All in `Levels.asset`; tune in the inspector. L2 lands after roughly two orders.
- **Every station type keeps `cap` and `queueDepth` on its own SO as the base**; a level only ever *adds*.
  Nothing validates that a level's cap grants don't out-run what the grid can hold.
- **Henhouse / Pasture / Creamery / Bakery are now buildable** (levels 3/4/5/7) but **have no recipes
  authored** — their `StationSO.recipes` are empty, so a built one opens a panel that says "NoRecipes".
  That is pre-existing, not new, but M8 is the first milestone where a player can actually reach it.
- **Animation was not machine-verified.** The editor does not tick frames while unfocused (see *Gotchas*), so
  the bar chase and badge pop were never observed in motion. The layout was verified via a camera capture; the
  logic was driven through the real bus. User played it.

**Gotchas for later milestones:**
- **★ `ResolveContext` now has THREE fields** (`StationId`, `ResourceId`, `StationType`). It is a struct with
  optional params, so old call sites still compile — but a new grant-keyed read **must pass `stationType:`**
  or it silently gets only the untargeted pool. `BuildSystem.Cap` / `JobSystem.QueueDepth` are the examples.
- **★ Grant vs. Effect is a real distinction.** A level moves a **base** (flat, pre-stack); an upgrade/trait/
  event scales it (§3.5 additive pool). Do **not** implement a level bonus as an `Effect` — it would join the
  percentage pool and compound wrongly. `LevelTests.AQueueUpgradeAppliesOnTopOfTheLevelRaisedBase` pins this.
- **★ `LevelEntryKind` is ONE vocabulary for two jobs** — authored grants *and* payload lines. Adding a kind
  means: the enum, a `Describe` case + serialized copy on `LevelUpPopup`, an `IconFor` case, a branch in
  `Progression.ApplyLevel` if it is not a standing bonus, and (if it is) a `Granted(...)` read in
  `ValueResolver`. M9's egg reward is exactly this walk.
- **Level 1 must have NO grants** — it is never crossed, so its grants would never apply. Boot validation
  throws, pointing at the station/order SOs as the home for starting values.
- **`Progression.Reset` clears `LevelGrants` and publishes `EffectsRecalculated`.** Any future state that a
  level grants must be cleared there too, or a debug reset leaves it stranded.
- **The Unity editor does not advance frames while unfocused.** `Time.frameCount` stuck at 2;
  `EditorApplication.QueuePlayerLoopUpdate()` does not help, and the Game View render texture goes stale, so
  `screenshot-game-view` returns the last frame from *entering* play mode. Workaround used here: temporarily
  set a Screen-Space-Overlay canvas to **ScreenSpaceCamera** on the main camera and use `screenshot-camera`,
  then revert. That is the only way found to see a new UI surface rendered. (Pointer injection is still
  unavailable — same gap every milestone has had.)
- **`LevelUpCanvas` and the `LevelXpPill` were authored by a one-shot editor script**, as every other UI
  surface was. They are now source-of-truth — edit them in the editor / via MCP, do not re-run the builder.
- **The popup's unlock rows are `Destroy`d then re-instantiated per screen.** Same pattern as every other
  panel; it looks wrong when inspected from a frozen editor (old rows still present) but resolves normally in
  a running frame.
- **Spec §7 is STILL stale** (M7's debt — it describes per-resource storage caps). §9 is accurate as built.
