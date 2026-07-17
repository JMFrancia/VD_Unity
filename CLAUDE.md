# VoidDay — Unity Prototype

## This file supersedes the global CLAUDE.md

This project is a **rapid prototype**. The global `~/.claude/CLAUDE.md` is tuned for slow, careful, production-quality work. Where the two conflict, **this file wins**.

Specifically **ignored** from the global file:
- The Code Review Checklist
- The 7-step Bug Fix Protocol
- The Multi-Phase Implementation Workflow (milestones are driven by a skill instead — see *Milestones* below)
- "Plan first / seek confirmation / pause for approval" ceremony
- Strict DRY

Specifically **retained**:
- KISS and YAGNI (with the carve-out in *Architecture* below)
- Fail loud — never swallow an exception
- Fail fast at the data boundary
- Fix root causes, not symptoms
- Comment hygiene (explain non-obvious *why*, never the obvious *what*; no leftover debug code)
- The notification sound when waiting on the user

**Testing is mostly suspended, with one exception.** No TDD, no coverage targets, no test gate. The exception is the **pure-C# economy core** (§ *The Core boundary*): recipes, costs, effect resolution, order pricing. Bugs there are invisible — they don't crash, they just make the game subtly wrong — and the core has no Unity dependencies, so tests are cheap. Test it. Nothing else.

---

## Stack

Unity **6000.3.7f1**, **URP (Universal 3D)**, new **Input System**.

- **3D meshes, top-down camera.** Portrait phone aspect. Touch/drag only — no keyboard.
- Target: WebGL build must load and be tappable in a browser.
- Verification is: press Play, or open the WebGL build. There is no other gate.

### Verify APIs against what's installed

Unity 6.3 and the current URP / Input System packages are recent enough that recall is unreliable — and the failure mode is a confident call to an API that was renamed or removed. Before using an unfamiliar API, check `Packages/manifest.json` for the actual package version and verify against the installed source rather than memory. If a call can't be confirmed, say so instead of guessing.

Legacy `Input.GetMouseButton` etc. do **not** work — this project uses the new Input System.

### Editor access via the Unity MCP

**A Unity MCP is configured in this project**, so an agent has editor access, not just file access. Beyond writing C# and asset text, the agent can create and modify GameObjects and components, **assign serialized fields and wire scene references**, create/edit ScriptableObject assets and materials, open/create/save scenes and prefabs, run EditMode/PlayMode tests, drive playmode, take screenshots, and refresh the AssetDatabase. An asset swap (placeholder → real) is an SO-reference edit the agent can make directly.

Still **prefer data-driven, code-driven setup** where it keeps the project clean — SO instances via `CreateAssetMenu`, scenes buildable from code, dependencies wired in `Awake` — because that keeps behavior in version control and out of hand-wired scene state. But when a milestone genuinely needs editor work (assigning a serialized reference, placing a GameObject, running a test), do it through the MCP rather than handing the user manual steps.

*(Before an unfamiliar MCP operation, confirm the tool exists and behaves as expected rather than assuming — the toolset can change.)*

---

## Architecture

Three rules define this project. They are **not** subject to YAGNI — they are the requirement, not speculative generality. Everything else bends for speed; these do not.

### 1. Data-driven — no hardcoded values

**Every tunable lives in a ScriptableObject.** Not in a constant, not in a serialized MonoBehaviour field, not as a literal in a method call.

A tunable is any number, string, or flag a designer might want to change: speeds, costs, timers, recipes, spawn rates, colors, labels, curve shapes, probabilities.

```csharp
// ❌ BAD — the value is trapped in code
producer.duration = 30f;
if (station.IsComplete) gold += 10;

// ✅ GOOD — sourced from ScriptableObjects
producer.duration = recipe.Duration;
if (station.IsComplete) gold += order.Reward;
```

The only literals allowed are structural: array indices, `0`/`1` identities, loop bounds derived from data.

If you need a value and there's no home for it in an SO, **add the field to the appropriate SO first**, then read it. Never inline with a "TODO: move to config."

