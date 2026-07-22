# Gems Currency — Implementation Log

Running record across milestones. **Read this first when picking up cold.**

Milestones: `00-summary.md` · Game spec: `docs/VoidDay-Spec-unity.md` · Game log:
`milestones/VoidDay-Spec-unity/LOG.md`

---

## Before Milestone 01 — context carried in from design (2026-07-22)

No gem code exists yet. What a cold start needs to know, verified by reading the source during design:

**The feature in one line:** a second currency whose only sink is finishing a running timer early.

**The three timers, and why skipping them is small.** All three are already absolute end-timestamps (§13)
driven by their owner's `Tick`, so a skip is the same one-line mutation everywhere — pull the timestamp to
`now` and the existing `Tick` completes the thing on its normal path:

| Timer | Owner | Existing progress read | Skip |
|---|---|---|---|
| Running job head | `JobSystem` | `TryGetHeadProgress` (out `secondsRemaining`) | `head.EndTime = now` |
| Construction site | `BuildSystem` | `TryGetSiteProgress` (out `secondsRemaining`) | `site.EndTime = now` |
| Order slot refill | `OrderBoard` | `RefillRemaining(slot, now)` — already public | `slot.RefillAt = now` |

**The timer "unification" is View-only.** `TimerWidget.prefab` is shared by `StationStateWidget` (jobs) and
`ConstructionSiteView` (builds). Core-side the three owners are entirely separate and stay that way. The
shared thing this feature adds is a *pricing rule* (`TimeSkip`) and an *event*, not a merged owner.

