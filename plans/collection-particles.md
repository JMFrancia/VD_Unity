# Collection Particles — Prototype Plan

> **⚠ SUPERSEDED (2026-07-22).** This document is kept only as the design record. The buildable plan is
> **`milestones/Collection-Particles/`**. A three-way cold audit reversed nine decisions below — most
> importantly the credit path (now event-driven, both events carry an amount), the flight API (now follows a
> live `RectTransform`, not a baked `Vector2`), and chunking (now `Core/Rules/EarnChunks` **with tests**).
> See the *Audit Reversals* table in `milestones/Collection-Particles/00-summary.md`.
> **Do not implement from this file.**

## Implementation Status
<!-- implement_phase reads and updates this ledger. -->
| Phase | State | Commit | Notes |
|---|---|---|---|
| 1 — Flight engine + money | ⬜ TODO | — | Proves the whole engine against an existing HUD target |
| 2 — XP stars | ⬜ TODO | — | Needs the user's star PNG on disk first |
| 3 — Resource pill rail | ⬜ TODO | — | New UI surface, no Figma pass — see phase notes |

## What It Is

Every time the player earns something, a small burst of icon particles flies from the point of
action to that thing's HUD home and is credited on arrival, one particle at a time. Money from a
filled order, resources from a collected job, XP from any grant. Level-up rewards are excluded.

The point is *felt payout*. Right now a collect is a silent number change; this makes earning read
as an event with weight and duration.

## How It Works

### Trigger off the earn event, never off the counter

The three bursts hang off `JobCollected`, `OrderFulfilled` and `XpGained` — **not** off
`ResourceChanged` / `MoneyChanged`. That is what excludes level-up rewards and debug cheats for
free: a level-up grant reaches the wallet through `Wallet.Add`, which never publishes
`OrderFulfilled`, so it simply never spawns particles. No filtering rule, no flag to keep in sync.

| Source event | Payload used | Icon | Destination |
|---|---|---|---|
| `JobCollected` | `Outputs` (one burst per resource) | `ResourceSO.icon` | transient resource pill (Phase 3) |
| `OrderFulfilled` | `Payout` | coin sprite | `HudCanvas/MoneyPill` |
| `XpGained` | `Amount` (skip `Source == "debug"`) | star sprite | `HudCanvas/LevelXpPill` |

### Origin — where the burst is born

Per source, whichever is actually the point of action:

- `JobCollected` / `StationBuilt` → the station's transform via `StationRegistry.Roots`, projected
  through the world camera to screen. Correct even when a pet collects, where there is no finger.
- `OrderFulfilled` and any other UI-driven earn → `Pointer.current.position`. Order fulfilment is
  always a finger on the Fill button, and the order panel is covering the world anyway, so the
  pointer *is* the point of action.
- XP inherits the origin recorded for its `Source` this frame ("job" → that station, "order" →
  the pointer, "build" → that station), falling back to the pointer.

### Ordering — why the flush is deferred to LateUpdate

`ProgressionSystem` awards XP synchronously inside the `JobCollected` dispatch, so `XpGained`
arrives nested inside its own trigger event. Whether the burst controller sees `JobCollected`
before or after that `XpGained` depends on `GameBoot`'s subscription order, which is not something
to build on.

So the controller **buffers**: handlers only record (origin-by-source, pending bursts) and
`LateUpdate` flushes them. By `LateUpdate` every bus dispatch for the frame has settled, so origin
and amount are always both present regardless of subscription order.

### Particle count and chunking

`count = min(amount, maxParticles)` with `maxParticles = 10` (serialized). Each particle carries
`amount / count`, and the first `amount % count` particles carry one extra — so the chunks sum to
the amount exactly, with no rounding drift and no reconciliation fudge. 7 XP → 7 particles of 1;
23 coins → 10 particles, three of 3 and seven of 2.

### Flight

DOTween sequence per particle, launched on a stagger so they leave one at a time:

1. **Scatter** — a short hop to a random offset around the origin, `Ease.OutQuad`. This is what
   makes a burst read as a burst rather than a queue.
