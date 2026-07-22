# VoidDay Balance Tool — Implementation Plan

> A standalone .NET + browser workbench for tuning the VoidDay economy, simulating a player through it, and
> letting an agent balance toward a declared goal.
>
> **The Unity project does not know this tool exists.** The dependency runs one way only.

## Implementation Status
<!-- Canonical phase state — implement_phase reads and updates this ledger. -->
| Phase | State | Commit | Notes |
|---|---|---|---|
| 1 — Asset reader (SO → BalanceConfig) | ⬜ TODO | — | |
| 2 — Asset writer + round trip | ⬜ TODO | — | Round trip closes; tool useful from here |
| 3 — Simulation engine (headless CLI) | ⬜ TODO | — | The big one |
| 4 — Server + settings editor UI | ⬜ TODO | — | The browser app appears |
| 5 — Agent interface (goal, loss, suggest, sweep) | ⬜ TODO | — | |
| 6 — Multi-seed sweep + report UI | ⬜ TODO | — | |
| 7 — A/B overlay + docs | ⬜ TODO | — | |
| 8 — Balancing session workflow + skill | ⬜ TODO | — | The agent-driven loop end to end |

---

## Overview

Balancing VoidDay currently means editing ScriptableObjects in the inspector and pressing Play. That answers
"does this feel right for the next 90 seconds" and nothing else. It cannot answer "how long is level 6",
"when does the player hit the silo wall", or "did that recipe change help or hurt".

The tool provides:

1. **A settings editor** — every economy tunable in one browser page.
2. **A simulator** — a bottleneck-seeking player driven through the real economy across many seeds.
3. **An agent interface** — a declarative goal, a scalar loss, and the primitives to minimize it.
4. **Named versions** — save, load, compare, and write back into the Unity assets.

**The load-bearing constraint:** `VoidDay.Core.asmdef` declares `noEngineReferences: true`, so every economy
rule compiles in a plain .NET 9 project unchanged. The tool compiles those exact source files. A
reimplementation of the rules in another language would drift within a week and the simulator would quietly
start lying — worse than having no simulator at all. This is the payoff the Core boundary was built for
(CLAUDE.md rule 3).

### The agnosticism rule

**The Unity project contains zero evidence this tool exists.** No new asmdef, no editor menu item, no
schema types, no changes to `GameBoot`. Delete `tools/` and the game is untouched.

The tool depends on the Unity project in two ways, both read-only from Unity's perspective:

| Direction | Mechanism |
|---|---|
| Tool → Unity **code** | `<Compile Include="../../Assets/Core/**/*.cs" />` — compiles the real rules |
| Tool → Unity **data** | Parses `Assets/Data/SO/*.asset`, `**/*.meta`, `Assets/Scenes/Farm.unity` |
| Unity → Tool | *(nothing)* |

**The cost, stated plainly.** Without a shared `CoreFactory`, the tool's `CoreHarness` mirrors
`GameBoot.Start()`'s object-graph wiring by hand, and the two can drift. Mitigation is a **staleness canary**
(Phase 3): a test that hashes `GameBoot.cs` and fails with *"GameBoot.cs changed since CoreHarness was last
reconciled"* when it moves. Silent drift becomes a loud failure, and Unity still knows nothing. Note this
applies only to the ~40 lines of *wiring* — the rules themselves are compiled directly and cannot drift.

### Scope

**In:** everything the shipped Core actually reads — resources, recipes, stations, station/silo upgrade
tracks, the level curve and its grants, order generation and pricing, XP, storage, build costs and caps.

**Out:**
- **Global / universal upgrades (M6).** `Station_Workshop.upgrades` is empty, no asset authors a `Global*`,
  `OrderPayout`, `OrderSlots` or `BuildCost` effect, and `ValueResolver` falls through to passthrough for
  `OrderPayout` and `BuildCost`. Nothing to tune, nothing to simulate. The effect-type dropdown offers only
  the six types with teeth: `StationSpeed`, `StationYield`, `StationCost`, `StationQueueDepth`, `XpGain`,
  `StorageCap`.
- **VoidPets (M9), relationships (M10), world events (M11).** Not implemented in the game.
- **Offline / multi-session play.** The game has no leave-and-return, so the sim models one continuous
  engaged session. See below.
- Anything presentational — camera, UI theme, SFX.

### Non-goals

- The tool does not become the runtime source of truth. ScriptableObjects stay authoritative; the tool edits
  them in place. CLAUDE.md rule 1 is preserved.
- The tool does not create resources or station types (those need icons, prefabs, meshes, thumbnails). It
  creates recipes, level rows and upgrade tiers, which need no art.

---

## Architecture

```
   ┌─ UNITY PROJECT (knows nothing about any of this) ──────────────┐
   │  Assets/Core/**/*.cs        Assets/Data/SO/*.asset             │
   │  Assets/Systems/Boot/       Assets/Scenes/Farm.unity           │
   └────────┬──────────────────────────────┬────────────────────────┘
            │ compiled by glob             │ parsed / patched
            ▼                              ▼
   ┌─ tools/VoidDay.Balance ────────────────────────────────────────┐
   │  Schema/    BalanceConfig DTOs, SimProfile, Goal               │
   │  Unity/     AssetReader, AssetWriter, GuidIndex, SceneScanner  │
   │  Sim/       CoreHarness, PlayerAgent, PressureLedger, Metrics  │
   │  Agent/     GoalEvaluator, Suggest, Sweep, Journal, Bounds     │
   │  Api/       minimal API                                        │
   │  Cli/       sim  eval  patch  suggest  sweep  read  write      │
   │  wwwroot/   editor + charts (Preact/htm + Chart.js, vendored)  │
   │  versions/  named configs (git-tracked)                        │
   │  AGENTS.md  the tool documenting itself to its agent           │
   └────────────────────────────────────────────────────────────────┘
```

