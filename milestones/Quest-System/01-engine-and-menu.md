# Milestone 01 — Quest Engine + Quest Menu

**Playable outcome:** Meet a quest's conditions and it's granted; perform the goal action
and its progress bar fills (never backward); open the quest menu from a button under the
debug button, tap a ready-to-collect quest, and receive XP + resources with a particle
burst. The full receive → track → collect loop, playable.

## Goal
This is the whole feature's spine. It delivers the pure-C# quest engine (data model +
rules) and the first display surface (the menu), so every later milestone is pure
presentation (M2 toast, M3 pill) or tool integration (M4/M5) layered on top with no
rework. It comes first because nothing else can be built or tested until a quest can be
granted, tracked, and collected.

## Build This

**Data (`Assets/Data/` + `Assets/Core/Model/`)**
- `QuestSO` (`Assets/Data/QuestSO.cs`, `sealed`, `namespace VoidDay.Data`,
  `[CreateAssetMenu(menuName = "VoidDay/Quest", fileName = "Quest")]`). Fields:
  `string id`, `List<QuestCondition> conditions`, `QuestGoal goal`, `QuestReward reward`.
  Match the exact conventions of `RecipeSO.cs` / `UpgradeSO.cs`.
- `QuestCondition` — flat `[Serializable]` struct: `ConditionKind kind` (enum) +
  generic payload fields (`int amount`, `string arg`, and an SO ref where needed, e.g.
  `ResourceSO resource`). Kinds: `MinLevel`, `UpgradePurchased`, `ResourceAtLeast`,
  `QuestCompleted`. Copy the shape of `Effect.Condition` (`Assets/Core/Model/Effect.cs`).
- `QuestGoal` — flat `[Serializable]` struct: `GoalKind kind` (enum) + `int amount` +
  `string targetId` (crop/recipe/upgrade/station id) + SO ref where the inspector needs
  one. Kinds (data-driven, keyed to existing events): `EarnMoney`, `FulfillOrders`,
  `HarvestCrops` (via `JobCollected`), `PurchaseUpgrades`, `BuildStations`, `ReachLevel`.
  Start with the subset the example quests need; the `switch` throws on any unhandled kind.
- `QuestReward` — flat `[Serializable]` struct: `int xp` (always) + `List<ResourceAmount>`
  optional resource grants (+ optional `int money`, `int gems` if an example needs them).
- Enums (`ConditionKind`, `GoalKind`) live in `Assets/Core/Model/` alongside `EffectType`
  etc. **Append-only** — several are serialized by integer index elsewhere in this project.
- Add `List<QuestSO> quests` to `GameConfigSO` (mirrors `stationRoster`); author it on
  `Assets/Data/SO/GameConfig.asset`.
- Author 2–3 example quest assets `Assets/Data/SO/Quest_<Name>.asset` spanning conditions
  and goal kinds (e.g. one gated on `MinLevel`, one gated on a `QuestCompleted` chain).

**Description generation (§ spec "Description")**
- Generate the human string from the goal ("Harvest 10 wheat", "Fulfill 1 order") in Core,
  from `GoalKind` + `amount` + `targetId`. Resource/station display names come from the
  projected models, not hardcoded.

