---
name: pipeline
description: >
  End-to-end asset pipeline orchestration. Use when the user wants to set up
  a new game project with asset-bot, populate it with assets from scratch, or
  run a full generation workflow. Guides the complete flow from empty project
  to exported game-ready assets. Coordinates all other skills in dependency
  order.
metadata:
  assetBot:
    commands:
      - asset-bot init
      - asset-bot status
      - asset-bot types register
      - asset-bot types list
      - asset-bot templates create
      - asset-bot generate bootstrap
      - asset-bot generate image
      - asset-bot generate pixel-art
      - asset-bot generate 3d
      - asset-bot generate audio
      - asset-bot generate from-template
      - asset-bot assets list
      - asset-bot assets promote
      - asset-bot export
    references:
      - ../../../references/CLI-REFERENCE.md
      - references/CONSISTENT-PIPELINE-REFERENCE.md
---

# pipeline

End-to-end orchestration for populating a game project with consistent, production-ready assets. This skill coordinates all other skills in dependency order.

## Overview

```
1. Analyze game → identify visual elements
2. Register asset types → define generation rules
3. Bootstrap style ref → establish visual identity
4. Generate in dependency order → core art → derived art → UI → marketing
5. Review candidates → user picks winners
6. Promote → approved assets
7. Export → copy to game project
```

## Phase 1: Project Setup

### Initialize

If `.asset-bot/` doesn't exist yet:

```bash
asset-bot init
```

This creates the project structure, skills, and reference docs.

### Analyze the Game

Read the game codebase to identify:

- **Visual element types** — characters, props, backgrounds, UI elements, icons, effects
- **Dimensions** — viewport size, sprite dimensions, tile sizes
- **Art style** — pixel art vs HD vs 3D
- **Quantity** — how many of each type
- **Dependencies** — which assets reference others (e.g., character sprites derive from concept art)

### Check Existing State

```bash
asset-bot status --json
asset-bot types list --json
asset-bot assets list --json
```

## Phase 2: Register Asset Types

For each visual element type, create a JSON definition and register it:

```bash
echo '{
  "typeId": "character-sprite",
  "name": "Character Sprite",
  "description": "Side-view character sprites at 128x128",
  "providerId": "nano-banana",
  "outputFormats": ["png"],
  "dimensions": { "width": 128, "height": 128 }
}' > /tmp/character-sprite.json

asset-bot types register --from-json /tmp/character-sprite.json --json
```

### Common API Choices

| Art Style    | API               | Notes                       |
| ------------ | ----------------- | --------------------------- |
| HD / painted | `nano-banana`     | Best quality, up to 14 refs |
| Pixel art    | `retro-diffusion` | Native pixel art, tilesets  |
| 3D models    | `hunyuan3d`       | Multiview pipeline          |
| Transparent  | `gpt-image`       | Native alpha channel        |

## Phase 3: Establish Style

**Style comes from reference images, not text.** This is the single most important rule.

### If the user has reference art:

```bash
asset-bot assets import --category style --name "Style Reference" --source-path ./ref-art.png --json
```

### If starting from scratch:

```bash
asset-bot generate bootstrap --prompt "Fantasy RPG art style, painterly, warm lighting, detailed brushwork" --category style --count 3 --json
```

Show all candidates to the user. They pick the best one(s) to establish the project's visual identity.

## Phase 4: Generate in Dependency Order

Always generate in this order:

1. **Core art** — Characters, key props, hero elements (these become refs for everything else)
2. **Derived art** — Variations, alternate poses, color variants
3. **Background / environment** — Using core art as style refs
4. **UI elements** — Panels, buttons, icons (see `ui-kit` skill)
5. **Marketing art** — Store screenshots, feature graphics (see `marketing-art` skill)

### Per-Asset Workflow

For each asset:

```bash
# Generate candidates
asset-bot generate image --prompt "Knight in full plate armor, standing pose, front view" --category characters --count 3 --json

# Show candidates to user for review
# User picks the best one

# Promote the winner
asset-bot assets promote --candidate-path <path-from-generation> --category characters --name "Knight" --asset-type-id character-sprite --json
```

### Template-Based Generation

For repetitive assets with consistent parameters, create templates first:

```bash
asset-bot templates create --from-json template.json --json
asset-bot generate from-template --template-id block-texture-nb --prompt "Blue crystal block surface" --json
```

### Negative Prompts Prevent Drift

When generating many assets in the same category, use negative prompts to prevent common issues:

```bash
asset-bot generate image --prompt "Red potion bottle, glass, glowing" --category items --json
```

If results show unwanted patterns (text overlays, multiple objects, wrong perspective), adjust prompts. See `references/CONSISTENT-PIPELINE-REFERENCE.md` for the full prompt construction policy.

## Phase 5: Review and Iterate

After each generation batch:

1. Show ALL candidate paths to the user
2. Let them pick winners
3. Promote winners with `asset-bot assets promote`
4. If nothing is good enough, regenerate with adjusted prompts or different seeds

### Quality Checks

- Style consistency with approved refs
- Correct dimensions for the asset type
- No text artifacts or unwanted elements
- Proper composition (single subject, correct framing)

## Phase 6: Export

Once assets are approved:

```bash
# Preview what would be exported
asset-bot export --dry-run --json

# Export to game project
asset-bot export --json

# Or export specific categories
asset-bot export --category characters items --json
```

## Skill Dependencies

This skill orchestrates the following:

| Skill                    | When to Read                            |
| ------------------------ | --------------------------------------- |
| `generate-image`         | Before any 2D generation                |
| `generate-pixel-art`     | For pixel art games                     |
| `generate-3d`            | For 3D model generation                 |
| `generate-audio`         | For SFX, music, voice                   |
| `generate-from-template` | For template-based batch generation     |
| `manage-assets`          | For CRUD operations on records          |
| `ui-kit`                 | For UI component generation             |
| `marketing-art`          | For store listings and feature graphics |
| `sync-assets`            | For import/export with game projects    |

## Decision Tree

```
User wants to...
  ├─ Set up a new project → Phase 1-2
  ├─ Generate first assets → Phase 3 (bootstrap) then Phase 4
  ├─ Add more assets to existing project → Phase 4 (skip 1-3)
  ├─ Export to game → Phase 6
  └─ Import existing game art → See sync-assets skill
```

## Anti-Patterns

- **Generating without refs** — Always bootstrap first if no style refs exist
- **Skipping review** — Never auto-promote without user approval
- **Wrong dependency order** — Don't generate UI before core art exists as refs
- **Style in text prompts** — When refs exist, text describes ONLY the subject
- **Too many refs** — 4-6 refs is optimal; more dilutes the signal
