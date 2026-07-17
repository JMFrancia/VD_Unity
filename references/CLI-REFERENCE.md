# CLI Reference

Generated from the operation registry. Do not edit by hand.

All commands accept these global flags:

| Flag | Description |
| ---- | ----------- |
| `--json` | Output as structured JSON |
| `--project <path>` | Explicit project path (auto-detected from cwd if omitted) |
| `--from-json <file>` | Read input from a JSON file (for commands with complex parameters) |

## Generation

### `asset-bot generate image`

Generate a 2D image with automatic reference handling and style consistency. Uses Nano Banana Pro by default. Style comes from reference images, not text. Text prompts should describe only the subject when refs are available.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--prompt` | string | Yes | What to generate - subject, action, composition. No style text when refs exist. |
| `--category` | string | Yes | Asset category for ref discovery and organization |
| `--refs` | array | No | Explicit ref image paths (skips auto-discovery) |
| `--aspect-ratio` | `1:1` / `9:16` / `16:9` / `4:3` / `3:4` | No | Output aspect ratio. Auto-inferred from existing assets if omitted. |
| `--count` | number | No | Number of candidates to generate |
| `--seed` | number | No | Seed for reproducibility |
| `--api` | string | No | Provider ID override. Default: nano-banana. Any registered provider ID is accepted. |

### `asset-bot generate bootstrap`

Generate initial style reference candidates when no refs exist yet. Use this once to bootstrap a project/category, then approve candidates and proceed with standard generation.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--prompt` | string | Yes | Style and subject description for the initial reference generation |
| `--category` | string | Yes | Asset category for organization |
| `--reference-image` | string | No | Optional path to a reference image used to guide the generated style |
| `--count` | number | No | Number of candidates to generate |
| `--api` | string | No | Provider ID override. Default: nano-banana. Any registered provider ID is accepted. |

### `asset-bot generate pixel-art`

Generate pixel art sprites, tilesets, or animations using Retro Diffusion. Supports multiple prompt styles (top-down, isometric, side-view, front-facing) and animation modes.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--prompt` | string | Yes | What to generate - literal visual description only |
| `--category` | string | Yes | Asset category |
| `--prompt-style` | string | Yes | Retro Diffusion prompt style (e.g. "rd_pro__topdown") |
| `--width` | number | Yes | Output width in pixels |
| `--height` | number | Yes | Output height in pixels |
| `--count` | number | No | Number of candidates |
| `--seed` | number | No | Seed for reproducibility |
| `--api` | string | No | Provider ID override. Default: retro-diffusion. Any registered provider ID is accepted. |

### `asset-bot generate 3d`

Generate a 3D model (GLB) through a multi-step pipeline: source image -> multiview -> 3D conversion. Supports Hunyuan3D (default), Tripo3D, and Meshy backends.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--prompt` | string | Yes | Subject description for the 3D model |
| `--category` | string | Yes | Asset category |
| `--pipeline` | `multiview` / `single-image` / `text-to-3d` | No | Generation pipeline. Default: multiview (best quality). |
| `--api` | string | No | Provider ID override. Default: hunyuan3d. Any registered provider ID is accepted. |
| `--source-image` | string | No | Path to source image (skips image generation step) |
| `--seed` | number | No | Seed for reproducibility |

### `asset-bot generate audio`

Generate sound effects, music, or speech using ElevenLabs. SFX uses text descriptions, music uses prompts, speech uses text + voice ID.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--type` | `sfx` / `music` / `voice` | Yes | Audio type to generate |
| `--prompt` | string | Yes | Description (SFX), prompt (music), or text (voice) |
| `--category` | string | No | Asset category |
| `--duration-sec` | number | No | Duration in seconds |
| `--output-format` | `mp3` / `wav` | No | Audio output container (default: mp3). |
| `--voice-id` | string | No | ElevenLabs voice ID (for voice type) |
| `--count` | number | No | Number of candidates |
| `--api` | string | No | Provider ID override. Default: elevenlabs-sfx/elevenlabs-music/elevenlabs-speech based on type. Any registered provider ID is accepted. |

### `asset-bot generate scene`

Generate a 3D scene (Gaussian splats) from text, images, or video using World Labs. Outputs SPZ files for real-time rendering.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--prompt` | string | Yes | Scene description |
| `--category` | string | Yes | Asset category |
| `--source-image` | string | No | Path to source image |
| `--resolution` | `100k` / `500k` / `full` | No | Splat resolution. Default: 500k. |
| `--api` | string | No | Provider ID override. Default: worldlabs. Any registered provider ID is accepted. |

### `asset-bot generate from-template`

