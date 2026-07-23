# VoidDay

A small, playable recreation of Hay Day's core experience, built in Unity for the Voidpet Game Developer Challenge.

**[Play the WebGL build](BUILD_LINK)** | **[Watch the gameplay video](VIDEO_LINK)** | **[Read the AI session log](docs/ai-transcript/README.md)**

> Replace `BUILD_LINK` and `VIDEO_LINK` above before submission. The AI session log link already resolves.

## Overview

VoidDay is a top-down farming/production-chain game for portrait mobile. You build stations that convert resources into other resources on timers, then sell the results to an Order Board for cash and XP — cash buys more stations and upgrades, XP levels the farm and unlocks new station types. The central tension is Hay Day's: a station **blocks** on its uncollected output, so every finished job demands a tap, and progression is the constant push-pull between adding throughput and staying on top of it.

## What I Built

- **The full production loop** — tap a station, queue jobs from its recipes, watch real-time timers, collect blocked output. Eight station types (Field, Henhouse, Bakery, Creamery, Pasture, Workshop, Silo, Order Board) chaining ten resources from raw crops up to multi-ingredient goods.
- **An Order Board economy** — procedurally generated orders you fulfill for cash and XP, with refilling slots.
- **Build, upgrade, and a unified effect system** — place and upgrade stations; upgrades, and everything else that modifies the game, resolve through one shared Effect schema so a new effect type becomes available to every system at once.
- **Levels, unlocks, and per-resource storage** — a Silo whose capacity is upgraded one resource at a time, and a level curve that gates new stations and caps.
- **A premium "gems" currency** whose single sink is skipping any running timer early — jobs, construction, and order refills, both from the HUD and by tapping the timer in the world.
- **Collection juice** — earning something throws icon particles from the point of action to its HUD counter, and the counter only ticks up as each particle lands.
- **Sound** and a data-driven placeholder-to-final art path, so real 3D station meshes drop in over primitives with no code change.

## How to Play

Touch and drag only — no keyboard.

- **Pan / zoom** the farm by dragging and pinching (mouse-drag and scroll on desktop).
- **Tap a station** to open its panel, then queue a job from one of its recipes. Jobs run on a timer.
- **Tap a finished job** to collect it. A station won't start its next job until you clear the finished one.
- **Tap the Order Board** to fulfill orders with resources you've produced, for cash and XP.
- **Spend cash** to build and upgrade stations and expand storage; **spend gems** to skip a timer you don't want to wait on.

The objective is the Hay Day objective: keep the production chain flowing, fill orders, and level up the farm.

## Scope and Product Decisions

I began by playing Hay Day to identify its essential systems, interaction patterns, and progression loop. I then selected a scope intended to feel like a coherent miniature game rather than a collection of disconnected features.

I treated the **production-chain economy and the moment-to-moment collection loop as the essential core** — the part of Hay Day you actually spend time doing — and built it to real depth: the blocking-on-uncollected-output tension, chained recipes, the order economy, upgrades, levels, and storage pressure all interact as one system.

I deliberately **cut the social and collectible layers.** The spec included VoidPets (collectable familiars that auto-collect and form relationships) and world events; I left those out because they sit *on top of* a working economy rather than being the thing that makes the loop feel good, and a shallow version of all of it would have read as a pile of half-features. I would rather ship a small game that hangs together than a large one that doesn't. The Workshop/universal-upgrade milestone was also pared to just the part a later milestone depended on, once playtesting showed the full version wasn't earning its place yet.

The two systems I *added* beyond the original spec — the gems skip-timer currency and the collection particles — came from playing the build and feeling what it was missing, not from the plan.

## Development Process

### Reference and Specification

After establishing the target scope, I captured the concept in a rough Markdown specification. I then iterated on the specification with Claude to identify missing requirements, resolve ambiguities, and convert the design into an actionable implementation plan.

### Adapting My AI Workflow for Prototyping

My existing AI-assisted Unity workflow was optimized for careful, production-oriented implementation. For this project, I adapted it for faster prototyping by adding a `--prototype` mode to several existing workflows and creating a lightweight preproduction pipeline.

Once the specification was stable, I ran three preproduction workstreams in parallel:

- Converted the specification and visual direction into an asset inventory, then produced 2D and 3D art through my asset-generation pipeline (image generation for 2D, Meshy for 3D).
- Converted the specification into concrete UI requirements, then used the Figma MCP to produce and approve interface mockups before any Unity UI work began.
- Decomposed the specification into milestone documents with the relevant UI and asset references attached to each milestone.