**Why SOs and not JSON:** the inspector is the tuning UI, for free. No custom editor screen, no serialization layer, no write endpoint. This is a real advantage — don't give it away by putting values in code.

### 2. Event-driven — no tight coupling

Systems communicate through a central event bus. A system may not hold a reference to, or call a method on, another system.

```csharp
// ❌ BAD — Combat now knows Audio and UI exist forever
audioSystem.Play("hit");
uiSystem.FlashHealthBar();

// ✅ GOOD — announce what happened and move on
bus.Publish(new PlayerDamaged(amount, remainingHp));
```

Emitters describe **what happened**, never what should happen in response. `JobCompleted` — not `PlaySound`. Listeners decide for themselves whether they care.

The bus is **plain C#** and lives in the Core (below), so the core can emit without touching Unity.

### 3. The Core boundary — the rule that keeps the rest honest

**`Core/` must never `using UnityEngine`.** This is the invariant, and it's mechanically checkable.

The economy — recipes, resources, costs, timers, the effect system, order pricing — is pure C# with no Unity dependencies. It can run headless, in a test, or in a sim, with no editor and no scene.

- **Core** owns state and rules. It does not know Unity exists.
- **Systems** are MonoBehaviours that drive the core: they pump `Tick(dt)`, translate input intents into core calls, and republish core events.
- **View** renders from state and captures input. It holds meshes, tweens, cameras — nothing a designer would tune, nothing another system needs to know about.

Unity tempts you to put game logic in `Update()`. Don't. A MonoBehaviour's `Update` should read as: *sync the view to core state*. If it contains a rule, that rule belongs in Core.

If a core type needs `Vector3`, it wants a plain `(int x, int y)` grid coordinate instead. If it genuinely needs Unity, the logic is in the wrong layer.

---

## Layout

```
Assets/
  Core/          Pure C#. No UnityEngine. Economy, effects, state, the bus.
    Model/         Plain state objects.
    Rules/         Recipes, costs, effect resolution, order pricing.
    Events/        Event types + the bus.
  Data/          ScriptableObject definitions + instances. The designer surface.
  Systems/       MonoBehaviours. Drive the core, republish its events.
  View/          MonoBehaviours. Render + input capture only.
  Scenes/
  Tests/         EditMode tests. Core only.
```

---

## Data loading

Validate SO content **once, at boot**, before anything runs. Every required reference assigned, every number in range. On failure, **throw immediately** with the asset name and field in the message. Never default-fill a missing value — a silent fallback turns a blank inspector field into a mysterious bug an hour later.

Past that boundary, assume data is well-formed. No defensive checks downstream.

---

## Speed rules

These are the deliberate relaxations. Use them.

- **No tests** outside the Core economy. Verify by playing.
- **No abstraction until the third occurrence.** Three similar lines beat a premature base class. Copy-paste is fine; a wrong abstraction is not.
- **One generic Producer component.** Buildings and fields are data, not subclasses. If you're writing `BakeryBehaviour : ProducerBehaviour`, stop — the difference belongs in an SO.
- **No error handling for impossible states.** Guard the data boundary. Nothing else.
- **No planning ceremony.** For a self-contained change, just build it. Surface a plan only when a decision is genuinely architectural — the event contract, the SO schema, or the layer boundaries.
- **Don't gold-plate.** Ugly-but-working beats elegant-and-pending. Primitives and untextured meshes are correct until proven otherwise.
- **Build continuously.** Don't stop mid-task to check in. Stop when it's playable, or when genuinely blocked.

Speed rules never override the three architecture rules. "It's just a prototype" is not a reason to hardcode a number or reach across systems — those are the two things that make a prototype expensive to iterate on, which is the entire point of moving fast.

---

## Milestones

Work is broken into **playable milestones**. A milestone is done when you can press Play and see the new thing work — not when it compiles.

Each completed milestone gets its own commit, so any milestone can be rewound if it doesn't feel right in play. Commit at the milestone boundary and nowhere else.

Get a milestone playable, then **stop for the user to play it** before moving on.

---

## Errors

Fail loud. There is no user to protect from a stack trace — you *want* the stack trace.

