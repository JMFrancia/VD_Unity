# Decision Round 4 — The Balance Tool (2026-07-22)

Record of *why* the balance tool is shaped the way it is. The spec (`docs/BalanceTool-Spec.md`) says what it
is; this says what else was on the table and why it lost.

This round **reverses a decision from the Unity spec.** §9 currently reads:

> **[SO]** The pitch's separate balance tool that "makes direct changes to that same JSON" is superseded —
> **the Unity inspector is the tuning UI** (CLAUDE.md). Editing an SO in the inspector *is* the balance edit;
> no separate tool, no write endpoint. See §16.

That held while the only balance question was "does this feel right for the next 90 seconds". It stops
holding once the questions are "how long is level 6", "when does the player hit the silo wall", and "did that
recipe change help or hurt" — none of which an inspector can answer. The inspector remains the authoring
surface and the runtime source of truth; the tool is an offline analysis and bulk-edit surface that round-
trips through it. §9 and §16 are amended in Milestone 07.

---

## 1. Reuse the Core, don't reimplement it

**Decided:** the simulator compiles `Assets/Core/**/*.cs` and runs the real `JobSystem`, `OrderBoard`,
`OrderGeneration`, `OrderPricing`, `ValueResolver`, `EffectResolver`, `Progression` and `LevelCurve`.

**Rejected:** a TypeScript web app reimplementing the economy. It would have been the fastest path to a good
UI, and it would have started truthful and drifted within a week — at which point the simulator would be
confidently wrong, which is worse than having no simulator. Every reported number would need re-verification
against the game by hand, defeating the purpose.

**Why it's possible at all:** `VoidDay.Core.asmdef` sets `noEngineReferences: true`, so the rules compile in a
plain .NET 9 project unchanged. This is the payoff CLAUDE.md rule 3 was written to buy, and the first time it
has been collected.

**Also rejected:** a Unity `EditorWindow`. It has real advantages — direct SO access, no round trip, Core
already compiled. But rich tables and charts are painful in UI Toolkit, every iteration costs a domain
reload, and long simulation runs would block the editor. The user's instinct that this was overcomplicated
was correct, for reasons beyond the ones they gave.

## 2. The dependency runs one way

**Decided:** the tool may read the Unity project; the Unity project may not know the tool exists. No asmdef,
no editor menu item, no shared schema types, no changes to shipped game code. Delete `tools/` and the game
is untouched.

**What this cost.** The first draft of the plan refactored `GameBoot.Start()` into a shared `CoreFactory` in
`Core/`, so the game and the simulator would construct the economy through one code path and could never
drift. That was the single most elegant idea in the draft, and it was rejected because it contaminates the
game with tooling concerns and rewrites a working composition root to serve something external.

**What replaced it.** The tool's `CoreHarness` mirrors `GameBoot`'s wiring by hand, guarded by a **staleness
canary** — a test hashing `GameBoot.cs` that fails when it changes. Silent drift becomes a loud failure. The
blast radius is small: only ~40 lines of *wiring* are mirrored, because the rules themselves are compiled
directly and cannot drift.

**Consequence:** the tool reads and writes `.asset` YAML itself rather than going through a Unity-side
importer. Tractable because asset bodies are plain, consistently-indented YAML — Unity's non-standard parts
(`%TAG`, `!u!` tags) live only in document headers.

## 3. No offline, no sessions

**Decided:** the simulator models one continuous engaged play session.

**Why:** the game has no leave-and-return. An earlier draft modelled idle-game sessions (N logins/day of M
minutes, timers running while away) on the assumption that was how it would be played. It isn't, yet.
Simulating it would have produced authoritative-looking numbers about a game that doesn't exist.

**Consequence:** level durations mean *engaged play time*, which is the more useful balancing number anyway.
Three time buckets collapse to two (acting / waiting), and pressure accrues continuously because the player
is always present. If leave-and-return ships later, the session model comes back — but as a change to a
working simulator, not as speculative generality now.

