# VoidDay Balance Tool — Milestone Plan

**Spec:** `docs/BalanceTool-Spec.md`
**Decision record:** `docs/decisions/04-balance-tool.md`
**Generated:** 2026-07-22

Smallest runnable artifact first, then layer over layer. "Playable" here means **runnable and demonstrable** —
you type a command or open a page and see the new thing work. Every milestone ends with something you can
operate yourself, not a library that compiles.

## Milestones
| # | Name | Demonstrable outcome | Doc |
|---|---|---|---|
| 1 | Read the Economy | `balance read` prints your entire economy as one JSON file, matching the inspector. | `01-read-the-economy.md` |
| 2 | Write It Back | Change a number in JSON, `write --apply`, see it in the inspector and in Play. | `02-write-it-back.md` |
| 3 | Simulate | A per-level table: time, acting vs waiting, money, and the ranked bottleneck. | `03-simulate.md` |
| 4 | The Workbench | Browser app — edit every tunable, save named versions, push to Unity. | `04-the-workbench.md` |
| 5 | Agent Primitives | `eval` returns a loss against a goal; `suggest` returns the knobs to turn. | `05-agent-primitives.md` |
| 6 | Reports & Comparison | 30-seed sweeps, five charts including the pressure heatmap, A/B overlay. | `06-reports-and-comparison.md` |
| 7 | Balancing Sessions | `/balance_game` runs the full loop: goals → iterate → report → gated export. | `07-balancing-sessions.md` |

## Where the value lands
- **After M2** the round trip is closed — the tool is genuinely useful even if nothing else ships. You can
  bulk-edit the economy in a text editor with a real diff, which the inspector cannot give you.
- **After M3** you get answers no amount of playing produces.
- **After M4** it's an app.
- **After M7** it's the workflow.

## Decisions Made
Taken while decomposing; the full reasoning is in `docs/decisions/04-balance-tool.md`.

1. **The simulator runs the real `Assets/Core` code**, compiled by glob. A reimplementation would drift and
   start lying. This is the first collection on CLAUDE.md rule 3.
2. **The Unity project stays agnostic.** Nothing under `Assets/` learns the tool exists. Cost: `CoreHarness`
   mirrors `GameBoot`'s wiring, guarded by a staleness canary (M3).
3. **Single continuous session, no offline.** The game has no leave-and-return; simulating one would produce
   authoritative numbers about a game that doesn't exist.
4. **The pressure ledger does three jobs** — the simulated player's decision input, the bottleneck report,
   and `suggest`'s input. One mechanism, three requirements.
5. **CLI-first, UI second.** Every capability is a verb with `--json`; the browser is a client of the same
   API. This is what makes it agent-drivable, and it is the right build order regardless.
6. **The workbench UI stays at M4** (user call) rather than deferring behind the agent loop, so hand-tuning
   is possible while the agent capability is still being built.
7. **Scope is what the game implements.** Global upgrades, VoidPets, relationships and world events are out.
8. **Gems are in scope and already shipped in Core** (`fe0e83c`, `a4b8ad2`). They buy the exact commodity the
   ledger measures, so they integrate with it rather than needing a parallel mechanism — but as a *consumable*
   remedy, categorically different from the permanent, money-bought, sometimes-delayed structural ones.
9. **★ Pressure is recorded gross of gem relief.** The one rule that must not be "simplified" later: netting
   relief off at accrual time lets a badly-paced config report healthy pressure because the simulated player
   bought their way out of it, and the loss function would approve it.

## Assumptions
Each is a risk; what breaks if it's wrong.

- **`.asset` bodies stay plain YAML.** Verified against `Upgrade_Field_Speed.asset` (nested effect structs)
  and `Levels.asset` (nested grant lists). If Unity ever emits a form the preprocessor doesn't handle, M1's
  reader fails loudly rather than silently mis-parsing — but M1 slips.
- **`GameBoot.Start()` is stable enough to mirror.** It has not changed in several milestones. If it churns,
  the canary fires often and the mirror becomes a maintenance tax rather than a one-off.
- **The pressure ledger's categories are the right decomposition.** If a real bottleneck exists that none of
  the eight captures, the simulated player is blind to it and `suggest` misdirects.
- **A bottleneck-seeking player with an optimality dial is a usable proxy for a real one.** Absolute times
  will be wrong; the design assumes *relative* answers are trustworthy. Recalibrate once playtest data exists.

## Gotchas
Traps a future implementer will hit.

