# Milestone 02 — Skip Any Timer

**Demonstrable outcome:** open the Order Board, skip an order so a slot starts refilling. The refilling card
shows a skip button with a gem cost that *falls* as the timer runs down. Tap it → "Skip for ◆2?" → Confirm →
the slot fills immediately and the gem pill drops by 2.

## Goal
Build the whole skip mechanism — the Core rule, all three owner hooks, the events, and the shared confirm
popup — and prove the complete tap → confirm → spend chain end-to-end on the **order refill** timer.

The order refill is chosen as the proving ground because its UI is plain UGUI (`OrderCard` already authors a
`refillingRoot`), which is far faster to wire than the 3D in-world rig. **Core finishes here for all three
timer kinds**; M3 is pure view wiring on top of a rule that is already done and tested.

## Build This

### Core
- **`Core/Model/TimerRef.cs`**
  ```csharp
  public enum TimerKind { Job, Construction, OrderRefill }

  public readonly struct TimerRef
  {
      public readonly TimerKind Kind;
      public readonly string StationId; // Job / Construction
      public readonly int Slot;         // OrderRefill
      public static TimerRef Job(string stationId);
      public static TimerRef Construction(string stationId);
      public static TimerRef OrderRefill(int slot);
  }
  ```
- **Two members on each owner** — a read and a write. The write is a one-line timestamp nudge; the owner's
  existing `Tick` does the completion on its normal path. **Do not duplicate any completion logic.**
  - `JobSystem.HeadSecondsRemaining(id, now)` / `SkipHead(id, now)` — only a `Running` head is skippable.
  - `BuildSystem.SiteSecondsRemaining(id, now)` / `SkipSite(id, now)` — `Site.EndTime` stops being `readonly`.
  - `OrderBoard.RefillRemaining(slot, now)` **already exists** / `SkipRefill(slot, now)`.
- **`Core/Rules/TimeSkip.cs`** — the single authority. Holds the bus, the `GemPurse`, the three owners, and
  the two tunables.
  - `bool CanSkip(TimerRef, now)` — is there a live timer here at all
  - `int CostFor(TimerRef, now)` — `max(minGemCost, ceil(secondsRemaining / secondsPerGem))`
  - `void Skip(TimerRef, now)` — charge the purse, nudge the timestamp, publish `TimerSkipped`.
    **Throws** if the purse can't cover it (the popup disables its button, so this is a contract breach).

### Events
- `TimerSkipTapped(TimerRef Timer)` — input intent: the player tapped a skippable timer. States what
  happened, not what should follow.
- `TimerSkipConfirmed(TimerRef Timer)` — the confirm popup said yes.
- `TimerSkipped(TimerRef Timer, int Cost)` — Core fact.

### Systems
- **`Systems/TimeSkipSystem.cs`** — subscribes `TimerSkipConfirmed` → `TimeSkip.Skip(...)`, and
  `DebugAddGemsRequested` → `GemPurse.Add(...)` (move it here from wherever M1 parked it if that is tidier).
  Holds no rule. Subscribes in `Init`, unsubscribes in `OnDestroy`.

### View
- **`View/SkipConfirmPopup.cs`** + an authored UGUI popup on `HudCanvas`. Subscribes `TimerSkipTapped`,
  reads `TimeSkip.CostFor` for its copy, greys Confirm when `gems < cost`, publishes `TimerSkipConfirmed` on
  Confirm. It must **not** publish `ExclusiveUiOpened`, or it would retract the Order Board it was opened
  from. ⚠️ **This makes it the first non-exclusive UI surface in the project** — every existing surface
  (`BuildMenu`, `Hud`'s debug + totals popups, `LevelUpPopup`, `OrderBoardPanel`, `SiloPanel`,
  `StationPanel`) does publish it. There is no stacking-popup precedent to copy; this is a new pattern.
  Flag it as a tier-2 decision.
- **`OrderCard`** — a skip `Button` inside the existing `refillingRoot`, beside the "Refilling · 0:47" text,
  showing the live gem cost. `BindRefilling` gains the cost and an `onSkip` callback. ⚠️ Name it
  `skipTimerButton` — `OrderCard` **already has a `skipButton`** (`OrderCard.cs:26`) meaning "discard this
  order", and the two must not be confused.