**Verified during design** (don't re-derive):
- `Wallet` is 28 lines — `Money`, `Add(delta)`, `EmitCurrent()`, `Reset()`. Note it has **no `CanAfford` and
  no `Spend`**: money is spent by callers checking `wallet.Money < cost` themselves then calling `Add(-cost)`
  (see `BuildSystem.Place`). `GemPurse` is a sibling in spirit, and deliberately adds `CanAfford`/`Spend`
  because gems have exactly one spender and the check belongs with the purse. Its `Reset(startingGems)` also
  takes a parameter where `Wallet.Reset()` does not — which means whoever calls it must know `startingGems`.
- `Hud` already owns the money pill (`MoneyChanged` → `moneyText`) and builds the debug cheat row from
  `_resources`. The gem pill and `+gems` button belong there; no new component is needed.
- `HudCanvas` reference resolution is **1080×1920**. The top row is three rects, verified by parsing the
  scene's RectTransform documents (a naive line-proximity grep reads the *wrong* document — an early draft of
  this plan mis-transcribed `Subtext` from inside `DebugMenu` as the money pill):

  | Object | Anchor | Pos | Size | Pivot | Spans x |
  |---|---|---|---|---|---|
  | `DebugButton` | `(0,1)` top-left | `(24, -24)` | `104×104` | `(0,1)` | 24–128 |
  | `LevelXpPill` | `(0.5,1)` top-centre | `(0, -24)` | `380×96` | `(0.5,1)` | 350–730 |
  | `MoneyPill` | `(1,1)` **top-right** | `(-24, -24)` | `240×96` | `(1,1)` | 816–1056 |

  The gap between the XP pill and the money pill is only **86px**, so nothing fits beside the money pill on
  that row. The gem pill is therefore a **second row on the right edge, directly under the money pill** —
  same `240×96`, anchor/pivot `(1,1)`, `anchoredPosition (-24, -144)`. This is the only placement that
  reflows no existing rect. (`Hud.cs:10`'s own comment already says "the money pill (top-right)".)
- `LevelUpPopup.BuildReward` renders `rewards[0]` only, and `BootValidator` enforces `moneyGrants <= 1`
  because of it. Adding `Gems` as a second reward kind requires widening that count, not the popup.
- `Progression.ApplyLevel` already has the one-shot-vs-standing-bonus branch `Gems` needs — `Money` pays
  `_wallet.Add(grant.Amount)` then `continue`s past `_grants.Add`.
- `OrderCard` authors both slot states in one prefab (`filledRoot` / `refillingRoot`); the skip button goes
  inside `refillingRoot` beside the "Refilling · 0:47" text.
- `OrderBoardPanel.Update` rebuilds every frame while open, so a falling gem cost needs no extra plumbing.
- `ConstructionSiteView.SpawnPlaceholder` disables the body's colliders **before** the timer is
  instantiated, so the timer's own collider survives enabled. M3 depends on this ordering.
- `WorldState` hides a station's radial while that station's panel is open (`showTimer = running &&
  !panelOpen`) — the BUG-03 design. A job skip therefore happens from the closed-panel view, by design.
- `InputRouter.TryTapStation` checks `QueueSlot` then `StationView`, both via `GetComponentInParent`. The
  `TimerWidget` check must go **first** or the station claims the tap.
- **`InputRouter.StationUnder()` is a SECOND raycast path** (run on press, feeding the long-press pickup at
  `InputRouter.cs:67-75`) and it has no `TimerWidget` guard either. Without one, a long press on a job radial
  picks the station up for a move. Both paths need the guard, not just the tap path.
- `WorldState.Rig.Widget` is a **`StationStateWidget`**, not a `TimerWidget` — the `TimerWidget` is private
  inside it (`StationStateWidget.cs:15`), exposed only via `SetTimerVisible` / `SetTimer`. Anything M3 needs
  from the timer must be added as a pass-through on `StationStateWidget` too.
- `OrderCard` **already has a serialized field named `skipButton`** (`OrderCard.cs:26`) meaning "discard this
  order". The gem control needs a different name — `skipTimerButton` — or the two will be confused.
- SO assets live in **`Assets/Data/SO/`** (`GameConfig.asset`, `Levels.asset`, `OrderConfig.asset`, all the
  recipes), not `Assets/Data/`.
- **There is no stacking-popup precedent in this project.** Every UI surface — `BuildMenu`, `Hud`'s debug and
  totals popups, `LevelUpPopup`, `OrderBoardPanel`, `SiloPanel`, `StationPanel` — publishes
  `ExclusiveUiOpened`. `SkipConfirmPopup` must NOT publish it (it would retract the Order Board it was opened
  from), which makes it the first non-exclusive surface. That is a new pattern, not a copy of an existing one.
- EditMode suite is **83 `[Test]` methods** across six files (BuildTimer 8, EffectSystem 20, Level 16,
  OrderBoard 9, OrderEconomy 11, Storage 19). The game's own LOG says "71/71" — that is the M8-era count and
  is stale; tests were added by later commits. **Re-run the suite to get a live baseline before trusting any
  number**, including this one.

**Open risks:** the tuning numbers (5 starting gems, 30s per gem, floor of 1) are guesses and are the first
thing to retune once M2 is playable.

---

## Before Milestone 01 — UI mockups authored and approved (2026-07-22)

The three milestones introduce UI surfaces that existed in **no** mockup or inventory. Per the standing
"mock up new UI in Figma before Unity scene surgery" rule, they were mocked and **approved by the user
before any implementation**. Do **not** re-open this question, and do **not** hand-author these surfaces
from the milestone docs' prose — read the frames.

**Figma file `X3UE3am9wbX0bKrfFOx8x0`.** Node map and the full rationale live in `docs/UI-Mockups.md`
(§ *Design decision: the gems currency surfaces*). Frames:

| Surface | Milestone | node |
|---|---|---|
| `hud.gems` — gem pill in HUD context | M1 | `69:2` |
| `popup.skipConfirm` — over a live Order Board | M2 | `71:2` |
| `panel.orderBoard` refilling slot + skip button | M2 | `69:73` |
| `world.timerSkip` — radial + gem cost | M3 | `69:126` |
| gem element states (affordable / unaffordable / colourway) | all | `71:88` |

**Approved decisions — inherit these, do not re-decide:**
- **Gems are cyan `#22D3EE`** (facet `#0FA8C4`), *not* the void violet `#8B5CF6`. Violet is reserved for
  eggs/pets/hearts and collides with `hud.eggButton` sitting right beside the gem pill.
- **The gem glyph is a 4-point polygon shape, never a text `◆` character.** Same shape in the pill, the
  cost chip, the skip button and the radial.
- **`popup.skipConfirm`'s primary button is gem-cyan**, not `popup.genericText`'s destructive red.
- **Popup copy is "Skip the wait?" + a cost chip**, not the M2 doc's literal `"Skip for ◆2?"`.
- **Unaffordable state:** Confirm greyed to `#D6CDBB` with `#A89E8C` text, plus a red hint line
  ("You need N more gems."). No toast, no `TimerSkipRefused` event — unchanged from the M2 doc.

**⚠️ APPROVED DEVIATION FROM `01-the-currency.md` — do not halt on this.** M1 says the gem pill goes at
`anchoredPosition (-24, -144)` and to "Reflow nothing else." **That is not achievable.** `hud.eggButton`
already occupies y=176–298 on the right edge — precisely that slot. The approved fix is to **move the egg
button down 116px**, so the right edge stacks **money → gems → egg**. This reflow is authorised; treat the
milestone doc's "reflow nothing else" as superseded on this one point only.

**Also corrected:** `Sheet — In-world UI` (`34:2`) is **stale** — it mocks `world.progressBar` as a *bar*,
but the build uses a radial `TimerWidget`. The new `world.timerSkip` sheet is drawn against the build. Use
`69:126`, not `34:2`, for anything M3 touches.

---

## Milestone 01 — The Currency
**Status:** ✅ Complete · **Date:** 2026-07-22

**Built:** Gems exist, are visible, and can be granted. Nothing spends them yet — that is M2.

- **Core (`Assets/Core/`)** — `Rules/GemPurse.cs` (new): `Gems`, `Add`, `CanAfford`, `Spend` (throws, never
  clamps), `EmitCurrent`, `Reset(startingGems)`. Deliberately a sibling of `Wallet`, not a shared base type.
  Its constructor takes `startingGems` so the purse is *born* correct — no boot-time `Add(5)` that would
  emit a phantom `+5` delta on the first frame. `Events/GameEvents.cs`: `GemsChanged(Delta, Total)` beside
  `MoneyChanged`, and `DebugAddGemsRequested(Amount)` beside `DebugAddMoneyRequested`.
  `Model/LevelModel.cs`: `LevelEntryKind.Gems`. `Rules/Progression.cs`: ctor takes a `GemPurse` (after
  `Wallet`); `ApplyLevel` gets a `Gems` branch mirroring `Money` — pay, add to `rewards`, `continue`.
  `ValueResolver` / `LevelGrants` untouched: gems are a one-shot reward, never a standing bonus.
- **Data** — `GameConfigSO` `[Header("Gems")]`: `startingGems` 5, `secondsPerGem` 30, `minGemCost` 1 (the
  last two are unread this milestone; M2 reads them). `UiThemeSO.gemAccent` = gem-cyan `#22D3EE`.
- **Systems** — `BootValidator`: the three new range rules, plus the per-level "at most one Money grant"
  rule widened to count **Money + Gems together** (`rewardGrants`), since `BuildReward` renders
  `rewards[0]` only. `GameBoot` constructs the purse right after `Wallet` and before `Progression`, injects
  it, and calls `gems.EmitCurrent()` beside `wallet.EmitCurrent()`. `Producer.Init` widened to
  `(bus, jobs, pool, wallet, gems, startingGems, startingCounts)`; `OnDebugReset` resets the purse next to
  `_wallet.Reset()`.
- **View** — `Hud`: `gemText`, `debugAddGemsButton`, `cheatGemAmount` (3), `gemFormat`, a `GemsChanged`
  subscription. No new component — `Hud` already owned the money pill and the cheat row. `LevelUpPopup`:
  `gemIcon` + `gemFormat`; `BuildReward` now picks its icon by `reward.Kind` and delegates its copy to the
  existing `Describe(...)` instead of hardcoding `moneyIcon` / `moneyFormat`.
- **Scene (`Farm.unity`)** — `HudCanvas/GemPill`, duplicated from `MoneyPill` so the chrome matches, with
  its `Button` destroyed (the pill has no action in M1). Anchor/pivot `(1,1)`, `anchoredPosition (-24,-144)`,
  size `240×96`. Child `Glyph` is an `Image` with no sprite (a solid quad) rotated 45° and tinted cyan —
  the 4-point polygon the approved mockup asks for, not a text `◆`. `Amount` is inset to `x: 88..-20` and
  left-aligned to sit beside the glyph. `DebugMenu/.../AddGems` duplicated from `AddMoney`, landing directly
  beneath it in the vertical layout.
- **Art** — `Assets/Art/UI/Icons/gem.png`, a generated 512px cyan diamond with a lighter facet, importer
  settings cloned from `coin.png`. Explicit placeholder (M1 authorises one); it is the level-up reward icon.
- **Tests** — `Assets/Tests/GemPurseTests.cs` (8) + 2 `Gems`-grant tests in `LevelTests`. Suite **83 → 93**,
  all green. Pure-C# economy core, which `CLAUDE.md` says is the one thing still worth testing.

**Deviations from the plan:**
- **The egg-button reflow in the previous LOG entry does not apply.** `hud.eggButton` does not exist in this
  project — there are no eggs in `Farm.unity`. The gem pill went in at `(-24,-144)` exactly as
  `01-the-currency.md` specifies and **nothing was reflowed**. The approved-deviation paragraph above is
  correct about the *mockup* but describes a HUD this build does not have; it is deferred, not applied. If
  an egg button is ever added on the right edge, that reflow becomes live again.
- **A level's Money grant was replaced, not supplemented.** M1's How-to-Test step 3 says to "author a Gems
  grant on an unreached level", but **every** level 2–20 in `Levels.asset` already carried a Money grant,
  and M1's own widened validation rule permits at most one reward (Money **or** Gems) per level. Adding was
  therefore impossible. **Level 3's `$150` Money grant is now a `2 gems` Gems grant** — level 3 being the
  earliest level whose only reward was money. See *Assumptions*.
- **Gem colours** followed the pre-flight resolution rather than the mockup's palette verbatim: one new
  `UiThemeSO.gemAccent`, with the existing `lockedBg` / `lockedText` reserved for M2's unaffordable state
  instead of adding near-duplicate greys.

**Tech debt:**
- **`gem.png` is a generated placeholder**, not commissioned art — a flat cyan diamond. The in-HUD glyph is
  a *different* placeholder (a rotated untextured quad), so the pill and the popup do not currently share
  one asset. When real gem art lands, point both at it and the divergence disappears.
- **`secondsPerGem` and `minGemCost` are authored but unread.** Intentional — splitting one `[Header]` across
  two milestones is worse than one unused field — but they are unvalidated by play until M2 prices a skip.
- **The gem pill is not a button.** The money pill opens `popup.totalResources`; the gem pill does nothing
  when tapped. Nothing in the spec says it should, but it is an asymmetry a player may probe.

**Assumptions:**
- **Level 3 is the right place for the first gem grant, and `2` is the right amount.** Both are guesses, as
  are `startingGems` 5 / `secondsPerGem` 30 / `minGemCost` 1. If wrong, the cost is one number in
  `Levels.asset` — but note the curve now pays **$150 less** in total, which is a real (small) economy nerf
  nobody asked for. Re-examine when M2 makes gems spendable and the whole set gets retuned together.
- **`LevelEntryKind.Gems` was appended after `Money`** (enum value 6) rather than inserted, so every
  existing serialized `kind:` index in `Levels.asset` stays valid. Reordering that enum later would silently
  rewrite every authored grant in the asset — don't.
- **`Producer` owns the gem debug cheat and the gem reset.** `DebugAddMoneyRequested` lives on
  `OrderBoardSystem` (which holds the wallet for order payouts), but gems have no owning system yet and
  `Producer` already needed the purse for `OnDebugReset`. If M2 gives gems a real owning system, the cheat
  should move with it. Cost of changing: one subscription and one `Init` argument.

**Gotchas for later milestones:**
- **`Producer.Init` gained two parameters** (`GemPurse gems, int startingGems`) in the middle of its
  signature. Anything that constructs a `Producer` outside `GameBoot` will not compile until updated.
- **`Progression`'s constructor gained a `GemPurse`** as its 6th argument, before `gated`. This broke both
  test fixtures that build a `Progression` (`LevelTests`, `OrderBoardTests`); both are fixed. Any new
  fixture must pass one — `new GemPurse(bus, 0)` is fine for tests that don't care.
- **A level may now hold at most ONE reward, counting Money and Gems together.** Authoring both on one level
  is a hard boot failure. If a level ever needs to pay both, `LevelUpPopup.BuildReward` must render the
  whole `rewards` list first — the validation rule exists only because it renders `rewards[0]`.
- **The purse is reset in `Producer.OnDebugReset` and NOWHERE else.** Do not add a reset to
  `Progression.Reset` — both run on `DebugResetRequested` and the purse would be reset twice.
- **The player loop is frozen at `frameCount = 1` in this environment.** The editor cannot be brought to the
  foreground, and `Application.runInBackground = true` (verified True inside playmode) does not help.
  `screenshot-game-view` therefore returns **frame 1 forever** — it will happily show you a stale HUD while
  you believe it is live. Always cross-check against component state read via `script-execute`. To get a
  *rendered* shot of anything after frame 1, temporarily flip the overlay canvases to `ScreenSpaceCamera`
  in playmode and use `screenshot-camera`, which renders on demand; the change is playmode-only and is
  discarded on exit.
- **`ProjectSettings.asset` is dirty and not ours.** DOTween (the untracked `Assets/Plugins/Demigiant/`)
  writes a `DOTWEEN` scripting define on every AssetDatabase refresh. It was deliberately left uncommitted.

---

## Milestone 02 — Skip Any Timer
**Status:** ✅ Complete · **Date:** 2026-07-22

**Built:** Gems have a sink. The whole skip mechanism exists — the Core pricing rule, all three owner hooks,
the three events, the shared confirm popup — and the complete tap → confirm → spend chain is proven
end-to-end on the order-refill timer.

- **Core** — `Model/TimerRef.cs` (new): `TimerKind { Job, Construction, OrderRefill }` + a plain readonly
  struct with `Job(id)` / `Construction(id)` / `OrderRefill(slot)` factories. `Rules/TimeSkip.cs` (new): the
  single pricing authority — `CanSkip`, `CostFor` = `max(minGemCost, ceil(remaining / secondsPerGem))`,
  `Skip` (charge, nudge the timestamp, publish). **Two members per owner, a read and a write**, exactly as
  the plan specified: `JobSystem.HeadSecondsRemaining` / `SkipHead`, `BuildSystem.SiteSecondsRemaining` /
  `SkipSite` (`Site.EndTime` stops being `readonly`), `OrderBoard.SkipRefill` (`RefillRemaining` already
  existed). Every skip is a **one-line timestamp nudge** — the owner's next `Tick` completes the thing on
  its normal path, so a skipped job/build/refill fires exactly the events a natural one does. No completion
  logic is duplicated anywhere.
- **The `-1` sentinel.** All three reads return **-1 when there is no live timer of that kind here**, which
  is what lets one pricing rule serve three unrelated owners through one private `SecondsRemaining` switch.
  `TryGetHeadProgress` could not have served: it reports `0` for a *complete* head too.
- **Events** — `TimerSkipTapped(TimerRef)`, `TimerSkipConfirmed(TimerRef)`, `TimerSkipped(TimerRef, Cost)`.
- **Systems** — `TimeSkipSystem.cs` (new): `TimerSkipConfirmed` → `TimeSkip.Skip(..., Time.timeAsDouble)`,
  plus the two gem debug affordances. It is now the gems' owning system (see *Deviations*).
- **View** — `SkipConfirmPopup.cs` (new), `OrderCard.BindRefilling(secondsRemaining, gemCost, onSkipTimer)`
  with a `skipTimerButton` / `skipTimerCostText` pair (deliberately NOT the existing `skipButton`, which
  discards an order and costs nothing), and `OrderBoardPanel` taking a `TimeSkip` and hoisting `slotIndex`
  before capturing it.
- **Scene (`Farm.unity`)** — `HudCanvas/SkipConfirmPopup`. The component sits on an always-active root so
  its `Update` and subscriptions survive; the child `Root` is what toggles. `Root` = a full-screen `Scrim`
  (black 45%, which dims *and* swallows taps meant for the board underneath) + a 720×500 cream `Panel`:
  "Skip the wait?" / "Order slot refills instantly." / a cost chip (rotated-quad gem glyph + amount) /
  a red shortfall line, hidden when affordable / Cancel + Skip. **HudCanvas sorts at 20 and
  OrderBoardCanvas at 15**, which is why the popup can stack over the board at all.
- **Prefab (`OrderCard.prefab`)** — `Refilling/SkipTimerButton`, a cyan `rounded_28` pill (200×96) on the
  right edge with a white rotated-quad glyph and the live cost. `Clock` and `RefillingText` shifted 110px
  left to make room. The cyan is **authored static chrome**: the card's control is always cyan, because
  affordability is the popup's job, not the card's.
- **Tests** — `Assets/Tests/TimeSkipTests.cs` (13): the floor at 1s remaining, ceil-not-round at 31s, no
  off-by-one at an exact multiple, a long timer priced high, a dead/absent timer refused, each of the three
  owners completing down its normal path with its normal events, the charge + `TimerSkipped` payload, and
  an unaffordable `Skip` throwing with the timer untouched. Suite **93 → 106**, all green.

**Deviations from the plan:**
- **Gems moved off `Producer` entirely.** The milestone doc offered "move it here from wherever M1 parked it
  if that is tidier"; it was. `TimeSkipSystem` now owns *both* the `DebugAddGemsRequested` cheat and the
  `DebugResetRequested` purse reset, so `Producer.Init` **loses** the `(GemPurse gems, int startingGems)`
  pair M1 added and is back to `(bus, jobs, pool, wallet, startingCounts)`. M1's LOG warning that "the purse
  is reset in `Producer.OnDebugReset` and NOWHERE else" is superseded: it is now reset in
  `TimeSkipSystem.OnDebugReset` and nowhere else. The one-reset-only rule is unchanged.
- **`SkipConfirmPopup` also *listens* to `ExclusiveUiClosed`.** The plan only said it must not *publish*
  `ExclusiveUiOpened`. Listening was added so a popup can never outlive the surface it stacks on (close the
  board with the popup open and the popup goes too). See *Gotchas*.
- **Nothing else.** The `TimerRef` shape, the two-members-per-owner hooks, the three event names, the cost
  formula, the `skipTimerButton` name and the hoisted `slotIndex` are all exactly as specified.

**Tech debt:**
- **The gem glyph is still three different placeholders** — the HUD pill's rotated quad, `gem.png` in the
  level-up popup, and now two more rotated quads (the card pill, the popup cost chip). All four collapse to
  one reference when real gem art lands. Nothing else changes.
- **`TimeSkip` holds all three owners.** Fine at three; if a fourth timer kind appears, the `switch` in
  `Skip` and the one in `SecondsRemaining` are the two places to touch, and that is the point at which an
  `ITimerOwner` seam would finally earn itself.
- **The popup has no open/close animation** and no sound. Every other surface here is equally plain, so this
  is consistent, not a regression.

**Assumptions:**
- **`secondsPerGem` 30 / `minGemCost` 1 are still the M1 guesses**, now load-bearing: a 60s order refill
  prices at 2 gems against a 5-gem starting purse, so a fresh player can afford exactly two skips. That felt
  reasonable in the numbers but has not been *played*. It is one number in `GameConfig.asset`.
- **Skipping shows no confirmation of what you got.** Confirm closes the popup and the card silently becomes
  a real order. There is no toast, no `TimerSkipped` listener anywhere in the View. If the spend feels
  invisible in play, that is the first thing to add — the event is already published.
- **The scrim swallows taps.** Chosen deliberately (you should not be able to Fill an order mid-confirm),
  but it means there is no tap-outside-to-dismiss: Cancel is the only way out. Unverified as a feel choice.

**Gotchas for later milestones:**
- **`SkipConfirmPopup` is the project's first stacking surface, and M3 reuses it unchanged.** It does not
  publish `ExclusiveUiOpened` (that would retract whatever opened it) but *does* subscribe to
  `ExclusiveUiClosed` → `Close()`. Any future stacking popup should copy that exact pair. Note the asymmetry
  is deliberate: publishing is what makes a surface exclusive; listening is what keeps it honest.
- **M3 needs no Core work at all.** `TimerRef.Job` and `TimerRef.Construction` are built, tested, and
  verified working in playmode; the popup is kind-agnostic. M3 is a collider, a cost label and two
  `InputRouter` guards — publish `TimerSkipTapped(TimerRef.Job(id))` and everything downstream already works.
- **The popup's copy is hardcoded to the order-refill case.** "Order slot refills instantly." is authored
  scene text with no serialized field behind it. M3 skipping a *job* will show that sentence unless the
  subtitle is made per-kind. Decide there, not here.
- **`Producer` no longer knows gems exist.** Anything that constructs a `Producer` outside `GameBoot` and was
  updated for M1's signature must be updated *back*.
- **Playmode is still frozen at `frameCount = 1`** and `Application.runInBackground = true` still does not
  help; `EditorApplication.QueuePlayerLoopUpdate()` does not either. Everything in this milestone was
  verified by driving the real objects directly from `script-execute` (reflect the private service fields off
  the scene components — `OrderBoardPanel._board` / `._bus` / `._skip`, `TimeSkipSystem._gems`,
  `Producer._jobs`, `BuildMenu._build`), which exercises the same code paths minus the `Update` pump. **To
  make a falling timer observable with a frozen clock, rewind the absolute timestamp instead of advancing
  `now`** — `slot.RefillAt = 25` is indistinguishable, to every read, from 35 seconds having passed.
- **The order-board station's type id is `orderBoard`** (capital B) and its instance id is `OrderBoard`.
  Field recipes are `field.cornGrow` / `field.wheatGrow`, not `grow-corn`. `henhouse` is level-3 gated, so
  `field` is the only type a level-1 test can place.

---

## Milestone 03 — In-World Skip
**Status:** ✅ Complete · **Date:** 2026-07-22

**Built:** Both in-world timers are now tap-to-skip surfaces. The job radial and the construction radial each
carry a live gem price beneath the ring and route a tap into M2's popup unchanged. **No Core change** — M2's
prediction that "M3 is a collider, a cost label and two `InputRouter` guards" held exactly.

- **`View/TimerWidget.cs`** — gains `tapCollider`, `costRoot`, `costLabel`, `costFormat`, a
  `public TimerRef Timer { get; set; }` and `SetCost(int gems)`. It stays pure presentation: it never asks
  `TimeSkip` anything, it renders the price its owner hands it.
- **`View/StationStateWidget.cs`** — `SetTimerRef(TimerRef)` / `SetTimerCost(int)` pass-throughs, because
  `WorldState.Rig.Widget` is a `StationStateWidget` and the `TimerWidget` inside it is private.
- **`View/InputRouter.cs`** — the `GetComponentInParent<TimerWidget>()` guard is the **first** branch in
  **both** raycast paths: `TryTapStation` publishes `TimerSkipTapped(widget.Timer)`; `StationUnder` (the
  press-time path feeding long-press pickup) returns `null`, so a long press on a radial can't pick the
  station up for a move.
- **`View/WorldState.cs`** / **`View/ConstructionSiteView.cs`** — both take a `TimeSkip` via `Init`, set the
  widget's `TimerRef` once at spawn (`Job(stationId)` / `Construction(stationId)`), and push the cost inside
  the per-frame poll they already ran.
- **`Systems/Boot/GameBoot.cs`** — `constructionSiteView.Init` moved down into the `Init` block (see
  *Deviations*), both calls now pass `timeSkip`.
- **`View/SkipConfirmPopup.cs`** — the subtitle is now per-`TimerKind` (see *Deviations*).
- **Prefab (`TimerWidget.prefab`)** — a `BoxCollider` on the root (`center (0,-0.09,0)`, `size
  0.7×0.78×0.06`) sized to cover the ring **and** the cost row: the whole radial is the tap target, as the
  mockup specifies, with no separate skip pill. A `Cost` child at `y = -0.4` holds `Glyph` (a Quad rotated
  45° on `GemGlyph.mat`) and `Amount` (3D TMP, left-aligned, fontSize 2.4). The collider is on the **root**,
  so `Billboard` keeps it facing the camera for free.
- **Art** — `Assets/Art/Materials/GemGlyph.mat`, URP/Unlit, `_BaseColor` = gem-cyan `#22D3EE`, double-sided.
  The in-world counterpart to the HUD's rotated-quad glyph.
- **`StationStateWidget.prefab` needed no edit.** Its nested `Timer` is a real **prefab instance** of
  `TimerWidget.prefab`, so the collider and the cost row propagated automatically. The milestone doc listed
  it as a file to touch; it isn't one.
- **Tests** — none added. This milestone is entirely View-layer; `CLAUDE.md` scopes tests to the Core
  economy, and M2 already covers the pricing rule and all three owner hooks. Suite stays at **106**, green.

**Deviations from the plan:**
- **`GameBoot`: `constructionSiteView.Init` was moved down, not `OrderBoard` hoisted.** The doc offered both
  and said to pick deliberately. Moving the one `Init` is the smaller diff, it lands where every other `Init`
  already lives, and it is safe because nothing publishes `StationConstructionStarted` / `StationBuilt`
  between the old call site and the new one — the subscription is no later in practice than it was.
- **The popup subtitle is now per-kind**, which M2 explicitly deferred to "decide there, not here."
  `SkipConfirmPopup` gained a `subtitleText` field plus three strings — `jobSubtitle` ("The job finishes
  instantly."), `constructionSubtitle` ("The building finishes instantly."), `orderRefillSubtitle` ("Order
  slot refills instantly.", byte-identical to the copy M2 authored into the scene). Bound once on open, not
  in `Refresh`: the kind can't change while a popup is open. **A fourth timer kind must add a string here or
  `SubtitleFor` throws** — deliberately loud.
- **`SetCost(0)` disables the collider, not just the cost row.** The doc says the widget being
  `SetActive(false)` makes the collider inert "for free" and no enable/disable logic is needed. That is true
  for *no timer*, but not for *a timer with nothing left to buy* — the widget is still visible then, and a
  live collider would swallow a tap into a popup that instantly closes itself. Tying the collider to the
  price keeps `tapCollider` from being a dead serialized field and makes "priced" and "tappable" the same
  state by construction.
- **Nothing else.** The `TimerRef` plumbing, the two pass-throughs, both `InputRouter` guards and the
  one-prefab-two-callers shape are exactly as specified.

**Tech debt:**
- **The in-world gem glyph is placeholder #5.** A rotated quad on a flat cyan unlit material — a sibling of
  the HUD pill's quad, the card pill's quad and the popup chip's quad, plus `gem.png` in the level-up popup.
  All five collapse to one reference when real gem art lands.
- **The cost row's layout is hand-tuned world units** (`glyph x -0.13 @ scale 0.13`, `amount x -0.03 @
  fontSize 2.4`). It reads well for 1–2 digit costs; a 3-digit price will drift right. Nothing in the game
  currently prices that high.
- **`TimeSkip` is now injected into two more View components.** Four View types hold it (`OrderBoardPanel`,
  `SkipConfirmPopup`, `WorldState`, `ConstructionSiteView`). That is fine — they only ever *read* the price —
  but it is the widest a Core rule has spread into the View layer so far.

**Assumptions:**
- **A radial always wins the raycast over the station under it.** `Physics.Raycast` returns the nearest hit
  and the radial floats above the body, so this holds at the current fixed camera angle. It was not proven
  with a real pointer (see below). If the camera ever tilts far enough that a station body occludes its own
  radial, the guard silently stops firing — the symptom would be "tapping the timer opens the station panel".
- **"Skip never steals a tap" is verified by inspection only.** Pointer injection is unavailable in this
  environment, and publishing `StationTapped` directly would bypass the very branch under test (the milestone
  doc's How-to-Test step 5 says so explicitly). What *was* verified at runtime: the collider is authored,
  enabled, and correctly bounded; the site body's own colliders are disabled while the timer's is not; and
  both guards are the first branch in their path. What was **not** verified: an actual finger on glass.
- **`secondsPerGem` 30 / `minGemCost` 1 are still the M1 guesses.** They now price three surfaces. A 5s corn
  job costs 1 gem — i.e. the floor makes short jobs a *bad* deal, which is probably correct but unplayed.

**Gotchas for later milestones:**
- **`TimerWidget.prefab` is the single authoring point for both radials.** `StationStateWidget.prefab`
  contains a prefab *instance* of it, not a copy, so anything added to `TimerWidget.prefab` appears on the
  job radial and the construction radial at once. Do not "fix" a divergence by editing the nested instance —
  that creates an override and breaks the one-visual guarantee.
- **`WorldState.Init` and `ConstructionSiteView.Init` both gained a `TimeSkip`** — `WorldState`'s is the 3rd
  argument (after `jobs`), `ConstructionSiteView`'s is the 3rd (after `build`). Anything constructing either
  outside `GameBoot` won't compile until updated.
- **`constructionSiteView.Init` no longer sits beside `stationRegistry.Init`.** If a future milestone needs
  it to subscribe earlier, the fix is to hoist the `OrderBoard`/`TimeSkip` construction, not to move the
  `Init` back — it depends on `TimeSkip` now.
- **`InputRouter`'s branch order is load-bearing and unenforced.** `TimerWidget` → `QueueSlot` →
  `StationView`, in `TryTapStation`; `TimerWidget` → `StationView` in `StationUnder`. All three sit under the
  same station root, so reordering them silently changes which one claims a tap. There is no test for this.
- **Playmode is still frozen at `frameCount = 1`.** Everything here was verified by pumping the private
  `Update()` methods through reflection (`WorldState`, `ConstructionSiteView`) and ticking `JobSystem` /
  `BuildSystem` by hand, then rendering `Camera.main` to a `RenderTexture`. For a shot that includes the
  overlay UI, flip every root `ScreenSpaceOverlay` canvas to `ScreenSpaceCamera` first and flip it back —
  the change is playmode-only, and the scene was confirmed `IsDirty=False` after exiting.
- **`BuildSystem.Demolish` throws on a station still under construction** (`JobSystem.Unregister` has no
  entry for it yet). Not a bug to fix here — just don't reach for it when setting up a test.
