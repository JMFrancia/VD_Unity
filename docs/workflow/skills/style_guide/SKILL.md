---
name: style_guide
description: Walks the user through defining a visual and audio style guide for a game, then writes it to docs/StyleGuide.md. Reads the spec first and only asks what the spec does not already answer. Produces the reference doc that asset_list and ui_inventory consume. Planning only; writes no code. Use when the user wants to establish art direction, visual style, palette, or audio direction before assets are made.
user-invocable: true
argument-hint: "[path to spec doc]"
---

# style_guide

Defines the visual and audio direction for a game and writes it to `docs/StyleGuide.md`.

**Behavior:** Write no code. This skill produces one document.

**Input:** A path to a spec document, as an argument or conversationally. If none is given, look for one in `docs/`; ask if ambiguous.

**Output:** `docs/StyleGuide.md` — the reference that `asset_list` and `ui_inventory` read.

---

## Step 1 — Read First, Ask Second

Read the spec in full, plus `CLAUDE.md`. Extract everything already decided. Specs typically pin more than the user remembers: dimensionality, perspective, screen orientation, grid/cell size, placeholder policy, named reference works.

**Do not ask what the spec already answers.** Restate those as settled and move on. Re-asking a decided question wastes the user's time and invites accidental contradiction.

Build the question list from what's genuinely missing.

---

## Step 2 — Ask, in One Batch, With Defaults

Write the questions to a single markdown file (`docs/StyleGuide-Questions.md`) rather than asking conversationally. The user answers inline and hands it back. This is faster than a chat back-and-forth and leaves a record.

**Give every question a recommended default**, and tell the user that writing `d` accepts it. Most questions get `d`; the few that don't are the ones that matter. Lead with the ones that actually change the outcome.

Cover, skipping anything the spec settles:

**Tone & direction** *(usually the highest-leverage section — do it first)*
- What is the emotional register? Where does it sit between its reference works?
- **Look for tension in the concept itself.** If the title, theme, or mechanics pull in different directions (a cozy genre with a dark name; cute characters doing grim things), that tension IS the art direction question. Surface it explicitly — it is nearly always the most consequential thing in this skill and the most expensive to resolve later.
- Reference works, and specifically *what* to take from each.

**Technique**
- Art style: pixel art / vector / hand-painted / flat / cel / low-poly / photographic
- If pixel: resolution and whether it's pixel-perfect
- Line language: outlined or not; weight; uniform or dynamic
- Shape language: round/soft vs angular/sharp — and what that says about the world
- Rendering: flat fill / gradient / textured / lit

**Color**
- Palette: constrained (name the count) or open
- Key hues; what the background does; what the accent does
- How color signals state (ready, blocked, locked, disabled)
- Contrast and readability constraints

**Format**
- Canvas/target resolution, aspect, orientation
- Sprite dimensions and their relationship to the grid cell
- Scaling policy: integer scale, nearest-neighbour vs smooth

**Motion**
- Animation style: none / tweened / frame-based; frame counts
- Idle behavior: static or alive
- Feedback: what a tap, a completion, a reward feels like
- Easing character: snappy / bouncy / smooth

**Type & UI**
- Typeface direction; pixel font vs webfont
- UI chrome: chunky/rounded/tactile vs flat/minimal
- Panel, button, and corner treatment
- Iconography style

**Audio** *(ask whether it's in scope at all before going deep)*
- SFX direction and register
- Music: needed? style? adaptive?
- Whether audio is in scope for the prototype at all

---

## Step 3 — Draft and Iterate

Read the answers. **If an answer contradicts the spec or another answer, say so and resolve it — do not paper over it.**

Present a tight summary of the resulting direction — a paragraph, not the whole doc. The user should be able to tell in fifteen seconds whether you understood them.

Iterate until they're satisfied. Run another question round if real gaps emerge; number the rounds.

---

## Step 4 — Write the Doc

Write `docs/StyleGuide.md`:

```markdown
# <Game> — Style Guide

**Spec:** <path>
**Status:** <date>

## Direction
The one-paragraph statement of what this game looks and feels like. If someone reads only this section, they should be able to reject a wrong asset.

## Tone
Where it sits, what tension it resolves and how, what it is deliberately NOT.

## References
| Work | Take this | Not this |

## Technique
Style, line, shape, rendering.

## Color
Palette (with hex values), roles, state signalling, contrast rules.

## Format
Resolutions, sprite dimensions, grid relationship, scaling policy.

## Motion
Animation approach, feedback character, easing.

## Type & UI
Typeface, chrome, panels, buttons, icons.

## Audio
SFX and music direction, or explicitly out of scope.

## Placeholder Policy
What placeholder art looks like until real art exists, and how it gets swapped (per the spec's data-driven asset paths).

## Decided vs Open
What is settled, and what is deliberately left for later.
```

**Rules for the doc:**
- Every rule must be **actionable by someone who wasn't in the conversation.** "Cozy but unsettling" is a vibe; "warm palette, round shapes, one desaturated cold accent that appears only on Void elements" is a rule.
- Include hex values, pixel dimensions, and frame counts. Numbers or it isn't a guide.
- State what the game is **not**, not just what it is. Exclusions do more work than inclusions.
- Where the spec already decided something, cite it (§N) rather than restating.

Tell the user the doc is ready, note that `asset_list` and `ui_inventory` consume it, and stop. Play the notification sound.

---

## Rules

- **Read the spec before asking anything.**
- **Never ask a question the spec answers.**
- **Surface concept tension explicitly** — it's the highest-value thing here.
- **Numbers, not adjectives.** A style guide without values isn't one.
- **Don't invent the user's taste.** Propose defaults, but a default is an offer, not a decision.
