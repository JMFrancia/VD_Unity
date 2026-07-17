# VoidDay — Unity 3D Port Questions

**How to use:** answer inline under each `**A:**`. Every question has a **Default** — write `d` to take it. Terse is fine.

These are the decisions the 2D→3D pivot forces that **no existing document answers.** The rest of the spec is engine-agnostic and ports without a decision. I've written `docs/VoidDay-Spec-unity.md` already, applying every default below and flagging each as an inference. If you `d` everything, the spec is done as written. Change an answer and I patch the affected section only.

Ordered by how much the answer changes the outcome.

---

## 1. Camera — the biggest visual call

The spec says "top-down." On flat 2D that's unambiguous. On 3D meshes, a pure 90°-overhead ortho camera flattens everything back into silhouettes — you lose the entire reason to be in 3D. HayDay itself uses a tilted ¾ view.

The task brief says 3D here most likely means **3D meshes under an orthographic camera** (not a perspective/free camera) — orthographic keeps the clean top-down read, snap-to-grid math, and clamped pan/pinch all intact. The only real question is the tilt.

**1a. Camera projection.**
Default: **Orthographic.** Keeps grid placement, pan-clamping, and pinch-zoom exactly as specced; no perspective distortion to fight on a phone.

**A:**
Agreed.

**1b. Camera tilt.**
Default: **angled ¾ top-down — camera pitched ~55–60° down, not straight 90°.** Enough to read the 3D silhouettes and let assigned VoidPets sit visibly on top of stations; still reads as "top-down farm." Pure 90° overhead is the fallback if you want the flattest, most map-like look.
Other option: pure 90° overhead (maximally readable grid, throws away 3D depth).

**A:**
Agreed.

---

## 2. ScriptableObject schema — how the data layer is shaped (architectural)

CLAUDE.md replaces the spec's JSON with ScriptableObjects and says the inspector is the tuning UI. Two structural choices follow that aren't in any doc. These set the designer's authoring surface, so they're worth pinning before I build around them.

**2a. SO granularity.**
Default: **one SO asset per entity, created via `[CreateAssetMenu]`** — a `RecipeSO` per recipe, a `StationSO` per station type, a `VoidPetSpeciesSO` per species, etc. This is the idiomatic Unity path and matches CLAUDE.md's "create SO instances so a human can make them in three clicks" and "buildings are data, not subclasses." A single global `GameConfigSO` holds the genuinely global scalars (grid size, camera zoom bounds, starting state).
Other option: one big SO per former JSON file holding a `List<>` (fewer assets, but every edit touches one shared asset and diffs poorly).

**A:**
Agreed.

**2b. Where the Effect model lives, given the Core boundary.**
The effect schema is pure data, and CLAUDE.md forbids `Core/` from `using UnityEngine`. A plain `[System.Serializable]` C# class has **no** UnityEngine dependency, so it can live in `Core/` **and** still be authored in the inspector as a field on a Data-layer SO.
Default: **`Effect` is a `[Serializable]` plain-C# type in `Core/`; SOs in `Data/` hold `Effect[]` fields.** One model, authored in the inspector, resolved headless in Core — no DTO/translation layer.
Other option: SO-only effect data converted to a Core model at boot (adds a mapping layer for no gain here).

**A:**
Agreed.

**2c. Optional/variant effect fields under Unity serialization.** The TS schema leans on optional fields (`resource?`, `trigger?`, `condition?`) and `Condition` has variant payloads. Unity's inspector doesn't do nullable or polymorphism cleanly without `[SerializeReference]`.
Default: **flat structs with enum discriminators + explicit `None` enum members** (e.g. `TriggerType.None`, `ConditionType.None`, `resource == ""` = all). KISS, inspector-friendly, no `[SerializeReference]`. Fail-loud validation at boot catches a malformed combo.
Other option: `[SerializeReference]` polymorphic `Condition` (cleaner types, more machinery).

**A:**
Agreed.

---

## 3. 3D placeholder policy (replaces dead §12.6)

The old policy — "colored rect + text label" — is a 2D artifact. CLAUDE.md's replacement is "primitives and untextured meshes are correct until proven otherwise."

**3a. Station bodies.**
Default: **Unity primitive meshes (Cube/Cylinder/etc.), one silhouette per station type, tinted by a `placeholderColor` on the `StationSO`, URP lit material.** A distinct primitive per station (field = flat quad, henhouse = cube, silo = cylinder…) reads far better in 3D than eight identically-shaped tinted cubes. Mesh choice is an SO field so it's a designer swap, not code.

**A:**
Agreed.

**3b. State display in 3D** (idle / working / ready / storage-full — old §12.6 used a progress bar + bouncing icon + distinct full state).
Default: **world-space progress bar above the station while working; a small hop/bounce tween + floating icon when ready; a distinct tint or icon for storage-full.** All driven off core state, all placeholder-swappable.

**A:**
Agreed. Be sure to add billboard logic for world-space UI.

---

## 4. Asset story — how much to spec now

The 2D spec barely mentions assets (sprite paths in JSON). 3D inflates this: meshes, materials, prefabs, world-space UI, plus the VoidPet models. The question is whether the spec should carry an art/asset plan now or stay primitives-only.

**Default: primitives-only for the prototype; no art pipeline in the spec.** SO asset fields reference **prefabs/meshes/materials** instead of sprite paths, so swapping placeholder→real art stays a designer-side reference change. When you want real art, the `asset_list` skill generates the production doc from this spec — out of scope until the loop is fun. I'll add a short §12.8 saying exactly this and nothing more.
Other option: commission a full asset inventory now (premature — nothing's tuned yet).

**A:**
I'm currently putting together assets to use for the prototype. I'd like these to be injected as we go. I'm using the new asset_list skill to generate assets as we are building. I don't mind using placeholders as we build and swapping out assets as they become available though.

ALSO please note I'm going to use the ui_inventory skill to create UI mockups and pass those in as well for visual reference creating UI.

---

## 5. Anything I got wrong porting the design?

The design itself is settled and I ported it verbatim — Fallow, station-blocking, the effect resolver, rule-generated relationships, all unchanged. This question is only a safety valve.

**5. Any part of the ported design that reads wrong to you in the Unity rewrite?**

**A:**
Looks fine, but just to be safe, launch two agents:
1. With fresh context, checks old spec and new unity spec to see if anything was lost or hallucinated
2. With fresh context, looks through new spec to see if any areas that don't make sense for unity
