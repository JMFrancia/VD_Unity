# Gems Currency — Milestone Plan

**Design source:** this plan (no separate spec — the feature was designed straight into these docs)
**Generated:** 2026-07-22

A second currency, **gems**, whose only job is buying time. The player starts with a few, earns a few more at
level-ups, and spends them to finish a running timer instantly.

"Playable" here means what it means everywhere in this project: press Play and see the new thing work.

**Why a shared authority rather than three ad-hoc skips.** This project's YAGNI rule would normally reject
`TimeSkip` as a premature abstraction over three call sites. It survives because the game has two more timer
sources already planned — M9 (VoidPets: hatch timers) and M11 (world events) — and the whole point of the
feature is that those become skippable by *calling* the rule, not by reimplementing pricing a fourth and
fifth time. If those milestones are ever cut, `TimeSkip` should be reconsidered.

## Milestones
| # | Name | Demonstrable outcome | Doc |
|---|---|---|---|
| 1 | The Currency | A gem pill reads `◆ 5` beside the money pill; cheats and level-ups move it. | `01-the-currency.md` |
| 2 | Skip Any Timer | Skip an order-slot refill for gems, through a confirm popup. | `02-skip-any-timer.md` |
| 3 | In-World Skip | Tap a job or construction radial to skip it for gems. | `03-in-world-skip.md` |

## The architecture, in one page

### The insight that makes this small

All three of the game's timers are already **absolute end-timestamps** (§13) driven by their owner's `Tick`.
So skipping is the same one-line mutation everywhere — *pull the end timestamp to now* — and each owner's
existing `Tick` completes the thing on its normal path. Nothing about completion, payout, blocking, or event
ordering is duplicated or special-cased.

| Timer | Owner | Skip is |
|---|---|---|
| Running job head | `JobSystem` | `head.EndTime = now` → `Tick` runs `Complete` |
| Construction site | `BuildSystem` | `site.EndTime = now` → `Tick` runs `Complete` |
| Order slot refill | `OrderBoard` | `slot.RefillAt = now` → `Tick` runs `Fill` |

### The pieces

- **`GemPurse`** (`Core/Rules/`) — a sibling of `Wallet`, same shape. Publishes `GemsChanged`.
- **`TimerRef`** (`Core/Model/`) — `{ Kind, StationId, Slot }` + three static factories. Names a timer
  without the caller knowing who owns it.
- **`TimeSkip`** (`Core/Rules/`) — the single authority: `CanSkip`, `CostFor`, `Skip`. Holds the purse, the
  three owners, and the two tunables.
- **`TimeSkipSystem`** (`Systems/`) — pumps intents into `TimeSkip`. Holds no rule.
- **`SkipConfirmPopup`** (`View/`) — one authored UGUI popup, shared by both tap surfaces.

### The chain

```
 in-world TimerWidget tap ─┐
                           ├─► TimerSkipTapped ─► SkipConfirmPopup ─► TimerSkipConfirmed ─► TimeSkipSystem
 OrderCard skip button ────┘                        "Skip for ◆2?"                            └─► Core
```

### Config — `GameConfigSO`, new `[Header("Gems")]`

| Field | Default | What it does |
|---|---|---|
| `startingGems` | 5 | Purse at boot and after a debug reset |
| `secondsPerGem` | 30 | One gem buys this many seconds |
| `minGemCost` | 1 | Floor — a nearly-done timer still costs something |

Cost formula: `max(minGemCost, ceil(secondsRemaining / secondsPerGem))`.

## Where the value lands
- **After M1** the currency exists and is visible. Nothing spends it yet, but the grant path and the HUD are
  proven, and the level curve can start paying gems out.
- **After M2** the whole tap → confirm → spend chain works end-to-end, and **Core is finished for all three
  timer kinds**. Only the order refill is reachable from the UI.
- **After M3** every timer in the game is skippable.

## Decisions Made