```csharp
// ❌ BAD — the bug is now invisible
var station = stations.Find(s => s.Id == id);
if (station == null) return;

// ✅ GOOD — the bug announces itself where it happens
var station = stations.Find(s => s.Id == id);
if (station == null) throw new InvalidOperationException($"No station with id {id}");
```

Never `catch` and return null. Never default a missing SO value. Never suppress a warning you don't understand.

---

## Notification

Play `afplay /System/Library/Sounds/Glass.aiff` when a milestone is playable, when a task is done, or when waiting on user input. Not mid-task.

---

## Docs

- `docs/VoidDay-Spec.md` — the game spec. Source of truth for design.
- `docs/decisions/` — the original pitch and three Q&A rounds. The spec cites these (R1 #62, R2 #9, etc.); they're the record of *why*.

**The spec was written for a 2D Phaser build.** The design is engine-agnostic and carries over intact — core loop, effect system, economy, VoidPets, UI. What does **not** carry: §2 (platform), §3.1's TypeScript syntax, §14's JSON file inventory (now ScriptableObjects), §12.6's "colored rects" placeholder policy (now primitives), and §16's Vite-specific deferral of the tuning screen — **the inspector solves that one for free.**

<!-- ASSET BOT START -->
# Asset Bot

Game asset generation with consistency and multi-format support. You interact with Asset Bot through its CLI.

## Quick Start

```bash
asset-bot status --json              # Project status
asset-bot generate image --help      # See generation flags
asset-bot assets list --json         # List all assets
```

All commands accept `--json` for structured output and `--project <path>` to override project detection. Project path is auto-detected by walking up from the current directory to find `.asset-bot/`.

## Credentials

API keys are read from `.env` in the project root. See `.env.example` for the full list.

## Critical Rules

1. **Literal prompts only.** Image/video/3D models interpret text literally. Never use metaphors or figurative language. Write "knight standing with sword raised above head", not "warrior channeling inner strength".

2. **Style comes from reference images, never from text.** When style refs exist, the text prompt describes only the subject. Refs carry the aesthetic. See `.claude/skills/pipeline/references/CONSISTENT-PIPELINE-REFERENCE.md` for the full policy.

## Before Using Any Command

Read the relevant skill first. Each one documents workflows, constraints, and examples:

```
.claude/skills/pipeline/               — End-to-end asset pipeline orchestration
.claude/skills/generate-image/         — 2D image generation
.claude/skills/generate-pixel-art/     — Pixel art, sprites, tilesets
.claude/skills/generate-3d/            — 3D model pipeline
.claude/skills/generate-from-template/ — Template-based generation
.claude/skills/generate-audio/         — SFX, music, voice
.claude/skills/generate-multiview/     — Multi-angle views
.claude/skills/generate-scene/         — 3D scenes / environments
.claude/skills/manage-assets/          — Asset CRUD, templates, project status
.claude/skills/ui-kit/                 — UI panel/button/icon sheets
.claude/skills/marketing-art/          — Store listings, feature graphics
.claude/skills/rig-animate/            — 3D rigging and animation
.claude/skills/sync-assets/            — Import/export to game projects
```

For exact command flags and parameters, see `references/CLI-REFERENCE.md`.

Prompt style guides and API references live in each skill folder under `.claude/skills/*/references/`.

## Extensions

Asset Bot supports project-local extensions under `.asset-bot/extensions/`. Extensions let you add new generation backends or workbench tools without editing Asset Bot itself.

```bash
asset-bot extension list              # List discovered extensions
asset-bot extension create <id> --kind generation-adapter   # Scaffold a new adapter
asset-bot extension create <id> --kind workbench-plugin     # Scaffold a new plugin
asset-bot extension enable <id>       # Enable an extension
asset-bot extension validate <id>     # Validate an extension
asset-bot extension docs              # Regenerate extension reference files
```

Extensions are `.mjs`-based with no build step required. Use `asset-bot extension ...` commands to manage extension manifests instead of editing `extension.json` by hand. See `.asset-bot/extensions/EXTENSIONS-REFERENCE.md` for details on installed extensions.
<!-- ASSET BOT END -->