- **`OrderBoardPanel`** — wires it. It already rebuilds every frame while open, so the falling cost needs no
  extra plumbing. ⚠️ **Hoist the loop variable before capturing it:**
  ```csharp
  int slotIndex = slot;   // `for` variables are NOT per-iteration in C# — capturing `slot` directly
                          // fires with slot == _cards.Count for every card.
  onSkip: () => _bus.Publish(new TimerSkipTapped(TimerRef.OrderRefill(slotIndex)))
  ```
  The existing `string orderId = order.Id;` at `OrderBoardPanel.cs:99` exists for exactly this reason.

### Tests (EditMode — this is Core, so the CLAUDE.md test carve-out applies)
- The cost curve: floor honoured at 1s remaining; `ceil` not `round` at boundaries; a long timer prices high.
- `SkipHead` completes the job on the next `Tick`, producing the same `JobCompleted` / `StationBlocked` pair
  a natural finish does.
- `SkipSite` completes the build on the next `Tick`, publishing `StationBuilt`.
- `SkipRefill` fills the slot on the next `Tick`, publishing `OrderGenerated` + `OrderSlotRefilled`.
- `Skip` throws on an unaffordable purse and leaves the timer untouched.

## Do NOT Build This
- **The in-world `TimerWidget` skip** — no collider, no cost label, no `InputRouter` change → M3.
  `TimerRef.Job` and `TimerRef.Construction` are built and tested here but unreachable from the UI. That is
  deliberate: infra ahead of the surface, kept invisible rather than shipped as an inert control.
- **Merging the three timer owners.** They stay separate; each gets its hook. See the summary's Decisions.
- **A `TimerSkipRefused` event / toast.** The popup disables Confirm when unaffordable, so there is no
  refusal path to announce.
- **Any non-timer gem sink.**

## Context
- **New:** `Core/Model/TimerRef.cs`, `Core/Rules/TimeSkip.cs`, `Systems/TimeSkipSystem.cs`,
  `View/SkipConfirmPopup.cs`, EditMode tests.
- **Touched:** `Core/Rules/JobSystem.cs`, `Core/Rules/BuildSystem.cs`, `Core/Rules/OrderBoard.cs`,
  `Core/Events/GameEvents.cs`, `Systems/Boot/GameBoot.cs`, `View/OrderCard.cs`, `View/OrderBoardPanel.cs`,
  `Assets/Scenes/Farm.unity` (HudCanvas popup), `Assets/Prefabs/**` (OrderCard).

## Principles
- **One rule, one home.** Pricing lives in `TimeSkip` and nowhere else. No View re-derives a cost.
- **Announce facts, not instructions** (rule 2): `TimerSkipTapped` says the player tapped a timer; the popup
  decides that means "ask them".
- **Core boundary** (rule 3): `TimerRef` is a plain struct — no `Vector3`, no Unity types.
- **Fail loud:** an unaffordable `Skip` throws rather than silently no-op'ing.

## Definition of Done
- A refilling order slot shows a skip button whose cost falls as the timer runs down.
- Confirming spends the gems and fills the slot immediately, via the *normal* `Fill` path — the card
  re-renders with a real order and `OrderGenerated` fires.
- Cancel spends nothing and leaves the timer running.
- At 0 gems the Confirm button is greyed and the skip does nothing.
- `TimeSkip.Skip` on `TimerRef.Job` and `TimerRef.Construction` works when driven directly from a test, even
  though no UI reaches them yet.
- New EditMode tests pass; the M1 baseline still passes.

## How to Test
1. Run the EditMode suite — the new cost-curve and per-owner skip tests, plus the M1 baseline still green.
2. Playmode (`runInBackground = true`): open the Order Board, Skip an order. Screenshot the refilling card
   showing the skip button and its cost.
3. Wait a few seconds and re-screenshot — the cost has fallen.
4. Publish `TimerSkipTapped(TimerRef.OrderRefill(0))` on the bus → screenshot the confirm popup reading
   "Skip for ◆N?".
5. Publish `TimerSkipConfirmed` → the slot fills immediately and the gem pill drops by N.
6. Publish `TimerSkipTapped` and then dismiss via Cancel → confirm nothing is spent and the refill timer is
   still counting down.
7. Spend to 0 gems, publish `TimerSkipTapped` again → screenshot the greyed Confirm button.
8. Drive `TimeSkip.Skip(TimerRef.Job(...))` and `TimerRef.Construction(...)` from a test and confirm the job
   completes / the station finishes building.
