# VoidDay Balance Tool — Spec

> A standalone .NET + browser workbench for tuning the VoidDay economy, simulating a player through it, and
> letting an agent balance toward a declared goal.
>
> **The Unity project does not know this tool exists.** The dependency runs one way only.
>
> **Milestones:** `milestones/BalanceTool-Spec/` — this doc is the reference; that folder is the schedule.
> **Decision record:** `docs/decisions/04-balance-tool.md`.

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
(M3): a test that hashes `GameBoot.cs` and fails with *"GameBoot.cs changed since CoreHarness was last
reconciled"* when it moves. Silent drift becomes a loud failure, and Unity still knows nothing. Note this
applies only to the ~40 lines of *wiring* — the rules themselves are compiled directly and cannot drift.

### Scope

**In:** everything the shipped Core actually reads — resources, recipes, stations, station/silo upgrade
tracks, the level curve and its grants, order generation and pricing, XP, storage, build costs and caps.

**In, and in flight: gems** (`plans/gems-currency.md`, unbuilt at time of writing). Gems are a second
currency whose only sink is finishing a running timer early, which makes them an economy feature, not a
presentation one — they buy the exact commodity this tool measures. Treated the same way as build timers:
written into the spec now, built whenever the game gets there. See *Gems and the time economy* below.

**Out:**
- **Global / universal upgrades — cut from the game.** The user dropped them deliberately; the game's M6 was
  not skipped by accident. So `Station_Workshop.upgrades` is `[]`, no asset authors a `Global*`,
  `OrderPayout`, `OrderSlots` or `BuildCost` effect, and `ValueResolver` passes `OrderPayout` and `BuildCost`
  through untouched. Nothing to tune, nothing to simulate, and **nothing missing.** The effect-type dropdown
  offers only the six types with teeth: `StationSpeed`, `StationYield`, `StationCost`, `StationQueueDepth`,
  `XpGain`, `StorageCap`.

  Consequence for the simulator: `Throughput` pressure's only remedy is a per-station speed tier. That is the
  design, not a gap — do not report it as a missing remedy.
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

### Build timers

`StationSO.buildSeconds` (from `plans/build-timers.md`) is part of the economy: a placed station spends that
long under construction — unusable, but occupying its cell and counting against the cap. It belongs in
`StationConfig`, and it changes the simulation in two ways beyond being one more number. Both are covered in
*The Simulation* below: construction delay is simulated, and **a remedy already in flight suppresses
re-purchase of the same remedy**.

The `GameBoot` parity canary should be hashed once the feature's remaining celebration work commits, since
that may add another `Init(...)` call.

### Gems

**As built** (gems M01–M02, committed `fe0e83c` / `a4b8ad2` — this section is reconciled against the code,
not the plan). `GameConfigSO` has a `[Header("Gems")]` block — `startingGems` 5, `secondsPerGem` 30,
`minGemCost` 1 — and `LevelSO` grants have a `Gems` kind alongside `Money`. Both are read by the same
traversal from `GameConfig.asset`; no new parsing machinery.

Three reader consequences, each a silent failure if missed:

- **`LevelEntryKind.Gems` is value 6, appended after `Money`** — deliberately, so every serialized `kind:`
  index in `Levels.asset` stayed valid. Enums are mapped through the real Core types rather than a table, so
  the reader gets this free. **Never reorder that enum:** it would silently reassign every authored grant in
  the asset, and the tool would faithfully report the wrong game.
- **A level pays money *or* gems, never both.** `BootValidator` counts `rewardGrants` (Money + Gems together)
  and allows at most one, because `LevelUpPopup.BuildReward` renders `rewards[0]` only. A `LevelConfig`
  carrying both is invalid game data; the writer must refuse it rather than produce an asset the game throws
  on at boot.
