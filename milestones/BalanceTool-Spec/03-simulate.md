# Milestone 03 — Simulate

**Demonstrable outcome:** run `balance sim` and get a per-level table — how long each level took, how much of
that was acting vs waiting, money at entry and exit, and the ranked bottleneck for that level — produced by
driving the game's real economy code.

## Goal
The milestone that makes the tool worth building. Everything before this was plumbing; this is the first
answer no amount of playing produces.

Also the largest and least certain milestone. Budget accordingly.

## Build This
- **`Sim/CoreHarness.cs`** — wires the Core object graph from a `BalanceConfig`, mirroring `GameBoot.Start()`.
  Header comment naming the mirrored file and the reconciliation date.
- **`Sim/GameBootParityTests.cs`** — the staleness canary: hash `GameBoot.cs`, compare to a recorded value,
  fail with *"GameBoot.cs changed since CoreHarness was last reconciled"*. **★ Hash it last — after every
  in-flight plan that could move `GameBoot` has landed.** Any plan adding a component that needs an
  `Init(...)` moves it. Known movers at time of writing: gems M01–M02 already did (the purse is constructed
  after `Wallet`, before `Progression`, with `EmitCurrent` beside the wallet's), gems M03 wires `TimeSkip`
  into `WorldState` / `ConstructionSiteView`, and Collection-Particles adds View components that need the
  bus. Hashing early guarantees the canary fires on arrival for a change you already knew about — which
  trains you to ignore it, defeating the point.
- **`Sim/SimClock.cs`** — single continuous session. 1s stepping while there is anything to do; exact jump to
  `min(next job end, next construction end, next order refill)` when there isn't. **Construction end is easy
  to omit** — the original three-timer list predates build timers — and omitting it makes the clock skip past
  a station coming online, under-reporting every config where construction is on the critical path.
- **`Sim/PressureLedger.cs`** — the eight categories per the spec. Five actionable (`Storage`, `Capacity`,
  `Throughput`, `Supply`, `Yield`), three diagnostic (`Income`, `OrderRefill`, `Unlock`). **★ Pressure is
  recorded gross**; gem relief is a separate `GemRelief` dictionary and net is derived, never stored.
- **Gem spending in `PlayerAgent`** — the consumable remedy class, per the spec's *Gems and the time economy*.
  Call the **real `TimeSkip.CostFor` / `Skip`**, never a reimplementation of the pricing formula. The three
  owner reads (`JobSystem.HeadSecondsRemaining`, `BuildSystem.SiteSecondsRemaining`,
  `OrderBoard.RefillRemaining`) return **-1 when there is no live timer of that kind** — that sentinel is what
  lets one rule serve three unrelated owners, so branch on it rather than on a zero.
- **`Sim/RecipeChain.cs`** — demand-driven backward chaining from the order board, deepest-unsatisfied-first,
  ties by value/sec, with a memoised cycle guard. `GreedyValuePerSecond` as the comparison policy.
- **`Sim/PlayerAgent.cs`** — buys the affordable remedy for the top pressure; the optimality dial driving
  remedy pick, reaction lag, action threshold and idle waste. **Tracks remedies in flight**: a station under
  construction is pending until `buildSeconds` elapses, and a pending remedy is excluded from the next pick.
- **`Sim/MetricsCollector.cs`** — subscribes to the real `EventBus` (`LevelUp`, `MoneyChanged`,
  `OrderFulfilled`, `JobCollected`, `UpgradePurchased`, `StationBuilt`, `StorageFull`, `StationBlocked`).
- **`Sim/SimRunner.cs`** + CLI `balance sim --config baseline --profile typical --seed 1`.
- **Tests** (Core economy exception applies): `SimIsDeterministic`, `PressureLedgerAccrualTests`,
  `RecipeChainTerminatesOnCycle`, `OptimalityMonotonicity`, `ZeroGemsMatchesPreGemBaseline`,
  `PressureIsGrossOfGemRelief`, `SkipCostMatchesCoreRule`.

## Do NOT Build This
- **Multi-seed sweeps or aggregation** → M6. One seed, one run, printed to the terminal.
- **Any chart or UI** → M4/M6. A text table is the deliverable.
- **Goal files, loss functions, `suggest`, `sweep`** → M5.
- **Sessions, journals, reports** → M7.

## Context
- **New:** `Sim/` (seven files), sim tests, `sim` verb.
- **Reads:** `BalanceConfig` from M1; the real Core, compiled.

## Principles
- **Determinism is load-bearing.** Same `(config, profile, seed)` ⇒ identical result. Two separate `Random`
  streams (order generation vs agent choices) so changing player behaviour never reshuffles the order
  sequence. **Sort before iterating any dictionary** — unordered iteration kills determinism quietly, and
  every downstream number becomes noise.
- **The clock jump must be exact, not approximate.** It is only valid because Core stores absolute timestamps
  and `TryStartHead` runs solely on collect/queue/cancel. If either stops being true, the jump becomes a bug.
- **★ `Producible()` stays a live closure** over `grid.All` × `catalog.ForStationType`. The game's M8 log
  documents this trap: a boot-time snapshot never learns about a runtime-built Henhouse; a roster-wide set
  offers cheesecake at level 1.
- **★ A remedy takes effect on completion, not on purchase.** `buildSeconds` means pressure keeps accruing
  during construction (correct — the player is still stuck), so the agent must not re-buy a remedy already in
  flight, or it spends a level's income on three fields when one was on the way.
- **★ A pending remedy is excluded from re-purchase, NOT from acceleration.** Construction is gem-skippable,
  and doing so is plausibly the best gem play in the game — it converts pressure guaranteed to accrue for a
  known duration into immediate relief. Conflating the two rules deletes that play from the simulation.
- **★ Gems must never reduce a pressure number.** A skip records the seconds it removed in `GemRelief`;
  `Pressure` accrues as though gems did not exist. Netting relief off at accrual time is the obvious
  implementation and it silently corrupts the tool: a config with absurd timers would report healthy pressure
  *because the simulated player bought their way out*, and the loss function would approve it.
- **Gem behaviour is `SimProfile`, gem pricing is `BalanceConfig`.** `GemPolicy` / `GemReserve` /
  `MinSkipSeconds` describe the player; `startingGems` / `secondsPerGem` / `minGemCost` describe the game.
- **Fail loud on impossible states**, but remember a *stalled config* is a finding, not an error — report it
  with a `StopReason`.

## Definition of Done
- `balance sim --seed 1` prints a per-level table for `baseline` with no errors.
- The numbers are plausible and **explainable**: level 1→2 short, later levels longer, and the reported
  bottleneck per level is one you can justify from the config.
- The same seed run twice produces byte-identical output.
- `--optimality 1.0` vs `0.4` lengthens level times monotonically.
- All four sim tests pass, plus the parity canary.
- A config with every `buildCost` at 999999 stops on the stall guard with a named `StopReason`; two mutually
  recursive recipes error loudly naming the cycle. Neither hangs.

## How to Test
1. Run against `baseline`. Read the table and satisfy yourself each level's bottleneck makes sense.
2. Run the same seed twice; diff the output.
3. Run at optimality 1.0, 0.65, 0.3 and confirm times lengthen monotonically and waiting grows.
4. Set `startingStorageCapacity` to 10 and re-run — `Storage` should dominate early levels.
5. Set `Station_Field.cap` to 1, then 6, and confirm `Yield` vs `Capacity` swap which one leads.
6. Run the two pathological configs and confirm they report rather than hang.
7. Set `gems.startingGems` to 0 and strip the level-3 gem grant; confirm the run matches a gem-free baseline.
8. Run with 50 gems against the same seed: `Throughput` pressure must be **unchanged**, with the difference
   appearing only in `GemRelief`, `SecondsPurchased` and level duration.

**Acceptance cases covered:** QA-6, QA-7, QA-8, QA-9, QA-17, QA-22, QA-23, QA-24, QA-25.
