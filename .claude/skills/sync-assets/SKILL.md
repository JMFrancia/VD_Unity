---
name: sync-assets
description: >
  Import assets from and export assets to external game projects. Use when
  the user wants to bring existing game files into asset-bot, sync approved
  assets back to a game engine directory, or configure import/export mappings
  between asset-bot and a target project. Handles scanning, previewing, and
  executing bulk file operations with configurable category mappings and
  template variables.
metadata:
  assetBot:
    commands:
      - asset-bot assets list
      - asset-bot assets import
    references:
      - ../../../references/CLI-REFERENCE.md
---

# sync-assets

Import from and export to external game projects. Handles scanning, mapping, previewing, and syncing assets between asset-bot and game engine directories.

## Import Workflow

```
status      ← Check if project is configured for import
    ↓
setup       ← Configure sync target + mappings
    ↓
scan        ← Discover importable files
    ↓
preview     ← Dry run — see what would happen
    ↓
execute     ← Create records from discovered files
```

### Import Functions

- `status({ projectId })` — Check sync target and config
- `setup({ projectId, syncTargetRoot, mappings?, saveConfig? })` — Configure sync target
- `scan({ projectId, mappings?, useSavedConfig? })` — Discover importable files
- `preview({ projectId, mappings?, useSavedConfig?, skipExisting? })` — Dry run
- `execute({ projectId, mappings?, useSavedConfig?, skipExisting? })` — Execute import
- `getConfig({ projectId })` — Get saved config
- `setConfig({ projectId, syncTargetRoot, mappings })` — Save config

### Import Mappings

Mappings control how files are categorized and named:

```json
{
  "sourcePattern": "cdn/contractors/*/portrait.png",
  "category": "contractors",
  "assetTypeId": "contractor-portrait",
  "idTemplate": "{parent}-portrait",
  "nameTemplate": "{parent} Portrait"
}
```

### Import Template Variables

| Variable      | Example                   | Description                |
| ------------- | ------------------------- | -------------------------- |
| `{directory}` | `cdn/contractors/phantom` | Full directory path        |
| `{filename}`  | `portrait`                | Filename without extension |
| `{parent}`    | `phantom`                 | Parent directory name      |
| `{ext}`       | `png`                     | File extension             |
| `{n}`         | `1`                       | Sequence number            |

### Import Pattern Examples

```
cdn/contractors/*/portrait.png     → cdn/contractors/phantom/portrait.png
cdn/businesses/*.png               → cdn/businesses/casino.png
assets/**/*.glb                    → assets/props/chair/model.glb
```

### Import Results

```json
{
  "imported": [{ "id": "phantom-portrait", "status": "approved" }],
  "skipped": [{ "sourcePath": "...", "reason": "already exists" }],
  "errors": [{ "sourcePath": "...", "error": "file not found" }]
}
```

### Typical Import Workflow

1. **Check status** — See if project is configured
2. **Setup** — Point to game content directory, define mappings
3. **Scan** — See what files are available
4. **Preview** — Dry run with `skipExisting: true`
5. **Execute** — Import the files
6. **Verify** — Use `asset-bot assets list` to confirm

## Export Workflow

Syncs canonical assets from asset-bot to external game projects. Maps categories to target directories with configurable filename patterns, template transforms, and optional JSON config updates.

### Prerequisites

- Project must have `syncTarget` configured (via project update)
- Assets must be `approved` with canonical files

### Export Functions

- `exploreTarget({ projectId, maxDepth? })` — Walk sync target, return structure
- `saveExportConfig({ projectId, config })` — Save mapper config
- `loadExportConfig({ projectId })` — Load mapper config
- `syncAssets({ projectId, dryRun?, categories? })` — Copy canonicals to target

### Export Config

```json
{
  "projectId": "my-game",
  "syncTargetRoot": "../venus-content/H5/my-game",
  "mappings": [
    {
      "assetCategory": "contractors",
      "targetDir": "cdn/contractors/{name|extract:(.+)-idle-\\d+|underscore}/",
      "filenamePattern": "{name|underscore}.mp4",
      "configPath": "config/contractor-system.config.json",
      "configKey": "simulation.entities.contractor_{name|extract:(.+)-idle-\\d+|underscore}.metadata.idleAnimations",
      "arrayMode": "replace",
      "groupBy": "{name|extract:(.+)-idle-\\d+}"
    }
  ]
}
```

### Export Template Variables

Base variables in `targetDir`, `filenamePattern`, `configKey`, `groupBy`:

- `{name}` — Record ID
- `{n}` — File index (1-based)
- `{category}` — Asset category

### Export Template Transforms

Chain transforms with pipe `|` syntax:

| Transform       | Description           | Example                                          |
| --------------- | --------------------- | ------------------------------------------------ |
| `underscore`    | Hyphens → underscores | `{name\|underscore}` → `black_widow_idle_1`      |
| `hyphen`        | Underscores → hyphens | `{name\|hyphen}` → `black-widow-idle-1`          |
| `extract:REGEX` | Regex capture group   | `{name\|extract:(.+)-idle-\\d+}` → `black-widow` |

Transforms chain: `{name|extract:(.+)-idle-\d+|underscore}` → `black_widow`

### Array Mode

| Mode               | Behavior                        |
| ------------------ | ------------------------------- |
| `append` (default) | Add to existing array (deduped) |
| `replace`          | Overwrite array                 |

### Grouping

`groupBy` groups assets before config updates. With `arrayMode: "replace"`:

1. Assets grouped by resolved `groupBy`
2. All files in group copied
3. Single config update replaces array with all group values

### Sync Behavior

1. Find approved assets matching each mapping's `assetCategory`
2. Resolve templates with transforms
3. Group by `groupBy` if set
4. Copy canonical files to resolved target paths
5. Update config files (ungrouped: per-file; grouped with replace: per group)
6. Update `syncTarget.lastSyncAt`

## Integration

- `manage-assets` — Record CRUD, project config
