# Milestone 01 — Money Particles

**Demonstrable outcome:** press Play, open the Order Board, and fill an order. Coins spray out from where
your finger is, one after another, drift and then accelerate into the money pill. The money counter does
**not** jump to the new total — it climbs one coin at a time, and lands on the exact payout when the last
coin arrives. Each arrival plays a sound and pops the pill.

## Goal

Build the whole mechanism — chunking, buffering, the origin queue, stagger, live-target flight, deferred
credit, arrival sound, pulse — against the destination that already exists in the HUD and needs no new UI.
M2 and M3 are new *paths* through machinery proven here.

This ordering is deliberate: if the feel is wrong, it is wrong in this milestone, before a single new surface
has been authored. **The API shapes fixed here are the ones M2 and M3 depend on** — the audit found three
places where a convenient M1 shortcut would have forced M3 to tear M1 up. They are called out below.

## Build This

### Core

- **`Core/Rules/EarnChunks.cs`** — pure static, no `UnityEngine`:
  ```csharp
  public static int[] Split(int amount, int maxParticles)
  ```
  `count = Mathf`-free `Math.Min(amount, maxParticles)`; each entry `amount / count`; the first
  `amount % count` entries get one extra. **Throws** on `amount <= 0` or `maxParticles <= 0` — an empty
  burst is a caller bug, not a state to clamp past.

- **`Tests/`** — EditMode tests for `EarnChunks`: the chunks sum to `amount` exactly for every amount 1…50
  across several `maxParticles`; the count never exceeds `maxParticles`; the remainder lands on the leading
  entries; `Split(0, 10)` and `Split(-1, 10)` throw. This is the one thing in the feature CLAUDE.md's
  test carve-out actually covers.

- **`Core/Events/GameEvents.cs`** — two events, in the Economy section beside `MoneyChanged`:
  ```csharp
  public readonly struct EarnBurstLaunched
  {
      public readonly string Kind;       // "money" | "xp" | "resource"
      public readonly string ResourceId; // null unless Kind == "resource"
      public readonly int Amount;        // the whole burst
      …
  }

  public readonly struct EarnParticleArrived
  {
      public readonly string Kind;
      public readonly string ResourceId;
      public readonly int Amount;        // this particle's chunk
      …
  }
  ```
  Facts, not instructions. **`Amount` on both is load-bearing** — it is what lets each destination own its
  own pending count instead of being driven by the controller (CLAUDE.md rule 2). Plain string/int only, so
  nothing from `UnityEngine` crosses the Core boundary.

### View

- **`View/EarnParticle.cs`** — one flying icon: `RectTransform` + `Image`.
  ```csharp
  public void Launch(Sprite icon, Vector2 fromLocal, RectTransform target,
                     FlightSettings settings, Action onArrive)
  ```
  - Sets `Image.sprite = icon` (nothing else assigns it — the controller passes it in).
  - **Scatter**, then **flight**. ★ The flight must **follow a live target**, not a baked `Vector2`:
    `DOVirtual.Float(0f, 1f, settings.flightSeconds, t => rect.anchoredPosition =
    Vector2.Lerp(scatterPoint, TargetLocal(), t)).SetEase(settings.flightEase)`, where `TargetLocal()`
    re-reads `target` each tick. **`DOAnchorPos` bakes the endpoint and will be wrong in M3**, where the pill
    is still sliding out while the first coins are already in the air.
  - **Fires `onArrive` exactly once**, from `OnComplete` — **and from `OnDestroy` if it never completed.**
    Kill the tween in `OnDestroy` after that. Without this, a torn-down canvas or a `DOTween.KillAll` strands
    a chunk in `pending` and every counter is permanently understated. The whole subtract-pending scheme
    depends on this guarantee.
  - Sizing comes from the prefab's own rect. Do not resize from the controller.

