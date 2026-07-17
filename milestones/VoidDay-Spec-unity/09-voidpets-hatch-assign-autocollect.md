# Milestone 09 — VoidPets: Hatch, Assign, Auto-Collect

**Playable outcome:** Hatch a granted egg into a revealed VoidPet, assign it to a station, and watch it auto-collect completed jobs — unblocking the station — while its trait modifies that station.

## Goal
VoidPets are what the whole blocking-friction exists *for* (§1): an assigned pet auto-collects, relieving the tap-every-job pressure. This milestone delivers one pet's full lifecycle — acquire, hatch, view, assign, auto-collect, trait-applies — and extends the effect system with the **trigger + condition** machinery that passive-only effects (M5/M6/M7) didn't need.

## Build This
- **Egg acquisition** (§10.1): eggs from level-up rewards **and** a small chance on order fulfillment (`egg.chance` effect). Eggs accumulate at `hud.eggButton` (top-right, count badge); nothing auto-opens the hatch popup. Emit `egg:granted {source}`.
- **`hud.eggButton`** (UI-Inventory): appears when ≥1 egg held; tap opens `popup.hatchEgg` for the next egg; multi-egg dismiss advances egg-to-egg.
- **Hatching** (§10.1): `popup.hatchEgg` — tap egg → reveal pet. **No duplicates**: a dupe roll rerolls into an unowned species (Core-side, before reveal). First-ever hatch reveals `hud.voidPetButton`. Emit `egg:hatched {petId, species}` + `xp:gained {source: hatch}` (the 4th XP source, §9).
- **Species & traits** (§10.2): 6 species, traits **fixed per species** on `VoidPetSpeciesSO` (never rolled), 1 trait normally / 2 for rarer; rarity Common/Rare/Epic. Placeholder species/traits invented into SO assets.
- **`menu.voidPet`** + **`hud.voidPetButton`** (§12.1, §12.3): the collection grid; tap a pet → `popup.petDetails` (picture = mesh render, blurb, italic quote, rarity, traits via the §3.6 generator, current assignment).
- **Assignment** (§10.3): `picker.petAssign` opened from `panel.station`'s assignment slot (hidden until ≥1 pet owned). One pet per station, generator stations only, free/instant. Assigned-pet symbol on cells; tapping an assigned pet → `popup.genericText` confirm-unassign/move dialog. Emit `pet:assigned`/`pet:unassigned`.
- **`world.assignedPet`** (§10.3, §12.6): the pet's own mesh on top of its station, idle bob tween.
- **Auto-collect** (§10.3): an assigned pet auto-collects **instantly on job completion**, which unblocks the station. Implement as a system listening to `job:completed` that invokes the same collect path M2 built (routed through `IsCollectionPossible` — if storage is full, collection still refuses, correct). Emit `job:collected {byPet}`. `pet.autoCollectSpeed` modifies collect speed.
- **Extend the effect system**: `TriggerType` firing (`JobCompleted`, `JobCollected`, `OrderFulfilled`, `StationBuilt`, `PetHatched`, `LevelUp`, `JobQueued`) with `triggerChance` (0→100, 0 treated as 100); `ConditionType` evaluation for `AssignedTo`, `ResourceAbove`, `PlayerLevelAbove`; `egg.chance`, `pet.autoCollectSpeed`. The "Cow Lover" reference (§3.6: assigned-to-pasture, 20% chance ×3 yield on completion) is the archetype — a triggered, conditional, chance-gated effect.
- **Range** (§10.4): grid-cell Manhattan distance helper `|Δcol|+|Δrow|` (needed by `WithinRange*` conditions; M10 uses it for pets, but build the metric here).
- **Debug** (§12.7): add **force-spawn egg** to `menu.debug`.

## Do NOT Build This
- **Relationships / proximity hearts / `WithinRangePet` / `local.*`** → M10.
- **`pet.effectStrength`** and **VoidPet Station area-range / `pet.*` reach beyond auto-collect** → deferred VoidPet Station (§16); not built. Build only `pet.autoCollectSpeed`.
- **`WithinRangeStation` condition** → no launch content; leave the enum member unresolved (summary Open Item).
- **Rarity visual treatment finalization** → StyleGuide open item; placeholder frames OK.
- **Assigning to non-generator stations** (Order Board/Silo/Workshop) → not allowed (§10.3).

