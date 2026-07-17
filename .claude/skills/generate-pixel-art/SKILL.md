---
name: generate-pixel-art
description: >
  Generate pixel art sprites, animations, and spritesheets with quality
  guardrails. Use when the user wants to create pixel art characters, props,
  walking cycles, idle animations, sprite transitions, or
  video-to-spritesheet conversions. Enforces dimension rules, input image
  preparation, and prompt structure per Retro Diffusion style families.
metadata:
  assetBot:
    commands:
      - asset-bot generate pixel-art
      - asset-bot preview candidates
    references:
      - ../../../references/CLI-REFERENCE.md
      - references/RETRO-DIFFUSION-REFERENCE.md
---

# generate-pixel-art

Pixel art generation with strict quality guardrails. Wraps `api-retro-diffusion` to prevent inputImage preparation mistakes, prompt issues, and dimension mismatches. Includes animation pipelines: sprite transitions (pose-to-pose via video) and static-to-spritesheet conversion.

**Use this skill instead of calling `api-retro-diffusion` directly for pixel art sprites and animations.**

## CLI Commands

```bash
# Static pixel art generation
asset-bot generate pixel-art --prompt "A knight in full plate armor" --category characters --prompt-style rd_pro__topdown --width 128 --height 128 --json

# Multiple candidates
asset-bot generate pixel-art --prompt "Small green slime monster" --category enemies --prompt-style rd_fast__low_res --width 32 --height 32 --count 4 --json

# With seed for reproducibility
asset-bot generate pixel-art --prompt "Blue potion bottle, glass with cork" --category items --prompt-style rd_pro__topdown --width 96 --height 96 --seed 200001 --json

# Preview generated candidates
asset-bot preview candidates --category characters --asset-id knight-001 --json
```

For exact parameters and flags, run `asset-bot generate pixel-art --help` or see `../../../references/CLI-REFERENCE.md`.

## When to Use

| Task              | This Skill | api-retro-diffusion Directly |
| ----------------- | ---------- | ---------------------------- |
| Character sprites | Yes        | No                           |
| Animations        | Yes        | No                           |
| Props, items      | Yes        | No                           |
| Tilesets          | No         | Yes (unique params)          |
| Image editing     | No         | Yes (`editImage`)            |
| Cost/credits      | No         | Yes                          |

## Hard Rules

### inputImage Rules

1. **MUST fill the target frame** — always use `prepareInputImage()`, never prepare manually
2. **MUST be RGB, no alpha** — `prepareInputImage()` strips alpha (composites onto black; override with `background` param)
3. **Character should occupy 70-90% of the frame**
4. **Resize strategy is automatic** — upscaling: nearest-neighbor; downscaling: Lanczos

### Prompt Rules

1. **Sentence + tags, not keyword soup**
   - Good: `"A knight in full plate armor holding a longsword, dark fantasy, detailed shading"`
   - Bad: `"knight, medieval, armor, sword, pixel, fantasy"`

2. **Animation prompt structure varies by style:**
   - `any_animation`: describe subject AND action — `"wizard casting fire spell, flames from hands"`
   - `walking_and_idle`: character description ONLY — `"knight in plate armor with red cape"`
   - `four_angle_walking`: character description ONLY — `"hooded rogue with twin daggers"`
   - `small_sprites`: character description ONLY — `"small green slime monster"`

3. **No negative prompts** — Retro Diffusion has no negative prompt parameter. Steer with positive language.

4. **Bypass expansion** when your prompt already provides sufficient direction — set `bypassPromptExpansion: true`

### Containment Rules

1. **Control effects through PROMPTING, not image sizing** — never shrink the character to make room for effects
2. **Use containment language:** `"compact effects"`, `"contained within frame"`, `"close-cropped"`
3. **If effects overflow the frame:** adjust the prompt, don't increase canvas size

### Reference Image Rules

| Style Family   | inputImage                         | referenceImages[]    |
| -------------- | ---------------------------------- | -------------------- |
| `animation__*` | Single ref (character consistency) | NOT supported        |
| `rd_pro__*`    | img2img (optional)                 | Up to 9 via findRefs |
| `rd_fast__*`   | img2img (optional)                 | NOT supported        |
| `rd_plus__*`   | img2img (optional)                 | NOT supported        |

### Dimension Rules

**Animation styles (fixed sizes):**

| Style                           | Width | Height         |
| ------------------------------- | ----- | -------------- |
| `animation__four_angle_walking` | 48    | 48             |
| `animation__walking_and_idle`   | 48    | 48             |
| `animation__small_sprites`      | 32    | 32             |
| `animation__8_dir_rotation`     | 80    | 80             |
| `animation__any_animation`      | 64    | 64             |
| `animation__vfx`                | 24-96 | 24-96 (square) |

**Static styles (ranges):**

| Family                                                                              | Min | Max |
| ----------------------------------------------------------------------------------- | --- | --- |
| `rd_pro__*`                                                                         | 96  | 256 |
| `rd_fast__*`                                                                        | 64  | 384 |
| `rd_plus__*`                                                                        | 64  | 384 |
| `rd_fast__low_res`, `rd_fast__mc_*`                                                 | 16  | 128 |
| `rd_plus__low_res`, `rd_plus__mc_*`, `rd_plus__topdown_item`, `rd_plus__skill_icon` | 16  | 128 |
| `rd_plus__classic`                                                                  | 32  | 192 |

## Core Functions

