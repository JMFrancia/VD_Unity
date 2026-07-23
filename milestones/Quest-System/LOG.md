# Quest System — Build Log

Context is carried forward in this file. `implement_milestone` starts bare, reads the
milestone docs + this log, builds one milestone, and appends a section here. No copy-paste
kickoff prompt.

---

## Before Milestone 01 — context carried in from design

Everything below was verified by reading the code during the design pass (2026-07-23). It's
here so a cold start doesn't re-derive it. **Verify file:line references still hold before
relying on them** — the repo moves.

### The one decision that shapes everything
Quest **rules** go in `Assets/Core` as engine-free pure C#. Reason: (1) the Core boundary
rule (CLAUDE.md rule 3) requires it, and (2) `tools/VoidDay.Balance/VoidDay.Balance.csproj`
globs `..\..\Assets\Core\**\*.cs` into the tool's own compile, so pure-Core quest logic runs
in the offline sim **for free** (M4). Putting quest logic in `Assets/Systems` would break both.
If M4 ever can't compile the quest logic, the bug is a stray `using UnityEngine` in Core — fix
Core, don't mirror the logic in the tool.

### Event bus (M1)
- `Assets/Core/Events/EventBus.cs` — `Subscribe<T>(Action<T>)`, `Unsubscribe<T>(Action<T>)`,
  `Publish<T>(T)`. Synchronous dispatch; reentrant publishes are fine. Convention: subscribe
  in `Init(...)`, mirror every subscribe with an unsubscribe in `OnDestroy`.
- Events catalog: `Assets/Core/Events/GameEvents.cs` (all immutable `readonly struct`).

### Goal-relevant events already emitted (M1 subscribes to these)
- `OrderFulfilled{ string OrderId, int Payout, int Xp }` — `OrderBoard.cs:114`.
- `JobCollected{ string StationId, IReadOnlyList<ResourceAmount> Outputs, bool ByPet }` —
  `JobSystem.cs:225`. **This is "crop harvested."**
- `MoneyChanged{ int Delta, int Total }` — `Wallet.cs:18`. **No dedicated "earned" event;
  filter `Delta>0`.** `GemsChanged` same shape (`GemPurse.cs`).
- `ResourceChanged{ string ResourceId, int Delta, int Total }` — `ResourcePool.cs:83`.
- `UpgradePurchased{ string StationId, string UpgradeId, int Tier, int Cost }` —
  `UpgradeSystem.cs:128`.
- `StationBuilt{ string StationId, string StationType, GridCoord Cell }` — `BuildSystem.cs:190`.
- `XpGained{ int Amount, string Source }` — `Progression.cs:62`;
  `LevelUp{ int Level, IReadOnlyList<LevelEntry> Unlocks, IReadOnlyList<LevelEntry> Rewards }`
  — `Progression.cs:121` (fires once per level crossed).

### Reward sinks + condition reads (M1)
- Grant: `Wallet.Add(int)` (`Wallet.cs:15`), `GemPurse.Add(int)` (`GemPurse.cs:22`),
  `ResourcePool.Add(string,int)` (`ResourcePool.cs:77`, throws if total would go negative),
  `Progression.AwardXp(int, string)` (`Progression.cs:57`, routed through `ValueResolver`,
  publishes `XpGained`).
- Condition reads: `Progression.PlayerLevel` (`Progression.cs:26`), `ResourcePool.Get(id)`,
  `UpgradeSystem.TierOf(stationId, trackId)` (`UpgradeSystem.cs:67`), quest-completed set
  (internal to `QuestLog`).
- There is **no unified "grant reward" facade** — `Progression.ApplyLevel` (`:98-116`) shows
  the loop-and-dispatch-per-kind pattern a `QuestReward` applier should mirror.

### Data pattern to copy (M1)
- `Assets/Core/Model/Effect.cs` — flat `[Serializable]` struct + `EffectType` enum
  discriminator + generic args + nested `Condition{ ConditionType type; string arg; int amount }`
  (`:100-105`). Rationale stated in-file (`:97-98`): flat + enum, inspector-friendly, KISS.
  Scope derived by exhaustive `switch` that **throws on unknown** (`EffectScopes.Of`, `:36-72`).
