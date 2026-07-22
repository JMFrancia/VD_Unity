# Collection Particles — Milestone Plan

**Design source:** `plans/collection-particles.md` (the prototype design pass this decomposes; kept as the
design record, **superseded by these docs wherever they disagree** — several of its decisions were reversed
by the cold audit, see *Audit Reversals*)
**Generated:** 2026-07-22 · **Cold-audited:** 2026-07-22 (three independent auditors; findings folded in)

Every time the player earns something, a burst of icon particles flies from the point of action to that
thing's HUD home and is credited on arrival, one particle at a time. Money from a filled order, resources
from a collected job, XP from any grant. **Level-up rewards are excluded.**

The point is *felt payout*. Today a collect is a silent number change; this makes earning read as an event
with weight and duration.

"Playable" here means what it means everywhere in this project: press Play and see the new thing work.

## Milestones
| # | Name | Demonstrable outcome | Doc |
|---|---|---|---|
| 1 | Money Particles | Fill an order → coins fly from your finger to the money pill, which ticks up one coin at a time. | `01-money-particles.md` |
| 2 | XP Stars | Any XP grant throws stars at the level pill; the bar creeps up per star instead of jumping. | `02-xp-stars.md` |
| 3 | Resource Pill Rail | Collect a job → a pill slides out from behind the money pill, resource icons fly into it, it retracts. | `03-resource-pill-rail.md` |

## The architecture, in one page

### Trigger off the earn event, never off the counter

The three bursts hang off `JobCollected`, `OrderFulfilled` and `XpGained` — **not** off `ResourceChanged` /
`MoneyChanged`. That is what excludes level-up rewards and debug cheats structurally rather than by a flag: a
level-up money grant reaches the wallet through `Wallet.Add`, which never publishes `OrderFulfilled`, so it
simply never spawns particles. Nothing to keep in sync.

| Source event | Payload used | Icon | Destination | Milestone |
|---|---|---|---|---|
| `OrderFulfilled` | `Payout` | coin sprite | `HudCanvas/MoneyPill` | 1 |
| `XpGained` | `Amount` (skip `Source == "debug"`) | star sprite | `HudCanvas/LevelXpPill/Badge` | 2 |
| `JobCollected` | `Outputs` — one burst per entry | `ResourceSO.icon` | transient resource pill | 3 |

`StationBuilt` is subscribed in M2 **solely to supply an origin** for the build XP grant. Building a station
throws no particles of its own.

### Two events, and why crediting is event-driven

```csharp
EarnBurstLaunched  { string Kind; string ResourceId; int Amount; }  // Amount = the whole burst
EarnParticleArrived{ string Kind; string ResourceId; int Amount; }  // Amount = this particle's chunk
```

Each destination view subscribes to **both** and keeps its own `pending`: `Launched` raises it by the burst
total, `Arrived` lowers it by the chunk. `EarnBurstController` therefore **never calls a method on `Hud`,
`LevelXpHud` or `ResourcePillRail` to move their state** — it announces what happened and each view decides
what that means about its own number (CLAUDE.md rule 2).

`ResourceId` is `null` for money and XP.

> **Audit reversal.** The original design had the controller holding `Hud` / `LevelXpHud` / `ResourcePillRail`
> and calling `AddPending` / `Credit` on them, with an `EarnParticleArrived` that carried no amount — which
> designed the event-driven option out of reach while citing rule 2 for the *sound* path only. Putting the
> amount on the event is what makes the rest of this honest.

**The one remaining direct call** is `ResourcePillRail.RectFor(resourceId)` — the controller asks the rail
*where* a pill is so it can aim at it. That is a positional **query**, not a command; it mutates nothing. It
survives because the alternative (routing a transform through an event) would put `RectTransform` in a Core
event, breaking rule 3 to satisfy rule 2. Called out rather than hidden.

### Origin — where the burst is born

Per source, whichever is genuinely the point of action:

- `JobCollected` / `StationBuilt` → the station's transform via `StationRegistry.Roots`, projected through
  the world camera to screen. Correct even when a pet collects, where there is no finger.
- `OrderFulfilled` and other UI-driven earns → `Pointer.current.position.ReadValue()`. Order fulfilment is
  always a finger on the Fill button, and the order panel covers the world anyway, so the pointer *is* the
  point of action. Reading the card's rect instead would couple the controller to `OrderBoardPanel`.
- XP inherits the origin recorded for its `Source` this frame ("job" → that station, "order" → the pointer,
  "build" → that station), falling back to the pointer.