- **`View/EarnBurstController.cs`** — the engine, on `FxCanvas`. `Init(EventBus bus)`.
  - **`[Serializable] public struct FlightSettings`** — `scatterRadius`, `scatterSeconds`, `scatterEase`,
    `flightSeconds`, `flightSecondsJitter`, `flightEase`. One serialized instance on the controller, passed
    by value into `Launch`. (`DG.Tweening.Ease` is an enum and serializes to the inspector — the eases are
    **tunables**, not literals in a method call.)
  - Other serialized fields: `earnParticlePrefab`, `maxParticles` (10), `staggerSeconds` (0.06),
    `coinSprite` (→ `Assets/Art/UI/Icons/coin.png`), `moneyTarget` (a `RectTransform`, inspector-wired to
    `HudCanvas/MoneyPill`).
  - **Buffering.** `OrderFulfilled`'s handler only *records*; it spawns nothing. Two buffers:
    - a **pending-burst list** — `(kind, resourceId, amount)`
    - an **origin queue per source string** — `Dictionary<string, Queue<Vector2>>`. ★ A queue, not a single
      slot: M2 records one origin per `JobCollected`, and two collects in one frame would otherwise
      overwrite each other. Build it as a queue now so M2 is a one-line addition.
    `OrderFulfilled` enqueues `("order", pointerLocal)` **and** adds a `("money", null, e.Payout)` burst.
  - **`LateUpdate()` flushes**, then clears both buffers. Per burst: `EarnChunks.Split(...)`, publish
    `EarnBurstLaunched`, then start a coroutine that spawns one `EarnParticle` per chunk `staggerSeconds`
    apart, each publishing `EarnParticleArrived` on arrival.
    ★ Flushing in `LateUpdate` is a **correctness** requirement, not tidiness — see the summary.
  - **Origin:** `Pointer.current.position.ReadValue()` (★ `.position` is a `Vector2Control`; without
    `ReadValue()` it does not compile), converted via
    `RectTransformUtility.ScreenPointToLocalPointInRectangle(fxRect, screenPoint, null, out var local)` —
    ★ **`null`**, because the canvas is Overlay.
  - Named handlers, `Unsubscribe` in `OnDestroy`.

- **`View/Hud.cs`** — the deferred money counter and the pulse.
  - ★ `Hud.cs:70`'s `MoneyChanged` handler is an **anonymous lambda**, and `Hud` has **no `OnDestroy` at
    all** (7 lambda subscriptions). Convert the money handler to a named method, add the two new
    subscriptions as named methods, and **add an `OnDestroy` that unsubscribes all three.** Follow
    `LevelXpHud` / `SfxController`, not the file you are editing. Leaving the other four lambdas alone is
    fine — do not refactor them.
  - Cache `_trueMoney` from `MoneyChanged`; add `_pendingMoney`; render
    `moneyText.text = $"$ {_trueMoney - _pendingMoney}"` from one `RefreshMoney()`.
  - Subscribe `EarnBurstLaunched` → if `Kind == "money"`, `_pendingMoney += Amount`, refresh.
  - Subscribe `EarnParticleArrived` → if `Kind == "money"`, `_pendingMoney -= Amount`, refresh, **and pulse**.
  - The pulse scales the **`MoneyPill` rect itself** — it has no icon child (its only child is `Amount`, a
    `Text`; the `$` is a literal in the format string). Serialized `pulseScale` (1.18) / `pulseSeconds`
    (0.18) **on `Hud`**, because `Hud` is the thing that pulses.
  - **No public `AddPending` / `Credit` API.** `Hud` learns from the bus. If you find yourself adding a
    method for `EarnBurstController` to call, the design has regressed — see the summary's Audit Reversals.

### Data & wiring

- **`Data/SfxLibrarySO.cs`** — ★ **append** three values to `SfxCue` (never insert): `EarnParticleMoney`,
  `EarnParticleXp`, `EarnParticleResource`. All three now; splitting an enum across three milestones is worse
  than two unused values. Then **select `Assets/Data/SO/SfxLibrary.asset` in the inspector** so `OnValidate`
  → `SyncRows` adds the rows, or `SfxController.Init` throws on boot.

