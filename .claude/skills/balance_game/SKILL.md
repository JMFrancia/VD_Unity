---
name: balance_game
description: Tune the VoidDay economy end to end — interview the user for a balance goal, iterate on the config with the offline balance tool while conversing, explain the result from the durable journal, and gate the export back into Unity. Use when the user wants to balance, tune, or rebalance the game's economy (pacing, pressure, money, gems). Drives tools/VoidDay.Balance via the CLI; writes no game code.
---

# /balance_game — the balancing workflow

You are tuning the **VoidDay economy** with the offline balance tool at `tools/VoidDay.Balance/`. The tool
reads the game's ScriptableObject assets, simulates the economy headlessly, and — only on the user's explicit
yes — writes a minimal diff back for playtesting. **Nothing under `Assets/` knows the tool exists**; keep it
that way.

**Read `tools/VoidDay.Balance/AGENTS.md` first** — it is the authoritative reference for the verbs, the goal
schema, the metric list, the path grammar, and the guardrails. This skill is the *procedure*; AGENTS.md is the
*contract*. Everything here composes CLI primitives that already exist — do not invent a verb or a metric.

Run the tool as `dotnet run --project tools/VoidDay.Balance -- <verb> …` (every verb takes `--json`). Suggest
the user open the **workbench** (`dotnet run --project tools/VoidDay.Balance` → `http://localhost:5177`, the
**Session** tab) so they can watch iterations land live while you work in the terminal.

---

## The four steps

### 1 — Interview for the goal → `goal.json`

Converse about what the balance should *feel* like, then turn the answers into a goal. Ask only what you can't
infer; drive toward concrete, measurable targets. Useful questions:

- **Pacing:** how long should the early levels take? The whole run to level 20?
- **Bottleneck texture:** which pressure should *lead* early — Storage? Capacity? Should any never dominate?
- **Money:** should the player ever be cash-starved, or swimming? At which levels?
- **Gems:** are gems meant to meaningfully compress time, or stay decorative? (Watch for gems *hiding* bad
  pacing — see the stopping rule.)

Turn the answers into a goal file (see AGENTS.md for the full schema). Metrics: `level.durationMinutes`,
`total.minutesToLevel`, `pressure.share`, `pressure.rank`, `level.moneyAtEntry`/`moneyAtExit`,
`gems.compressionShare`, `gems.heldAtExit`. **Read the goal back to the user in plain words and get agreement
before starting.** Save it (e.g. `tools/VoidDay.Balance/goals/<name>.json`), then open the session:

```bash
balance session start --name <slug> --goal goals/<name>.json
```

This seeds `sessions/<date>-<slug>/` with `goal.json`, `config.start.json`, `config.current.json` and an empty
`journal.jsonl`. The `goal.json` in the session must match what the user agreed — confirm it.

### 2 — Change values (patches against the working config)

All editing is **config→config**, never Unity. The session's `config.current.json` is the working copy;
`eval --session` patches it in place. Never touch `config.start.json`.

### 3 — The iteration loop (converse throughout)

Repeat until the goal is met or shown unreachable:

```
eval  → where does the current config hurt against the goal?   (loss + per-target breakdown)
report/suggest → which bottleneck dominates, and which knobs own it?
sweep → how sensitive is the loss to a candidate knob?          (measurement; not committed, not journaled)
eval --session … --rationale "…" → commit the chosen change: patch config.current, sim, journal one line
```

Concretely:

```bash
balance eval  --session <slug> --rationale "baseline as found"          # iteration 1: measure the start
balance suggest --config <slug's current, or a version>                 # name the dominant bottleneck's knobs
balance sweep --goal goals/<name>.json --path <knob> --from A --to B    # sensitivity of one knob (free, unjournaled)
balance eval  --session <slug> --path <knob> --value <v> --rationale "why this change, in one sentence"
```

- **`eval --session … --rationale …` is the iteration primitive.** It applies the patch to `config.current.json`,
  persists it, sims, scores against `goal.json`, and appends **one** journal line. `--rationale` is **required** —
  it is the *why* the generated report cannot invent. Make each rationale a real, specific sentence.