## 4. Bottleneck-seeking player, not a priority list

**Decided:** the simulated player accumulates *seconds lost* per stall cause and buys the affordable remedy
for whichever is worst.

**Rejected:** a hand-ordered spending priority list the designer reorders. It models an archetype rather than
a player, and it answers "what happens if players buy fields before upgrades" only by asking the designer to
guess the ordering they were trying to discover.

**The consequence that justifies it:** the pressure ledger does **three jobs with one mechanism** — it is the
player's decision input, the bottleneck report the user asked for, and the input to `suggest` in the agent
interface. None of the three needed separate machinery.

**Trap noted:** a bottleneck-seeker plays near-optimally, so its level times are a floor rather than an
average. The `optimality` dial (0–1) bridges that with one knob driving four mechanisms, rather than a second
"casual player" code path.

## 5. Balance the game, not the player

**Decided:** `SimProfile` (player behaviour) is read-only to `patch`; only `BalanceConfig` (the game) can be
edited during optimization.

**Why:** without this, the cheapest way for an agent to lower any loss is to raise `optimality` — making the
*simulated player* better rather than the *game* better. It would report success having changed nothing
about the game. This is the sharpest reward-hacking failure mode available here and it is closed
structurally, not by instruction.

## 6. The agent lives in the harness, not in the app

**Decided:** the balancing workflow is a skill (`.claude/skills/balance_game/`) driving a `--json` CLI. The
tool ships session directories and a journal; the app shows a live view of a running session.

**Rejected:** embedding an LLM in the .NET app with a chat panel. Self-contained, but it means building a
transcript UI, key management and a tool-calling loop to arrive at a worse version of the harness already
open. **Also deferred:** exposing the verbs as an MCP server. Typed tools reduce agent error, but it is a
second interface to keep in sync with the CLI for a modest gain inside Claude Code. The CLI is the contract.

**The durability point:** the end-of-session write-up is generated from `journal.jsonl`, never narrated from
memory. A real balancing run spans dozens of iterations and will exhaust a context window; an agent
summarising from a compacted context produces a plausible, tidier story than what happened, with no signal
it is doing so.

## 7. Scope: only what the game actually implements

**Decided:** the tool covers what shipped Core reads. Global/universal upgrades, VoidPets, relationships and
world events are out.

**Global / universal upgrades were cut from the game** — a deliberate design decision by the user, not an
unbuilt milestone. The game's LOG goes `## Milestone 05` → `## Milestone 07` because M6 was dropped, and the
code matches: `Station_Workshop.upgrades` is `[]`, no asset authors a `Global*` / `OrderPayout` /
`OrderSlots` / `BuildCost` effect, and `ValueResolver` passes `OrderPayout` and `BuildCost` through
untouched. The only effect types authored anywhere are `StationSpeed`, `StationYield`, `StationQueueDepth`
and `StorageCap`.

**Consequence for the simulator:** `Throughput` pressure's only remedy is a per-station speed tier. That is
the intended design — the tool must **not** surface it as a missing remedy or a Workshop-shaped hole. If
global upgrades are ever reinstated, the effect vocabulary and the remedy catalog widen together.

*(Housekeeping: the game's `milestones/VoidDay-Spec-unity/00-summary.md` still lists M6 in its milestone
table, which will read as unfinished work to a cold reader. Worth striking there.)*

## Open items

- Should `versions/*.json` and `sessions/` be git-tracked? Planned yes, so balance history is reviewable.
  Revisit if they churn noisily.
- Should overnight/blocked-at-login storage pressure be credited at a discount? Moot while there is no
  offline play; revisit if leave-and-return ships.
- Schema drift: adding a field to an SO without adding it to `BalanceConfig` means the tool silently can't
  tune it, and the round-trip test won't catch it (both sides omit it symmetrically). No mechanical guard
  found; a periodic manual audit is the honest backstop.
