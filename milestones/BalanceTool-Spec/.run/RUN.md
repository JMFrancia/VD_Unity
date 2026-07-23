# BalanceTool-Spec — Run 2026-07-22

**Range:** 01–07 · **Status:** ✅ complete
**Estimate:** ~2.5–5 h wall-clock · ~1.6–3.0M tokens · confidence medium
**Dominant driver:** M03 (CoreHarness reconciling real Core against GameBoot).

## Resolved up front
- **QA scope → ALL QA-1…QA-26.** M07's "run QA-1…QA-21" phrasing is stale (predates the gem QA cases). The M07 acceptance replay covers every case through QA-26, including the gem-policy cases QA-22–26. Pass this to the M07 subagent.

## Predicted stop points (forecast)
1. M03 — layer boundary: `CoreHarness` mirroring `GameBoot.Start()` (wiring order, `Producible()` live closure, `-1` sentinel across three timer owners).
2. M03 — parity-canary timing: hashing `GameBoot.cs` last, before confirming in-flight gems/VFX committed.
3. M01 — Core failing to compile standalone under net9.0, or YAML preprocessor hitting an unhandled asset form.
4. M02 — structural insertion (new RecipeSO + generated .meta GUID) with minimal diffs.
5. M07 — doc-vs-spec (QA numbering, now resolved above).

## ⚠ Live environment observation (during M01)
- **The user is committing to this tree in parallel.** Mid-M01, `bc42d4a` (game fix, `Assets/View/*`) and `db32bfa` ("Readme, milestones, skills") landed from another process. `db32bfa` swept `.run/RUN.md` + the M01 journal into git tracking. **Collision surface with the tool is ~nil** — the tool is one-way and lives entirely in `tools/`; the user edits `Assets/` game code. M01 still committed cleanly. Not a halt. But: run artifacts are now git-tracked, so subagents MUST stage by name (never `git add -A`). If a later milestone halts `contaminated`, the user editing a shared file (`.gitignore`, `LOG.md`, a `.asset` write-target) is the cause.

## Schema set in M01 (M02 writer + M03 sim inherit)
- `BalanceConfig`: enums as names; references resolve to ids; `LevelGrantConfig.TargetStation` = stationType string or null (=all). Schema type `ResourceQuantity` (renamed from `ResourceAmount` to avoid clash with `Core.Model.ResourceAmount`).
- `buildSeconds` absent from all 8 stations + `perStationBuilt` from XpConfig → reader mirrors Unity's SO field-initializer defaults (`15f` / `5`); absent scalar = what the game loads. M03 construction-delay sim inherits `buildSeconds=15` per station.
- `versions/` lives at `tools/VoidDay.Balance/versions/`, not repo root.

## Writer contract set in M02 (M04 workbench inherits) — FLAGS
- **Writer edits by re-reading current + diffing incoming; ONLY changed fields written** (what makes a no-op round-trip byte-identical). Any NEW editable surface needs both a diff branch AND a line/append target, else it is REFUSED loudly (never silently dropped).
- **★ Scope reduction from doc:** level-row and upgrade-tier *insertion* were deliberately NOT built (no DoD case; nested-YAML risk under the minimal-diff bar). Nested-collection *edits* (recipe I/O, upgrade tiers/effects, level thresholds/grants, startingResources) are **refused loudly**, not editable. **M04's "edit every tunable in a browser" is therefore constrained** to the surfaces the writer supports (scalars + recipe insertion). If M04 needs those refused surfaces editable, that is new writer work — surface it.
- Absent scalars (`buildSeconds`, `perStationBuilt`) are edited by APPENDING one line at EOF (relies on Unity name-based SO deserialization).
- `.meta` GUID: `Guid.NewGuid("N")` for new recipes; RecipeSO `m_Script` guid stolen from an existing recipe asset at write time (no drift).
- Gems (`startingGems`/`secondsPerGem`/`minGemCost`) are schema-grouped under `Gems` but authored TOP-LEVEL in `GameConfig.asset`; writer targets `GameConfigPath` for them.

