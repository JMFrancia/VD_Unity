# VoidDay — Game Spec (Unity 3D)

A farming/pipeline-management prototype inspired by HayDay. Portrait mobile, touch-only, WebGL.

**Status:** ported from `VoidDay-Spec.md` (which consolidated the original pitch + three rounds of Q&A, R1/R2/R3) to **Unity 6000.3.7f1 / URP (Universal 3D) / ScriptableObjects**. The *design* is unchanged — it was settled across three Q&A rounds and is engine-agnostic. This document changes only the engine-specific parts: platform, camera, placeholder art, and the data layer (JSON → ScriptableObjects). It is the source of truth for milestone decomposition.

**Companion docs:** `CLAUDE.md` (architecture rules — data-driven via SOs, event-driven via a Core bus, the Core/Systems/View boundary). This spec assumes those rules and does not restate them. `docs/decisions/` holds the pitch and the three Q&A rounds; citations like `R1 #62`, `R2 #9`, `R3 #12` point there.

**Port markers used below:**
- **[3D]** — a decision the 2D→3D pivot forced, resolved to the default in `VoidDay-Spec-unity-questions.md`. Not from the original design. If a question answer changes, only the marked section changes.
- **[SO]** — a translation of "JSON" to the ScriptableObject data layer mandated by CLAUDE.md. Mechanical; no design change.

---

## 1. Core Loop

The player builds stations that convert resources into other resources on timers, then sells the results to the Order Board for cash and XP. Cash buys more stations and upgrades. XP levels the farm, which unlocks new station types, higher caps, and new upgrades. VoidPets are collectable familiars assigned to stations to auto-collect and grant bonuses; pets near each other form relationships that grant further bonuses.

```
resources ──queue job──> station ──timer──> output ──collect──> resources
                                                                    │
                                                              fulfill order
                                                                    │
                                                            cash + XP + eggs
                                                                    │
                                              ┌─────────────────────┼──────────────┐
                                          build/upgrade         level up        VoidPets
                                              │                     │              │
                                              └──── more throughput ─┴──────────────┘
```

**The tension:** stations **block** on uncollected output (§4.4). Every completed job demands a tap. This is deliberate friction, and relieving it is what VoidPets are *for*.

---

## 2. Platform

- **Portrait phone** dimensions. Touch/drag only — no keyboard. Uses the **new Input System** (`com.unity.inputsystem`); legacy `Input.GetMouseButton` does not work in this project.
- **WebGL build** must load and be tappable in a browser. Verification is: press Play in the editor, or open the WebGL build. There is no other gate.
- **[3D] Top-down view of an open grassy area, rendered as 2.5D — 3D-mesh stations plus 2D-billboard creatures — under an orthographic camera** (§12.5). Orthographic preserves the clean top-down read, snap-to-grid math, and clamped pan/pinch. The camera is pitched to an angled ¾ top-down (~55–60° down), not straight overhead, so 3D station forms read and a billboarded VoidPet sits visibly on top of its station. VoidPets render as 2D billboards, not meshes (§10.3, §12.6).
- **[3D] Placeholder art: untextured primitive meshes tinted per station.** See §12.6. (The 2D "colored rect + text label" policy is retired.)

---

## 3. The Effect System

**This is the spine of the game. Build it deliberately; everything else hangs off it.**

VoidPet traits, relationship traits, station upgrades, universal upgrades, and world events are all *the same thing*: something that emits Effects. One schema, one resolver, one description generator. Adding a new effect `type` once makes it available to every system simultaneously.

### 3.1 Schema

**[SO]** The schema below is the original TypeScript (R3) translated to C#. Per CLAUDE.md's Core boundary, `Effect` and `Trait` are **plain `[System.Serializable]` C# types in `Core/`** — no `using UnityEngine`, so they resolve headless in Core *and* serialize into the inspector when a `Data/` SO exposes an `Effect[]` field. One model, authored in the inspector, resolved in Core; no DTO layer.

