# Tripo3D Prompting Reference

Quick reference for prompting Tripo3D v3 effectively. Covers text-to-3D, image-to-3D, and multi-view workflows.

Based on [Tripo's official prompt engineering guide](https://www.tripo3d.ai/blog/text-to-3d-prompt-engineering).

## Core Principle

**Decompose your prompt into components. Lead with the subject.**

Tripo gives higher weight to information that appears early in the prompt. Front-load the subject and key features, then follow with materials, style, and technical hints.

```
# BAD: Vague, buries the subject
low poly game asset, stylized, a bottle

# GOOD: Subject first, then details
Low-poly potion bottle, faceted geometric shape, glowing purple liquid
inside translucent glass, cork stopper with twine, vibrant flat colors,
hand-painted texture style, mobile game asset
```

## Prompt Structure

**[Subject + Features], [Materials/Textures], [Style/Genre], [Technical Hints]**

### Six Components

1. **Main Subject** — What you're making: "treasure chest", "goblin warrior"
2. **Descriptors** — Size, age, condition: "ancient", "cracked", "ornate"
3. **Materials & Textures** — Surface properties: "rough stone", "polished brass", "weathered leather"
4. **Style & Genre** — Artistic direction: "hand-painted fantasy", "low-poly cartoon", "steampunk"
5. **Technical Specs** — Optimization hints: "game-ready", "mobile asset", "clean topology"
6. **Context & Function** — Intended use: "inventory icon", "environment prop", "playable character"

### Asset Bot Template

When working inside a project, use the structured template from the [Consistent Pipeline Reference](CONSISTENT-PIPELINE-REFERENCE.md):

```
{ASSET_NAME},
{VIEWPOINT}, {MATERIAL},
clean background, no text, no watermark
```

**Note:** Do not add style descriptors to text prompts when style refs are provided. Refs carry the style.

## Negative Prompts

Use `negativePrompt` to explicitly exclude unwanted features.

```
# General exclusions
photorealistic, anime, hyper-detailed, text, watermark, blurry, distorted

# Structural exclusions (good for game assets)
thin sections, complex overhangs, floating elements, disconnected parts

# Character-specific
extra limbs, extra fingers, deformed face
```

Negative prompts are optional but recommended — they prevent style drift and structural problems.

## Domain Templates

### Game Props

```
{subject}, {key features}, {material details},
clean background, no text, no watermark
```

Example:

```
Medieval wooden barrel with iron bands and brass tap,
rough oak planks, oxidized metal rings, hand-painted fantasy style,
soft rim lighting, warm desaturated palette, game-ready prop
```

### Characters & Creatures

```
{style} {subject}, {physical build}, {key characteristics},
{pose}, {clothing/armor/skin}, {quality level}
```

Example:

```
Stylized orc berserker, muscular build, green skin with battle scars,
A-pose, leather shoulder guard and fur loincloth, tusked jaw,
hand-painted fantasy style, game-ready character
```

For characters, consider:

- Use `A-pose` or `T-pose` for rigging-ready output
- Specify clothing/armor materials explicitly
- Keep poses neutral for animation compatibility

### Environment Objects

```
{subject}, {architectural/natural features}, {prominent features},
{primary materials}, clean background, no text, no watermark
```

Example:

```
Ruined stone archway with ivy growth, Gothic fantasy style,
crumbling masonry with exposed bricks, moss and vine details,
grey limestone and dark mortar, clean background, no text, no watermark
```

## Advanced Settings

Combine prompts with Tripo's platform controls for better results:

| Setting                      | When to Use                                   |
| ---------------------------- | --------------------------------------------- |
| `textureQuality: "detailed"` | Final assets; increases texture resolution    |
| `pbr: true`                  | When you need metallic/roughness/normal maps  |
| `faceLimit`                  | Control polygon budget per asset type         |
| `modelSeed` / `textureSeed`  | Lock seeds for reproducibility across a batch |

## Image-to-3D Tips

When using `imageToModel`:

- **Clean input images** — Solid or transparent background, centered subject, good lighting
- **Single object** — One subject per image; multi-object images produce confused geometry
- **Consistent angle** — Front or front 3/4 view works best
- **textureAlignment**: Use `"original_image"` to match input colors closely, `"geometry"` for the model to re-interpret textures
- **orientation**: Use `"align_image"` to match the image's facing direction

## Multi-View Tips

When using `multiviewToModel`:

- **Resolution: 1024x1024+ per view** — Tripo accepts 20-6000px but recommends 1024x1024 for optimal quality. Low-res inputs produce poor geometry and textures. Use `util-upscale` skill (Topaz CGI model) to upscale views before sending.
- **Front view is required** — Always provide `frontImagePath`
- **Consistent style across views** — All images should be from the same generation session or style
- **Matching subject** — The model assumes all views show the same object
- **Clean backgrounds** — Remove busy backgrounds before sending
- More views = better geometry. 4 views (front/back/left/right) gives the best results.

## Anti-Patterns

| Pattern                        | Problem                        | Fix                                                                      |
| ------------------------------ | ------------------------------ | ------------------------------------------------------------------------ |
| "a chest"                      | Too vague, generic output      | Add materials and details: "wooden chest with iron bands and brass lock" |
| "epic legendary sword of doom" | Metaphor, unpredictable        | Literal: "ornate longsword with gold crossguard and ruby pommel"         |
| Missing materials              | Bland, textureless surface     | Specify: "rough stone", "polished brass", "weathered leather"            |
| No style direction             | Inconsistent with other assets | Use image refs for style consistency; describe materials explicitly      |
| Subject buried at end          | Key details get less weight    | Front-load the subject: start with what it IS                            |
| Over-describing pose for props | Confuses the model             | Reserve pose descriptions for characters                                 |
| Busy background in input image | Geometry artifacts             | Use clean/transparent backgrounds                                        |

## Technical Limits

| Constraint        | Value                       |
| ----------------- | --------------------------- |
| Max polygons      | Up to 2M (v3.0)             |
| Texture quality   | Standard or Detailed        |
| PBR maps          | Metallic, roughness, normal |
| Multi-view inputs | 1-4 images (front required) |
| Output format     | GLB                         |
| Polling timeout   | 10 minutes                  |
| Negative prompts  | Supported (text-to-3D only) |

## Sources

- [Text-to-3D Prompt Engineering (Tripo Blog)](https://www.tripo3d.ai/blog/text-to-3d-prompt-engineering)
- [Tripo3D](https://www.tripo3d.ai)
- [Consistent Pipeline Reference](CONSISTENT-PIPELINE-REFERENCE.md)
