# Qwen Multi-Angle Reference

Technical reference for camera rotation via Qwen's LoRA-based multi-angle editing model.

## Model

| Field     | Value                                           |
| --------- | ----------------------------------------------- |
| Endpoint  | `fal-ai/qwen-image-edit-2511-multiple-angles`   |
| Provider  | fal.ai                                          |
| Technique | LoRA-based camera control on Qwen image editing |
| Pricing   | ~$0.035/megapixel (~$0.15 for 4 standard views) |

## How It Works

The model applies a trained LoRA to rotate the camera viewpoint around the subject while preserving identity, style, and details. Unlike prompt-based rotation (which changes the subject), this produces geometrically consistent views suitable for 3D reconstruction.

## Angle System

### Horizontal Angle (0-360°)

Controls azimuthal rotation around the subject.

| Angle | View             |
| ----- | ---------------- |
| 0°    | Front (original) |
| 45°   | Front-right      |
| 90°   | Right profile    |
| 135°  | Back-right       |
| 180°  | Back             |
| 225°  | Back-left        |
| 270°  | Left profile     |
| 315°  | Front-left       |

### Vertical Angle (-30° to 90°)

Controls elevation of the camera.

| Angle | View                         |
| ----- | ---------------------------- |
| -30°  | Below eye level (looking up) |
| 0°    | Eye level (default)          |
| 30°   | Slight elevated              |
| 60°   | High angle                   |
| 90°   | Top-down / bird's eye        |

### Zoom (0-10)

Controls camera distance from the subject.

| Value | Effect              |
| ----- | ------------------- |
| 0-2   | Close-up            |
| 3-4   | Closer than default |
| 5     | Default distance    |
| 6-7   | Slightly farther    |
| 8-10  | Far / full-body     |

## Standard Multiview Preset

The 4-angle set used by `generate-3d` for 3D conversion:

```
Front:  horizontal=0,   vertical=0, zoom=5
Right:  horizontal=90,  vertical=0, zoom=5
Back:   horizontal=180, vertical=0, zoom=5
Left:   horizontal=270, vertical=0, zoom=5
```

All views use the same seed for consistency across the set.

## Parameters

| Param                 | Type     | Default     | Notes                                       |
| --------------------- | -------- | ----------- | ------------------------------------------- |
| `image_urls`          | string[] | required    | Array with one data URL of the source image |
| `horizontal_angle`    | number   | 0           | Azimuthal rotation (0-360)                  |
| `vertical_angle`      | number   | 0           | Elevation (-30 to 90)                       |
| `zoom`                | number   | 5           | Camera distance (0-10)                      |
| `lora_scale`          | number   | API default | LoRA strength for camera control            |
| `guidance_scale`      | number   | API default | CFG guidance scale                          |
| `num_inference_steps` | number   | API default | Inference steps                             |
| `negative_prompt`     | string   | —           | What to avoid                               |
| `seed`                | number   | random      | For reproducibility                         |

## Best Practices

1. **Clean backgrounds** — Single-color or simple backgrounds produce the best rotations. Complex scenes may distort.
2. **Single subject** — One character or object per image. Multiple subjects confuse the rotation model.
3. **Same seed across views** — Always use the same seed for all views in a multiview set to maintain consistency.
4. **Eye-level vertical** — Keep `vertical_angle=0` for standard multiview sets. Elevated angles are useful for visualization but not for 3D conversion.
5. **Consistent zoom** — Use the same zoom value across all views in a set.
6. **Front-facing source** — Start with a front-facing image for best results. The model works by rotating from the source view.

## Anti-Patterns

- **Busy backgrounds** — Complex environments get distorted during rotation. Use clean/solid backgrounds.
- **Multiple subjects** — The model can't distinguish which subject to rotate. Isolate the subject first.
- **Inconsistent seeds** — Different seeds across views produce inconsistent style/details.
- **Extreme angles** — Angles beyond the range (e.g., vertical > 90°) produce artifacts.
- **Low-resolution source** — Results inherit source quality. Use high-quality source images.

## When to Use Qwen vs NB Turnaround Sheet

| Method                                             | Best For                                         | Technique                                      |
| -------------------------------------------------- | ------------------------------------------------ | ---------------------------------------------- |
| **Qwen** (`method: "qwen"`)                        | **Scenes/locations** (environments, backgrounds) | LoRA camera rotation, 4 API calls              |
| **NB Turnaround Sheet** (`method: "nb-turnsheet"`) | **Subjects** (characters, props, vehicles)       | Composed 2x2 sheet via Nano Banana, 1 API call |

Qwen's LoRA-based approach actually rotates the camera, preserving the exact subject identity. This is essential for 3D reconstruction where all views must depict the same object. It works best for scenes where the camera orbits a location.

For subject-centric multiview (characters, props), use `nb-turnsheet` via the `generate-multiview` skill — it produces a turnaround sheet in a single API call.

## Integration

- **Skill wrapper:** `api-fal` → `generateMultiAngleImage()`
- **Orchestrator:** `generate-multiview` → `generateMultiview({ method: "qwen" })` (calls api-fal 4 times)
- **3D pipeline:** `generate-3d` delegates multiview to `generate-multiview`, then feeds output into Hunyuan3D v3 or Tripo v3
