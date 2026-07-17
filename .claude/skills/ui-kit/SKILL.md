---
name: ui-kit
description: >
  Generate complete UI component kits with panels, buttons, icons, bars, and
  controls. Use when the user wants to create a cohesive set of game UI
  elements, generate a UI texture atlas, or produce styled UI components from
  a mockup. Generates ~57 components in 4 API calls via sheet-based
  generation with pixel-precise slicing and nine-slice metadata.
metadata:
  assetBot:
    commands:
      - asset-bot generate image
      - asset-bot preview candidates
      - asset-bot assets promote
    references:
      - ../../../references/CLI-REFERENCE.md
---

# ui-kit

Hybrid UI kit pipeline: wireframe mockup for style + sheet-based generation for component extraction. Generates all ~57 UI components in just 4 NB calls (one per sheet), then assembles into a texture atlas.

## Architecture

1. **Wireframe** — Static wireframe PNG shows a fixed quest-journal layout. Spatial arrangement never changes; only visual style varies.
2. **Mockup** — Wireframe passed as ref to NB with style prompt. 3 variants generated; user picks one. Becomes style anchor for all sheets.
3. **Sheet Generation** — 4 sheets (512×512 each), each containing labeled rectangles. One NB call per sheet fills all rectangles. Pixel-precise slicing extracts individual components.
4. **Assembly** — All sprites packed into a single atlas with nine-slice metadata.
5. **Preview** — Interactive HTML viewer with nine-slice demos and composition previews.

### Generation Flow

```
Wireframe (static PNG)
    |  NB + style prompt
Mockup (3 variants → user picks one)
    |  used as style ref for all sheets
Sheet Generation (4 sheets, 512×512 each)
    |  renderTemplate → NB → sliceSheet
57 individual component PNGs
    |  packAtlas
Texture atlas + preview HTML
```

### Sheets

| Sheet               | ID                  | Components | Order     |
| ------------------- | ------------------- | ---------- | --------- |
| Panels & Decorative | `panels-decorative` | 12         | 0 (first) |
| Buttons             | `buttons`           | 18         | 1         |
| Icons               | `icons`             | 16         | 1         |
| Bars & Controls     | `bars-controls`     | 11         | 1         |

`panels-decorative` generates first (order 0) because panels with `usesAsRef: true` become style anchors for sheets 2–4. Sheets 2–4 (order 1) run in parallel.

## Commands

### reset — Remove all UI kit data

| Param       | Type   | Required |
| ----------- | ------ | -------- |
| `projectId` | string | Yes      |

### init — Create default spec + state

| Param               | Type     | Required |
| ------------------- | -------- | -------- |
| `projectId`         | string   | Yes      |
| `stylePrompt`       | string   | No       |
| `mockupPath`        | string   | No       |
| `excludeBatches`    | string[] | No       |
| `excludeComponents` | string[] | No       |

### mockup — Generate or register style anchor

In generate mode, produces 3 variants. Use `mockupSelect` to switch after reviewing.

| Param         | Type                    | Required |
| ------------- | ----------------------- | -------- |
| `projectId`   | string                  | Yes      |
| `mode`        | `generate` / `register` | Yes      |
| `stylePrompt` | string                  | No       |
| `imagePath`   | string                  | No       |
| `variants`    | number                  | No       |

### mockupSelect — Pick variant

| Param       | Type   | Required |
| ----------- | ------ | -------- |
| `projectId` | string | Yes      |
| `variant`   | number | Yes      |

### plan — Preview generation plan (no API calls)

| Param       | Type   | Required |
| ----------- | ------ | -------- |
| `projectId` | string | Yes      |
| `sheetId`   | string | No       |

### generate — Generate all components in a sheet

| Param       | Type    | Required |
| ----------- | ------- | -------- |
| `projectId` | string  | Yes      |
| `sheetId`   | string  | Yes      |
| `seed`      | number  | No       |
| `force`     | boolean | No       |