```csharp
// Core/Model — no UnityEngine dependency.

public enum EffectOp   { Flat, Pct, Mult }
public enum EffectType { /* PascalCase of the §3.2 names: StationSpeed, StationCost,
                            LocalYield, GlobalCost, BuildCost, OrderPayout, XpGain,
                            StorageCap, EggChance, PetEffectStrength, ... */ }
public enum TriggerType   { None, JobQueued, JobCompleted, JobCollected,
                            OrderFulfilled, StationBuilt, PetHatched, LevelUp }
public enum ConditionType { None, AssignedTo, WithinRangePet, WithinRangeStation,
                            ResourceAbove, PlayerLevelAbove }

[System.Serializable]
public struct EffectValue {
    public EffectOp op;
    public float amount;              // +25% => {Pct, 25}; ×3 => {Mult, 3}; +2 => {Flat, 2}
}

// [3D-adjacent inference] Condition is a flat struct with an enum discriminator +
// generic args, rather than a [SerializeReference] hierarchy — inspector-friendly, KISS.
[System.Serializable]
public struct Condition {
    public ConditionType type;        // None = always true
    public string  arg;               // stationType / petId / resource, per type
    public int     amount;            // n, per type
}

[System.Serializable]
public class Effect {
    public string      id;            // internal, for debugging — never player-facing
    public EffectType  type;          // what it touches
    public EffectValue value;
    public string      resource;      // "" = all resources; else scopes to one (see §3.2)
    public int         range;         // grid cells; required by local.* / pet.* types
    public TriggerType trigger;       // None = passive, always active
    public int         triggerChance; // 0–100; treat 0 as "unset" => 100 (validated at boot)
    public Condition   condition;     // ConditionType.None = always true
}

[System.Serializable]
public class Trait {
    public string   id;
    public string   name;             // player-facing ("Cow Lover")
    public Effect[] effects;
}
```

The **trait** carries the player-facing name. Effects have ids for debugging but are never individually surfaced. Optional TS fields become explicit `None`/sentinel values (empty `resource`, `TriggerType.None`, `ConditionType.None`); the boot validator (§ *Data loading* in CLAUDE.md) rejects malformed combinations loudly rather than defaulting past them.

### 3.2 `EffectType` vocabulary

The dotted names below are the design vocabulary; the C# `EffectType` enum members are their PascalCase form (`station.speed` → `StationSpeed`, `pet.autoCollectSpeed` → `PetAutoCollectSpeed`), since dots aren't valid identifiers.

| Type | Touches |
|---|---|
| `station.speed` | job timer at the pet's/upgrade's own station |
| `station.cost` | recipe input cost at own station |
| `station.yield` | job output quantity at own station |
| `station.queueDepth` | job queue depth at own station |
| `local.speed` / `local.cost` / `local.yield` | same, but every station within N grid cells |
| `global.speed` / `global.cost` / `global.yield` | every station on the map |
| `build.cost` | station build cost |
| `order.payout` | cash from fulfilling an order |
| `order.slots` | Order Board slot count |
| `xp.gain` | XP from all sources |
| `storage.cap` | per-resource storage cap |
| `egg.chance` | egg drop rate on order fulfillment (§10.1) |
| `pet.effectStrength` | scales the magnitude of another pet's effects |
| `pet.autoCollectSpeed` | how fast a pet auto-collects |

**`station.*` vs `local.*` vs `pet.*`:** `station.*` is the emitter's own station. `local.*` reaches every station within `range` grid cells (Manhattan, per §10.4). `pet.*` reaches other *VoidPets* within `range` — required by the pitch's "a trait that affects nearby assigned VoidPets" and by the deferred VoidPet Station's area bonus. `range` is per-effect, so different traits can have different reach.

**The `resource` scope:** any `*.cost` or `*.yield` type may carry a `resource` narrowing it to one resource. This expresses the pitch's *"universal cost decrease for specific resource"* — `global.cost` with `resource: "wheat"` means wheat costs less everywhere. Empty = applies to all resources.

### 3.3 `TriggerType` vocabulary

`JobQueued`, `JobCompleted`, `JobCollected`, `OrderFulfilled`, `StationBuilt`, `PetHatched`, `LevelUp`

`None` = passive modifier, always applied. A real trigger fires once on that event, subject to `triggerChance`.

### 3.4 `Condition` vocabulary

`AssignedTo: <stationType>` · `WithinRangePet: <petId, n>` · `WithinRangeStation: <stationType, n>` · `ResourceAbove: <resource, n>` · `PlayerLevelAbove: <n>`

`None` = always true.

`WithinRangePet` requires that **both** pets are currently assigned — an unassigned pet sitting in the menu is not "near" anything. Per R2 #10: *"the condition of that trait only kicks in when they are assigned within range of one another."*

### 3.5 Resolver — stacking math

Effects never suppress each other; all applicable effects apply simultaneously. For a given `type`, resolve in this fixed order:

1. Sum all `Flat` amounts, add.
2. Sum all `Pct` amounts, apply once. (`+25%` and `+25%` = **+50%**, not ×1.5625.)
3. Multiply all `Mult` amounts in sequence.

