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

## Milestone 04 — The Workbench
**Status:** ✅ Complete · **Date:** 2026-07-22
_(sha not recorded here — the entry ships inside its own commit; the milestone number in the commit message is the link.)_

**Built:**
- **The CLI became an app.** `Api/Server.cs` — ASP.NET Core minimal API + static `wwwroot/`, launched by a new `serve` verb (also bare `dotnet run` with no verb, or with only `--options`). `--port` default 5177. Endpoints, all JSON via **Newtonsoft** (byte-parity with `read`'s output): `GET/POST/DELETE /api/versions`, `GET/PUT /api/config`, `POST /api/sim`, `POST /api/write`. The browser is a pure client of the M01 reader / M02 writer / M03 runner — **zero economy logic in JS.**
- **`wwwroot/`** — Preact + htm vendored as one self-contained ESM file (`vendor/htm-preact-standalone.module.js`, htm@3.1.1+preact@10, MIT, no import map, no CDN, no npm). `index.html` (styles) + `app.js` (the whole workbench). Seven tabs: Global / Resources / Recipes / Stations / Upgrades / Levels / Orders. Editable typed tables; add-row for **recipes** (with input/output ingredient editor), **level rows** (with grant editor), **upgrade tiers** (with effect editor). Effect-type dropdown restricted to the **six with teeth**. Version toolbar: load / Save (in-place) / Save-as / Delete / Run-sim (raw table modal, **no charts** — M06) / Push-to-Unity (change-summary modal).
- **Client-side validation mirrors `BootValidator`** (subset the doc names): thresholds strictly ascending, level-1 has no grants, no duplicate ids, all refs resolve, one reward grant per level, effect `triggerChance` 0–100, six-types-only. Save / Save-as / Push are disabled while invalid; errors listed at the top.
- csproj: `<FrameworkReference Microsoft.AspNetCore.App>` + `InvariantGlobalization`. Project stays `OutputType=Exe`; **CLI verbs `read`/`write`/`sim` unchanged** (regression-verified).

**Verified** (`dotnet run -- serve` + curl/python driver; no editor; **no live browser — the Chrome extension was not connected**):
- Static (`/`, `/app.js`, `/vendor/*`) serve 200; `/api/versions`→`["baseline"]`; bad version name → 400 (path-escape guard).
- **The DoD write flow end to end:** loaded baseline, edited `field.wheatGrow` duration 5→2.5 **and** `field` buildCost 50→40, saved-as `test-tune` (baseline.json **byte-identical** md5 unchanged), dry-run `/api/write` listed exactly those two scalar changes, `--apply` produced **exactly two one-line `git diff`s** in the two assets. Reverted the demo edits — `git status Assets/` clean.
- **Refusal surfaced, not swallowed:** pushing a nested level-threshold edit returns the writer's verbatim `Levels: editing level thresholds or grants is not supported…`; the browser shows it in a "Push refused" modal. Nothing written.
- `validate()` (run under node against baseline + broken configs): baseline 0 errors; descending threshold / level-1 grant / duplicate id / dangling ref / two-reward all caught.
- Add-level-row + Money grant **survives save/load**; `POST /api/sim` returns SimResult + 25-line table (L20); PUT saves in place; DELETE works; baseline delete refused; save-as over an existing name refused (original untouched). **18/18 tests still pass.**
- **NOT verified:** live browser rendering/interaction (extension offline) — the API path the browser drives is fully exercised via curl; the JS is syntax-checked and its pure logic unit-tested under node.

**Deviations from the plan:**
- **★ Two edit surfaces, honestly separated** (the central design call). The workbench edits **every** `BalanceConfig` field and round-trips them through the **version JSON** (save/load — plain serialization, not the writer), which is how "every field editable" and "add a recipe / level row / upgrade tier survives save/load" are met in full. **Push-to-Unity** funnels through the M02 writer, which supports only scalar edits + recipe insertion; `/api/write` therefore returns **either** a change summary **or** the writer's refusal verbatim, and the UI surfaces the refusal. The DoD's push test only exercises scalar edits, which the writer supports. This avoids the "browser silently produces an edit the writer refuses" hidden failure without extending the writer.
- **Chart.js not vendored** — charts are M06's (Do-NOT-Build here). Only htm/preact vendored this milestone. Run-sim shows the runner's raw text table, per the doc's "raw table shown, but no visualisation."

**Tech debt:**
- Push-to-Unity can only reach the writer-supported surface (scalars + recipe insertion). Editing a resource displayName, recipe I/O, upgrade tier/effect, level threshold/grant, or startingResources **saves fine to a version but refuses on push** — inherited straight from M02's writer contract. When a caller genuinely needs to push those, extend the writer (list-index line addressing) with its own tests; the UI already sends the full config, so no client change is needed.
- `/api/write` re-reads all Unity assets per call (fresh reader + `current`). Fine for a localhost dev tool; not optimised.
- Number inputs coerce to JS `Number`; Newtonsoft re-coerces to the field's int/float on the way back (verified round-trip). No client-side int/float distinction is enforced.

**Assumptions:**
- The vendored `htm/preact/standalone` single file is self-sufficient (no import map). Confirmed: exports `html`/`render`/`useState`/`useEffect`/… and both JS files pass `node --check`; live-browser render was **not** confirmable (extension offline).
- `ContentRootPath = <tool dir>` makes `wwwroot/` resolve regardless of the caller's cwd — holds for `dotnet run --project …` from repo root (the DoD's invocation) and from the project dir.

