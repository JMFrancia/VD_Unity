# Collection Particles — Implementation Log

Running record across milestones. **Read this first when picking up cold.**

Milestones: `00-summary.md` · Design source: `plans/collection-particles.md` (**superseded** — see Audit
Reversals in the summary) · Game spec: `docs/VoidDay-Spec-unity.md` · Game log:
`milestones/VoidDay-Spec-unity/LOG.md`

---

## Before Milestone 01 — context carried in from design (2026-07-22)

No particle code exists yet. What a cold start needs to know, verified by reading the source and querying the
live editor during design, then re-checked by three independent cold auditors.

**The feature in one line:** earning something throws icon particles from the point of action to that thing's
HUD home, and the counter only moves as each particle lands.

**The exclusion that defines it:** level-up rewards throw nothing. This is achieved *structurally* — bursts
hang off `JobCollected` / `OrderFulfilled` / `XpGained`, never off `ResourceChanged` / `MoneyChanged`. A
level-up grant reaches the wallet through `Wallet.Add` and so never triggers a burst. There is no filter to
maintain. **If anyone rewires the trigger to a counter-change event, the feature silently breaks.**

### The design was cold-audited before any code — nine things were reversed

`plans/collection-particles.md` is kept as the design record but is **wrong in nine places**. The summary's
*Audit Reversals* table is the authority. The four that matter most:

1. **Crediting is event-driven.** Both `EarnBurstLaunched` and `EarnParticleArrived` carry an `Amount`, and
   each destination subscribes and owns its own `pending`. The original had `EarnBurstController` holding
   `Hud` / `LevelXpHud` / `ResourcePillRail` and calling `AddPending`/`Credit` on them, with an arrival event
   that carried no amount — which designed the compliant option out of reach while citing rule 2 for the
   *sound* path only.
2. **Flight follows a live `RectTransform`, not a baked `Vector2`.** M3's pill is still sliding out while the
   first particles are in the air, and reflows when a sibling retracts. `DOAnchorPos` bakes the endpoint;
   use `DOVirtual.Float` lerping to the target's current position. Getting this wrong in M1 means tearing up
   M1 during M3.
3. **Chunking lives in `Core/Rules/EarnChunks` with EditMode tests.** The original put it in the View and
   used that placement as the reason not to test it. It is integer arithmetic that decides whether displayed
   totals are exact — CLAUDE.md's one mandatory-test carve-out.
4. **Origins are a queue per source, introduced in M1.** The original introduced the origin table in M2 (so
   M2 asserted M1 had built something M1 never specified) and keyed it one-slot-per-source (so two jobs
   collected in the same frame — pet auto-collect — would both launch from the wrong station).

### Verified during design (don't re-derive)

**Packages and assemblies**
- **DOTween Pro is installed** at `Assets/Plugins/Demigiant/` (user imported it 2026-07-22).
  `Assets/Resources/DOTweenSettings.asset` has `uiEnabled: 1`, `spriteEnabled: 1`, `physicsEnabled: 1`.
  **Setup has already been run; do not re-run it.**
- **Only `Core/` and `Tests/` have asmdefs.** `View/`, `Systems/` and `Data/` compile into
  `Assembly-CSharp`, which references plugin DLLs automatically — `using DG.Tweening;` needs no assembly work.

**Event dispatch**
- **`EventBus` invokes handlers in subscription order** (`Delegate.Combine` on a type-keyed dictionary).
  Subscription order is `GameBoot`'s injection order. **Do not build on it.**
- **★ `Progression.AwardXp` publishes `XpGained` synchronously and then calls `AdvanceLevels()`**
  (`Progression.cs:55-62`), so a `JobCollected` dispatch nests `XpGained` *and* possibly `LevelUp` inside
  itself. This is the entire reason `EarnBurstController` buffers to `LateUpdate` instead of spawning in the
  handler.
- `AwardXp` **returns early when the resolved amount is 0** and runs the raw amount through `ValueResolver`
  (`ResolveKind.XpGain`) first. Always burst on `XpGained.Amount`, never on `XpConfigSO.perJobCollected`.
- **XP sources** are `"job"`, `"order"`, `"build"`, `"debug"` (`ProgressionSystem`). Only `"debug"` is skipped.

