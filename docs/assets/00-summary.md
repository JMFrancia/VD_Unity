# VoidDay — Asset Summary

**Spec:** `docs/VoidDay-Spec-unity.md` · **Style guide:** `docs/StyleGuide.md` · **Generated:** 2026-07-17

These docs are the **naming authority** for asset ids. Milestone plans and SO asset-path fields reference these ids; they do not invent their own. **This summary does not schedule assets to milestones** — asset→milestone scheduling lives one-way in `milestones/<spec>/00-summary.md` so the two can't drift.

## Totals

| Type | Count | Doc |
|---|---|---|
| Models (3D meshes) | 17 | `01-models.md` |
| Materials | 13 | `01-models.md` |
| UI sprites | ~29 | `02-ui.md` |
| VFX (particle/shader) | 7 (+1 global) | `03-vfx.md` |
| SFX | 20 | `04-sfx.md` |
| Music | 1 | `05-music.md` |
| Fonts | 1 family | `06-fonts.md` |
| **Total asset items** | **≈ 88** | — |

Everything has a primitive / UGUI-default / tween placeholder, so production is **incremental** — real assets swap into the same SO slot with no code change (spec §12.8).

## Cost Drivers

Where the volume and the *difficulty* actually are:

1. **VoidPet 2D→3D conversion (6 meshes)** — the only expensive, skilled, IP-critical line. Every other model has a trivial primitive stand-in; these don't. **Cutting to fewer species is the biggest single lever.**
2. **UI sprite set (~29)** — high count but mostly simple icons + reusable 9-slice chrome. Resource icons (11) are needed earliest (they appear in orders, recipes, popup, and the billboarded ready-icon).
3. **SFX set (20)** — breadth, not depth; each clip is short.

### Where the count *didn't* explode (worth knowing)

**States × entities did not multiply.** 8 stations × 5 visual states (idle/working/ready/full/ghost) is **8 meshes + ~4 shared overlays**, not 40 bespoke assets — because the architecture renders states via **shared world-space overlays + material tints** driven by the View layer syncing to Core state (spec §12.6). The data-driven design collapses the usual state explosion. Same for VoidPets: 6 meshes, no per-state art (idle bob is a tween).

## Assumptions

Each is inferred, not read — and therefore a risk:

- **Portraits = 3D mesh render** (decided), so no 2D portrait line. If posed 2D portraits are wanted later, that's +6 assets.
- **Ready floating-icon = the output resource's own icon**, billboarded — no separate per-resource mesh/sprite.
- **Egg = one design**, not per-species or per-rarity. Rarity eggs would be +2.
- **Rarity frames ×3** are a proposed visual treatment (StyleGuide Open item), not spec-mandated.
- **HUD button icons ×3** (build/debug/pets) assumed as glyphs; could stay UGUI-text → −3.
- **SFX grouping** — 20 clips cover all §15 events by folding near-duplicates (assign/unassign, egg-grant, money-changed). No event is silent, but some share a cue.
- **Station primitives** for Pasture/Creamery/Bakery/Workshop/Order Board are unspecified shapes (StyleGuide TBD) — assumed "pick distinct silhouettes."

## Open Items

Unresolved; each blocks a specific line:

- **Which 6 IP creatures are the prototype species** (spec §10.2) → blocks all `mesh.pet.*` (and their portrait renders). The IP sheet has ~15+ emotion-named creatures; emotion names hint at trait affinity but that's a design call.
- **Remaining station primitive silhouettes** (5 stations) → placeholder shape only; doesn't block a mesh id.
- **Rarity visual treatment** (Common/Rare/Epic) → blocks `ui.frame.rarity.*` final look.
- **Font family final pick** within the rounded-sans set → blocks the TMP SDF generation.
- **All numeric/tuning values, exact hex, dimensions** — tuned in play (spec §17); do not block asset *identity*.

## Deferred

Per the "omit deferred features" decision — tracked here, not specced into the type docs:

- **VoidPet Station** (spec §16) — mesh + area-bonus VFX. Needs `pet.*` effect types; deferred, not cut.
- **Save/load UI** (spec §13, §16).
- **Adaptive/layered music** (StyleGuide Q16).
- **Money-sink art** beyond build/upgrade (spec §16).
- **Marketing / meta** — app icon, splash, WebGL favicon. Out of prototype scope; trivial to add later.
- **Textures / normal maps** — prototype is untextured tint-only (StyleGuide); real IP art may reintroduce them as an SO material swap.
