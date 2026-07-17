# Milestone 10 — VoidPet Relationships

**Playable outcome:** Assign two pets to stations within range of each other, watch a heart appear over both, and after ~30s of continuous proximity get a friendship popup that grants each pet a local bonus that applies while they stay near.

## Goal
The last VoidPet layer and the last new effect vocabulary: **proximity-conditional, area-scoped** effects. It extends the effect system with the `WithinRangePet` condition, the `local.*` scope, and per-effect `range` — completing the schema from §3. It's a pure extension: the generated friendship trait is an ordinary Trait resolved by the same machinery.

## Build This
- **Proximity detection** (§10.4, §10.5): two **assigned** pets within `range` grid cells (Manhattan, reusing M9's metric) → `world.relationshipHeart` over both heads. Only assigned pets count (an unassigned pet in the menu is near nothing, §3.4).
- **Formation** (§10.5): after ~30s of *continuous* proximity, form the friendship; emit `relationship:forming {petA, petB, progress}` while accumulating and `relationship:formed {petA, petB, traits}` on completion. Formation time + range from `RelationshipConfigSO`.
- **`popup.relationshipFormed`** (§12.4): shows the two pets and the traits gained.
- **Generated trait** (§10.5): each species has an `affinity` naming an effect type (Hard Worker→speed, Thrifty→cost). On befriending, **each pet gains a Trait granting `local.<partner's affinity>`**, with a `WithinRangePet: <partnerId, n>` condition, magnitude from `RelationshipConfigSO` scaled by the rarer of the two. Name pattern *"Friendship with <Pet>"*. It's an **ordinary Trait** — nothing special-cases it.
- **Persistence** (§10.5): once formed, permanent (survives separation); the *bonus* only applies while back in range — enforced entirely by the trait's own `WithinRangePet` condition, not by special logic. A pet can hold relationships with everyone in range; bonuses stack per §3.5.
- **Extend the effect system**: `WithinRangePet` condition evaluation (both pets assigned + within range), `local.*` scope (every station within `range` cells of the emitter), per-effect `range`. This is the machinery `station.cost`'s wiring (M5) was waiting for — a `local.cost` friendship trait now actually reduces input cost at nearby stations.

## Do NOT Build This
- **`pet.*` reach / VoidPet Station area bonus** → deferred (§16). `local.*` is station-scoped and is what relationships use; do not build `pet.*` area effects.
- **`pet.effectStrength`** → deferred territory; not built.
- **New acquisition/hatch/assignment** — all from M9; this only adds proximity + formation.
- **Relationship-timer visualization specifics** (fill/pulse on the heart) → inferred, not spec-pinned (UI-Inventory); a plain heart is sufficient, add a progress cue only if trivial.
- **Relationships between a pet and a station type** (`WithinRangeStation`) → no launch content.

## Context
Builds on M9 (pets, assignment, range metric, `world.assignedPet`), M5 (resolver + description generator). Adds to the spine:
- **Events added:** `relationship:forming {petA, petB, progress}`, `relationship:formed {petA, petB, traits}`.
- **Data added:** `RelationshipConfigSO` → model (formation time, range, affinity→magnitude table); `VoidPetSpeciesSO.affinity` read.
- **Systems touched:** `Systems/VoidPets` (proximity tracking, formation timer, trait generation), `Core/Rules/EffectResolver` (`WithinRangePet`, `local.*`, `range`); `View/RelationshipHeart`, `View/RelationshipFormedPopup`.

## Principles
- **The generated trait is ordinary** (§10.5): it's a normal Trait with a `WithinRangePet` condition; the resolver and description generator handle it with no special case. This is the payoff of building the effect system as the spine.
- **Persistence via the condition, not a flag** (§10.5): don't track "active/inactive bonus" separately — the `WithinRangePet` condition re-evaluates each resolve, so the bonus naturally applies only in range.
- **The Core boundary** (rule 3): proximity, formation timing, trait generation, condition evaluation — all `Core/`. Hearts + popup are `View`. Distance is Manhattan on `(int col,int row)`.
- **Test the core**: proximity (only assigned pets; continuous-30s resets on separation), the generated-trait magnitude (scaled by the rarer), and `WithinRangePet`/`local.*` resolution are pure-C# — cover them.

## Assets Required
- `mesh.pet.<species>` [from M9], `icon.heart` [placeholder OK — void-accent heart]
- **SFX** [placeholder OK]: `sfx.relationship.form` (warm chime)
- **VFX** [placeholder OK]: `vfx.relationshipForm` (heart burst)

## UI Mockups Required
- `world.relationshipHeart`, `popup.relationshipFormed` — [mockups needed]

## Definition of Done
- Two assigned pets within range show a heart over both; separating them clears the heart and resets the timer.
- After ~30s continuous proximity, the friendship popup fires listing the traits gained.
- Each pet gains a *"Friendship with <Pet>"* trait (readable via the §3.6 generator) granting a `local.<affinity>` bonus.
- The bonus applies only while the pets are back in range (move them apart → bonus stops; back together → resumes), with no special-case code — purely the `WithinRangePet` condition.
- Multiple relationships on one pet stack per §3.5.

## How to Test
1. Have two pets (M9); assign each to a generator station placed within range of each other.
2. Confirm a heart appears over both; move one station out of range (M4 move) → heart clears, timer resets.
3. Keep them in range ~30s → friendship popup fires with the gained traits.
4. Open each pet's details → confirm a *"Friendship with <Pet>"* trait with a readable local bonus.
5. Verify the bonus is live only in range: measure the affected stat, move apart (bonus gone), move back (bonus returns).
6. Form a second relationship on the same pet → confirm both bonuses stack.
