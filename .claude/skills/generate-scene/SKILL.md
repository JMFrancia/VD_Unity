---
name: generate-scene
description: >
  Generate 3D scenes and environments using World Labs Marble. Use when the
  user wants to create explorable 3D environments, Gaussian splat scenes, or
  iteratively build game maps with inpainted points of interest. Handles
  multiview generation, scene polling, download of SPZ/collider/panorama
  assets, and a scene-builder workflow with FLUX Pro Fill inpainting.
metadata:
  assetBot:
    commands:
      - asset-bot generate scene
    references:
      - ../../../references/CLI-REFERENCE.md
---

# generate-scene

High-level 3D scene generation using World Labs Marble. Handles multiview generation, media upload, world generation, polling, and asset download. Includes scene-builder workflows for iterative POI inpainting via FLUX Pro Fill.

**Do not call api-worldlabs directly for scene generation.** Use this skill's CLI commands instead.

## CLI Commands

```bash
# Generate a scene from a source image
asset-bot generate scene --prompt "Medieval courtyard with fountain" --category environments --source-image concept.png --json

# Specify splat resolution
asset-bot generate scene --prompt "Forest clearing" --category environments --resolution full --json
```

For exact parameters and flags, run `asset-bot generate scene --help` or see `../../../references/CLI-REFERENCE.md`.

## Recommended Workflow (Two-Step with Review)

**Always review multiview images before sending to World Labs.** This prevents wasted API calls on bad inputs.

### Step 1: Generate Multiview Images

Use `generateMultiviewImages` to create 4 images at 4K resolution (5504×3072): front, right, back, left.

### Step 2: Review ALL 4 Images (Required)

**You MUST review all 4 views before continuing.** Check each view for:

- [ ] Consistent art style across all 4 views
- [ ] Ground-level perspective maintained (not aerial)
- [ ] No major artifacts, distortions, or hallucinations
- [ ] Reasonable scene continuity when rotated

**If any view is bad:** Regenerate the multiview set.

### Step 3: Send to World Labs

Use `continueFromMultiview` to upload approved images and generate the 3D scene.

## Pipeline Overview

### Default: Multiview-First (Recommended)

```
Concept art → 4K ground-level multiview (4 directions) → World Labs multi-image → Poll → Download SPZ + Collider + Panorama
```

Quality: ★★★★★ (multi-image input, 4K resolution, ground-level perspective)

### Fallback: Panorama

```
Concept art → 360° equirectangular pano → World Labs single-image → Poll → Download
```

Use `usePano: true` when you already have a panoramic image or speed > quality. Quality: ★★★☆☆.

## Ground-Level Perspective (Default)

All generation defaults to **ground-level perspective** — eye-height viewpoint as if standing on the ground. For aerial/bird's-eye views, use `perspective: 'aerial'`.

## Core Functions

### Two-Step Workflow (Recommended)

- `generateMultiviewImages(args)` — Generate 4K multiview images for review
- `continueFromMultiview(args)` — After approval, send to World Labs

### Single-Step (Legacy)

- `generate(args)` — Full pipeline with project context (no review step)
- `generateDraft(args)` — Quick draft (defaults to mini model)

### Other Pipelines

- `generateFromVideo(args)` — Video → scene
- `generateFromMultiview(args)` — Pre-made multi-angle images → scene
- `findInputs(args)` — Discover approved images/videos for scene generation

## generate Parameters

| Param          | Type    | Default             | Notes                                        |
| -------------- | ------- | ------------------- | -------------------------------------------- |
| `projectId`    | string  | required            |                                              |
| `category`     | string  | required            |                                              |
| `outputPath`   | string  | required            |                                              |
| `prompt`       | string  | —                   | Text prompt                                  |
| `inputImage`   | string  | —                   | Local source image                           |
| `model`        | string  | `"Marble 0.1-plus"` | `"plus"` or `"mini"`                         |
| `perspective`  | enum    | `"ground"`          | `"ground"` or `"aerial"`                     |
| `locationHint` | string  | —                   | For multiview prompts (e.g. "the courtyard") |
| `usePano`      | boolean | `false`             | Use panorama pipeline                        |

## Multiview Prompt Templates

### Ground-Level (Default)

- **Front:** "Ground-level view standing in [location]. Eye-level perspective as if standing on the ground."
- **Right:** "Same scene, turned 90 degrees right. Ground-level eye-height perspective."
- **Back:** "Same scene, turned 180 degrees around. Ground-level eye-height perspective."
- **Left:** "Same scene, turned 90 degrees left. Ground-level eye-height perspective."

