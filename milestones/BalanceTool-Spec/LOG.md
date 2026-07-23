# VoidDay Balance Tool — Implementation Log

Running record across milestones. **Read this first when picking up cold.**

Spec: `docs/BalanceTool-Spec.md` · Milestones: `00-summary.md` · Decisions: `docs/decisions/04-balance-tool.md`

---

## Before Milestone 01 — context carried in from design (2026-07-22)

No code exists yet. What a cold start needs to know:

**The one-way dependency is the defining constraint.** The tool may read the Unity project; nothing under
`Assets/` may learn the tool exists. No asmdef, no editor menu item, no shared schema, no changes to
`GameBoot`. `git status` on `Assets/` stays clean except for assets the tool is explicitly asked to write.

**Verified during design** (don't re-derive):
- `dotnet` 9.0.304 and 9.0.203 are installed; `node` v22.17.1.
- `com.unity.nuget.newtonsoft-json` is already resolved in `Library/PackageCache` (transitive via the MCP
  package) and referenced by the generated csprojs — but the tool takes Newtonsoft from NuGet independently.
- `VoidDay.Core.asmdef` has `noEngineReferences: true`, so `Assets/Core/**/*.cs` compiles outside Unity. This
  is the assumption the whole tool rests on; M1 proves it.
- `.asset` bodies are plain, consistently-indented YAML. Checked against `Upgrade_Field_Speed.asset` (three
  tiers of nested effect structs) and `Levels.asset` (nested grant lists). Unity's non-standard parts
  (`%YAML`, `%TAG`, `!u!114 &11400000`) are confined to document headers.
- Pre-placed stations in `Farm.unity` are prefab instances; `m_SourcePrefab` GUIDs map straight to
  `Assets/Prefabs/Stations/*.prefab`. Field `e9b5f8337186c4b57a96cb1a914f462f`, Silo
  `fa1786f3fe3f34ffbaf9191282680326`, OrderBoard `36bebf2fe7f5445fdb0e4e2796fb3ede`.
- Only four effect types are authored anywhere: `StationSpeed` (0), `StationYield` (2), `StationQueueDepth`
  (3), `StorageCap` (14). Six have teeth in `ValueResolver` (those four plus `StationCost` and `XpGain`).

**★ `.gitignore` blocks the tool.** Line 22 is a bare `*.csproj`, there is no `bin/` rule, and `/[Oo]bj/` is
root-anchored. M1 must add `!tools/**/*.csproj`, `tools/**/bin/`, `tools/**/obj/` **first**, or the project
file is silently untracked and a fresh clone is broken.

**★ Build timers are part of the economy.** `plans/build-timers.md` is functionally complete (remaining work
is celebration VFX — presentation, not economy). `StationSO.buildSeconds` is a settled tunable: a placed
station spends that long under construction, unusable, while occupying its cell and counting against the cap.

Two simulation consequences, both recorded in the spec:
1. **Construction delay is simulated** — the station is capped from purchase but produces nothing until
   `buildSeconds` elapses, and pressure keeps accruing throughout (correct: the player really is still stuck).
2. **★ Remedies in flight suppress re-purchase** — otherwise the agent watches Capacity pressure keep climbing
   during construction and buys a second field, then a third, spending the level's income on stations that
   were already on the way. Track a purchase as pending until it completes and exclude it from the next pick.
   A pre-timer simulation would not have needed this.

Hash the `GameBoot` parity canary (M3) once the celebration work commits — that phase may add another
`Init(...)` call.

---

## Addendum — gems landed mid-planning (2026-07-22)

The spec and milestone docs were reconciled against **as-built gem code**, not the plan. What a cold start
needs:

**Gems M01–M02 are committed** (`fe0e83c`, `a4b8ad2`). **Core is finished for all three timer kinds** —
`GemPurse`, `TimerRef`, `TimeSkip` (`CanSkip` / `CostFor` / `Skip`), and a read+write pair on `JobSystem`,
`BuildSystem` and `OrderBoard`. Gems M03 is **View-only** (TimerWidget collider, cost label, `InputRouter`
routing), so it does not gate the simulator's gem model.

**★ M1 and M2 are not blocked by gems at all.** The reader and writer touch assets, not `GameBoot`. Only M3
should wait, and only for one reason: the parity canary hash. M01–M02 already moved `GameBoot` (purse
constructed after `Wallet`, before `Progression`; `EmitCurrent` beside the wallet's) and M03 will likely move
it again wiring `TimeSkip` into `WorldState` / `ConstructionSiteView`.

**★ Call the real `TimeSkip`, never a copy of the pricing formula.** `CostFor` is
`max(minGemCost, ceil(remaining / secondsPerGem))`. The three owner reads return **-1 when there is no live
timer of that kind** — that sentinel is what lets one rule serve three unrelated owners; branch on it, not on
a zero. (`TryGetHeadProgress` cannot serve: it reports `0` for a *complete* head too.)

**★ Pressure is gross of gem relief.** See `00-summary.md` decision 9. The single rule most likely to be
"simplified" into a bug.

**★ The baseline moved.** Level 3 pays `2 gems` instead of `$150` — gems M01 could not add a grant, since
every level already had one and a level may hold at most one reward. The curve pays $150 less across a run.
The gems LOG calls it "a real (small) economy nerf nobody asked for"; it is a good first question for the
finished tool, not a bug to fix.

**★ `LevelEntryKind.Gems` is value 6, appended.** Never reorder that enum.

**Verified, correcting an earlier note:** the `.gitignore` bare `*.csproj` is **line 20**, not 22. Still no
`bin/` rule, `/[Oo]bj/` still root-anchored — the trap is real, the line number was not.

**★ Unity playmode verification cannot be trusted to screenshots here.** The gems LOG records that the player
loop freezes at `frameCount = 1` when the editor cannot be foregrounded, and that
`Application.runInBackground = true` reads `True` and **does not help**. `screenshot-game-view` returns frame
1 forever — a plausible, stale image. Verify by reading component state via `script-execute`. This matters
less for this tool than for the game (verification here is `dotnet run`), but it applies to any playmode
cross-check.

**`ProjectSettings.asset` is dirty and not ours** — DOTween writes a scripting define on every AssetDatabase
refresh. Left uncommitted deliberately. Never stage it.

---

**Notes on the game, not the tool:**
- **Global / universal upgrades are cut** — a deliberate design decision, not an unbuilt milestone. There is
  nothing to restore and nothing to wait for. `Station_Workshop.upgrades` is `[]`; `ValueResolver` passes
  `OrderPayout` and `BuildCost` through untouched. **Do not report this as a gap:** `Throughput` pressure's
  only remedy being a per-station speed tier is the intended design. (The game's own milestone summary still
  lists M6 in its table and will mislead a cold reader — worth striking, but that is the game's doc.)
- Spec §9 forbids this tool; amended in M07. §16 may repeat the claim.
- Spec §7 is stale (per the game's M8 log). Pre-existing, unrelated to this work.

**Traps to carry forward** — see `00-summary.md` *Gotchas* for the full list. The five that will cost the
most if missed: `Producible()` must stay a live closure; a remedy in flight must suppress re-purchase;
`SimProfile` must be read-only to `patch`; the session report must be generated from the journal rather than
narrated; and dictionary iteration order must never affect a sim result.

---

<!--
Per completed milestone, append a section in this shape (matching the game's LOG.md):

## Milestone NN — Name
**Status:** ✅ Complete · **Commit:** `sha` · **Date:** YYYY-MM-DD

**Built:** what landed, by area.
**Verified:** what was actually checked, and how. Say plainly what was NOT verified.
**Deviations from the plan:** what differed and *why*.
**Tech debt:** what was left.
**Gotchas for later milestones:** traps discovered. Mark load-bearing ones with ★.
-->

## Milestone 01 — Read the Economy
**Status:** ✅ Complete · **Date:** 2026-07-22
_(sha not recorded here — the entry ships inside its own commit; the milestone number in the commit message is the link.)_

**Built:**
- `.gitignore` fix FIRST: `!tools/**/*.csproj`, `tools/**/bin/`, `tools/**/obj/` (verified a tools csproj is no longer swept up by the bare `*.csproj` / root-anchored `/[Oo]bj/`).
- `tools/VoidDay.Balance/` (net9.0 exe, YamlDotNet 16.2.1 + Newtonsoft.Json 13.0.3), globbing `../../Assets/Core/**/*.cs`.
  - `Unity/GuidIndex.cs` (scan `Assets/**/*.meta` → guid→path), `Unity/AssetReader.cs` (5-line fail-loud preprocessor + YamlDotNet, ref resolution), `Unity/SceneScanner.cs` (line-scan `Farm.unity` `m_SourcePrefab`, keep `Assets/Prefabs/Stations/`), `Unity/RawAssets.cs` (raw camelCase DTOs), `Unity/EconomyReader.cs` (traversal + enum mapping via real Core enums), `Schema/BalanceConfig.cs`, `Cli/Program.cs` (`read --project --out`).
- `tools/VoidDay.Balance.Tests/` (xUnit) — the two spec-mandated M01 guards.
- `tools/VoidDay.Balance/versions/baseline.json`.

**Verified:**
- **Core compiles standalone under net9.0** — the whole-tool assumption. Only Nullable-context warnings, zero errors.
- `read` → 8 stations, 12 recipes, 8 upgrades, 10 resources, 20 levels. Every DoD spot-check matches the inspector: grid 20/30/1, refund 0.5, storage 30; gems 5/30/1; xp 2/5; orders all 10 fields; Field cap 2 / cost 50 / unlock 1 / queue 3; `field.wheatGrow` dur 5; `silo.cap` tiers 120/300/700, effect amt 25; 20 thresholds; **level 3 = 2 gems (not $150)**; startingStations Field/Silo/OrderBoard = 1 each (scanned).
- Enums are strings (no int discriminators). Fail-loud confirmed: hiding a referenced resource's `.meta` throws `Unresolvable GUID …` naming the guid; `git status Assets/` clean after restore. Two `read` runs byte-identical (deterministic). `dotnet test` → 2/2 pass.
- **NOT verified:** in-editor Unity behaviour (this run never opens the editor, by design).

**Deviations from the plan:** none of substance. Schema type `ResourceAmount` renamed to `ResourceQuantity` to avoid a clash with `VoidDay.Core.Model.ResourceAmount` (the tool compiles Core, so both are in scope).

**Tech debt:** the tool's csproj sets `Nullable=enable`, so Core's non-nullable-context code emits ~30 harmless warnings on every build. Cosmetic; left as-is.

**Gotchas for later milestones:**
- ★ **`buildSeconds` is absent from all 8 station assets** (the field post-dates them); `perStationBuilt` is absent from `XpConfig.asset`. Unity applies the SO field initializer (15f, 5) to such fields, so the *game* sees those defaults — confirmed by `plans/build-timers.md` ("`buildSeconds` defaults to 15 on every existing Station SO"). The reader mirrors this: raw DTO defaults equal the SO initializers, so an absent scalar reproduces exactly what Unity loads. **A present-but-unresolvable object *reference* still throws loud** — the two paths are distinct. **M03's construction-delay sim inherits buildSeconds = 15 for every station.** If any station's asset gains an explicit `buildSeconds:` later, the reader picks it up automatically.
- ★ **Schema shape (`BalanceConfig` + sub-configs) is set here; M02 writer and M03 sim inherit it.** Enums are names, references are resolved to ids, `LevelGrantConfig.TargetStation` is a stationType string or `null` (= all stations).
- **`versions/` lives at `tools/VoidDay.Balance/versions/`** (spec arch diagram + Config table), not repo root. `--out` defaults there regardless of cwd; `--project` is discovered by walking up to the folder with `Assets/` + `.gitignore`.
- Resources are reached only via recipe I/O + `startingResources`; the resource cache must be fully populated (recipes/upgrades projected) before resources are emitted (a bug caught in build: resources projected too early → only the 2 starting resources).
- Two resource assets are `CropSO` (a `ResourceSO` subclass with an extra `cropSprite`); the reader reads by field name and ignores unmatched properties, so the subclass is transparent.

## Milestone 02 — Write It Back
**Status:** ✅ Complete · **Date:** 2026-07-22
_(sha not recorded here — the entry ships inside its own commit; the milestone number in the commit message is the link.)_

**Built:**
- `Unity/AssetWriter.cs` — surgical writer. `Plan(incoming)` validates + diffs against a fresh read (writes nothing); `Apply(plan)` performs edits. Types: `WritePlan`, `ScalarChange`, `RecipeInsertion`, `WriteRefusedException`.
  - **Scalar edits** replace only the value after `: ` on the one matching top-level line (2-space indent match; asserts exactly one match). **Never reserializes** — a one-field change is a one-line `git diff`.
  - **Absent-scalar edits** (`perStationBuilt`, `buildSeconds` — absent from assets, SO-default) append one line at EOF; Unity reads SO fields by name (order-independent), so this is a valid 1-line diff.
  - **Recipe insertion** writes a `RecipeSO` `.asset` (m_Script guid *stolen from an existing recipe*, not hardcoded) + a `.meta` with a fresh `Guid.NewGuid("N")`, then appends one reference line to the owning `StationSO.recipes` block.
  - **Refusals abort before the first byte:** schemaVersion mismatch; resource/station id matching no asset; any nested-collection edit, deletion, or resource/station creation. Fail loud, fail whole.
- Extended `EconomyReader`: exposes `RecipeGuidById` / `ResourceGuidById` / `StationGuidByType` source maps + `XpConfigPath` / `OrderConfigPath` / `LevelsPath` + `Guids` — so a config path resolves to the exact asset. `BalanceConfig.CurrentSchemaVersion` const.
- `Program.cs`: `write --config X [--project ..] [--apply]`; **dry-run is the default**, prints `asset field: old → new` per change.
- `WriterTests.cs` — 6 tests (round-trip plans zero changes; scalar edit = 1 change; new recipe = 1 insertion; schemaVersion/bogus-id/nested-level refusals). **8/8 total pass.**

**Verified** (dotnet build/run/test + `git diff`; no editor, by design):
- No-op write → `no changes`, `Assets/` clean. Halving `field.wheatGrow` 5→2.5 → **exactly one changed line**; re-read shows 2.5; second write `no changes`.
- Bogus resource id and `schemaVersion: 999` → both `refused:` (exit 1), `Assets/` untouched.
- New recipe `field.testGrow` → valid asset+meta+1-line wiring; full re-read (13 recipes, wired into field) proves the reference graph is intact — the same traversal `GameBoot` runs, which is the boot-validity proxy this run can give.
- **NOT verified:** in-editor `BootValidator`/playmode (this run never opens Unity).

**Deviations from the plan:** Level-row and upgrade-tier *insertion* (listed under "Build This") were **not** built — no DoD/acceptance case exercises them and each carries real nested-YAML risk under a minimal-diff bar. Deferred to when a caller needs them (likely M4). Recipe insertion (the DoD-tested, boot-validated path) is complete.

**Tech debt:** ★ **Nested-collection edits are refused, not written** — changing a recipe's inputs/outputs, an upgrade tier/effect, a level `xpThreshold`/grant, or `startingResources` aborts loud (`… not supported by the M2 writer`). These are legitimate balance knobs; M4's workbench will need a surgical path (list-index line addressing) or a scoped block-replace. Absent-scalar append (buildSeconds/perStationBuilt) *is* supported.

**Assumptions:** (1) Unity deserializes SO fields by name, order-independent — so appending an absent scalar at EOF is safe (holds for `MonoBehaviour` YAML; if ever false, the appended field would be ignored, not corrupt). (2) The stolen `m_Script` guid + `--- !u!114 &11400000 / mainObjectFileID: 11400000` header make a boot-valid `RecipeSO` — verified structurally + by re-read, not by the in-editor importer.

**Gotchas for later milestones:**
- ★ **Writer contract (M4 inherits):** the writer edits by re-reading current state and diffing incoming; **only changed fields are written**, which is what makes the no-op round-trip byte-identical. Any new editable surface must add both a diff branch and a line/append target, or be refused — never silently dropped.
- ★ **Gems are authored top-level on `GameConfig.asset`** (`startingGems`/`secondsPerGem`/`minGemCost`) even though the schema groups them under `Gems`. The writer targets `GameConfigPath` for them; a future reader/writer split must preserve that.
- **CLI arg parsing uses the top-level `args`, not `Environment.GetCommandLineArgs()`** (the latter mis-indexes under `dotnet run`). Run verbs as `dotnet run -- <verb> …`; `dotnet run --nologo/-v` leaks those flags into `args`.
- New recipe assets land at `Assets/Data/SO/Recipe_<id-with-dots→underscores>.asset` with `m_Name` matching. Filename is cosmetic to the game (referenced by guid), but keep the convention for reviewability.

## Milestone 03 — Simulate
**Status:** ✅ Complete · **Date:** 2026-07-22
_(sha not recorded here — the entry ships inside its own commit; the milestone number in the commit message is the link.)_

**Built:**
- `Sim/` (10 files): `CoreHarness` (mirrors `GameBoot.Start()` wiring order exactly, header names the file + commit `4b13863` + reconcile date), `ConfigProjector` (BalanceConfig→Core models, mirrors `ModelProjector` incl `EffectConfig`→`Effect`), `SimClock` (1s step / exact jump to `min(job end, construction end, order refill)` — construction IS in the jump set), `PressureLedger` (8 categories, GROSS + separate `GemRelief`, net derived), `RecipeChain` (demand-driven backward chaining, memoised cycle guard that throws `RecipeCycleException` on a real A↔B cycle but allows self-grow), `PlayerAgent` (bottleneck-seeker, optimality dial, gems as the consumable remedy calling the real `TimeSkip`), `MetricsCollector` (subscribes the real `EventBus`), `SimRunner` (the loop + text table).
- `Schema/SimProfile.cs`, `Schema/SimResult.cs` (`LevelReport`/`PurchaseRecord`/`StopReason`).
- CLI `sim --config <name|file> [--profile typical|perfect] [--seed N] [--optimality X] [--no-gems] [--json]`.
- Tests: `GameBootParityTests` (canary) + 9 sim tests. **18/18 pass** (2 M01 + 6 M02 + 10 new).

**Verified** (`dotnet build`/`run`/`test`; no Unity editor, by design):
- `sim --config baseline --seed 1` → per-level table, reaches L20 in 54.1m; L1→2 short, later levels longer; bottlenecks justifiable. Two `--json` runs byte-identical (determinism). Optimality monotonic: 47.0m(1.0) < 54.1m(0.65) < 89.3m(0.3). Cycle config throws loudly, no hang. **Assets/ stays clean** (agnosticism holds).
- Capacity↔Yield swap confirmed (field cap 6 → `Capacity:field`; baseline cap 2 → `Yield:field`). Storage/OrderRefill detection confirmed with a silo-flood config. Gem relief confirmed gross (`GemRelief`/`SecondsPurchased` populate; pressure never netted).
- **NOT verified:** in-editor Unity (this run never opens it). No multi-seed/eval/UI (M4–M7).

**Deviations from the plan:**
- **★ `buildCost = 999999` does NOT stall baseline** (DoD/QA-17 expected a stall). It reaches L20 in 212m. Root cause: the preplaced field's `cornGrow` (corn→2corn) + corn being sellable is a **complete self-sustaining money loop** — only corn is producible+sellable, so orders are always corn and never require a *built* station. This is CORRECT sim behaviour and exactly the kind of finding the tool exists to surface, not a bug. Same root cause makes **QA-8's "Storage dominates at cap 10" not hold** for baseline: early game is *production-constrained* (corn scarce, sold immediately), not storage-constrained. The Storage/stall GUARDS both work — proven with a silo-flood config (Storage dominates) and an unwinnable config (`SimStallGuardFires`, corn needs an unobtainable input → stall). The automated tests use those genuinely-triggering configs rather than the baseline QA scenarios.
- Pressure accrues **continuously** (over every clock slice, not only idle) per the spec's "the player is always present, pressure accrues continuously" — Storage per blocked station always; Capacity/Supply/Yield/Throughput + diagnostics only when the player has no productive action.

**Tech debt:**
- `PressureIsGrossOfGemRelief` is tested as a **ledger invariant** (Accrue never subtracts; AccrueGemRelief adds gross+relief) rather than a full-baseline 0-vs-50-gem equality — an end-to-end equality is fragile because faster leveling under gems shifts the order-generation stream, so the two runs legitimately diverge. The invariant is the load-bearing rule; the gross-accrual is correct by construction in `SimRunner`.
- Absolute times are a model, not a player (spec risk 3) — trust *relative* answers.

**Gotchas for later milestones:**
- **★ Second mirror surface, NOT covered by the GameBoot canary:** `CoreHarness` also mirrors two *Systems-layer* behaviours essential to the sim — `ProgressionSystem` (XP awards on `JobCollected`/`OrderFulfilled`/`StationBuilt` — without it nothing ever levels) and `UpgradesSystem` (register a runtime-built station's upgrade tracks on `StationBuilt`). If either `.cs` changes, the canary won't fire. Grep `Assets/Systems/ProgressionSystem.cs` / `UpgradesSystem.cs` when reconciling.
- **★ Parity canary frozen against `GameBoot.cs` @ commit `4b13863`** (last commit to touch it). Normalized-SHA256 (CRLF/CR→LF) `052ff334f060b74f5ebb86d78804a2940c2a824c9fc368b87f22c2c3c663cb96`, baked in `GameBootParityTests.ExpectedHash`. All named in-flight GameBoot movers (gems M01–M03 `fe0e83c`/`a4b8ad2`/`1822352`, Collection-Particles M01–M03 `734e546`/`aedc6a2`/`4b13863`) are committed. If it fires: reconcile `CoreHarness` then reprint the new hash from the failure message.
- **Sim data contract (M5/M6 inherit):** `SimResult`/`LevelReport` shape — `Pressure` gross + `GemRelief` separate, net derived; category keys are `Storage`/`Throughput`/`Income`/`OrderRefill`/`Unlock` and the parametrised `Capacity:<type>`/`Supply:<good>`/`Yield:<type>`. `eval`/`suggest` read these keys.
- **Two Random streams:** order = `new Random(seed)`; agent = `new Random(seed*1103515245+12345)`. Never `HashCode.Combine` (per-process randomized → breaks determinism).
- Only the six effect types with teeth are honoured (inherited from `ValueResolver`); the sim reads resolved values, so it inherits this for free.
- `SimProfile` is the player, not the game — the whole `profile/*` namespace must stay read-only to `patch` (M5), gem behaviour fields included.