- A **bare** `eval --session <slug> --rationale "…"` (no `--path`) re-measures the current config without changing
  it — use it for the opening baseline and for re-checks.
- **`suggest` and `sweep` are free exploration** — they journal nothing. Only committed iterations
  (`eval --session`) land in the journal.
- **Converse as you go.** Explain what each iteration found. If the user interjects or asks a question, answer
  from the session state (`session status`, the journal, a fresh `eval`) — you never lose the run, because the
  run lives on disk, not in your context.

**Guardrails are enforced by the tool, not by good behaviour** (AGENTS.md): `patch`/`eval --session` reject any
`profile/*` path (the simulated *player*, not the game), any undeclared knob, and any out-of-range value. You
cannot lower the loss by making the player better — that changes nothing real, and the tool refuses it.

### 4 — Explain, then gate the export

When the goal is met (loss at/near target, breakdown clean):

```bash
balance session report --name <slug>      # generates report.md FROM journal.jsonl; prints terminal highlights
```

- **★ The report is generated, never narrated.** `session report` reads only the journal and the start/current
  configs. **Do not write your own summary of what happened** — a compacted context yields a tidier story than
  the truth. Present the tool's terminal highlights and point the user at `report.md` as the durable record.
- **★ Always save a named version.** Unless the run ended in a problem (goal unreached, aborted), promote the
  tuned config into `versions/` before you finish — `cp sessions/<dir>/config.current.json versions/<slug>.json`
  — so it becomes a first-class `--config <slug>` any later run can `eval`/`sim`/A-B-overlay against `baseline`.
  The session dir is the run's journal; `versions/` is the durable, comparable artifact. This is independent of
  the Unity export — save the version whether or not the user approves the `.asset` write.
- **Gate the export.** Show the user the exact change summary (`report.md`'s *Diff to export to Unity* section,
  or a dry-run `balance write --config sessions/<dir>/config.current.json`). **Ask for explicit approval before
  any `.asset` write.**
  - **If the user declines: stop. Write nothing.** `git diff` on `Assets/` stays empty. "No" is honoured.
  - **If the user approves:** `balance write --config sessions/<dir>/config.current.json --apply`. The writer
    produces a minimal diff (scalar edits + recipe insertion) or **refuses loudly** anything it can't do
    surgically — surface the refusal verbatim, never pretend it wrote.

---

## The autonomy boundary (memorise this)

| Free — no approval needed | Gated — explicit approval required |
|---|---|
| `eval`, `sim`, `sweep`, `suggest`, `report`, `session *` | `write --apply` → writes `Assets/Data/SO/*.asset` |
| `patch` / `eval --session` → `config.current.json` | |

Everything on the left is JSON in a session directory and costs only time. The moment a byte would touch
`Assets/`, stop and get the user's yes with the full change summary shown first.

---

## The infeasibility stopping rule

Step 3's *"…or until confident the goal is not achievable"* must be **argued from evidence, not asserted**. When
the loss stalls above target and `suggest` keeps pointing at the same knobs:

1. Take **every knob `suggest` implicates**.
2. `sweep` each one across its **full bound-to-bound range**.
3. If loss stays above target at **both extremes of every implicated knob**, the goal is unreachable within the
   allowed design space.
4. **Declare it unreachable and present the sweep data as the argument** — the curves that show the loss floor.
   Do not grind indefinitely, and do not give up after two iterations. The sweep evidence is the deliverable.

Often the culprit is two targets in tension (e.g. *every level under 30s* **and** *money at level 10 above
50,000*). Name the tension explicitly and show the sweeps that prove it.

---

## Rules

- **The CLI is the contract.** Compose the existing verbs; never invent one. The user can re-run any command to
  audit you.
- **Every iteration carries a real rationale.** It is the one input the report depends on.
- **The report is generated from the journal — never narrated.** Any claim you can't trace to a journal line is a
  bug you are introducing.
- **"No" leaves `Assets/` untouched.** The export is gated; declining writes nothing.
- **The tool is one-way.** You may read and (on approval) write `Assets/`, but never make the game aware of the
  tool.