2. **Flight** — `DOAnchorPos` to the destination's screen point with `Ease.InQuad`, so it drifts
   slowly out of the scatter and accelerates into the target. That slow head is the "float-y".

On arrival the particle credits its chunk, publishes its arrival, and destroys itself.

### The lagging counter — subtract-pending, not snap-back

The requirement is that the counter disagrees with Core mid-flight and agrees exactly at the end.
Rather than tracking a separate display number that has to be re-synced (and can drift), every
target renders:

```
displayed = trueValue - pending
```

`trueValue` is whatever Core last said. `pending` goes up when a burst launches and down by each
chunk as particles land. When the last particle lands `pending` is 0 and the display *is* the truth
— by construction, with no snap step to get wrong.

It also survives interleaving for free: a purchase mid-flight lowers `trueValue`, and the display
drops with it while still lagging by the correct amount. Same for a second overlapping burst.

### Sound and pulse

The controller publishes `EarnParticleArrived { Kind }` on each arrival — a fact, not an
instruction. `SfxController` maps it to one of three new cues, and the destination view pulses its
own icon. Nobody reaches across a system to make a noise.

## Data & Config

**No new ScriptableObject.** Everything needed already has a home:

- Resource icons — `ResourceSO.icon`, already authored.
- Coin and star sprites — serialized fields on `EarnBurstController` (they are chrome, not game
  data, and per the View/SO split presentation references live on the View).
- Flight feel — serialized on `EarnBurstController`: `maxParticles`, `staggerSeconds`,
  `scatterRadius`, `scatterSeconds`, `flightSeconds`, `flightSecondsJitter`, ease curves,
  `pulseScale`, `pulseSeconds`, `pillDwellSeconds`.
- Sound — three rows appended to `SfxCue` / `SfxLibrarySO`, clips assigned in the inspector.

**New scene objects**

- `FxCanvas` — screen-space overlay, `sortingOrder = 100` (existing canvases top out at 30), so
  particles fly over open panels instead of behind them. Hosts `EarnBurstController`.

**New prefabs**

- `Prefabs/UI/EarnParticle.prefab` — a single `Image` plus `EarnParticle`.
- `Prefabs/UI/ResourcePill.prefab` — Phase 3.

**New enum values** — appended to `SfxCue` (never inserted; the enum's integer values are what the
library asset serializes): `EarnParticleResource`, `EarnParticleMoney`, `EarnParticleXp`.

## Phases

### Phase 1 — Flight engine + money particles

The whole mechanism, proven against the one target that already exists and needs no new UI.

- `Core/Events/GameEvents.cs` — add `EarnParticleArrived { Kind }`.
- `View/EarnParticle.cs` — the flying icon; `Fly(from, to, ...)` builds the DOTween sequence and
  fires a callback on arrival.
- `View/EarnBurstController.cs` — buffered subscribe → `LateUpdate` flush → chunking → staggered
  spawn. Money path only.
- `View/Hud.cs` — money text becomes `trueTotal - pending`; `AddMoneyCredit(int)` and a one-shot
  scale pulse on the money pill icon.
- `Data/SfxLibrarySO.cs` + `View/SfxController.cs` — the three cues and the `EarnParticleArrived`
  mapping (all three wired now; two are simply unused until later phases).
- `Systems/Boot/GameBoot.cs` — `earnBurstController.Init(bus, roots, worldCamera, hud, ...)`.
- Author `FxCanvas` and `EarnParticle.prefab` in the editor; wire the coin sprite.

**Verify:** fill an order → coins fly from your finger to the money pill, the counter ticks up
per coin instead of jumping, the pill pulses per arrival, one sound per coin.

### Phase 2 — XP stars

- **Blocked on the asset:** the star PNG needs to be on disk. Drop it anywhere under `Assets/`
  and say where — background cutout, transparent trim and sprite import settings are handled from
  there, matching the existing icon set.
- `EarnBurstController` — XP path: subscribe `XpGained`, skip `Source == "debug"`, resolve origin
  by source.
