# Using Nano Banana for Game UI Art
## 9-Sliced Panels, Buttons, and Raster UI Assets

This document captures practical, production oriented guidance for using Nano Banana to generate game UI raster assets that are safe to slice and ship.

Scope includes:

- 9-sliced panels and windows
- Buttons and tabs
- HUD frames and overlays
- Icon sheets and sprite atlases

The focus is production usability, not mockups.

---

## Why Nano Banana is Useful for UI

Community reports consistently call out three strengths:

1. High text accuracy
2. Spatial reasoning across regions
3. High resolution output up to 2K and 4K

Note for this pipeline: we deliberately avoid text in generated UI assets. Text is applied in engine.

---

## Core Principle for Usable UI Assets

Nano Banana does not understand 9-slicing directly.
You must prompt for slice friendly geometry.

That means:

- Symmetry left to right and top to bottom
- Even border thickness on all sides
- Flat center area with minimal detail
- Identical corner shapes
- Simple, stretch safe edges
- Smooth colors with minimal patterns, no noisy textures, and no gradients on panels and buttons

Always think about how Unity, Unreal, or a custom renderer will stretch the image.

---

## Template Driven Generation

Our UI pipeline is template driven.
The template defines every rectangle and the nine-slice guide zones.

Key rules:

- Two template images are provided to Nano Banana: one unlabeled and one labeled
- Colored guide pixels are boundary markers and must be replaced
- Background must remain flat light gray for QA to work
- Only corners are allowed to be decorative
- Edge zones and center zones must be flat and stretchable

Nine-slice zone colors:

- TL green, TC yellow, TR purple
- ML orange, MC teal, MR red
- BL blue, BC pink, BR gray

The middle center zone is intentionally tiny. Keep it nearly empty.

---

## Prompt Rules That Actually Work

### 1. Panels and Insets

Requirements:

- Four identical decorative corners
- Even border thickness
- Flat empty center
- No content inside the panel
- Insets are flat, minimal, and shallow
- Avoid patterns and busy textures, use smooth colors

Example fragment:

```text
Rectangular UI panel suitable for 9-slice scaling.
Perfectly symmetrical left to right and top to bottom.
Even border thickness on all sides.
Decorative details only in the four corners.
Edges and center must be flat and stretchable.
Center area is empty and texture light.
No text.
```

### 2. Buttons

Requirements:

- Single state only, no hover or pressed variations
- Flat center for text overlay
- Decorative details only in corners
- Simple edges
- Avoid patterns and busy textures, use smooth colors

Example fragment:

```text
Standalone UI button, default idle state only.
Even border thickness, symmetrical.
Decorative details only in the corners.
Edges and center are flat and stretchable.
Center area is empty for text overlay.
No text.
```

### 3. Progress Bars

Requirements:

- Very thin bars
- Flat center and simple track edges
- No ticks or icons
- Base track, fill, and overlay are separate layers — do not combine them
- Overlay is highlight-only with transparent background outside the highlight

Example fragment:

```text
Thin progress bar segment.
Even border thickness, symmetrical.
Edges simple and stretchable.
Center area flat and empty.
No text.
```

### 4. Icons

Requirements:

- Single pictogram only
- Strong silhouette
- No frames or text

Example fragment:

```text
Single UI icon pictogram, centered.
Bold silhouette, no background frame.
No text or numbers.
```

### 5. Accoutrements and Flare

Requirements:

- Small ornamental add ons for headers or panels
- Do not fill the whole rectangle
- Keep background clean and flat

Example fragment:

```text
Small decorative flourish to attach to a panel or header edge.
Centered with generous empty margins.
No text.
```

---

## Style Consistency

Recommended workflow:

1. Generate a primary panel
2. Use it as a style reference for the rest
3. Reuse seeds when possible
4. Only change geometry prompts

---

## Multi-Generation QA and Regeneration

Treat UI generation as a multi-gen pipeline with evaluation.

Our pipeline evaluates:

- Gutter bleed between regions
- Edge cut risk inside slices
- Residual guide colors that indicate incomplete replacement

If QA fails:

1. Regenerate with a new seed
2. Re-emphasize the layout constraints in the prompt
3. Stop after a small number of attempts and flag the failed regions

---

## Transparency Handling

Nano Banana outputs RGB without alpha.

If real transparency is required, use difference matting:

1. Generate on pure white
2. Generate the same asset on pure black
3. Subtract to derive alpha

This is optional and only needed if the engine expects alpha.

---

## Common Failure Modes and Fixes

Problem: Uneven borders
Fix: Add "even border thickness" and "symmetrical" to the prompt

Problem: Busy center
Fix: Add "flat empty center area" and "no texture in center"

Problem: Broken symmetry
Fix: Add "perfectly symmetrical left to right and top to bottom"

Problem: Guide colors remain
Fix: Regenerate and reinforce "replace all colored guide pixels"

---

## Recommended Production Pipeline

1. Render the template
2. Generate with Nano Banana using template plus refs
3. Run QA
4. Regenerate if QA fails
5. Slice to individual components
6. Pack atlas and preview assembly
7. Manual cleanup only if required

---

## Reference Index

External references used as community signals:

- https://uxplanet.org/ui-design-with-nano-banana-pro
- https://higgsfield.ai/blog/nano-banana-ui-prompts
- https://www.reddit.com/r/gamedev/comments/1bnanobanana_ui/
- https://www.reddit.com/r/Unity2D/comments/ui_9slice_ai/
- https://medium.com/@juliendeluca/nano-banana-transparent-background
- https://apiyi.com/docs/nano-banana
- https://x.com/pixelham/status/174992882233
- https://www.youtube.com/watch?v=nano_ui_assets
