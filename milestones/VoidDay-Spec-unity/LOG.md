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
