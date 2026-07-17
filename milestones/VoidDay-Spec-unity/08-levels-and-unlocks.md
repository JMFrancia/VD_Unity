# Milestone 08 — Levels & Unlocks

**Playable outcome:** Cross an XP threshold and watch a level-up popup fire — then see the build menu unlock new station types, station caps and queue depth rise, order slots increase, and level-gated upgrades become buyable.

## Goal
The payoff milestone. The player-progression *value* has existed since M3 (frozen at level 1); this milestone supplies the **increment** and everything it drives. It sits after Build/Upgrades/Storage on purpose: those milestones already put the unlock states, caps, and gated upgrades on screen, so when leveling finally turns on, it visibly moves things the player can already see — no invisible plumbing. By now a real XP total has banked from many milestones of play, so the first level-up may cross several thresholds.

## Build This
- **Level thresholds + increment** (§9): `LevelSO` set — explicit XP-threshold table, no formula, 20 levels. When `xpTotal` crosses a threshold, increment `playerLevel`, emit `level:up {level, unlocks, rewards}` (and `unlock:granted {kind, id}` per grant). This is the increment M3 deliberately withheld.
- **Generic level-grant applier** (§4.3, §6, §9): on level-up, apply whatever the `LevelSO` grants — **auto-granted**: new station-type unlocks, raised station-type caps, raised queue depth, raised order slots. **Purchasable**: level-gated upgrades become buyable. Build this as one generic applier over the level's grant list, not four hardcoded cases.
- **Activate the dormant seams**: the build menu's `unlock:granted` listener (M4) now fires → locked entries lift; the cap seams (M4 station caps, M3 order slots, M2 queue depth) now receive level contributions and the menus/panels re-read. All of this is *activation of existing read-sites*, not new plumbing in those milestones.
- **`hud.levelXp`** (§12.1) — the **first user-facing level UI**, built here because this is where leveling starts moving. Level badge + XP bar reading `playerLevel`/`xpTotal` against the current `LevelSO` threshold; XP fill animates on `xp:gained`, badge pops on `level:up`. It appears reflecting the XP **already banked since M3**, so on first show it may sit near-full and cross several thresholds at once (a deliberate reveal). The XP value + accrual it displays already exist (M3) — this milestone only surfaces them.
- **`popup.levelUp`** (§12.4): congrats header, new level, unlock list, reward list — all from the `level:up` payload.
- **Debug** (§12.7): add **level up** (grant exactly enough XP to cross the next threshold) to `menu.debug`.

## Do NOT Build This
- **Egg rewards / hatching** → M9. **Constraint: reachable `LevelSO` reward lists must contain no eggs until M9 exists** — otherwise this milestone forward-depends on the pet system. (Level rewards may include eggs conceptually; just don't populate them in reachable levels yet.)
- **World-event unlock at level 5** → M11 (the level gate is read there).
- **New effect scopes** — none; leveling drives auto-grants and gate-flips, not new `EffectType`s.
- **Re-implementing the build menu / caps / upgrade gates** — those exist (M4–M7); this only makes the level *rise* so they react.
- **XP accrual** — already built (M3); do not rebuild it.

## Context
Builds on M3 (`playerLevel`/`xpTotal` value, XP accrual), M4 (build menu + `unlock:granted` listener + cap seam), M6 (order slots), M7 (caps), M5/M6 (upgrades to gate). Adds to the spine:
- **Events added:** `level:up {level, unlocks, rewards}`, `unlock:granted {kind, id}` (now *emitted*, having been listened-for since M4).
- **Data added:** `LevelSO` (per level or one ordered set): XP threshold, per-level unlocks + rewards.
- **Systems touched:** `Systems/Progression` (threshold check, increment, grant application), `View/LevelUpPopup`, `View/LevelXpHud` (new — the badge + bar deferred from M3); existing read-sites in `View/BuildMenu`, `Systems/OrderBoard`, `Systems/Producer` now receive live level changes.

## Principles
- **Generic grant application** (KISS + §9): one applier over the `LevelSO` grant list. Adding a new grantable later means data, not code.
- **Data-driven** (rule 1): thresholds, unlocks, rewards, caps-by-level, slots-by-level — all `LevelSO`. No formula, no literal curve in code (§9).
- **Event-driven** (rule 2): `level:up` announces the fact; the popup, the SFX, and the build menu each *listen* and decide. Progression never calls the popup.
- **Test the core**: threshold-crossing (including crossing multiple levels from one XP grant) and grant application are pure-C# — cover them.
- **Verify Unity APIs**: nothing new beyond popup UGUI.

## Assets Required
- `ui.badge.level`, `ui.bar.xp` [placeholder OK — first used here, deferred from M3], `ui.panel` [placeholder OK]
- **SFX** [placeholder OK]: `sfx.level.up` (void-shimmer fanfare — priority cue), `sfx.xp.gain` (accrual from M3, now audible against the visible bar)
- **VFX** [placeholder OK]: `vfx.levelUp` (rising sparkle)

## UI Mockups Required
- `popup.levelUp` — [mockup needed]; celebratory, void-accent glow on the reward beat.
- `hud.levelXp` (level badge + XP bar) — [mockup needed]; first appearance of the level UI, top-center.

## Definition of Done
- The level badge + XP bar appear for the first time (deferred from M3), already reflecting the XP banked across earlier milestones.
- Accumulating (or debug-granting) enough XP crosses a threshold and fires the level-up popup listing the new level, unlocks, and rewards.
- After the level-up, a previously-locked station type is now buildable in the build menu; a raised station cap lets you build one more; the Order Board shows more slots; queue depth is deeper; any level-gated upgrade is now buyable.
- A single large XP grant that spans multiple thresholds levels up correctly (popup(s) reflect it).
- No reachable level reward grants an egg.

## How to Test
1. Play up XP (fulfill orders / collect) or debug → level up. Confirm the level badge + XP bar now appear (they were hidden through M3–M7), reflecting banked XP.
2. Confirm the level-up popup fires with the correct new level, unlock list, and reward list.
3. Open the build menu → a formerly-locked type (e.g. Henhouse) is now available.
4. Build it (confirm the raised cap allows it); check the Order Board has more slots and the station queue is deeper.
5. Open a panel/Workshop with a level-gated upgrade → it's now buyable.
6. Debug-grant a big XP chunk spanning two thresholds → confirm the level jumps correctly.
7. Confirm no level-up rewarded an egg (that's M9).
