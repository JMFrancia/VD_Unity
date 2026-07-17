---
name: rig-animate
description: >
  Rig and animate humanoid 3D models with quality gates at each step. Use
  when the user wants to add rigging or animations to a 3D character, convert
  a T-pose image into an animated character, or generate motion from text
  descriptions. Supports Meshy preset animation library and Uthana
  text-to-motion. Includes an end-to-end pipeline from 2D image to animated
  GLB.
metadata:
  assetBot:
    commands:
      - asset-bot generate image
      - asset-bot generate 3d
    references:
      - ../../../references/CLI-REFERENCE.md
---

# rig-animate

Rig and animate humanoid 3D models with quality gates at each step.

## Pipeline

```text
RECOMMENDED:  asset-bot generate image (NB T-pose) -> rig-animate (multiview → HY3D → rig + animate)
ALTERNATIVE:  asset-bot generate 3d (external)     -> rig-animate (rig + animate + texture patch)
ONE-COMMAND:  full() — 2D image or GLB → 3D model → animated character (auto-picks provider)
```

```text
Source (T-pose image, external GLB, Meshy task ID, or prompt)
  |
  +- prepareModel ------- Acquire a model for rigging
  |                        RECOMMENDED: sourceImagePath (NB multiview → HY3D)
  |                        Alt 1: external GLB from any source
  |                        Alt 2: existing Meshy task ID
  |                        AVOID: Meshy text-to-3D prompt
  |                        <- REVIEW: does the model appear in T-pose?
  |
  +- Animation path A (Meshy — preset library):
  |  +- rigModel --------- Rig with pre/post material audit
  |  |                      Auto-patches textures if rigged GLB has 0 materials
  |  +- animateModel ------ Apply animations from Meshy's 586-animation library
  |
  +- Animation path B (Uthana — custom text-to-motion):
     +- animateModelUthana  Upload model → generate motion → download animated GLB
                            Auto-optimizes oversized models (>20 MB → rig-ready preset)
                            Falls back to default "Tar" character if no model provided

  full() orchestrates the entire pipeline in one call.
```

## CLI Commands

Use CLI commands for asset creation steps:

```bash
# Step 1: Create a clean T-pose source image
asset-bot generate image --prompt "full-body character in strict T-pose, neutral background" --category characters --aspect-ratio 1:1 --json

# Step 2: Produce a GLB from that image via multiview pipeline
asset-bot generate 3d --prompt "same character as source image" --category characters --pipeline multiview --source-image .asset-bot/runs/generate-image/characters/<run-id>/candidate_001.png --json
```

Step 3: Run rig/animation via `@asset-bot/core` `rigAnimate.*` APIs in custom automation.

For exact parameters and flags, run `asset-bot generate image --help` or `asset-bot generate 3d --help`, or see `../../../references/CLI-REFERENCE.md`.

## Image-First Workflow (RECOMMENDED)

The best results come from generating a 2D T-pose reference image first using Nano Banana (via `asset-bot generate image`), then converting to 3D via HY3D multiview pipeline. This gives you:

1. **Full control over pose** — draw the exact T-pose you want in 2D
2. **Better geometry** — HY3D multiview reconstruction produces cleaner meshes than Meshy image-to-3D
3. **Style consistency** — use project refs in the 2D generation step

`prepareModel` with `sourceImagePath` automatically runs: NB multiview (4 angles) → Hunyuan3D v3 → GLB.

```ts
import { rigAnimate } from '@asset-bot/core';

// Step 1: Use `asset-bot generate image` to create a T-pose source image
// Step 2: Use `asset-bot generate 3d --pipeline multiview` to create GLB, or call prepareModel directly
const model = await rigAnimate.prepareModel(runtime, {
  sourceImagePath: '/tmp/pikachu-2d/candidate_001.png',
  outputPath: '/tmp/pikachu-rig',
});

// Step 3: Rig (uses modelPath from prepareModel output)
const rigged = await rigAnimate.rigModel(runtime, {
  modelPath: '/tmp/pikachu-rig/candidate_001.glb',
  outputPath: '/tmp/pikachu-rig',
});

// Step 4: Animate
const animated = await rigAnimate.animateModel(runtime, {
  rigTaskId: rigged.taskId,
  actionIds: [0, 10, 100],
  outputPath: '/tmp/pikachu-rig',
});
```