## Context
Builds on M8 (level rewards can grant eggs — now allowed), M3 (order-fulfill for `egg.chance`), M2 (collect path, `job:completed`), M5 (resolver, description generator). Adds to the spine:
- **Events added:** `input:petAssignRequested {petId, stationId}`, `input:petUnassignRequested {petId}`; `egg:granted {source}`, `egg:hatched {petId, species}`, `pet:assigned`, `pet:unassigned`; `job:collected {byPet}` (byPet now populated).
- **Data added:** `VoidPetSpeciesSO` → model (rarity, blurb, quote, affinity, `Trait[]`+`Effect[]`, mesh ref); `egg.chance` on order fulfillment.
- **Systems touched:** new `Core/Rules/EffectResolver` (triggers, conditions, chance), `Core/Rules/Range` (Manhattan), `Systems/VoidPets` (acquisition, hatch, assignment, auto-collect); `View/HatchPopup`, `View/PetMenu`, `View/PetDetailsPopup`, `View/PetAssignPicker`, `View/AssignedPet`, `View/HUD` (egg + pet buttons).

## Principles
- **The generated trait is an ordinary Trait** (§10.5 preview / §3): nothing special-cases pet effects — they flow through the same resolver + description generator. If you're branching on "is this a pet," stop.
- **The Core boundary** (rule 3): acquisition, dedupe reroll, trigger/condition evaluation, range — all `Core/`. The pet mesh + bob tween are `View`. Range is `(int col,int row)` Manhattan, never world units (§10.4).
- **Event-driven** (rule 2): auto-collect *listens* to `job:completed` and invokes the collect rule; it does not reach into the Producer.
- **Test the core**: dedupe reroll (never returns an owned species), trigger firing + `triggerChance`, condition evaluation, and the Cow-Lover chance-×3-yield case are pure-C# — cover them.
- **Fail loud**: assigning a pet to a non-generator or nonexistent station throws.

## Assets Required
- `mesh.pet.<species>` ×6 [needs real asset — the one skilled, IP-critical line; placeholder = dark capsule + 1 emissive eye], `mesh.egg` [placeholder OK — emissive ovoid]
- `mat.pet.void`, `mat.pet.eyeGlow` [placeholder OK]
- `icon.hud.pets` [placeholder OK], egg indicator (void-accent) [placeholder OK], `ui.frame.rarity.common/rare/epic` [placeholder OK — proposed treatment]
- **SFX** [placeholder OK]: `sfx.egg.hatch`, `sfx.pet.assign`
- **VFX** [placeholder OK]: `vfx.hatchReveal`

## UI Mockups Required
- `hud.eggButton`, `hud.voidPetButton`, `menu.voidPet`, `picker.petAssign`, `popup.hatchEgg`, `popup.petDetails`, `popup.genericText` (confirm dialog), `world.assignedPet` — [mockups needed]

## Definition of Done
- A granted egg appears at `hud.eggButton`; tapping it opens the hatch popup; tapping the egg reveals a pet (never a duplicate species); first hatch reveals the VoidPet menu button and grants XP.
- The pet details popup renders the pet, its rarity, and its traits as plain-language sentences.
- Assigning a pet to a generator station puts its mesh on the station; on the next job completion it **auto-collects** (station unblocks with no tap).
- The pet's trait measurably modifies its station (e.g. a speed/yield trait, or the Cow-Lover chance-×3).
- Assignment is one-per-station, generator-only, free/instant; unassign/move goes through a confirm dialog.

## How to Test
1. Debug → force-spawn egg (and/or level up for a reward egg) → egg indicator shows a count.
2. Tap it → hatch popup → tap egg → a pet is revealed; the VoidPet menu button appears.
3. Open the VoidPet menu → tap the pet → details popup shows rarity + trait sentences.
4. Open the Field panel → tap the assignment slot → picker → assign the pet. Its mesh appears on the Field.
5. Queue a job; let it complete → it auto-collects with no tap (station never blocks).
6. Confirm the trait's effect (time a speed trait, or observe the Cow-Lover ×3 proc over several jobs).
7. Force-spawn several eggs and hatch them → confirm no duplicate species (dupes reroll).
8. Tap the assigned pet in the picker → confirm the unassign/move confirm dialog.
