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
Lists `runs.jsonl`. Flat and global this milestone; sessions/structured reports come later.

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
  contain dots**. Collections: `resources`, `recipes`, `stations` (id = stationType), `upgrades`.
- **Nested lists:** `upgrades/silo.cap/tiers[0].cost`, `upgrades/silo.cap/tiers[0].effects[0].amount`.

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

Pushing a version back into Unity is the separate, gated `write` verb (scalar edits + recipe insertion only) —
never a side effect of `patch`.