### Why the flush is deferred to `LateUpdate` — and why origins are queues

`ProgressionSystem.OnJobCollected` calls `Progression.AwardXp`, which publishes `XpGained` **inside** the
`JobCollected` dispatch (`Progression.cs:60`) and then runs `AdvanceLevels()`, which can publish `LevelUp` in
the same call stack. `EventBus` invokes handlers in subscription order, so whether the burst controller sees
`JobCollected` before or after that nested `XpGained` depends on `GameBoot`'s ordering.

So the controller **buffers**: handlers only record, and `LateUpdate` flushes. By then every dispatch for the
frame has settled.

**Origins are a queue per source, not a single slot.** Two jobs collected in one frame (an explicit case —
pet auto-collect) would otherwise record two origins under the key `"job"`, the second overwriting the
first, and both XP bursts would launch from the wrong station. Record `("job", pos)` in order, dequeue in
order, and the XP bursts pair with their own stations. Clear the queues at the end of each flush.

### Particle count and chunking

`Core/Rules/EarnChunks.Split(amount, maxParticles)` returns the per-particle amounts: `count = min(amount,
maxParticles)`, each carries `amount / count`, and the first `amount % count` carry one extra — so the chunks
sum to the amount **exactly**. 7 XP → seven of 1; 23 coins → ten, three of 3 and seven of 2. `amount <= 0`
**throws** (fail loud); the callers must not launch an empty burst.

> **Audit reversal.** This lives in `Core/Rules/` as a pure static **with EditMode tests**, not in the View.
> The original placed it in the View and used that placement as the reason not to test it. It is integer
> arithmetic that decides whether the player's displayed totals end up exact — precisely the "invisible, just
> makes the game subtly wrong" class CLAUDE.md carves out as the one place tests are mandatory.

### Flight

A DOTween sequence per particle, launched on a stagger so they leave one at a time:

1. **Scatter** — a short hop to a random offset around the origin, `scatterEase` (default `OutQuad`). This is
   what makes a burst read as a burst rather than a queue.
2. **Flight** — to the destination with `flightEase` (default `InQuad`), so it drifts slowly out of the
   scatter and accelerates in. That slow head is the "float-y" the design asked for.

**The flight follows a live `RectTransform`, not a baked `Vector2`.** M3's pill is still sliding out while
the first particles are already in the air, and reflows upward when a sibling retracts — a fixed endpoint
would aim them at where the pill *was*. Implemented as `DOVirtual.Float(0, 1, flightSeconds, …)` lerping
from the scatter point to the target's current anchored position each tick, rather than `DOAnchorPos`.

> **Audit reversal.** The original fixed `Fly(Vector2 from, Vector2 to, …)` in M1, which M3 would have had to
> tear up. Getting this right in M1 is the difference between layering and rework.

**Every launched chunk is credited exactly once.** `EarnParticle` fires its arrival callback from
`OnDestroy` if the tween never completed, so a torn-down canvas, a domain reload or a `DOTween.KillAll`
cannot strand a chunk in `pending` and leave every counter permanently understated. The subtract-pending
scheme is only correct "by construction" if this holds.

### The lagging counter — subtract-pending

The requirement is that the counter disagrees with Core mid-flight and agrees **exactly** at the end. Rather
than tracking a separate display number that has to be re-synced (and can drift), every target renders:

```
displayed = trueValue - pending
```

`trueValue` is whatever Core last said. When the last particle lands `pending` is 0 and the display *is* the
truth — no snap step to get wrong. It survives interleaving for free: a purchase mid-flight lowers
`trueValue`, and the display drops with it while still lagging by the correct amount.

### Sound and pulse

The controller publishes; `SfxController` maps `EarnParticleArrived` to one of three new cues, and each
destination view pulses **its own chrome** on the same event — the money pill rect, the XP badge, the
resource pill's icon.

**★ The existing umbrella cues collide with this.** `SfxController` already plays `SfxCue.OrderFulfilled` on
`OrderFulfilled`, `SfxCue.XpGained` on `XpGained` and `SfxCue.JobCollected` on `JobCollected`. Unmuted, one
order fulfil now fires the order chime **plus** up to 10 coin cues **plus** the XP cue **plus** up to 10 star
cues. See *Open Items* — this needs the user's ear, not a silent decision.

### The pieces