Predictable, designer-readable, and avoids runaway stacking. Lives in `Core/Rules` — pure C#, testable headless (this is exactly the economy core CLAUDE.md says to test).

### 3.6 Procedural descriptions

One function generates a player-facing sentence from any Trait's effect data. Serves VoidPet details, upgrade menus, relationship popups, and world event popups.

> **Cow Lover:** When assigned to a pasture, 20% chance of ×3 yield on job completion.
>
> **Hard Worker:** +25% speed at its station.
>
> **Thrifty:** −15% recipe cost at its station.

These three are the user's own hand-written examples (R2 #9) and serve as the reference cases for the generator.

---

## 4. Stations

### 4.1 Model

One station type covers everything — the **recipe** decides behavior. Every recipe is a convert; there are no free producers except Fallow (§5.2). **[SO]** This is CLAUDE.md's "one generic Producer component; buildings and fields are data, not subclasses" — a single `Producer` System driven by a `StationSO` + `RecipeSO`, never a `BakeryBehaviour : ProducerBehaviour`.

- **Placement:** snap-to-grid, one station per cell, no overlap. **[3D]** The grid lies on the world **XZ plane** (Y up); cell `(col, row)` maps to a world position, one station per cell.
- **Map:** fixed grid from the `GameConfigSO` (~20×30), entirely buildable, no terrain restrictions.
- **Footprint:** 1×1 for now. `StationSO` carries `width`/`height` so this can change.

### 4.2 Station list

| Station | Produces | Notes |
|---|---|---|
| Field | wheat, corn | Pre-placed at start ×1 |
| Henhouse | eggs | |
| Pasture | milk | |
| Creamery | cream, cheese | |
| Bakery | bread, cornbread, brioche, cheesecake | |
| Order Board | — | Free, pre-placed, sell orders (§6) |
| Silo | — | Pre-placed, storage cap upgrades (§7) |
| Workshop | — | Universal upgrades (§8) |

The Silo and Workshop hold nothing — resources are a global pool of numbers, per the pitch. Both are buildings whose only function is selling upgrades, which is the pitch's "Upgrade Stations: place to purchase specific universal upgrades." **Workshop** is the R3 #12 rename of the pitch's "barn (universal upgrades)."