Generate asset candidates using a template. Templates lock down provider settings, dimensions, and prompt structure. Refs are discovered automatically from approved assets.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--template-id` | string | Yes | Template ID to use |
| `--prompt` | string | Yes | Subject description (style comes from refs, not text) |
| `--category` | string | No | Override category (defaults to template asset type) |
| `--name` | string | No | Asset name |
| `--negative-prompt` | string | No | Negative prompt |
| `--refs` | array | No | Extra reference image paths |
| `--count` | number | No | Number of candidates |
| `--seed` | number | No | Seed for reproducibility |

## Assets

### `asset-bot assets list`

List all assets in the project, optionally filtered by category or status. Returns asset ID, name, category, status, and file info.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--category` | string | No | Filter by category |
| `--status` | `pending` / `reviewing` / `approved` / `failed` | No | Filter by status |

### `asset-bot assets get`

Get details for a single asset by ID, including generation provenance and file metadata.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--asset-id` | string | Yes | The asset ID |
| `--category` | string | Yes | The asset category |

### `asset-bot assets import`

Import an existing image file as an approved project asset.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--category` | string | Yes | Asset category |
| `--name` | string | Yes | Display name for the new asset |
| `--source-path` | string | Yes | Absolute or project-relative image path |
| `--asset-id` | string | No | Optional explicit asset ID |

### `asset-bot assets promote`

Promote a generation candidate to an approved asset record.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--candidate-path` | string | Yes | Path to the candidate file |
| `--category` | string | Yes | Asset category |
| `--name` | string | Yes | Display name for the asset |
| `--asset-type-id` | string | Yes | Asset type ID |
| `--asset-id` | string | No | Explicit asset ID (auto-generated from name if omitted) |

### `asset-bot assets delete`

Delete an asset record and all its files.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--category` | string | Yes | Asset category |
| `--asset-id` | string | Yes | Asset ID to delete |

### `asset-bot assets candidates`

List generation candidates for an existing asset.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--category` | string | Yes | Asset category |
| `--asset-id` | string | Yes | Asset ID |

## Types

### `asset-bot types register`

Register a new asset type in the project, including generation provider, output formats, and optional quality/export rules.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--type-id` | string | Yes | Unique type identifier |
| `--name` | string | Yes | Display name |
| `--description` | string | Yes | What this asset type is for |
| `--provider-id` | string | Yes |  |
| `--generation-api` | string | No | Deprecated; use providerId instead. |
| `--prompt-style` | string | No | Default promptStyle for retro-diffusion generation. Locks the model tier for this asset type — templates that omit promptStyle inherit this value. Templates may override for edge cases. See api-retro-diffusion SKILL.md for valid values (e.g. "rd_pro__default", "animation__any_animation"). |
| `--output-formats` | array | Yes |  |
| `--dimensions` | object | No |  |
| `--ref-requirements` | array | No |  |
| `--export-specs` | array | No |  |
| `--qa-checks` | array | No |  |

### `asset-bot types list`

List all registered asset types.

No parameters.

## Templates

### `asset-bot templates create`

Create a custom template in the project. Templates define reusable generation settings for a specific asset type.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--template-id` | string | Yes | Unique template identifier |
| `--name` | string | Yes | Display name |
| `--description` | string | Yes | What this template produces |
| `--asset-type-id` | string | Yes | Which asset type this template belongs to |
| `--provider-id` | string | Yes |  |
| `--generation-api` | string | No | Deprecated; use providerId instead. |
| `--output-format` | `png` / `jpg` / `webp` / `glb` / `fbx` / `obj` / `svg` / `mp4` / `spz` / `mp3` / `wav` | Yes |  |
| `--params` | object | Yes |  |

### `asset-bot templates list`

List all templates available to the current project.

No parameters.

### `asset-bot templates get`

Get one template by templateId from the current project.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--template-id` | string | Yes | The template ID |

### `asset-bot templates delete`

Delete a custom template.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--template-id` | string | Yes | Template ID to delete |

## Preview

### `asset-bot preview candidates`

Open a local visual preview for generated candidates. Accepts candidate paths + output format, then returns a session URL for side-by-side review.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--title` | string | Yes | Preview session title |
| `--candidates` | array | Yes | Candidates to render in the preview viewer |
| `--output-format` | string | Yes | Output format used to select the correct viewer (png, glb, spz, mp3, mp4, etc.) |

### `asset-bot preview set-preference`

Set the project preview target preference. Use "browser" to always open the local HTTP preview.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--preference` | `browser` | Yes | Preferred preview target for this project |

## Project

### `asset-bot status`

Show project status and registered asset types.

No parameters.

### `asset-bot export`

Export approved assets to the sync target project.

| Flag | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `--categories` | array | No | Filter by categories |
| `--dry-run` | boolean | No | Preview actions without writing files |