- **`Core/Rules/EarnChunks.cs`** — pure static, tested.
- **`Core/Events/GameEvents.cs`** — `EarnBurstLaunched`, `EarnParticleArrived`.
- **`View/EarnParticle.cs`** — one flying icon, following a live target.
- **`View/EarnBurstController.cs`** — buffered subscribe → `LateUpdate` flush → chunking → staggered spawn.
- **`View/ResourcePill.cs` / `View/ResourcePillRail.cs`** — M3 only.

### Config

**No new ScriptableObject.** Everything needed already has a home:

- Resource icons — `ResourceSO.icon`, already authored.
- Coin and star sprites, and the money/XP destination `RectTransform`s — serialized on
  `EarnBurstController` and inspector-wired (rule 4). They are chrome, not game data.
- Flight feel — serialized on `EarnBurstController`: `maxParticles`, `staggerSeconds`, `scatterRadius`,
  `scatterSeconds`, `scatterEase`, `flightSeconds`, `flightSecondsJitter`, `flightEase`. (DOTween's `Ease`
  is an enum and serializes to the inspector — the eases are tunables, not literals.)
- Pulse feel — serialized on **each destination view**, because each pulses its own chrome:
  `Hud.pulseScale/pulseSeconds`, `LevelXpHud.particlePopScale/particlePopSeconds`,
  `ResourcePill.pulseScale/pulseSeconds`.
- Pill feel — serialized on `ResourcePillRail`: `slideSeconds`, `slideEase`, `dwellSeconds`, `slotPitch`,
  `firstSlotOffset`.
- Particle size — **owned by `EarnParticle.prefab`'s own rect**, not overridden from the controller. Static
  presentation belongs in the prefab.
- Sound — three rows appended to `SfxCue` / `SfxLibrarySO`, clips assigned in the inspector.

### New scene objects

- **`FxCanvas`** — screen-space overlay, `sortingOrder = 100`. The scene's existing canvases run 10/15/15/20/30
  (`HudCanvas` is 20), so 100 puts particles over open panels instead of behind them. Hosts
  `EarnBurstController`; particles are instantiated as its children. **No `GraphicRaycaster`** — particles
  must never eat a tap.

## Where the value lands
- **After M1** the entire mechanism — buffering, chunking, stagger, live-target flight, deferred credit,
  arrival sound, pulse — is proven against the one target that already exists. If the feel is wrong, it's
  wrong here, before any new surface has been authored.
- **After M2** the second stream exists and the two overlap on an order fulfil — the first time simultaneous
  bursts are visible, and the first use of the world→screen origin path.
- **After M3** the headline case works, and the game gains a resource readout it never had.

## Production Order

| Milestone | Assets | UI mockups | Notes |
|---|---|---|---|
| 1 | `Assets/Art/UI/Icons/coin.png` — **exists** | none | No new surface; the money pill only gains a pulse. |
| 2 | `Assets/Art/UI/Icons/xp.png` — **DONE** (cut + imported + approved 2026-07-22) | none | Was the one critical-path item; now clear. |
| 3 | Resource icons — **exist** on each `ResourceSO` | `ResourcePill.prefab` — no Figma pass | Duplicate of the authored money pill with the icon swapped. |

**Audio, all three milestones:** clips come from `Assets/Casual Game Sounds U6/CasualGameSounds/` — **51
opaquely-named `DM-CGS-NN.wav` files**, so they must be auditioned, not picked by name. Assign on
`Assets/Data/SO/SfxLibrary.asset`, which currently has clips on roughly 7 of its 24 cues.

