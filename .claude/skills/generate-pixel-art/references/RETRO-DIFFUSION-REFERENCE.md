# Retro Diffusion Prompting Reference

Quick reference for prompting Retro Diffusion effectively. Covers sprites, animations, tilesets, and image editing.

## Core Principle

**Write a sentence, then add tags. NOT keyword soup.**

Retro Diffusion uses prompt expansion to enrich your input. Give it a clear concept sentence, then append style/mood tags. The model works by concept closeness — the closer your description matches a real visual concept, the better the output.

```
# BAD: Keyword soup
knight, medieval, armor, sword, pixel, fantasy, dark, 4k

# GOOD: Sentence + tags
A knight in full plate armor holding a longsword, dark fantasy, detailed shading
```

There is **no negative prompt parameter**. Steer results with positive language instead of trying to exclude things.

```
# Can't do this (no parameter exists):
negativePrompt: "blurry, modern, realistic"

# Do this instead — describe what you WANT:
"crisp outlines, medieval style, pixel art aesthetic"
```

## Prompt Structure

**[Subject sentence]. [Style tags], [mood tags]**

Include descriptive style tags after your subject sentence for clarity:

```
A goblin merchant behind a wooden stall, hand-painted fantasy,
warm palette, soft rim lighting, detailed shading
```

### When to Bypass Prompt Expansion

Set `bypassPromptExpansion: true` when:

- You need exact control over the output (e.g., specific tile textures)
- Prompt expansion is adding unwanted detail or changing your intent
- Your prompt already covers style direction sufficiently

Leave it off (default) for most generation — expansion generally improves results.

## Animation Workflows

Animations have **fixed output sizes** per style. The `width`/`height` you pass must match the required size.

### `animation__any_animation` (64x64)

The most flexible animation style. **You must describe both the subject AND the action.**

```
# GOOD: Subject + action
"a wizard casting a fireball spell, magical particles"

# BAD: Subject only (animation will be generic/random)
"a wizard"
```

Use a 64x64 `inputImage` for strong subject adherence. Without it, the model interprets your prompt freely.

### `animation__walking_and_idle` (48x48)

Generates walking + idle cycles. **Describe the character only** — the animation type is predetermined.

```
# GOOD: Character description
"armored knight with blue cape and shield"

# BAD: Describing the action (redundant, may confuse)
"armored knight walking forward step by step"
```

### `animation__four_angle_walking` (48x48)

4-direction walking cycles. Same rule — **describe the character, not the motion**.

```
"red-robed mage with wooden staff"
```

### `animation__small_sprites` (32x32)

Tiny sprites with walking, arm, looking, surprised, and laying animations. Keep prompts simple due to low resolution.

```
"green slime creature"
```

### `animation__8_dir_rotation` (80x80)

Full 8-direction rotation of a subject. Describe the subject from a neutral angle.

```
"wooden barrel with iron bands"
```

### `animation__vfx` (24-96px square)

Visual effects: fire, explosions, lightning, magic. **Describe the effect, not a character.**

```
"blue lightning bolt strike, bright flash"
```

### Animation Anti-Patterns

| Mistake                                   | Why It Fails                           | Fix                                                      |
| ----------------------------------------- | -------------------------------------- | -------------------------------------------------------- |
| No action in `any_animation` prompt       | Animation has no clear motion          | Add explicit action: "casting a spell", "swinging sword" |
| Describing motion for `walking_and_idle`  | Conflicts with predetermined animation | Describe only the character's appearance                 |
| No `inputImage` for character consistency | Each generation looks different        | Provide 64x64 RGB reference image                        |
| Wrong resolution                          | API error or distorted output          | Use exact required size per style                        |
| Keyword soup prompt                       | Incoherent result                      | Write a sentence, then add tags                          |

## Tileset Workflows

### Standard Tileset (`rd_tile__tileset`, 16-32px)

Single prompt describes the tile texture:

```
"grey cobblestone path with moss between cracks"
```

### Advanced Tileset (`rd_tile__tileset_advanced`, 16-32px)

