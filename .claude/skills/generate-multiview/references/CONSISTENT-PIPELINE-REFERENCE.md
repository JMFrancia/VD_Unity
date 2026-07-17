# Consistent AI Asset Pipeline Reference

Production rules for generating visually consistent 2D and 3D game assets at scale. Assets should look like they were made by one artist per project.

## 1. Model Locking

Never mix generation models inside a single visual style.

| Model           | Use For                                                       | Lock Rule                                 |
| --------------- | ------------------------------------------------------------- | ----------------------------------------- |
| Nano Banana Pro | UI, icons, characters, props, concept art, key art            | One style preset per game                 |
| Retro Diffusion | True pixel art only (enforces pixel grid, palette discipline) | If pixel art, ALL assets from RD          |
| Hunyuan3D       | Characters and props (best shape fidelity, multiview support) | Default for all 3D generation             |
| Tripo3D         | Text-to-3D, rigging, animation                                | Use for text-to-3D or when rigging needed |
| Meshy           | Multi-image-to-3D, retexturing                                | Use for retexturing uniformity pass       |

## 2. Prompt Construction

Never free-prompt in production. Use structured templates.

### Prompt Template

**When style refs exist:** Describe ONLY the subject. Style comes from refs.

```
{ASSET_NAME},
{VIEWPOINT}, {MATERIAL},
clean background, no text, no watermark
```

**Bootstrapping only (no refs yet):** Include style/lighting to establish the look.

```
{ASSET_NAME},
{VIEWPOINT}, {LIGHTING}, {MATERIAL}, {STYLE},
clean background, no text, no watermark
```

Style consistency comes from **image references**, not text. The prompt describes WHAT to generate; refs show HOW it should look. Never put style descriptors (aesthetic, medium, lighting mood) in prompts when style refs are provided. See section 8 for ref limits per API.

### Negative Prompts

Always include negatives to prevent drift. Example:

```
photorealistic, anime, hyper-detailed skin,
text, watermark, logo, blurry, distorted,
multiple views, cluttered background
```

### Camera / Viewpoint

Decide once per project and lock:

- `front` | `isometric` | `orthographic` | `3/4`
- Inconsistent camera = inconsistent style

## 3. Seed Discipline

### Golden Seed Workflow

1. Generate 20-50 candidates
2. Pick 3-5 perfect outputs
3. Save their seeds
4. Reuse across all assets of that type

```typescript
const GOLDEN_SEEDS = [483920, 483921, 483922];
const seed = GOLDEN_SEEDS[i % GOLDEN_SEEDS.length] + variantOffset;
```

### Category Seed Ranges

| Category   | Seed Range    |
| ---------- | ------------- |
| Characters | 100000-199999 |
| Props      | 200000-299999 |
| UI         | 300000-399999 |

### Lock Generation Parameters

- CFG: 8-12
- Steps: 30-50
- Resolution: fixed per asset type

## 4. 2D to 3D Pipeline

**Use the `generate-3d` skill** — do not call api-fal or api-tripo directly.

### Recommended Pipeline (Multiview)

1. Generate 2D concept art (Nano Banana or Retro Diffusion)
2. Approve style
3. Generate 4 angle views with **`generate-multiview`** skill (front, right, back, left)
4. Convert multiview to 3D with **Hunyuan3D v3** multiview-to-model
5. Retexture if needed (Meshy retexture with shared style prompt)
6. Normalize scale and export

### Alternative Pipelines

- **Single-image**: Skip multiview, send one image directly to Hunyuan3D v3 or Tripo v3. Faster but lower quality geometry.
- **Text-to-3d**: No source image needed. Use Tripo v3 text-to-model (Hunyuan3D does not support text-to-3D).

### Multi-View Generation

Use the **`generate-multiview`** skill for all multiview generation. Two methods:

| Method                   | Best For                                         | Technique                   | API Calls |
| ------------------------ | ------------------------------------------------ | --------------------------- | --------- |
| `nb-turnsheet` (default) | **Subjects** (characters, props, vehicles)       | NB turnaround sheet → split | 1         |
| `qwen`                   | **Scenes/locations** (environments, backgrounds) | Qwen LoRA camera rotation   | 4         |

**NB Turnaround Sheet** generates a single composed 2x2 image via Nano Banana Pro, then splits it into 4 quadrant views. Best for subjects where style fidelity matters.

**Qwen Multi-Angle** uses a LoRA-based camera rotation model to produce geometrically consistent views by rotating the camera rather than regenerating the subject. Best for scenes where the camera orbits a location.

Standard 4-view set: front (0°), right (90°), back (180°), left (270°) — all at eye level with same seed.

See [generate-multiview SKILL.md](../.claude/skills/generate-multiview/SKILL.md), [Qwen Multi-Angle Reference](QWEN-MULTIANGLE-REFERENCE.md), [Nano Banana Reference](NANO-BANANA-REFERENCE.md).

### Multi-View 3D Endpoints

- **Hunyuan3D v3** (`fal-ai/hunyuan3d-v3/image-to-3d`): front + left/back/right, best quality (default)
- **Tripo3D v3** (direct API, via `api-tripo`): front + left/back/right, up to 2M polys
- **Tripo3D v2.5** (`tripo3d/tripo/v2.5/multiview-to-3d`): front + left/back/right via fal
- **Meshy Multi-Image** (`fal-ai/meshy/v5/multi-image-to-3d`): 1-4 angle images, PBR support

## 5. Retexturing for Uniformity

Use `fal-ai/meshy/v5/retexture` to apply consistent materials across all 3D models:

1. Generate or import meshes (neutral/untextured OK)
2. Retexture with one shared style prompt or reference images
3. Enable PBR for metallic/roughness/normal maps
4. Export consistent materials

This is the 3D equivalent of image refs — one retexture prompt per game for texture uniformity.

## 6. Post-Processing (Mandatory)

### 2D Assets

- Normalize color curves
- Enforce palette (match ref style)
- Resize uniformly (match template dimensions)
- Strip metadata

### 3D Assets

- Normalize scale (consistent world units)
- Check poly counts (match template faceCount targets)
- Fix pivots (center, ground-plane aligned)
- Convert formats consistently (GLB for runtime, FBX for engines)

## 7. Multi-Style Projects

Safe if each style is fully isolated:

- One ref set per game style
- One model per style
- One seed strategy per style
- One export format per style

Think of them as independent style modules that never share generation parameters.

## 8. Reference Image Limits

| API               | Max Refs             | Notes                                    |
| ----------------- | -------------------- | ---------------------------------------- |
| Nano Banana Pro   | 14 (6 high fidelity) | Label each image in prompt               |
| RD_PRO            | 9                    | Strongest for pixel art consistency      |
| Hunyuan3D v3      | 4 views              | front required, left/back/right optional |
| Tripo3D Multiview | 4 views              | front required, left/back/right optional |
| Meshy Multi-Image | 4 images             | Different angles of same object          |
| Qwen Multi-Angle  | 1 input, 4 output    | One source image → 4 rotated views       |
| Tripo3D (single)  | 1                    | Single input image only                  |

## Sources

- [Retro Diffusion](https://retrodiffusion.ai)
- [Tripo3D](https://tripo3d.ai)
- [Hunyuan3D](https://github.com/Tencent/Hunyuan3D)
- [fal.ai](https://fal.ai)
- [Meshy on fal.ai](https://blog.fal.ai/meshy-os/)
