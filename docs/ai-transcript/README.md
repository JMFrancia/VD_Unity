# AI Session Log

This is the record of how VoidDay was built with coding agents — how the work was directed, and what I did when the generated result wasn't good enough.

**On curation.** This is a curated log, not a raw dump. The session index and the full prompt log below are generated directly from local Claude Code history by [`tools/transcript/build_transcript.py`](../../tools/transcript/build_transcript.py) and can be regenerated at any time. Every prompt is verbatim except where marked `[... redacted]` — those are personal asides, a contact address, one access-bearing key, and references to separate proprietary tooling, each itemized with a reason in [`REDACTIONS.md`](../../tools/transcript/REDACTIONS.md). No technical content was altered, softened, or added after the fact. Duplicate forked sessions (from resuming or rewinding a conversation) are collapsed so the same exchange doesn't appear twice, and the first day — repo setup and a build spec written under a methodology I abandoned — is omitted as dead work.

## By the numbers

| | |
|---|---|
| Sessions | 54 |
| Prompts I typed | 360 |
| Elapsed | 7 days (Jul 16–23) |
| Commits | 96, one per milestone boundary |
| Milestone docs | 34, across 5 plans |
| Pure-C# core | 40 files, zero `using UnityEngine` |

## How I ran it

The volume above is not the point — most of those prompts are ordinary iteration. What made the difference was the scaffolding around them:

