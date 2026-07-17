# fal.ai 3D & Retexture API Reference

Technical reference for 3D generation and retexturing endpoints available through fal.ai.

## Endpoints Overview

| Endpoint                    | ID                                            | Input                   | Output           | Pricing           |
| --------------------------- | --------------------------------------------- | ----------------------- | ---------------- | ----------------- |
| Hunyuan3D v3                | `fal-ai/hunyuan3d-v3/image-to-3d`             | 1 image                 | GLB              | Varies            |
| Tripo3D v2.5 Single         | `tripo3d/tripo/v2.5/image-to-3d`              | 1 image                 | GLB              | $0.20-0.40        |
| Tripo3D v2.5 Multiview      | `tripo3d/tripo/v2.5/multiview-to-3d`          | 1-4 views               | GLB              | $0.20-0.40        |
| Meshy 5 Multi-Image         | `fal-ai/meshy/v5/multi-image-to-3d`           | 1-4 images              | GLB/FBX/OBJ/USDZ | ~10-20 credits    |
| Meshy 5 Retexture           | `fal-ai/meshy/v5/retexture`                   | 3D model + prompt/image | GLB/FBX/OBJ/USDZ | Varies            |
| Meshy 5 Remesh              | `fal-ai/meshy/v5/remesh`                      | 3D model                | GLB              | Varies            |
| Meshy 6 Preview Image-to-3D | `fal-ai/meshy/v6-preview/image-to-3d`         | 1 image                 | GLB              | ~10-20 credits    |
| Meshy 6 Preview Text-to-3D  | `fal-ai/meshy/v6-preview/text-to-3d`          | text prompt             | GLB              | ~10-20 credits    |
| Qwen Multi-Angle            | `fal-ai/qwen-image-edit-2511-multiple-angles` | 1 image + angle         | PNG              | ~$0.035/megapixel |

## Tripo3D v2.5 Multiview-to-3D

Generates 3D models from multiple view images. Better geometry than single-image.

### Endpoint

`tripo3d/tripo/v2.5/multiview-to-3d`

### Parameters

| Param               | Type    | Required | Default          | Notes                          |
| ------------------- | ------- | -------- | ---------------- | ------------------------------ |
| `front_image_url`   | string  | yes      | -                | Front view (required)          |
| `left_image_url`    | string  | no       | -                | Left view                      |
| `back_image_url`    | string  | no       | -                | Back view                      |
| `right_image_url`   | string  | no       | -                | Right view                     |
| `seed`              | integer | no       | random           | Geometry reproducibility       |
| `face_limit`        | integer | no       | -                | Max face count                 |
| `pbr`               | boolean | no       | false            | Enable PBR materials           |
| `texture`           | enum    | no       | "standard"       | "no", "standard", "HD"         |
| `texture_seed`      | integer | no       | random           | Texture reproducibility        |
| `auto_size`         | boolean | no       | false            | Real-world scaling             |
| `quad`              | boolean | no       | false            | Quad mesh (+$0.05)             |
| `texture_alignment` | enum    | no       | "original_image" | "original_image" or "geometry" |
| `orientation`       | enum    | no       | "default"        | "default" or "align_image"     |

### Output

- `model_mesh` — Primary 3D model (GLB)
- `base_model` — Base model file
- `pbr_model` — PBR variant (if enabled)
- `rendered_image` — Preview (WebP)

### Pricing

$0.20 (no texture), $0.30 (standard), $0.40 (HD). +$0.05 for quad.

## Meshy 5 Multi-Image-to-3D

Generates 3D from 1-4 images of the same object at different angles.

### Endpoint

`fal-ai/meshy/v5/multi-image-to-3d`

### Parameters

| Param               | Type     | Required | Default    | Notes                                       |
| ------------------- | -------- | -------- | ---------- | ------------------------------------------- |
| `image_urls`        | string[] | yes      | -          | 1-4 images, different angles                |
| `topology`          | enum     | no       | "triangle" | "triangle" or "quad"                        |
| `target_polycount`  | integer  | no       | 30000      | Target polygon count                        |
| `symmetry_mode`     | enum     | no       | "auto"     | "off", "auto", "on"                         |
| `should_remesh`     | boolean  | no       | true       | Enable remesh                               |
| `should_texture`    | boolean  | no       | true       | Generate textures                           |
| `enable_pbr`        | boolean  | no       | false      | PBR maps                                    |
| `is_a_t_pose`       | boolean  | no       | false      | A/T pose for characters                     |
| `texture_prompt`    | string   | no       | -          | Text guidance for texturing (max 600 chars) |
| `texture_image_url` | string   | no       | -          | Image guidance for texturing                |

### Output

- `model_glb` — GLB model
- `model_urls` — URLs in GLB, FBX, OBJ, USDZ formats
- `thumbnail` — Preview PNG
- `texture_urls` — Base color + optional PBR maps

### Processing Time

3-7 minutes. Use different angles with consistent lighting.

## Meshy 5 Retexture

Applies new textures to existing 3D models using text or image guidance.

### Endpoint

`fal-ai/meshy/v5/retexture`

### Parameters

| Param                | Type    | Required    | Default | Notes                                                                  |
| -------------------- | ------- | ----------- | ------- | ---------------------------------------------------------------------- |
| `model_url`          | string  | yes         | -       | 3D model URL or base64 data URI (.glb, .gltf, .obj, .fbx, .stl)        |
| `text_style_prompt`  | string  | conditional | -       | Style description (max 600 chars). Required if no `image_style_url`    |
| `image_style_url`    | string  | conditional | -       | Style reference image (.jpg, .png). Required if no `text_style_prompt` |
| `enable_original_uv` | boolean | no          | true    | Keep original UVs                                                      |
| `enable_pbr`         | boolean | no          | false   | Generate PBR maps                                                      |

### Output

- `model_glb` — Retextured GLB
- `model_urls` — FBX, USDZ, GLB, OBJ formats
- `texture_urls` — Base color + PBR maps
- `thumbnail` — Preview image

### Processing Time

3-5 minutes.

### Use Case

Apply one shared style prompt across all 3D models in a project for visual uniformity. Use consistent retexture prompts and image refs to maintain a unified look.

## Meshy 5 Remesh

Optimizes mesh topology without changing appearance.

### Endpoint

`fal-ai/meshy/v5/remesh`

## Meshy 6 Preview

Latest Meshy generation. Higher quality but in preview.

### Endpoints

- Image-to-3D: `fal-ai/meshy/v6-preview/image-to-3d`
- Text-to-3D: `fal-ai/meshy/v6-preview/text-to-3d`

## When to Use What

| Scenario                                | Best Endpoint                                 |
| --------------------------------------- | --------------------------------------------- |
| Single concept art → multiview images   | Qwen Multi-Angle (4 rotated views for 3D)     |
| Single concept art → 3D prop            | Hunyuan3D v3 (shape accuracy)                 |
| Single concept art → rigged character   | Tripo3D v2.5 single image                     |
| Multiple view renders → high-quality 3D | Tripo3D v2.5 multiview or Meshy 5 multi-image |
| Unify textures across 3D assets         | Meshy 5 retexture                             |
| Quick 3D from text description          | Meshy 6 Preview text-to-3d                    |
| Optimize existing mesh topology         | Meshy 5 remesh                                |

## Tripo v3.0

Released September 2025. Available through Tripo Studio and Tripo API directly (not yet on fal.ai). Offers Ultra mode with up to 2M polygons, cleaner geometry, and higher-fidelity textures. Standard and Detailed (Ultra) modes. Monitor fal.ai for availability.
