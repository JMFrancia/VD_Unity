---
name: generate-image
description: >
  Generate 2D images with automatic reference handling and style consistency.
  Use when the user wants to create game art, concept art, icons, or any 2D
  visual asset. Handles model selection (Nano Banana Pro or GPT Image), prompt
  construction, reference image discovery, and concept-to-production pipelines
  including background removal, centering, and multi-angle view derivation.
metadata:
  assetBot:
    commands:
      - asset-bot generate image
      - asset-bot generate bootstrap
    references:
      - ../../../references/CLI-REFERENCE.md
      - references/NANO-BANANA-REFERENCE.md
      - references/CONSISTENT-PIPELINE-REFERENCE.md
      - references/ICON-GENERATION-REFERENCE.md
---

# generate-image

High-level 2D image generation with automatic reference handling and style consistency. Dispatches to API skills based on configuration. Includes concept-to-production workflows (background removal, centering, view derivation).

**Do not call API skills directly for image generation.** Use this skill's CLI commands instead.

## CLI Commands

```bash
# Standard generation (with refs discovered automatically)
asset-bot generate image --prompt "Knight in full armor" --category characters --json

# Explicit refs
asset-bot generate image --prompt "Same knight, side view" --category characters --refs style-ref.png concept.png --json

# Control output
asset-bot generate image --prompt "Blue potion bottle" --category items --aspect-ratio 1:1 --count 3 --seed 42 --json

# Use GPT Image for native transparency
asset-bot generate image --prompt "Sword icon on transparent background" --category icons --api gpt-image --json

# Bootstrap when no refs exist
asset-bot generate bootstrap --prompt "Fantasy RPG art style, painterly" --category style --count 3 --json
```

For exact parameters and flags, run `asset-bot generate image --help` or see `../../../references/CLI-REFERENCE.md`.

## Reference Images First (CRITICAL)

**Image refs carry the design. Text prompts only describe what to change.**

When modifying or deriving from existing assets, ALWAYS pass the source as a reference image. The text prompt should be short and describe only the delta. Do NOT re-describe in text what the reference image already shows. Long descriptive text competes with the image signal and produces generic output.

```
# BAD: Re-describing the design in text
--prompt "A military airship with torpedo hull, silver armor plating, orange racing stripes,
         pagoda superstructure, rear propellers..."
--refs sovereign-concept.png

# GOOD: Let the ref carry the design, text only says what changes
--prompt "Same airship from Image 1. Pure side profile view. Simplified shapes, fewer small
         protruding details. White background, flat lighting."
--refs sovereign-concept.png
```

## Model Selection

**Nano Banana Pro (api-google-genai) is the default for all non-pixel-art 2D generation.**

| Use Case                    | Model           | `--api` flag            |
| --------------------------- | --------------- | ----------------------- |
| Standard images             | Nano Banana Pro | `nano-banana` (default) |
| Alt 2D, native transparency | GPT Image       | `gpt-image`             |

**If a user requests a different model:** Confirm they don't want Nano Banana — it's better in almost all cases. Nano Banana supports up to 14 reference images natively. GPT Image is a good alternative when native transparent backgrounds or strong instruction following are needed.

## Prompt Writing (CRITICAL)

**Before writing any prompt, read [NANO-BANANA-REFERENCE.md](references/NANO-BANANA-REFERENCE.md).**

### Style Comes From Refs, Not Text

**When style refs are provided, the text prompt describes ONLY the subject.** Do NOT include style descriptors (aesthetic, medium, rendering technique, lighting mood) in the text — the style reference images carry all of that.

**When bootstrapping (no refs yet),** include style/lighting in the text to establish the look.

### Prompt Elements

| Element         | With Refs            | Bootstrapping (no refs) |
| --------------- | -------------------- | ----------------------- |
| **Subject**     | Yes                  | Yes                     |
| **Action**      | Yes                  | Yes                     |
| **Location**    | Yes                  | Yes                     |
| **Composition** | Yes                  | Yes                     |
| **Lighting**    | NO — refs carry this | Yes                     |
| **Style**       | NO — refs carry this | Yes                     |

### Prompt Template (with refs)

