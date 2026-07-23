---
name: preproduction
description: Runs the full pre-production pipeline on a game spec — style_guide, then asset_list, ui_inventory, and plan_milestones — then independently reconciles the resulting docs against each other and writes a manifest. Resumable; skips stages whose docs already exist. Ends where implementation begins; writes no code and does not implement. Use when the user has a finished spec and wants everything needed to start building.
user-invocable: true
argument-hint: "[path to spec doc]"
---

# preproduction

Takes a finished spec and produces everything needed before code: a style guide, an asset catalog, a UI inventory, and a milestone plan — then **checks they agree with each other.**

**Behavior:** Write no code. Do not implement anything. This skill ends where `implement_milestone` begins.

**Input:** A path to a spec, as an argument or conversationally. If none given, look in `docs/`.

**Output:**
```
docs/StyleGuide.md
docs/assets/            (one doc per type + summary)
docs/UI-Inventory.md
milestones/<spec>/      (one doc per milestone + summary)
docs/Preproduction.md   (the manifest)
```

**This skill is mostly sequencing.** Its real value is Step 5 — the independent reconciliation that none of the four sub-skills can do for themselves.

---

## Step 0 — Resolve and Survey

Read the spec. **If it has large TBDs or unresolved contradictions, stop and say so** — pre-production on an unfinished spec produces four documents that all inherit the same holes.

Then survey what already exists:

| Doc | Skill | Exists? |
|---|---|---|
| `docs/StyleGuide.md` | `style_guide` | |
| `docs/assets/` | `asset_list` | |
| `docs/UI-Inventory.md` | `ui_inventory` | |
| `milestones/<spec>/` | `plan_milestones` | |

**Show the user this table and the plan** before running anything. For each existing doc, ask: **reuse, regenerate, or skip?** Default to reuse — never silently re-ask a question the user already answered.

Note that this runs in stages with a user gate in each. It is not fire-and-forget; it'll take a while, and they'll be answering questions throughout.

---

## Step 1 — `style_guide`

Invoke `style_guide` via the Skill tool with the spec path. Let it run its full course, including its own user iteration. Do not shortcut its questions.

Skip if reusing an existing `docs/StyleGuide.md`.

**Why first:** the catalogs need dimensions, palette, and format to specify assets rather than merely list them.

---

## Step 2 — `asset_list`

Invoke `asset_list` with the spec path. It reads the style guide itself.

Skip if reusing `docs/assets/`.

---

## Step 3 — `ui_inventory`

Invoke `ui_inventory` with the spec path. It reads the style guide itself.

Skip if reusing `docs/UI-Inventory.md`.

> Steps 2 and 3 are independent of each other and could run in either order. Run them sequentially anyway — each has a user gate, and interleaving two question rounds is confusing.

---

## Step 4 — `plan_milestones`

Invoke `plan_milestones` with the spec path. It reads both catalogs and cites their ids.

Skip if reusing `milestones/<spec>/`.

**Why last:** the catalogs are the naming authority and are a pure function of the spec. The milestone cut depends on scope and priority calls, which makes it the volatile one. Stable before volatile.

---

## Step 5 — Reconcile (the reason this skill exists)

Four docs generated in sequence by one agent **will** drift. The agent that wrote them cannot see it — it will ratify its own reasoning. Confirming this is cheap; skipping it is how a broken reference reaches an implementer.

**Spawn two cold subagents in parallel** (one message, two tool calls). Give them only the file paths — **never your reasoning about them.**

**Agent A — Reference integrity.** Mechanical, both directions:
- Every asset id cited in a milestone doc resolves to an entry in `docs/assets/`
- Every UI id cited in a milestone doc resolves to an entry in `docs/UI-Inventory.md`
- Every UI surface in the inventory has the assets it needs present in the catalog
- Every asset in the catalog is reachable — cited by some milestone, or explicitly deferred
- Ids are spelled identically across docs (`station.field.working` vs `field_working` is a finding)

**Agent B — Contradiction and deferred integrity.** Semantic:
- Does the style guide contradict the spec? (dimensions, orientation, perspective, cell size, placeholder policy)
- Do any two docs disagree about the same thing?
- Does anything the spec marks **deferred** appear in a milestone, catalog, or inventory?
- Does any doc assert something the spec never said? (the fabrication check)
- Are the spec's open/unresolved items handled, or silently assumed away?

Both are read-only and return findings ranked by importance.

**Fold real findings back into the offending doc.** Don't accept a finding you believe is wrong — say why in Step 6 instead. If a finding reveals a genuine gap in the spec, **stop and ask the user.** Do not invent the answer.

---

## Step 6 — Write the Manifest

Write `docs/Preproduction.md`:

```markdown
# <Game> — Pre-production Manifest

**Spec:** <path> · **Generated:** <date>

## Artifacts
| Doc | Purpose | Status |
Status: generated this run / reused / skipped.

## Reconciliation
What the cross-document check found, and what was fixed. If nothing was found, say so —
a clean result is information, not an empty section.

## Open Items — All Docs
Every unresolved item, collected from all four docs into one place. This is the list the
user actually needs. For each: what it blocks, and which doc owns it.

## Assumptions — All Docs
Everything assumed rather than read from the spec, collected. Each is a risk.

## What's Ready
Whether the milestone plan can be implemented, and what would block milestone 1.

## Next
`implement_milestone` — one milestone, then stop.
```

Tell the user pre-production is complete, point at the manifest, and stop. Play the notification sound.

**Do not implement anything.**

---

## Rules

- **Never implement.** This skill ends where `implement_milestone` begins.
- **Reuse over re-ask.** An existing doc is an answered question. Regenerate only on request.
- **Let the sub-skills run their own gates.** Don't shortcut their questions to move faster; their iteration is where the quality is.
- **The reconcile is cold or it's worthless.** Never let an agent that watched the docs get written audit them.
- **Never invent a spec answer.** A gap found in Step 5 is a question for the user, not a blank to fill.
- **A clean reconcile is a result.** Report it plainly; don't manufacture findings to look thorough.
