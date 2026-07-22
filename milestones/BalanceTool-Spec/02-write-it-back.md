# Milestone 02 — Write It Back

**Demonstrable outcome:** halve a recipe duration in the JSON, run `write --apply`, see the new value in the
Unity inspector, press Play and watch the job finish in half the time — with `git diff` showing exactly one
changed line.

## Goal
Close the round trip. After this milestone the tool is genuinely useful even if nothing else ships: you can
bulk-edit the economy in a text editor, with a real diff and real version control, which the inspector
cannot give you.

This is also **the most dangerous code in the tool** — it edits files the game depends on. Build it
paranoid.

## Build This
- **`Unity/AssetWriter.cs`** — resolve a config path to a specific line in a specific asset, replace only the
  scalar after the colon, leave every other byte untouched. **Never reserialize** — YamlDotNet round-tripping
  reformats whole files and produces unreviewable diffs.
- **Structural insertion** for new recipes, level rows and upgrade tiers: correctly-indented block insertion.
  A new `RecipeSO` also needs a generated `.meta` with a fresh GUID and a reference appended to its owning
  `StationSO.recipes`.
- **Change summary** — `asset.field: old → new`, one line each, plus assets to be created.
- **Dry-run is the default.** `--apply` is required to touch a file.
- **Refusals:** a `schemaVersion` mismatch, a resource or station id matching no asset, or any structurally
  invalid config aborts **before** writing anything. Never half-apply.
- **CLI:** `balance write --config X --project ../.. [--apply]`.
- **Round-trip test:** `read → write --apply → read` produces byte-identical JSON and the second write
  reports zero changes.

## Do NOT Build This
- **Creating resources or station types.** They need icons, prefabs, meshes and thumbnails; those stay
  authored in Unity. Recipes, level rows and upgrade tiers only.
- **A backup/undo system.** Git is the undo. Don't build a shadow copy mechanism.
- **The server or UI** → M4.
- **Any simulation** → M3.

## Context
- **New:** `Unity/AssetWriter.cs`, round-trip test, `write` verb.
- **Touched in the Unity project:** only assets the user explicitly asks the tool to write.

## Principles
- **Minimal diff is a correctness property, not a nicety.** A change you can't review in `git diff` is a
  change you can't trust. One field changed ⇒ one line changed.
- **Fail loud, fail early, fail whole.** Validate everything before the first byte is written.
- **The inspector stays the source of truth** (CLAUDE.md rule 1). This writes *into* that surface; it does
  not replace it.

## Definition of Done
- `write` with no edits reports zero changes and leaves `git diff` on `Assets/` empty.
- Halving `Recipe_Field_WheatGrow.duration` and applying produces exactly one changed line in `git diff`, the
  inspector shows the new value, and a wheat job in Play takes half as long.
- A JSON with a bogus resource id, and one with `schemaVersion: 999`, both abort with a named message and
  leave `Assets/` untouched.
- The round-trip test passes.
- Adding a new recipe row creates a valid `RecipeSO` + `.meta`, wires it into its station, and the game boots
  with it (passes `BootValidator`).

## How to Test
1. `read`, then `write --apply` with no edits. Confirm zero changes reported and `git diff` empty.
2. Halve a field recipe duration. Run `write` (no `--apply`) and read the one-line summary. Run with
   `--apply`. Check `git diff`, the inspector, and then Play.
3. `git checkout Assets/` to revert.
4. Hand-corrupt a JSON two ways (bogus id; bad schemaVersion). Confirm both abort and `git diff` is empty.
5. Add a recipe row in the JSON, apply, and confirm the game boots and the recipe appears in the station panel.

**Acceptance cases covered:** QA-2, QA-3, QA-4.