## Key Value

1. **Image-first workflow** — convert 2D T-pose images to rigged 3D characters
2. **External GLB support** — accepts models from any source (Hunyuan3D, Tripo, etc.)
3. **Post-rig texture patching** — auto-injects baseColor texture when rigging strips materials
4. **Material audit** — detects PBR channel loss during rigging
5. **T-pose prompt augmentation** — wraps prompts with literal pose enforcement (text-to-3D fallback)
6. **Uthana text-to-motion** — custom animations from natural language descriptions
7. **One-command pipeline** — `full()` handles everything from 2D image to animated GLB

## Commands

### prepareModel

Acquire a model for rigging. Four source modes.

| Param             | Type   | Required | Default     | Notes                                                      |
| ----------------- | ------ | -------- | ----------- | ---------------------------------------------------------- |
| `sourceImagePath` | string | one of   | --          | T-pose character image → NB multiview → HY3D (RECOMMENDED) |
| `modelPath`       | string | one of   | --          | Local GLB from any source                                  |
| `prompt`          | string | one of   | --          | Meshy text-to-3D (AVOID)                                   |
| `meshyTaskId`     | string | one of   | --          | Existing Meshy task ID                                     |
| `aiModel`         | enum   | no       | `"meshy-6"` | For text-to-3D mode only                                   |
| `outputPath`      | string | yes\*    | --          | Directory for outputs (\*required with sourceImagePath)    |

### rigModel

Rig a model with pre/post material audit and auto texture patching.

| Param                | Type    | Required | Default | Notes                             |
| -------------------- | ------- | -------- | ------- | --------------------------------- |
| `modelPath`          | string  | one of   | --      | Local GLB file path               |
| `meshyTaskId`        | string  | one of   | --      | Meshy task ID                     |
| `modelUrl`           | string  | one of   | --      | Public URL to GLB                 |
| `sourceGlbPath`      | string  | no       | --      | Source GLB for texture patching   |
| `sourceGlbUrl`       | string  | no       | --      | Source GLB URL for pre-rig audit  |
| `heightMeters`       | number  | no       | 1.7     | Character height in meters        |
| `textureImageUrl`    | string  | no       | --      | Public URL to UV texture          |
| `autoExtractTexture` | boolean | no       | true    | Extract baseColor from source GLB |
| `outputPath`         | string  | no       | --      | Directory for outputs             |

**Auto texture patch:** When the rigged GLB has 0 materials and a source GLB is available (via `sourceGlbPath` or `modelPath`), the pipeline automatically extracts the baseColor texture from the source and injects it into the rigged GLB. Patched files are returned in `patchedPaths`.

### animateModel

Apply one or more animations from Meshy's 586-animation preset library.

| Param        | Type     | Required | Default | Notes                 |
| ------------ | -------- | -------- | ------- | --------------------- |
| `rigTaskId`  | string   | yes      | --      | Completed rig task ID |
| `actionIds`  | number[] | yes      | --      | Animation IDs (0-586) |
| `outputPath` | string   | no       | --      | Directory for outputs |

### animateModelUthana

Animate a 3D model using Uthana text-to-motion. Custom animations from natural language descriptions.

| Param           | Type    | Required | Default        | Notes                                              |
| --------------- | ------- | -------- | -------------- | -------------------------------------------------- |
| `prompt`        | string  | yes      | --             | Motion description (literal, no metaphors)         |
| `modelPath`     | string  | no       | --             | GLB to upload to Uthana (auto-optimized if >20 MB) |
| `characterId`   | string  | no       | `cXi2eAP19XwQ` | Pre-uploaded Uthana character ID                   |
| `outputPath`    | string  | yes      | --             | Output directory                                   |
| `length`        | number  | no       | 5              | Motion duration in seconds (0.25-10)               |
| `format`        | enum    | no       | `"glb"`        | `"glb"` or `"fbx"`                                 |
| `footIk`        | boolean | no       | true           | Foot inverse kinematics (prevents foot sliding)    |
| `characterName` | string  | no       | file basename  | Name when uploading new character                  |
| `model`         | string  | no       | --             | Diffusion model variant                            |
| `seed`          | number  | no       | --             | Random seed (1-99999) for reproducibility          |