> **On the name "Silo":** it was briefly renamed to Vault over a HayDay collision (HayDay's silo *holds* crops; ours sells cap upgrades). Reverted — the confusion is theoretical, it's one tap to resolve, "Vault" was never actually approved, and Silo fits the place-noun pattern that "Storage" breaks. It's a `displayName` on the `StationSO`; change it freely once it's on a real button.

### 4.3 Economy

- **Build cost:** money only.
- **Move:** free.
- **Demolish:** 50% refund of build cost.
- **Caps:** per station type, from the `StationSO`, raised by level. Starts at 2 Fields, 1 of everything else.
- **Queue depth:** 3 by default, raised by level and station upgrades.

### 4.4 Job rules

- **Inputs consumed at queue time.** You cannot queue what you cannot afford. Naturally self-limits queue depth early.
- **Cancel:** full refund if not yet started; no refund once running.
- **Blocking:** a completed job's output **sits at the station and the next queued job does not start until collected.** A 3-deep queue only advances on collection. This is the core friction.
- **Collection:** tap the station on the map when it's in the "job complete" state. There is **no collect button** in the panel.
- **Storage full:** collection is refused, station stays blocked, shows a "storage full" state. Queued jobs still run, then block behind it. **Nothing is ever destroyed.**

### 4.5 Station panel

Carries:

- Recipe list (queue a job)
- Job queue display (with cancel)
- Station upgrades
- VoidPet assignment slot

No collect button — collection is a map tap.

**Tap resolution:** a tap on a station **collects if collection is possible; otherwise it opens the panel.** So a ready station collects on the first tap and opens its panel on the second, and a storage-full station — where collection is refused (§4.4) — opens the panel immediately.

> This rule matters more than it looks. A blocked station is exactly the one you want to open, because the panel is where you assign the VoidPet that unblocks it. "Tap only ever collects" would lock you out of the fix at the moment you need it.

---

## 5. Resources & Recipes

### 5.1 Resources

**Raw:** wheat, corn, eggs, milk
**Processed:** cream, cheese, bread, cornbread, brioche, cheesecake

Wheat and corn are the only self-replicating resources (1 → 2). Everything else is a net drain, which makes field count the effective ceiling on the whole economy.

> That ceiling is an observation about the recipe list as specified, not a stated design goal — nobody has decided yet whether it's a feature. Flagged because it's the kind of property that's much cheaper to notice now than after tuning.

### 5.2 Recipes

| Station | Recipe | Speed |
|---|---|---|
| Field | 1 wheat → 2 wheat | fast |
| Field | 1 corn → 2 corn | fast |
| Field | **Fallow:** 0 → 1 wheat | **very slow** |
| Field | **Fallow:** 0 → 1 corn | **very slow** |
| Henhouse | 1 wheat → 1 egg | |
| Pasture | 2 corn → 1 milk | |
| Creamery | 1 milk → 1 cream | |
| Creamery | 2 milk → 1 cheese | |
| Bakery | 2 wheat → 1 bread | |
| Bakery | 1 wheat + 1 corn → 1 cornbread | |
| Bakery | 1 wheat + 1 egg → 1 brioche | |
| Bakery | 1 wheat + 1 egg + 1 cheese → 1 cheesecake | |

**Fallow exists to prevent a softlock.** Because everything is a convert and nothing else produces wheat or corn from nothing, hitting zero wheat would otherwise be permanently unrecoverable (fulfill an order with your last wheat, or convert wheat → egg at the henhouse, and the run is dead — with no starting cash there's no bailout). Fallow is always available and deliberately slow: a safety net, never a strategy.

**Cream is terminal** — produced by nothing else, consumed by nothing. Its only use is order fodder. Intentional.

**Timers are per-recipe and optional.** The pitch says recipes convert resources *"sometimes with a timer"*, so a `RecipeSO`'s timer is optional — an absent (≤0) timer means the job completes instantly. Every launch recipe has one; the schema allows for instant recipes without a rewrite.

### 5.3 Starting state

No cash. 1 wheat, 1 corn. Pre-placed: **1 Field, 1 Silo, 1 Order Board.** (Sourced from the `GameConfigSO` start block — §14.)

---

## 6. Order Board

Free, pre-placed, always on the map. Map-only entry — no HUD button. Tap to open.

- **Slots:** 3, raised by player level and the `order.slots` effect.
- **Refill:** a fulfilled or skipped slot refills after a timer (~60s).
- **Expiry:** orders never expire on their own.
- **Skip:** an X on each order card. Single tap, no confirm, free. Slot immediately begins refilling.
- **Payout:** cash + XP, both derived from the requested ingredients' base values × a multiplier.
- **Orders may request more than the player currently holds** — that's the pull that drives production.

### 6.1 Generation

Procedural. Random pick from resources the player has unlocked a station for, quantity scaled to player level, weighted toward higher-tier goods as level rises. Lives in `Core/Rules` (pure C#, testable) — so its randomness is `System.Random`, never `UnityEngine.Random`, and it operates on the projected `Core/Model` resource objects (§14), never on the `ResourceSO` assets directly.

**Wheat is excluded from the order pool** — it's the one good never sold as-is. Corn is sellable. This is expressed once, as a `sellable` flag on wheat's resource model (projected from its `ResourceSO`, §14); generation reads that flag off the model.

---

## 7. Storage

- **Per-resource caps.** Each resource has its own cap (`ResourceSO.startingCap`).
- **The Silo raises all caps at once** (one upgrade track, applies globally via a `storage.cap` effect).
- Full storage blocks collection per §4.4 — never destroys anything.

---

## 8. Upgrades

Both kinds are **tiered**, with per-tier costs listed explicitly on the `UpgradeSO` (no formula), and both emit Effects (§3).

- **Station upgrades** — bought in the station panel. Job speed, queue depth, output yield.
- **Universal upgrades** — bought at the **Workshop** (a building you place and tap). Global job speed %, global build cost %, order payout %, extra order slot.
- **Silo upgrades** — storage cap. Bought at the **Silo** (a building you place and tap). Raises every resource's cap at once.

---

## 9. XP & Levels

- **XP from:** collecting a job output, fulfilling an order, building a station, hatching an egg.
- **Curve:** explicit table on the `LevelSO` set, no formula. 20 levels for the prototype.
- **Unlocks:** station types and caps are **auto-granted**; upgrades become **purchasable**.
- World events unlock at level 5, and most individual events carry their own minimum level.

> **[SO]** The pitch's separate balance tool that "makes direct changes to that same JSON" is superseded — **the Unity inspector is the tuning UI** (CLAUDE.md). Editing an SO in the inspector *is* the balance edit; no separate tool, no write endpoint. See §16.

---

## 10. VoidPets

### 10.1 Acquisition

Eggs from level-up rewards, plus a small chance on order fulfillment (modifiable via `egg.chance` — this is what R3 #4 asked about: the egg drop rate on order fulfillment). Tap the egg in the hatch popup to reveal the pet.

**No duplicates** — a dupe roll is rerolled into an unowned species.

### 10.2 Species & traits

6 species for the prototype. Traits are **fixed per species**, authored on the `VoidPetSpeciesSO`, never rolled. 1 trait normally, 2 for rarer pets.

**Rarity:** Common / Rare / Epic. Higher rarity = more and stronger traits.

Placeholder species and traits to be invented into SO assets and tuned after play.

### 10.3 Assignment

- One pet per station, assignable to generator stations only.
- Assign/unassign is free and instant.
- **An assigned pet auto-collects** — instantly on job completion, which is what unblocks the station (§4.4).
- **[3D]** The pet renders as a **2D billboard** (the species' flat art on a camera-facing quad, §12.6) on top of its assigned station — **not** a 3D mesh. Stations are 3D; creatures are 2D-in-3D. This preserves the original flat-silhouette VoidPet art exactly and suits the fixed-angle orthographic camera (§12.5). Pets do not rotate in true 3D.

### 10.4 Range

**[3D]** Range is measured in **grid cells, Manhattan distance** — `|Δcol| + |Δrow|` on integer cell coordinates, independent of world units or the camera tilt. Radius comes from the effect/relationship SO. (Because the grid is a fixed lattice on the XZ plane, this is the same integer computation as the 2D spec; the plane changes rendering, not the distance metric.)

### 10.5 Relationships

**Formation:** two assigned pets within range of each other show a heart icon over both heads. After ~30s of continuous proximity, a popup announces the friendship and the traits gained.

**Persistence:** once formed, permanent — survives separation. The *bonus* only applies while they're back in range (enforced by the trait's own `WithinRangePet` condition).

**Multiplicity:** a pet can hold relationships with everyone in range. Bonuses stack per §3.5.

**Content — how the trait is generated:** each species carries an `affinity` field naming an effect type it pushes (Hard Worker → `speed`, Thrifty → `cost`). On befriending, **each pet gains a trait granting a `local.<partner's affinity>` bonus** — you get a taste of what your friend is good at, but only while near them. Magnitude from the relationship SO, scaled by the rarer of the two.

The generated trait is an ordinary Trait (§3.1) with a `WithinRangePet: <partnerId, n>` condition. Nothing special-cases it.

> Name pattern: *"Friendship with \<Pet\>"* → `local.speed +15%`, condition `WithinRangePet: <thatPet>`.

---

## 11. World Events

Unlocked at level 5. Fire on a random interval from the events SO (roughly every few minutes). Most events carry a minimum level.

- **Effects:** the same Effect vocabulary as everything else. No new mechanics.
- **Notification:** per-event, by type. Events with a real effect show a **popup the first time** (to explain), and a **toast every time after**. Flavor-only events are toast-only.
  - With no save (§13), "first time" resets each session — acceptable for now.

**Launch set:** 2 flavor-only events + **Dopamine Rain** — `global.speed +25%` for 2 minutes.

---

## 12. UI

### 12.1 HUD

| Element | Position | Behavior |
|---|---|---|
| Build menu button | bottom left | Tap to open/close |
| Money | top right | Tap opens total-resource popup, tap again to close |
| Level badge + XP bar | top center | |
| Debug menu button | top left | Tap to open/close |
| VoidPet menu button | bottom right | **Only appears once the player has their first VoidPet** |

HUD is screen-space UGUI (`com.unity.ugui`), overlaid on the 3D world.

### 12.2 Placement interaction

Drag-based, HayDay-style:

1. Open the build menu, **drag** a station out of it.
2. The menu **retracts** as you drag off it, so the whole screen is placeable.
3. Drop on a valid cell to place.
4. **Cancel** by dropping on an invalid cell, or by dragging back over the build menu button (which re-opens it).

**Moving an existing station:** long-press to pick up, drag, tap to confirm.

**[3D]** The drag ghost is a translucent instance of the station mesh; the target cell is resolved by raycasting the pointer onto the XZ ground plane and snapping to the nearest grid cell. Valid/invalid cells tint the ghost.

> Tap-then-tap doesn't work here — a ghost can't follow a finger that's been lifted.

### 12.3 Menus

- **Build menu** — available stations + money costs. Locked stations show a lock icon in grayscale.
- **VoidPet menu** — all collected pets; tap one for its details popup.

### 12.4 Popups

- **Level up** — congrats, new level, unlock list, reward list.
- **Hatch egg** — shows egg, tap to hatch, reveals pet.
- **VoidPet details** — picture, blurb, italicized quote in quotation marks, rarity, traits (procedurally described, §3.6), current station assignment.
- **Total resources** — every resource and the amount held.
- **Relationship formed** — the two pets, the traits gained.
- **Generic text popup** — data-driven dialogue window.
- **Event popup** — data-driven.
- **Toasts** — temporary, corner, data-driven.

### 12.5 Camera

**[3D]** Orthographic, angled ¾ top-down (pitched ~55–60° down). **Pan** drags the camera across the XZ plane — computed from the pointer's raycast-to-XZ-plane delta (the same raycast §12.2 uses), so the world point stays under the finger 1:1 despite the tilt; a naive screen-space delta would drift. **Pinch-zoom** changes the orthographic size. Min/max zoom and pan bounds come from the `GameConfigSO`. Panning is clamped to map bounds.

> **Testing note:** pinch-zoom needs two simultaneous touches, so it is **not** reproducible with a desktop-browser mouse — only on a touch device. Tap, drag, and placement all are, provided input binds to `Pointer` (mouse + touch) rather than `Touchscreen`. Zoom aside, this preserves the "tappable in a browser" verification gate (§2).

### 12.6 Placeholder art

**[3D]** Replaces the retired 2D "colored rect + text label" policy. Per CLAUDE.md, primitives and untextured meshes are correct until proven otherwise.

- **Station body:** a **Unity primitive mesh, one silhouette per station type**, tinted by `placeholderColor` on the `StationSO`, URP lit material. Distinct primitives (e.g. field = flat quad, silo = cylinder, henhouse = cube) read far better in 3D than eight identically-shaped tinted cubes. Mesh is an SO field, so it's a designer swap, not code.
- **Working:** a **world-space progress bar** above the station.
- **Ready to collect:** a small **hop/bounce tween** plus a floating icon above the station.
- **Storage full:** a distinct state (tint or icon), visually separate from "ready."
- **Assigned VoidPet:** renders as a **2D billboard** — the species' flat art on a camera-facing quad — on top of its station, **not** a 3D mesh (§10.3). Because the original VoidPet art is flat graphic silhouette work, it lives natively as 2D-in-3D (à la Don't Starve / Paper Mario) rather than being reconstructed into a mesh; this preserves the art exactly and is the intended creature look, not a placeholder. Stations remain 3D meshes/primitives — creatures are the only billboarded world objects besides UI. VoidPets use their **real art immediately** (it already exists); there is no primitive-placeholder phase for pets.

**[3D] Billboarding.** VoidPet billboards and all world-space UI — progress bars, ready/floating icons, relationship hearts — **billboard to face the camera** (rotates each frame to match the camera's orientation) so it stays readable under the angled ¾ camera (§12.5). Because the camera is orthographic and fixed-angle, this is a constant yaw/pitch match, not per-object perspective correction.

All state visuals are driven off Core state by the View layer — the View syncs to state, it never holds a rule (CLAUDE.md).

### 12.7 Debug menu

Starting set — **grow this as we go, whenever a debug affordance would save time:**

add money · add resources · **level up** (grant exactly enough XP) · force-spawn egg · force-fire world event · reset

### 12.8 Assets

**[3D] Two asset tracks — 3D stations, 2D creatures.**
- **Stations & world** are **3D** (meshes/materials/prefabs). They start as tinted primitives and swap to real meshes as produced.
- **VoidPets** are **2D billboards** (§10.3, §12.6) built from the existing flat VoidPet art — so they need **no 3D pipeline and no placeholder phase**; the real sprite goes on the quad from the start. Producing a pet asset means preparing its 2D art (background-removed / centered), not modelling a mesh.

**Assets are injected incrementally as they're produced.** Real assets arrive during the build rather than after it: the designer generates them with the `asset_list` skill as milestones proceed and swaps placeholders in as each becomes available. This is a designer-side reference change, never a code change — a `StationSO` references a **prefab/mesh/material**, a `VoidPetSpeciesSO` references a **2D sprite/texture** (§14); a real asset drops into the same slot the placeholder occupied.

**UI is built against `ui_inventory` mockups.** The designer generates UI mockups with the `ui_inventory` skill and passes them in as visual reference for each screen (§12.1–12.4); build UI to match the mockup for a surface when one exists, primitives/UGUI defaults until then.

The contract for both: **nothing about a placeholder may leak into code or the event layer** — an asset swap is only ever an SO reference edit. This subsection exists so the build stays swap-ready and nobody hard-codes a placeholder.

---

## 13. Session & Persistence

Session-only. No save/load for the prototype; a save file (or `PlayerPrefs`) comes later — **[SO]** the 2D spec's "localStorage" is replaced by whatever persistence the Unity/WebGL build uses, a later change either way.

Timers do **not** advance while the tab is closed — but **timers are stored as absolute timestamps**, so offline progress is a small change later rather than a rewrite.

Reset lives in the debug menu.

---

## 14. Data Files → ScriptableObjects

> **This inventory is proposed, not agreed.** In the 2D spec the file split was never put to the user (the pitch says only "all data stored in JSON"). The SO breakdown below inherits that status: it is a **proposed** starting point, not an approved schema. The *contents* are all sourced from the decision record; the *partition* is not.

**[SO]** CLAUDE.md replaces JSON with ScriptableObjects and makes the inspector the tuning surface. The proposed default (see `-questions.md` Q2a) is **one SO asset per entity via `[CreateAssetMenu]`**, plus a single `GameConfigSO` for global scalars. This maps the 2D spec's file inventory onto SO types:

| 2D file | SO type(s) | Holds |
|---|---|---|
| `game.json` | `GameConfigSO` (single) | grid dimensions, cell size, camera min/max zoom, pan bounds |
| `resources.json` | `ResourceSO` (per resource) | id, display name, base value, starting storage cap, `sellable` flag, **mesh/icon ref** |
| `recipes.json` | `RecipeSO` (per recipe) | inputs, outputs, optional timer, station type |
| `stations.json` | `StationSO` (per station) | build cost, cap, footprint, unlock level, recipe refs, **mesh/prefab ref**, `placeholderColor` |
| `orders.json` | `OrderConfigSO` (single) | slot count, refill timer, payout multipliers, generation weights |
| `upgrades.json` | `UpgradeSO` (per upgrade) | station + universal + silo tiers, costs, emitted `Effect[]` |
| `levels.json` | `LevelSO` (per level, or one ordered set) | XP threshold, per-level unlocks and rewards |
| `xp.json` | `XpConfigSO` (single) | XP granted per action |
| `voidpets.json` | `VoidPetSpeciesSO` (per species) | rarity, blurb, quote, affinity, `Trait[]` + `Effect[]`, **2D art (sprite/texture) ref for the billboard** — not a mesh (§10.3) |
| `relationships.json` | `RelationshipConfigSO` (single) | formation time, range, affinity → effect magnitude table |
| `events.json` | `WorldEventSO` (per event) | interval, min level, `Effect[]`, notification type |
| `start.json` | (fields on `GameConfigSO`) | starting cash, resources, pre-placed stations |

**SOs are a Data-layer authoring surface, not a Core dependency.** The SOs above are `UnityEngine.ScriptableObject`s and live in `Data/`; `Core/` must never reference a `*SO` type (the boundary rule). So at boot the Systems layer **projects each SO into a plain `Core/Model` object** — a `ResourceModel`, `RecipeModel`, etc. — and hands *those* to the Core rules (order generation, pricing, the resolver). The one exception is the embedded Core types (`Effect[]`, `Trait[]`): those are already pure-C# (§3.1), so they cross the boundary as-is with no projection. The rule of thumb: a Core rule reads a `*Model`, never a `*SO`.

**Asset references live in data, never in code.** Every station and resource SO carries its own mesh/prefab/material reference and the `VoidPetSpeciesSO` its 2D sprite reference (§10.3), so swapping placeholder art for real art is a designer-side edit. Until real art exists, stations render as tinted primitives (§12.6) using `placeholderColor`; VoidPets use their real 2D art from the start. (Asset refs stay on the SO — the `Core/Model` projection carries only the rule-relevant scalars, no UnityEngine handles.)

**Wheat's order-pool exclusion is expressed once**, as `sellable: false` on the wheat `ResourceSO`, projected onto its resource model. Order generation (§6.1) reads that flag off the model. Do not duplicate it as a separate exclusion list on the order config — one rule, one home.

**Boot validation (CLAUDE.md).** Every SO is validated once at boot: every required reference assigned, every number in range. On failure, throw immediately with the asset name and field. Never default-fill a missing value.

---

## 15. Event Catalog

> **This catalog is engineering-derived, not agreed.** It was drafted from `CLAUDE.md`'s event-driven rule and the Job/Order naming convention the user approved (R1 #9). The individual event names and every payload shape are proposed. It's here because milestones that each invent their own event names is the single most likely way this decomposition goes wrong — but it hasn't been reviewed.

**[SO/Core]** The bus is **plain C# in `Core/Events`** (CLAUDE.md), so the Core can emit without touching Unity. Systems republish Core events; the payload names below are the proposed contract. Systems talk only through the bus. Emitters describe **what happened**, never what should happen in response.

**Boot:** `data:loaded` · `game:started`

**Input intents** (emitted by the View/input layer, never acted on by it):
`input:placeRequested {stationType, cell}` · `input:moveRequested {stationId, cell}` · `input:stationTapped {stationId}` · `input:jobQueueRequested {stationId, recipeId}` · `input:jobCancelRequested {stationId, jobIndex}` · `input:orderFulfillRequested {orderId}` · `input:orderSkipRequested {orderId}` · `input:upgradePurchaseRequested {upgradeId}` · `input:petAssignRequested {petId, stationId}` · `input:petUnassignRequested {petId}`

**Stations:** `station:built` · `station:moved` · `station:demolished` · `station:blocked {stationId, reason}`

**Jobs:** `job:queued` · `job:started` · `job:completed {stationId, outputs}` · `job:collected {stationId, outputs, byPet?}` · `job:cancelled`

**Economy:** `resource:changed {resource, delta, total}` · `storage:full {resource}` · `money:changed {delta, total}`

**Orders:** `order:generated {slot, order}` · `order:fulfilled {orderId, payout, xp}` · `order:skipped {orderId}` · `order:slotRefilled {slot}`

**Progression:** `xp:gained {amount, source}` · `level:up {level, unlocks, rewards}` · `unlock:granted {kind, id}`

**VoidPets:** `egg:granted {source}` · `egg:hatched {petId, species}` · `pet:assigned` · `pet:unassigned` · `relationship:forming {petA, petB, progress}` · `relationship:formed {petA, petB, traits}`

**World events:** `worldEvent:started {eventId, effects, duration}` · `worldEvent:ended {eventId}`

**Effects:** `effects:recalculated`

> Note there are no `ui:*` events. UI systems (toasts, popups) *listen* to domain events and decide for themselves whether to show something — per `CLAUDE.md`, an emitter never says `showToast`.

---

## 16. Deferred

Explicitly out of scope for the first build. Named here so nobody builds them early.

- **VoidPet Station** — from the original pitch: an area bonus to nearby VoidPets. Deferred, not cut (R2 #8). The `pet.*` effect types (§3.2) are what it will need; `local.*` is station-scoped and will not serve.
- **[SO] In-game tuning screen — resolved.** The pitch asked for a screen that writes JSON permanently, and the 2D spec deferred it (a browser page can't write to `data/`, so it needed a Vite write endpoint). **In Unity this is moot: the inspector edits SO assets directly and persists them — the in-game tuning UI exists for free** (CLAUDE.md); no write endpoint needed. (A separate balance app the user is building lives outside this spec's scope.)
- **Save/load** (§13), **offline timer progress** (§13).
- **Money sinks** beyond building and upgrading — "not right now" (R1 #8).

**Not deferred — decided:** there is no direct resource selling. Orders are the only cash source (R1 #7). That's a permanent economic decision, not a someday-item, and it's why the Order Board is load-bearing.

---

## 17. Open Items

- All numbers — costs, timers, XP values, level thresholds, payout multipliers, storage caps, trait magnitudes — are to be invented as placeholders into the SO assets and tuned by play. Explicitly approved (R1 #62).
- The 6 species, their traits, blurbs, quotes, and affinities are placeholders to be invented and rewritten after play (R2 #14).

**Genuinely unanswered — needs a decision:**

- **§15's event catalog and §14's SO split** are proposed, not agreed (see the notes on each). The JSON→SO *translation* is settled by CLAUDE.md; the *partition* of entities into SO types is not.
- **§4.5's tap-resolution rule** (collect-if-possible, else open panel) is a decision made while writing the original spec, not one the user made. It resolves a real lockout but deserves a look.

**Resolved during the Unity port** (was open, now settled — see `VoidDay-Spec-unity-questions.md`):

- **[3D] The Unity-port decisions** — camera projection + tilt (§2, §12.5), SO granularity and the Effect model's home (§3.1, §14), the 3D placeholder + billboarding policy (§12.6), and the assets/UI-mockup workflow (§12.8) — were put to the user and confirmed. The spec reflects those answers; this line records that they are decided, not open.
