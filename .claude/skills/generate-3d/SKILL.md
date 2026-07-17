---
name: generate-3d
description: >
  Generate 3D models from 2D source images or text prompts. Use when the user
  wants to create GLB/3D assets, convert concept art to 3D models, or run the
  multiview-to-mesh pipeline. Handles source image discovery, multiview
  generation, API dispatch (Hunyuan3D, Tripo, Meshy), auto-optimization, and
  optional retexturing for batch consistency.
metadata:
  assetBot:
    commands:
      - asset-bot generate 3d
    references:
      - ../../../references/CLI-REFERENCE.md
      - references/QWEN-MULTIANGLE-REFERENCE.md
      - references/TRIPO-REFERENCE.md
      - references/FAL-3D-REFERENCE.md
---

# generate-3d

High-level 3D generation orchestrator. Handles the full pipeline from 2D source image to 3D model with optional retexturing. Includes concept-to-3D workflows with human review gates at each step.

**Do not call api-fal or api-tripo directly for 3D generation.** Use this skill's CLI commands instead.

## CLI Commands

```bash
# Multiview pipeline (default, best quality)
asset-bot generate 3d --prompt "Stylized robot explorer" --category characters --pipeline multiview --json

# From explicit source image
asset-bot generate 3d --prompt "Robot explorer" --category characters --source-image concepts/robot-explorer.png --json

# Text-to-3D (no source image)
asset-bot generate 3d --prompt "Low-poly treasure chest" --category props --pipeline text-to-3d --api tripo3d-direct --json

# Single-image quick prototype
asset-bot generate 3d --prompt "Shield icon" --category props --pipeline single-image --json

# With seed for reproducibility
asset-bot generate 3d --prompt "Fantasy sword" --category weapons --seed 200001 --json
```

For exact parameters and flags, run `asset-bot generate 3d --help` or see `../../../references/CLI-REFERENCE.md`.

## Multiview-First Workflow (IMPORTANT)

**Always prefer the multiview pipeline for 3D generation.** Multiview images (front/right/back/left) dramatically improve 3D reconstruction quality.

1. **Generate multiview images first** using `generate-multiview` (or the `multiview` function)
2. **Review the multiview outputs** with the user — ensure they look correct from all angles
3. **Convert to 3D** using `convertMultiview`

If the user asks for a 3D model and no multiview images exist yet, **suggest generating multiviews as the first step.** Single-image 3D is a quick prototype only.

## Pipeline Overview

```
Source Image → generate-multiview (4 views) → Hunyuan3D v3.1 Multiview → GLB
                                                                          ↓
                                                                 Optional Retexture (Meshy)
                                                                          ↓
                                                                 Auto-Optimize (web-standard)
```

**Automatic Optimization:** All generated GLBs are auto-optimized via `util-optimize-mesh` with the `web-standard` preset (85–97% size reduction). Both original and optimized models are saved:

- `candidate_001.glb` — Original
- `candidate_001.optimized.glb` — Web-ready

Set `skipOptimize: true` to disable.

## Pipeline Selection

| Pipeline              | Steps                                     | When to Use                          |
| --------------------- | ----------------------------------------- | ------------------------------------ |
| `multiview` (default) | Source → multiview → Hunyuan3D v3.1 → GLB | **Best quality, always recommended** |
| `single-image`        | Source → Hunyuan3D image-to-model → GLB   | Quick prototype only                 |
| `text-to-3d`          | Prompt → Tripo v3 text-to-model → GLB     | No source image available            |

## 3D API Selection

| API              | Default | Supports                | Notes                                 |
| ---------------- | ------- | ----------------------- | ------------------------------------- |
| `hunyuan3d`      | Yes     | multiview, single-image | v3 (default) or v3.1 Pro, PBR support |
| `tripo3d-direct` | No      | All pipelines           | Up to 2M polys, text-to-3d support    |
| `meshy`          | No      | multiview only          | Meshy 5 via fal, PBR support          |

## Hunyuan3D Versions

| Version        | Views   | Notes                             |
| -------------- | ------- | --------------------------------- |
| `v3` (default) | Up to 4 | Supports LowPoly generation type  |
| `v3.1`         | Up to 8 | Better reconstruction, no LowPoly |

Set `hunyuanVersion: "v3.1"` for the newer endpoint.

## Concept-to-3D Pipeline

Convert concept art into 3D models with review gates at each step.

### Pipeline

```
Prepared Concept (approved, bg-removed)
  ├─ standardize ── Re-render at consistent 3/4 angle via Nano Banana
  │                 Uses concept as reference → consistent camera angle
  │                 ← REVIEW before proceeding
  ├─ derive3d ───── Multiview generation (NB turnsheet or Qwen)
  │                 Use multiviewOnly: true to stop for review
  │                 ← REVIEW each angle individually
  └─ convertTo3d ── Approved multiview images → Hunyuan3D → GLB
                    ← REVIEW final 3D model
```

### standardize

Re-render concept at consistent angle via Nano Banana.

| Param                | Type    | Required | Default                                 |
| -------------------- | ------- | -------- | --------------------------------------- |
| `projectId`          | string  | yes      | —                                       |
| `category`           | string  | yes      | —                                       |
| `sourceAssetId`      | string  | yes      | —                                       |
| `subjectDescription` | string  | yes      | —                                       |
| `cameraAngle`        | string  | no       | `"3/4 elevated angle from above-right"` |
| `autoApprove`        | boolean | no       | false                                   |

Creates `{sourceAssetId}-standardized` with `source-concept` refLink. Cost: ~$0.04.

### derive3d

Multiview generation with optional review gate.

