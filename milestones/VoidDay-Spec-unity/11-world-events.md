# Milestone 11 — World Events

**Playable outcome:** At level 5+, a world event fires on its own — Dopamine Rain speeds every job by 25% for two minutes with a first-time explanatory popup — and firing it again shows a toast instead; two flavor-only events show toasts too.

## Goal
The final layer, and the first **time-limited** effect (every prior effect was permanent). It reuses the effect vocabulary wholesale — "the same Effect vocabulary as everything else. No new mechanics" (§11) — so it's mostly an event scheduler + notification policy over `global.speed`, which already exists from M6. Comes last because it gates on level 5 (M8) and leans on the widest effect scope.

## Build This
- **Event scheduler** (§11): world events unlock at **level 5** (read `playerLevel`); fire on a random interval from the events data (`System.Random`, roughly every few minutes); most events carry a per-event minimum level. Emit `worldEvent:started {eventId, effects, duration}` and `worldEvent:ended {eventId}`.
- **Time-limited effects** (§11): a started event's `Effect[]` applies for `duration`, then ends and recalculates. Dopamine Rain = `global.speed +25%` for 2 minutes. This is the one new bit of machinery — a timed effect that adds to and later removes from the active set (via `effects:recalculated`). Reuses M6's `global.speed` scope.
- **Launch set** (§11): Dopamine Rain + **2 flavor-only events** (no real effect).
- **Notification policy** (§11), by event type:
  - Real-effect events → `popup.event` the **first time** this session (to explain), `toast.generic` **every time after**. "First time" resets per session (no save, §13) — acceptable.
  - Flavor-only events → `toast.generic` only.
  - Per CLAUDE.md, the popup/toast **listen** to `worldEvent:started` and decide for themselves — no `ui:*` event tells them to show.
- **`popup.event`** (§12.4): event name, description, effect via the §3.6 generator (data-driven from `WorldEventSO`). **`toast.generic`**: icon + short line, auto-timeout.
- **Debug** (§12.7): add **force-fire world event** to `menu.debug` (so it's demonstrable without waiting for the interval or grinding to level 5).

## Do NOT Build This
- **New effect scopes / types** → none; Dopamine Rain reuses `global.speed` (M6). If a flavor event needs an effect it's not flavor-only.
- **`vfx.dopamineRain` as a blocker** → placeholder tint/pulse is fine; real VFX is additive polish.
- **Save-persisted "first time seen"** → session-only (§13); resets each session by design.
- **Events before level 5** → gated; don't fire below the threshold (except debug force-fire for testing).
- **A generic toast system beyond what's needed** — `toast.generic` is data-driven but keep it minimal (corner, timeout).

## Context
Builds on M8 (level 5 gate), M6 (`global.speed`), M5 (resolver, description generator, `effects:recalculated`). Adds to the spine:
- **Events added:** `worldEvent:started {eventId, effects, duration}`, `worldEvent:ended {eventId}`.
- **Data added:** `WorldEventSO` → model (interval, min level, `Effect[]`, notification type).
- **Systems touched:** new `Systems/WorldEvents` (scheduler, timed-effect lifecycle), `Core/Rules/EffectResolver` (timed add/remove — or a timed-effect wrapper feeding the existing resolver); `View/EventPopup`, `View/Toast`.

## Principles
- **Same vocabulary, no new mechanics** (§11): a world event is just another Effect emitter with a duration. If you're adding effect *types* for events, you've misread the spec.
- **Emitters describe what happened** (rule 2): `worldEvent:started` announces the fact + its effects; the popup, toast, VFX, and SFX each listen and decide. No `showPopup` emitter.
- **Data-driven** (rule 1): interval, min level, effect magnitude, duration, notification type — all `WorldEventSO`. Dopamine Rain's +25%/2min are SO values.
- **Core randomness is `System.Random`** (§11 scheduler in Core rules).
- **Test the core**: the timed-effect lifecycle (applies for duration, then removed and recalculated) and the level-5 gate are pure-C# — cover the apply/expire path.

## Assets Required
- **VFX** [placeholder OK — tint/pulse]: `vfx.dopamineRain`, plus the global `vfx.post.bloom` volume if not already present
- `ui.toast` [placeholder OK — rounded rect], `ui.panel` [placeholder OK]
- **SFX** [placeholder OK]: `sfx.worldEvent.start` (void whoosh)

## UI Mockups Required
- `popup.event`, `toast.generic` — [mockups needed]; void-accent for void events.

## Definition of Done
- Below level 5, no world events fire on their own.
- At level 5+, events fire on the configured interval; Dopamine Rain applies `global.speed +25%` for 2 minutes, then expires and speeds return to normal.
- Dopamine Rain's **first** occurrence this session shows the explanatory popup; subsequent occurrences show a toast.
- The two flavor events show toasts only (no popup, no effect).
- Debug → force-fire lets you trigger any event on demand.

## How to Test
1. Debug → level up to 5+ (or debug-grant XP).
2. Debug → force-fire Dopamine Rain → the first time, the explanatory popup appears; confirm every job speeds up (time one) for ~2 minutes, then returns to normal.
3. Force-fire Dopamine Rain again → this time a toast appears (not the popup).
4. Force-fire each flavor event → toast only, no effect.
5. Confirm that below level 5 (debug → reset, don't level) no events auto-fire.
6. Let the game run at level 5+ and confirm events fire on the interval without debug.
