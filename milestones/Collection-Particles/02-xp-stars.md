# Milestone 02 — XP Stars

**Demonstrable outcome:** press Play and collect a ready job. Purple stars fly from the station up to the
level pill, one at a time, and the XP bar creeps up per star instead of jumping. Fill an order and stars fly
from your finger alongside the coins — two streams to two destinations from one action. The level badge pops
per star, and that pop is visibly gentler than a level-up pop.

## Goal

The second stream, and the first time two bursts are in the air at once. It introduces the **world→screen**
origin path, which M3 reuses.

## ✅ The star sprite is DONE — this milestone is no longer blocked

`Assets/Art/UI/Icons/xp.png` exists as of 2026-07-22: the user's source image, background cut, unpremultiplied
against white so the antialiased edge stays clean on dark, trimmed, and formatted to the folder convention
(**512×512 RGBA, content 458×438 centred**, matching coin/heart/gem). Importer settings were cloned from
`coin.png.meta` (`textureType: 8` Sprite, `spriteMode: 1` Single, `alphaIsTransparency: 1`), GUID
`5dc45ffd6fa84db9871441092145eb39`. Previewed on checkerboard / light / dark / violet and at 34px HUD scale,
and approved.

**Do not re-cut or restyle it.** Just wire it.

*(Note: `tools/asset-prep/cutout.py` is NOT a general CLI — it is a hardcoded batch over
`references/voidpet-ip/*.png` with creature-specific logic, and running it re-cuts the pets. The star was
cut with an adapted variant of the same white-key approach.)*

There is still no `docs/assets/02-ui.md` entry for an XP icon; it is new and unreconciled.

## Build This

- **`View/EarnBurstController.cs`** — the XP path. Everything structural (buffering, the origin **queue** per
  source, the `LateUpdate` flush, chunking, live-target flight) already exists from M1; this adds entries.
  - `Init` gains `stationRoots` and `worldCamera`:
    `Init(EventBus bus, IReadOnlyDictionary<string, Transform> stationRoots, Camera worldCamera)`.
    Update the call in `GameBoot.Start()` — pass `roots` and the same `worldCamera` the other views get.
  - Serialized: `starSprite`, `xpTarget` (a `RectTransform`, inspector-wired to
    `HudCanvas/LevelXpPill/Badge`).
  - Subscribe `JobCollected` → **enqueue** `("job", stationScreenLocal(e.StationId))`. No burst yet — the
    resource bursts are M3; this milestone only needs the origin.
  - Subscribe `StationBuilt` → enqueue `("build", stationScreenLocal(e.StationId))`. Origin only —
    **building a station throws no particles of its own.**
  - Subscribe `XpGained` → **skip `e.Source == "debug"`**, else buffer a `("xp", null, e.Amount)` burst
    tagged with its source string.
  - At flush, each XP burst **dequeues** the origin for its own source. ★ The queue is why two jobs collected
    in the same frame (pet auto-collect) pair with their own stations instead of both using the last one.
    If the queue for a source is empty, fall back to the pointer.
  - `stationScreenLocal(id)`: `_stationRoots[id].position` → `_worldCamera.WorldToScreenPoint(...)` → canvas
    local (★ `null` camera in `ScreenPointToLocalPointInRectangle` — Overlay). **Fail loud** on an id absent
    from the map; that is a bug, not a state to skip.
  - ★ The amount is **`XpGained.Amount`**, never `XpConfigSO.perJobCollected` — `AwardXp` runs the value
    through `ValueResolver` (`ResolveKind.XpGain`) before publishing, so an XP-boosting upgrade would make
    the two disagree. It also returns early at 0, so no event means no burst.

- **`View/LevelXpHud.cs`** — the deferred bar and a *parameterised* pop.
  - Add `_pendingXp`. `Sync()` computes the target fill from
    `Mathf.Max(0, _progression.XpIntoLevel - _pendingXp)` over `XpSpanOfLevel`.
  - Subscribe `EarnBurstLaunched` → if `Kind == "xp"`, `_pendingXp += Amount`, `Sync()`.
  - Subscribe `EarnParticleArrived` → if `Kind == "xp"`, `_pendingXp -= Amount`, `Sync()`, **and pop**.
    Both unsubscribed in the existing `OnDestroy` (this file already has one — follow it).
  - **★ Parameterise the pop, or it cannot be made smaller.** Today `Update()` computes
    `t = Clamp01(_popRemaining / badgePopSeconds)` then
    `scale = 1 + (badgePopScale - 1) * Sin(t * π)` (`LevelXpHud.cs:77-80`). Simply setting `_popRemaining`
    to a smaller value starts `t` mid-curve, so the badge **snaps** to ~95% amplitude and decays — it does
    not shrink the pop, it makes it jerkier. Amplitude is governed by `badgePopScale`.
    **Fix:** add `_popScale` / `_popSeconds` fields set at trigger time, and have `Update()` read those
    instead of the serialized constants. A level-up triggers with `badgePopScale` / `badgePopSeconds`; a
    particle arrival triggers with new serialized `particlePopScale` (≈1.12) / `particlePopSeconds` (≈0.16).
    ★ Do **not** add a DOTween scale tween on `Badge` — `Update()` writes `badge.localScale` every frame
    while a pop is running and would overwrite it.

