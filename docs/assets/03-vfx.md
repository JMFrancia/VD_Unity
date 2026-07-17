# Assets — VFX (particle / shader)

**Spec:** `docs/VoidDay-Spec-unity.md` · **Style guide:** `docs/StyleGuide.md`
**Count:** 7 effects (+ 1 global post-process profile)

> Scope note: in the primitive prototype, **tweens** (hop/bounce, idle bob, scale-pop) are **View-layer code**, not VFX assets — they live in Motion (StyleGuide) and `02-ui.md`, not here. This doc lists only genuine **particle-system / shader** effects worth authoring as their own discipline (your choice to keep VFX separate). Until authored, each has a trivial tween/tint placeholder so nothing blocks first-playable.

## Format

- **Delivery:** one **prefab per effect** (URP particle system and/or shader-graph material), spawned by a View listener off a domain event (spec §15) — never emitted by Core.
- **Palette:** the reserved **void accent** `#8B5CF6` violet ↔ `#22D3EE` cyan (StyleGuide Color). VFX is where the accent is allowed to be loud.
- **Bloom:** a single **global URP Volume** (`vfx.post.bloom`) makes emissive void elements glow (StyleGuide Q6). This is the cheap "cosmic" sell — not a per-asset effect.
- **Billboard:** world-space bursts face the camera (spec §12.6).

## Assets

| id | What | Trigger event (§15) | Qty | Spec | Placeholder |
|---|---|---|---|---|---|
| `vfx.dopamineRain` | Void motes raining over the farm during Dopamine Rain (2 min) | `worldEvent:started` (Dopamine Rain) | 1 | §11 | tint pulse / none |
| `vfx.collectPop` | Small void-accent burst on collect (the loop's reward beat) | `job:collected` | 1 | §4.4, StyleGuide Motion | scale-pop only |
| `vfx.levelUp` | Level-up flourish (rising sparkle) | `level:up` | 1 | §9, §12.4 | popup only |
| `vfx.hatchReveal` | Egg-crack + void burst on hatch | `egg:hatched` | 1 | §10.1, §12.4 | swap mesh only |
| `vfx.relationshipForm` | Heart-burst when a friendship forms | `relationship:formed` | 1 | §10.5, §12.4 | heart icon only |
| `vfx.readySparkle` | Gentle glow/sparkle on a ready-to-collect station | `job:completed` | 1 | §12.6 | bounce + tint only |
| `vfx.placementPoof` | Soft dust/void poof on station placement | `station:built` | 1 | §12.2 | none |

### Global
| id | What | Qty | Spec |
|---|---|---|---|
| `vfx.post.bloom` | URP Volume bloom profile — makes void emissives glow | 1 | §12.6, StyleGuide Q6 |

## Notes

- **Flavor world-events** (the 2 non-Dopamine-Rain launch events, spec §11) are **toast-only, no VFX** — they carry no real effect, so no particle asset.
- Everything here is **additive polish**: the game is fully playable with placeholders (tweens/tints). Prioritize `vfx.collectPop` and `vfx.readySparkle` first — they're on the core loop's most-repeated beat.
- If **MoreMountains Feel** is later adopted (StyleGuide Motion / Q18), several of these become Feel feedbacks rather than hand-built prefabs — the ids and trigger events stay the same, so this doc survives the swap.
