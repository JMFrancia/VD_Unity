# VoidDay — Style Guide

**Spec:** `docs/VoidDay-Spec-unity.md`
**Status:** 2026-07-17 · Round 1 answered
**Consumed by:** `asset_list`, `ui_inventory`

---

## Direction

VoidDay is a **warm, chunky, cozy farm sitting on a soft island in a dreamy violet void** — HayDay's readability and juice, rendered in clean toon-lit 3D under an angled ¾ top-down camera (spec §2, §12.5). The hook is a **deliberate contrast**: the farm is sunny, rounded, and tactile, while the collectible **VoidPets are sleek, near-black silhouette shadow-creatures** with an indigo sheen and glowing eyes — cool, expressive, a little mysterious, never cutesy and never scary. The void is *whimsical and inviting*, expressed as a single reserved cyan-violet glow that appears **only** on void things (pets, eggs, void world-events). Warm world, cool familiars, one glowing accent that ties them together.

If you read only this paragraph: a wrong asset is anything realistic-textured, anything with a second saturated accent color competing with the void violet, any VoidPet that is round-blobby-cute instead of sleek-silhouette, or any UI that is thin and flat instead of fat and rounded.

## Tone

- **Where it sits:** HayDay warmth × sleek shadow-familiar cool. Cozy management loop, charismatic void aesthetic.
- **The tension it resolves (spec's core art question):** "VoidDay / VoidPet" pulls cozy-farm against void. **Resolution:** the *world* leans fully cozy; the *void* is concentrated into the pets and their glow. They contrast rather than blend, and that contrast is the identity. (Answers Q1 = cozy-cosmic, sharpened by the IP art.)
- **It is deliberately NOT:** horror or gory (the pets are shadow-*familiars*, not monsters); realistic/PBR-illustrated like HayDay's actual texturing; a second-accent rainbow — the void violet is the only "special" hue; round-cute for the creatures — their silhouette language is established IP and is preserved.

## References

| Work | Take this | Not this |
|---|---|---|
| **HayDay** | Chunky readable buildings, generous rounded thumb-friendly UI, satisfying collect-pop juice, warm daylight farm palette | Its realistic-illustrated texturing; its busy decoration |
| **Cosmic/void palette discipline** (Cult of the Lamb's *pretty* register) | One glowing otherworldly hue, used sparingly and reserved | Its darkness, dread, or gore |
| **The VoidPet IP sheet itself** (`docs/` ref image) | The exact creature silhouettes, indigo-on-black sheen, glowing eyes, dynamic flowing poses, emotion-naming | — this is the source of truth for creatures; do not invent new creature language |

> Q2 note: the Ooblets/Slime Rancher "blobby creature" reference was **rejected** — creatures come from existing IP (2D→3D conversion), not invention.

## Technique

- **Rendering:** URP **lit, toon-ish / low-spec** (Q3a). Soft ambient + one key light, minimal specular, colors read as clean near-flat fills. Forgiving of untextured primitives; cohesive across the placeholder phase.
- **Outlines:** **none** (Q4a). Rely on silhouette + tint + the ¾ camera. Revisit only if forms fail to read.
- **Shape language — split by subject:**
  - **World & buildings:** round, soft, chunky. Rounded corners, fat forms, cozy (Q5a).
  - **VoidPets:** sleek, dynamic, silhouette-driven — the IP language. Flowing bodies, expressive poses, spiky/wispy edges. **Do not round them into blobs.** (IP override of the Q5 default, creatures only.)
- **Post-processing:** subtle **bloom on void elements only** (Q6a) — pet eyes/edge-sheen, eggs, and void world-events (e.g. Dopamine Rain) glow; the farm stays grounded. Single URP Volume, no per-asset work. This is what sells "cosmic" cheaply, and the IP art already carries the glow it targets.

## Color

All hex values are placeholders to be tuned in play (spec §17), but the **roles are fixed** — the guide constrains roles, not final swatches.

### Environment
| Role | Hex (start) | Notes |
|---|---|---|
| Farm ground / grass | `#7DBE5A` warm green | Sunny, saturated-but-soft |
| Ground secondary / tilled | `#9AD070` / `#6B4A34` | Lighter grass / soil brown |
| Void backdrop (the "sky") | `#1A1430` → `#2A2048` soft indigo | Dark-violet star-field, NOT blue sky (Q7a) |
| Backdrop stars/motes | `#4A3D70` faint | Sparse, low-contrast |

### The single Void accent (Q8)
| Role | Hex (start) | Notes |
|---|---|---|
| Void glow (primary) | `#8B5CF6` violet | The reserved hue. Pets' sheen/eyes, eggs, hearts, void events |
| Void glow (secondary) | `#22D3EE` cyan | The cool end of the same accent; use for highlight/emissive tips |

**Discipline rule:** this violet↔cyan glow is the ONLY otherworldly accent. Nothing else in the game gets it. Reserving it is what makes void things feel special. The IP art already uses exactly this indigo-violet sheen — 3D materials should match it, not repaint it.

### VoidPet body palette (from the IP sheet)
| Role | Hex (start) | Notes |
|---|---|---|
| Body base | `#12121C` near-black | Solid, silhouette-forward |
| Body secondary planes | `#3C3C5A` / `#4A4A66` slate-indigo | Desaturated highlight planes |
| Edge sheen / rim | `#6B6B90` → void violet emissive | Where the bloom catches |
| Eyes (life) | pale `#E8E8F0` or violet `#8B5CF6`, **emissive** | The single brightest point on the creature |
| Ground shadow | `#000000` @ ~25% soft ellipse | Grounds the floating silhouette |

### Station tint scheme (spec §12.6 `placeholderColor`, Q9a — warm naturalistic-by-function)
| Station | Hex (start) | Primitive (spec §12.6) |
|---|---|---|
| Field | `#7DBE5A` green | flat quad |
| Henhouse | `#C86B4A` warm terracotta | cube |
| Pasture | `#8Fb96A` grassy green | (TBD primitive) |
| Creamery | `#EDE3C8` cream | (TBD primitive) |
| Bakery | `#C79A4E` golden-brown | (TBD primitive) |
| Silo | `#8A8F98` grey-metal | cylinder |
| Workshop | `#5B7A99` steel-blue | (TBD primitive) |
| Order Board | `#8A5A3C` wood-brown | (TBD primitive) |

### State-signal colors (Q10 — must be identical everywhere)
| State | Signal |
|---|---|
| **Ready to collect** | Void accent glow `#8B5CF6` + bounce/float (draws the eye to the tap) |
| **Working** | Neutral progress bar: fill `#EDEDED`/`#7DBE5A` on track `#2A2438` |
| **Storage-full / blocked** | Warning amber→red tint `#E8A33D`/`#D9534F` + icon (spec §4.4) |
| **Locked** | Desaturated grayscale + lock icon (spec §12.3) |
| **Placement ghost valid / invalid** | Green `#5FD35F` / red `#D9534F` translucent tint (spec §12.2) |

## Format

Portrait mobile, touch-only, WebGL (spec §2). 3D — so "dimensions" govern meshes/prefabs and the grid, not sprite sheets.

| Thing | Value | Source |
|---|---|---|
| Reference resolution | **1080 × 1920** (9:16 design target) | portrait phone, spec §2 |
| Aspect handling | Design for 9:16; must survive 9:19.5–9:21 (safe-area margins on HUD) | — |
| Camera | Orthographic, pitched ~55–60° ¾ top-down; ortho size + pan bounds from `GameConfigSO` | spec §12.5 |
| Grid cell | **1 Unity unit = 1 cell** (proposed convention); grid ~20×30 on XZ plane | spec §4.1 |
| Station footprint | 1×1 cell; mesh ≈ **0.9 unit** to leave a visible gutter | spec §4.1 |
| VoidPet mesh | Sits on top of its station; ≈ 0.5–0.7 unit tall, reads at min zoom | spec §10.3 |
| Asset delivery | **Prefab / mesh / material** references on the SO (never sprite paths, never texture atlases) | spec §12.8, §14 |
| Scaling | URP handles it; no integer-scale/nearest-neighbour concerns (not pixel art) | — |
| World-space UI | Billboards to face the camera every frame (constant yaw/pitch, ortho) | spec §12.6 |

## Motion

- **Easing character:** **snappy & bouncy** (Q11a). Overshoot-and-settle. Default pop: scale `0 → 1.12 → 1.0`, ease-out-back, **~0.18s**. Collect-pop and placement-drop use the same curve so the whole game feels of a piece.
- **Ready-to-collect:** hop/bounce loop — vertical **~0.1 unit**, period **~0.6s**, ease-in-out, plus the floating icon above (spec §12.6).
- **Idle life:** assigned **VoidPets bob/breathe** — vertical **~0.05 unit**, period **~1.5s**; stations stay static (Q12a). Focuses motion on the characters, which suits the expressive IP.
- **Feedback feel:** every tap that does something gives a pop; every completion gives a bounce; every reward gives a glow-flash in the void accent. Nothing important happens without a visible beat.
- **Tooling (Q18):** hand-roll these with tweens now. **MoreMountains Feel is deferred to a dedicated "juice" milestone** — it's inspector-wired, doesn't speed up first-playable, and adopting it later is an additive swap (a View listener calls `PlayFeedbacks()` off a domain event), not a rewrite. Keep placeholder juice out of Core/event layer so the swap stays clean.

## Type & UI

- **Chrome (Q13a):** chunky, rounded, tactile. Fat rounded panels (**corner radius ~24–32px @1080w**), big rounded buttons, soft drop shadows, generous padding. HayDay lineage, thumb-first.
- **Touch targets:** minimum **~120px @1080w** (≈ 44pt) on any tappable control.
- **Typeface (Q14a):** one **rounded friendly sans** — Fredoka / Baloo / Nunito family feel. Weights: **Bold** (headers, numbers), **SemiBold** (buttons/labels), **Regular** (body/blurbs). VoidPet quotes render *italic in quotation marks* (spec §12.4) — if the family lacks italic, use a skewed/oblique fallback.
- **Iconography (Q15a):** filled, rounded, single-weight. Resource icons = simple rounded flat symbols. Consistent with chrome.
- **HUD is screen-space UGUI** overlaid on the 3D world (spec §12.1). World-space feedback (bars, ready icons, hearts) is separate and billboarded (spec §12.6).
- **The void accent in UI:** used only where UI touches void content — the VoidPet menu, egg/hatch popups, relationship hearts, void-event toasts. Regular farm UI stays warm/neutral.

## Audio

In scope for the prototype (confirmed). Register: **cozy-cosmic, soft, poppy.**

- **Music (Q16a):** one **warm, gentle, slightly dreamy/spacey ambient loop.** Mellow, non-intrusive, loops forever. Single track for the prototype. (Adaptive/layered is out of scope — flagged for later.)
- **SFX (Q17a):** soft, rounded, "poppy" — never harsh or realistic-foley. One SFX per **distinct player action / system event**, sourced from the §15 event catalog. Signature moments:
  - Collect-pop (satisfying, the core loop's reward beat)
  - Soft UI tap / panel open-close
  - Sparkly **hatch** + **level-up** chime (void-flavored shimmer)
  - Whooshy **void-event** sting (Dopamine Rain etc.)
  - Order-fulfilled cash chime, station-place thunk (soft), blocked/storage-full soft error
- **Void audio flavor:** hatch, level-up, and void events carry a faint shimmer/reverb tail that ordinary farm SFX don't — the sonic equivalent of the reserved violet glow.

## Placeholder Policy

Per spec §12.6 and CLAUDE.md — primitives and untextured meshes are correct until proven otherwise, and **nothing about a placeholder may leak into code or the event layer** (spec §12.8).

- **Stations:** one Unity **primitive mesh per station type** (distinct silhouettes — field=quad, silo=cylinder, henhouse=cube, others TBD), tinted by `placeholderColor` (§ table above), URP lit material.
- **VoidPets:** a placeholder primitive (e.g. dark tinted capsule/sphere with a small emissive "eye") until the real IP mesh is converted 2D→3D. The dark body + violet emissive already approximates the final read.
- **Feedback:** hand-rolled tweens + world-space UGUI bars/icons; billboarded.
- **UI:** UGUI defaults / primitives until `ui_inventory` mockups exist; then build to the mockup per surface (spec §12.8).
- **Swap mechanism:** every station/resource/pet SO carries its own **mesh/prefab/material** reference (spec §14). A real asset drops into the exact slot the primitive occupied — a designer-side SO edit, never a code change.

## Decided vs Open

**Decided (this guide):**
- Cozy-cosmic register; warm world × sleek dark void-familiars; single reserved violet↔cyan glow.
- Toon-lit URP, no outlines, bloom on void elements only.
- Split shape language (round world / sleek creatures).
- Color roles + state-signal mapping (values tunable, roles fixed).
- Chunky rounded UI, rounded-sans type, filled icons.
- Audio in scope: one ambient loop + poppy SFX per event; Feel deferred.
- Creatures come from existing IP — silhouette + indigo sheen + glowing eyes + emotion-naming preserved in 2D→3D conversion.

**Open / deferred:**
- **Exact hex values, ortho size, cell size, tween magnitudes, frame timings** — all tuned in play (spec §17); this guide fixes the roles and starting values only.
- **Which 6 of the IP creatures are the prototype's species** (spec §10.2, §17) and their **trait affinities** — a design/`asset_list` decision, not a style one. The emotion-naming (Determination, Joy, Apathy…) is a strong hint for affinity mapping but is out of scope here.
- **Remaining station primitives** (Pasture, Creamery, Bakery, Workshop, Order Board) marked TBD above.
- **Typeface final pick** within the rounded-sans family.
- **VoidPet rarity visual treatment** (Common/Rare/Epic, spec §10.2) — likely more/brighter void-glow and more elaborate silhouette for higher rarity, but not yet specified.
