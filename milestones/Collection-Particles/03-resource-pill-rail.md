# Milestone 03 — Resource Pill Rail

**Demonstrable outcome:** press Play and collect a ready job. A pill slides out from behind the money pill
carrying the wheat icon and its count; wheat icons fly from the station into it, ticking the count up one at
a time and pulsing its icon on each arrival; a moment after the last one lands the pill slides back behind
the money pill and disappears.

## Goal

The headline case — the one the feature was asked for. It is last because resources are the only earn with
**no HUD home at all**, so this milestone has to bring its own destination.

The pill retracts *behind the money pill* on purpose: that is the control the player taps to open
`popup.totalResources`, so the motion teaches the affordance.

**This also supersedes a catalog entry.** `docs/assets/03-vfx.md` lists `vfx.collectPop` — "small
void-accent burst on collect (the loop's reward beat)", trigger `job:collected`, placeholder "scale-pop
only", flagged as one of the two to prioritise. That is this beat. Update the catalog row to point at the
particle burst rather than letting a separate VFX get authored for the same moment.

## Build This

- **`Prefabs/UI/ResourcePill.prefab`** — a duplicate of the authored `HudCanvas/MoneyPill` chrome with the
  `$`-text swapped for an icon + count. Same `240×96`, same rounded background sprite, same type styling.
  - Components: `RectTransform`, `Image` (background), `CanvasGroup` (for the fade), child `Image` (resource
    icon), child `Text` (count), and a **`View/ResourcePill.cs`** behaviour.
  - `ResourcePill` owns `Bind(Sprite icon, int count)`, `SetCount(int)`, and **`Pulse()`** — a one-shot
    scale pop on its **icon child** (unlike the money pill, this destination genuinely has an icon to
    pulsate, which is what the user asked for). Serialized `pulseScale` / `pulseSeconds` **on `ResourcePill`**,
    because it is the thing that pulses.
  - `raycastTarget` off on everything — the pill is not interactive.

- **`View/ResourcePillRail.cs`** — on `HudCanvas`.
  - `Init(EventBus bus, ResourcePool pool, IReadOnlyList<ResourceSO> resources)`.
  - **Subscriptions** (named handlers, all unsubscribed in `OnDestroy`):
    - `EarnBurstLaunched` → if `Kind == "resource"`: ensure a pill exists for `ResourceId` (slide it out if
      not), `pending[id] += Amount`, refresh.
    - `EarnParticleArrived` → if `Kind == "resource"`: `pending[id] -= Amount`, refresh, `pill.Pulse()`,
      restart that pill's dwell timer.
    - `ResourceChanged` → refresh the pill for that id if it is out (a spend elsewhere must move the number).
    - `GameReset` → drop every pill immediately and clear all pending.
  - **`public RectTransform RectFor(string resourceId)`** — the controller's aiming query. Read-only; it
    mutates nothing. This is the feature's one direct cross-view call, kept because routing a transform
    through a Core event would break rule 3 to satisfy rule 2.
  - Renders `pool.Get(id) - pending[id]` — the same subtract-pending pattern as `Hud` and `LevelXpHud`.
  - **One pill per distinct resource**, stacked downward. Slot *n* sits at
    `anchoredPosition = (-24, firstSlotOffset - n * slotPitch)`, anchor/pivot `(1,1)`.
    **`firstSlotOffset` (−264) and `slotPitch` (120) are serialized fields, not literals** — see the slot
    collision below. −264 because the gem pill already owns −144.
  - **Slide-out from behind the money pill:** start at the money pill's own `anchoredPosition` with
    `CanvasGroup.alpha = 0` and a slightly-under-1 `localScale`, tween to the slot. Retract is the reverse.
    Serialized `slideSeconds`, `slideEase`, `dwellSeconds`.
  - Retract `dwellSeconds` after that pill's **last** arrival. An overlapping burst for the same resource
    **reuses the pill and extends the dwell**; it never spawns a second one.
  - **Reflow when a pill retracts:** remaining pills slide up to close the gap.
  - ★ **No `VerticalLayoutGroup`.** The pills animate in and out independently and a layout group would
    fight the slide tween every frame. The rail owns its own arithmetic — deliberately, not by oversight.

- **★ Sibling order — the instruction that is easy to get backwards.** UGUI renders **later** siblings on
  **top**. To slide out from *behind* the money pill the rail must be an **earlier** sibling than `MoneyPill`.
  Verified live 2026-07-22: `HudCanvas`'s children are `MoneyPill`, `GemPill`, `DebugButton`, `TotalsPopup`,
  `DebugMenu`, `BuildMenuButton`, `BuildTray`, `LevelXpPill`, `ToastStack` — **`MoneyPill` is index 0**. So
  insert the rail at index **0**.

- **★ The slot collision — REAL, not hypothetical.** Gems-Currency M1 **shipped** (commit `fe0e83c`) and
  `HudCanvas/GemPill` is live at anchor/pivot `(1,1)`, `(-24, -144)`, `240×96`, sibling index 1 — verified in
  the editor. **So the rail starts at `firstSlotOffset = -264`**, the next 120px row down.
  The Gems LOG also records an approved plan to drop `hud.eggButton` 116px for a **money → gems → egg** right
  edge; `hud.eggButton` is **not built yet**, so −264 is free today. **Re-read the column in the editor
  before authoring** — `firstSlotOffset` and `slotPitch` are serialized so a later collision is a field edit,
  not a code change. The resource pill yields because it is **transient** while gems and eggs are permanent,
  and the retract target is always the money pill regardless of where pills rest, so the "teaches the
  affordance" motion survives any offset.

- **`View/EarnBurstController.cs`** — the resource path.
  - `JobCollected` is already subscribed (M2 records its origin). Now **also buffer one burst per entry in
    `e.Outputs`**, tagged `Kind = "resource"` with that entry's `ResourceId` and `Amount`.
  - Icon: look the `ResourceSO` up by `ResourceAmount.ResourceId`. ★ The controller has **no resource
    catalog yet** — widen `Init` to take `IReadOnlyList<ResourceSO> resources` (the same list `GameBoot`
    already passes to `hud` and `siloPanel`) and build an id→SO dictionary. **Fail loud** on an unknown id.
  - Serialized: a `ResourcePillRail` reference (inspector-wired), used only for `RectFor`.
  - Destination: `rail.RectFor(resourceId)`, resolved at spawn — and the flight follows it **live**, which
    is why M1's `Launch(…, RectTransform target, …)` had to take a transform. The pill is still sliding for
    the first `slideSeconds` of the burst; a baked endpoint would aim the early particles at the money pill.
  - Wiring: **one mechanism only** — the rail is an inspector-wired `[SerializeField]` on the controller
    (rule 4's default). It is **not** passed through `Init`; `Init` injection is the carve-out for
    non-serializable core services, and a MonoBehaviour is not one.

- **`View/SfxController.cs`** — assign a clip to `SfxCue.EarnParticleResource`, auditioned from
  `Assets/Casual Game Sounds U6/CasualGameSounds/`.
  **★ Apply M1's umbrella-cue decision to `SfxCue.JobCollected`** — it fires on this same event.

- **`Systems/Boot/GameBoot.cs`** — a serialized `resourcePillRail` field added to the **`RequireWired()`**
  list (`:221`); `resourcePillRail.Init(bus, pool, resourceList)` in `Start()` before
  `earnBurstController.Init(...)`, whose call widens to include `resourceList`.

## Do NOT Build This

- **A persistent storage/silo counter.** The pill is transient by design; a permanent readout is a separate
  UI decision and is deferred.
- **Changes to `popup.totalResources` or `panel.silo`.** They re-render on `ResourceChanged` and are usually
  closed. *(Acknowledged deviation: opening the totals popup mid-flight shows the true total beside a lagging
  pill. The user's requirement was about the counter the particles feed; making every resource surface lag
  in sympathy is scope this feature does not have. Recorded as a decision, not an oversight.)*
- **Particles on a resource *spend*.** Fulfilling an order consumes goods and emits `ResourceChanged` with a
  negative delta. The controller does not listen to `ResourceChanged` at all — keep it that way.
- **A "level-up resource grant" exclusion or test.** ★ It does not exist: `LevelEntryKind` is
  `{ StationType, Upgrade, StationCap, QueueDepth, OrderSlots, Money, Gems }` — there is no resource kind and
  no authored grant uses one. Do not write a test for a mechanism the game does not have.
- **Particles for `DebugAddResourceRequested`.** The cheat routes through `ResourcePool.Add`, not
  `JobCollected`, so this is free — do not "fix" it.
- **Cycling one pill through several resources.** Stack them.
- **A `+N` display.** The pill shows the running total.
- **A `VerticalLayoutGroup`** on the rail. See above.
- **Passing the rail through `Init`.** Inspector-wired. One mechanism, not two.
- **Object pooling** for the pills or the particles.
- **A Figma pass.** Deliberately skipped, agreed with the user.

## Context

- **Existing from M1/M2:** the flight engine (live-target, credit-on-destroy), `EarnChunks` (+ tests), the
  origin queues (`JobCollected` already recorded), both events, the subtract-pending pattern on two
  destinations, all three `SfxCue` values.
- **New files:** `View/ResourcePill.cs`, `View/ResourcePillRail.cs`, `Prefabs/UI/ResourcePill.prefab`.
- **Events added:** none.
- **Systems touched:** `View/EarnBurstController.cs`, `Systems/Boot/GameBoot.cs`,
  `Assets/Scenes/Farm.unity` (HudCanvas, incl. **sibling reorder**), `Assets/Data/SO/SfxLibrary.asset`,
  `docs/assets/03-vfx.md` (the `vfx.collectPop` row).

## Principles

- **Unity-native authoring (rule 4):** `ResourcePill` is an authored prefab instantiated per resource because
  its *count* is data-driven. No hierarchy assembled in code, no runtime UI construction. The rail is
  inspector-wired.
- **Event-driven (rule 2):** the rail learns amounts from `EarnBurstLaunched` / `EarnParticleArrived` and
  truth from `ResourcePool` (injected state, as `Hud` and `SiloPanel` already do). The controller's only
  contact is the read-only `RectFor` query.
- **Data-driven (rule 1):** slide duration, ease, dwell, `slotPitch`, `firstSlotOffset` and the pulse are all
  serialized. Icons come from `ResourceSO.icon`.
- **Third occurrence rule:** subtract-pending now appears on `Hud`, `LevelXpHud` and `ResourcePillRail` —
  the third. **If** the three are genuinely identical, extracting a small helper is now warranted; the XP one
  works in floats against a bar, so they may not be. Look at the real code before abstracting; do not do it
  preemptively.

## Assets Required

- Resource icons — **already authored** on each `ResourceSO`. `[placeholder OK]`
- One SFX clip auditioned from `Assets/Casual Game Sounds U6/CasualGameSounds/`. `[placeholder OK]`

## UI Mockups Required

- `ResourcePill.prefab` — `[placeholder layout OK]`, no Figma pass. Match the money pill by **reading the
  authored rect**, not by eye. No `docs/UI-Inventory.md` surface exists for it; it is new and unreconciled.

## Definition of Done

- Collecting a job slides a pill out **from behind** the money pill with the right icon and the resource's
  current total.
- Icons fly from the station into that pill; the count ticks per arrival, the **icon pulses** per arrival,
  and the number lags `ResourcePool.Get(id)` mid-flight while matching it exactly at the end.
- **One sound per arrival**, with M1's umbrella-cue decision applied to `SfxCue.JobCollected`.
- The pill retracts behind the money pill after the dwell.
- Collecting the same resource again while its pill is out **reuses** the pill and extends the dwell.
- A collect yielding two resource types produces two stacked pills; when one retracts the other closes the
  gap. *(Requires a temporary two-output recipe — see testing.)*
- Debug-add-resource and order-fulfil consumption produce **no** particles and no pills.
- `docs/assets/03-vfx.md`'s `vfx.collectPop` row points at this feature.
- The EditMode suite passes at its baseline.

## How to Test

0. Run the EditMode suite and confirm the baseline.
1. **Re-read the right-edge column in the editor before authoring.** As of 2026-07-22 it is `MoneyPill`
   (−24) then `GemPill` (−144), so the rail starts at −264. If `hud.eggButton` has landed since, drop
   another row.
2. `Application.runInBackground = true`, enter playmode.
3. Collect a single-output job (a Field crop). Watch the pill slide out from behind the money pill, receive
   its icons, tick up, pulse per arrival, dwell, retract. Screenshot mid-flight and at rest.
4. **Lag check.** Pause mid-flight; compare the pill's number against `ResourcePool.Get(id)`. They must
   differ, and agree once the last icon lands.
5. **Multi-output.** ★ **No authored recipe has more than one output** — all 12 `Recipe_*.asset` files have
   exactly one `outputs` entry. Temporarily add a second output to one recipe (e.g. `Recipe_Field_WheatGrow`),
   collect it, confirm two pills stack, both fill, both retract, and the survivor reflows upward. **Revert
   the recipe afterwards.**
6. **Overlap.** Collect the same resource twice in quick succession → one pill, extended dwell, correct final
   total. Then two *different* resources in quick succession → two pills.
7. **Rapid multi-station collect.** Tap three ready stations back to back. Overlapping resource bursts *and*
   overlapping XP bursts are in the air at once; every counter must reconcile exactly and no pill may
   duplicate or strand. This is the stress case for the whole subtract-pending scheme.
8. **Depth.** Confirm the pill slides out from *behind* the money pill. If it slides out in front, the
   sibling order is inverted (later siblings render on top).
9. **Exclusion sweep.** In one session: hit a `+5 wheat` debug cheat, and fill an order that consumes goods.
   **Neither may spawn a particle or a pill.** *(Do not test "a level that grants a resource" — no such
   grant kind exists.)*
10. **XP still works.** Collecting a job fires resource icons *and* XP stars from the same station to two
    destinations. Both streams complete.
11. **Reset.** Publish `DebugResetRequested` mid-flight. Pills drop, pending clears, and the next collect
    behaves normally with correct totals.
12. Re-run the EditMode suite.