- **`View/SfxController.cs`** — assign a clip to `SfxCue.EarnParticleXp`, auditioned from
  `Assets/Casual Game Sounds U6/CasualGameSounds/`. The mapping was written in M1.
  **★ Apply M1's umbrella-cue decision to `SfxCue.XpGained`** — it fires on this same event, so an
  unmuted `XpGained` cue plus up to 10 star cues is the same pile-up M1 resolved. Whatever was decided and
  recorded in `LOG.md` for money applies here.

## Do NOT Build This

- **Resource particles or the pill rail** → M3. `JobCollected` is subscribed here for its **origin only**.
- **Particles for level-up *rewards*.** Crossing a level pays out unlocks and rewards; none of that spawns
  particles. Only `XpGained` does, and a level-up grants no XP.
- **Particles for `StationBuilt` itself.** Origin only.
- **A `LevelUp` subscription.** The bar already resets its fill on level-up; leave that path alone.
- **Pausing or re-basing in-flight stars when a level is crossed mid-flight.** The bar clamps at 0 and
  drains. Understated for a moment, self-correcting, explicitly not worth a rule.
- **A DOTween tween on the badge.** See above.
- **Regenerating or restyling the star.** Use the user's image.
- **A new SFX cue.** All three were appended in M1.
- **Public credit methods on `LevelXpHud`.** It learns from the bus, like `Hud`.

## Context

- **Existing from M1:** `EarnChunks` (+ tests), `EarnParticle` (live-target flight, credit-on-destroy),
  `EarnBurstController` (buffering, origin queues, flush, stagger), `FxCanvas`, both events, the three
  `SfxCue` values, the subtract-pending pattern on `Hud`.
- **New files:** none — `Assets/Art/UI/Icons/xp.png` is the only new asset.
- **Events added:** none.
- **Systems touched:** `View/EarnBurstController.cs`, `View/LevelXpHud.cs`,
  `Systems/Boot/GameBoot.cs` (the widened `Init` call), `Assets/Scenes/Farm.unity` (wire `starSprite`,
  `xpTarget`), `Assets/Data/SO/SfxLibrary.asset`.

## Principles

- **Core boundary (rule 3):** the origin queues, the world→screen projection and the pending count are all
  View. Core learns nothing about where a star flew.
- **Event-driven (rule 2):** `LevelXpHud` subscribes; the controller never calls it. The controller reads
  `StationRegistry.Roots` — the shared map `GameBoot` already injects into four other views — rather than
  calling `StationRegistry`.
- **Data-driven (rule 1):** `particlePopScale` / `particlePopSeconds` are serialized, not derived from the
  level-up constants.
- **Fail loud:** an unknown station id throws. Do not `TryGetValue` and return.

## Assets Required

- **`Assets/Art/UI/Icons/xp.png`** — **DONE** (cut, trimmed, imported, previewed, approved 2026-07-22).
  `[real asset, ready]`
- One SFX clip auditioned from `Assets/Casual Game Sounds U6/CasualGameSounds/`. `[placeholder OK]`

## UI Mockups Required

**None.** The level pill is already authored; it only gains a second, gentler pop.

## Definition of Done

- Collecting a job throws stars **from the station** to the level pill; the bar rises per star.
- Filling an order throws coins **and** stars from the finger simultaneously, to two destinations.
- The bar lags: mid-flight it reads below `Progression.XpIntoLevel`; on the last arrival it matches exactly.
- **One sound per star arrival**, with M1's umbrella-cue decision applied to `SfxCue.XpGained`.
- The badge pops per star, and the pop is visibly **smaller** than a level-up pop — achieved by amplitude
  (`particlePopScale`), not by a shortened duration.
- `DebugLevelUpRequested` produces **no** stars.
- Two jobs collected in the same frame produce two star bursts from **their own** stations.
- The EditMode suite passes at its M1 baseline.

## How to Test

0. Run the EditMode suite and confirm the M1 baseline.
1. `Application.runInBackground = true`, enter playmode. (`xp.png` is already cut and approved — no asset
   prep needed.)
3. Queue a job, wait, tap the ready station. Stars must originate **at the station in the world**, not at the
   finger — verify by tapping a station near a screen edge, where the two are far apart.
4. Pause mid-flight. The bar's fill must correspond to less XP than `Progression.XpIntoLevel` reports.
   Resume; on the last star they must agree.
5. Fill an order. Coins and stars leave the same point and split to two destinations. Neither stream steals
   the other's particles.
6. **XP with no other burst.** Trigger an XP grant that is not a job collect or an order fulfil (e.g. build a
   station — its XP grant should throw stars from the new station, with no coins and no resources).
7. **Origin-queue check.** Collect two ready stations in the same frame (queue two, let both finish, then
   publish both `QueueSlotTapped`/`StationTapped` intents in one frame via `script-execute`). Each star
   burst must start at its **own** station. If both start at the same one, the origin store is a single slot
   instead of a queue.
8. **Cheat exclusion.** Publish `DebugLevelUpRequested`. XP is granted with `Source == "debug"` — **no
   stars.** The bar, the level and the popup all behave as before.
9. **Level-up mid-flight.** Set up a collect that crosses a threshold. Expect: the popup fires on its own
   schedule, the bar resets, and still-flying stars drain into the new level from 0. Slightly odd, never
   stuck. Record what it actually looks like in `LOG.md`.
10. **Pop size.** Trigger a level-up and a star arrival close together. The star pop must be visibly gentler,
    and the badge must not flicker, snap, or freeze at a scaled size.
11. Re-run the EditMode suite.
