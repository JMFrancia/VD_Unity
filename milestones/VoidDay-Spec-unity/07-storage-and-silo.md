# Milestone 07 — Storage & Silo

**Playable outcome:** Fill a resource to its cap, watch the station block with a "storage-full" state that refuses collection, then buy a Silo upgrade to raise every cap at once and unblock it.

## Goal
Adds the second kind of station block (§4.4) — collection refused because storage is full — and the relief valve for it (the Silo). It slots cleanly into M2's generic `IsCollectionPossible` predicate: storage-full becomes just another reason the predicate returns false, so tapping a full station opens its panel (per §4.5) rather than collecting. Introduces the `storage.cap` effect scope and enforces per-resource caps for the first time.

## Build This
- **Per-resource caps** (§7): each resource has its own cap from `ResourceSO.startingCap`, read through a **seam** (the Silo's `storage.cap` effect raises it). Enforce at collection: resources were **uncapped through M6** — this milestone turns on the cap check.
- **Storage-full block** (§4.4): when collecting would exceed a resource's cap, **refuse collection**; the station stays blocked and shows `world.storageFull` (a distinct warning tint/icon, visually separate from "ready"). Queued jobs still run then block behind it. **Nothing is ever destroyed.** Emit `storage:full {resource}` / `station:blocked`.
- **Predicate extension** (§4.5): `IsCollectionPossible` now returns false when the output resource is at cap → tapping opens `panel.station` immediately (collection refused), exactly as §4.4 requires. This is an extension of M2's predicate, not a new branch in the tap handler.
- **`panel.silo`** (§7, §8): the Silo (pre-placed from M1) becomes tappable, selling a single tiered storage-cap track as `pattern.purchaseRow`s — effect description, per-tier money cost, tier progression, `Buy` / `Maxed`. Optionally shows the current global cap.
- **`storage.cap` effect** (§3.2, §7): raises **every** resource's cap at once (one track, applies globally). Extend the resolver with this scope, wired at the cap read-site.
- **`world.storageFull` visual** (§12.6): warning amber→red tint + icon, billboarded, distinct from the void-accent ready state.

## Do NOT Build This
- **The Silo *holding* resources** — it holds nothing; resources are a global number pool (§4.2). The Silo only sells cap upgrades.
- **Per-resource cap upgrades** — the Silo raises all caps together (§7); no single-resource track.
- **Level-raised storage caps** — storage caps are Silo-driven, not level-driven (§7; level raises station-type caps and queue depth, that's M8).
- **Triggers/conditions/pets** → M9/M10.
- **Total-resources popup cap display** — optional/inferred (UI-Inventory); build only if trivial, otherwise leave.

## Context
Builds on M2 (collection path, `IsCollectionPossible` predicate, `world.readyIcon`), M5 (resolver), M1 (pre-placed Silo). Adds to the spine:
- **Events added:** `storage:full {resource}` (and `station:blocked {reason: storageFull}`).
- **Data added:** `ResourceSO.startingCap` read → resource model cap; `UpgradeSO` Silo cap tiers + `storage.cap` `Effect[]`.
- **Systems touched:** `Systems/ResourcePool` (cap enforcement), `Systems/Producer` (collection refusal → blocked state), `Core/Rules/EffectResolver` (`storage.cap` scope), `Systems/Upgrades` (silo track); `View/SiloPanel`, `View/WorldState` (storage-full visual).

## Principles
- **Clean extension** (skill rule): the storage-full block rides M2's predicate — if M2 built `IsCollectionPossible` generically (it did), this adds a cap check to it, no tap-handler rework.
- **Fail loud but don't destroy** (§4.4, CLAUDE.md): full storage refuses and blocks; it never silently drops output.
- **Data-driven** (rule 1): caps and cap-upgrade tiers on SOs; `storage.cap` magnitude in the `Effect`.
- **One rule, one home** (§7): the Silo applies its cap increase via a `storage.cap` effect like any other emitter — no special-case cap logic.
- **Test the core**: cap enforcement (collect at cap refused; below cap allowed) and `storage.cap` resolution are pure-C# — cover them.

## Assets Required
- `mesh.station.silo`, `mat.station.silo` [placeholder OK — from M1]
- `world.storageFull` icon/tint — warning amber/red `#E8A33D`/`#D9534F` [placeholder OK — tint + icon]; grey-metal Silo identity `#8A8F98` [placeholder OK]
- **SFX** [placeholder OK]: `sfx.storage.full` (soft error); reuse `sfx.order.fulfill` for the cap-upgrade purchase

## UI Mockups Required
- `panel.silo` — [mockup needed]; rows are `pattern.purchaseRow`.
- `world.storageFull` — [mockup needed]; must read as distinct from `world.readyIcon`.

## Definition of Done
- Collecting toward a resource already at cap is refused; the station shows the storage-full state (visually distinct from ready).
- A storage-full station opens its panel on tap (collection refused → tap resolves to open, per §4.5).
- Queued jobs behind a full block still run, then block; nothing is destroyed.
- Buying a Silo upgrade raises **every** resource's cap; the previously-blocked station can now be collected.

## How to Test
1. Pick a resource with a low starting cap; queue and collect until you're near the cap.
2. Run one more job so collecting would exceed the cap → collection is refused, storage-full state shows.
3. Tap the full station → its panel opens (does not collect).
4. Queue more jobs → they run and then block behind the full one; confirm no resources vanish.
5. Tap the Silo → buy a cap upgrade → confirm every resource's cap rises.
6. Return to the blocked station → collect now succeeds and it unblocks.