- `Assets/Data/LevelSO.cs` — `LevelDef.grants : List<LevelGrant>`, each flat with a
  `LevelEntryKind` enum + `StationSO` ref + `int amount`. Closest analog to quest reward/goal.
- `[SerializeReference]` is used **nowhere** — do not introduce it.

### SO + boot conventions (M1)
- SO definitions: `Assets/Data/*.cs`, `namespace VoidDay.Data`, `sealed`,
  `[CreateAssetMenu(menuName="VoidDay/…")]`. Instances: `Assets/Data/SO/*.asset` (flat).
- **No registry SO, no `Resources.Load`.** Root is `GameConfigSO`
  (`Assets/Data/SO/GameConfig.asset`), assigned on `GameBoot` (`GameBoot.cs:20`). "All of type
  X" lists hang off it (`stationRoster`, `levels`, …). Add `List<QuestSO> quests` the same way.
- `GameBoot.Start()` (`Assets/Systems/Boot/GameBoot.cs:54`): `BootValidator.Validate` →
  `RequireWired()` null-checks → `ModelProjector` SO→Core → construct Core graph (`:60-200`,
  `OrderBoard`/`Progression`/`JobSystem` around `:189-200`) → `Init(bus, …)` inject into scene
  components (`:203-230`) → publish `DataLoaded`/`GameStarted` (`:237-238`). Construct
  `QuestLog` near the other Core systems; add `ValidateQuest` + `ProjectQuest` +
  `RequireWired` entry for the menu view.

### UI reuse (M1–M3)
- **Panel contract:** `Assets/View/SiloPanel.cs` / `OrderBoardPanel.cs` — `[SerializeField]
  GameObject panelRoot` (starts inactive), `Init(bus,…)`, opens on `*Requested`, publishes
  `ExclusiveUiOpened("<source>")`/`ExclusiveUiClosed`, closes when another exclusive UI opens.
- **Row template:** `OrderBoardPanel.SyncCardCount` (`:120`) pools rows;
  `SiloPanel.BuildStored`/`StationPanel.BuildTiles` clear-and-rebuild. Model `QuestRow` on
  `ResourceRow.cs`/`UpgradeRow.cs` (`MonoBehaviour` + `Bind(...)`).
- **ScrollRect:** the ONLY ScrollRect in the project is the horizontal BuildTray. A vertical
  scrolling quest menu (Viewport + Content + `VerticalLayoutGroup` + `ContentSizeFitter`) must
  be **authored fresh** — no vertical-scroll template to clone.
- **Fill bar:** no shared component — it's an `Image` (Type=Filled, Horizontal) driven by
  `fillAmount`. Cleanest precedent: `SiloPanel.capacityBarFill` (`:25,136`).
- **Toast (M2):** `Assets/View/ToastController.cs` (`Show(string, Sprite)`, `:46`) +
  `Assets/Prefabs/UI/Toast.prefab`. It self-subscribes to events and decides when to toast;
  add a `QuestGranted` subscription. Injected in `GameBoot.cs:47`.
- **Collect particles (M1/M3):** `Assets/View/EarnBurstController.cs` +
  `Assets/Prefabs/UI/EarnParticle.prefab` — flies icons to HUD targets on
  `OrderFulfilled`/`JobCollected`/`XpGained`. World confetti alt: `ConfettiBurst.prefab` via
  `ConstructionSiteView.cs:134`.
- **Pill-from-behind (M3):** `Assets/View/ResourcePillRail.cs` already implements
  "slide out from behind a pill" with DOTween + a `hidePoint` RectTransform — mirror it.
- **HUD host + button placement:** `Assets/View/Hud.cs` wires `debugToggleButton` in code
  (`:79`, click added in code — the scene `m_OnClick` is empty). `HudCanvas/DebugButton` is
  anchored top-left `{0,1}` pivot `{0,1}` at `{24,-24}`, size 104×104. Put the quest button at
  ~`{24,-152}`. XP bar: `LevelXpPill` under `HudCanvas`, top-center `{0.5,1}` at `{0,-24}`,
  380×96; `barFill` is a green Filled `Image` chased smoothly in `LevelXpHud.cs:119-122`.

### Balance tool map (M4–M5)
- Run: `dotnet run --project tools/VoidDay.Balance -- <verb>` (`.NET 9`). Verbs: `read, write,
  sim, eval, patch, suggest, sweep, report, serve, session …` (`Cli/Program.cs`). Bare = web
  workbench.