**Canvases and geometry** (read from the live editor — a proximity grep on `.unity` returns a *different*
object's document)
- **`HudCanvas` is `ScreenSpaceOverlay`**, `sortingOrder 20`, `scaleFactor 2`, reference resolution
  1080×1920. Scene canvas sort orders run 10 / 15 / 15 / 20 / 30, so `FxCanvas` at **100** is clear.
  ★ Overlay means every `RectTransformUtility.ScreenPointToLocalPointInRectangle` call passes **`null`** for
  the camera — passing `Camera.main` compiles and silently produces wrong coordinates.

  | Object | Anchor/Pivot | anchoredPosition | Size | Children |
  |---|---|---|---|---|
  | `HudCanvas/MoneyPill` | `(1,1)` | `(-24, -24)` | `240×96` | `Amount` (Text) **only** |
  | `HudCanvas/LevelXpPill` | `(0.5,1)` | `(0, -24)` | `380×96` | `Badge`→`Number`, `Caption`, `BarTrack`→`BarFill` |

- **★ `HudCanvas` child order is `MoneyPill`, `GemPill`, `DebugButton`, `TotalsPopup`, `DebugMenu`,
  `BuildMenuButton`, `BuildTray`, `LevelXpPill`, `ToastStack` — `MoneyPill` is index 0.** UGUI renders **later** siblings on
  **top**, so M3's rail must be inserted at index **0**, pushing `MoneyPill` to 1. "Below in sibling order"
  is the *opposite* of "behind".
- **★ `MoneyPill` has no coin icon.** Its one child is `Amount`; the `$` is a literal in `Hud.cs:70`'s format
  string (`$"$ {e.Total}"`). "Pulse the icon" therefore means pulse the whole pill rect. The XP badge and
  M3's resource pill *do* have icons and pulse those.
- **★ `LevelXpHud`'s pop cannot be shrunk by shortening it.** `Update()` computes
  `t = Clamp01(_popRemaining / badgePopSeconds)`, `scale = 1 + (badgePopScale − 1)·Sin(t·π)`
  (`LevelXpHud.cs:77-80`). A shorter `_popRemaining` starts `t` mid-curve, so the badge **snaps** to ~95%
  amplitude and decays. Amplitude is `badgePopScale`. M2 parameterises the pop (`_popScale` / `_popSeconds`
  set at trigger time). A DOTween tween on `Badge` would be overwritten every frame.

**Subscription hygiene — the codebase is NOT consistent here**
- **★ "Every view unsubscribes in `OnDestroy`" is FALSE.** `Hud` (7 subscriptions), `OrderBoardPanel` (8),
  `StationPanel` (12) and `WorldState` (3) all subscribe with **inline lambdas** and have **no `OnDestroy`
  at all**. `LevelXpHud` and `SfxController` do it properly — **follow those**, not the file you are editing.
- Consequently `Hud.cs:70`'s `MoneyChanged` handler is an anonymous lambda: M1 must convert it to a named
  method and add an `OnDestroy`, because there is no existing teardown to join.

**Sound**
- **★ `SfxCue` values must be APPENDED only** — the enum's integers are what `SfxLibrary.asset` serializes,
  and the enum carries a comment saying so. **After adding values, select
  `Assets/Data/SO/SfxLibrary.asset` in the inspector** so `OnValidate` → `SyncRows` adds the rows
  (`SyncRows` has exactly one caller). `SfxController.Init` throws for any cue without a row. A cue with no
  clip is silent by design, not an error.
- **★ Umbrella-cue collision, unresolved and needing the user's ear.** `SfxController` already plays
  `SfxCue.OrderFulfilled` on `OrderFulfilled`, `SfxCue.XpGained` on `XpGained` and `SfxCue.JobCollected` on
  `JobCollected` — the same three events the bursts hang off. Unmuted, one order fulfil fires the order
  chime **plus** up to 10 coin cues **plus** the XP cue **plus** up to 10 star cues. Default is to silence
  the umbrella cues (`SfxController.cs:138`'s existing comment is precedent: "Income already has a voice —
  the order chime… doubling it would just muddy the payout"), but **decide it in M1 with sound on and record
  the choice here.** Throttling via `minInterval` instead would contradict the user's literal "a sound effect
  for each particle".
- `SfxLibrarySO.Entry.minInterval` defaults to 0 so it is safe out of the box — but any value above
  `staggerSeconds` silently drops arrivals, which looks like a bug and isn't.
- **Clips come from `Assets/Casual Game Sounds U6/CasualGameSounds/` — 51 files named `DM-CGS-01.wav`…**
  The names tell you nothing; audition them. `SfxLibrary.asset` currently has clips on roughly 7 of 24 cues.

**Data realities**
- **★ No authored recipe has more than one output.** All 12 `Recipe_*.asset` files in `Assets/Data/SO/` have
  exactly one `outputs` entry. The types support more (`RecipeSO.outputs` is a `List<Ingredient>`,
  `JobCollected.Outputs` is an `IReadOnlyList<ResourceAmount>`) and M3 handles it, but the multi-pill case
  **cannot be exercised without authoring a temporary two-output recipe.**
- **★ Level-ups cannot grant resources.** `LevelEntryKind` is `{ StationType, Upgrade, StationCap,
  QueueDepth, OrderSlots, Money, Gems }` (`LevelModel.cs:11-20`); every authored grant in `Levels.asset` is a
  cap/queue/slots bonus or `Money`. The exclusion to *verify* is the Money grant — do not write a test for a
  resource grant.
- **`OrderFulfilled` carries `{ OrderId, Payout, Xp }`**, and `ProgressionSystem` awards `e.Xp` from it, so
  one fulfil produces both a money burst and an XP burst.
- **`OrderBoardPanel` stays open through a fulfil** (subscribes `OrderFulfilled` → `RebuildIfOpen`), so money
  particles cross a live panel — hence `FxCanvas` above it.
- **`StationRegistry.Roots`** is the live shared `IReadOnlyDictionary<string, Transform>` already injected
  into `cameraController`, `worldState`, `stationPanel` and `stationFlattenMask`. Take the same reference.
- **SO assets live in `Assets/Data/SO/`**, not `Assets/Data/`.

**Input**
- **★ `Pointer.current.position` is a `Vector2Control`, not a `Vector2`** — the Input System package is in
  use (`InputRouter.cs:3,40`). `.ReadValue()` is required or it does not compile.
- **`InputRouter` ignores taps that start over UI** (`_pressStartedOverUi`), so it cannot supply the origin
  for an order fulfil. Read `Pointer.current` directly.

**Boot**
- **`GameBoot` has no `Init` method** — the injection sequence is `void Start()` (`:50-217`), with
  `hud.Init(...)` at `:203`. It also has a **`RequireWired()`** null-check list at `:221` that every new
  serialized dependency is expected to join.

**Catalogs**
- **`docs/assets/03-vfx.md` already lists `vfx.collectPop`** — "small void-accent burst on collect (the
  loop's reward beat)", trigger `job:collected`, placeholder "scale-pop only", flagged as one of two to
  prioritise. **That is M3's beat**; update the row rather than letting a separate VFX be authored for the
  same moment. No `docs/UI-Inventory.md` surface exists for the resource pill and no `docs/assets/` entry
  exists for the XP icon — both new and unreconciled.

**Assets**
- **Icon inventory** in `Assets/Art/UI/Icons/` (all 512×512 RGBA): bread, brioche, build, cheese,
  cheesecake, **coin**, corn, cornbread, cream, egg, gear, **gem**, heart, lock, milk, ready, slot-fill,
  slot-outline, storagefull, voidpet, wheat, **xp**.
- **★ `Assets/Art/UI/Icons/xp.png` is DONE** (2026-07-22) — the user's star, white-keyed and
  **unpremultiplied** so the antialiased edge is clean on dark, 512×512 with content 458×438 centred to match
  the siblings, importer cloned from `coin.png.meta` (`textureType: 8`, `spriteMode: 1`,
  `alphaIsTransparency: 1`), GUID `5dc45ffd6fa84db9871441092145eb39`. Previewed and approved. **M2 is not
  blocked.** Do not re-cut it.
- **★ `tools/asset-prep/cutout.py` is NOT a general CLI.** It ignores arguments and runs a hardcoded batch
  over `references/voidpet-ip/*.png` with creature-specific logic (text-label band removal, eye
  preservation). Invoking it re-cuts the pets as a side effect. It also needs PIL + numpy, which the system
  python lacks (use a venv on `python3.13`). For a new icon, adapt its white-key approach rather than
  calling it.

**Cross-feature**
- **★ The gem pill SHIPPED — the collision is real.** Gems-Currency M1 is committed (`fe0e83c`) and
  `HudCanvas/GemPill` is live at anchor/pivot `(1,1)`, `(-24, -144)`, `240×96`, sibling index 1 — verified in
  the editor 2026-07-22. **M3's rail therefore starts at `firstSlotOffset = -264`** and inserts at sibling
  index 0. The Gems LOG also records an approved plan to drop `hud.eggButton` 116px for a
  **money → gems → egg** right edge; that button is **not built yet**, so −264 is free today — re-read the
  column before authoring. `firstSlotOffset` / `slotPitch` are serialized so a later collision is a field
  edit, not a code change.
- **★ As of 2026-07-22 the working tree does NOT compile.** Gems-Currency M2 is mid-implementation:
  `Core/Rules/TimeSkip.cs` and `Core/Model/TimerRef.cs` are new and untracked, and
  `GameBoot.cs:193` calls `producer.Init(...)` with 7 arguments while `Producer.Init` still declares fewer —
  `error CS1501: No overload for method 'Init' takes 7 arguments`. **Nothing in this plan can start until
  that settles**: `tests-run` aborts on compile errors and playmode will not enter.

**Testing**
- **Nothing here adds a Core rule except `EarnChunks`** — which is exactly CLAUDE.md's mandatory-test
  carve-out and gets EditMode tests in M1. Everything else is View; verify by playing.
- Every milestone re-runs the full EditMode suite, because all three touch `GameEvents.cs`, which the Core
  tests compile. **The suite count in other docs is stale** (game LOG says 71, Gems LOG says 83).
  **M1 runs the suite first and records the real live baseline.**

**Environment gotchas** (project-wide)
- The editor does not advance frames while unfocused — set `Application.runInBackground = true` before
  verifying anything in playmode, or `screenshot-game-view` returns the frame from entering play mode.
- Pointer injection is unavailable, so no milestone can verify a real tap. Drive chains by publishing the
  intent on the bus and verify tap wiring by inspection — and note that a bus-driven `OrderFulfilled`
  originates its burst from wherever the pointer happens to be. Say so rather than implying a tap was tested.

**Open risks:** every feel number (`staggerSeconds` 0.06, `flightSeconds` 0.6, `scatterRadius` 90,
`dwellSeconds`) is a guess and is the first thing to retune once M1 is playable. The umbrella-cue decision is
unmade.

---

## Milestone 01 — Money Particles
**Status:** ✅ Complete · **Date:** 2026-07-22

**Built:** Filling an order now sprays coins from the pointer position into the money pill, and the money
counter climbs one coin at a time instead of jumping.

- `Core/Rules/EarnChunks.cs` — `Split(amount, maxParticles)`, pure C#, throws on either argument ≤ 0.
- `Tests/EarnChunksTests.cs` — 10 EditMode tests. **Live suite baseline is now 116** (was **106** before this
  milestone; the 71/83 figures in the other logs were stale). M02/M03 compare against 116.
- `Core/Events/GameEvents.cs` — `EarnBurstLaunched` and `EarnParticleArrived`, both carrying
  `(string Kind, string ResourceId, int Amount)`, plus a `EarnKind` constants class (`Money`/`Xp`/`Resource`).
- `View/EarnParticle.cs` — one flying icon. Scatter (`DOAnchorPos` to a fixed point) then flight
  (`DOVirtual.Float` lerping to `TargetLocal()` **re-read every tick**, so a still-moving destination is
  tracked). `onArrive` fires exactly once, from `OnComplete` **or** from `OnDestroy` if it never completed.
  `FlightSettings` is declared in this file (not in the controller file) — same namespace, no practical
  difference.
- `View/EarnBurstController.cs` — on the new `FxCanvas`. Subscribes `OrderFulfilled`, **records only**, and
  flushes in `LateUpdate`. Origins are a `Dictionary<string, Queue<Vector2>>` keyed by source string.
- `View/Hud.cs` — `_trueMoney` / `_pendingMoney`, `RefreshMoney()`, `Pulse()`, three **named** handlers and a
  new `OnDestroy` that unsubscribes exactly those three. The other four lambda subscriptions were left alone
  deliberately.
- `Data/SfxLibrarySO.cs` — `EarnParticleMoney` / `EarnParticleXp` / `EarnParticleResource` **appended** to
  `SfxCue`; `SfxLibrary.asset` re-synced to 27 rows. `View/SfxController.cs` plays one cue per arrival.
- `Systems/Boot/GameBoot.cs` — serialized `earnBurstController`, in `RequireWired()`, `Init(bus)` right after
  `hud.Init(...)`.
- `Scenes/Farm.unity` — new root `FxCanvas` (Overlay, `sortingOrder 100`, CanvasScaler 1080×1920 match 0.5
  matching `HudCanvas`, **no `GraphicRaycaster`**). Scene diff is **pure insertion** — 0 lines removed.
- `Prefabs/UI/EarnParticle.prefab` — 72×72 rect, anchors/pivot centred, `Image` with `raycastTarget` off and
  `preserveAspect` on.

**Deviations from the plan:** two, both cosmetic.
1. **The umbrella SFX cue is NOT silenced.** The plan's default was to mute `SfxCue.OrderFulfilled`; the user
   resolved this up front as **keep both** — the order chime still fires and the per-particle stream layers on
   top. This binds M02 (`XpGained`) and M03 (`JobCollected`) too — do not re-litigate it.
2. **The three new cues ship CLIPLESS**, by the user's decision, rather than auditioning a clip from
   `Assets/Casual Game Sounds U6/`. `SfxController.Play` already returns early on a null clip, so a clipless
   cue is silent, not an error, and boot does not throw.

**Tech debt:**
- **`EarnParticleMoney`, `EarnParticleXp` and `EarnParticleResource` have no clip assigned.** The rows exist
  on `Assets/Data/SO/SfxLibrary.asset` and the code plays them; they are simply silent until someone
  auditions the `DM-CGS-NN.wav` library by ear. **Nothing else is needed — drop a clip on the row.** Note
  that `Entry.minInterval` must stay ≤ `staggerSeconds` (0.06) or arrivals get silently throttled.
- **Every feel number is an unverified guess** shipped per the run decision: `staggerSeconds` 0.06,
  `flightSeconds` 0.6 ± 0.08 jitter, `scatterRadius` 90, `scatterSeconds` 0.18, eases `OutQuad`/`InQuad`,
  `pulseScale` 1.18, `pulseSeconds` 0.18. **All are inspector-exposed** on `FxCanvas/EarnBurstController` and
  on `Hud`. Retune by playing; no code change needed.
- A burst whose spawn coroutine is interrupted mid-stagger (controller disabled while spawning) would strand
  the un-spawned chunks in `pending`. It cannot happen today — the controller lives for the whole session —
  and the *flight* side is fully covered by credit-on-destroy.

**Assumptions:**
- **`maxParticles` = 10 is a hard ceiling, not a per-payout count.** A payout of 108 throws 10 coins of
  11/11/11/11/11/11/11/11/10/10. If a burst should *feel* proportional to size, this is the field to change.
- **Scatter can push a coin off-screen** when the launch point is near a screen edge (`scatterRadius` 90 in
  1080-wide reference space). Observed in verification. Harmless — the coin flies back in — but if it looks
  wrong, lower `scatterRadius`.
- **A real finger tap was never tested.** Pointer injection is unavailable, so every fulfil was driven by
  publishing `OrderFulfillRequested` on the bus; the origin therefore came from wherever the OS mouse
  happened to be. The `Pointer.current.position.ReadValue()` → `ScreenPointToLocalPointInRectangle(fxRect,
  screen, **null**, …)` path is verified by inspection and by the fact that particles spawned at plausible
  in-canvas coordinates.

**Gotchas for later milestones:**
- **`EarnKind` constants live in `Core/Events/GameEvents.cs`.** Use `EarnKind.Xp` / `EarnKind.Resource`, do
  not re-spell the strings.
- **The origin↔burst pairing rule:** `PendingBurst` carries a `Source` string, and `LateUpdate` dequeues
  **exactly one** origin from that source's queue per burst — and **throws** if the queue is empty. So every
  recorded burst must have enqueued exactly one origin under its source key **in the same frame**. M02's
  `XpGained` burst therefore needs its **own** enqueue (it cannot borrow the `"order"` origin that the money
  burst already consumed), and M03's `JobCollected` needs one enqueue per burst it records.
- **`IconFor` / `TargetFor` are `switch` expressions that throw on an unhandled kind.** M02 and M03 each add
  one arm plus the serialized sprite/target field beside `coinSprite` / `moneyTarget`.
- **`Hud` now has an `OnDestroy`.** If a later milestone converts more of its lambdas to named handlers, join
  that method rather than adding a second one.
- **`_origins.Clear()` runs every `LateUpdate`**, so an origin recorded with no burst behind it is dropped
  after one frame. That is intentional.
- **Playmode was NOT frozen during this milestone** — `Time.frameCount` climbed normally and coroutines and
  tweens ran. But `screenshot-game-view` still fails outright ("Game View render texture is not available"),
  so **state reads via `script-execute` remain the only verification channel.**
- **Verification recipe that worked**, reusable in M02/M03: reflect `_bus` and `_pool` off `Hud`, and
  `_board` / `_wallet` off `OrderBoardSystem`; subscribe probe handlers that write into
  `UnityEditor.SessionState` (statics do **not** survive between `script-execute` calls — each one compiles a
  fresh assembly, but `SessionState` does); trigger; then read the trace in a later call.
