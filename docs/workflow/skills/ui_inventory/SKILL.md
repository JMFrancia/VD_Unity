---
name: ui_inventory
description: Reviews a game spec and produces a complete inventory of every UI surface it requires — HUD, menus, panels, popups, toasts, overlays — with each surface described in enough structural detail to feed a mockup tool like Gliffy. Reads docs/StyleGuide.md if present. Iterates with the user, then writes docs/UI-Inventory.md. Planning only; writes no code. Use when the user wants a UI list, screen inventory, or mockup briefs from a spec.
user-invocable: true
argument-hint: "[path to spec doc]"
---

# ui_inventory

Enumerates every UI surface a spec requires, described so each one can be drawn without re-reading the spec.

**Behavior:** Write no code. This skill produces one document.

**Input:** A path to a spec, as an argument or conversationally. If none given, look in `docs/`.

**Output:** `docs/UI-Inventory.md`.

---

## Step 1 — Read the Sources

1. **The spec, in full** — the authority on what UI exists and what it contains.
2. **`docs/StyleGuide.md`** if present — chrome, type, color, and UI treatment.
3. **`CLAUDE.md`** — specifically the layer rules. Scenes capture input and emit intents; they don't hold logic. That shapes how each surface is described.

No style guide is fine here — structure is separable from styling. Note its absence in the doc and leave visual treatment as `TBD`.

---

## Step 2 — Enumerate Every Surface

Walk the spec **section by section**. UI hides outside the UI section — a mechanic that says "the player taps the station to collect" is specifying an interaction surface, and a rule that says "shows a storage-full state" is specifying a visual state.

Sweep for:

- **Persistent HUD** — every element, its corner, its toggle behavior, and its visibility conditions (an element that only appears once the player owns something is a state, not a footnote)
- **Menus** — build menus, collection menus, anything browsable
- **Panels** — the per-entity surfaces. These are usually the most-used screens in the game and the most under-specified in the spec.
- **Popups** — modal, dismissable, celebratory, informational
- **Toasts** — transient, non-blocking
- **Overlays** — drag/ghost previews, placement validity, selection highlights, range indicators
- **In-world UI** — progress bars, ready icons, state badges, anything rendered on an entity rather than in a layer above
- **Empty/edge states** — zero items, locked, full, blocked, disabled, insufficient funds. **These are surfaces and they're always missing from specs.**

---

## Step 3 — Describe Each Surface for Drawing

The test: **could someone draw this from the entry alone, without the spec open?** If not, it's not done.

Per surface:

Give every surface a stable **id** (`panel.station`, `popup.levelUp`, `hud.money`). **This doc is the naming authority** — milestone docs cite these ids rather than inventing their own descriptions, and mockups are filed against them.

Consequently: **this doc does not reference milestones.** Surface → milestone scheduling lives in the milestone summary, in one direction only, so the two can't drift.

```markdown
### <id> — <Name>
- **Type:** HUD / menu / panel / popup / toast / overlay / in-world
- **Spec:** §N
- **Purpose:** one line
- **Trigger:** what opens it
- **Dismissal:** how it closes
- **Modal:** does it block input behind it
- **Position:** where on screen
- **Contents:** every element, in hierarchy order. Each with: what it shows,
  where its data comes from, and whether it's interactive.
- **States:** default, empty, loading, error, locked, disabled — whichever apply
- **Interactions:** what each control does, expressed as the intent it emits
  (per CLAUDE.md, a scene emits `input:thingRequested`; it doesn't act)
- **Notes:** anything the spec left open
```

**Cite the spec (§N) for the source, but the contents list must be complete here** — this doc is the mockup brief, and a brief that says "see §12.4" can't be drawn.

That's the one deliberate exception to cite-don't-restate: contents get restated because that's the deliverable. Everything else — rationale, mechanics, data rules — gets cited.

---

## Step 4 — Present and Iterate

Show the user a **table**: surface name, type, trigger, and a one-line content summary. Scannable in under a minute.

Explicitly flag:
- **Surfaces the spec implies but never describes** — these are the valuable finds. A spec that says "purchase upgrades at the station" but never describes the upgrade UI has a hole, and this skill's main job is finding it.
- Anything you inferred rather than read
- Contradictions between spec sections
- Empty/edge states the spec never mentions

Iterate until satisfied. **Do not write the doc before they approve the list.**

---

## Step 5 — Write the Doc

Write `docs/UI-Inventory.md`:

```markdown
# <Game> — UI Inventory

**Spec:** <path> · **Style guide:** <path or "none"> · **Generated:** <date>

## Surfaces
| id | Name | Type | Trigger | Mockup needed |

## Screen Map
How surfaces relate — what opens what. A mermaid diagram if it helps.

## <Surface entries, grouped by type>
...

## Gaps Found
Surfaces the spec implies but never describes. The reason to read this section.

## Assumptions
Inferred rather than read. Each is a risk.

## Open Items
Unresolved, and what it blocks.

## Deferred
UI for deferred spec features — listed so nobody mocks it up early.
```

Tell the user the doc is ready and stop. Play the notification sound.

---

## Rules

- **Walk the spec section by section.** UI hides in mechanics prose.
- **Edge states are surfaces.** Empty, locked, full, blocked, disabled. Specs never list them.
- **The drawability test:** if it can't be drawn from the entry alone, the entry isn't finished.
- **Interactions are intents, not actions.** Per CLAUDE.md, a button emits `input:xRequested`.
- **Flag inferences.** If the spec didn't say it, say you decided it.
- **Finding gaps is the point.** The surfaces the spec forgot are worth more than the ones it listed.
