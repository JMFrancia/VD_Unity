# Milestone 04 — Build & Manage Stations

**Playable outcome:** Open the build menu, drag a station onto a valid grid cell to place it for a money cost, demolish one for a refund, and long-press to pick one up and move it.

## Goal
Spending the cash M3 earns to expand the farm. This is where the station registry M1 built for runtime-add pays off, and where the build-menu's gating states (locked / cap-reached / can't-afford) appear — including the *static* locked state that M8 will later make dynamic. It comes before leveling because it needs money (M3), not levels: the level value is frozen at 1, so this milestone builds and demonstrates entirely at level 1.

## Build This
- **`menu.build`** (§12.3): a tray of every station type. Each entry: primitive thumbnail (tinted), `displayName`, money build cost. Roster always fully shown; unavailable entries stay visible but disabled with a reason marker.
- **Build-menu states** (§12.3, UI-Inventory `menu.build`): **available** (affordable, unlocked, under cap) → draggable; **locked** (`unlockLevel > playerLevel`) → grayscale + `icon.lock`, not draggable; **cap-reached** (owned = per-type cap) → disabled + `owned/cap` badge; **can't-afford** (cost > money) → disabled + cost in warning color.
- **Drag placement** (§12.2, `overlay.placementGhost`): drag an available entry out → the menu retracts → a translucent station-mesh ghost follows the pointer, snapped to the nearest cell via raycast-to-XZ. Valid cell = green tint, invalid (occupied/off-grid) = red tint. Drop on valid → place; drop on invalid or back over the build button → cancel.
- **Placement** consumes the money cost, adds the station to the registry/occupancy (M1), renders it. Emit `station:built` + `xp:gained` (build XP, §9).
- **Demolish** (§4.3): 50% refund of build cost; remove from registry. Emit `station:demolished`.
- **Move** (§12.2, `overlay.moveGhost`): long-press a placed station to pick it up → drag (same snap/tint) → **tap to confirm** the new cell. Free (§4.3). Emit `station:moved`.
- **Station-type caps enforced at build** (§4.3): read the cap through a **seam** (M8 raises it by level) — do not hardcode the cap value at the read site. Starting caps: 2 Fields, 1 of everything else.
- **The `unlock:granted` listener** on `menu.build`: written now so the menu re-evaluates lock state when the event fires — but nothing fires it until M8, so this is not part of this milestone's *verifiable* outcome (see DoD).
- **Launch-data constraint:** any station a pre-M8 milestone must place must have `unlockLevel = 1` — critically **Workshop = 1** (M6 must place a Workshop, and level can't rise before M8). Field is already level 1.
- **Debug** (§12.7): no new hook required here (money already addable via M3).

## Do NOT Build This
- **Leveling / making locks lift / caps rise** → M8. Build the static lock display + the (dormant) listener; do not build the increment that activates them.
- **Station upgrades / the upgrade rows** → M5.
- **The Effect resolver** → M5. Build cost / cap reads go through the seam as passthroughs.
- **`build.cost` effect** → M6 (base cost only here, via the seam).
- **VoidPet-assignable-only distinctions in the build menu** → not relevant here; assignment is M9.
- **Placing Henhouse/Pasture/Creamery/Bakery** as a *demonstrated* outcome → those are level-locked (unlockLevel > 1); they show as locked and become buildable in M8. Only level-1 types (2nd Field, Workshop) are placeable now.

## Context
Builds on M1 (registry/occupancy, ghost = station mesh + ghost material), M3 (money to spend, `playerLevel` value). Adds to the spine:
- **Events added:** `input:placeRequested {stationType, cell}`, `input:moveRequested {stationId, cell}`; `station:built`, `station:moved`, `station:demolished`; `xp:gained {source: build}`; (`unlock:granted` *listened for*, emitted by M8).
- **Data added:** `StationSO` fields now read: `buildCost`, per-type `cap` (base), `unlockLevel`, `footprint` (1×1). New meshes/materials for buildable types (Workshop, and the level-locked types' entries).
- **Systems touched:** `Systems/BuildPlacement` (ghost, snap, validity), `Systems/StationRegistry` (add/remove/move — extended, not rebuilt), `Systems/Progression` (build XP); `View/BuildMenu`, `View/PlacementGhost`, `View/MoveGhost`.

## Principles
- **Clean extension, not rework** (skill rule): placement adds into M1's registry/occupancy — if M1 registered its pre-placed stations properly, nothing here retrofits them.
- **Read caps/costs through a seam** (architectural): so M6 (`build.cost`) and M8 (level-raised caps) extend the read site instead of surgically reopening it.
- **Data-driven** (rule 1): build cost, refund %, caps, unlock levels, footprint — all `StationSO`. The 50% refund is a config value, not a literal.
- **Event-driven** (rule 2): `station:built` announces the fact; the placement poof and thunk SFX *listen*.
- **Verify Unity APIs**: pointer long-press detection, raycast-to-plane, translucent URP material for the ghost — check installed versions.

## Assets Required
- All buildable-type meshes + materials: `mesh.station.henhouse`/`pasture`/`creamery`/`bakery`/`workshop` and `mat.station.*` [placeholder OK — primitives; TBD silhouettes per asset doc]. (Field/Silo/OrderBoard from M1.)
- `mat.ghost.valid`, `mat.ghost.invalid` [placeholder OK — translucent green/red]
- `icon.hud.build` [placeholder OK], `icon.lock` [placeholder OK — padlock], `ui.card` [placeholder OK — from M2]
- **SFX** [placeholder OK]: `sfx.station.place` (soft thunk), `sfx.station.move`, `sfx.station.demolish`
- **VFX** [placeholder OK]: `vfx.placementPoof`

## UI Mockups Required
- `menu.build` (with locked / cap / can't-afford markers) — [mockup needed]
- `overlay.placementGhost`, `overlay.moveGhost` — [mockup needed]

## Definition of Done
- The build menu shows every station type; level-locked types are grayed with a lock, the maxed Field/Silo/Order Board show `owned/cap`, and any type you can't afford shows its cost in warning color.
- Dragging an affordable, unlocked, under-cap station shows a ghost that tints green on valid cells and red on occupied/off-grid; dropping on green places it and deducts money.
- Demolishing refunds 50%; moving via long-press + tap-to-confirm relocates for free.
- Building grants a little XP (bar nudges).
- Placing a 2nd Field works (cap 2); attempting a 3rd shows cap-reached.

## How to Test
1. Earn/borrow cash (fulfill orders or debug → add money).
2. Open the build menu → confirm the four state markers exist (a locked type, a capped type, an affordable type, and — spend down — a can't-afford type).
3. Drag a Workshop out → confirm the menu retracts and the ghost tints green/red correctly; drop on a valid cell → it places and money drops.
4. Drag onto an occupied cell → red, drop cancels (no charge).
5. Long-press the new Workshop → move-ghost appears; drag to a new cell → tap to confirm → it relocates, no charge.
6. Demolish it → 50% of the build cost returns.
7. Build a 2nd Field → succeeds; confirm a 3rd is blocked by cap-reached.
8. Confirm Henhouse/etc. remain locked (they unlock in M8).