1. **Time-scaled pricing with a floor of 1.** Chosen over a flat cost so a long bake is worth more than a
   nearly-finished one. Two tunables, one formula, one home.
2. **The "shared timer system" is a shared *rule*, not a shared owner.** The job/build unification the user
   remembers was a **View**-layer change — `TimerWidget.prefab` is shared. Core-side the three owners stay
   separate; they each get a two-member hook. Do not attempt to merge them.
3. **Confirm popup on every skip, both surfaces.** The in-world widget is tappable as a whole rather than
   growing a separate skip pill, so without a confirm a player checking the time would spend gems by accident.
4. **`GemPurse` is a copy of `Wallet`, not a shared base class.** Two currencies is the second occurrence,
   not the third (CLAUDE.md: no abstraction until the third).
5. **Gem pill sits directly under the money pill, on the right edge** — a second HUD row, anchor/pivot
   `(1,1)`, `anchoredPosition (-24, -144)`, size `240×96`. The money pill really is top-right; the XP pill
   sits top-centre and leaves only 86px beside it, so a same-row placement would have to reflow a rect this
   feature has no business touching. See the LOG's verified geometry table.
6. **Timers only.** No buy-missing-order-goods, no instant station unlock, no gem-bought silo capacity. Those
   need pricing rules that aren't time, and the currency should be proven first.
7. **A level pays cash *or* gems, never both.** `LevelUpPopup.BuildReward` renders `rewards[0]` only, so
   widening the existing one-reward validation was free while rendering a reward *list* was not.
8. **Skipping a job into a full silo is allowed.** The job completes and sits blocked as `storage-full`,
   identical to letting it finish on its own. Refusing the skip would be a new rule for a state the player
   can already see coming.
9. **`TimeSkip.Skip` throws when the purse can't cover it.** The confirm popup disables its button, so an
   unaffordable call is a contract breach — exactly like `OrderBoard.Fulfill` throwing on missing goods.

## Assumptions
Each is a risk; what breaks if it's wrong.

- **Pulling an end-timestamp to `now` is sufficient to complete a timer.** True for all three owners today
  because each `Tick` tests `now >= EndTime`. If a future timer gates completion on something else, it needs
  a real `Skip` method rather than a timestamp nudge.
- **A one-frame delay between the skip and the completion is invisible.** `TimeSkipSystem` mutates the
  timestamp; the owner's `Update` completes it on the same or next frame.
- **5 starting gems / 30s per gem is roughly right.** Pure guess — it is the first thing to retune once the
  feature is playable.

## Gotchas
Traps a cold implementer will hit.

- **★ `TimerWidget` is 3D, not UGUI.** A billboarded quad on `VoidDay/RadialProgress` plus a 3D
  `TextMeshPro`. World-space canvases do not render in this project's URP camera setup — that is *why* the
  in-world rig is meshes. The skip target is a `Collider` routed through `InputRouter`, never a `Button`.
- **★ `InputRouter` must check `TimerWidget` BEFORE `QueueSlot` and `StationView`, in BOTH raycast paths.**
  The widget sits under the station root, so `GetComponentInParent<StationView>` would otherwise claim the
  tap. `TryTapStation` (`InputRouter.cs:88`) is the obvious one; `StationUnder` (`:77`, run on press) feeds
  the long-press pickup and needs the same guard, or a long press on a radial picks the station up for a move.
- **★ `WorldState.Rig.Widget` is a `StationStateWidget`, not a `TimerWidget`.** The timer is private inside
  it (`StationStateWidget.cs:15`), exposed only as `SetTimerVisible` / `SetTimer`. M3 must add pass-through
  members to `StationStateWidget` as well — it is a fourth View file, not just the three obvious ones.