**Catalog reconciliation:** `docs/assets/03-vfx.md` already lists **`vfx.collectPop`** — "small void-accent
burst on collect (the loop's reward beat)", trigger `job:collected`, placeholder "scale-pop only", and flags
it as one of the two to prioritise. **That is M3's beat.** This feature supersedes the `scale-pop only`
placeholder; the catalog entry should be updated to point at the particle burst rather than a new VFX being
authored separately. No `docs/UI-Inventory.md` surface exists for the resource pill, and no `docs/assets/`
entry exists for the XP icon (`xp.png`) — both are new and unreconciled.

**Critical path: clear.** The star sprite was the only blocking asset and it is done. Every milestone can
now start on demand.

**No Figma pass for the resource pill** — a deliberate exception to the standing new-UI rule, agreed with the
user. There is no layout to iterate: the pill is a duplicate of the money pill chrome, anchored beneath it.
The only real unknown is *motion*, which a static mock cannot answer.

## Decisions Made

1. **Bursts trigger off the earn events, not the counter-change events.** Structurally excludes level-up
   rewards and debug cheats instead of filtering them.
2. **Crediting is event-driven; both events carry an amount.** See the architecture section — this reverses
   the design source.
3. **`displayed = true − pending`**, with arrive-or-credit-on-destroy so no chunk can strand.
4. **Chunking lives in `Core/Rules/` and is tested.** Reverses the design source.
5. **Flight follows a live `RectTransform`.** Reverses the design source; required by M3.
6. **Origins are per-source queues**, so two same-source earns in one frame don't collide.
7. **Flush in `LateUpdate`**, because `XpGained` is published nested inside `JobCollected`.
8. **Origin is the station for world earns, the pointer for UI earns.**
9. **No new ScriptableObject**; pulse tunables live on the view that pulses.
10. **Three SFX cues, not one.** A coin and a star sounding identical would flatten two simultaneous streams.
11. **Instantiate/Destroy, no pool.** ≤10 UI images per burst.
12. **The resource pill shows the running total** (`14 → 15 → 16`), not `+3` — the same number the totals
    popup shows, so the retract-behind-the-money-pill motion teaches what tapping it opens.
13. **Multiple output types stack downward as separate pills** rather than cycling one pill.
14. **The money pill pulses as a whole rect.** It has no coin icon — its only child is `Amount`, a `Text`,
    and the `$` is a literal in `Hud.cs`'s format string. Adding a coin `Image` is a UI change this feature
    has no mandate for. The XP badge and the resource pill *do* have icons and pulse those.
15. **`EarnParticleArrived` / `EarnBurstLaunched` live in `Core/Events/GameEvents.cs`** with every other
    event, because that is where the bus is and there is no View-side event catalog. Both are plain
    string/int structs — no `UnityEngine` type crosses the boundary. Consequence: every milestone re-runs
    the EditMode suite, since the Core tests compile `GameEvents.cs`.
16. **`M3`'s rail owns its own layout arithmetic** rather than a `VerticalLayoutGroup`, because pills
    animate in and out independently and a layout group would fight the slide tween every frame. Pitch and
    first-slot offset are serialized, not literals.

## Audit Reversals

Folded in from three cold auditors, recorded so nobody re-derives the rejected version from
`plans/collection-particles.md`:

| Was | Now | Why |
|---|---|---|
| Controller calls `AddPending`/`Credit` on three views | Both events carry `Amount`; views subscribe | Rule 2 — the original cited it for sound only |
| `Fly(Vector2 from, Vector2 to, …)` | `Launch(…, RectTransform target, …)`, live-sampled | M3's pill moves during flight; fixed endpoint = M1 rework |
| Chunking in the View, untested | `Core/Rules/EarnChunks` + EditMode tests | CLAUDE.md's one mandatory-test carve-out |
| Origin table introduced in M2 | Introduced in M1 | M2 asserted M1 built something M1 never specified |
| Origin = one slot per source | Queue per source | Two same-source earns in one frame collided |
| Eases hardcoded in `Fly` | Serialized `Ease` fields | Rule 1 — the one tunable the user named by hand |
| Pulse tunables on the controller | On each destination view | Three docs gave three different homes |
| M1 takes `stationRoots`/`worldCamera` unused | M2 adds them | The "signature won't churn" rationale was false — it churns in M2 and M3 anyway |
| Rail is a later sibling than `MoneyPill` | Rail is an **earlier** sibling | UGUI renders later siblings on top — the instruction was inverted |

## Assumptions
Each is a risk; what breaks if it's wrong.

- **`Pointer.current` is non-null when an order is fulfilled.** True for touch and mouse. A fulfil driven
  from the bus (every scripted test in this project, since pointer injection is unavailable) has no
  meaningful pointer and will originate the burst from wherever it last was. Expected, not a bug.
- **0.06s stagger × 10 + ~0.6s flight ≈ 1.2s** reads as satisfying rather than sluggish. Pure guess; first
  thing to retune once M1 is playable.
- **Particles rendering over open panels is wanted.** `FxCanvas` at 100 means a coin crosses the order panel
  on its way to the money pill. If that reads badly, the answer is a lower sort order.
- **`OrderFulfilled.Payout` and `ResourceAmount.Amount` are always > 0.** `EarnChunks.Split` throws
  otherwise, which will surface it loudly rather than dividing by zero.

## Gotchas
Traps a cold implementer will hit. ★ = would silently produce wrong behaviour.

- **★ UGUI renders LATER siblings on top.** To slide out from *behind* the money pill, the rail must be an
  **earlier** sibling than `MoneyPill`. Verified live (2026-07-22): `HudCanvas`'s children are `MoneyPill`,
  `GemPill`, `DebugButton`, `TotalsPopup`, `DebugMenu`, `BuildMenuButton`, `BuildTray`, `LevelXpPill`,
  `ToastStack` — **`MoneyPill` is index 0**, so the rail must be inserted at index 0.
- **★ `LevelXpHud`'s pop cannot be made smaller by shortening it.** `Update()` computes
  `t = clamp01(_popRemaining / badgePopSeconds)` then `scale = 1 + (badgePopScale − 1)·sin(t·π)`
  (`LevelXpHud.cs:77-80`). Setting `_popRemaining` below `badgePopSeconds` starts `t` mid-curve, so the badge
  **snaps** to ~95% amplitude and decays. Amplitude is governed by `badgePopScale`. M2 must parameterise the
  pop (`_popScale` / `_popSeconds` set at trigger time) — see `02-xp-stars.md`.
- **★ `HudCanvas` is `ScreenSpaceOverlay`**, so every `RectTransformUtility.ScreenPointToLocalPointInRectangle`
  call passes **`null`** as the camera. Passing `Camera.main` compiles, runs, and silently yields wrong
  coordinates.
- **★ `Pointer.current.position` is a `Vector2Control`, not a `Vector2`** — the Input System package is in
  use. `.ReadValue()` is required or it does not compile.
- **★ Adding a value to `SfxCue` requires re-syncing `SfxLibrary.asset`.** `SfxController.Init` throws if any
  cue lacks a row, and rows are only rebuilt by `SfxLibrarySO.OnValidate` (its sole caller). Select the asset
  in the inspector after adding the enum values. **Append only** — the enum's integers are what the asset
  serializes, and inserting mid-list silently reassigns every clip below.
- **★ The XP burst amount must come from `XpGained.Amount`, not `XpConfigSO.perJobCollected`.**
  `Progression.AwardXp` runs the amount through `ValueResolver` (`ResolveKind.XpGain`) before publishing, so
  an XP-boosting upgrade would make the two disagree. It also returns early at 0, so no event means no burst.
- **★ "Every view unsubscribes in `OnDestroy`" is FALSE in this codebase.** `Hud` (7 subscriptions),
  `OrderBoardPanel` (8), `StationPanel` (12) and `WorldState` (3) all subscribe with **inline lambdas** and
  have **no `OnDestroy` at all**. New subscriptions added to `Hud` in M1 therefore have no existing teardown
  to join — use **named handlers** and add an `OnDestroy`, matching `SfxController` / `LevelXpHud` rather
  than the file you are editing. Relatedly, `Hud.cs:70`'s `MoneyChanged` handler is an anonymous lambda, so
  there is no existing method to modify — it must be converted.
- **★ No authored recipe has more than one output.** All 12 `Recipe_*.asset` files in `Assets/Data/SO/` have
  exactly one `outputs` entry. The *types* support more (`RecipeSO.outputs` is a `List<Ingredient>`,
  `JobCollected.Outputs` is an `IReadOnlyList<ResourceAmount>`) and M3 handles it — but the multi-pill case
  **cannot be exercised without authoring a temporary two-output recipe**, which M3's test steps now say to do.
- **★ Level-ups cannot grant resources.** `LevelEntryKind` is `{ StationType, Upgrade, StationCap,
  QueueDepth, OrderSlots, Money, Gems }` — there is no resource kind, and every authored grant in
  `Levels.asset` is a cap/queue/slots bonus or `Money`. Do not write a test for "a level that grants a
  resource"; the exclusion to verify is the **Money** grant.
- **★ The gem pill HAS SHIPPED and owns the slot under the money pill.** Not hypothetical — Gems-Currency M1
  is committed (`fe0e83c`) and `HudCanvas/GemPill` is live at anchor/pivot `(1,1)`, `anchoredPosition
  (-24, -144)`, `240×96`, **sibling index 1**. Verified in the editor 2026-07-22.
  **So M3's rail starts at `firstSlotOffset = -264`** (the next 120px row down), and inserts at sibling
  index **0**, pushing `MoneyPill` to 1 and `GemPill` to 2.
  The Gems LOG also records an approved plan to move `hud.eggButton` down 116px for a **money → gems → egg**
  right edge — `hud.eggButton` is **not built yet**, so -264 is free today, but re-check the column before
  authoring. `firstSlotOffset` and `slotPitch` are serialized precisely so this is a field edit, not a code
  change. The retract target is always the money pill regardless of where pills rest, so the "teaches the
  affordance" motion survives any offset — which is why the resource pill yields (it is transient; gems and
  eggs are permanent).