**Gotchas for later milestones:**
- **★ API + wwwroot layout is the M06/M07 inheritance.** Endpoints are `GET/PUT /api/config`, `GET/POST/DELETE /api/versions`, `POST /api/sim`, `POST /api/write`. `wwwroot/vendor/` is where vendored ESM lives — **M06 adds `chart.js` here** (same no-CDN, no-build rule) and builds the charts/A-B/live-session view on top of `app.js`'s tab shell + `TAB_VIEWS` map. Sim already returns `{ result: SimResult, table }`, so M06's charts read `result` (the M03 keys) with no server change.
- **Server JSON is Newtonsoft, not System.Text.Json** — deliberately, because `BalanceConfig` uses public **fields** (STJ ignores fields by default) and version files must stay byte-identical to `read`'s output. Any new endpoint must keep using `JsonConvert` or the wire format drifts.
- **`serve` verb detection:** the first arg is the verb **unless it starts with `--`** (then it's `serve` + options). Don't add a verb beginning with `--`.
- **Version files are the working store.** `versions/*.json` is git-tracked; the workbench never writes Unity except through the gated Push. `baseline` is protected from delete and from save-as overwrite.
- **Push honesty is load-bearing.** If M06/M07 ever bypass `/api/write`'s dry-run and write directly, the "refusal surfaced in the UI" guarantee is lost. Keep the plan→(summary|refusal)→confirm→apply flow.

## Milestone 05 — Agent Primitives
**Status:** ✅ Complete · **Date:** 2026-07-22
_(sha not recorded here — the entry ships inside its own commit; the milestone number in the commit message is the link.)_

**Built:**
- `Agent/` (6 files): `Goal.cs`/`GoalEvaluator.cs` (the loss), `ConfigPath.cs` (path grammar), `Bounds.cs` + `Patch.cs` (guardrails), `Suggest.cs` (pressure→knob map), `Sweep.cs` (1-D sensitivity), `Journal.cs` (`runs.jsonl`).
- **Loss:** 8 metrics — `level.durationMinutes`, `total.minutesToLevel`, `pressure.share` (min/max), `pressure.rank`, `level.moneyAtEntry`/`moneyAtExit`, `gems.compressionShare` (min/max), `gems.heldAtExit`. Scale-free monotonic normaliser (over `(v-max)/max(|max|,|v|)`, under `(min-v)/max(|min|,|v|)`, both in `[0,1)`). Loss = Σ(violation×weight), **always with a per-target breakdown**. Reads `Pressure` GROSS of gem relief (M03 invariant); parametrised keys (`Capacity:field`) aggregated into families (`Capacity`) so a goal names a family.
- **`suggest`:** dominant-pressure→knob map. Storage branch exact to the doc; other families derived from config. **★ relief branch:** when `GemRelief/dominantPressure ≥ 0.15`, gem knobs (`gems.secondsPerGem`/`startingGems`/`minGemCost`) join a **separate** `relief` list framed "HIDES the bottleneck, does not remove it".
- **`patch`:** config→config on a deep clone, never Unity. Rejects the whole `profile/*` namespace by prefix, undeclared-bound paths, and out-of-bounds values — atomic fail-whole.
- **`bounds.json`:** wildcard patterns (`recipes/*/duration`, `stations/*/{buildCost,cap,…}`, `upgrades/*/tiers[*].cost` + `effects[*].amount`, singleton global/xp/gems/orders). Bounds.json is the movable-knob allowlist.
- **CLI:** `eval` / `patch` / `suggest` / `sweep` / `report`, all `--json`. `AGENTS.md` documents verbs, goal schema, path grammar, guardrails, worked loop.
- Tests: `GoalLossIsMonotonic`, `PatchRejectsOutOfBounds`, `PatchRejectsProfilePaths`, `PatchRejectsGemPolicyPaths`, `PatchAppliesInBoundsToClone`, `SuggestFlagsGemReliefWhenLarge`. **26/26 pass.**

**Verified** (`dotnet test` + built dll; no Unity, by design):
- `eval baseline` → loss 24.54 with a per-target table across all 5 metric families. `pressure.rank Capacity` contribution rises 2.0→2.5 when `stations/field/cap=1` makes Yield lead. `pressure.share Storage min 0.3` penalises the levels with too little Storage.
- `patch profile/optimality` and `profile/gemPolicy` **rejected**, naming the read-only rule; `recipes/field.wheatGrow/duration=9999` **rejected**, naming the bound; in-bounds patch writes a config→config JSON, Unity untouched.
- `suggest` on an engineered storage-flood config → **Storage dominant** + exactly the three doc storage knobs. `sweep stations/field/buildCost 20→200` → sensible loss curve. `runs.jsonl` grows one line per `eval`.
- **NOT verified end-to-end:** the ★ gem-relief branch of `suggest` — the M03 player is conservative and won't spend gems in reachable CLI configs, and `profile/*` is read-only so the spend can't be forced from the tool. Locked instead with a **direct unit test** on a synthesised `SimResult` (large relief ⇒ gem knobs listed; small relief ⇒ not). Honest gap, covered by test.

**Deviations from the plan:**
- **`patch` requires a declared bound** (not just "refuses out-of-bounds"): a path with no `bounds.json` entry is rejected, making bounds.json the allowlist of movable knobs. Stricter than the doc's literal wording, in the spirit of "guardrails are structural." Loosening = add bounds entries.
- **`eval` is single-seed** (`--seed`, default 1). The doc allows running the configured seed count but assigns median/percentile aggregation to M06; kept single-seed for KISS. M06 owns multi-seed.
- **Non-Storage suggest maps are config-derived heuristics** (only Storage is doc-specified). Documented as such in AGENTS.md.

**Tech debt:**
- `sweep` runs a full sim per step, no parallelism — fine for a localhost primitive.
- `total.minutesToLevel` for an unreached level falls back to `TotalSeconds` (treated as a large violation); per-level unreached adds a flat 1.0 unit. Adequate; not tuned.

**Assumptions:**
- Newtonsoft binds camelCase goal/patch JSON to PascalCase C# fields case-insensitively (holds; the tool already relies on this for `BalanceConfig`).
- Halving all recipe durations lowers a duration-capped loss **summed over a range** even though one level can rise on its own (order-stream shift — an M03 reality). The monotonic test uses a range for exactly this reason; a single-level assertion would be flaky.

**Gotchas for later milestones:**
- **★ Loss data contract (M06/M07 inherit):** `LossReport { Loss, Targets[] }`, each `TargetResult { Metric, Scope, Bound, Measured, Violation, Weight, Contribution, Detail }`. `eval --json` emits this; M06 charts read it. Ranged targets **sum** one violation per in-range level — scope width scales contribution, so **weight is the balancing lever**.
- **★ Path grammar (patch/sweep):** singleton `root.field`; collection `collection/id/field` (`/` delimits so ids keep their dots); nested `tiers[0].effects[0].amount`. Adding a patchable knob = add a `bounds.json` pattern; there is no separate allowlist to update.
- **★ `profile/*` rejected by NAMESPACE prefix, never a field list** — `gemPolicy`/`gemReserve`/`minSkipSeconds` and any future profile field are caught the same way. Do not "helpfully" convert this to an allowlist.
- **`runs.jsonl`** lives at `tools/VoidDay.Balance/runs.jsonl`, **gitignored** (runtime log, grows per eval). M07 restructures it into sessions; today it is a flat global log. Never stage it.
- **Only `eval` journals.** `sweep` runs N internal evals but records none (it is a sensitivity scan, not a decision). `patch` writes a config, not a journal line.
- **No automated search built** (deliberate — Do NOT Build). The verbs are primitives an external agent composes; the M07 skill drives the loop.

## Milestone 06 — Reports & Comparison
**Status:** ✅ Complete · **Date:** 2026-07-22
_(sha not recorded here — the entry ships inside its own commit; the milestone number in the commit message is the link.)_

**Built:**
- **`Sim/SimSweep.cs`** — N seeds (fixed set `1..N`) via `Parallel.For` into a seed-ordered array, reduced to median/p10/p90 per level (duration, acting, waiting, money entry/exit) + per-family gross pressure. `PurchaseAgg` = median/p10/p90 of the level each remedy is *first* bought (seeds that never bought it are excluded, not counted as level 0). Individual `SimResult`s retained. Adds NO sim behaviour — pure aggregation over M03's runner.
- **`LevelReport.PressureFamilies()`** (Schema) — the parametrised-key→family rule (`Capacity:field`→`Capacity`) moved onto the data object so the heatmap and the M05 loss aggregate pressure identically. `GoalEvaluator.Families` unchanged in behaviour.
- **`POST /api/sim` extended:** `seeds > 1` ⇒ `{ sweep: aggregate, seeds: [{seed, levelReached, totalMinutes, stop, table}] }`; absent/1 ⇒ the original single-seed `{result, table}` (back-compat).
- **Chart.js 4.4.6 UMD vendored** → `wwwroot/vendor/chart.umd.js` (MIT, 206 KB), loaded as a `<script>` global — no CDN, no npm, no build.
- **`wwwroot/app.js` Reports mode** (Edit/Reports header toggle): A/B version pickers + seed count + profile + Run. Five views — (1) time/level median bar + p10–p90 band, (2) money entry/exit with band, (3) acting-vs-waiting stacked, (4) **pressure heatmap** (level×family, gross), (5) purchase timeline (first-bought level floating `[p10,p90]` bar). A/B overlays charts 1 & 3 and adds a **per-level B−A delta table**. Seed strip → click opens that seed's exact CLI table in a modal.
- Tests: `SweepIsDeterministic` (byte-identical aggregate despite `Parallel.For`), `SweepBandIsNonDegenerate`, `SweepSeedMatchesSingleRun`. **29/29 pass.**

**Verified** (`dotnet test` + live server via python/curl driver; no Unity; **no live browser — Chrome extension not connected**):
- 30-seed sweep on baseline: median 57 m, band non-degenerate (19/20 duration bands have width), families aggregate correctly, purchase timeline populated, drill-in seed table **byte-equal** to the single-seed `/api/sim` and to `sim --seed n`.
- **Control passes exactly:** A/B of baseline vs an identical copy → every level delta **exactly 0** (renders "—").
- Chart builders exercised in node against the live sweep JSON (single + A/B) → all produce well-formed datasets, no undefined field access. `node --check` clean on `app.js` + `chart.umd.js`.
- **NOT verified:** live browser rendering (no extension) — the data the charts consume is confirmed correct; the visual draw is not.

**Deviations from the plan:**
- **★ Three directional DoD/How-to-Test cases do not fire on baseline — because baseline's economy does not exercise those paths, exactly as M03's LOG documented.** The comparison machinery is correct (control = 0; the slow-orders case below shows a real, dramatic delta), but:
  - *Halved build costs* → does **not** drop time-to-level or `Capacity` pressure. Baseline runs on the pre-placed field's self-sustaining corn loop and barely builds, so build cost is nearly inert (M03: "orders are always corn and never require a built station").
  - *All `unlockLevel`=10* → **no** `Unlock`/`Supply` pressure appears (a single-seed run emits only `Capacity:field`/`Yield:field`); the player never needs a locked station, so locking them costs nothing.
  - *`refillSeconds`=600, `slotCount`=1* → `OrderRefill` climbs **enormously** (0→~14 000 s, dominant — this one strongly confirms the heatmap "explain a category from the config"), but `Income` does **not** climb (pressure categories partition per-slice; OrderRefill absorbs the wait).
  These are M03 sim facts, and M06 must not change the sim. **Not fixed by design.** The verifiable heatmap/A-B evidence comes from the slow-orders case and the exact-zero control.
- **Heatmap is an HTML/CSS colour table, not a Chart.js chart** — a 2-D grid needs no extra vendored plugin (`chartjs-chart-matrix`) and reads better. The other four views are Chart.js.
- **`/api/sim` kept back-compatible** rather than replaced — single-seed clients (CLI-shaped modal, M04) still get `{result, table}`.

**Tech debt:**
- No parallelism cap on `SimSweep` — `Parallel.For` uses the default scheduler; fine for 30 seeds on localhost.
- A/B overlay is limited to charts 1 (time) and 3 (money) per the doc; charts 2/4/5 show config A only.

**Assumptions:**
- The vendored Chart.js UMD auto-registers all controllers on load (the full `chart.umd.js` build does) — so `new window.Chart(canvas, cfg)` needs no `Chart.register(...)`. Not confirmed in a live browser (extension offline); the config objects are validated structurally in node.
- `Parallel.For` never makes a seed's result depend on scheduling — held by SimSweep determinism test (byte-identical aggregate across two runs).

**Gotchas for later milestones:**
- **★ Sweep endpoint contract (M07 inherits):** `POST /api/sim` with `seeds>1` returns `{ sweep, seeds[] }`; `sweep` is the `SimSweep.Aggregate` (`Levels[].{Duration,Acting,Waiting,MoneyEntry,MoneyExit}` as `{Median,P10,P90}`, `Levels[].Pressure{family→Stat}` gross, `Purchases[].FirstLevel` Stat, `TotalMinutes`/`LevelReached` Stat). `seeds[]` carries each run's rendered `table` so a seed opens without re-running.
- **★ The pressure-family rule lives on `LevelReport.PressureFamilies()`** now — the heatmap and the loss share it. If a new parametrised pressure key is added, both pick it up for free; do not re-implement the split.
- **★ Baseline is build-cost / unlock insensitive** (self-sustaining pre-placed corn field). Any milestone that wants to *demonstrate* Capacity/Unlock/Supply pressure must use a config that forces a built station into the money loop (e.g. a sellable good only a buildable station can produce), not baseline. This is the single most likely source of "the tool looks broken" confusion — it is the game, not the tool.
- Chart.js is a **UMD global** (`window.Chart`), not an ESM import — it is loaded by a `<script>` before `app.js`. A new chart file must keep that pattern (the CSP/no-CDN rule bans an import from a CDN; the vendored UMD is the sanctioned path).