- **★ Adding a `LevelEntryKind` is a five-part walk** (from the game's M8 log): the enum, a `Describe` case +
  serialized copy on `LevelUpPopup`, an `IconFor` case, a branch in `Progression.ApplyLevel` if it is not a
  standing bonus, and a `ValueResolver` read if it is. For `Gems` it is only **four** of those: it is a
  one-shot reward like `Money`, so it takes the `Money` branch and touches neither `ValueResolver` nor
  `LevelGrants` — and `IconFor` feeds the *unlock* rows only (`LevelUpPopup.cs:132`), while the reward block
  hardcodes `rewardIcon.sprite = moneyIcon` at `:125`. The reward icon is what has to become kind-aware.
- **★ `BootValidator` currently enforces "at most one `Money` grant per level"** because
  `LevelUpPopup.BuildReward` renders `rewards[0]`. Widen it to count `Money` + `Gems` together, or a level
  authored with both silently drops one.
- **★ Level 1 must have NO grants** — it is never crossed. `BootValidator` throws on a non-empty
  `levels[0].grants`.
- **★ `Producer.OnDebugReset` must reset the purse** to `startingGems`, next to `_wallet.Reset()` — that
  method owns currency reset. **Do NOT also reset it in `Progression.Reset`**: that method resets level, XP
  and grants and never touches `Wallet`, and it is invoked *from* the same debug reset (`JobSystem.ResetAll`
  → `GameReset` → `ProgressionSystem.OnGameReset`), so resetting in both places double-resets and puts
  currency logic in the progression object. `Producer.Init` will need `startingGems` added to its signature.
- **★ `ConstructionSiteView` disables colliders on the placeholder body BEFORE instantiating the timer**
  (`SpawnPlaceholder` then `Instantiate(timerTemplate, ...)`). The timer's own collider therefore survives
  enabled — this ordering is load-bearing for M3 and must not be flipped.
- **`WorldState` hides a station's job radial while that station's panel is open** (`showTimer = running &&
  !panelOpen`, the BUG-03 design). So a job skip happens from the closed-panel view. This is intended, not a
  gap — do not "fix" it by forcing the timer visible.
- **`BuildSystem.Site.EndTime` is `readonly`** and must stop being so.
- **The Unity editor does not advance frames while unfocused.** `Time.frameCount` sticks and the Game View
  render texture goes stale, so `screenshot-game-view` returns the frame from *entering* play mode. Set
  `Application.runInBackground = true` before verifying anything in playmode.
- **Pointer injection is unavailable.** No milestone can verify a real tap. Drive the chain by publishing the
  intent on the bus directly, and verify the tap *wiring* by inspecting the authored collider + the
  `InputRouter` branch. Say so in the log rather than implying a tap was tested.
- **`GemPurse` must be constructed before `Progression`** in `GameBoot` — `Progression.ApplyLevel` pays gem
  grants.
- **★ `TimeSkip` needs `OrderBoard`, but `constructionSiteView.Init` is called before `OrderBoard` exists.**
  `GameBoot.cs:114` inits the construction view; `orderBoard` is not constructed until `:187`. M3 needs
  `TimeSkip` in that `Init`, so **one of the two has to move** — either hoist the `OrderBoard` construction
  above the `Init` block, or move `constructionSiteView.Init` down with the other `Init` calls. The latter is
  smaller and matches where every other `Init` already lives. Decide it in M3, not by accident.
- **Every `*System` subscribes in `Init` and unsubscribes in `OnDestroy`.** `TimeSkipSystem` follows suit;
  a missing `OnDestroy` leaks across domain reloads.

## Testing
Per CLAUDE.md, the **pure-C# economy core is the one place tests are not suspended**. `TimeSkip`'s pricing
formula and the three owner hooks are exactly that: invisible bugs that make the game subtly wrong. M2 adds
EditMode tests for the cost curve and for each owner's skip. M1 and M3 are currency plumbing and view
wiring — verify by playing, no tests.

The suite is **83 `[Test]` methods** across six files as of 2026-07-22. The game's own LOG still says
"71/71" — that count is stale. **M1 must run the suite first and record the real baseline**; every later
milestone compares against that, not against a number copied out of a doc.