- **`View/SfxController.cs`** — subscribe `EarnParticleArrived`, switch on `Kind` to the matching cue,
  unsubscribe in `OnDestroy`. Assign a clip to the money cue from
  `Assets/Casual Game Sounds U6/CasualGameSounds/` — ★ 51 files named `DM-CGS-01.wav`…, so **audition
  them**; the names tell you nothing.

- **★ Decide the umbrella-cue collision, with sound on** (summary → Open Items). `SfxController` already
  plays `SfxCue.OrderFulfilled` on this very event, so one fulfil now fires the order chime **plus** up to
  10 coin cues. Default is to silence the umbrella cue and let the stream carry the beat — but play it both
  ways and let the user pick. Record the choice in `LOG.md`.

- **`Systems/Boot/GameBoot.cs`** — ★ there is **no `Init` method**; the injection sequence is in `void
  Start()` (`:50-217`). Add a serialized `earnBurstController` field, add it to the **`RequireWired()`**
  null-check list at `:221`, and call `earnBurstController.Init(bus);` in `Start()` after `hud.Init(...)`
  (`:203`).

- **Scene authoring (`Assets/Scenes/Farm.unity`)** — a new root `FxCanvas`: `Canvas`
  (`ScreenSpaceOverlay`, `sortingOrder = 100`), `CanvasScaler` matching `HudCanvas` (1080×1920 reference),
  **no `GraphicRaycaster`**, and the `EarnBurstController` component with `coinSprite` and `moneyTarget`
  wired.

- **`Prefabs/UI/EarnParticle.prefab`** — `RectTransform` (its own authored size) + `Image`
  (`raycastTarget` off) + `EarnParticle`.

## Do NOT Build This

- **XP stars.** No `XpGained` subscription, no `LevelXpHud` changes → M2.
- **Resource particles or the pill rail.** No `JobCollected` subscription, no `ResourcePill` → M3.
- **`stationRoots` / `worldCamera` on `Init`.** They are **not** taken this milestone. The signature churns
  in M2 and M3 regardless, so taking them early buys nothing and ships dead parameters. `Init(EventBus bus)`.
- **World→screen projection.** Nothing needs it yet → M2.
- **Particles for `MoneyChanged`.** The trigger is `OrderFulfilled` and only `OrderFulfilled`. Hanging it off
  `MoneyChanged` is the one change that silently breaks the feature's defining exclusion.
- **Particles on a spend.** Money leaving is not an earn.
- **`DOAnchorPos` for the flight.** It bakes the endpoint. See above.
- **Public credit methods on `Hud`.** The events carry the amount.
- **Object pooling.**
- **A coin `Image` inside the money pill.** Tempting, since the sprite is right there — but it is a UI change
  this feature has no mandate for.
- **A new ScriptableObject** for feel tunables.
- **Refactoring `Hud`'s other four lambda subscriptions.** Out of scope.

## Context

- **Existing:** `Hud` owns the money pill and subscribes `MoneyChanged` (`Hud.cs:70`, a lambda).
  `SfxController` maps events to cues and has a proper `OnDestroy`. `EventBus` is
  `Subscribe`/`Unsubscribe`/`Publish`, type-keyed, handlers invoked in subscription order.
- **New files:** `Core/Rules/EarnChunks.cs`, a test file under `Tests/`, `View/EarnParticle.cs`,
  `View/EarnBurstController.cs`, `Prefabs/UI/EarnParticle.prefab`.
- **Events added:** `EarnBurstLaunched`, `EarnParticleArrived`.
- **Data added:** three `SfxCue` values; rows auto-added to `SfxLibrary.asset` by `OnValidate`.
- **Systems touched:** `Core/Events/GameEvents.cs`, `Data/SfxLibrarySO.cs`, `View/SfxController.cs`,
  `View/Hud.cs`, `Systems/Boot/GameBoot.cs`, `Assets/Scenes/Farm.unity`.

