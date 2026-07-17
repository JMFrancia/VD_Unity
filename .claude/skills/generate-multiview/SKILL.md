---
name: generate-multiview
description: >
  Generate 4 angle views (front, right, back, left) from a single source
  image. Use when the user needs multi-angle turnaround views for 3D
  reconstruction, character sheets, or environment rotation. Supports Nano
  Banana turnaround sheet (best for characters and props) and Qwen camera
  rotation (best for scenes and environments).
metadata:
  assetBot:
    commands:
      - asset-bot generate 3d
    references:
      - ../../../references/CLI-REFERENCE.md
      - references/NANO-BANANA-REFERENCE.md
      - references/QWEN-MULTIANGLE-REFERENCE.md
      - references/CONSISTENT-PIPELINE-REFERENCE.md
---

# generate-multiview

Generate 4 angle views (front, right, back, left) from a single source image.

**Do not call api-fal or api-google-genai directly for multiview generation.** Use this skill's CLI commands instead.

## Method Selection

| Method                   | Best For                                         | Technique                                          | API Calls | Cost   |
| ------------------------ | ------------------------------------------------ | -------------------------------------------------- | --------- | ------ |
| `nb-turnsheet` (default) | **Subjects** (characters, props, vehicles)       | NB turnaround sheet (1 composed image, then split) | 1         | ~$0.04 |
| `qwen`                   | **Scenes/locations** (environments, backgrounds) | Qwen LoRA camera rotation (4 individual views)     | 4         | ~$0.15 |

**Rule of thumb:** If it's a thing (character, prop, ship), use `nb-turnsheet`. If it's a place (room, landscape, arena), use `qwen`.

## CLI Commands

Multiview generation is exposed through `asset-bot generate 3d` (it runs the multiview pipeline internally). Use direct `@asset-bot/core` calls only when building custom automation/code.

```bash
# Multiview pipeline (default)
asset-bot generate 3d --prompt "Stylized robot explorer, game-ready silhouette" --category characters --pipeline multiview --source-image concepts/robot-explorer.png --json

# Qwen method for scenes
asset-bot generate 3d --prompt "Medieval tavern interior" --category environments --pipeline multiview --source-image concepts/tavern.png --json
```

For exact parameters and flags, run `asset-bot generate 3d --help` or see `../../../references/CLI-REFERENCE.md`.

### Core API (advanced/custom flows)

```ts
import { generateMultiview } from '@asset-bot/core';

// NB turnaround sheet (default) — best for subjects
await generateMultiview(runtime, {
  sourceImagePath: '/path/to/concept.png',
  outputDir: 'out/',
});

// Qwen camera rotation — best for scenes
await generateMultiview(runtime, {
  sourceImagePath: '/path/to/scene.png',
  outputDir: 'out/',
  method: 'qwen',
  seed: 150000,
});
```

## Functions

- `generateMultiview(args)` — Main entry: routes to nb-turnsheet or qwen
- `splitSheet(args)` — Split a 2x2 turnaround sheet into 4 view images
- `generateLayoutTemplate(args)` — Generate a labeled 2x2 grid template PNG

## generateMultiview Parameters

| Param             | Type   | Default          | Notes                        |
| ----------------- | ------ | ---------------- | ---------------------------- |
| `sourceImagePath` | string | required         | Path to source image         |
| `outputDir`       | string | required         | Where to save outputs        |
| `seed`            | number | random           | Generation seed              |
| `method`          | enum   | `"nb-turnsheet"` | `"nb-turnsheet"` or `"qwen"` |

### nb-turnsheet Options

| Param                | Type   | Default           | Notes                               |
| -------------------- | ------ | ----------------- | ----------------------------------- |
| `layoutTemplatePath` | string | static (built-in) | Custom 2x2 layout template override |
| `imageSize`          | enum   | `"4K"`            | `"1K"`, `"2K"`, `"4K"`              |

### qwen Options

| Param               | Type   | Default     | Notes              |
| ------------------- | ------ | ----------- | ------------------ |
| `loraScale`         | number | API default | LoRA strength      |
| `guidanceScale`     | number | API default | CFG guidance scale |
| `numInferenceSteps` | number | API default | Inference steps    |
| `negativePrompt`    | string | —           | What to avoid      |

## Output