- `View/LevelXpHud.cs` — `_pendingXp`; `Sync()` computes fill from `XpIntoLevel - _pendingXp`
  (clamped ≥ 0); `AddXpCredit(int)`; badge pulse per arrival, reusing the existing pop field.

**Verify:** collect a job → stars fly from the station to the level pill *and* resource-less XP
still lands correctly; fill an order → stars fly from your finger alongside the coins; the bar
creeps up per star rather than in one step.

### Phase 3 — Resource pill rail

Resources have no persistent HUD counter, so the burst brings its own: a pill slides out from
behind the money pill carrying that resource's icon and running count, receives its particles,
holds, then slides back behind the money pill — which is exactly where the player taps to open the
totals popup, so the gesture teaches the affordance.

- **No Figma pass** (deliberate exception to the usual new-UI rule): there is no layout to
  iterate. The pill is a duplicate of the authored money pill with the icon swapped, anchored
  directly beneath it. The only real unknown is *motion*, which a static mock cannot answer.
- `Prefabs/UI/ResourcePill.prefab` — a duplicate of the money pill chrome with the icon swapped.
- `View/ResourcePillRail.cs` — one pill per distinct in-flight resource, stacked downward under the
  money pill; slide out on burst start, retract `pillDwellSeconds` after the last arrival; an
  overlapping burst for the same resource extends the dwell rather than spawning a second pill.
  Renders `pool.Get(id) - pending[id]`.
- `EarnBurstController` — resource path: one burst per entry in `JobCollected.Outputs`.

**Verify:** collect a job → a wheat pill slides out from behind the money pill, wheat icons fly
into it, its count ticks per arrival, then it slides away. Collect a multi-output recipe → two
pills stack.

## Decisions Made

- **Trigger off `JobCollected` / `OrderFulfilled` / `XpGained`, not the counter-change events.**
  This is what excludes level-up rewards and debug cheats structurally rather than by a flag.
- **Origin is the station for world earns, the pointer for UI earns.** Getting the fulfilled order
  card's rect would mean the burst controller holding a reference to `OrderBoardPanel`; the finger
  is on the Fill button anyway, so the pointer is both simpler and more accurate.
- **Flush in `LateUpdate`.** Cheaper and more robust than depending on `GameBoot` subscription
  order for the nested `JobCollected` → `XpGained` dispatch.
- **`displayed = true - pending`** instead of a tracked display value with a snap-back. Removes the
  entire class of "the counter ended up wrong" bug.
- **No new ScriptableObject.** Feel tunables are presentation and live on the View; the only game
  data involved (resource icons) already exists on `ResourceSO`.
- **Instantiate/Destroy, no pool.** ≤10 UI images per burst. If it ever shows up in a profile,
  pooling is a contained change inside `EarnBurstController`.
- **Three SFX cues, not one.** A coin and an XP star sounding identical would flatten the two
  streams that already fire simultaneously on an order fulfil.
- **Ignored for now:** a level-up crossing mid-flight resets `XpIntoLevel` while XP is still
  pending, so the bar clamps at 0 and drains from there — briefly understated, self-correcting, and
  not worth a rule. Particles are not paused/cleaned on a debug reset; they simply land into a
  counter that has already snapped.

## How to Verify by Playing

1. **Small money burst** — fill a cheap order. Coin count should equal the payout (under 10).
   Counter ticks once per coin; final value matches the money pill after everything lands.
2. **Big money burst** — debug-add resources, fill a high-payout order. Exactly 10 coins, each
   crediting a chunk; the total must land on the exact payout, not one off.
3. **Simultaneous streams** — fill an order and watch coins go to the money pill while stars go to
   the level pill from the same finger position.
4. **Job collect** — tap a ready station. Resources fly to a pill that slides out from behind the
   money pill; stars fly to the level pill from the station.
5. **Level-up rewards are silent** — hit the debug "level up" button. The popup's rewards must
   produce **no** particles, and the money/XP counters snap normally.
6. **Interleave** — start a big burst, then immediately buy something. Money should drop by the
   purchase while still lagging, and end exactly right.
7. **Rapid collects** — tap several ready stations back to back. Overlapping bursts, pills extend
   rather than duplicate, and every counter reconciles.