**CLI-first, UI second.** Every capability is a CLI verb with `--json`; the browser app is a client of the
same API. This is what makes the tool agent-drivable, and it happens to be the right build order too.

---

## Reading and writing Unity assets

The fiddliest, least glamorous part of the tool, and the part most likely to be underestimated.

### GUID index

Scan every `Assets/**/*.meta` for its `guid:` line → `guid → asset path` dictionary. ~30 lines. Every
cross-asset reference in Unity is `{fileID: 11400000, guid: <32 hex>, type: 2}`, so this index is what turns
a reference into a file to open.

### Reading `.asset`

Unity's YAML is not standard-conformant: `%TAG` directives apply only to the first document, so YamlDotNet
throws *"While parsing a node, find undefined tag handle"* on a raw asset file
([aaubry/YamlDotNet#140](https://github.com/aaubry/YamlDotNet/issues/140)). The
[documented workaround](https://github.com/aaubry/YamlDotNet/issues/307) is to preprocess.

Our `.asset` files are **single-document with plain, consistently-indented bodies**, so preprocessing is
five lines: drop `%YAML 1.1`, drop `%TAG !u! …`, rewrite `--- !u!114 &11400000` to `---`. Then YamlDotNet
parses cleanly. Verified against `Upgrade_Field_Speed.asset` (three tiers, nested effect structs) and
`Levels.asset` (nested grant lists).

`Farm.unity` and `.meta` files get **no parser at all** — a GUID line-scan is sufficient and far more robust
than parsing a 16k-line multi-document scene.

### Traversal

`GameConfig.asset` is the root. Follow `stationRoster` → `StationSO` → `recipes[]` + `upgrades[]` → `RecipeSO`
→ resource refs; plus `orderConfig`, `xpConfig`, `levels`, `startingResources`. Everything the game reads is
reachable from the same root `GameBoot` starts from, which is what keeps the config honest.

### Enums

Assets store enums as ints (`type: 0`, `kind: 3`, `op: 1`). **Because the tool compiles Core, it maps them
via the real enum types** — `(EffectType)0 → "StationSpeed"`, `(LevelEntryKind)3 → "QueueDepth"` — rather than
a hand-maintained table that could drift. JSON always carries the string name, so reordering an enum can
never silently reassign a value.

### Writing — surgical, not reserialized

Reserializing with YamlDotNet would reformat entire files and produce enormous, unreviewable git diffs. So
writes are **line-based surgical patches**: resolve a config path to a specific line, replace only the scalar
after the colon, leave every other byte untouched. A one-field change produces a one-line diff.

Structural additions (a new recipe, level row or upgrade tier) insert correctly-indented blocks. A new
`RecipeSO` also needs a `.meta` with a fresh GUID and a reference appended to its owning `StationSO.recipes`.

**Dry-run is the default.** Nothing is written without `--apply`. Git is the undo.

---

## Data Structures

### `BalanceConfig` (tools/VoidDay.Balance/Schema/)

```csharp
public sealed class BalanceConfig
{
    public int SchemaVersion;             // writer refuses a mismatch
    public string Name;
    public GlobalConfig Global;           // grid, refundPercent, startingStorageCapacity,
                                          // startingResources, startingStations
    public XpConfig Xp;                   // perJobCollected, perStationBuilt
    public OrderConfig Orders;            // the ten OrderConfigSO fields
    public List<ResourceConfig> Resources;   // id, displayName, baseValue, sellable, tier
    public List<RecipeConfig> Recipes;       // id, stationType, inputs, outputs, duration
    public List<StationConfig> Stations;     // stationType, buildable, buildCost, cap, unlockLevel,
                                             // queueDepth, w/h, recipeIds, upgradeIds
    public List<UpgradeConfig> Upgrades;     // id, displayName, unlockLevel, tiers[cost, effects]
    public List<LevelConfig> Levels;         // index 0 == level 1
}
```

`Global.StartingStations` is scanned from `Farm.unity`: find each `m_SourcePrefab` GUID, keep those resolving
under `Assets/Prefabs/Stations/`, map prefab → `StationSO` → `stationType`, and count. Currently 1 Field,
1 Silo, 1 Order Board. Scanning rather than hand-typing means it can't go stale when someone places a fourth
station in the scene.

### `SimProfile`

Player behaviour is not game balance and lives in its own file, so swapping an archetype never touches the
economy.

```csharp
public sealed class SimProfile
{
    public string Name;                    // "typical", "perfect"
    public float Optimality;               // 0..1
    public ActionCosts Actions;            // seconds per tap / queue / fulfill / purchase
    public float ReactionLagSeconds;
    public int CashReserve;                // never spend below this
    public RecipePolicy RecipePolicy;      // DemandChain (default) | GreedyValuePerSecond
    public int SeedCount;                  // 30
    public int MaxSimulatedHours;          // 40
    public int StallGuardMinutes;          // 45 — no XP for this long ⇒ abort and report
}
```

No session fields. The player is always present.

### `LevelReport`

```csharp
public sealed class LevelReport
{
    public int Level;
    public double EnteredAt, ExitedAt, DurationSeconds;
    public double ActingSeconds;                  // consumed by taps/queues/fulfills/purchases
    public double WaitingSeconds;                 // the remainder — watching timers
    public int MoneyAtEntry, MoneyAtExit, MoneyEarned, MoneySpent;
    public int OrdersFulfilled, OrdersSkipped, JobsCollected;
    public Dictionary<string, double> Pressure;   // category ⇒ seconds lost
    public List<PurchaseRecord> Purchases;
}
```

---

## The Simulation

### Single continuous session

The game has no leave-and-return, so the sim models one uninterrupted engaged play session. Level durations
mean **engaged play time**, which is the more useful balancing number anyway.

Stepping: 1s granularity while the player has anything to do; when they don't, **jump the clock to the next
event** — `min(next job end, next order refill)`. This is exact, not an approximation, because Core stores
timers as absolute timestamps (spec §13) and `JobSystem.TryStartHead` only runs on collect/queue/cancel. A
30-seed run is a few hundred thousand steps; seeds run in parallel via `Parallel.For`.

### The pressure ledger

The player follows no hand-ordered priority list. The sim accumulates **seconds lost** to each stall cause —
one common unit, so causes are directly comparable — and the player buys the affordable remedy for whichever
is worst. Because the player is always present, pressure accrues continuously.

**The ledger does three jobs at once:** it is the player's decision input, the bottleneck report, and the
input to `suggest` in the agent interface. One mechanism, three requirements.

**Actionable pressures** — the player can buy a fix:

| Category | Accrues each second while… | Remedy |
|---|---|---|
| `Storage` | a completed job can't be collected, silo full (credited **per blocked station**, so three blocked stations hurt 3×) | next Silo `storage.cap` tier |
| `Capacity[type]` | the agent wants to queue at type T but every T instance is at `QueueDepth` | build another T (if unlocked + under cap), else T's queue-depth tier |
| `Throughput` | nothing collectable, nothing queueable, no order fulfillable — pure timer-watching | speed tier on the type with the most run-time this level |
| `Supply[good]` | an order wants good G and no *placed* station can produce it | build the station type whose recipes output G |
| `Yield[type]` | an input is missing whose producer type is **saturated** — all instances built to cap *and* all queues full | yield tier on that producer type |

`Yield` vs `Capacity` is the one subtle split: if you can still add jobs, the fix is more jobs (Capacity); if
you cannot, the fix is more output per job (Yield).

**Diagnostic pressures** — no purchasable remedy, reported only:

| Category | Accrues while… | What it tells you |
|---|---|---|
| `Income` | the top-ranked remedy is identified but unaffordable | money curve too flat for the cost curve |
| `OrderRefill` | every slot is empty and refilling while the player holds sellable goods | `refillSeconds` too slow or `slotCount` too low |
| `Unlock` | every remedy the ledger wants is gated behind a higher `unlockLevel` | the level curve gates progression too hard |

`Unlock` is the direct read-out for *"what unlocks should happen at which level"* — a level where it dominates
is one where the player is ready for something the curve won't give them yet.

### Recipe choice — demand-driven backward chaining

Each time the agent has a free queue slot:

1. Collect the goods every order on the board is asking for.
2. Walk the recipe tree backward from each, subtracting what's in the pool.
3. Take the **deepest unsatisfied** node whose station type is placed with a free slot; ties break by output
   value per second.
4. If nothing in the chain is queueable, keep raw producers busy (fields don't idle).

This is the only policy that surfaces `Supply` pressure, because it goes looking for what orders demand.
`GreedyValuePerSecond` remains available as a comparison mode. The walk memoises visited recipe ids, so an
accidentally cyclic recipe pair errors loudly instead of hanging.

### The optimality dial

A pure bottleneck-seeker plays near-optimally — a *floor* on level times, not an average. One knob in `[0,1]`
drives four mechanisms at once:

| Mechanism | At `1.0` | As it drops |
|---|---|---|
| Remedy pick | `argmax(pressure)` | softmax, temperature `(1 − optimality)`; uniform at 0 |
| Reaction lag | immediate | `ReactionLagSeconds / max(optimality, 0.1)` |
| Action threshold | acts on any pressure | waits until pressure exceeds `(1 − optimality) × 60s` |
| Idle waste | none | wastes `(1 − optimality) × 15%` of elapsed time |

Run at 1.0 for the best case, ~0.65 for a plausible average. Same engine, one number, no second code path.

### Determinism

Every run is a pure function of `(BalanceConfig, SimProfile, seed)`. Two `System.Random` streams derive from
the seed and stay separate — one for `OrderGeneration` (injected exactly as `GameBoot` does), one for the
agent's softmax and jitter — so changing player behaviour never reshuffles the order sequence. Dictionary
iteration order is never allowed to affect results; anywhere the agent scans stations or resources, it sorts
first.

### Termination

Whichever fires first: max level reached, `MaxSimulatedHours` elapsed, or the stall guard. A run that stops
early reports its `StopReason` and partial levels — a config that stalls at level 6 is a *finding*.

---

## The Agent Interface

Everything here also improves the human tool. None of it is agent-only overhead.

### Goal files → a scalar loss

```json
{ "name": "capacity-driven-first-hour",
  "targets": [
    {"metric":"level.durationMinutes", "levels":"1-5",  "max":15,               "weight":2},
    {"metric":"total.minutesToLevel",  "level":10,      "target":180, "tol":30, "weight":3},
    {"metric":"pressure.rank",         "category":"Capacity", "levels":"3-8", "maxRank":1, "weight":2},
    {"metric":"pressure.share",        "category":"Capacity", "levels":"3-8", "min":0.30, "max":0.55, "weight":2},
    {"metric":"pressure.share",        "category":"Storage",  "levels":"all", "max":0.20, "weight":1},
    {"metric":"level.moneyAtExit",     "level":10, "min":500, "max":1500,      "weight":1}
  ]}
```

Four metric families:

| Metric | Expresses |
|---|---|
| `level.durationMinutes` | pacing, per level or range |
| `total.minutesToLevel` | the shape of the whole curve |
| `pressure.share` (**`min` and `max`**) | how much of a bottleneck to apply — not just a cap |
| `pressure.rank` | **which** bottleneck should dominate, and where |
| `level.moneyAtEntry` / `level.moneyAtExit` | purchases stay real decisions |

`pressure.rank` and `pressure.share`-with-`min` are what let you juggle several bottlenecks and target a
*texture of play* — "this stretch should feel like capacity pressure, not storage pressure" — rather than
just a speed. Each target yields a normalized violation × weight; the sum is the loss.

`balance eval --config X --goal G --json` → `{"loss": 4.71, "breakdown": [...]}`. **This is the single
highest-leverage affordance**: without one number to minimize, an agent flails across ~200 dimensions.

### Verbs

| Verb | Purpose |
|---|---|
| `read` | Unity assets → `BalanceConfig` JSON |
| `write --apply` | `BalanceConfig` JSON → Unity assets (dry-run without `--apply`) |
| `sim` | run one seed, full `SimResult` |
| `eval` | run the seed set, score against a goal, return loss + breakdown |
| `patch` | apply a patch list config→config; no Unity writes |
| `suggest` | given a result + goal, return the knobs causally implicated |
| `sweep` | 1-D sensitivity: loss across a parameter range |
| `report` | per-level metrics + pressure ranking |

Patches are small and legible: `[{"op":"set","path":"recipes/field.wheatGrow/duration","value":20}]`.

### `suggest` — the pressure-to-knob map

Given the dominant pressure at a level, return the parameters causally implicated. `Storage` dominant →
`global.startingStorageCapacity`, `upgrades/silo.cap/tiers[*].cost`, `upgrades/silo.cap/tiers[*].effects[*].amount`.
This collapses a 200-knob search into a shortlist of 3–6, and it exists only because the pressure ledger
already knows *why* the player is stuck.

### Guardrails

- `bounds.json` — per-parameter min/max/step. `patch` refuses out-of-bounds values, so an agent can't set a
  duration to −5 or a cost to 10^9.
- **`SimProfile` paths are read-only to `patch`.** Without this, an agent lowers the loss by raising
  `Optimality` — making the *simulated player* smarter rather than the *game* better. It would report success
  having changed nothing about the game. `bounds.json` covers `BalanceConfig` paths only, and `patch` rejects
  a `profile/*` path outright. Varying the profile is a deliberate robustness check, run *after* a config is
  settled, never during optimization.
- `--apply` required for any Unity write; dry-run prints the change summary.
- `runs.jsonl` — every eval appends `{configHash, patch, loss, breakdown}`. The agent sees its own
  trajectory and doesn't repeat itself; the human gets a balance history for free.

### `AGENTS.md`

Lives in `tools/VoidDay.Balance/`. Documents the verbs, the goal and patch schemas, the parameter path
grammar, the bounds file, and a worked example loop. Cheap to write, disproportionately effective — an agent
that must reverse-engineer a CLI wastes most of its budget before its first useful run.

**The tool does not optimize by itself.** The agent runs the loop, bringing judgement the loss function
lacks — it knows a 3-second recipe is absurd even when the loss approves.

---

## The Balancing Session Workflow

The end-to-end loop the tool exists to serve:

1. **Converse about goals** with the user → a `goal.json`.
2. **Change values** → patches against a working config.
3. **Simulate, read results, iterate** until the goal is met or shown unreachable — conversing throughout.
4. **Explain what was done and how**, then export to Unity for playtesting.

Steps 2 and 4 are CLI verbs. Steps 1 and 3 are *conversation*, which an app can only host by becoming a chat
app — embedding an LLM, managing keys, rendering a transcript, running its own tool-calling loop. That is a
large build that duplicates the harness already in use and would be worse at it. So the workflow is made
first-class **without putting the agent inside the app**, via three pieces.

### 1. The skill — `.claude/skills/balance_game/SKILL.md`

The workflow *is* a skill, the same shape as `design_feature` and `implement_milestone`. `/balance_game`
loads the procedure: interview the user about goals, write `goal.json`, run the iteration loop, converse as
it goes, generate the report, and gate the export. Conversation happens in the terminal where it is native
and free. The agent drives the CLI via Bash; every verb returns `--json`, and the user can run any of the
same commands by hand to audit the agent's work. **The CLI is the contract** — no second interface to keep
in sync.

### 2. Sessions in the tool

```
tools/VoidDay.Balance/sessions/2026-07-22-capacity-pacing/
  goal.json           the agreed goal (output of step 1)
  config.start.json   the config as found
  config.current.json the working config — patched freely, never touches Unity
  journal.jsonl       every iteration: patch, loss, breakdown, rationale
  report.md           generated by `balance session report`
```

Verbs: `session start`, `session status`, `session report`.

**The report is generated from the journal, not narrated from memory.** A real balancing run spans dozens of
iterations and will exhaust a context window; an agent summarising from a compacted context will produce a
tidier story than what actually happened. Reading the journal makes the write-up true by construction. The
`rationale` field — the one thing the agent must supply per iteration — is what lets the report explain *why*
each change was made, not just what changed.

The agent then presents **highlights in the terminal** for discussion, with `report.md` as the durable record.

### 3. The live session view

The browser polls the active session directory and re-renders: the loss curve growing, the pressure heatmap
shifting, per-level times moving. The agent iterates in the terminal, the user watches and interjects. Small
to build once Phase 6's charts exist, and it is what makes the app part of the workflow rather than a
separate destination.

### Autonomy boundary

| Sandboxed — no approval | Gated — approval required |
|---|---|
| `eval`, `sim`, `sweep`, `suggest`, `report` | `write --apply` → `Assets/Data/SO/*.asset` |
| `patch` → `config.current.json` | |

Everything the agent does while searching is JSON in a session directory and costs only time. Touching game
data requires the user's yes, with the full change summary presented first.

### Stopping rule for infeasibility

Step 3's *"or until confident goals are not achievable"* must be evidence-based, not a vibe. The skill's
stopping rule: sweep every knob `suggest` implicates to both its bounds; if loss stays above target across
all of them, declare the goal unreachable **and present the sweep data as the argument**. Without an explicit
rule an agent either gives up early or grinds forever.

---

## Implementation Phases

Each phase ends with the implementer pausing, playing the notification sound, presenting exactly what to
verify, and waiting for confirmation before committing.

### Phase 1 — Asset reader

**Deliverables**
- `tools/VoidDay.Balance/` csproj (net9.0) globbing `../../Assets/Core/**/*.cs`; YamlDotNet + Newtonsoft.
- `Unity/GuidIndex.cs`, `Unity/AssetReader.cs` (with the 5-line YAML preprocessor), `Unity/SceneScanner.cs`.
- `Schema/BalanceConfig.cs` and friends; enum int↔name mapping via the real Core enums.
- CLI: `balance read --project ../.. --out versions/baseline.json`.

**Verify:** open `baseline.json` beside the inspector. `Recipe_Field_WheatGrow.duration`, `Station_Field.cap`
/ `buildCost` / `unlockLevel`, `Upgrade_Silo_Cap` tier costs and effect amounts, all 20 level thresholds, the
ten `OrderConfig` fields — all match. `startingStations` reads 1 Field, 1 Silo, 1 Order Board. Enums are
strings.

### Phase 2 — Asset writer + round trip

**Deliverables**
- `Unity/AssetWriter.cs` — surgical line patching; block insertion for new recipes / level rows / upgrade
  tiers; `.meta` generation for new assets.
- Change summary (`asset.field: old → new`), dry-run default, `--apply` to write.
- `balance write --config X --project ../.. [--apply]`.
- Round-trip test: `read → write --apply → read` produces byte-identical JSON, and the second write reports
  zero changes.

**Verify:** halve `Recipe_Field_WheatGrow.duration` in the JSON, run `write` without `--apply` and read the
one-line summary, run with `--apply`, confirm the inspector updates and `git diff` shows exactly one changed
line. Press Play and watch a wheat job finish in half the time. `git checkout` to revert.

**The round trip is closed here — the tool is genuinely useful even if nothing else ships.**

### Phase 3 — Simulation engine (headless)

**Deliverables**
- `Sim/CoreHarness.cs` — wires the Core object graph from a `BalanceConfig`, mirroring `GameBoot.Start()`.
  **`Producible()` must stay a live closure** over `grid.All` × `catalog.ForStationType`, never a boot-time
  snapshot (M8's log flags this: a snapshot never learns about a runtime-built Henhouse).
- `Sim/GameBootParityTests.cs` — the staleness canary hashing `GameBoot.cs`.
- `Sim/SimClock.cs` (1s stepping + exact event jumps), `Sim/PressureLedger.cs` (eight categories),
  `Sim/RecipeChain.cs` (backward chaining + cycle guard), `Sim/PlayerAgent.cs` (remedies + optimality dial),
  `Sim/MetricsCollector.cs` (subscribes to the real `EventBus`), `Sim/SimRunner.cs`.
- CLI: `balance sim --config baseline --profile typical --seed 1`, printing a per-level table.

**Verify:** run against `baseline`. Numbers should be *plausible and explainable* — level 1→2 short, later
levels longer, and the reported bottleneck per level matching what you'd predict from the config. Run the
same seed twice for byte-identical output. Run `--optimality 1.0` vs `0.4` and confirm times lengthen
monotonically.

### Phase 4 — Server + settings editor UI

**Deliverables**
- Minimal API: `GET/PUT /api/config`, `GET/POST/DELETE /api/versions`, `POST /api/sim`, `POST /api/write`.
- `wwwroot/` — Preact + htm vendored as ESM, no build step. Tabs: **Global / Resources / Recipes / Stations /
  Upgrades / Levels / Orders**. Add-row for recipes, level rows and upgrade tiers; resources and station
  types edit-only.
- Effect editor restricted to the six types with teeth.
- Client-side validation mirroring `BootValidator`: thresholds strictly ascending, level 1 has no grants, no
  duplicate ids, references resolve, `triggerChance` 0–100.

**Verify:** `dotnet run`, open the browser, load `baseline`, edit a duration and a build cost, save as a new
version, confirm both files on disk and that `baseline.json` is untouched. Push the new version to Unity
from the UI and confirm the asset changed.

### Phase 5 — Agent interface

**Deliverables**
- `Agent/Goal.cs` + `GoalEvaluator.cs` — the five metric families including `pressure.rank` and
  `pressure.share` with `min`/`max`; normalized weighted loss with per-target breakdown.
- `Agent/Suggest.cs` — the pressure→knob map. `Agent/Sweep.cs` — 1-D sensitivity.
- `Agent/Patch.cs` + `bounds.json`, **rejecting any `profile/*` path** so the agent cannot lower the loss by
  improving the simulated player instead of the game.
- `Agent/Journal.cs` → `runs.jsonl`.
- CLI verbs `eval`, `patch`, `suggest`, `sweep`, `report`, all with `--json`.
- `tools/VoidDay.Balance/AGENTS.md`.

**Verify:** write a goal file. Run `eval` and read the loss breakdown. Run `suggest` and confirm the returned
knobs are the ones you'd have picked by hand for the dominant pressure. Run a `sweep` over
`stations/field/buildCost` and confirm loss varies sensibly across the range. Then **hand the goal to an
agent and let it run the loop** — the real test is whether it lowers the loss without absurd values.

### Phase 6 — Multi-seed sweep + report UI

**Deliverables**
- `Sim/SimSweep.cs` — N seeds in parallel, median / p10 / p90 per metric.
- Chart.js 4.x vendored into `wwwroot/vendor/`. Five views:
  1. **Time per level** — median bar + p10–p90 whisker. The headline.
  2. **Time composition** — acting vs waiting per level.
  3. **Money** — entry and exit per level, with band.
  4. **Pressure heatmap** — level × category by seconds lost. *The bottleneck view.*
  5. **Purchase timeline** — level at which each remedy is first bought.
- Click any seed to open its individual run.

**Verify:** 30-seed sweep on `baseline`. The p10–p90 band is non-degenerate (seeds genuinely diverge), the
heatmap highlights a category you can explain from the config, and opening one seed reproduces its Phase 3
CLI output exactly.

### Phase 7 — A/B overlay + docs

**Deliverables**
- Baseline/candidate pickers; charts 1 and 3 overlay both; a per-level delta table with direction marked.
  Both sides run the same seed set, so a difference is a real config effect and not seed noise.
- `tools/VoidDay.Balance/README.md` — the ledger semantics, the optimality dial, the asset round trip.
- **Amend `docs/VoidDay-Spec-unity.md` §9**, which currently reads *"the pitch's separate balance tool… is
  superseded — the Unity inspector is the tuning UI… no separate tool, no write endpoint."* Replace with:
  the inspector remains the authoring surface and runtime source of truth; an external balance tool reads and
  writes those assets offline. Docs-only — no code dependency is created, and the agnosticism rule holds.

**Verify:** duplicate `baseline` as `cheap-fields`, halve every station `buildCost`, A/B on the same 30 seeds.
Earlier levels complete sooner and `Capacity` pressure falls.

### Phase 8 — Balancing session workflow + skill

**Deliverables**
- `Agent/Session.cs` — session directories, `session start` / `session status` / `session report`.
- `journal.jsonl` gains a required `rationale` string per iteration; `report.md` is generated from it —
  goal, starting values, every iteration with its rationale, final loss breakdown, and the exact diff
  exported to Unity.
- `.claude/skills/balance_game/SKILL.md` — the four-step workflow: goal interview → patch loop → report →
  gated export. Encodes the autonomy boundary and the infeasibility stopping rule.
- Live session view in the browser: polls the active session directory, re-renders the loss curve, pressure
  heatmap and per-level times as iterations land.
- Terminal highlights at completion, with `report.md` as the durable record.

**Verify:** run `/balance_game` end to end. Give it a goal in conversation, confirm `goal.json` matches what
you agreed, watch the browser update as it iterates, confirm it stops and asks before writing to Unity,
read `report.md` and check every claim against `journal.jsonl`. Then run the same commands by hand and
confirm you get the same numbers the agent reported.

---

## Systems Affected

**No file under `Assets/` is modified by this feature.** That is the agnosticism rule, and it is checkable:
after Phase 6, `git status` on `Assets/` should be clean except for assets the tool deliberately wrote.

| Path | Change |
|---|---|
| `tools/VoidDay.Balance/**` | **New** — the entire tool |
| `.claude/skills/balance_game/**` | **New** — the workflow skill (Phase 8) |
| `docs/VoidDay-Spec-unity.md` §9 | Amended (docs only; §9 currently forbids this tool) |
| `.gitignore` | Add `tools/VoidDay.Balance/{bin,obj}/`; keep `versions/` and `sessions/` tracked |

---

## Config

Tunables introduced by the tool (all in `SimProfile`; none in game code):

| Setting | Default |
|---|---|
| `Optimality` | 0.65 (1.0 for the best-case floor) |
| `Actions.Tap / Queue / Fulfill / Purchase` | 1.5 / 2.5 / 3.0 / 4.0 s |
| `ReactionLagSeconds` | 2.0 |
| `CashReserve` | 0 |
| `RecipePolicy` | `DemandChain` |
| `SeedCount` | 30 |
| `MaxSimulatedHours` | 40 |
| `StallGuardMinutes` | 45 |

No new tunables enter the game. Every economy number the tool edits already lives in an SO.

---

## Testing Strategy

CLAUDE.md suspends testing **except** the pure-C# economy core — which is what this feature touches, so the
exception applies.

| Test | Phase | Guards |
|---|---|---|
| `ReaderMatchesKnownAssets` | 1 | Parsed values match hand-checked asset content |
| `EnumMappingIsSymmetric` | 1 | int↔name round-trips for every Core enum |
| `RoundTripIsLossless` (read→write→read byte-identical) | 2 | Reader and writer agree on every field |
| `WriteProducesMinimalDiff` | 2 | A one-field change touches exactly one line |
| `WriteRejectsSchemaVersionMismatch` | 2 | Stale JSON can't corrupt assets |
| `GameBootParityCanary` | 3 | `GameBoot.cs` hasn't changed since `CoreHarness` was reconciled |
| `SimIsDeterministic` | 3 | Same seed ⇒ identical result. Everything else is meaningless without it |
| `PressureLedgerAccrualTests` | 3 | Each category accrues only under its stated condition; `Yield` vs `Capacity` split |
| `RecipeChainTerminatesOnCycle` | 3 | Bad recipe graph errors instead of hanging |
| `OptimalityMonotonicity` | 3 | Lower dial ⇒ never-faster levels |
| `GoalLossIsMonotonic` | 5 | Moving a metric toward its target never raises loss |
| `PatchRejectsOutOfBounds` | 5 | An agent can't write a negative duration |

Everything else is verified by using the tool.

---

## Risks & Open Questions

1. **`CoreHarness` drift is now the top risk** (it replaced the `GameBoot` refactor risk, which agnosticism
   removed). Mitigated by the parity canary, and bounded: only ~40 lines of *wiring* are mirrored — the rules
   are compiled directly and cannot drift.
2. **Asset writing is the most likely source of real damage.** It edits files the game depends on. Mitigated
   by dry-run default, `--apply`, minimal diffs, and git. Never run `write --apply` with uncommitted asset
   changes.
3. **The simulated player is a model, not a player.** Absolute level times will be wrong; *relative* answers —
   this config vs that, which pressure dominates level 6 — are the trustworthy output. Recalibrate
   `Optimality` once real playtest data exists.
4. **An agent will exploit a loose goal function.** It optimizes what you wrote, not what you meant — a goal
   that only constrains level durations will happily make every recipe instant. `bounds.json` is the backstop,
   and goals should pin the texture (`pressure.rank`) alongside the speed. The sharpest instance of this —
   lowering loss by raising `Optimality` instead of changing the game — is closed by making `SimProfile`
   read-only to `patch`, but the general failure mode remains and is why the export stays user-gated.
5. **A long balancing run will exhaust the agent's context.** This is why `report.md` is generated from
   `journal.jsonl` rather than narrated from memory: a compacted context yields a plausible, tidier story than
   what happened. Any claim in a report that isn't traceable to a journal line is a bug in the generator.
6. **Schema drift.** Adding a field to an SO without adding it to `BalanceConfig` means the tool silently
   can't tune it. The round-trip test won't catch it (both sides omit it symmetrically). A periodic manual
   audit of SO fields vs `BalanceConfig` is the honest backstop.
7. **Open — should `versions/*.json` be git-tracked?** Planned yes, so balance history is reviewable. Move to
   ignored with an explicit "promote to baseline" if they churn noisily.

---

## Manual QA Test Cases

Run after Phase 7. Each states what to do, what to expect, and what it proves.

### QA-1 — Read fidelity
**Do:** `balance read`. Open `baseline.json` beside the inspector.
**Expect:** `Recipe_Field_WheatGrow.duration`, `Station_Field.cap`/`buildCost`/`unlockLevel`,
`Upgrade_Silo_Cap` tier costs and effect amounts, all 20 level thresholds, and `OrderConfig`'s ten fields all
match. `startingStations` reads 1 Field / 1 Silo / 1 Order Board. Enums are strings.
**Tests:** the reader sees every field the game reads (Phase 1).

### QA-2 — Round trip is lossless and minimal
**Do:** `read`, `write --apply` with no edits, `read` again. Diff the two JSONs and run `git diff`.
**Expect:** JSONs byte-identical; the change summary lists **zero** changes; `git diff` on `Assets/` is empty.
**Tests:** reader/writer agreement, and that writing is genuinely a no-op when nothing changed.

### QA-3 — A real edit reaches the game with a minimal diff
**Do:** Halve `Recipe_Field_WheatGrow.duration`. `write` (dry-run), read the summary, then `write --apply`.
`git diff`. Press Play and queue a wheat job.
**Expect:** one summary line with correct old→new; `git diff` shows exactly one changed line; the job
completes in half the time.
**Tests:** the full round trip, minimal-diff writing, and that SOs remain the runtime source of truth.

### QA-4 — Writer fails loud and never half-applies
**Do:** Hand-edit a JSON to rename a resource id to `"bogus"`; separately set `schemaVersion` to 999. Try
`write --apply` on each.
**Expect:** both abort naming the offending id / version, and `git diff` on `Assets/` is empty afterward.
**Tests:** fail-loud at the data boundary; no partial application.

### QA-5 — Unity is genuinely agnostic
**Do:** `git status`. Then temporarily rename `tools/` and open the Unity project; press Play.
**Expect:** no tracked changes under `Assets/` attributable to the tool's existence. Unity opens, compiles
with zero errors, and the game plays normally with `tools/` gone.
**Tests:** the one-way dependency rule — the whole architectural constraint of this feature.

### QA-6 — Simulation determinism
**Do:** Run the same config + profile + seed twice, once from the CLI and once from the browser.
**Expect:** identical results to the last decimal from both entry points.
**Tests:** the seeded-`Random` split, and that no wall-clock time or unordered dictionary iteration leaks
into the sim. Without this every other number is meaningless.

### QA-7 — Optimality dial behaves
**Do:** Same config and seed at optimality 1.0, 0.65, 0.3.
**Expect:** total time to max level rises monotonically. At 1.0 acting is a large share of elapsed time; at
0.3 waiting grows visibly.
**Tests:** the dial's four mechanisms engage; "average" and "optimal" are distinguishable.

### QA-8 — Storage bottleneck detected and fixed
**Do:** Set `startingStorageCapacity` to 10, run a sweep. Then set it to 200 and re-run.
**Expect:** at 10, `Storage` dominates early levels, the Silo tier is bought almost immediately, and levels
are slower. At 200, `Storage` collapses to near zero and a **different** category takes the top slot.
**Tests:** `Storage` accrual, the storage remedy, and that the ledger *re-ranks* rather than being hardcoded.

### QA-9 — Capacity vs Yield are distinguished
**Do:** Set `Station_Field.cap` to 1, run. Then set it to 6, run again.
**Expect:** at cap 1, `Yield[field]` outweighs `Capacity[field]` (you cannot add fields, so more-per-job is
the only fix). At cap 6, `Capacity[field]` outweighs `Yield[field]` and extra fields appear in the purchase
timeline.
**Tests:** the saturation rule splitting the two — the subtlest logic in the ledger.

### QA-10 — Unlock pressure reads the level curve
**Do:** Set every `StationSO.unlockLevel` to 10 and run.
**Expect:** `Unlock` dominates levels 1–9, with `Supply` rising alongside as orders request goods no placed
station can make.
**Tests:** the diagnostic answering "what unlocks should happen at which level" — the tool's first purpose.

### QA-11 — Order board starvation
**Do:** Set `refillSeconds` to 600 and `slotCount` to 1. Run.
**Expect:** `OrderRefill` and `Income` climb sharply; money at level exit falls; levels lengthen.
**Tests:** the income diagnostics, and that order tunables propagate into the sim.

### QA-12 — Goal loss is meaningful
**Do:** Write a goal capping levels 1–5 at 15 min. `eval` baseline. Then halve every recipe duration and
`eval` again.
**Expect:** loss falls, and the breakdown attributes the fall to the duration targets specifically.
**Tests:** the loss function responds to the thing it claims to measure — the agent loop's foundation.

### QA-13 — Pressure targeting works in both directions
**Do:** Write a goal with `pressure.rank: Capacity, maxRank 1, levels 3-8`. `eval` baseline and read the
breakdown. Then set `Station_Field.cap` to 1 (making Yield dominate) and `eval` again.
**Expect:** loss on that target rises when Capacity stops being rank 1. Add a `pressure.share` target with
`min: 0.30` and confirm a config with *too little* Capacity pressure is also penalized.
**Tests:** `pressure.rank` and the `min` side of `pressure.share` — targeting a texture of play, not just
capping a bottleneck.

### QA-14 — `suggest` returns the right knobs
**Do:** Force a Storage bottleneck (QA-8's capacity-10 config) and run `suggest`.
**Expect:** `global.startingStorageCapacity` and the Silo tier cost/amount paths — not a generic list.
**Tests:** the pressure→knob map; without this an agent has no shortlist.

### QA-15 — An agent can actually close the loop
**Do:** Hand an agent `AGENTS.md`, a goal file, and the CLI. Let it run unattended.
**Expect:** loss decreases over iterations; `runs.jsonl` shows the trajectory; every proposed value is within
`bounds.json`; no `.asset` file is written without an explicit `--apply`.
**Tests:** the entire agent interface end to end — the ultimate test of your third requirement.

### QA-16 — A/B overlay isolates a real change
**Do:** Duplicate `baseline` as `cheap-fields`, halve every `buildCost`. A/B on the same 30 seeds. Then A/B
`baseline` against an unmodified copy of itself.
**Expect:** the first shows earlier levels sooner and `Capacity` pressure falling. The self-comparison shows
**every delta exactly zero**.
**Tests:** the comparison isolates config effects from seed noise — the self-comparison is the control.

### QA-17 — Pathological configs report rather than hang
**Do:** Set every `buildCost` to 999999 and run. Separately author two recipes whose outputs are each other's
inputs and run.
**Expect:** the first stops on the stall guard with `StopReason` naming it, reporting partial levels with
`Income` dominant. The second errors loudly naming the cycle. Neither hangs.
**Tests:** the stall guard and the cycle guard — a balance tool must survive bad balance.

---

## Implementation Complete — QA Checklist

**IMPORTANT: When implementation is finished, the implementer MUST display the Manual QA Test Cases section
above to the user as a checklist for verification. Do not skip this step.**
