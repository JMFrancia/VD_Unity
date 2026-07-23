# VoidDay Balance Tool

An **offline** balance instrument for the VoidDay economy. It reads the game's ScriptableObject assets,
simulates the economy headlessly (pure C#, no Unity, no editor), scores a run against a stated goal, and —
only on explicit confirmation — writes a minimal diff back into the `.asset` files for playtesting.

> **One-way dependency (the defining rule).** The tool reads and writes `Assets/`, but **nothing under
> `Assets/` knows this tool exists** — no asmdef, no menu item, no shared schema, no runtime write endpoint in
> the game. `git status` on `Assets/` stays clean except for an asset the tool was explicitly, interactively
> asked to write. The game spec's amendment lives at `docs/VoidDay-Spec-unity.md` §9/§16; the tool's own spec
> at `docs/BalanceTool-Spec.md`.

Run from anywhere in the repo — the tool finds the project root (the folder with `Assets/` + `.gitignore`):

```bash
dotnet run --project tools/VoidDay.Balance -- <verb> [--options]
```

`dotnet run` with no verb (or only `--options`) launches the **workbench** (browser app) on
`http://localhost:5177`.

---

## The verbs

| Verb | Does | Touches Unity? |
|------|------|----------------|
| `read` | Read the SO assets → `versions/baseline.json` | reads only |
| `write` | Diff a config against the assets; `--apply` writes a **minimal** diff | **writes** (gated) |
| `sim` | Run one seeded player through the economy → per-level table | no |
| `serve` | The workbench + live session view | no (until a gated push) |
| `eval` | Sim + score against a goal → loss + per-target breakdown | no |
| `patch` | Apply edits **config→config** (guardrailed), never to Unity | no |
| `suggest` | Name the knobs that own the dominant bottleneck | no |
| `sweep` | 1-D loss curve across one knob's range | no |
| `report` | List the flat eval log (`runs.jsonl`) | no |
| `session start/status/report` | Drive and record a full balancing run | no (until a gated push) |

The **CLI is the whole contract** — every verb takes `--json`, and the user can run any command by hand to
audit the agent's work. There is no second interface (no MCP).

---

## The asset round trip

```
Unity SO assets ──read──▶ versions/baseline.json ──patch/edit──▶ versions/<candidate>.json
                                                                        │
                                                        sim / eval / sweep (JSON only, free)
                                                                        │
                                                   write --apply (GATED, confirmed) ──▶ Unity SO assets
```

- **`read`** produces `versions/baseline.json`, a faithful `BalanceConfig` mirror of every field the game reads.
- **Editing** happens on versions (in the workbench or via `patch`) — plain JSON, costs only time, Unity untouched.
- **`write`** re-reads the current assets, diffs the incoming config, and writes **only the changed fields**:
  a one-field change is a **one-line `git diff`** (it never reserialises an asset). It supports **scalar edits**
  and **recipe insertion**; it **refuses**, loudly and wholly, anything it cannot do surgically (nested-collection
  edits, deletions, id/schema mismatches). A refusal is surfaced, never silently dropped.
- The write is **gated**: `write` dry-runs by default and prints the change summary; `--apply` is required to
  touch a byte, and in the session workflow that approval is the user's explicit yes.

---

## Ledger semantics (how a bottleneck is measured)

The sim keeps a **pressure ledger**: for each level, seconds-of-progress lost per category, accrued continuously
(the player is always present). Categories: `Storage`, `Throughput`, `Income`, `OrderRefill`, `Unlock`, plus the
**parametrised** families `Capacity:<station>`, `Supply:<good>`, `Yield:<station>`. Consumers aggregate the
parametrised keys into their family (`Capacity:field` → `Capacity`), so a goal names a family without knowing
every suffix.

- **Capacity vs Yield** are split by a saturation rule: below the station cap, more stations would help →
  `Capacity`; at the cap, only more-per-job helps → `Yield`.
- **★ Pressure is recorded GROSS of gem relief.** A gem skip records `GemRelief` (and `SecondsPurchased`)
  **separately** and **never** reduces `Pressure`. Net is derived (`Pressure − GemRelief`), never stored. This is
  the load-bearing gem rule: if pressure dropped when the player skipped, gems would hide bad balance from the
  loss function. The one rule most likely to be "simplified" into a bug — don't.

---

## The optimality dial

`SimProfile.Optimality` (0…1, default 0.65) models *how well the player plays*, via four mechanisms: action
timing, reaction lag, how aggressively remedies are bought, and how wastefully gems are spent. Lower optimality
⇒ never-faster levels (monotonic). It also has a **gem row**: at 1.0 a gem buys near `secondsPerGem` of
progress; at 0.3 a sloppy player skips nearly-finished timers, so `SecondsPerGemRealised` falls toward
`minGemCost` — the same gems, less value. `Optimality` lives in the `profile/*` namespace, which is the
**simulated player, not the game** — and is therefore **read-only to `patch`** (lowering loss by making the
player smarter improves nothing real).

---

## Goals, bounds, and guardrails

- A **goal** (`goals/*.json`) is a set of targets, each a bound (`min`/`max`/`maxRank`) on one metric over one
  scope. `eval` scores a `SimResult` against it → one loss + a per-target breakdown. See `AGENTS.md` for the
  metric list and the path grammar.
- **`bounds.json`** is the allowlist of movable knobs (wildcard patterns). `patch` moves only declared knobs,
  only within range, and **rejects the entire `profile/*` namespace** — by prefix, not a field list.

---

## Balancing sessions (the workflow)

A session is a directory under `sessions/` holding one tuning run's durable state:

```
sessions/<date>-<slug>/
  goal.json            the agreed goal
  config.start.json    the config as found (never mutated)
  config.current.json  the working config — patched freely, never touches Unity
  journal.jsonl        one line per iteration: patch, loss, breakdown, RATIONALE
  report.md            GENERATED from journal.jsonl (never narrated)
```

```bash
balance session start --name capacity-pacing --goal goals/pace.json      # seeds the session dir
balance eval --session capacity-pacing --path stations/field/cap --value 3 --rationale "give Capacity room"
balance eval --session capacity-pacing --rationale "re-measure after the cap change"   # bare re-eval
balance session status --name capacity-pacing
balance session report --name capacity-pacing                            # writes report.md, prints highlights
```

`eval --session` is the **iteration primitive**: it applies the optional patch to `config.current.json`,
persists it, sims, scores, and appends one journal line — and it **requires `--rationale`**, the one thing the
report cannot generate for you.

**★ The report is generated, never narrated.** `session report` reads only `journal.jsonl`, `config.start.json`
and `config.current.json`. A long run exhausts a context window and an agent summarising from a compacted
context produces a tidier story than what happened; reading the journal makes the write-up true by construction.
Every claim in `report.md` traces to a recorded line.

The full conversational workflow — goal interview, iteration loop, gated export — is the `/balance_game` skill
(`.claude/skills/balance_game/SKILL.md`). The **live session view** (workbench → *Session* tab) polls the active
session directory and re-renders the loss curve, pressure heatmap and per-level times as iterations land.

---

## Layout

```
tools/VoidDay.Balance/
  Cli/          the CLI entrypoint (all verbs)
  Unity/        reader + surgical writer + guid/scene indexing
  Sim/          the headless economy (mirrors GameBoot wiring; pure C#)
  Agent/        goal loss, suggest, sweep, bounds, patch, journal, sessions
  Api/          the workbench server (a thin client of the above)
  Schema/       BalanceConfig, SimProfile, SimResult
  wwwroot/      the workbench UI (Preact + Chart.js, vendored, no CDN/build)
  versions/     config versions (git-tracked); baseline.json is the read-from-Unity reference
  goals/        goal files
  sessions/     balancing sessions (runtime output)
  bounds.json   the movable-knob allowlist
runs.jsonl      the flat eval log (gitignored runtime output)
```

Tests (the pure-C# economy core only, per `CLAUDE.md`) live in `tools/VoidDay.Balance.Tests/`.
