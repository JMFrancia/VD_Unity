# World Labs Marble API
## Research Summary: Best Inputs for High-Quality Game Scenes

This document summarizes **what the research and official guidance actually say** about inputs for achieving the **highest-quality scenes** in Marble, specifically for **game environments** and **engineering pipelines**.

---

## Executive takeaway

- **Multi-view inputs produce the highest spatial accuracy**
- **Ground-level perspective is the default for immersive scenes**
- **4K resolution (5504×3072) maximizes detail**
- **A single 360° pano is the best single asset (fallback)**
- **Video sweeps often outperform panos**
- **One good pano beats multiple weak images**

In short:

> **Multi-view (4K ground-level) > Video > Single pano > Single image**

---

## Default Pipeline: Multiview-First

The `generate-scene` skill uses **multiview as the default** pipeline:

```
Concept art → 4K ground-level multiview (4 directions) → World Labs multi-image → Download
```

### Why Multiview is Default

1. **Real parallax** — 4 camera positions provide actual depth information
2. **Ground-level immersion** — Eye-height perspective for games/VR
3. **4K resolution** — Maximum detail (5504×3072 per view)
4. **Quality tier ★★★★★** — Highest accuracy per World Labs docs

### Ground-Level Perspective

All generated multiviews default to **ground-level** (eye-height) perspective:
- First-person viewpoint as if standing on the ground
- Looking up at tall structures
- Foreground elements (paths, grass, objects) visible
- Suitable for immersive game environments

For aerial/bird's-eye views, use `perspective: 'aerial'`.

### Multiview Prompt Templates

**Ground-Level (default):**
- Front: "Ground-level view standing in [location]. Eye-level perspective, first-person viewpoint."
- Right: "Same scene, turned 90 degrees right. Ground-level eye-height perspective. Main subject out of frame."
- Back: "Same scene, turned 180 degrees around. Ground-level eye-height perspective."
- Left: "Same scene, turned 90 degrees left. Ground-level eye-height perspective. Main subject out of frame."

**Key principles:**
- Keep prompts minimal — let the reference image drive style
- Explicitly mention perspective ("Ground-level", "eye-height")
- Describe what's out of frame to help the model understand rotation

---

## Input types ranked by quality

### 1. Multi-image (highest accuracy)

**Recommended when available**

Marble explicitly supports:
- **4 directional views** (Front, Back, Left, Right)
- **6–8 images with auto-layout**

Why this is best:
- Multiple camera centers provide real parallax
- Occlusions resolve more cleanly
- Corners, depth breaks, and interiors reconstruct better
- Fewer floating artifacts and warped surfaces

Best use cases:
- Interior scenes
- Architecture
- Tight spaces
- Gameplay-critical geometry

Requirements:
- Same resolution and aspect ratio
- Consistent exposure and lighting
- Partial overlap for auto-layout
- Environment only (no people or animals)

Official guidance:
- https://docs.worldlabs.ai/marble/create/image-prompts
- https://docs.worldlabs.ai/marble/create/multi-image-prompts

---

### 2. Short video sweep (very strong)

**Often better than a single pano**

Marble supports short videos (≤30s) capturing a smooth sweep of a space.

Why it works:
- Provides continuous parallax
- Captures fine surface detail
- Resolves ambiguous geometry better than a static pano

Best practices:
- Smooth camera motion
- Minimal motion blur
- Consistent exposure
- 180°–360° coverage

Best use cases:
- Walkthroughs of interiors
- Real-world captures
- Cinematic environments

Official guidance:
- https://docs.worldlabs.ai/marble/create/video-prompts

---

### 3. Single 360° pano (best single input)

**Best single asset if you can only provide one thing**

Why panos work well:
- Full 360° context up front
- Strong lighting and material coherence
- Fewer hallucinated regions than flat images

Limitations:
- Single projection
- Limited parallax
- Depth ambiguity behind objects

Recommended pano spec:
- **True equirectangular**
- **2:1 aspect ratio**
- **2560 × 1280** minimum (official recommendation)

Higher quality tiers (practical):
- 4096 × 2048 (sweet spot)
- 8192 × 4096 (only if genuinely sharp)

Official guidance:
- https://docs.worldlabs.ai/marble/create/prompt-guides

---

### 4. Single flat image (lowest quality)

**Supported but least reliable**

Limitations:
- Marble must hallucinate unseen geometry
- Higher chance of warped depth
- Best only for concept-style scenes

Use only when:
- No pano or multi-view is available
- Visual mood matters more than layout accuracy

---

## Comparative summary

| Input Type | Geometry Accuracy | Lighting Coherence | Ease | Recommended |
|---|---|---|---|---|
| Multi-image | ★★★★★ | ★★★★☆ | Medium | Yes |
| Video sweep | ★★★★☆ | ★★★★☆ | Medium | Yes |
| Single pano | ★★★☆☆ | ★★★★★ | Easy | Yes |
| Single image | ★★☆☆☆ | ★★★☆☆ | Easy | Last resort |

---

## Key research conclusions

- **Multi-view is explicitly recommended** when accuracy matters
- **Panos are not the "best" input**, but they are the **best single input**
- **Lighting quality matters more than raw resolution**
- **One clean pano beats many noisy views**
- Marble performs best when given **real spatial signal**, not upscaled noise

---

## Practical recommendations for game teams

If you can choose:
- Use **multi-view** for gameplay-critical spaces
- Use **video** when capturing real environments
- Use **panos** for fast, reliable world generation
- Avoid relying on **single flat images** for final content

---

## Generating Equirectangular Panoramas from Concept Art

When you have concept art (flat perspective image) and need to convert it to equirectangular format for World Labs, use the **dual-reference approach**:

### The Problem

Text prompts alone (e.g., "create equirectangular panorama") don't reliably produce true equirectangular projection. Image generation models interpret "360 panorama" as "wide scenic view" rather than the actual spherical projection format.

### The Solution: Dual-Reference Technique

Use **two reference images**:

1. **Reference 1 (Projection Format):** An actual equirectangular image that demonstrates the correct projection — curved horizon, spherical distortion, seamless left-right wrap.

2. **Reference 2 (Content/Style):** Your concept art providing the scene content, layout, and art style.

### Prompt Template

```
CRITICAL - PROJECTION FORMAT:
You MUST use the exact equirectangular projection shown in Reference 1. This means:
- Curved horizon line
- Spherical distortion (stretched at top/bottom poles)
- Left edge seamlessly connects to right edge
- Full 360° wrap-around view
- Suitable for VR/360 viewer display

SCENE CONTENT:
Use Reference 2 for the scene content, layout, composition and painterly style.

Create a 360° panoramic image as if the viewer is standing in the middle of an open space and can look all around in every direction. Make the panorama seamless with no visible breaks.
```

### Implementation

The `generate-scene` skill implements this automatically via `preparePanorama()`. An equirectangular reference image is stored at `.claude/skills/generate-scene/refs/equirectangular-reference.jpg`.

### Aspect Ratio

Use **21:9** (the widest available in Nano Banana). True equirectangular is 2:1, but 21:9 (2.33:1) is close enough and produces good results.
