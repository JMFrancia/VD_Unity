# Icon Generation Reference

Best practices for generating game UI icons with consistent style and clean transparency.

## Recommended: Use the `icon-standard` Template

The built-in `icon-standard` template handles icon-specific concerns automatically:

- **256x256 generation** — forces visual simplicity (icons must read at small sizes)
- **Automatic background removal** — via `removeBg: true`
- **Icon-optimized prompt suffix** — appends bold shapes, silhouette, gray void, centering guidance

Use the `generate_from_template` MCP tool:

```json
{
  "templateId": "icon-standard",
  "category": "icons",
  "prompt": "a health potion, glass bottle with red liquid, cork stopper"
}
```

The template appends its own suffix — your prompt only needs to describe WHAT the icon is, not HOW to compose it. For project-specific needs (different size, extra style keywords), create a custom template that overrides `icon-standard`.

## Pipeline Overview

```
1. Generate at 256x256 on neutral gray background (template handles this)
2. Background removal runs automatically (template handles this)
3. Review for quality/style match
4. Export PNG with transparency
```

## Background Color

**Use neutral gray (#808080 or similar mid-tone), NOT black or white.**

| Background | Problem                                                                       |
| ---------- | ----------------------------------------------------------------------------- |
| Black      | Dark shadows and object edges merge with background, AI can't find boundaries |
| White      | Bright highlights and reflections merge, rim lighting gets washed out         |
| **Gray**   | Creates contrast with both light AND dark areas, clean edge detection         |

### Prompt Pattern

The `icon-standard` template appends this automatically. For ad-hoc generation:

```
...floating in empty neutral gray void, no surface, no reflections, no environment.
```

## Composition

| Element       | Recommendation                                              |
| ------------- | ----------------------------------------------------------- |
| **Fill**      | Object fills 60-70% of frame                                |
| **Position**  | Centered, with padding on all sides                         |
| **Angle**     | Slight diagonal or 3/4 view (more dynamic than straight-on) |
| **Isolation** | No surface, no shadows on ground, floating                  |

## Prompt Template (Ad-Hoc / Bootstrapping Only)

Only needed when generating icons without style refs (bootstrapping). When style refs exist, omit all style language — refs carry the style.

```
A [ITEM DESCRIPTION] floating in empty neutral gray void, no surface, no reflections,
no environment. [MATERIAL AND DETAIL DESCRIPTION]. The [item] is centered and isolated,
[ANGLE/ORIENTATION]. Pure neutral gray negative space surrounds the object completely.
Close-up, [item] fills 70% of frame. Single unified composition.
```

## Size and Format

| Stage                         | Format         | Size                                  |
| ----------------------------- | -------------- | ------------------------------------- |
| Generation (template default) | PNG            | 256x256 (1:1 aspect ratio)            |
| After BG removal              | PNG with alpha | 256x256                               |
| Final export                  | PNG with alpha | As needed for UI (typically 32-128px) |

**Why 256x256?** Icons must read well at 32-64px. Generating at 1024px encourages excessive detail that becomes noise at small sizes. 256px naturally constrains the model toward bold, simple shapes. Projects that need larger source icons can create a custom template overriding the dimensions.

## Background Removal

The `icon-standard` template runs background removal automatically. For manual use:

Background removal is handled automatically by the `icon-standard` template (`removeBg: true`).

Output: `{original_name}_nobg.png` with transparent background.

## Checklist

Before generating an icon:

- [ ] Using `icon-standard` template (or a custom override)
- [ ] Prompt describes the subject only (template handles composition guidance)
- [ ] Lighting setup matches project style (see project's PROJECT.md)

After generation:

- [ ] Object has clean edges (background already removed by template)
- [ ] Lighting matches project style
- [ ] No environmental elements leaked in
- [ ] Reads well when scaled down to target size (32-64px)
