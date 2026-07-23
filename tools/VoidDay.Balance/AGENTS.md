# VoidDay Balance Tool — Agent Primitives

This tool turns "is the balance good?" into a measurement. It exposes **primitives** an external agent composes
into a tuning loop. **There is no built-in optimizer** — you run the loop and bring the judgement the loss
function lacks (it will happily approve a 3-second recipe; you know that's absurd).

Everything is a CLI verb. Every verb takes `--json`. The CLI is the whole contract — no MCP, no second interface.

Run from anywhere in the repo; the tool finds the project root (the folder with `Assets/` + `.gitignore`).
Invoke as `dotnet run --project tools/VoidDay.Balance -- <verb> …` (or run the built dll directly).

---

## The verbs

| Verb | Does | Writes |
|------|------|--------|
| `eval` | Sim a config, score it against a goal → one loss + a per-target breakdown | appends 1 line to `runs.jsonl` |
| `patch` | Apply edits **config→config** (never to Unity), guardrailed | the patched config JSON |
| `suggest` | Name the knobs responsible for the dominant bottleneck | — |
| `sweep` | 1-D loss curve across one knob's range, N steps | — |
| `report` | List the flat global eval log (`runs.jsonl`) | — |
| `session start/status/report` | Drive and record a full balancing run (§ Balancing sessions) | a session dir |
| `quest create/delete/move/list` | Structural quest authoring **config→config** (§ Quest authoring) | the edited config JSON |

`sim`, `read`, `write`, `serve` are the earlier-milestone verbs; see `--help`/`Usage`.

### `eval`
```
balance eval --goal <file> [--config <name|file>] [--patch <file>]
             [--seed N] [--profile typical|perfect] [--optimality X] [--no-gems] [--json]
```
- `--config` is a version name (`baseline` → `versions/baseline.json`) or a path. Default `baseline`.
- `--patch` (optional) applies a patch **in memory** before simming — measure a candidate without persisting it.
  The patch is guardrailed exactly as the `patch` verb (bounds + `profile/*` rejection).
- Prints the loss and a per-target table. `--json` emits the full `LossReport` (each target's measured value,
  violation, weight, contribution). **One eval = one appended line in `runs.jsonl`** (`{ts, config, goal,
  configHash, patch, loss, breakdown}`).

### `patch`
```
balance patch (--patch <file> | --path <p> --value <v>) [--config <name|file>] [--out <file>]
```
- `--patch <file>` is a JSON array: `[{"op":"set","path":"stations/field/buildCost","value":40}, …]`.
  `--path/--value` is the single-op shorthand. Only `op: "set"` exists.
- Applies to a **deep copy** and writes the result to `--out` (or stdout). The input is never mutated; nothing
  touches Unity. A rejected op aborts the **whole** patch, loud, with exit 1.

### `suggest`
```
balance suggest [--config <name|file>] [--seed N] [--profile P] [--optimality X] [--no-gems] [--json]
```
Sims the config, finds the dominant pressure family, and lists the **structural** knobs that *remove* it.
**★ Where gems are papering over the bottleneck, gem knobs appear in a separate `relief` list** — raising the
gem drip *hides* the bottleneck, it does not remove it. Prefer the structural knob; the gem knob is the
cheaper-looking non-fix.

### `sweep`
```
balance sweep --goal <file> --path <knob> --from A --to B [--steps N]
              [--config <name|file>] [--seed N] [--json]
```
The coordinate-descent primitive: for each of N values in `[A,B]`, set the knob, sim, score → `(value, loss)`.
Exploratory measurement — it does **not** enforce `bounds.json` (you may want to see past the legal range), but
it will not vary a `profile/*` path. Compose sweeps yourself; there is no automated descent.

### `report`
```
balance report [--json]
```
Lists the flat, global `runs.jsonl` (one line per ad-hoc `eval`). Per-run structured reports come from
**sessions** (below), which is where a real tuning run should live.

---

## Quest authoring

Quests are the one collection the tool can **create, delete and reorder** — not just tune. A quest is a whole
object (id, conditions, goal, reward), not a single numeric knob, so it lives outside the scalar `patch` grammar
in its own `quest` sub-verb. Every sub-command is **config→config** (like `patch`): it loads a config, edits the
`Quests` list in memory, and writes the result to `--out` (or stdout). **Nothing touches Unity** — that is the
separate, gated `write --apply`. In a session, edit the working copy in place by passing the session's
`config.current.json` as both `--config` and `--out`, then re-measure with a bare `eval --session … --rationale`.

```
balance quest create --config <name|file> --id <id> --goal-kind <K> --goal-amount <N> [--goal-target <id>]
                     [--reward-xp N] [--reward-money N] [--reward-gems N] [--reward-resource <id:amount>]
                     [--condition <Kind:amount[:arg]>] [--at <index>] [--out <file>]
balance quest delete --config <name|file> --id <id> [--out <file>]
balance quest move   --config <name|file> --id <id> --to <index> [--out <file>]
balance quest list   --config <name|file> [--json]
```

- **`--goal-kind`** is a `GoalKind`: `EarnMoney`, `FulfillOrders`, `HarvestCrops`, `PurchaseUpgrades`,
  `BuildStations`, `ReachLevel`. A `HarvestCrops` goal **requires** `--goal-target <resourceId>`.
- **`--condition`** is `Kind:amount[:arg]` where `Kind` is a `ConditionKind`: `MinLevel` (amount = level),
  `ResourceAtLeast` (`:count:resourceId`), `QuestCompleted` (`:0:prerequisiteQuestId`), `UpgradePurchased`
  (`:0:trackId`). One condition per create flag (a multi-condition quest is authored by editing the config JSON).
- **`--reward-resource`** is `resourceId:amount`. Keep `--reward-xp > 0` — the in-game collect particle rides the
  XP reward (a zero-XP quest collects silently).
- **`move`/`--at`** reorder the `GameConfigSO.quests` **list position** (which is all "reorder" means here — not
  steps within a quest). Indices clamp to the valid range.
- **`delete`** removes the quest from the config; the export then deletes its `.asset`. Deleting a quest another
  quest's `QuestCompleted` condition depends on is refused at `write` (it would fail `BootValidator` at play).

Once quests are authored/tuned in the config, the scalar knobs `quests/<id>/reward.{xp,money,gems}`,
`quests/<id>/goal.amount`, `quests/<id>/conditions[n].amount` move under `patch`/`eval --session`/`sweep` like any
other knob, and `quest.completions` / `quest.rewardShare` measure the result. Export is the same gated
`write --apply` (below).

---

## Balancing sessions

A **session** is a directory holding one tuning run's durable state — so a run survives a context window and
its write-up is true by construction:

```
sessions/<date>-<slug>/
  goal.json            the agreed goal
  config.start.json    the config as found (never mutated)
  config.current.json  the working config — patched freely, never touches Unity
  journal.jsonl        one line per iteration: {Iteration, Ts, Patch[], ConfigHash, Loss, Breakdown, Rationale}
  report.md            GENERATED from journal.jsonl (never narrated)
```

```
balance session start  --name <slug> --goal <file> [--config <name|file>]   # seeds the dir (config default: baseline)
balance session status [--name <slug>] [--json]                             # iterations, first→last loss, last rationale
balance session report [--name <slug>] [--print]                            # writes report.md, prints highlights
```

`--name` resolves by exact dir name, or by `-<slug>` suffix, or — omitted — the most recently modified session.

**The iteration primitive is `eval --session`**, not a new verb:

```
balance eval --session <slug> --rationale "why, in one sentence" [--path <knob> --value <v> | --patch <file>]
```

It (1) loads `config.current.json` + `goal.json`, (2) applies the optional patch and **persists it back to
`config.current.json`**, (3) sims + scores, (4) appends **one** journal line. **`--rationale` is required** — it
is the one thing the report cannot generate for you. A **bare** `eval --session <slug> --rationale "…"` (no
patch) re-measures the current config (use it for the opening baseline). The same guardrails apply as `patch`
(bounds + `profile/*` refusal); a rejected patch aborts the iteration, loud, and journals nothing.

**`suggest` and `sweep` journal nothing** — they are free exploration. Only a committed `eval --session` lands a
line. **★ `session report` reads only `journal.jsonl` + `config.start.json`/`config.current.json`** — every
claim in `report.md` traces to a recorded line; nothing is narrated from memory.

The **live session view** (workbench → *Session* tab, endpoints `GET /api/sessions`, `GET /api/session?name=…`)
polls the active session directory and re-renders the loss curve, pressure heatmap and per-level times as
iterations land — it re-sims `config.current` through `/api/sim`, holding no economy logic of its own.

The full conversational procedure (goal interview → loop → gated export) is the `/balance_game` skill
(`.claude/skills/balance_game/SKILL.md`).

---

## Goal schema

A goal is a set of **targets**, each a bound on one metric over one scope. A target's violation is normalised
(scale-free, monotonic, in `[0,1)`) and multiplied by its `weight`; the loss is the sum. **Weight is your
lever** — a target over a wide level range accumulates one violation per in-range level, so widen the scope or
lower the weight to keep it from dominating.

```json
{
  "name": "my-goal",
  "targets": [
    { "metric": "level.durationMinutes", "levels": "1-5", "max": 15, "weight": 1 },
    { "metric": "total.minutesToLevel",  "level": 20, "max": 60, "weight": 1 },
    { "metric": "pressure.share", "category": "Storage", "levels": "6-9", "min": 0.30, "max": 0.60, "weight": 1 },
    { "metric": "pressure.rank",  "category": "Capacity", "levels": "3-8", "maxRank": 1, "weight": 1 },
    { "metric": "level.moneyAtEntry", "level": 5, "min": 100, "weight": 1 },
    { "metric": "level.moneyAtExit",  "level": 5, "max": 5000, "weight": 1 },
    { "metric": "gems.compressionShare", "levels": "1-20", "min": 0.05, "max": 0.40, "weight": 1 },
    { "metric": "gems.heldAtExit", "level": 20, "max": 10, "weight": 1 }
  ]
}
```

| Metric | Meaning | Bounds |
|--------|---------|--------|
| `level.durationMinutes` | minutes spent on each level in scope | `min`/`max` |
| `total.minutesToLevel` | wall-clock to reach `level` | `min`/`max` |
| `pressure.share` | a pressure **family**'s share of total pressure that level (gross of gem relief) | `min` **and/or** `max` — two-sided, so you can demand a bottleneck (`min`) not just cap one |
| `pressure.rank` | the family's rank by pressure (1 = leads) | `maxRank` |
| `level.moneyAtEntry` / `level.moneyAtExit` | cash at a level boundary | `min`/`max` |
| `gems.compressionShare` | fraction of a level's wall-clock bought away by gems | `min` **and** `max` — too high = fake timers, too low = gems are decoration |
| `gems.heldAtExit` | gems held at a level's exit | `min`/`max` |
| `quest.completions` | quests whose bar filled that level (the sim auto-collects on completion) | `min`/`max` |
| `quest.rewardShare` | quest-reward **money** as a share of the level's money earned | `min`/`max` — cap how much income is quest handouts, or demand a floor |

Scope: `"level": N` (single) or `"levels": "a-b"` (inclusive range). `pressure.*` need a `category`
(a **family**: `Storage`, `Capacity`, `Yield`, `Throughput`, `Supply`, `Income`, `OrderRefill`, `Unlock` —
the parametrised keys `Capacity:field`, `Supply:corn`, … are aggregated into their family automatically).

---

## Path grammar (patch & sweep)

One path addresses one **numeric scalar** knob.

- **Singleton:** `global.startingStorageCapacity`, `xp.perJobCollected`, `gems.secondsPerGem`,
  `orders.refillSeconds` — `<root>.<field>`.
- **Collection element:** `stations/field/buildCost`, `recipes/field.wheatGrow/duration`,
  `resources/corn/baseValue` — `<collection>/<id>/<field>`. The `/` delimits the id from the field, so **ids may
  contain dots**. Collections: `resources`, `recipes`, `stations` (id = stationType), `upgrades`, `quests` (id = quest id).
- **Nested lists:** `upgrades/silo.cap/tiers[0].cost`, `upgrades/silo.cap/tiers[0].effects[0].amount`.
- **Quest scalars:** `quests/quest.starter/reward.xp`, `quests/quest.starter/reward.money`,
  `quests/quest.starter/goal.amount`, `quests/quest.starter/conditions[0].amount` — reward amounts, the goal
  target count, and a condition threshold. (Creating / reordering / deleting quests is not a scalar knob — that
  is the `write` verb's structural job.)
- **Index-addressed root:** `levels[0].xpThreshold`, `levels[1].grants[0].amount` — the level list is addressed
  by **position** (level N = `levels[N-1]`), not by an id like the slash collections; grants nest inside.

Field names match case-insensitively (the camelCase above binds to the PascalCase C# fields).

---

## Bounds & the read-only rule (patch guardrails)

`patch` is guardrailed by **`bounds.json`** and is enforced by the tool, not by asking nicely:

1. **`profile/*` is rejected outright.** The whole namespace is the *simulated player* (`optimality`,
   `gemPolicy`, `gemReserve`, `minSkipSeconds`, …), not the game. Lowering the loss by making the player smarter
   improves nothing real — it is the exploit this rule exists to forbid. Rejected by **namespace**, so a new
   profile field is caught the same way.
2. **Only declared knobs move.** A path with no bound in `bounds.json` is rejected — `bounds.json` is the
   allowlist of movable knobs. Bounds are patterns: `[n]`→`[*]`, and an id may be `*` (`stations/*/buildCost`
   covers every station). Most-specific pattern wins.
3. **Values stay in range.** A value outside `[min, max]` is rejected, naming the bound.

`sweep` shares the path grammar and the `profile/*` refusal, but **ignores** bounds (measurement, not a commit).

---

## Worked loop

```bash
# 1. Where does baseline hurt against my goal?
balance eval --goal goals/pace.json --json          # loss + breakdown; appended to runs.jsonl

# 2. What's the dominant bottleneck, and which knobs own it?
balance suggest                                     # e.g. "Storage" → global.startingStorageCapacity, silo.cap.*

# 3. How sensitive is the loss to one of those knobs?
balance sweep --goal goals/pace.json --path global.startingStorageCapacity --from 20 --to 120 --steps 6

# 4. Measure a specific candidate without committing it.
balance eval --goal goals/pace.json --patch patches/store60.json    # patches/store60.json = [{"op":"set","path":"global.startingStorageCapacity","value":60}]

# 5. Commit the edit into a new config version (still config→config; Unity untouched).
balance patch --config baseline --path global.startingStorageCapacity --value 60 --out versions/store60.json

# 6. Re-eval the committed config; the journal records the trail.
balance eval --config store60 --goal goals/pace.json
balance report
```

Pushing a version back into Unity is the separate, gated `write` verb — never a side effect of `patch`. It
handles: line-addressable scalar edits (including `recipes/*/unlockLevel` and `upgrades/*/unlockLevel`);
recipe insertion; **positional edits to the Levels asset** (`levels[*].xpThreshold`,
`levels[*].grants[*].amount`) plus **structural grant changes** — a grant added, removed, or retargeted to a
new kind/station regenerates that level's grant block byte-for-byte (an unchanged grant line stays
diff-clean); **upgrade tier costs and effect `value.amount`s** (`upgrades/*/tiers[*].cost`,
`upgrades/*/tiers[*].effects[*].amount`); and **the full quest structural set** — creating a quest writes a new
`Quest_<id>.asset` (+ `.meta`) and wires it into `GameConfig.quests`, deleting a quest removes its asset and
unwires it, reordering regenerates the `GameConfig.quests` reference block byte-for-byte, and quest scalar knobs
(`quests/*/reward.*`, `quests/*/goal.amount`, `quests/*/conditions[*].amount`) are line-addressable single-scalar
edits. What it still **refuses loudly before writing a byte**: adding or removing a whole level, adding or
removing an upgrade tier or effect, changing an effect field other than its amount, creating or deleting a
resource or station, editing an existing quest's goal kind/target or its condition/reward-resource *structure*
(only the quest scalars above move), and deleting a quest another quest still depends on.