- Schema: `Schema/BalanceConfig.cs` (add `List<QuestConfig> Quests`); result rollup
  `Schema/SimResult.cs` (`LevelReport`). Reader: `Unity/EconomyReader.cs` +
  `Unity/RawAssets.cs` (add `QuestRaw` + `_questGuidById` back-map, enums by name). Harness:
  `Sim/CoreHarness.cs` (mirrors `GameBoot`; `GameBootParityTests` canary; hand-mirrors
  `ProgressionSystem`/`UpgradesSystem` at `:130-133`). Metrics: `Sim/MetricsCollector.cs`
  (subscribes real bus; add `QuestCompleted`/`QuestCollected`). Knobs: `Agent/Patch.cs`
  (`op:"set"` only today; positional lists, no move/insert) + `bounds.json` allowlist.
  Write-back: `Unity/AssetWriter.cs` (never reserialize; `InsertRecipe` `:384` and grant-block
  regen are the templates for create/reorder; refuses list add/remove elsewhere). Contract:
  `AGENTS.md`. Skill: `.claude/skills/balance_game/SKILL.md` (defers to `AGENTS.md`; forbidden
  from inventing verbs/metrics).

### UI mockups — DONE & APPROVED (2026-07-23)
Figma file: https://www.figma.com/design/oNpLZGKUGhyd07cxCAKQB4 — frames `01 · Quest Menu`,
`02 · Quest Button` (normal + flashing-green), `03 · New-Quest Toast`, `04 · Progress Pill`
(progress + completion). Styled from a live Game-View screenshot of the HUD: cream panels,
**Baloo 2** font, green progress bars, purple debug button. Quest button chosen as a **gold**
rounded-square with a white checklist icon (distinct from the purple debug button). Row layout:
description on top, then `[progress bar | %]`. Build M1–M3 to match these. (This is a standalone
mockup file, not the project's main Figma — the user was fine with that for this feature.)

### "Reorder" — RESOLVED (2026-07-23, user-confirmed)
- **"Reorder" = reordering the `GameConfigSO.quests` list position.** Not steps within a quest,
  not a first-class chain. M5 builds against that.

---
<!-- implement_milestone appends one section per completed milestone below -->

## Milestone 01 — Quest Engine + Menu
**Status:** ✅ Complete · **Date:** 2026-07-23

**Built:** The whole quest spine — pure-C# engine + the first display surface.
- **Core (engine-free, so M4's balance sim compiles it in for free):**
  `Core/Model/QuestKinds.cs` (`ConditionKind`, `GoalKind` — flat append-only enums),
  `Core/Model/QuestModel.cs` (`QuestModel`/`QuestConditionModel`/`QuestGoalModel`/`QuestRewardModel`
  + `QuestStatus` read struct), `Core/Rules/QuestDescription.cs` (goal → player string),
  `Core/Rules/QuestLog.cs` (grant / track / collect). Events in `Core/Events/GameEvents.cs`:
  `QuestGranted{QuestId,Description}`, `QuestProgressed{QuestId,Progress}`, `QuestCompleted{QuestId}`,
  `QuestCollected{QuestId}`, intent `CollectQuestRequested{QuestId}`.
- **Data:** `Data/QuestSO.cs` (`QuestSO` + `[Serializable]` `QuestCondition`/`QuestGoal`/`QuestReward`
  /`QuestResourceGrant` structs); `GameConfigSO.quests` (`List<QuestSO>`); three authored assets
  `Data/SO/Quest_{Starter,Harvest,Chain}.asset`, registered on `GameConfig.asset`.
- **Boot:** `ModelProjector.ProjectQuest`, `BootValidator.ValidateQuest` (throws on bad field;
  a `QuestCompleted` condition must name a real quest id), `GameBoot` constructs `QuestLog`
  (after `orderBoard`/`timeSkip`), `Init`s the menu, `RequireWired`.
- **View:** `View/QuestMenuPanel.cs` (SiloPanel/OrderBoardPanel contract; opens on its own gold HUD
  button; `ExclusiveUiOpened("quests")`), `View/QuestRow.cs` + `Prefabs/UI/QuestRow.prefab`.
  Scene `Farm.unity`: `HudCanvas/QuestButton` (gold, top-left `{24,-152}` under the debug button)
  + `HudCanvas/QuestMenu` (card + vertical `ScrollRect`/Viewport/Content authored fresh — no vertical
  scroll template existed).
- **Tests:** `Assets/Tests/QuestTests.cs` — 12 EditMode tests, all green.

**Implemented subset (YAGNI, per the doc):** conditions `MinLevel`/`ResourceAtLeast`/`QuestCompleted`;
goals `EarnMoney`/`FulfillOrders`/`HarvestCrops`/`ReachLevel`. `UpgradePurchased` condition and
`PurchaseUpgrades`/`BuildStations` goals are declared enum values (append-only) but their switch case
throws if ever evaluated — no example quest exercises them. Add the case + wiring when a quest needs it.

**Deviations from the plan:**
- The quest OPEN button is wired **inside `QuestMenuPanel`** (a `[SerializeField] Button openButton`,
  like SiloPanel's `closeButton`), NOT in `Hud.cs` as the doc suggested. Wiring it in `Hud.cs` would have
  required inventing a `QuestMenuRequested` routing event to cross HUD→panel — a change to the event
  contract the doc's Context section fixes. Self-contained panel keeps the contract exactly as specified.
  Reversible; costs nothing to move later.
- `QuestReward` reward resources use a dedicated `[Serializable] QuestResourceGrant` (ResourceSO + amount),
  not `List<ResourceAmount>` verbatim (that Core struct is `readonly`, not inspector-serializable). Same
  authoring shape as `Ingredient`/`StartingResource`; projected to `ResourceAmount` at boot.

**Tech debt:**
- `QuestLog` does **not** handle `GameReset` (the §13 debug reset). Quests persist across a debug reset
  instead of re-granting. Out of DoD/scope; cheap to add (clear active/completed, restore candidates,
  re-`EvaluateGrants` on `GameReset`) when M2/M3 or playtest wants it.

**Assumptions:**
- Collect's particle burst rides the **XP reward** via the existing `XpGained → EarnBurstController`
  pointer-fallback path (`AwardXp` source tag `"quest"`). Resource rewards land on the HUD counters via
  `ResourceChanged` but throw **no** flying particle — the resource-burst path needs a station-world origin
  a UI collect has none of. Keep `reward.xp > 0` on every quest or a collect throws nothing. If M3 wants
  resource icons to fly from a collect, `EarnBurstController` needs a pointer-origin resource path.
- `QuestCompleted` **condition** is satisfied by **collection** (`QuestCollected`), not by hitting 100%
  (`QuestCompleted` event). This is what DoD #7 requires; chains + the M4 sim inherit this semantics.

**Verified (automated, no user):** boot has no exceptions; `Quest_Starter`+`Quest_Harvest` grant on
`GameStarted`; menu opens from the button and renders rows; `EarnMoney` hits 100% on +$100 and does **not**
drop on a −$50 spend; collecting applies XP (+40, level→3) and +2 wheat through the real sinks and retires
the quest; the `Quest_Chain` (`QuestCompleted quest.starter`) grants **only after** the starter is
collected, then its `ReachLevel 4` completes on level-up; collecting the chain pays its XP and retires it;
`HarvestCrops` advances on `JobCollected` (unit + live). Full EditMode suite 128/128. Screenshot confirms
the gold quest button placed under the purple debug button.

**Gotchas for later milestones:**
- A pre-existing `SFXManager` (`Assets/Utilities/SFXManager.cs:141`) `KeyNotFoundException` fires during a
  level-up SFX fade — it is the in-flight **audio** work (dirty `SfxController`/`SfxLibrary`/new
  `SfxLibrarySOEditor`), NOT quest code. Any level-up path triggers it; ignore for quests.
- `QuestMenuPanel` holds the live `QuestLog` and `EventBus` in private `_log`/`_bus`; `Hud` holds
  `_progression`/`_pool`. Reflecting those is the cleanest way to drive/read quests headlessly in playmode.
- Menu `Rebuild` is clear-and-`Destroy()`; `Destroy` is deferred to end-of-frame, so multiple rebuilds in
  one synchronous `script-execute` frame show inflated row counts — read row counts in a **later** call.
- Enums `ConditionKind`/`GoalKind` and `LevelEntryKind` are serialized by integer index — **append only**.