All methods output to: `outputDir/multiview/{front,right,back,left}/candidate_001.png`

The `nb-turnsheet` method also saves the unsplit sheet at `outputDir/multiview/_sheet/candidate_001.png`.

**Result fields (nb-turnsheet):**

- `paths` — front/right/back/left file paths
- `seed` — generation seed used (may differ from input if retried)
- `sheetPath` — path to the unsplit 2x2 sheet
- `qaResult` — `{ passed, verticalBleed, horizontalBleed }` from gutter check
- `attempt` — which attempt succeeded (1-based, max 3)

## NB Turnaround Sheet Method

1. Uses a static 2x2 layout template (`layout-template.png` in skill directory)
2. Calls Nano Banana Pro once with source image + layout template as references
3. **QA check** — samples gutter strips along the center cross for content bleed
4. If QA fails (bleed > 35%), retries up to 3 times with enhanced centering prompt
5. Splits the resulting 2x2 sheet into 4 quadrant PNGs via sharp
6. Saves individual views to the standard output structure

The prompt faithfully recreates the subject EXACTLY — same art style, rendering, colors, proportions, and all visual designs — while eliminating fine details that would not translate well to a 3D mesh.

**QA Check (gutter bleed detection):**

- Samples pixel strips flanking the center vertical and horizontal lines
- Skips the center 6px (model often draws grid lines there)
- Counts non-white pixels as bleed score (0.0 = perfect, 1.0 = fully occupied)
- Threshold: 0.35 — typical good sheets score 0.20–0.30 for large subjects
- On failure, retries with a different seed and a prompt that emphasizes quadrant containment
- After max retries, splits anyway and reports `qaResult.passed = false`

**Advantages:**

- Single API call (cheaper, faster)
- Native high resolution (up to 4K)
- Good subject identity preservation via reference image
- Automatic cleanup of fine details for 3D-readiness
- QA-gated: auto-retries on gutter overrun

**Limitations:**

- Model may not always produce perfect 2x2 grid alignment
- Complex scenes with backgrounds may not split cleanly
- Model sometimes adds text labels despite prompt — seed-dependent
- Side profiles may have a slight 3/4 tilt if the source image is at an angle — reroll with a different seed if needed

## Qwen Method

Calls Qwen's LoRA-based multi-angle model 4 times with standard angles:

| View  | Horizontal | Vertical | Zoom |
| ----- | ---------- | -------- | ---- |
| Front | 0°         | 0°       | 5    |
| Right | 90°        | 0°       | 5    |
| Back  | 180°       | 0°       | 5    |
| Left  | 270°       | 0°       | 5    |

All views use the same seed for consistency.

**Advantages:**

- Geometrically consistent camera rotation (LoRA-based, not prompt-based)
- Good for scenes where the camera orbits a location
- Resolution matches input

**Limitations:**

- 4 API calls instead of 1
- Resolution limited by source image quality

## splitSheet Parameters

| Param       | Type   | Default  | Notes                         |
| ----------- | ------ | -------- | ----------------------------- |
| `sheetPath` | string | required | Path to 2x2 sheet image       |
| `outputDir` | string | required | Where to save quadrant images |

Quadrant mapping: top-left = front, top-right = right, bottom-left = back, bottom-right = left.

## generateLayoutTemplate Parameters

| Param        | Type   | Default  | Notes                          |
| ------------ | ------ | -------- | ------------------------------ |
| `outputPath` | string | required | Where to save the template PNG |
| `size`       | number | 1024     | Square size in pixels          |

## Integration

This skill integrates with:

- `asset-bot generate 3d` — primary agent-facing command for multiview-driven 3D generation
- `asset-bot generate image` — common upstream step to create the source concept image
- `@asset-bot/core generateMultiview()` — direct API for custom pipelines
- `@asset-bot/core generate3D()` — consumes multiview outputs for 3D model generation

## References

- [Nano Banana Reference](references/NANO-BANANA-REFERENCE.md)
- [Qwen Multi-Angle Reference](references/QWEN-MULTIANGLE-REFERENCE.md)
- [Consistent Pipeline Reference](references/CONSISTENT-PIPELINE-REFERENCE.md)

## Preview

When the user asks to preview generated assets, use the `/playground` skill to create an interactive HTML viewer. Do not attempt browser automation or raw file opens.
