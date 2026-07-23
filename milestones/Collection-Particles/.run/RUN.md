# Collection Particles — Run 2026-07-22

**Range:** 01–03 · **Status:** complete
**Estimate:** 50–90 min wall-clock · 350k–650k tokens · confidence medium-high

## Resolved up front

- **Umbrella SFX cues → KEEP BOTH.** Do NOT silence `SfxCue.OrderFulfilled` / `XpGained` / `JobCollected`. The existing one-shot cue still fires on the event, and the new per-particle arrival sounds layer on top of it. This resolves the plan's Open Item in M01 and binds M02 and M03 too — do not re-litigate it per milestone.
- **New SFX cues ship CLIPLESS.** The three new per-particle cues must be authored as real cues with no clip assigned, silent by design, and must NOT throw at boot for a missing clip. Do not pick clips blind from the `DM-CGS-NN.wav` library. Record the unassigned cues in `LOG.md` as tech debt for the user to fill in by ear later.
- **Feel numbers → ship the plan's guessed defaults unverified.** Use the plan's values (stagger 0.06, flight 0.6, scatter 90, dwell, etc.) as-authored. Ensure every one is inspector-exposed so the user can retune by playing the finished build. Do NOT halt to ask for a judgement call on timing/feel — it cannot be evaluated without playing, and the user has accepted them unverified.

## Environment (carried from the Gems-Currency run)

- **Playmode is frozen at `frameCount = 1`.** `screenshot-game-view` returns frame 1 forever and shows a stale HUD while looking live. `Application.runInBackground = true` does NOT help. Verify by state read via `script-execute`, not by image. A state-read verification is the CORRECT method here, not a fallback.
- **Unity MCP is bound to the right Editor** — probed at run start: `dataPath=/Users/joefrancia/Desktop/VoidPet/VD_Unity/Assets`, `productName=VoidDay`, active scene `Assets/Scenes/Farm.unity`.
- `ProjectSettings/ProjectSettings.asset` is dirty and deliberately uncommitted (DOTween scripting define + runInBackground flip). Mixed provenance — leave it alone, never stage it.
- The staged rename `plans/balance-tool.md → docs/BalanceTool-Spec.md` is pre-existing in the index. Leave it, never stage or commit it.
- `LOG.md`'s "the working tree does NOT compile" note is **stale** — Gems-Currency M01–M03 are all committed.
- Plan line-number drift (harmless): `hud.Init` is at `GameBoot.cs:216` (not :203); `Hud`'s `MoneyChanged` lambda is at `Hud.cs:80` (not :70).

| # | Milestone | Status | Commit | Notes |
|---|-----------|--------|--------|-------|
| 01 | Money Particles | ✅ complete | `734e546` | EditMode baseline is **116** (was 106 pre-M01). Verified by state read; `screenshot-game-view` unusable. |
| 02 | XP Stars | ✅ complete | `aedc6a2` | Origin-stealing re-fire storm found & fixed; star sprite is a gem.png placeholder |
| 03 | Resource Pill Rail | ✅ complete | `4b13863` | Transient per-resource pill w/ Revive(); HudCanvas sibling order shifted |

## Flags raised (for end-of-run review)

### M01
- **`EarnKind` constants class (Money/Xp/Resource)** added to `Core/Events/GameEvents.cs`. M02/M03 must use these constants, not raw strings.
- **Origin↔burst pairing:** `PendingBurst` carries a `Source` string and `LateUpdate` dequeues exactly one origin from that source's queue per burst, throwing if empty. M02's `XpGained` burst needs its OWN enqueue (it cannot borrow the `order` origin the money burst consumed); M03 needs one enqueue per `JobCollected` burst.
- **`IconFor`/`TargetFor` are switch expressions that throw on an unhandled kind** — M02/M03 each add one arm plus a serialized sprite/target field.
- `FlightSettings` struct is declared in `View/EarnParticle.cs`, not in `EarnBurstController.cs` (same namespace; no functional difference).
- **EditMode suite baseline moved 106 → 116.** Other docs' 71/83 figures are stale.
- The three new `SfxCue` values ship **clipless** (silent by design) per the run decision — recorded as tech debt in `LOG.md`.

### M02
- **Every burst must enqueue its OWN origin.** `GameBoot` inits `progressionSystem` BEFORE `earnBurstController`, so an order's XP burst was recorded first and stole the money burst's `order` origin — a real every-frame re-fire storm, now fixed.
- `LateUpdate` drains `_bursts`/`_origins` in a `finally` — without it a throwing burst re-fires forever. Keep it.
- `PendingBurst` gained `bool PointerFallback`; `Dequeue(source)` became `OriginFor(burst)`. Money strict throw-on-empty, XP true, M03 false.
- **`starSprite` is wired to `gem.png` as a PLACEHOLDER.** `Assets/Art/UI/Icons/xp.png` does not exist, never was in git, is in no stash. The plan/00-summary/LOG all record it as cut and approved — that record is wrong. Swapping real art in is one inspector field, zero code.
- Core levels up at grant time, before any star flies — a mid-flight level-up reads as bar-resets-then-stars-drain-from-0. Self-correcting, no rule added.

### M03
- **`ResourcePillRail.Revive()`** — a burst arriving while that resource's pill is mid-retract pulls the pill back instead of stacking a duplicate. Not in the plan; later work inherits the invariant *one pill per resource, always*. Not exercised in play.
- **`EarnParticle.TargetLocal()` gained a `_target == null` guard** (an M01 file, outside M03's listed surface): `GameReset` destroys pills while icons fly, and without it the flight tween throws every tick. Verified by construction only.
- **`HudCanvas` sibling order changed** — `ResourcePillRail` is index 0, every other child shifted down one. Anything later that must render behind the money pill goes to index 0 and pushes the rail down.
- Subtract-pending third-occurrence refactor **deliberately skipped** under time pressure; deferred in `LOG.md`. The three implementations are similar but not identical.
- Multi-output case exercised with a synthetic two-output `JobCollected`, not an edited recipe — no authored recipe has more than one output.

## Estimate vs actual

| | Predicted | Actual |
|---|---|---|
| Wall-clock | 50–90 min | **~34 min** (M01 15, M02 10, M03 8.5) |
| Halts | umbrella-cue collision near-certain; audio likely | **zero** — all three pre-answered at the gate |
| Asset readiness | "art fully ready" | **wrong** — `xp.png` never existed; scout fabricated the confirmation |