- **★ `.gitignore` has a bare `*.csproj` (line 20, re-verified 2026-07-22)** and no `bin/` rule; `/[Oo]bj/` is root-anchored. Without
  `!tools/**/*.csproj`, `tools/**/bin/` and `tools/**/obj/`, the tool's project file is silently untracked and
  a fresh clone is broken. **M1 must fix this first.**
- **★ `Producible()` must stay a live closure** over `grid.All` × `catalog.ForStationType`, never a boot-time
  snapshot. `GameBoot` learned this the hard way (see the M8 addendum in the game's LOG): a snapshot never
  learns about a runtime-built Henhouse, and a roster-wide version offers cheesecake at level 1. `CoreHarness`
  inherits the trap.
- **★ `SimProfile` must be read-only to `patch`.** Otherwise an agent lowers the loss by raising `optimality`
  — improving the simulated player instead of the game — and reports success having changed nothing. Reject
  the whole `profile/*` namespace; the gem behaviour fields are the same exploit on new paths.
- **★ Never reorder a Core enum.** `LevelEntryKind.Gems` is value 6, appended so existing serialized `kind:`
  indices stayed valid. Reordering silently reassigns every authored grant in `Levels.asset`.
- **★ Hash the `GameBoot` canary only after gems M03.** M01–M02 already moved `GameBoot`; M03 wires
  `TimeSkip` into the world views and will likely move it again.
- **The baseline already shifted:** level 3 pays `2 gems` instead of `$150`. Any pre-gems intuition about the
  money curve is $150 stale.
- **★ The end-of-session report must be generated from `journal.jsonl`**, never narrated from the agent's
  memory. A long run exhausts a context window, and a compacted context yields a tidier story than the truth.
- **Write surgically, never reserialize.** Reserializing with YamlDotNet reformats whole files and produces
  unreviewable diffs. A one-field change must produce a one-line `git diff`.
- **Enums are ints in the asset, strings in the JSON.** Map them via the real Core enum types (the tool
  compiles them) rather than a hand-maintained table that can drift. `(EffectType)0 → "StationSpeed"`.
- **Dictionary iteration order must never affect a sim result.** Anywhere the agent scans stations or
  resources, sort first — otherwise determinism dies quietly and every downstream number becomes noise.
- **Unity YAML is not standard-conformant.** `%TAG` applies only to the first document, so YamlDotNet throws
  on raw asset files. Strip the three header lines and re-emit `---`.
- **Only six effect types have teeth today**: `StationSpeed`, `StationYield`, `StationCost`,
  `StationQueueDepth`, `XpGain`, `StorageCap`. The rest resolve to passthrough — offering them in the editor
  would let someone author an effect that does nothing.

## Build timers are part of the economy
`plans/build-timers.md` is functionally complete (remaining work is celebration VFX — presentation, not
economy). Treat `StationSO.buildSeconds` as a settled tunable and build against it from M1.

**Why it matters beyond being one more number:** a placed station spends `buildSeconds` under construction —
unusable, but occupying its cell and counting against the cap. So a station bought to relieve `Capacity`
pressure **does not relieve it immediately**. Two consequences the simulator must honour, or it will
misreport the pressure the design leans on hardest:

1. **Construction delay is simulated.** The station exists and is capped from purchase, but produces nothing
   until `buildSeconds` elapses. Pressure keeps accruing throughout.
2. **★ Remedies in flight suppress re-purchase.** Without this the agent sees Capacity pressure still rising
   during construction and buys a second field, then a third — spending the level's income on stations that
   were already on the way. Track a remedy as pending from purchase until it completes, and exclude it from
   the next remedy pick. This is a real behaviour introduced by build timers; a pre-timer sim wouldn't need it.

The `GameBoot` parity canary (M3) should be hashed once the celebration work commits, since that phase may
add another `Init(...)` call.

## Notes on the game (not the tool)

- **Global / universal upgrades are cut.** The user dropped them deliberately — the game's M6 was not skipped
  by accident, and there is nothing to restore. `Station_Workshop.upgrades` is `[]`, no asset authors a global
  effect, and `ValueResolver` passes `OrderPayout` and `BuildCost` straight through. **The tool must not
  report this as a gap:** `Throughput` pressure having only a per-station speed remedy is the design.
  The game's `milestones/VoidDay-Spec-unity/00-summary.md` still lists M6 in its table, which will mislead the
  next cold reader — worth striking there, but that is the game's doc, not ours.
- Spec §9 forbids this tool; amended in M07. §16 may repeat the claim.
- Spec §7 is stale (per the game's M8 log). Pre-existing, unrelated to this work.
