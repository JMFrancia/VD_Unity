# Milestone 02 — New-Quest Toast

**Playable outcome:** When a quest is granted, a toast slides into the corner showing the
quest's description, then auto-dismisses — the same toast styling already used elsewhere.

## Goal
Give the player immediate feedback that a new quest arrived, without opening the menu.
Small, isolated, and pure presentation — it layers on M1's `QuestGranted` event with zero
changes to the engine. It comes before the pill (M3) because it's the cheaper of the two
display surfaces and de-risks nothing else.

## Build This
- Extend `Assets/View/ToastController.cs` to subscribe to `QuestGranted` and call its
  existing `Show(string message, Sprite icon)` with the quest description (carried in the
  `QuestGranted` payload) and a quest icon.
- Add the serialized quest-toast icon field (and any copy prefix, e.g. "New Quest:") to
  `ToastController`, matching how its existing per-reason icon/copy fields are declared.
- No new prefab — reuse `Assets/Prefabs/UI/Toast.prefab` and the existing `ToastStack`.
- Wire the new serialized icon in the scene.

## Do NOT Build This
- **Progress pill and its completion/flash behavior** → M3. A granted quest toasts; it does
  not spawn a pill.
- **A distinct toast per progress or per completion** — only *granted* toasts here. Progress
  and completion feedback are the pill's job (M3).
- **Changes to `QuestLog` or quest data** — this milestone is View-only. If the description
  isn't already on the `QuestGranted` payload, that's an M1 gap to fix in M1, not new logic
  here.
- **Any queueing/dedup logic for simultaneous grants** — `ToastController`/`ToastStack`
  already stacks multiple toasts; rely on that.

## Context
Builds directly on M1.
- **Events added:** none (consumes M1's `QuestGranted`).
- **Data files/fields added:** a `[SerializeField] Sprite questToastIcon` (+ optional copy)
  on `ToastController`.
- **Systems touched:** `Assets/View/ToastController.cs`; `Assets/Scenes/Farm.unity` (wire
  the icon). `GameBoot.cs` already injects `toastController`.

## Principles
- **Event-driven (rule 2):** the toast reacts to a domain fact (`QuestGranted`); it does not
  reach into `QuestLog`.
- **Unity-native authoring (rule 4):** icon assigned as a serialized reference; no runtime
  construction. The toast prefab is already authored.
- **Data-driven (rule 1):** description text comes from the event payload (generated in Core
  from the goal), never rebuilt in the View.

## Assets Required
- Quest toast icon — `[placeholder OK]` (reuse an existing HUD icon if convenient).

## UI Mockups Required
**Approved (2026-07-23)** — [VoidDay — Quest System UI](https://www.figma.com/design/oNpLZGKUGhyd07cxCAKQB4),
frame `03 · New-Quest Toast`: gold quest icon + "NEW QUEST" label + description, in the existing
cream toast frame. Reuses `Toast.prefab`; match the copy/icon shown.

## Definition of Done
- In Play, the moment a quest is granted, a toast appears with that quest's description and
  auto-dismisses after the standard lifetime.
- Two quests granted close together produce two stacked toasts (no crash, no overwrite).

## How to Test
1. Press Play.
2. Trigger a quest grant (reach the gating level, or collect a prerequisite quest so a
   chained one is granted).
3. Confirm a toast slides in with the correct description and fades out on its own.
4. Set up (or trigger) two grants near-simultaneously; confirm both toasts stack and each
   dismisses cleanly.
