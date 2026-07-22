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