```
[Subject with defining traits] [action/state] [location context].
[Composition: shot type, camera angle, framing].
Single unified composition.
```

### Prompt Template (bootstrapping — no refs)

```
[Subject with defining traits] [action/state] [location context].
[Composition: shot type, camera angle, framing].
[Lighting: source, quality, shadows, color].
[Style: aesthetic, medium]. Single unified composition.
```

### Prompt Checklist

Before generating, verify your prompt has:

- [ ] Natural sentence structure (not comma-separated tags)
- [ ] Subject with visual traits
- [ ] Action or state verb
- [ ] Location/setting context
- [ ] Shot type (close-up, medium, full)
- [ ] Camera angle (eye level, low, high)
- [ ] NO style/lighting/aesthetic text (when refs are provided)
- [ ] "Single unified composition" (anti-panel)

### Anti-Patterns

- **Style text with refs**: "painterly noir style" when refs already show the style — dilutes ref signal
- **Tag soup**: "building, night, neon, noir, 4k" — random results
- **Missing composition**: No shot type = model guesses framing
- **Metaphors**: "emanating power" — use literal visual description

## Icon Generation

**For UI icons that need transparent backgrounds, read [ICON-GENERATION-REFERENCE.md](references/ICON-GENERATION-REFERENCE.md).**

- Use **neutral gray background** (#808080), not black or white
- Object floats in void, no surface/environment
- 1:1 aspect ratio, object fills 70% of frame
- After generation, run through `util-remove-bg`

## How Ref Labeling Works (Internal)

The generation engine labels each reference image by role (character, location, prop, style, source) so the model knows which images are subjects to reproduce vs styles to match. You do not control this directly via CLI flags — it is handled automatically:

- **`asset-bot generate image --refs`** — flat ref paths are labeled as `style`.
- **`asset-bot generate from-template`** — the template's ref-mapper assigns roles based on the template configuration.
- **Auto-discovery** (no `--refs`) — all discovered refs are labeled as `style`.

Priority: template ref-mapper > explicit `--refs` > auto-discovery.

## Ref Discovery & Selection

**Fewer, well-chosen refs > many refs that dilute the signal.**

Per NANO-BANANA-REFERENCE.md: "up to 14 (6 with high fidelity)". Default: 6 refs.

### Selection Priority

1. **Approved assets in the same category** — best style match
2. **Approved assets in related categories** — same project
3. **Project-level style refs** — `refs/style/`
4. **Per-category refs** — `refs/per-type/<category>/`

### Ref Count Guidance

| Ref Count | Quality          | Use Case                              |
| --------- | ---------------- | ------------------------------------- |
| 1-3       | Highest fidelity | Tight matching, risk of overfitting   |
| 4-6       | Optimal          | Balanced style matching (recommended) |
| 7-14      | Lower fidelity   | Model averages/dilutes the signal     |

### Aspect Ratio Inference

When `--aspect-ratio` is not provided, the skill probes existing approved assets in the same category to detect dimensions and maps to the closest standard ratio. If no images found, defaults to `1:1`.

## Bootstrapping (No Refs Exist)

When no refs are found:

1. STOP and ask user for a reference image or style description
2. Use `asset-bot generate bootstrap` to generate candidates from prompt (+ optional reference image)
3. User approves some as refs
4. Standard generation can proceed

## Concept-to-Production Pipeline

Transform concept art into production-ready 2D assets through pixel-preserving transformations.

### Pipeline Steps

```
Concept Art (approved record)
  ├─ analyze ────── Dimensions, format, subject bounds (local, no API)
  ├─ prepare ────── Background removal → center/pad → save as new record
  ├─ deriveViews ── Multi-angle rotation (4 views)
  ├─ batch ──────── Run operations on all concepts in a category
  └─ plan ───────── Dry run: list what batch would do
```

These pipeline steps use core functions directly. See the `generate-multiview` skill for multi-angle view derivation.

## Integration

- `generate-multiview` — Multi-angle view derivation
- `manage-assets` — Record creation, linking, ref discovery
- `references/NANO-BANANA-REFERENCE.md` — Prompt rules for Nano Banana Pro
- `references/CONSISTENT-PIPELINE-REFERENCE.md` — Cross-cutting generation policy