**Smart defaults:**

- No model → uses Uthana's built-in "Tar" character (good for testing)
- Model >20 MB → auto-optimized with `rig-ready` preset (texture compression, no geometry loss)
- `footIk: true` → prevents foot sliding for grounded animations
- Requires `UTHANA_API_KEY` environment variable

### full

End-to-end pipeline: 2D image (or GLB) → 3D model → animated character.

| Param               | Type     | Required | Default            | Notes                                                     |
| ------------------- | -------- | -------- | ------------------ | --------------------------------------------------------- |
| `sourceImagePath`   | string   | one of   | --                 | 2D T-pose character image                                 |
| `modelPath`         | string   | one of   | --                 | Existing GLB model                                        |
| `outputPath`        | string   | yes      | --                 | Output directory                                          |
| `animationProvider` | enum     | no       | `"uthana"`         | `"uthana"` (text-to-motion) or `"meshy"` (preset library) |
| `motionPrompt`      | string   | no       | `"idle breathing"` | For Uthana: motion description                            |
| `motionLength`      | number   | no       | 5                  | For Uthana: duration in seconds                           |
| `actionIds`         | number[] | no       | `[0]`              | For Meshy: animation IDs                                  |

### listAnimations

List Meshy animation categories and IDs. Accepts optional `{ category: string }` filter.

## Known Limitations

- **PBR material loss during rigging** — Meshy bakes PBR textures, dropping normal maps and metallic/roughness. The pipeline detects this and auto-patches baseColor when possible.
- **textureImageUrl requires public URL** — Meshy's texture preservation param needs a hosted URL, not a local path. The auto-patch approach works with local files.
- **Humanoid models only** — Both Meshy rigging and Uthana auto-rig require clear limb structure.
- **Large file warning** — Hunyuan3D GLBs can be 45+ MB. Using `modelPath` converts to data URI (33% bloat). For large files, host the GLB and use `modelUrl` instead.
- **Uthana 20 MB upload limit** — `animateModelUthana` auto-optimizes with `rig-ready` preset when models exceed this limit.

## Integration

This pipeline integrates with:

- `asset-bot generate image` — recommended first step for a clean T-pose source image
- `asset-bot generate 3d` — multiview to GLB conversion path for image-first workflows
- `@asset-bot/core rigAnimate.prepareModel()` - model acquisition before rigging
- `@asset-bot/core rigAnimate.rigModel()` / `rigAnimate.animateModel()` - Meshy rig + preset animation path
- `@asset-bot/core rigAnimate.animateModelUthana()` - Uthana text-to-motion path
- `@asset-bot/core optimizeMesh()` - auto-optimization for oversized rig inputs

## Cost Estimates

| Operation                           | Cost   | API Calls               | Time      |
| ----------------------------------- | ------ | ----------------------- | --------- |
| prepareModel (sourceImagePath)      | ~$0.35 | 2 (NB multiview + HY3D) | 3-6 min   |
| prepareModel (external GLB)         | $0     | 0                       | instant   |
| prepareModel (Meshy text-to-3D)     | ~$0.40 | 2                       | 2-5 min   |
| rigModel                            | ~$0.20 | 1                       | 1-3 min   |
| animateModel (per animation, Meshy) | ~$0.10 | 1                       | 30s-1 min |
| animateModelUthana                  | ~$0.05 | 1-2 (motion + download) | 10-30s    |
| full (image → Uthana)               | ~$0.40 | 4                       | 4-7 min   |
| full (image → Meshy, 3 anims)       | ~$0.85 | 6                       | 6-12 min  |
