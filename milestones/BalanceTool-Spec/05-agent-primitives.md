# Milestone 05 — Agent Primitives

**Demonstrable outcome:** write a goal file describing what you want the game to feel like, run `balance eval`
and get a single number plus a breakdown of which targets are violated — then run `suggest` and get back the
handful of knobs actually responsible.

## Goal
Turn "is this balance good?" from a judgement call into a measurement. The scalar loss is the single
highest-leverage thing in the tool: without one number to minimize, an agent flails across ~200 dimensions
and a human argues from vibes.

## Build This
- **`Agent/Goal.cs` + `GoalEvaluator.cs`** — five metric families:
  - `level.durationMinutes` (per level or range)
  - `total.minutesToLevel`
  - `pressure.share` with **both `min` and `max`** — apply a bottleneck deliberately, not just cap it
  - `pressure.rank` — *which* bottleneck should dominate, and where
  - `level.moneyAtEntry` / `level.moneyAtExit`
  - `gems.compressionShare` with **both `min` and `max`**, and `gems.heldAtExit`

  Each target yields a normalized violation × weight; the sum is the loss. Report the per-target breakdown,
  never just the total.

  `gems.compressionShare` is two-sided deliberately: too high and the timers are fake (the player is buying
  the game), too low and gems are decoration. Neither failure is visible from level durations alone.
- **`Agent/Suggest.cs`** — the pressure→knob map. `Storage` dominant ⇒ `global.startingStorageCapacity`,
  `upgrades/silo.cap/tiers[*].cost`, `upgrades/silo.cap/tiers[*].effects[*].amount`. Possible only because
  the pressure ledger already knows *why* the player is stuck.

  **★ Where `GemRelief` is large, gem knobs join the shortlist — flagged as a different kind of fix.**
  Raising the gem drip *hides* a bottleneck; fixing the structural knob *removes* it. Present them as
  distinct choices with that framing, or an agent will reliably take the cheaper-looking gem knob and declare
  the problem solved.
- **`Agent/Sweep.cs`** — 1-D sensitivity: loss across a parameter range, N steps. The coordinate-descent
  primitive.
- **`Agent/Patch.cs`** — `[{"op":"set","path":"...","value":...}]` applied config→config, never to Unity.
- **`bounds.json`** — per-parameter min/max/step; `patch` refuses out-of-bounds values.
- **★ `patch` rejects any `profile/*` path.** Non-negotiable: otherwise the cheapest way to lower any loss is
  to raise `optimality`, improving the *simulated player* rather than the *game*, and reporting success
  having changed nothing. **Reject the whole namespace, never a list of known field names** — `gemPolicy`,
  `gemReserve` and `minSkipSeconds` are new instances of exactly this exploit, and the next profile field
  will be another.
- **`Agent/Journal.cs`** → `runs.jsonl`, appending `{configHash, patch, loss, breakdown}` per eval.
- **CLI verbs:** `eval`, `patch`, `suggest`, `sweep`, `report` — all with `--json`.
- **`tools/VoidDay.Balance/AGENTS.md`** — verbs, goal schema, patch schema, path grammar, bounds, and a
  worked example loop.
- **Tests:** `GoalLossIsMonotonic`, `PatchRejectsOutOfBounds`, `PatchRejectsProfilePaths`,
  `PatchRejectsGemPolicyPaths`.

## Do NOT Build This
- **Automated search.** No `optimize` verb, no hill-climbing. The agent runs the loop, bringing judgement the
  loss function lacks — it knows a 3-second recipe is absurd even when the loss approves.
- **Session directories, journals-per-session, generated reports** → M7. `runs.jsonl` here is a flat global
  log; sessions structure it later.
- **The skill** → M7.
- **Multi-seed aggregation** → M6. `eval` may run the configured seed count, but the charting and
  median/percentile presentation belong to M6.

## Context
- **New:** `Agent/` (five files), `bounds.json`, `AGENTS.md`, five CLI verbs.
- **Reads:** M3's `SimResult` and its pressure ledger — this milestone adds no economy logic.

## Principles
- **One number, always with a breakdown.** A loss you can't decompose is a loss you can't act on.
- **Guardrails are structural, not advisory.** `bounds.json` and the `profile/*` rejection are enforced by
  the tool, not by asking an agent nicely.
- **The CLI is the contract.** No MCP server, no second interface. `--json` on everything.

## Definition of Done
- A goal file evaluates to a loss with a per-target breakdown.
- Halving every recipe duration lowers the loss on a duration-capped goal, and the breakdown attributes the
  fall to those targets specifically.
- `pressure.rank` penalises a config where the named category stops leading; `pressure.share` with `min`
  penalises a config with *too little* of that pressure.
- `suggest` on a forced Storage bottleneck returns the storage knobs, not a generic list.
- `sweep` over `stations/field/buildCost` produces a sensible loss curve.
- `balance patch --path profile/optimality --value 1.0` is **rejected**, naming the read-only rule.
- An out-of-bounds value is rejected naming the bound.
- `runs.jsonl` grows one line per eval.

## How to Test
1. Write a goal capping levels 1–5 at 15 minutes. `eval` baseline; read the breakdown.
2. Halve every recipe duration; `eval` again; confirm loss falls and the breakdown explains why.
3. Add `pressure.rank: Capacity, maxRank 1, levels 3-8`. `eval`. Then set `Station_Field.cap` to 1 (making
   Yield lead) and confirm that target's loss rises.
4. Add a `pressure.share` target with `min: 0.30` and confirm too *little* of that pressure is penalised.
5. Force a Storage bottleneck; run `suggest`; confirm the returned knobs are the ones you'd pick by hand.
6. Attempt the `profile/optimality` patch and an out-of-bounds duration; confirm both are refused.

**Acceptance cases covered:** QA-12, QA-13, QA-14, QA-18.