### generateOne — Re-generate a single component

Re-runs its entire sheet with `force=true`.

### assemble — Pack into texture atlas

| Param                    | Type    | Required |
| ------------------------ | ------- | -------- |
| `projectId`              | string  | Yes      |
| `padding`                | number  | No       |
| `maxWidth` / `maxHeight` | number  | No       |
| `powerOfTwo`             | boolean | No       |

### preview — Interactive HTML preview

### status — Generation progress per sheet

### validate — Check extracted components for issues

### full — End-to-end pipeline

| Param         | Type   | Required |
| ------------- | ------ | -------- |
| `projectId`   | string | Yes      |
| `stylePrompt` | string | Yes      |

Steps: init → mockup → generate panels-decorative → generate remaining (parallel) → assemble → preview.

## Default Components (57 total)

| Batch      | Count | Components                                                                                                      |
| ---------- | ----- | --------------------------------------------------------------------------------------------------------------- |
| panels     | 7     | panel-bg-a/b, panel-inset-a/b, panel-header, tooltip-bg, dialog-overlay                                         |
| buttons    | 18    | 5 types × 3 states + tabs + close                                                                               |
| bars       | 3     | bar-track, bar-fill, bar-overlay                                                                                |
| icons      | 16    | sword, shield, potion, coin, gem, heart, star, scroll, key, lightning, fire, skull, clock, compass, chest, flag |
| decorative | 5     | divider-h, divider-v, corner-ornament, badge, ribbon                                                            |
| controls   | 8     | toggle on/off, checkbox on/off, scroll track/thumb, slider track/thumb                                          |

## Agent Usage Rules (CRITICAL)

1. **Full paths** — Always show the user the full absolute path to every generated image.
2. **Mockup variant review** — After generating mockup variants, show ALL variant paths. Let user review and pick. Use `mockupSelect` to promote. Do NOT proceed to generation until user approves a mockup.
3. **Sheet order** — Always generate `panels-decorative` first (style anchors). Then generate remaining sheets in parallel.
4. **Visual review after each sheet (MANDATORY)** — After generating each sheet, visually inspect before proceeding. Check for:
   - **Text artifacts** — template labels rendered as visible text
   - **Icon backgrounds** — icons should be standalone objects, NOT framed by panels
   - **Style consistency** — components should match mockup colors, borders, materials
     If any sheet has issues, re-generate with `force: true` (different seed) before continuing. Do NOT proceed to assembly until all sheets pass review.

## Ref Priority (Per Sheet)

1. Mockup image (style anchor)
2. Template PNG (layout guide)
3. `usesAsRef` panel refs (from sheet 1, if not sheet 1 itself)
4. Project style refs from `refs/style/`
5. Cap at 14 total

## File Layout

```
projects/<projectId>/
  ui-kit/
    ui-kit-spec.json     # Component spec
    ui-kit-state.json    # Generation progress
    mockup/
      mockup.png         # Active variant
      mockup_1.png       # Variant 1–3
    sheets/
      panels-decorative_template.png
      panels-decorative_generated.png
      panels-decorative_slices/
      ...
    extracted/
      panels/ buttons/ ...
  exports/
    ui-kit/
      atlas.png / atlas.json / ui-components.json / preview.html
```

## Typical Workflow

```
1. init — set style
2. mockup — generate 3 variants, SHOW ALL to user
3. mockupSelect — user picks variant
4. generate panels-decorative — style anchors first
5. generate buttons, icons, bars-controls — in PARALLEL
6. REVIEW each sheet — re-generate if issues
7. assemble — pack atlas
8. preview — interactive HTML
```

## Integration

- `api-google-genai` — Nano Banana Pro image generation
- `util-render-template` — Template rendering (SliceMap → labeled PNG)
- `util-slice-sheet` — Sheet slicing (generated PNG → individual components)
- `util-pack-atlas` — Atlas packing
- `manage-assets` — Asset record CRUD
