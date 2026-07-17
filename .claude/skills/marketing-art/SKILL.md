---
name: marketing-art
description: >
  Generate marketing art, store listings, and feature mockups for game
  projects. Use when the user wants to create store banners, wallpapers,
  logos, catalog thumbnails, store screenshots, feature graphics, or
  multi-panel feature mockups. Handles per-image prompts with variable
  dimensions, cross-image consistency via shared style refs, and logo
  compositing.
metadata:
  assetBot:
    commands:
      - asset-bot generate image
      - asset-bot preview candidates
      - asset-bot assets promote
    references:
      - ../../../references/CLI-REFERENCE.md
      - references/MOCKUP-PROMPT-REFERENCE.md
---

# marketing-art

Generate marketing art and feature mockups for game projects. Handles store banners, wallpapers, logos, store listings, and multi-panel feature mockups. Takes per-image prompts with variable dimensions, generates via Nano Banana Pro with shared style refs and cross-image consistency refs, then resizes to exact target pixels.

## Presets

| Type                    | Width | Height | Candidates | Description                         |
| ----------------------- | ----- | ------ | ---------- | ----------------------------------- |
| `tall-banner`           | 1568  | 2720   | 3          | Scene base art for store listings   |
| `logo`                  | 1240  | 1240   | 3          | Game title logo on solid background |
| `wide-wallpaper`        | 3224  | 1536   | 3          | Cinematic hero image                |
| `catalog`               | 360   | 360    | 3          | Platform catalog thumbnail          |
| `store-screenshot`      | 1080  | 1920   | 2          | Screenshot with value prop headline |
| `store-feature-graphic` | 1024  | 500    | 2          | Google Play feature graphic         |

A default campaign: 6 tall-banners + 1 logo + 1 wide-wallpaper + 1 catalog = 27 candidates.

Each preset includes `promptGuidance` — creative direction for the agent to internalize when writing prompts. This guidance is NOT auto-injected into the API prompt.

## Writing Prompts That Convert

Marketing art has one job: make someone tap "Install" in under 2 seconds.

### The gameContext Paragraph

Every campaign needs a `gameContext` string — a 2-3 sentence game summary injected into every prompt.

**Write it as a pitch, not a description:**

- Bad: "An idle game with businesses and contractors"
- Good: "A criminal empire idle game. Dark noir city, neon-lit streets, tarot-themed factions."

### Scene Prompt Rules

YOU write the full prompt for each image. The pipeline only auto-injects the anti-text guard and ref labels.

1. **One clear focal point.** A single massive airship filling the frame > a sprawling battle with 20 elements.
2. **Dramatic, not descriptive.** "Bird's-eye view of a dark city grid, single golden building glowing" > "City at night with buildings."
3. **Literal language only.** No metaphors — AI interprets text literally.
4. **Specify lighting explicitly.** "Dramatic red rim light from the left, cool teal fill from the right, deep shadows."
5. **Specify camera/viewpoint.** "Low angle looking up" (heroic), "Bird's-eye view" (scope), "Eye-level close-up" (detail).
6. **Use depth layers.** "Rain droplets in foreground, character mid-frame, city skyline behind."
7. **Name materials and surfaces.** "Wet asphalt reflecting neon" > "dark street."

### Per-Type Prompt Strategy

**tall-banner:** Think movie poster without title. Strong vertical composition. Leave negative space (top 20% or bottom 30%) for text compositing. Each of 6 banners shows a DIFFERENT aspect.

**logo:** Game title IS the image. Bold lettering, centered, dominant. Solid flat background. One iconic visual element. Must read at any size down to 64px.

**wide-wallpaper:** Cinematic widescreen. Leave breathing room for logo compositing. Panoramic scope with layered depth.

**catalog:** Bold, simple, high-contrast subject. Must pop as a tiny thumbnail in a grid.

### Common Prompt Mistakes

| Mistake                       | Fix                                                                    |
| ----------------------------- | ---------------------------------------------------------------------- |
| "Beautiful fantasy landscape" | "Floating stone fortress in orange sunset, airships docked"            |
| "Dark and moody atmosphere"   | "Deep shadows, single streetlight cone, wet pavement, neon reflection" |
| "Epic battle scene"           | "Two airships nose-to-nose exchanging cannon fire"                     |

## Commands

### plan — Dry run

Discovers refs, maps dimensions, surfaces mismatches.

| Param          | Type                 | Default  |
| -------------- | -------------------- | -------- |
| `projectId`    | string               | required |
| `campaignName` | string               | required |
| `images`       | MarketingImageSpec[] | optional |

### generateImage — Single image

| Param               | Type               | Default  |
| ------------------- | ------------------ | -------- |
| `projectId`         | string             | required |
| `campaignName`      | string             | required |
| `image`             | MarketingImageSpec | required |
| `gameContext`       | string             | optional |
| `usePriorImageRefs` | boolean            | `true`   |

### generateSet — Batch all images

