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
