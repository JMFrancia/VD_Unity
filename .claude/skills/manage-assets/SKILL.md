---
name: manage-assets
description: >
  Manage projects, asset records, templates, and manifests. Use when the user
  wants to create or list projects, register asset types, create or update
  asset records, promote candidates, manage templates, generate manifests,
  link references, or check project status. Provides unified CRUD for all
  day-to-day data management operations.
metadata:
  assetBot:
    commands:
      - asset-bot status
      - asset-bot assets list
      - asset-bot assets get
      - asset-bot assets import
      - asset-bot assets promote
      - asset-bot assets delete
      - asset-bot assets candidates
      - asset-bot types register
      - asset-bot types list
      - asset-bot templates create
      - asset-bot templates list
      - asset-bot templates get
      - asset-bot templates delete
    references:
      - ../../../references/CLI-REFERENCE.md
---

# manage-assets

Unified CRUD for projects, records, templates, and manifests. This skill consolidates all day-to-day data management operations.

## CLI Commands

For exact parameters and flags, run `asset-bot <command> --help` or see `../../../references/CLI-REFERENCE.md`.

### Project

```bash
asset-bot status --json
```

### Assets

```bash
asset-bot assets list --category <category> --status approved --json
asset-bot assets get --asset-id <id> --category <category> --json
asset-bot assets import --category <category> --name "My Asset" --source-path ./path/to/file.png --json
asset-bot assets promote --candidate-path ./path/to/candidate.png --category <category> --name "My Asset" --asset-type-id <typeId> --json
asset-bot assets delete --category <category> --asset-id <id> --json
asset-bot assets candidates --category <category> --asset-id <id> --json
```

### Asset Types

```bash
asset-bot types register --from-json asset-type.json --json
asset-bot types list --json
```

### Templates

```bash
asset-bot templates create --from-json template.json --json
asset-bot templates list --json
asset-bot templates get --template-id <id> --json
asset-bot templates delete --template-id <id> --json
```

## Project Management

### Functions

- `createProject(args)` — Create project with directory scaffold
- `getProject({ projectId })` — Get project config
- `listProjects()` — List all projects
- `updateProject(args)` — Partial update of project config
- `deleteProject({ projectId })` — Delete project and all files
- `registerAssetType({ projectId, assetType })` — Register asset type
- `listAssetTypes({ projectId })` — List registered asset types
- `getProjectStatus({ projectId })` — Summary with counts

### Asset Type Registration

Asset types require a JSON definition:

```json
{
  "typeId": "block-texture",
  "name": "Block Texture",
  "description": "Colored block surface textures",
  "providerId": "nano-banana",
  "outputFormats": ["png"]
}
```

Register via: `asset-bot types register --from-json type.json --json`

### Project Scaffold

Created by `asset-bot init`:

```
.asset-bot/
  project.json
  asset-types/
  templates/
  refs/style/
  refs/per-type/
  assets/
  exports/
```

## Record Management

### Functions

- `createAsset({ projectId, asset })` — Create record with directory scaffold (can include `refLinks`)
- `getAsset({ projectId, category, assetId })` — Get a record
- `listAssets({ projectId, category? })` — List records, optionally filtered
- `updateAsset({ projectId, category, assetId, ... })` — Update record fields
- `deleteAsset({ projectId, category, assetId })` — Delete record and files
- `deleteCategory({ projectId, categories, dryRun? })` — Delete all in matching categories (supports `*` glob)
- `listCandidates({ projectId, category, assetId })` — List candidate files
- `selectCandidate({ projectId, category, assetId, candidateFilename })` — Copy candidate to canonical
- `generateManifest({ projectId })` — Build manifest.json of approved assets
- `linkRefAsset({ projectId, category, assetId, ref })` — Link a record as ref (adds/replaces by role)
- `unlinkRefAsset({ projectId, category, assetId, role })` — Remove a ref link by role
- `importCanonical({ projectId, asset, sourcePath })` — Import external file as approved canonical
- `getAssetRefLinks({ projectId, category, assetId })` — Get resolved ref paths

### Record Directory Structure

```
assets/<category>/<id>/
  record.json       # Status, refLinks, metadata
  canonical/        # Selected winner
  candidates/       # Generated variants
  .archive/         # Previous canonical files
  refs/             # Per-record reference material
```

### Candidate Workflow

1. Create record → status: `pending`
2. Generate candidates → status: `generating` → `reviewing`
3. Select candidate → copied to `canonical/`, old canonical archived
4. Record status → `approved`
5. Generate manifest → includes all approved assets

### Ref Links

Link records as references for generation:

```typescript
ref: {
  role: string; // "portrait", "style", "source-image", etc.
  assetId: string; // Target record ID
  category: string; // Target category
}
```

Use `linkRefAsset` to add, `unlinkRefAsset` to remove by role.

## Template Management

### Functions

- `createTemplate({ projectId, template })` — Create custom template
- `updateTemplate({ projectId, templateId, template })` — Update custom template
- `listTemplates({ projectId })` — All templates (built-in + custom)
- `getTemplate({ projectId, templateId })` — Get by ID
- `deleteTemplate({ projectId, templateId })` — Delete custom template
- `listBuiltInTemplates()` — Built-in templates only

### Template Resolution Order

1. Custom templates in `templates/`
2. Built-in templates in core

Built-in templates cannot be deleted.

### Template Structure

Templates require a JSON definition:

```json
{
  "templateId": "block-texture-nb",
  "name": "Block Texture (NB)",
  "description": "Nano Banana block texture template",
  "assetTypeId": "block-texture",
  "providerId": "nano-banana",
  "outputFormat": "png",
  "params": {
    "promptTemplate": "A {{subject}} game texture, top-down view, seamless tile",
    "width": 512,
    "height": 512
  }
}
```

Create via: `asset-bot templates create --from-json template.json --json`

## Bulk Operations

For importing multiple files from external projects, use the `sync-assets` skill which provides scanning, previewing, and importing workflows.