Generates all images in parallel. Style refs are the primary consistency mechanism.

### reroll — Re-generate one image

Uses only **approved** siblings as cross-refs (stricter than initial generation). Accepts `additionalRefs` to preserve content refs.

### compositeImage — Overlay logo onto base

| Param              | Type    | Default           |
| ------------------ | ------- | ----------------- |
| `baseImageName`    | string  | required          |
| `overlayImageName` | string  | required          |
| `position`         | enum    | `"bottom-center"` |
| `scale`            | number  | `0.3`             |
| `removeBg`         | boolean | `false`           |

Positions: `top-left`, `top-center`, `top-right`, `center-left`, `center`, `center-right`, `bottom-left`, `bottom-center`, `bottom-right`.

### generateStoreListing — Store screenshots + feature graphic

| Param            | Type                  | Default      |
| ---------------- | --------------------- | ------------ |
| `projectId`      | string                | required     |
| `campaignName`   | string                | required     |
| `screenshots`    | StoreScreenshotSpec[] | required     |
| `featureGraphic` | MarketingImageSpec    | optional     |
| `orientation`    | string                | `"portrait"` |

StoreScreenshotSpec: `valueProp` (required, all-caps headline) + `prompt` (required, distinct scene).

## MarketingImageSpec

```typescript
{
  name: string;
  prompt: string;
  type?: 'tall-banner' | 'logo' | 'wide-wallpaper' | 'catalog';
  width?: number;
  height?: number;
  numCandidates?: number;
  seed?: number;
  additionalRefs?: Array<{ path: string; label: string }>;
}
```

## Ref Control

Three layers of refs feed into each image:

1. **Style refs** (auto-discovered from `refs/style/`) — project-wide consistency
2. **Cross-image refs** (prior campaign images) — campaign consistency
3. **Additional refs** (per-image) — specific subjects

Labels matter — they appear in the prompt and tell the model HOW to use each ref: "Nine Iron character portrait — match face and outfit exactly" not "character ref".

## Dimension Mapping

1. **Closest aspect ratio** — from `1:1`, `2:3`, `3:2`, `3:4`, `4:3`, `4:5`, `5:4`, `9:16`, `16:9`, `21:9`
2. **Smallest overcover** — pick smallest tier where output >= target in both dimensions
3. **Resize** — `sharp` resizes to exact target pixels using `fit: 'cover'`

## Feature Mockup Pipeline

Generate a set of related 9:16 mockup panels for mobile game features.

### Workflow

1. User provides feature spec
2. Agent writes 4 panel prompts per MOCKUP-PROMPT-REFERENCE.md
3. Agent shows prompts for review
4. `plan()` — validate refs
5. `generateSet()` — generate panels
6. User reviews, selects candidates
7. `reroll()` if needed

### Commands

#### generatePanel — Single panel

| Param               | Type    | Default  |
| ------------------- | ------- | -------- |
| `projectId`         | string  | required |
| `featureName`       | string  | required |
| `panelIndex`        | number  | required |
| `panelCount`        | number  | `4`      |
| `prompt`            | string  | required |
| `usePriorPanelRefs` | boolean | `true`   |
| `numVariants`       | number  | `2`      |

#### generateSet — All panels

Generates sequentially. Each panel after the first uses prior panel candidates as cross-refs.

#### reroll — Re-generate one panel

Uses only **approved** sibling panels as cross-refs (stricter).

### Cross-Panel Ref Strategy

- Panel 1: style refs only
- Panel 2: style refs + panel 1 candidate
- Panel N: style refs + panels 1..(N-1) candidates

Style ref budget: `maxStyleRefs = 6 - priorPanelCount`, floored at 2.

### Prompt Writing

See [MOCKUP-PROMPT-REFERENCE.md](references/MOCKUP-PROMPT-REFERENCE.md).

Key points:

- Each panel follows Nano Banana's 6-element format
- Describe UI text literally ("a button labeled HARVEST")
- Describe animations as frozen moments
- Avoid triptych trigger terms

## Store Listing Workflow

1. Ask for GDD/feature list. Derive `gameContext`.
2. **Value props (REQUIRED):** Propose 3-4 distinct value props — one per screenshot. Present for approval. Do NOT generate until approved.
3. `plan` — review available content. Suggest content refs.
4. Write screenshot prompts, show for review.
5. `generateStoreListing` — sequential screenshots + feature graphic.
6. User reviews, selects candidates. `reroll` if needed.

**Value prop rules:** distinct per screenshot, short/punchy/all-caps, ordered hero-first, answers "why download this?".

## gameContext Persistence

`gameContext` is stored in `project.json` and shared across all marketing generation. Auto-saves on first use, auto-loads on subsequent calls.

## Integration

- `api-google-genai` — Nano Banana Pro generation
- `generate-image` — `findRefs` for style discovery
- `manage-assets` — Record CRUD, candidate workflow
- `util-remove-bg` — Background removal for compositing