**Rules (`Assets/Core/Rules/QuestLog.cs`, pure C# — no `UnityEngine`)**
- Constructed in `GameBoot` alongside `OrderBoard`/`Progression`, given the `EventBus`
  and read-only handles for condition evaluation (`Func<int> playerLevel`, `ResourcePool`,
  `UpgradeSystem`), plus the reward sinks (`Wallet`, `GemPurse`, `ResourcePool`,
  `Progression`).
- Subscribes to the goal-relevant events and advances progress. Progress is a monotonic
  `float` in `[0,1]`, computed from a **baseline snapshot taken at grant time** and the
  running **max** so it can never decrease (see Gotchas in summary).
- Grant evaluation: on state-change events (`LevelUp`, `ResourceChanged`,
  `UpgradePurchased`, `QuestCompleted`), re-check every ungranted, not-yet-completed
  quest's conditions; grant when all are true; remove from the candidate pool.
- Collect: on a `CollectQuestRequested` input intent, if that quest is ready, apply the
  reward through the existing sinks and move it to the completed set.
- Publishes: `QuestGranted`, `QuestProgressed`, `QuestCompleted` (reached 100%, awaiting
  collect), `QuestCollected` (reward applied). Payloads in `GameEvents.cs`.
- Exposes read queries the menu renders from (`IReadOnlyList` of active + ready quests
  with description + progress).

**Boot wiring**
- `BootValidator`: add `ValidateQuest` — every referenced SO assigned, amounts in range,
  `QuestCompleted` conditions reference a real quest id. Throw loudly (asset + field).
- `GameBoot`: project `config.quests` → Core models, construct `QuestLog`, `Init` the
  quest menu view.

**UI — Quest Menu (authored prefab + scene button)**
- `QuestMenuPanel` (`Assets/View/QuestMenuPanel.cs`) following the `SiloPanel`/
  `OrderBoardPanel` contract: `[SerializeField] GameObject panelRoot` (starts inactive),
  injected `EventBus`, opens on its button, publishes `ExclusiveUiOpened("quests")` /
  `ExclusiveUiClosed`, closes when another exclusive UI opens.
- A **vertical `ScrollRect`** (Viewport + Content with `VerticalLayoutGroup` +
  `ContentSizeFitter`) — none exists in the project yet, author it fresh. Ready-to-collect
  quests pinned to top and highlighted; active quests below; scrolls on overflow.
- `QuestRow` prefab (`Assets/Prefabs/UI/QuestRow.prefab`) + `QuestRow.cs` with a
  `Bind(...)`, modeled on `ResourceRow`/`UpgradeRow`. Shows description (below) and a
  progress bar with **% on the right** using the `Image` Type=Filled fill-bar idiom
  (`SiloPanel.capacityBarFill`). Tapping a ready row publishes `CollectQuestRequested`.
- Collect particles: reuse `EarnBurstController` (the `OrderFulfilled`/`JobCollected` FX
  path that flies icons to the HUD) so a collect throws XP/resource icons to their pills.
- Quest-menu button on `HudCanvas`, anchored top-left `{0,1}` pivot `{0,1}` at roughly
  `x:24, y:-152` (directly under `DebugButton` which is `{24,-24}`, 104 tall). Wire its
  click in `Hud.cs` the way `debugToggleButton` is wired.

## Do NOT Build This
- **New-quest toast** → M2. Do not toast on `QuestGranted` yet.
- **Progress pill / XP-bar animation / flashing button** → M3. On progress, only the menu
  updates this milestone; no pill, no button flashing.
- **Any `tools/VoidDay.Balance` change** → M4/M5. Keep quest code confined to `Assets/`.
  (But quest *rules* MUST be engine-free Core — see Principles — so M4 gets them for free.)
- **Cross-session persistence / save system** — out of scope for the prototype; quests
  reset on reload.
- **New goal/condition kinds beyond what the example quests exercise** — YAGNI; add kinds
  when a quest needs them. The `switch` throwing on unknown is the guard.
- **A quest debug menu** — deferred (prototype mode adds no dev tooling).

## Context
First milestone — nothing quest-related exists yet.

- **Events added** (in `GameEvents.cs`): `QuestGranted{ string QuestId, string Description }`,
  `QuestProgressed{ string QuestId, float Progress }`,
  `QuestCompleted{ string QuestId }` (100%, awaiting collect),
  `QuestCollected{ string QuestId }` (reward applied), and input intent
  `CollectQuestRequested{ string QuestId }`.
- **Data files/fields added:** `Assets/Data/QuestSO.cs`; `Assets/Data/SO/Quest_*.asset`
  (2–3); `List<QuestSO> quests` on `GameConfigSO` + `GameConfig.asset`; enums
  `ConditionKind`, `GoalKind` in `Assets/Core/Model/`.
- **Systems touched:** `Assets/Core/Rules/QuestLog.cs` (new); `Assets/Core/Events/GameEvents.cs`;
  `Assets/Systems/Boot/GameBoot.cs`, `BootValidator.cs`, `ModelProjector.cs`;
  `Assets/View/QuestMenuPanel.cs` + `QuestRow.cs` (new); `Assets/View/Hud.cs`;
  `Assets/Scenes/Farm.unity` (`HudCanvas` button + quest menu canvas);
  `Assets/Prefabs/UI/QuestRow.prefab` (new).

## Principles
- **Core boundary (CLAUDE.md rule 3) is load-bearing here:** `QuestLog` and the quest
  enums/models must never `using UnityEngine`. This is not just hygiene — the balance tool
  compiles `Assets/Core` in, so quest rules being pure Core is what lets M4 run them.
- **Data-driven (rule 1):** every quest number/threshold/reward lives in the `QuestSO`.
  No goal amounts, no XP values, no thresholds in code. Descriptions read display names
  from projected models.
- **Event-driven (rule 2):** `QuestLog` only listens and announces; it never calls another
  system. Collect is an input intent, not a method call.
- **Unity-native authoring (rule 4):** the menu, its ScrollRect, and the row template are
  authored prefabs/scene objects wired via `[SerializeField]` — no runtime UI construction.
- **Fail loud at the data boundary:** `BootValidator.ValidateQuest` throws on the first bad
  field; downstream assumes well-formed. The goal/condition `switch` throws on unknown enum.
- **Verify Unity 6.3 / URP / Input System APIs against installed source before use.**

## Assets Required
- Quest-menu button icon — `[placeholder OK]` (a primitive/solid button is fine).
- Quest row progress-bar + panel chrome — `[placeholder OK]`.
- Reuses existing `EarnParticle.prefab` for collect FX — no new asset.

## UI Mockups Required
**Approved (2026-07-23)** — [VoidDay — Quest System UI](https://www.figma.com/design/oNpLZGKUGhyd07cxCAKQB4):
- Quest menu panel → frame `01 · Quest Menu` (ready-at-top highlighted vs active rows, row =
  description + progress bar + % on the right, scrollbar hint). Build to match.
- Quest-menu button (normal state) → frame `02 · Quest Button`. Gold rounded-square, checklist
  icon, placed directly under the purple debug button. (Its flashing-green state is M3.)

## Definition of Done
- With the editor in Play, a quest whose conditions you can satisfy (e.g. reach a level, or
  complete a prerequisite quest) becomes granted.
- Performing its goal action fills the row's progress bar and % monotonically; spending
  money (a negative `MoneyChanged`) does **not** drop an "earn $X" quest's progress.
- The quest menu opens from the button under the debug button, scrolls on overflow, shows
  ready quests highlighted at the top and active quests below.
- Tapping a ready quest applies XP + any resources (visible on the HUD counters) with a
  particle burst, and that quest never reappears/re-grants.

## How to Test
1. Press Play. Confirm no boot exceptions (validation passes).
2. Open the quest menu via the new button (under the debug button, top-left). Confirm an
   active quest shows with a description and a 0%/low progress bar.
3. Perform the goal action (e.g. fulfill an order / harvest a crop / earn money). Watch the
   bar and % rise in the menu. Do the action again — it keeps rising, never resets.
4. For an "earn $X" quest: spend money (buy something) and confirm progress does **not**
   drop.
5. Drive the quest to 100%. Confirm it moves to the top and is highlighted.
6. Tap it. Confirm XP + resources land on the HUD with a particle burst, and the quest
   disappears from the active list and does not come back.
7. If you authored a chained quest (condition `QuestCompleted`), confirm it gets granted
   only after you collect its prerequisite.