Dual-prompt system for wang-style tilesets:

- `prompt` = inside/primary texture
- `extraPrompt` = outside/surrounding texture

```
prompt: "shallow river water with visible pebbles"
extraPrompt: "sandy riverbank with small rocks"
```

Optionally provide `inputImage` (inside ref) and `extraInputImage` (outside ref) for stronger adherence.

### Other Tile Styles

| Style                     | Size     | Prompt Focus                            |
| ------------------------- | -------- | --------------------------------------- |
| `rd_tile__single_tile`    | 16-64px  | Detailed texture description            |
| `rd_tile__tile_variation` | 16-128px | Modification of `inputImage` (required) |
| `rd_tile__tile_object`    | 16-96px  | Small placeable asset description       |
| `rd_tile__scene_object`   | 64-384px | Large environmental object              |

## Reference Images

### inputImage (img2img)

Guides generation toward a reference. Critical for consistency across assets.

- **Format**: RGB, no alpha channel (strip transparency before sending)
- **Strength**: 0.5-0.8 recommended (0 = ignore image, 1 = copy exactly)
- **Animation use**: 64x64 input for `any_animation` gives near-perfect subject adherence
- **Tileset use**: Provides texture reference for tile generation

### Reference Images (rd_pro\_\_ styles)

Up to 9 reference images for style-consistent generation. Only available with `rd_pro__` prompt styles.

- **Format**: RGB, base64-encoded, no alpha
- **Use case**: Maintaining visual consistency across a set of assets
- **Best with**: `rd_pro__default`, `rd_pro__edit`, `rd_pro__spritesheet`

### Palette Control

Use `inputPalette` to enforce a specific color palette:

- Provide a small image containing your target colors
- Set `returnPrePalette: true` to also get the unpalettized version for comparison

## Technical Limits

| Constraint          | Value                                                         |
| ------------------- | ------------------------------------------------------------- |
| Max generation size | 256x256 (most styles), 384x384 (some rd_fast), 512x512 (rare) |
| Animation sizes     | Fixed per style (32x32, 48x48, 64x64, 80x80, 24-96px)         |
| Reference images    | Up to 9 (rd_pro\_\_ styles only)                              |
| Edit image size     | 16x16 to 256x256                                              |
| Prompt expansion    | On by default; bypass with `bypassPromptExpansion: true`      |
| Negative prompts    | Not supported — steer with positive descriptions              |
| CFG sensitivity     | Higher values increase saturation/contrast; keep moderate     |
| Animation output    | 1 image per request (always)                                  |
| Cost                | Varies by style tier: rd_fast < rd_plus < rd_pro < animation  |

## Anti-Patterns

| Pattern                           | Problem                                 | Fix                                                                       |
| --------------------------------- | --------------------------------------- | ------------------------------------------------------------------------- |
| Keyword soup                      | "knight, sword, armor, dark, pixel, 4k" | Write a sentence: "A knight in dark armor holding a sword"                |
| Metaphors/figurative language     | "warrior channeling inner strength"     | Literal description: "warrior with raised sword, glowing runes on blade"  |
| Missing action in `any_animation` | Static or random animation              | Describe the action: "swinging a pickaxe downward"                        |
| No `inputImage` for consistency   | Each asset looks different              | Provide RGB reference image                                               |
| No style refs                     | Assets don't match project style        | Use `inputImage` and `referenceImages` (rd_pro\_\_) for style consistency |
| Trying to use negative prompts    | No such parameter exists                | Rephrase as positive: "crisp" instead of "not blurry"                     |
| Wrong resolution for animation    | Error or distortion                     | Check required size per animation style                                   |
| Over-prompting at low resolution  | Details lost at 32x32                   | Keep prompts simple for small sprites                                     |

## Sources

- [Retro Diffusion API Examples (GitHub)](https://github.com/Retro-Diffusion/api-examples)
- [Retro Diffusion API Docs (GitBook)](https://astropulse.gitbook.io/retro-diffusion)
- [Retro Diffusion](https://retrodiffusion.ai)