- **`GameBoot` has no `Init` method** — the injection sequence is in `void Start()` (`GameBoot.cs:50-217`,
  `hud.Init(...)` at `:203`). It also has a `RequireWired()` null-check list at `:221` that every new
  serialized dependency is expected to join.
- **Verified HUD geometry** (`HudCanvas` reference resolution 1080×1920, `scaleFactor` 2, `sortingOrder` 20):

  | Object | Anchor/Pivot | anchoredPosition | Size | Children |
  |---|---|---|---|---|
  | `MoneyPill` | `(1,1)` | `(-24, -24)` | `240×96` | `Amount` (Text) **only — no icon** |
  | `LevelXpPill` | `(0.5,1)` | `(0, -24)` | `380×96` | `Badge`→`Number`, `Caption`, `BarTrack`→`BarFill` |

- **Only `Core/` and `Tests/` have asmdefs.** `View/`, `Systems/` and `Data/` are in `Assembly-CSharp`,
  which sees plugin DLLs automatically — `using DG.Tweening;` needs no assembly-reference work.
- **DOTween Pro is installed** at `Assets/Plugins/Demigiant/`, `Assets/Resources/DOTweenSettings.asset` has
  `uiEnabled: 1`. Do not re-run setup.
- **`StationRegistry.Roots` is the live shared map** already injected into `cameraController`, `worldState`,
  `stationPanel` and `stationFlattenMask`. Take the same reference; never build a second map.
