# Milestone 01 — The Currency

**Demonstrable outcome:** press Play and a gem pill reads `◆ 5` on the right edge, directly under the money
pill. Hit `+gems` in the debug menu and it counts up. Level up into a level authored with a gem grant and the
level-up popup shows a gem reward line while the pill rises. Hit Reset and it drops back to 5.

## Goal
Gems exist, are visible, and can be granted. Nothing spends them yet — that is M2. This milestone is the
currency plumbing and the HUD, done once so the skip work has something to charge against.

## Build This

- **`Core/Rules/GemPurse.cs`** — a sibling of `Wallet`, deliberately a copy rather than a shared base class
  (two currencies is the second occurrence, not the third):
  `Gems`, `Add(delta)`, `CanAfford(cost)`, `Spend(cost)`, `EmitCurrent()`, `Reset(startingGems)`.
  `Spend` **throws** if the purse can't cover it — like `OrderBoard.Fulfill` throwing on missing goods.
  Note it is a sibling in *spirit*, not in exact shape: `Wallet` has no `CanAfford`/`Spend` (callers check
  `wallet.Money < cost` then `Add(-cost)`), and `Wallet.Reset()` is parameterless. Gems get the tighter API
  because they have exactly one spender, so the check belongs with the purse.
- **`GemsChanged(int Delta, int Total)`** in `Core/Events/GameEvents.cs`, alongside `MoneyChanged`.
- **`DebugAddGemsRequested(int Amount)`**, routed like the other cheats (add → `gems:changed`).
- **`GameConfigSO`** — a new `[Header("Gems")]` block with `startingGems` (5), `secondsPerGem` (30),
  `minGemCost` (1). All three land now even though only the first is read this milestone; the other two are
  config M2 reads, and splitting the header across two milestones is worse than one unused field.
- **`BootValidator`** — `startingGems >= 0`, `secondsPerGem > 0`, `minGemCost >= 1`.
- **`LevelEntryKind.Gems`** — the full five-part walk, as a **one-shot reward like `Money`**:
  - the enum value
  - `Progression.ApplyLevel` — takes the `Money` branch (pay it, add to `rewards`, `continue`). It is **not**
    a standing bonus: do **not** touch `ValueResolver` or `LevelGrants`.
  - `LevelUpPopup` — a `gemFormat` serialized copy string and a `gemIcon` sprite; `BuildReward` picks its
    icon by `reward.Kind` instead of hardcoding `moneyIcon`; a `Describe` case.
  - `BootValidator` — widen the existing "at most one `Money` grant per level" rule to count `Money` + `Gems`
    together, since `BuildReward` renders `rewards[0]` only.
- **`GameBoot`** — construct `GemPurse` right after `Wallet` and **before `Progression`** (which pays gem
  grants), inject it where needed, and `EmitCurrent()` alongside `wallet.EmitCurrent()`.
- **`Hud`** — a gem pill beside the money pill and a `+gems` debug button. **No new component:** `Hud`
  already owns the money pill and the cheat row. Add `gemText`, a `cheatGemAmount`, and a `GemsChanged`
  subscription.
- **Scene authoring** — the gem pill in `HudCanvas`, styled as the money pill is, as a **second row on the
  right edge directly under the money pill**: anchor/pivot `(1,1)`, `anchoredPosition (-24, -144)`, size
  `240×96`. Reflow nothing else — the money pill is top-right, the XP pill is top-centre, and there is only
  86px between them. A placeholder gem icon is fine.
- **Reset** — `Producer.OnDebugReset` resets the purse to `startingGems`, next to `_wallet.Reset()`; this
  needs `startingGems` added to the `Producer.Init` signature. Do **not** also reset in `Progression.Reset`
  (see the summary's gotchas — it would double-reset).

## Do NOT Build This
- **Any skipping.** No `TimerRef`, no `TimeSkip`, no skip buttons, no confirm popup → M2.
- **A `TimerWidget` collider or cost label** → M3.
- **A gem *sink* of any kind.** Gems accumulate and do nothing this milestone. That is correct.
- **Commissioned gem art.** A placeholder icon is the rule until proven otherwise.
- **Generalising `Wallet` and `GemPurse` into a shared currency type.** Explicitly rejected — see the
  summary's Decisions.

## Context
- **New:** `Core/Rules/GemPurse.cs`.
- **Touched:** `Core/Events/GameEvents.cs`, `Core/Model/LevelModel.cs` (the enum),
  `Core/Rules/Progression.cs`, `Data/GameConfigSO.cs`, `Systems/Boot/BootValidator.cs`,
  `Systems/Boot/GameBoot.cs`, `Systems/Producer.cs` (incl. its `Init` signature), `View/Hud.cs`,
  `View/LevelUpPopup.cs`, `Assets/Scenes/Farm.unity` (HudCanvas), `Assets/Data/SO/GameConfig.asset`.

## Principles
- **Data-driven** (rule 1): the starting amount and the cheat amount are inspector fields, never literals.
- **Event-driven** (rule 2): the HUD learns the balance from `GemsChanged`, never by holding the purse.
- **Core boundary** (rule 3): `GemPurse` is pure C#, no `UnityEngine`.
- **Fail loud:** `Spend` on an empty purse throws; it is never clamped to zero.

## Definition of Done
- Boot is clean; `BootValidator` passes and rejects a negative `startingGems` with a named message.
- The gem pill renders at `◆ 5` on the first frame with no special-case first-frame path (`EmitCurrent`
  does it, exactly as the money pill works).
- `+gems` moves the pill; Reset returns it to `startingGems`.
- A level authored with a `Gems` grant pays out, and the level-up popup shows a gem line with the gem icon.
- A level authored with **both** a `Money` and a `Gems` grant is rejected at boot with a named message.
- The EditMode suite is green at its **recorded live baseline** (run it first; the docs' counts are stale).

## How to Test
0. **Run the EditMode suite before touching anything** and record the pass count. That is the baseline every
   later milestone compares against; the number written in these docs may already be stale.
1. Enter playmode (set `Application.runInBackground = true` first — the editor freezes when unfocused).
   Screenshot the HUD: the pill reads `◆ 5` on the right edge, directly under the money pill, with nothing
   else moved.
2. Publish `DebugAddGemsRequested(3)` on the bus → the pill reads `◆ 8`.
3. Author a `Gems` grant on an unreached level in `Assets/Data/SO/Levels.asset`, publish `DebugLevelUpRequested`, and
   confirm the popup shows the gem reward line and the pill rises. Screenshot the popup.
4. Publish `DebugResetRequested` → the pill returns to `◆ 5`.
5. Temporarily set `startingGems = -1` and confirm boot throws naming the asset and field. Revert.
6. Temporarily author `Money` + `Gems` on one level and confirm boot throws. Revert.
7. Re-run the EditMode suite and confirm it matches the step-0 baseline.