## Sim contract set in M03 (M04/M05/M06 inherit) — FLAGS
- **Parity canary FROZEN vs `GameBoot.cs` @ commit `4b13863`** (last commit to touch it; all named in-flight movers — gems M01–M03, Collection-Particles M01–M03 — confirmed committed). Normalized-SHA256 `052ff334…c663cb96` in `GameBootParityTests`. GameBoot was clean+unchanged at hash time.
- **★ SECOND mirror surface NOT guarded by the canary:** `CoreHarness` also mirrors `ProgressionSystem` (XP awards — without it nothing levels) and `UpgradesSystem` (registers runtime-built stations). The GameBoot canary does NOT catch drift in those two `.cs` files. Later milestones touching the sim must reconcile them too.
- **Sim data contract (M05/M06 inherit):** `SimResult`/`LevelReport` with `Pressure` gross + `GemRelief` separate (net DERIVED, never stored). Category keys: `Storage`/`Throughput`/`Income`/`OrderRefill`/`Unlock` + `Capacity:<type>`/`Supply:<good>`/`Yield:<type>`. TWO Random streams: order=`seed`, agent=`seed*1103515245+12345` (never `HashCode.Combine`).
- Pressure accrues continuously over EVERY clock slice (spec "player always present"), not only idle waits — required for Storage to register.
- Real economy findings (NOT bugs): baseline early game is a self-sustaining production-constrained corn loop (only corn is producible+sellable); `buildCost=999999` doesn't stall it and cap-10 storage doesn't dominate. The stall/storage GUARDS both work (proven with triggering configs). Automated tests use triggering configs, not the baseline QA scenarios.
- `PressureIsGrossOfGemRelief` tested as ledger invariant (`Accrue` never subtracts) rather than full-baseline 0-vs-50-gem equality (faster leveling under gems legitimately shifts the order stream).
- `SimProfile`/`profile/*` is the read-only-to-`patch` namespace (M05 must reject the whole namespace).

## Workbench contract set in M04 (M06/M07 inherit) — FLAGS
- **Two edit surfaces, honestly separated:** the workbench edits EVERY field and round-trips through version JSON (save/load, NOT the writer); push-to-Unity funnels through the M02 writer and returns a change summary OR the writer's refusal verbatim (UI surfaces refusals, never silently drops). DoD push test uses only writer-supported scalar edits.
- **API + wwwroot layout M06 builds on:** endpoints `GET/PUT /api/config`, `GET/POST/DELETE /api/versions`, `POST /api/sim`, `POST /api/write`; server JSON is Newtonsoft (`BalanceConfig` public fields; version files stay byte-identical to `read`); `wwwroot/vendor/` is where M06 adds `chart.js` (same no-CDN/no-build rule); `/api/sim` returns `{result, table}`.
- Chart.js deliberately NOT vendored yet — charts are M06 (Do-NOT-Build in M04); only htm/preact vendored.