- **A project constitution.** [`CLAUDE.md`](../../CLAUDE.md) at the repo root defines four non-negotiable architecture rules (data-driven, event-driven, a pure-C# core boundary, Unity-native authoring) and explicitly supersedes my global instructions, which are tuned for slow production work. It also lists what to *relax* for speed. Agents drift; a written constitution is what you point at when they do.
- **A custom skill harness.** The stages of the project — style guide, asset list, UI inventory, milestone decomposition, implementation, doc audits — each run as a reusable skill I wrote, so every stage emits an artifact the next one consumes. The skills are in [`docs/workflow/`](../workflow/) with an explanation of why each exists.
- **Milestones over phases.** Work was decomposed into playable milestones — a milestone is done when you can press Play and see the new thing, not when it compiles. Each gets its own commit so it can be rewound if it doesn't feel right in play.
- **A running log per plan.** Each `milestones/<plan>/LOG.md` carries context between sessions, so a fresh agent re-reads the plan instead of inheriting a polluted context window.
- **Editor-native authoring.** Scenes, prefabs, materials and serialized references are authored through the Unity MCP exactly as a human would author them — not generated in code at runtime. Turning point #2 below is why.
- **The core runs headless.** The economy is pure C# with no Unity dependency, which is what made the balance tool in turning point #6 possible.

## Six turning points

The moments that shaped the project — where I set the direction, or where the generated result wasn't good enough and had to be redirected. Everything else in the log is iteration around these.

### 1. Designing the agent workflow itself

*Jul 22.* Two plans were ready to build and running them by hand would have taken the rest of the week. Rather than prompt through them, I specified an orchestrator — and the part I cared about was the failure mode, not the happy path:

> An orchestartor launching agents for each with a running log … so that the whole thing can be done synchronously without killing us on context? Only halting if we run into an issue? I guess my concern there is what if an agent runs into an issue, stops to ask about it from orchestrator, then it can't be restarted from where it left off. I guess perhaps we could have a sub-log for each milestone that gets fed into the main plan log once that milestone is complete, that way a milestone could be split between multiple fresh agents if needed. Thoughts?

Three more constraints followed, each aimed at a specific hazard:

> That it starts by looking at what will be done, and makes a rough estimate of how long it will take / how many tokens it will use / when and where it may stop user for feedback / any questions that need to be asked upfront.
>
> We should commit after each phase, but ONLY commit work RELATED to that phase (IE what was touched). That way can rewind if need be. If some cross-contamination by another agent running, how do you think we should proceed there? I don't think we can commmit just PART of a file.
>
> I also have a collection-particles spec. I was hoping to run implement_all_milestones on that one simulatensously since they touch very different parts of the project.

Resumability, cost visibility before committing to a run, commits scoped so any milestone can be rewound, and parallelism only where two plans don't touch the same files. That became [`implement_all_milestones`](../workflow/skills/implement_all_milestones/SKILL.md).

### 2. The scene was empty and nothing was a prefab

*Jul 18.* I opened the Unity project properly for the first time and found the agent had been building the entire scene hierarchy programmatically at runtime — no prefabs, an empty scene, a UI factory constructing canvases in code. It compiled. It ran. It was structurally wrong, and it would have made every subsequent art and layout task painful.

> We need to completely re-assess how we are doing this project. It must follow BEST UNITY PRACTICES. When I said I wanted this game to be data-driven, I meant like no hard-coded variables IN OUR LOGIC, emphasis on using scriptable objects, becuase that is BEST UNITY PRACTICE. This is... something else, I dont' even know what. We cannot go further with the project this way.

This became architecture rule #4 in `CLAUDE.md`, written to be unambiguous for every future session: *scenes and prefabs are authored, not generated.* The prototype was rebuilt on it.

### 3. Stopping the work before it started

*Jul 20.* The cheapest correction is the one that lands before any code is written. Noticing the agent was about to start on the UI system, I checked first:

> Were you just about to start work on the UI system? IE you haven't touched it yet? If so, just occurred to me, if you're going to futz with our UI I'd prefer we bring in the Figma MCP and mock it up, get my approval, then pipe that over rather than iterate over raw Claude + Unity MCP.

Iterating on layout through an agent and the Unity editor is slow and hard to judge. Mocking it up first, approving it, then implementing against the approved design is faster and produces a better result — and it cost nothing because the work hadn't begun.

### 4. Specifying a feel, precisely

*Jul 20.* I wanted crops to visibly grow out of the field. "Make the crops grow" would have produced something arbitrary, so I specified the mechanic exactly:

> At 0%, they are instantiated just below the field on the Y axis such that the top of the image is just below the surface of the field. As the field grows, they slowly translate up along the Y axis. When fully ready to pick at 100%, the bottom of the crop graphic is touching the surface of the field. So they just slowly "move" upward, giving the appearance of growing. For this to work, the crop graphic must be relatively tall (large Y:X ratio). **Do you understand? Please re-state it for me based on your understanding.**

Asking for the plan restated before any code gets written catches misunderstandings while they're still free. I used it throughout.

### 5. Art direction on specifics

*Jul 17–18.* Generated station models came back looking plausible and weren't usable. The most useful note was about the *pipeline* rather than any single asset:

> I love th direction for all of them stylistically, but wouldn't it be better to make concept art that is low-res, matching the art style the game is going for? So taht we don't then have to rely on Gliffy to downgrade the appearance in a way we like?

Then the specifics, which are the only kind of art note that actually changes an output:

> Henhouse should NOT have hens. None of these should have any living thing. […] Workshop and creamery have identical round little door, which I don't like. […] Field should NOT have wooden borders or any plants.

Later the agent ran a mesh-reduction pass and reported the result as "indistinguishable" from the original. It had torn the roof off the bakery. That established a standing rule that no asset counts as done until I can actually look at it, and the destructive remesh step was dropped.

### 6. Questioning an abstraction — and being repaid for it

*Jul 20.* Two days in, I stopped to interrogate the pure-C# economy core, an architecture I had asked for myself and which was by then load-bearing:

> Why do we need a pure-c# economy core for this project? I know it's in there so you assumed it was something we decided earlier, but I don't think I understood how complicated it would make things. […] I've been trying really hard to find the right balance between "this is a prototype, not production code, let's chill on super-strict architecture" and "I want to be able to iterate on this so please don't make spaghetti code that will fall apart the minute I add on or take out a feature, causing cascading errors".

I kept the core. *Jul 22*, it paid for itself — I wanted to tune the economy without playing the game for hours, so I specified a side app driving the same economy code with a simulated player:

> Note: if it makes more sense to make this app INSIDE Unity as an editor tool I'm open to it but my instinct is that that would be overcomplicated. **We separated out core economy logic from normal Unity logic for a reason, and this is it.**
>
> I feel like the Unity project should be agnostic to the seperate tool, but not vice-versa. […] One more thing: I want an agent to be able to use this tool to try and balance the game toward a specific goal.

The boundary questioned on Tuesday is what let the economy run headless on Thursday, with the dependency pointed deliberately one way: the game never knows the tool exists.

## Where to look

- [`sessions.md`](sessions.md) — every session, dated and titled. Skimmable in a few minutes.
- [`prompts/`](prompts/) — the full prompt log, one file per day.
- [`../workflow/`](../workflow/) — the custom skills that drove these sessions, and why each exists.
- [`../../milestones/`](../../milestones/) — the plans these sessions executed, each with a running `LOG.md`.
- [`../../CLAUDE.md`](../../CLAUDE.md) — the project constitution the agents worked under.