### Implementation and Human-Guided Iteration

I implemented the selected milestones in Unity through MCP, handling them individually so that I could playtest and evaluate the result between each stage.

I used my existing debugging and feature-design workflows to correct weak generated output, improve interactions, and refine the game. I skipped or reordered milestones when the playable result showed that other work would have greater impact. Several features, including the collection particles and the gems skip-timer currency, emerged during this process rather than from the original specification.

A concrete record of where I redirected the agent — including the point where I found the scene was being built entirely in code with no prefabs and forced a full architectural correction — is in the [AI session log](docs/ai-transcript/README.md).

## Economy Simulation and Balancing

To make progression tuning faster, I built a separate headless economy simulator that integrates with the Unity project. It reuses the game's own pure-C# economy core (which carries no Unity dependency) and supports:

- Simulating progression without repeatedly operating the Unity client
- Editing the economy from one interface
- Saving and comparing named economy configurations
- Writing selected configurations directly into Unity `ScriptableObject` assets
- Giving Claude explicit balance goals and allowing it to iterate against simulation results

The tool reads the Unity project but nothing under `Assets/` knows the tool exists — a strict one-way dependency. This separated economy iteration from moment-to-moment game implementation and made both manual and agent-assisted balancing faster.

<details>
<summary><strong>AI Workflow Details</strong></summary>

| Workflow | Purpose |
| --- | --- |
| [`style_guide`](docs/workflow/skills/style_guide/SKILL.md) | Establishes the game's visual direction through an interactive design process |
| [`ui_inventory`](docs/workflow/skills/ui_inventory/SKILL.md) | Converts the specification and style guide into concrete UI requirements |
| [`asset_list`](docs/workflow/skills/asset_list/SKILL.md) | Produces an inventory of required 2D and 3D assets |
| [`plan_milestones`](docs/workflow/skills/plan_milestones/SKILL.md) | Breaks the specification into milestone documents with relevant UI and asset references |
| [`preproduction`](docs/workflow/skills/preproduction/SKILL.md) | Runs the complete style, asset, UI, and milestone-planning sequence |
| [`implement_milestone`](docs/workflow/skills/implement_milestone/SKILL.md) | Implements a single Unity milestone from its specification and references |
| [`implement_all_milestones`](docs/workflow/skills/implement_all_milestones/SKILL.md) | Implements milestones sequentially while maintaining assumption and technical-debt logs across isolated contexts |

Although I created both single-milestone and one-shot implementation paths, I used the single-milestone workflow for the final build. This preserved more control over playtesting, prioritization, and iteration. The skills themselves, and an explanation of why each exists, are in [`docs/workflow/`](docs/workflow/README.md).

</details>

## Key Tradeoffs

- **Cut VoidPets and world events to deepen the core loop.** The collectible/social layer is what a Hay Day clone is tempted to lead with; I judged that a solid economy was the thing worth getting right first, and a shallow version of everything would have felt like disconnected systems.
- **Rebuilt the project's foundation mid-stream.** The agent's initial approach generated the entire scene and UI hierarchy in code at runtime, with no prefabs. It ran, but it was structurally wrong for Unity and would have made every later art and layout task painful. I stopped and re-established authored scenes/prefabs and a data-driven architecture as a hard rule before continuing.
- **Prioritized game-feel over feature breadth late in the build.** The last additions were juice (collection particles) and a convenience currency (gem skips) rather than new systems, because playtesting said the loop needed to *feel* better more than it needed to be bigger.

## Known Issues

- Balance is functional but not deeply tuned; the economy simulator exists precisely to keep iterating on it.
- The game targets a portrait mobile aspect; on a desktop browser it renders in that aspect rather than filling the window.
- No save/load or offline progression — the build is scoped to a single play session.

## What I Would Build Next

- **VoidPets** — the hatch/assign/auto-collect layer, which is the designed relief valve for the collection friction, while adding narrative/collection elements.
- **Deeper economy tuning** using the simulator to smooth progression pacing across levels.

## Running the Project Locally

1. **Unity `6000.3.7f1`** (URP / Universal 3D), with the new Input System package.
2. Clone the repository and open the project folder in Unity Hub with that editor version.
3. Open **`Assets/Scenes/Farm.unity`** and press Play.
4. No additional setup is required to run in the editor; the WebGL build target is used for the shipped build.

## Build and Shipping

The game was built for WebGL and published on Unity Play.
