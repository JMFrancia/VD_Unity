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