## Agent-primitive contract set in M05 (M06/M07 inherit) — FLAGS
- **Loss shape:** scale-free normaliser `(v-max)/max(|max|,|v|)`; ranged targets SUM one violation per in-range level (scope width scales contribution; weight is the balancing lever). `LossReport`/`TargetResult` is the data contract `eval --json` emits and **M06 charts read**.
- **`patch` rejection protocol:** requires a DECLARED bound (`bounds.json` is the movable-knob allowlist — stricter than the doc's literal wording) and rejects the whole `profile/*` namespace by prefix, never a field list.
- `eval` is single-seed — **M06 owns multi-seed median/percentile aggregation**. Only `eval` journals (sweep/patch do not). Non-Storage `suggest` maps are config-derived heuristics (only Storage is doc-specified).
- `runs.jsonl` is **gitignored** runtime output at `tools/VoidDay.Balance/runs.jsonl`; **M07 restructures it into sessions**.
- M05 added a `report` verb (eval-log report) — distinct from M06's Chart.js reports; additive.

## Reports contract set in M06 (M07 inherits) — FLAGS
- **Endpoint contract:** `POST /api/sim` with `seeds>1` returns `{sweep: SimSweep.Aggregate, seeds:[{seed,levelReached,totalMinutes,stop,table}]}`; absent/`1` keeps single-seed `{result,table}`.
- Pressure-family aggregation lives on `LevelReport.PressureFamilies()` so the heatmap and the M05 loss share one rule.
- Heatmap is an HTML/CSS colour table, not a Chart.js chart (avoids a 2nd plugin).
- **FINDING (not a defect):** 3 directional DoD cases don't fire on baseline — baseline's self-sustaining pre-placed corn field means halved build cost is nearly inert, all-`unlockLevel=10` emits no Unlock/Supply pressure, slow-orders makes OrderRefill (not Income) dominate. This is M03's documented sim behaviour. Comparison machinery proven correct by exact-zero self-vs-self control + dramatic slow-orders delta (OrderRefill 0→~14000s).

## Tree notes
- This run **never opens the Unity Editor** (verification = `dotnet run` + C# tests). Editor lock relaxed.
- One-way dependency: nothing under `Assets/` learns the tool exists. `git status Assets/` stays clean except assets explicitly written (M01 `.gitignore`, M02 write-targets, M07 export).
- Pre-existing dirty/uncommitted, DO NOT stage: `ProjectSettings/ProjectSettings.asset` (DOTween define), staged rename `plans/balance-tool.md → docs/BalanceTool-Spec.md`, `README.md` (untracked), `milestones/BalanceTool-Spec/03-simulate.md` (modified plan doc), `milestones/Collection-Particles/.run/`, `docs/workflow/skills/preproduction/`.

| # | Milestone | Status | Commit | Notes |
|---|-----------|--------|--------|-------|
| 01 | Read the Economy | ✅ complete | `2024d1d` | Core compiles standalone net9.0; reader→baseline.json; 2 xUnit guards pass. Clean commit (tool files + .gitignore + LOG.md + baseline.json only). |
| 02 | Write It Back | ✅ complete | `f80c23b` | Surgical writer (Plan/Apply, 1-field=1-line diff), recipe structural insertion, fail-whole refusals; 8/8 writer tests. Assets/ clean. |
| 03 | Simulate | ✅ complete | `798ef92` | `sim` verb drives real Core via CoreHarness; PressureLedger gross+GemRelief; per-level bottleneck table; 18/18 tests. Reaches L20 @ 54.1m, byte-identical re-run, optimality-monotonic. Assets/ clean. |
| 04 | The Workbench | ✅ complete | `f717935` | Minimal-API `serve` + vendored Preact/htm wwwroot (7 tabs, versions, push-to-Unity via M02 writer w/ refusal modal). DoD write flow passed; **live browser render NOT confirmed (Chrome ext not connected) — user should eyeball.** Assets/ clean. |
| 05 | Agent Primitives | ✅ complete | `3c77271` | eval/patch/suggest/sweep/report verbs, scale-free scalar loss, bounds.json + namespace-wide profile/* rejection, AGENTS.md; 26/26 tests. Gem-relief suggest branch locked via synthesized-SimResult unit test (un-triggerable E2E). Assets/ clean. |
| 06 | Reports & Comparison | ✅ complete | `8df2b4e` | 30-seed SimSweep (median/p10/p90), 5 chart views + A/B overlay/deltas, vendored Chart.js; 29/29 tests. **Orchestrator fix folded in:** SimSweep.cs shipped with a raw NUL byte in a composite-key literal (`p.Kind + "\0" + p.Target`) → git treated it binary; replaced with ` ` escape (runtime-identical, source now text/reviewable), retested 29/29. Live browser render NOT confirmed (no Chrome ext). Assets/ clean. |
| 07 | Balancing Sessions | ✅ complete | `715299a` | Session dirs + generated (never-narrated) report + `eval --session` primitive + `/api/session` view + `/balance_game` skill + §9/§16 spec amendment; full QA-1…26 replay (19 live-pass, rest documented sim caveats w/ unit tests). 33/33 tests. Assets/ clean. |