| Function                   | Purpose                                        |
| -------------------------- | ---------------------------------------------- |
| `generate(args)`           | Static pixel art with dimension/ref guardrails |
| `generateAnimation(args)`  | Animation spritesheet generation               |
| `prepareInputImage(args)`  | Flatten alpha, resize, center for RD input     |
| `previewSpritesheet(args)` | Standalone spritesheet-to-GIF utility          |
| `validateDimensions(args)` | Check dimensions against style rules           |

## Workflows

### Static Sprite (with refs)

1. `validateDimensions()` — confirm style+size combo
2. `prepareInputImage()` — if using inputImage
3. `generate()` — with projectId/category for auto ref discovery
4. Review candidates

### Animation (from existing sprite)

1. Have an approved sprite as inputImage
2. `generateAnimation()` — auto-sets dimensions, preps inputImage
3. `asset-bot preview candidates` — review generated spritesheet candidates
4. Optional: `previewSpritesheet()` if you specifically need GIF row previews

## Sprite Transition Pipeline

Animate transitions between two poses from a sprite sheet. Extracts cells, generates a transition video, processes frames, and assembles into a spritesheet.

### Prerequisites

- `ffmpeg` installed and in PATH
- `magick` (ImageMagick 7+) installed and in PATH

### Pipeline Steps

```
Sprite Sheet (grid of poses)
  ├─ extractCells ──── Crop start/end cells from grid
  ├─ padCells ──────── Auto-size canvas, composite on colored bg
  ├─ generateVideo ─── Kling 2.6 Pro or WAN FLF2V transition
  ├─ extractFrames ─── ffmpeg frame extraction
  ├─ outlineFrames ─── BiRefNet + outline at native resolution
  ├─ assembleSheet ─── ImageMagick montage → 8-bit PNG
  └─ full ──────────── Run complete pipeline
```

### full Parameters

| Param                   | Type         | Default     | Notes                            |
| ----------------------- | ------------ | ----------- | -------------------------------- |
| `sheetPath`             | string       | required    | Source sprite sheet              |
| `gridRows` / `gridCols` | number       | required    | Grid dimensions                  |
| `startCell` / `endCell` | `{row, col}` | required    | 0-indexed cells                  |
| `outputDir`             | string       | required    | All outputs land here            |
| `canvasColor`           | string       | `"#000000"` | Canvas background                |
| `gravity`               | enum         | `"south"`   | `"south"`, `"center"`, `"north"` |
| `prompt`                | string       | auto        | Motion description               |
| `videoModel`            | enum         | `"kling"`   | `"kling"` or `"wan-flf2v"`       |
| `frameCount`            | number       | 8           | Frames in output                 |
| `outlineWidth`          | number       | 1           | Outline thickness (px)           |
| `skipOutline`           | boolean      | false       | Skip BiRefNet + outline          |

### Video Model Choice

| Model                   | Best for                            |
| ----------------------- | ----------------------------------- |
| `kling` (Kling 2.6 Pro) | General transitions, complex motion |
| `wan-flf2v` (WAN FLF2V) | Controlled A→B transitions          |

## Video-to-Spritesheet Pipeline

Transform static images into animated spritesheets via video generation.

### Prerequisites

- Source image must be **at least 300×300 pixels** (Kling 2.6 Pro minimum). Upscale with `util-upscale` if smaller.

### Pipeline Steps

```
Source Image (static tile/character)
  ├─ generate ─── Video via Kling 2.6 Pro (loop: start=end)
  ├─ extract ──── Frames via ffmpeg at specified FPS
  ├─ removeBg ─── (Optional) Background removal
  ├─ assemble ─── Spritesheet via ImageMagick montage
  └─ full ──────── Complete pipeline
```

### full Parameters

| Param         | Type    | Default                   | Notes                       |
| ------------- | ------- | ------------------------- | --------------------------- |
| `imagePath`   | string  | required                  | Source image                |
| `outputDir`   | string  | required                  | All outputs                 |
| `prompt`      | string  | `"subtle idle animation"` | Motion description          |
| `frameCount`  | number  | 8                         | Output frames               |
| `frameSize`   | number  | 256                       | Output frame size (px)      |
| `durationSec` | 5 / 10  | 5                         | Video duration              |
| `removeBg`    | boolean | false                     | Remove background           |
| `layout`      | enum    | `"horizontal"`            | `"horizontal"` or `"grid"`  |
| `record`      | object  | —                         | Optional record integration |

### Tips

- **Idle animations:** Use prompts like "subtle breathing", "gentle idle movement"
- **Frame count:** 8 for simple idles, 16 for complex animations
- **Frame size:** 256px for high quality, 128px for smaller files

## Cost Estimates

| Operation                          | Cost                                            |
| ---------------------------------- | ----------------------------------------------- |
| Static sprite generation           | Per-credit (RD)                                 |
| Animation generation               | Per-credit (RD)                                 |
| Video-to-spritesheet (full)        | ~$0.10 (no bg removal) / ~$0.24 (8 frames + bg) |
| Sprite transition (full, 8 frames) | ~$0.18                                          |

## Seed Discipline

| Category   | Seed Range    |
| ---------- | ------------- |
| Characters | 100000-199999 |
| Props      | 200000-299999 |
| UI         | 300000-399999 |

## Integration

- `api-retro-diffusion` — Raw pixel art generation
- `api-fal` — Kling 2.6 Pro / WAN FLF2V video generation
- `generate-image` — `findRefs()` for rd_pro\_\_ styles
- `util-add-outline` — BiRefNet segmentation + outline
- `util-remove-bg` — Background removal for frames
- `util-upscale` — Upscale small sources before video generation
