# The Workflow Harness

VoidDay wasn't built by prompting an agent freehand. It was built by a set of custom skills — reusable procedures I wrote and refined so that each stage of the project produced a durable artifact the next stage could consume, and so that any stage could be re-run in a fresh context without carrying a polluted context window forward.

These are copies of the skills used on this project, included as evidence of how the work was directed. They live in `~/.claude/skills/` on my machine; the copies here are read-only documentation so they don't shadow the live versions.

## The pipeline

Spec in, playable milestones out. Each stage writes a document; the next stage reads it.

```
docs/VoidDay-Spec-unity.md          the game spec — source of truth
        │
        ├── style_guide      ──▶  docs/StyleGuide.md
        ├── asset_list       ──▶  docs/assets/
        ├── ui_inventory     ──▶  docs/UI-Inventory.md
        │
        └── plan_milestones  ──▶  milestones/<spec>/00-summary.md + one doc per milestone
                    │
                    ├── implement_milestone       one milestone, then stop for me to play it
                    └── implement_all_milestones  the whole plan back-to-back, unattended
                                │
                                └──▶  milestones/<spec>/LOG.md   (context handoff)
```

## The skills

### Planning

| Skill | What it does |
|---|---|
| [`design_feature`](skills/design_feature/SKILL.md) | Designs a feature from scratch — confirms scope, explores the codebase, resolves edge cases in batches, writes a phased plan. Passing `-p` switches it to prototype mode, which emits playable milestones instead of phases. Used to spec the balance tool. |
| [`plan_milestones`](skills/plan_milestones/SKILL.md) | Decomposes a finished spec into ordered, playable milestones — smallest playable artifact first, then layers. Audits its own coverage with independent cold agents before writing. |
| [`style_guide`](skills/style_guide/SKILL.md) | Establishes visual and audio direction, reading the spec first and only asking what the spec doesn't already answer. |
| [`asset_list`](skills/asset_list/SKILL.md) | Enumerates every asset the game needs, one doc per asset type. |
| [`ui_inventory`](skills/ui_inventory/SKILL.md) | Inventories every UI surface the spec implies, in enough structural detail to feed a mockup tool. |

### Implementation

| Skill | What it does |
|---|---|
| [`implement_milestone`](skills/implement_milestone/SKILL.md) | Builds exactly one milestone, verifies it runs, stops for me to play it, commits, appends to the log. The rapid-prototyping path — no TDD, no test gate. |
| [`implement_all_milestones`](skills/implement_all_milestones/SKILL.md) | Runs a whole plan unattended, one fresh subagent per milestone. Opens with a pre-flight briefing and halts the run on any architectural question rather than guessing. |
| [`implement_phase`](skills/implement_phase/SKILL.md) | The production-grade counterpart: one phase, TDD, user verification, commit, plan back-sync. |

### Keeping documents true

| Skill | What it does |
|---|---|
| [`audit_doc`](skills/audit_doc/SKILL.md) | Audits one architecture doc against current source, looping cold audit → repair → re-audit until nothing consequential remains. |
| [`audit_project`](skills/audit_project/SKILL.md) | The same check fanned out across every architecture doc in the project in parallel. |

## Why it's built this way

Three problems drove the design, and each maps to a specific mechanism:

**Context rot.** A long session accumulates dead ends and superseded decisions, and output quality falls off. So each milestone runs in a fresh context and reads the plan rather than inheriting the conversation. `milestones/<plan>/LOG.md` is the handoff — the previous milestone writes down what actually got built and how it deviated from plan, and the next one starts there.

**Agents drift from stated rules.** [`CLAUDE.md`](../../CLAUDE.md) is the standing constitution, and it explicitly overrides my global instructions for this project. When a generated result violated it, the fix was usually to sharpen the written rule rather than to correct the one instance — see turning points 1 and 2 in the [session log](../ai-transcript/README.md).

**Plans and docs go stale silently.** A plan written on day 2 describes a codebase that no longer exists by day 5, and nothing announces it. The audit skills use *cold* agents — no memory of writing the doc — because an agent asked to check its own work grades generously. `plan_milestones` runs the same cold-audit step on its own coverage before it writes.

## Caveat

These are working tools from a rapid prototype, not a polished product. They encode my preferences (commit at milestone boundaries, stop for a human to actually play the build, fail loud rather than defensively) and they assume the conventions in `CLAUDE.md`. They're here to show the shape of the harness, not as something to drop into another project unchanged.