### Aerial

- **Front:** "Elevated view looking down at [location]. Bird's eye perspective."
- **Right/Back/Left:** "Same scene, turned [X] degrees. Elevated aerial perspective."

## Output Structure

```
outputPath/
  multiview/              — 4K multiview images
    front.png / right.png / back.png / left.png
  scene_100k.spz          — Low-res Gaussian splat
  scene_500k.spz          — Mid-res Gaussian splat
  scene_full.spz          — Full-res Gaussian splat
  collider.glb            — Collision mesh
  panorama.jpg            — Panoramic image
  thumbnail.jpg           — Scene thumbnail
```

## Scene Builder (Iterative Inpainting)

Build scenes/maps by inpainting POIs onto a foundational scene image using FLUX Pro Fill for mask-based inpainting. Preserves exact dimensions and pixels outside the mask.

### Builder Flow

```
starter → [cleanup] → [simplify] → addPoi → addPoi → ... → done
            optional    optional       ▲           │
                                       └───────────┘  (revert to any snapshot)
```

### Steps

1. **starter** — Generate scene image with NB or RD. User picks a candidate.
2. **cleanup** (optional) — Mark rectangles over unwanted elements. Each rectangle is inpainted with natural surroundings via FLUX Pro Fill.
3. **simplify** (optional) — Flatten scene into a clean foundation.
4. **addPoi** — Mark a rectangle, create mask, inpaint POI via FLUX Pro Fill.
5. Repeat addPoi for each POI. `revert` to any snapshot at any time.

### Builder Commands

#### starter — Generate scene candidates

| Param           | Type     | Default         | Notes                                     |
| --------------- | -------- | --------------- | ----------------------------------------- |
| `projectId`     | string   | required        |                                           |
| `prompt`        | string   | required        | Scene description (literal, no metaphors) |
| `sceneModel`    | string   | `"nano-banana"` | `"nano-banana"` or `"retro-diffusion"`    |
| `refs`          | string[] | auto-discovered | Reference images                          |
| `numCandidates` | number   | 3               |                                           |

#### cleanup — Remove unwanted elements

| Param       | Type          | Default  | Notes                   |
| ----------- | ------------- | -------- | ----------------------- |
| `projectId` | string        | required |                         |
| `sceneId`   | string        | required |                         |
| `removals`  | RemovalRect[] | required | `{ x, y, w, h, label }` |

#### addPoi — Add a point of interest

| Param       | Type             | Default  | Notes                      |
| ----------- | ---------------- | -------- | -------------------------- |
| `projectId` | string           | required |                            |
| `sceneId`   | string           | required |                            |
| `poiPrompt` | string           | required | What to generate (literal) |
| `poiLabel`  | string           | required | Short label                |
| `rect`      | `{ x, y, w, h }` | required | Placement rectangle        |

#### revert — Roll back to previous snapshot

| Param           | Type   | Required |
| --------------- | ------ | -------- |
| `projectId`     | string | yes      |
| `sceneId`       | string | yes      |
| `snapshotIndex` | number | yes      |

### Interactive Rectangle Tool

Use `/playground` to create an interactive HTML tool for marking rectangles. Output JSON format:

```json
{
  "rects": [
    { "x": 120, "y": 50, "w": 200, "h": 150, "label": "the tree", "mode": "remove" },
    { "x": 300, "y": 200, "w": 180, "h": 220, "label": "tavern", "mode": "add-poi" }
  ]
}
```

### Two Model Paths

`sceneModel` is set at scene creation and locked for the scene's lifetime.

| Step    | Nano Banana            | Retro Diffusion        |
| ------- | ---------------------- | ---------------------- |
| starter | NB with refs           | RD with `promptStyle`  |
| cleanup | FLUX Pro Fill (always) | FLUX Pro Fill (always) |
| addPoi  | FLUX Pro Fill (always) | FLUX Pro Fill (always) |

Recommend Retro Diffusion for pixel art scenes. NB for painted/illustrated styles.

## Cost

- Nano Banana: 4 images at 4K ≈ $0.40
- World Labs Mini: ~30-45s, lower cost
- World Labs Plus: ~5 min, higher quality
- FLUX Pro Fill: ~$0.03 per inpaint

## Integration

- `api-worldlabs` — World Labs Marble API
- `api-google-genai` — Nano Banana for multiview and starter
- `api-fal` — FLUX Pro Fill for inpainting
- `generate-pixel-art` — RD path for starter (dynamic import)
- `manage-assets` — Record CRUD, input discovery
- `util-preview-scene` — Interactive HTML viewers
