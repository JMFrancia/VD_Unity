---
name: generate-from-template
description: >
  Generate assets from predefined templates with automatic reference discovery
  and API dispatch. Use when the user wants to create assets using a specific
  template, or when batch-generating assets that follow a consistent recipe.
  Handles ref scoring, prompt passthrough, and dispatch to the correct API
  based on template configuration. Style consistency comes from image refs,
  not text injection.
metadata:
  assetBot:
    commands:
      - asset-bot generate from-template
    references:
      - ../../../references/CLI-REFERENCE.md
      - references/WAN-REFERENCE.md
---

# generate-from-template

Generation orchestrator — drives the full workflow from template + prompt to candidate assets. Handles ref discovery, API dispatch, and candidate saving. Creates or updates records, then generates actual asset files. Style consistency comes from image refs, not text injection.

## CLI Commands

```bash
# Generate from a template
asset-bot generate from-template --template-id block-texture-nb --prompt "Mossy stone surface" --category textures --json

# With name and candidate count
asset-bot generate from-template --template-id character-portrait --prompt "Elderly wizard with long beard" --category characters --name "Gandalf" --count 4 --json

# With seed for reproducibility
asset-bot generate from-template --template-id item-icon --prompt "Red healing potion" --category items --seed 200001 --json

# With negative prompt
asset-bot generate from-template --template-id scene-bg --prompt "Dark forest clearing" --category backgrounds --negative-prompt "bright, sunny" --json
```

For exact parameters and flags, run `asset-bot generate from-template --help` or see `../../../references/CLI-REFERENCE.md`.

## Workflow

```
prepareGeneration()     ← dry run: show refs, validation, prompt preview
       ↓ (user confirms)
generate()              ← full execution: API call → candidates saved
```

**Always run `prepareGeneration()` first** to review refs, validation, and the constructed prompt before committing to an API call.

## generate(args)

Full generation workflow:

1. Load template (built-in or custom)
2. Load or create record
3. Resolve refLinks → file paths
4. Validate refs against asset type requirements
5. Build prompt (passthrough — style from image refs)
6. Dispatch to appropriate API
7. Save candidates to `candidates/`
8. Update record (status: `reviewing`)

| Param            | Type      | Required | Notes                                     |
| ---------------- | --------- | -------- | ----------------------------------------- |
| `projectId`      | string    | Yes      |                                           |
| `templateId`     | string    | Yes      | Built-in or project custom                |
| `assetId`        | string    | No       | Existing record; creates new if not found |
| `category`       | string    | No       | Required for new record                   |
| `name`           | string    | No       | Display name for new record               |
| `prompt`         | string    | Yes      | Literal language                          |
| `negativePrompt` | string    | No       |                                           |
| `refLinks`       | RefLink[] | No       | Explicit ref overrides                    |
| `numVariants`    | number    | No       | Override template default                 |
| `seed`           | number    | No       |                                           |

Returns: `{ record, candidatePaths, refsUsed, apiResult }`

## prepareGeneration(args)

Dry run — shows what would happen without calling the API.

Returns:

- `template` — resolved template
- `record` — existing record or null
- `refSuggestions` — discovered ref candidates with scores
- `resolvedRefs` — file paths for linked refs
- `mappedRefs` — API-specific parameter mapping
- `validation` — ref requirement pass/fail
- `prompt` — constructed prompt (passthrough)
- `numVariants` — how many candidates will be generated

## Dispatch Table

| providerId        | API Function                       | Primary Ref Role                   |
| ----------------- | ---------------------------------- | ---------------------------------- |
| `retro-diffusion` | `generatePixelArt()`               | style → referenceImages            |
| `nano-banana`     | `generateImageCandidates()`        | style → referenceImages            |
| `gpt-image`       | `generateImageFromRefs()`          | style → referenceImages            |
| `hunyuan3d`       | `generateModel3DHunyuan()`         | source-image → imagePath           |
| `tripo3d`         | `generateModel3DTripo()`           | source-image → imagePath           |
| `tripo3d-direct`  | `imageToModel()` / `textToModel()` | source-image → imagePath           |
| `kling-video`     | `generateVideoKling26Pro()`        | portrait → startImagePath          |
| `wan-video`       | `generateVideoWAN26()`             | portrait → imagePath               |
| `wan-flf2v`       | `generateVideoWANFLF2V()`          | first-frame → firstFramePath       |
| `meshy`           | `generateModel3DMeshyMultiImage()` | front/back/left/right → imagePaths |

## Ref Role → API Parameter Mapping

| Role                                | 2D APIs           | 3D APIs   | Video APIs     |
| ----------------------------------- | ----------------- | --------- | -------------- |
| `style`                             | referenceImages[] | —         | —              |
| `portrait`                          | referenceImages[] | imagePath | startImagePath |
| `source-image`                      | referenceImages[] | imagePath | startImagePath |
| `start-frame`                       | —                 | —         | startImagePath |
| `first-frame`                       | —                 | —         | firstFramePath |
| `end-frame`                         | —                 | —         | endImagePath   |
| `last-frame`                        | —                 | —         | lastFramePath  |
| `front` / `back` / `left` / `right` | —                 | multiview | —              |

## Ref Scoring

| Score | Condition                                         |
| ----- | ------------------------------------------------- |
| 1.0   | Already linked with canonical files (satisfied)   |
| 0.9   | Same category, approved, matching asset type      |
| 0.7   | Same category, approved, any format               |
| 0.6   | Different category, name matches role keyword     |
| 0.4   | Different category, approved, has canonical files |

## Style Consistency

Style consistency is achieved through **image references**, not text injection. Prompts are passed through as-is — they describe WHAT to generate, while refs show HOW it should look.

Negative prompts are built from explicit text + template defaults only.

## Integration

- All Tier 1 API skills — dispatched based on template `providerId`
- `manage-assets` — Record CRUD, ref discovery
- `src/utils/ref-discovery.ts` — `suggestRefs()`