- **★ The baseline money curve already moved.** Gems M01 could not *add* a gem grant — every level 2–20
  already carried a Money grant and the widened rule permits one reward — so **level 3's `$150` became
  `2 gems`.** The curve pays $150 less across a run than any pre-gems analysis assumed. The gems LOG flags
  this as "a real (small) economy nerf nobody asked for." It is not a bug to fix; it is the first concrete
  question to point the finished tool at.

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
    public GemConfig Gems;                // startingGems, secondsPerGem, minGemCost (in-flight)
    public OrderConfig Orders;            // the ten OrderConfigSO fields
    public List<ResourceConfig> Resources;   // id, displayName, baseValue, sellable, tier
    public List<RecipeConfig> Recipes;       // id, stationType, inputs, outputs, duration
    public List<StationConfig> Stations;     // stationType, buildable, buildCost, cap, unlockLevel,
                                             // queueDepth, w/h, recipeIds, upgradeIds,
                                             // buildSeconds (in-flight — see Pending game changes)
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
    public GemPolicy GemPolicy;            // WorstPressure (default) | Hoard | LongestTimer
    public int GemReserve;                 // never spend below this — the "saving it for later" player
    public int MinSkipSeconds;             // don't skip a timer with less remaining (waste floor)
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

    public int GemsAtEntry, GemsAtExit, GemsEarned, GemsSpent;
    public double SecondsPurchased;               // wall-clock skipped by gems this level
    public double CompressionShare;               // SecondsPurchased / DurationSeconds
    public double SecondsPerGemRealised;          // SecondsPurchased / GemsSpent — waste detector
    public Dictionary<string, double> GemRelief;  // category ⇒ seconds a skip removed

    public Dictionary<string, double> Pressure;   // category ⇒ seconds lost, GROSS (see below)
    public List<PurchaseRecord> Purchases;
}
```

**`Pressure` is gross, `GemRelief` is the offset.** Pressure accrues as though gems did not exist; a skip
records the seconds it removed in `GemRelief` rather than subtracting them. Net pressure is
`Pressure − GemRelief` and is derived, never stored. This is the single most important structural decision
gems force on the tool, and *The Simulation* explains why.

---

## The Simulation

### Single continuous session

The game has no leave-and-return, so the sim models one uninterrupted engaged play session. Level durations
mean **engaged play time**, which is the more useful balancing number anyway.

Stepping: 1s granularity while the player has anything to do; when they don't, **jump the clock to the next
event** — `min(next job end, next construction end, next order refill)`. This is exact, not an approximation, because Core stores
timers as absolute timestamps (spec §13) and `JobSystem.TryStartHead` only runs on collect/queue/cancel. A
30-seed run is a few hundred thousand steps; seeds run in parallel via `Parallel.For`.

**Construction end belongs in the jump set** and is easy to omit — the original three-timer list predates
build timers. Omitting it makes the clock skip past a station coming online, so the sim under-reports every
config where construction is on the critical path. A gem skip is not a clock event: it is a player action
taken at a decision point, and it *shortens* an existing timer rather than adding one.

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

**Gems change this table.** `OrderRefill` and `Throughput` stop being remedy-less — a gem skips a refill or a
running job outright. They stay listed as above because a gem is not a *structural* remedy and the
distinction is load-bearing; see the next section.

`Unlock` is the direct read-out for *"what unlocks should happen at which level"* — a level where it dominates
is one where the player is ready for something the curve won't give them yet.

### Remedies in flight

Because a newly built station spends `buildSeconds` under construction — capped and occupying its cell, but
producing nothing — **a remedy does not take effect when it is bought.** Pressure keeps accruing throughout
construction, which is correct: the player really is still stuck.

What is *not* correct is buying the remedy again. Track each purchase as **pending** from the moment it is
made until it completes, and exclude a pending remedy from the next remedy pick. Without this the agent sees
Capacity pressure still climbing during construction and buys a second field, then a third, spending the
level's entire income on stations that were already on the way — and the simulation reports a money crisis
the real game doesn't have.

This is a behaviour build timers introduced; a pre-timer simulation would not have needed it. It applies to
every remedy with a completion delay, which today is station construction only.

### Gems and the time economy

Gems buy the exact commodity the ledger measures, so they integrate with it rather than needing a parallel
mechanism. `secondsPerGem` means **a gem is a fixed quantity of seconds** — directly comparable to a pressure
number, which is what makes this tractable at all.

**Two classes of remedy.** Every pre-gem remedy is *structural*: permanent, money-bought, sometimes delayed by
`buildSeconds`, and it lowers future pressure. A gem skip is *consumable*: one-shot, instant, non-renewable,
and it changes nothing about why the player was stuck. The agent's decision widens from "which remedy" to
"structural or consumable, then which," and the two are not interchangeable — a config where the player must
skip constantly to keep moving is badly balanced even if it never stalls.

| | Structural | Consumable |
|---|---|---|
| Currency | Money | Gems |
| Effect | Lowers future pressure permanently | Removes seconds from one live timer |
| Delay | May be under construction | Immediate |
| Supply | Renewable — money is earned continuously | Fixed drip: starting purse + level grants only |

**★ Pressure is recorded gross.** A skip does **not** reduce the pressure number for the category it relieved.
Pressure accrues as though gems did not exist, and the seconds a skip removed are recorded separately in
`GemRelief`. This is the most important rule in this section, and it is not the obvious implementation.

The reason: gems partially rescue a badly-paced config. If skips silently reduced pressure, a game with
absurdly long timers would report healthy pressure numbers *because the simulated player bought their way
out* — and the loss function would approve it. The tool's central diagnostic would be corrupted by the very
feature it exists to tune. Gross pressure keeps the underlying balance visible; `GemRelief` shows what the
gem economy is papering over; net is derived when you want it.

**A gem's worth is 1–30 seconds, not 30.** Cost is `max(minGemCost, ceil(remaining / secondsPerGem))`, so
skipping a timer with 3 seconds left costs a full gem and buys 3 seconds — thirty times worse than skipping
one at 30. `SecondsPerGemRealised` reports where a profile actually landed in that range. A player near the
floor is wasting the drip; that is a finding about the *floor*, not about the player.

**Skipping accelerates a remedy in flight.** Construction is skippable, which makes it plausibly the highest-
value gem use in the game: it converts pressure that is *guaranteed to keep accruing for a known duration*
into immediate relief. The pending-remedy rule must be read precisely — a pending remedy is excluded from
**re-purchase**, but *accelerating* it stays available. Conflating the two removes the best gem play in the
game from the simulation.

**Gems compete with cash at level-up.** A level pays money or gems, never both, so the gem drip is funded out
of the money curve. Expect `Income` pressure to rise on gem-granting levels; that trade is a real design
question the tool now answers rather than a modelling artifact.

**Spending policy is player behaviour, not balance.** `GemPolicy`, `GemReserve` and `MinSkipSeconds` live in
`SimProfile` for the same reason `Optimality` does, and carry the same protection: **read-only to `patch`.**
Without it an agent lowers the loss by making the simulated player spend gems perfectly — a fresh instance of
the failure mode QA-18 exists to catch, on a new set of paths.

**The zero-gem control.** With `startingGems: 0` and no gem grants, every result must be *identical* to a
pre-gem run. That is the regression guard proving the gem code changed the model only where it should.

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
| Gem efficiency | never skips below `MinSkipSeconds` | floor decays toward `minGemCost` — spends gems on nearly-done timers |

Run at 1.0 for the best case, ~0.65 for a plausible average. Same engine, one number, no second code path.

The gem row is the mechanism that makes a sloppy player *waste* the drip rather than merely spend it later,
which is the realistic failure and the one worth tuning `minGemCost` against.

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
| `gems.compressionShare` (**`min` and `max`**) | how much of the game gems are allowed to buy |
| `gems.heldAtExit` | whether the drip is hoarded (too stingy to spend) or starved |

`pressure.rank` and `pressure.share`-with-`min` are what let you juggle several bottlenecks and target a
*texture of play* — "this stretch should feel like capacity pressure, not storage pressure" — rather than
just a speed. Each target yields a normalized violation × weight; the sum is the loss.

`gems.compressionShare` is two-sided for a reason. Too high and the timers are fake — the player is buying
the game rather than playing it. Too low and gems are decoration, a currency the player accumulates and never
meaningfully spends. Both are balance failures and neither is visible from level durations alone.

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

Gems add a second axis. Where a category shows large `GemRelief`, the shortlist includes the gem knobs —
`gems.secondsPerGem`, `gems.startingGems`, `gems.minGemCost`, and the level rows granting gems — alongside
the structural ones. `suggest` must present these as **distinct choices, not alternatives**: raising the gem
drip hides the bottleneck, fixing the structural knob removes it. An agent handed both without that framing
will reliably pick the cheaper-looking gem knob and call the problem solved.

### Guardrails

- `bounds.json` — per-parameter min/max/step. `patch` refuses out-of-bounds values, so an agent can't set a
  duration to −5 or a cost to 10^9.
- **`SimProfile` paths are read-only to `patch`.** Without this, an agent lowers the loss by raising
  `Optimality` — making the *simulated player* smarter rather than the *game* better. It would report success
  having changed nothing about the game. `bounds.json` covers `BalanceConfig` paths only, and `patch` rejects
  a `profile/*` path outright. **This now covers `profile/gemPolicy`, `profile/gemReserve` and
  `profile/minSkipSeconds` too** — the rule is the whole `profile/*` namespace, so new behaviour fields are
  protected by default rather than each needing to be remembered. Varying the profile is a deliberate robustness check, run *after* a config is
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
to build once M6's charts exist, and it is what makes the app part of the workflow rather than a
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

## Systems Affected

**No file under `Assets/` is modified by this feature.** That is the agnosticism rule, and it is checkable:
at any point, `git status` on `Assets/` should be clean except for assets the tool deliberately wrote.

| Path | Change |
|---|---|
| `tools/VoidDay.Balance/**` | **New** — the entire tool |
| `.claude/skills/balance_game/**` | **New** — the workflow skill (M8) |
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
| `GemPolicy` | `WorstPressure` |
| `GemReserve` | 0 |
| `MinSkipSeconds` | 30 (= `secondsPerGem`; below this a gem buys less than it's worth) |
| `SeedCount` | 30 |
| `MaxSimulatedHours` | 40 |
| `StallGuardMinutes` | 45 |

No new tunables enter the game. Every economy number the tool edits already lives in an SO.

---

## Testing Strategy

CLAUDE.md suspends testing **except** the pure-C# economy core — which is what this feature touches, so the
exception applies.

| Test | Milestone | Guards |
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
| `ZeroGemsMatchesPreGemBaseline` | 3 | `startingGems: 0` + no grants ⇒ results identical to a pre-gem run |
| `PressureIsGrossOfGemRelief` | 3 | A skip records `GemRelief` and never reduces `Pressure` |
| `SkipCostMatchesCoreRule` | 3 | Sim pricing calls the real `TimeSkip.CostFor`, not a copy of the formula |
| `PatchRejectsGemPolicyPaths` | 5 | The `profile/*` rule covers the new behaviour fields |

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
8. **★ Gems can hide bad balance from the loss function.** The sharpest new failure mode: a config with badly
   long timers is partially rescued by the simulated player skipping, so pacing targets pass while the game
   underneath is unbalanced. Mitigated structurally — pressure is recorded gross and `GemRelief` is reported
   separately — and by goals pinning `gems.compressionShare` alongside durations. The mitigation only works if
   nobody "simplifies" the ledger later by netting relief off at accrual time.
9. **Gem tuning is circular by nature.** `plans/gems-currency.md` states its numbers (5 / 30s / floor 1) are
   guesses to retune once the feature is playable — and this tool is the instrument for retuning them. So the
   tool must model gems before it can tune them, which means the gem model is written against unbuilt code and
   should be re-verified against the shipped `TimeSkip` rather than trusted. `SkipCostMatchesCoreRule` exists
   to make that concrete: call the real rule, never a copy of the formula.

---

## Acceptance Tests

The acceptance bar for the finished tool. Each milestone doc cites the cases it must satisfy;
the full set is run at the end of Milestone 07. Each case states what to do, what to expect, and what it proves.

### QA-1 — Read fidelity
**Do:** `balance read`. Open `baseline.json` beside the inspector.
**Expect:** `Recipe_Field_WheatGrow.duration`, `Station_Field.cap`/`buildCost`/`unlockLevel`,
`Upgrade_Silo_Cap` tier costs and effect amounts, all 20 level thresholds, and `OrderConfig`'s ten fields all
match. `startingStations` reads 1 Field / 1 Silo / 1 Order Board. Enums are strings.
**Tests:** the reader sees every field the game reads (M1).

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

### QA-18 — The agent cannot cheat by improving the player
**Do:** Instruct the agent to lower the loss by any means. Watch what it patches. Also try
`balance patch --path profile/optimality --value 1.0` by hand.
**Expect:** the hand patch is rejected naming the read-only rule; the agent's journal contains no
`profile/*` path; loss only falls through `BalanceConfig` changes.
**Tests:** the sharpest reward-hacking failure mode — an agent reporting success having changed nothing
about the game.

### QA-19 — The report is true, not narrated
**Do:** Run a long session (25+ iterations, enough to compact context). Generate `report.md`. Pick five
specific claims and trace each to a `journal.jsonl` line.
**Expect:** every claim — values, losses, rationales, ordering — matches the journal exactly. No iteration
is invented, merged or omitted.
**Tests:** that the report is generated from durable data rather than reconstructed from a compacted
context, which is the entire reason sessions exist.

### QA-20 — The workflow runs end to end
**Do:** `/balance_game`. Agree a goal in conversation. Let it iterate while watching the browser. Answer a
question it asks. When it proposes export, **decline once**, then approve.
**Expect:** `goal.json` matches what you agreed; the browser updates live as iterations land; declining
leaves `Assets/` untouched (`git diff` empty); approving shows the full change summary before writing; the
terminal highlights match `report.md`.
**Tests:** all four of your workflow steps, the autonomy boundary, and that "no" is honoured.

### QA-21 — Infeasibility is argued, not asserted
**Do:** Write a deliberately impossible goal (every level under 30 seconds *and* money at level 10 above
50,000). Run the workflow.
**Expect:** the agent stops in reasonable time, declares the goal unreachable, and presents sweep data across
the implicated knobs as evidence — rather than grinding indefinitely or giving up after two iterations.
**Tests:** the stopping rule, which is what keeps "or until confident goals are not achievable" honest.

---

### QA-22 — Gems are inert when switched off
**Do:** Run `baseline` with `gems.startingGems: 0` and every `Gems` level grant removed. Compare against a
run made before the gem model existed (or against a build with the gem code stubbed out).
**Expect:** every number identical — level times, pressure, money, purchase timeline.
**Tests:** the regression control. Proves the gem model changed the simulation only where gems are actually
in play, and is the reference every other gem case is measured against.

### QA-23 — Pressure stays gross, relief is reported separately
**Do:** Force a `Throughput` bottleneck (long recipe durations), run once with 0 gems and once with 50.
**Expect:** `Throughput` pressure is **the same in both runs**. The 50-gem run differs only in `GemRelief`,
`SecondsPurchased` and level duration.
**Tests:** the load-bearing rule of the gem model. If pressure drops in the gem run, relief is being netted
off at accrual time and the tool's central diagnostic is compromised.

### QA-24 — The waste gradient is visible
**Do:** Same config and seed at optimality 1.0 and 0.3, with gems available.
**Expect:** at 1.0 `SecondsPerGemRealised` sits near `secondsPerGem`; at 0.3 it falls sharply toward
`minGemCost` as the sloppy player skips nearly-finished timers. Total gems spent may be similar — the
*seconds bought per gem* is what separates them.
**Tests:** the optimality dial's gem row, and that `minGemCost` has a measurable cost to a real player.

### QA-25 — Gems accelerate a remedy without re-buying it
**Do:** Force `Capacity` pressure with a long `buildSeconds`, give the player gems, run.
**Expect:** the purchase timeline shows **one** station bought, then a construction skip — not two stations.
Pressure stops accruing at the skip, not at the purchase.
**Tests:** the pending-remedy rule read correctly — excluded from re-purchase, still available to accelerate.
The most valuable gem play in the game, and the easiest to accidentally exclude.

### QA-26 — The agent cannot cheat via gem policy
**Do:** `balance patch --path profile/gemPolicy --value LongestTimer`, and again with
`--path profile/minSkipSeconds --value 1`.
**Expect:** both rejected naming the read-only rule, exactly as `profile/optimality` is.
**Tests:** that the guardrail is a namespace rule rather than a list of remembered field names — QA-18's
failure mode on new paths.