| Param             | Type    | Required | Default          |
| ----------------- | ------- | -------- | ---------------- |
| `projectId`       | string  | yes      | —                |
| `category`        | string  | yes      | —                |
| `assetId`         | string  | yes      | —                |
| `outputPath`      | string  | yes      | —                |
| `multiviewMethod` | enum    | no       | `"nb-turnsheet"` |
| `multiviewOnly`   | boolean | no       | false            |

### convertTo3d

Convert existing multiview images to 3D. Expects images at `outputPath/multiview/{front,right,back,left}/candidate_001.png`.

| Param        | Type   | Required | Default       |
| ------------ | ------ | -------- | ------------- |
| `projectId`  | string | yes      | —             |
| `category`   | string | yes      | —             |
| `assetId`    | string | yes      | —             |
| `outputPath` | string | yes      | —             |
| `api`        | enum   | no       | `"hunyuan3d"` |
| `faceLimit`  | number | no       | API default   |

### Recommended Concept-to-3D Workflow

```
1. prepare (via generate-image skill) → bg removal, centering
   → REVIEW: background removed, centered
2. standardize → re-render at consistent 3/4 angle
   → REVIEW: does the 3/4 view preserve identity?
3. derive3d (multiviewOnly: true) → generate multiview images
   → REVIEW: each of the 4 angles individually
4. convertTo3d → convert approved views to 3D
   → REVIEW: GLB in 3D viewer
```

## Core Functions

- `generate(args)` — Full pipeline: source → multiview → 3D → optional retexture → auto-optimize
- `prepareGeneration(args)` — Dry run: show plan without executing
- `findSourceImage(args)` — Discover approved 2D concept art for 3D conversion
- `generateMultiview(args)` — Re-exported from `generate-multiview`
- `convertMultiview(args)` — Convert existing multiview images to 3D (includes auto-optimize)
- `retextureBatch(args)` — Apply shared style to multiple GLBs

## Source Image Discovery

`findSourceImage()` searches in order:

1. **Explicit path** — `sourceImagePath` provided directly
2. **Specific asset** — `sourceAssetId` + `sourceCategory` lookup
3. **Same category** — any approved image asset in `targetCategory`
4. **Cross-category** — approved image assets in other categories

## Multiview Generation

Handled by **`generate-multiview`** skill. See its [SKILL.md](../generate-multiview/SKILL.md).

| Method                   | Best For                                     |
| ------------------------ | -------------------------------------------- |
| `nb-turnsheet` (default) | Subjects (characters, props, vehicles)       |
| `qwen`                   | Scenes/locations (environments, backgrounds) |

## generate Parameters

| Param             | Type    | Default          | Notes                                           |
| ----------------- | ------- | ---------------- | ----------------------------------------------- |
| `projectId`       | string  | required         |                                                 |
| `category`        | string  | required         |                                                 |
| `outputPath`      | string  | required         |                                                 |
| `pipeline`        | enum    | `"multiview"`    | `"multiview"`, `"single-image"`, `"text-to-3d"` |
| `api`             | enum    | `"hunyuan3d"`    | `"hunyuan3d"`, `"tripo3d-direct"`, `"meshy"`    |
| `hunyuanVersion`  | enum    | `"v3"`           | `"v3"` or `"v3.1"`                              |
| `sourceImagePath` | string  | auto             | Explicit source image                           |
| `sourceAssetId`   | string  | auto             | Source asset (uses discovery)                   |
| `prompt`          | string  | —                | Required for text-to-3d                         |
| `multiviewMethod` | enum    | `"nb-turnsheet"` | `"nb-turnsheet"` or `"qwen"`                    |
| `faceLimit`       | number  | API default      | Max polygon count                               |
| `retexturePrompt` | string  | —                | Retexture output with Meshy                     |
| `skipOptimize`    | boolean | `false`          | Skip auto GLB optimization                      |

## Seed Discipline

| Seed            | Controls                  | Category Ranges                                     |
| --------------- | ------------------------- | --------------------------------------------------- |
| `multiviewSeed` | Multiview generation      | Use same range as category                          |
| `modelSeed`     | 3D geometry (Tripo only)  | Characters 100k-199k, Props 200k-299k, UI 300k-399k |
| `textureSeed`   | 3D texturing (Tripo only) | Same ranges                                         |

## Retexturing for Batch Consistency

`retextureBatch()` applies one shared style prompt across multiple GLBs. Use one consistent retexture prompt per game for visual uniformity.

## Cost Estimates

| Operation                         | Cost   |
| --------------------------------- | ------ |
| standardize                       | ~$0.04 |
| derive3d (nb-turnsheet)           | ~$0.04 |
| derive3d (qwen)                   | ~$0.15 |
| convertTo3d                       | ~$0.25 |
| Full concept-to-3D (nb-turnsheet) | ~$0.33 |

## Integration

- `generate-multiview` — Multiview generation (nb-turnsheet / Qwen)
- `api-fal` — Hunyuan3D v3.1, Meshy, Tripo v2.5
- `api-tripo` — Tripo v3 direct API
- `generate-image` — Nano Banana for standardize step
- `manage-assets` — Record CRUD, source discovery
- `util-optimize-mesh` — Auto-optimization (web-standard preset)

## References

- [generate-multiview SKILL.md](../generate-multiview/SKILL.md)
- [Qwen multi-angle reference](references/QWEN-MULTIANGLE-REFERENCE.md)
- [Tripo3D prompting reference](references/TRIPO-REFERENCE.md)
- [fal.ai 3D & retexture reference](references/FAL-3D-REFERENCE.md)
