# Milestone 03 — Progress Pill

**Playable outcome:** Doing an action that advances a quest makes a pill drop from behind
the XP bar showing the quest's description and progress, its bar animates up, and it
retracts a few seconds later. If that tick completes the quest, the pill instead stays at
least 20 seconds flashing green inviting a tap (tap = collect, with particles); if
untapped it retracts and the quest-menu button flashes green until everything's collected.

## Goal
The richest, fiddliest display surface — the live progress feedback loop. It comes last of
the display milestones because it depends on M1's `QuestProgressed`/`QuestCompleted` events
and the M1 collect path, and because its animation is the highest-risk UI work (isolating it
keeps M1's core loop from stalling on tween tuning).

## Build This

**Progress pill (`Assets/View/QuestPill.cs` + `Assets/Prefabs/UI/QuestPill.prefab`)**
- Authored pill prefab parented under `HudCanvas` near `LevelXpPill` (top-center,
  anchored `{0.5,1}`, ~24px down). Draw order: the pill must sit **behind** the XP pill so
  it reads as emerging from behind it — set sibling order accordingly.
- Reuse the **"slide out from behind a pill"** pattern already implemented in
  `Assets/View/ResourcePillRail.cs` (DOTween + a `hidePoint` RectTransform). Study it first;
  mirror its hide/show tween structure rather than inventing one.
- Contents: quest description + a progress bar (the `Image` Type=Filled idiom) that
  **animates up** from the previous value to the new one (tween the `fillAmount`, don't snap).
- Subscribes to `QuestProgressed`: drop down from behind the XP bar, animate the bar to the
  new progress, hold a few seconds, retract back up behind the XP bar and hide.
- Only one pill exists. If multiple quests progress from one action, show the most-recently
  -progressed quest (see Gotchas in summary). A newer progress event interrupts/refreshes
  the current pill rather than queueing.

**Completion behavior**
- Subscribes to `QuestCompleted`: instead of the short hold, the pill **stays ≥20 seconds**
  flashing green, prompting a tap. The 20s timer and flash are presentation and live on the
  View (read live, not cached).
- Tapping the pill while it's in the completion state publishes `CollectQuestRequested`
  (same intent M1's menu uses) → reward + particle burst via the existing `EarnBurstController`
  path. Then the pill retracts.
- If the 20s elapse untapped, the pill retracts **and** the quest-menu button starts
  flashing green.

**Quest-menu button flashing (`Hud.cs` / the M1 quest button)**
- The button flashes green while any quest is completed-but-uncollected. Drive it off quest
  state: start flashing when a completion goes uncollected past the pill window; stop when
  the completed-uncollected count returns to zero (i.e. the player collected everything,
  via the menu or a later pill).
- This is a View concern reading quest queries (or listening to `QuestCompleted` /
  `QuestCollected`); no engine change.

## Do NOT Build This
- **Changes to reward logic or the completed-ids set** → owned by M1's `QuestLog`. The pill
  only *invokes* collect via `CollectQuestRequested` and *reflects* state.
- **The new-quest toast** → M2 (already built). A grant does not spawn a pill; only progress
  and completion do.
- **A queue of multiple simultaneous pills** — one pill, newest-progress-wins. Do not build
  multi-pill stacking.
- **Any `tools/VoidDay.Balance` work** → M4/M5.
- **Persisting the flashing state across reloads** — no save system in the prototype.

## Context
Builds on M1 (events + collect intent) and M2 (toast already handles grants).
- **Events added:** none — consumes M1's `QuestProgressed`, `QuestCompleted`,
  `QuestCollected`, and publishes the existing `CollectQuestRequested` on tap.
- **Data files/fields added:** presentation tunables (flash color, 20s window, tween
  durations, `hidePoint`) as `[SerializeField]` on `QuestPill`/`Hud` — View tunables, not
  SO data.
- **Systems touched:** `Assets/View/QuestPill.cs` (new); `Assets/Prefabs/UI/QuestPill.prefab`
  (new); `Assets/View/Hud.cs` (button flash); `Assets/Scenes/Farm.unity` (pill under
  HudCanvas, wiring); `GameBoot.cs` (`Init` the pill).

## Principles
- **Presentation tunables live on the View, read live each frame** — the 20s window, flash
  color, and tween timings are View fields (per the project's MVC-boundary memory), never in
  a `QuestSO`. The `QuestSO` holds only game data.
- **Event-driven (rule 2):** the pill listens to quest facts and publishes the collect
  intent; it never calls `QuestLog` directly.
- **Unity-native authoring (rule 4):** the pill is an authored prefab with serialized refs;
  no runtime UI construction.
- **Verify DOTween / URP / Input System APIs against installed source before use** — confirm
  the tween API against `ResourcePillRail.cs`'s actual usage rather than from memory.

## Assets Required
- Quest pill chrome + progress bar — `[placeholder OK]`.
- Green flash is a color/material tween — no new asset. Collect FX reuses `EarnParticle.prefab`.

## UI Mockups Required
**Approved (2026-07-23)** — [VoidDay — Quest System UI](https://www.figma.com/design/oNpLZGKUGhyd07cxCAKQB4):
- Progress pill + completion state → frame `04 · Progress Pill`: cream pill (description + bar +
  %) below the XP bar; completion card shows the flashing-green "Quest complete! → TAP TO
  COLLECT" treatment. **Note:** the mockup draws the pill *just below* the XP bar for legibility;
  the "emerges from behind the XP bar" motion is an implementation detail (build it as the spec
  describes — this milestone's Build This — not as the static mockup positions it).
- Quest-menu button flashing-green state → frame `02 · Quest Button` (the glowing green-ringed
  variant).

## Definition of Done
- In Play, an action that advances a quest drops the pill from behind the XP bar, animates
  the bar up, and retracts it after a few seconds.
- An action that completes a quest keeps the pill visible ≥20s, flashing green; tapping it
  collects the reward (particles) and retracts it.
- Letting the 20s lapse retracts the pill and starts the quest-menu button flashing green;
  collecting all completed quests (via menu or pill) stops the flashing.

## How to Test
1. Press Play. Advance a quest partway (one goal action). Confirm the pill drops from behind
   the XP bar, the bar animates up (not a snap), it holds, then retracts behind the XP bar.
2. Advance a *different* quest immediately after; confirm the pill refreshes to show the
   most recent one (no second pill).
3. Drive a quest to completion. Confirm the pill stays flashing green for ≥20s. Tap it —
   confirm reward + particles and retract.
4. Complete another quest and this time **don't** tap within 20s. Confirm the pill retracts
   and the quest-menu button begins flashing green.
5. Open the menu and collect the outstanding quest(s). Confirm the button stops flashing.