## Principles

- **Core boundary (rule 3):** `EarnChunks` and both events are plain C#. No `Vector2`, no `Sprite`.
- **Event-driven (rule 2):** the controller announces; `Hud` and `SfxController` each decide what it means.
  The controller holds no reference to `Hud`.
- **Data-driven (rule 1):** every number *and both eases* are serialized. No literal durations, radii,
  counts or curves in a method call.
- **Unity-native authoring (rule 4):** `FxCanvas` is authored in the scene; `EarnParticle` is an authored
  prefab instantiated per particle because its *count* is data. Nothing from `new GameObject`.
- **Fail loud:** `EarnChunks.Split` throws on a non-positive amount.
- **Verify the API:** DOTween is new here. Check `Assets/Plugins/Demigiant/DOTween/DOTween.XML` or
  `Modules/DOTweenModuleUI.cs` for the exact `DOVirtual.Float` / `Sequence` / `SetEase` signatures rather
  than recalling them.

## Assets Required

- `Assets/Art/UI/Icons/coin.png` — **already exists**. `[placeholder OK]`
- One SFX clip auditioned from `Assets/Casual Game Sounds U6/CasualGameSounds/`. `[placeholder OK]`

## UI Mockups Required

**None.** No new visible surface — the money pill is already authored and only gains a scale pulse.

## Definition of Done

- Boot is clean; `SfxController.Init` does not throw (the library asset was re-synced).
- `EarnChunks` EditMode tests pass, including the fail-loud cases.
- Filling an order spawns exactly `min(payout, 10)` coins, launched one at a time.
- The money counter **lags** during flight and reads the exact true total once the last coin lands —
  verified against `Wallet.Money`, not by eye.
- A payout above 10 still lands on the exact total.
- **One sound per arrival**, and the umbrella-cue decision has been made with the user and recorded in
  `LOG.md`.
- The money pill pops per arrival.
- **Level-up money grants produce no particles**; the counter moves normally for them.
- Coins render *over* the open Order Board panel.
- `Hud` has an `OnDestroy` that unsubscribes its three new named handlers.
- The whole EditMode suite passes at its live baseline.

## How to Test

0. **Run the EditMode suite first and record the pass count.** That is the baseline every later milestone
   compares against; numbers in other docs are stale.
1. Set `Application.runInBackground = true` before entering playmode.
2. Enter playmode. Debug-add resources until an order is fillable, then tap **Fill**. Coins leave one at a
   time from the finger position, drift, then accelerate into the money pill.
3. **The lag is the point.** Pause mid-flight and compare the on-screen counter against `Wallet.Money`. They
   must differ. Resume; on the last coin they must match exactly.
4. **Big burst.** Fill a high-payout order (temporarily raise one if needed). Exactly 10 coins, and the final
   total exact — not one or two off. This is where a chunking bug shows.
5. **Exclusion check.** Publish `DebugLevelUpRequested` and cross a level that grants **Money** (that is the
   only reward kind that exists — `LevelEntryKind` has no resource kind). **No coins may appear.** If they
   do, the trigger is wired to `MoneyChanged` instead of `OrderFulfilled`.
6. **Spend check.** Buy a station mid-flight during a big burst. The counter drops by the cost while still
   lagging, and still lands exactly right.
7. **Strand check.** Start a big burst and exit playmode mid-flight, then re-enter and fill another order.
   The counter must be correct — no permanently missing amount. (This exercises the credit-on-destroy path.)
8. **Sound.** Play a fulfil with audio on and judge the umbrella-cue collision. Decide, apply, record.
9. **Layering.** Coins pass over the Order Board panel, not behind it.
10. **Tap-through.** A coin crossing a button does not swallow the tap.
11. Re-run the EditMode suite; confirm it matches the step-0 baseline.
