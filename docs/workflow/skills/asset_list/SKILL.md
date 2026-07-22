---
name: asset_list
description: Reviews a game spec and enumerates every asset the game needs, then writes one doc per asset type into docs/assets/ (sprites, ui, vfx, sfx, music, models…). Reads docs/StyleGuide.md if present for dimensions and format. Presents the full list for the user to iterate on before writing. Planning only; writes no code. Use when the user wants an asset list, asset breakdown, or art production docs from a spec.
user-invocable: true
argument-hint: "[path to spec doc]"
---

# asset_list

Enumerates every asset a spec implies and writes production docs, one per asset type.

**Behavior:** Write no code. This skill produces documents.

**Input:** A path to a spec, as an argument or conversationally. If none given, look in `docs/`.

**Output:** `docs/assets/` containing `00-summary.md` and one doc per asset type.

---

## Step 1 — Read the Sources

Read, in order:
1. **The spec, in full.** It is the authority on what exists in the game.
2. **`docs/StyleGuide.md`** if it exists — the authority on dimensions, palette, and format.
3. **`CLAUDE.md`** — for the placeholder policy and the data-driven asset-path rule.

**If there's no style guide, say so and ask whether to proceed.** Without one you can enumerate *what* is needed but not specify *how* it should look — the docs will be lists rather than briefs. That's a legitimate choice; make it the user's.

---

## Step 2 — Ask Only What's Missing

Most of this is derivable. Ask only what genuinely isn't, in one batch, with defaults (`d` accepts):

- 2D or 3D, if the spec doesn't pin it (determines whether model/texture/rig docs exist at all)
- Are SFX and music in scope?
- Is VFX a separate discipline here, or does it fall out of sprites?
- Any assets needed for systems the spec defers — produce now for the art pipeline's lead time, or omit?
- Target platform constraints that affect format or budget

Skip anything the spec or style guide settles.

---

## Step 3 — Enumerate Systematically

Walk the spec **section by section**, not from memory. Assets hide in prose and in tables, and an asset you forget here is one nobody makes.

Sweep for:

- **Entities** — every buildable, placeable, or spawnable thing, from the spec's own lists
- **States** — this is where counts explode and where enumeration usually fails. For each entity, ask what visual states the spec implies: idle, active/working, complete/ready, blocked, error/full, locked, selected, ghost/preview, disabled. **A spec that says "shows a storage-full state" is specifying an asset.**
- **Items** — every resource, currency, and collectable needs an icon
- **Characters** — each species/variant, times each state and rarity treatment
- **UI chrome** — panels, buttons, frames, bars, badges, lock icons, tabs, scroll affordances. Cross-check against `ui_inventory` output if it exists.
- **Feedback & VFX** — anything the spec says appears, pulses, bounces, floats, or announces. Progress bars, ready indicators, heart icons, toasts, level-up flourishes.
- **Environment** — ground, terrain, grid, borders, background
- **Type** — fonts, and any bitmap font that needs generating
- **Audio** — one SFX per distinct player action and per system event; music per context
- **Marketing/meta** — icon, splash, favicon, if in scope

For each asset, capture:

| Field | Notes |
|---|---|
| `id` | Stable, matches the data key it'll be referenced by. **This doc is the authority for asset ids** — see below |
| What it is | One line |
| Spec ref | §N — **cite, don't restate** |
| Variants/states | The count driver |
| Quantity | Show the arithmetic: 6 species × 4 states = 24 |
| Dimensions/format | From the style guide; `TBD` if none |
| Placeholder | What stands in until it's real |

### This doc owns asset ids

These docs are the **naming authority**. Milestone docs, JSON asset paths, and any downstream tooling reference these ids; they do not invent their own descriptions.

Consequently: **this doc does not reference milestones.** Asset → milestone scheduling lives in the milestone summary, in one direction only, so the two can't drift apart. If asked which assets a milestone needs, point at `milestones/<spec>/00-summary.md`.

Choose ids that will survive: stable, lowercase, dotted or kebab, matching the data key the game will load them by (per `CLAUDE.md`, asset paths live in JSON). `station.field.working` beats `field_sprite_2`.

---

## Step 4 — Present and Iterate

Show the user a **summary** — counts per type, the total, and the biggest cost drivers. Not the whole list; a table they can scan.

Explicitly flag:
- **Where the count explodes** (states × variants is almost always the surprise)
- Anything you inferred rather than read — the spec says "an icon appears"; you decided it's one icon per resource
- Anything you couldn't resolve
- Where the total looks unreasonable for the project's stated scope

Iterate until the user is satisfied. **Do not write docs before they approve the list.**

---

## Step 5 — Write the Docs

Create `docs/assets/`. Write one doc per type that actually has assets — don't create an empty `models.md` for a 2D game.

Typical set: `01-sprites.md`, `02-ui.md`, `03-vfx.md`, `04-sfx.md`, `05-music.md`, `06-fonts.md`. For 3D add models, textures, rigs, animations.

Per doc:

```markdown
# Assets — <Type>

**Spec:** <path> · **Style guide:** <path or "none">
**Count:** N

## Format
Dimensions, file format, naming convention, delivery notes. From the style guide.

## Assets
| id | What | States/Variants | Qty | Spec | Placeholder |

## Notes
Ambiguities, assumptions, anything the spec left open.
```

Then `docs/assets/00-summary.md`:

```markdown
# <Game> — Asset Summary

**Spec:** <path> · **Style guide:** <path or "none"> · **Generated:** <date>

## Totals
| Type | Count | Doc |

## Cost Drivers
Where the volume actually is, and what would cut it.

## Assumptions
What was inferred rather than read from the spec. Each is a risk.

## Open Items
Unresolved, and what it blocks.

## Deferred
Assets for deferred spec features, or explicitly excluded.
```

Tell the user the directory is ready and stop. Play the notification sound.

---

## Rules

- **Walk the spec section by section.** Never enumerate from memory.
- **States are assets.** The count is entities × states, and that's where estimates go wrong.
- **Cite the spec, don't restate it.** Docs that duplicate spec text drift from it.
- **Ids match data keys.** These docs feed JSON asset paths (per CLAUDE.md); the ids should line up.
- **This doc names things; it does not schedule them.** Scheduling is the milestone plan's job, referenced one way.
- **Flag inferences.** If the spec didn't say it, say that you decided it.
- **Don't invent style.** No style guide means `TBD` in the format column, not a guess.