- **SO assets live in `Assets/Data/SO/`**, not `Assets/Data/`.
- **The editor does not advance frames while unfocused.** Set `Application.runInBackground = true` before
  verifying anything in playmode.
- **Pointer injection is unavailable**, so no milestone can verify a real tap. Drive chains by publishing the
  intent on the bus and verify tap wiring by inspection — and say so in the log rather than implying a tap
  was tested.

## Open Items

- **★ The umbrella-cue collision needs the user's ear.** Existing cues fire on the same three events, so one
  order fulfil could produce 22 sounds. Options: (a) silence `SfxCue.OrderFulfilled` / `XpGained` /
  `JobCollected` and let the particle stream carry the beat; (b) keep the umbrella cue and set a
  `minInterval` on the particle cue. **(b) contradicts the user's literal "a sound effect for each
  particle"**, so (a) is the default — but `SfxController.cs:138`'s existing comment ("Income already has a
  voice — the order chime… doubling it would just muddy the payout") is precedent for exactly this
  reasoning, and the user should hear it before it's locked. **Decide during M1, with sound on.**
  *(Note: `SfxLibrarySO.Entry.minInterval` defaults to 0, so it is safe out of the box — but any value above
  `staggerSeconds` silently drops arrivals, which looks like a bug and isn't.)*
- **Feel tuning** (stagger, flight duration, scatter radius, dwell) is guessed and expected to change after
  M1 is playable. Not blocking.

## Deferred
Named so nobody builds them early:

- **Particles for level-up rewards.** The feature's defining exclusion.
- **A persistent storage/silo HUD counter.** M3's pill is transient by design.
- **Object pooling.**
- **Particles for `StationBuilt`, `UpgradePurchased`, or any spend.** Only the three earn events.
- **Cleaning up in-flight particles on a debug reset.** They credit on destroy into an already-snapped
  counter, so the totals stay correct; the visual is harmless.

## Testing

Per CLAUDE.md the pure-C# economy core is the one place tests are not suspended. **`Core/Rules/EarnChunks`
is exactly that** — integer arithmetic whose bugs are invisible and make totals subtly wrong. M1 adds
EditMode tests for it: exact-sum for amounts 1…50 against several `maxParticles`, the remainder distribution,
and the fail-loud on `amount <= 0`. Everything else (flight, pulse, pill motion) is View — verify by playing.

Every milestone also **re-runs the whole EditMode suite**, because all three touch `GameEvents.cs`, which the
Core tests compile. **M1 runs the suite first and records the real live baseline**; counts written in other
docs are stale (the game LOG says 71, the Gems LOG says 83).
