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

## Milestone 02 — New-Quest Toast
**Status:** ✅ Complete · **Date:** 2026-07-23

**Built:** A granted quest now toasts. View-only, zero engine change.
- `View/ToastController.cs`: added `[SerializeField] string questGrantedPrefix = "New Quest: "` +
  `[SerializeField] Sprite questGrantedIcon`; subscribes/unsubscribes `QuestGranted` alongside the
  existing `CollectRefused`; `OnQuestGranted(e) => Show(questGrantedPrefix + e.Description, questGrantedIcon)`.
  Reuses the existing `Show(string,Sprite)` + `Toast.prefab` + `ToastStack` — no new toast system.
- Scene `Farm.unity`: `HudCanvas/ToastStack` (ToastController host) `questGrantedIcon` wired to the placeholder
  sprite `Assets/Art/UI/Icons/ready.png`.

**Deviations from the plan:** none. (Description comes straight off the `QuestGranted` payload as the doc's
Context specifies; no engine/data touched.)

**Tech debt:**
- Toast copy is a single-line `prefix + description` string because `Toast.Show` takes one text field. The
  mockup (`03 · New-Quest Toast`) shows a separate bold "NEW QUEST" label above the description; folded into
  the "New Quest: " prefix instead. If a later polish pass wants the two-line label look, `Toast.prefab` +
  `Toast.Show` need a second text slot — out of this milestone's View-only, no-new-prefab cut.
- Icon is a **placeholder** (`ready.png`, the checkmark HUD icon) — the doc marked the quest toast icon
  `[placeholder OK]` and no dedicated quest sprite exists (M1's quest button uses a Text glyph, not a sprite).
  Swap for a real gold quest icon when one is produced.

**Assumptions:**
- `ToastStack`/`ToastController` already stacks multiple toasts (relied on per the doc's "no dedup logic"
  instruction). Verified live: two near-simultaneous grants produced two independent stacked toasts, each
  auto-dismissing on its own timer.

**Gotchas for later milestones:**
- Boot grants (`Quest_Starter`+`Quest_Harvest` on `GameStarted`) fire their toasts at t≈0 and self-destroy
  after `lifetime` (3s) + `fadeSeconds` (0.35s). A state-read verification must run **within** that window or
  it sees an empty stack — publish a fresh `QuestGranted` on the live bus and read synchronously instead
  (Show is synchronous: Instantiate + SetActive same frame). The live bus is reflectable off
  `ToastController._bus` (private).
- `M3`'s progress pill must NOT also toast on grant — the toast is the *granted* announcement, the pill owns
  progress/completion. Kept strictly to `QuestGranted` here.

## Milestone 03 — Progress Pill
**Status:** ✅ Complete · **Date:** 2026-07-23

**Built:** The live progress-feedback surface. View-only, zero engine/data change.
- **View:** `View/QuestPill.cs` (new) — a single newest-wins pill. Subscribes `QuestGranted` (caches id→desc, the
  only quest event carrying the text — a grant does NOT drop a pill), `QuestProgressed` (drop from behind the XP
  bar, chase bar to new fraction, hold `progressHoldSeconds`, retract), `QuestCompleted` (hold ≥20s flashing green,
  tappable), `QuestCollected` (retract if it's the shown quest). Tapping publishes `CollectQuestRequested` (M1's
  intent) → reward + XP-burst particles via the existing collect path. Slide mirrors `ResourcePillRail` (DOTween
  DOAnchorPos/DOFade/DOScale + `hidePoint`); bar chase mirrors `LevelXpHud` (`MoveTowards`, so it creeps not snaps).
- **View:** `View/Hud.cs` — quest-button green-glow flash. `HashSet<string> _uncollectedCompleted` (add on
  `QuestCompleted`, remove on `QuestCollected`); `Update()` pulses `questButtonGlow` alpha (sin) while the set is
  non-empty, restores 0 when empty. New fields `questButtonGlow` (Graphic) + `questFlashPeriod`.
- **Boot:** `GameBoot` — `questPill` `[SerializeField]` + `questPill.Init(bus)` + `RequireWired` entry.
- **Prefab/Scene:** `Prefabs/UI/QuestPill.prefab` (new) — cream `rounded_30` pill 380×88 (Description Text +
  BarTrack/BarFill `rounded_8` Filled-Horizontal green + Percent Text + CanvasGroup + Button). Instance under
  `HudCanvas` top-center @ `{0,-128}`, sibling **behind** `LevelXpPill` so it emerges from behind the XP bar;
  `hidePoint`→`LevelXpPill` (instance override). `HudCanvas/QuestButtonGlow` — green `rounded_30` halo behind the
  gold Q button, alpha driven by `Hud`.

**Deviations from the plan:** none material. Pill/glow chrome is placeholder (doc-sanctioned `[placeholder OK]`),
built from the same cream/green/`rounded_*` vocabulary as the XP pill and quest rows to match Figma frame 04.

**Tech debt:**
- The completion flash tints the pill **background** cream→green (clear, readable). The Figma completion card has a
  richer "Quest complete! → TAP TO COLLECT" two-line treatment; folded into a single `completionPrompt` string +
  bg-tint flash. A polish pass wanting the exact card look needs more chrome on the prefab.
- Button flash is a green halo Image behind the gold button (placeholder for the mockup's glowing green ring).
- No `GameReset` handling on pill or the Hud flash-set — matches M1's `QuestLog`, which persists quests across the
  debug reset (its own noted debt). If M-later adds reset-requgrant, clear `_uncollectedCompleted` + retract the pill.

**Assumptions:**
- **Newest-progress-wins on ONE pill** (doc's explicit cut). A `QuestProgressed`/`QuestCompleted` for a different
  quest refreshes the same pill; a different quest's progress will even interrupt a completion flash. Safe because
  the button-glow safety net keeps flashing for any still-uncollected completion, so a bumped completion is never
  lost — just relocated from pill to button.
- **Button flashes while ANY quest is completed-but-uncollected** (Build This's first sentence), which *includes*
  the pill's own 20s completion window — chosen over the bullet's "past the pill window" phrasing because the
  Context forbids adding events, and a pill→button handoff with no coupling would need one. Simplest event-only
  reading. If a designer wants the button to stay dark until the pill lapses, that needs pill→Hud coupling or a new
  event (contract change → not this milestone).
- Bar snaps to 0 when the pill switches to a *different* quest (so the new quest fills up rather than sliding down
  from the old value); a same-quest refresh animates from its current fill.

**Verified (automated, no user):** boot clean + `RequireWired` passed. Progress cycle: drop → bar chases 0→0.40
(MoveTowards) → hold → retract, all via real-bus events. Completion: `Completion` state, tappable, `Complete! Tap
to collect`, 20s hold, bg flashes green; 20s-lapse-untapped retracts AND leaves the button glow pulsing
(glowAlpha≈0.22 while set non-empty); collecting clears the set → glow off. **Real-quest tap:** +$100 completed
`quest.starter` → pill Completion → real `button.onClick` collected it (removed from active, XP reward applied,
level 1→3), pill retracted, glow cleared. Live screenshot shows the green completion pill directly below the level
bar (matches Figma 04). Sole exception in the run = the known pre-existing `SFXManager.cs:141` level-up-SFX
`KeyNotFoundException` (dirty audio work), not quest code.

**Gotchas for later milestones:**
- The pill listens to quest **facts** only and never references `QuestLog` (rule 2). Descriptions come from
  `QuestGranted`; if a future path ever emits `QuestProgressed`/`QuestCompleted` for a quest **without** a prior
  `QuestGranted`, the pill shows a blank description. Every quest is granted before it can progress, so this holds.
- `QuestPill` private state (`_state`, `_currentId`, `_targetFill`, `_holdRemaining`) and `Hud._uncollectedCompleted`
  are the cleanest headless read/drive points; `_bus` is reflectable off `QuestPill`/`Hud`/`ToastController`.
- Tool round-trip latency means many real seconds pass between `script-execute` calls — a 2.2s progress hold or a
  20s completion hold will have **already elapsed** by the next read. Read timers/alpha immediately, or drive +
  read in the *same* `script-execute`, not across calls.

## Milestone 04 — Quests in the Headless Sim
**Status:** ✅ Complete · **Date:** 2026-07-23

**Built:** The offline balance tool (`tools/VoidDay.Balance`, .NET 9) now runs the **real** M1 quest rules in
its sim. Because the tool globs `Assets/Core/**/*.cs` into its own compile, `QuestLog` ran headless with zero
mirroring — the predicted "stray `using UnityEngine` in Core" stop point did **not** occur (Core quest files
are clean).
- **Schema:** `QuestConfig`/`QuestConditionConfig`/`QuestGoalConfig`/`QuestRewardConfig` + `BalanceConfig.Quests`.
- **Reader:** `QuestRaw` DTOs + `GameConfigRaw.quests`; `EconomyReader.ReadQuests` (enums carried **by name** via
  `EnumName<ConditionKind>`/`<GoalKind>`, reward `ResourceSO` refs resolved to ids) + `_questGuidById` back-map
  (`QuestGuidById` accessor, for M5's writer).
- **Harness:** `CoreHarness` constructs `QuestLog` right after `TimeSkip`, mirroring `GameBoot` 204-211 (same
  bus + read handles + reward sinks + resourceName closure). `ConfigProjector.Quest` mirrors
  `ModelProjector.ProjectQuest`.
- **Collection:** new `Sim/QuestCollector.cs` — the sim's stand-in for the player's collect tap. It **enqueues**
  completions; `SimRunner` drains them **top-level** at the loop head (`DrainCollections`). (Collecting
  reentrantly inside the `QuestCompleted` dispatch stack-overflowed — see Gotchas.)
- **Metrics:** `MetricsCollector` counts `QuestsCompleted` (on `QuestCompleted`) and attributes quest reward
  income (`QuestReward{Xp,Money,Gems,Resources}`, on `QuestCollected`, from the config reward table);
  `SimResult.LevelReport` surfaces all five; the text `Render` adds a `[quests N +Xxp +$Y +Zres]` note.
- **Knobs + metrics + contract:** `ConfigPath` gained the `quests` collection (`quests/<id>/reward.xp`,
  `.../goal.amount`, `.../conditions[0].amount`); `bounds.json` allowlists them; `quest.completions` +
  `quest.rewardShare` goal metrics added to `GoalEvaluator`/`Goal`; `AGENTS.md` documents the metrics, the path
  grammar and the bounds in lockstep (the skill forbids inventing metrics).
- **Parity:** adding `QuestLog` to the harness reconciles the Core graph; `GameBootParityTests` hash re-stamped
  `bde1702`→`5043016` (`33a3c2f2…`). Full tool suite **47/47**.

**Deviations from the plan:**
- **Quest scalar knob paths use the actual slash grammar `quests/<id>/reward.xp`** (like `recipes`/`upgrades`),
  not the doc's shorthand `quests/<id>.reward.xp` — the `/` delimits id from member, consistent with every
  other collection. Same knobs, correct spelling.
- **The sim auto-collects a completed quest** (the doc assumed rewards "flow automatically", but collection is a
  manual `CollectQuestRequested` intent the `PlayerAgent` never issues — so without this, reward income is
  always zero and DoD#2 fails). Uses M1's **existing** intent — no new event. Immediate + untimed (models the
  economic fact only; the sim ignores UI, per project memory). See the stack-overflow gotcha below.
- **Spliced the 3 quests into the committed `versions/baseline.json`** (tuning untouched, +70 lines only) so the
  **default** `sim`/`eval`/session path exercises quests. `read --out` was **not** used to regenerate baseline —
  that would silently re-tune it to the (different) live Unity economy.

**Tech debt / FLAGS (M5 inherits):**
- **`QuestConfig` shape, `QuestRaw` mapping, `quest.completions`/`quest.rewardShare` metrics, and the
  `quests/*/…` bounds are new surfaces M5's write-back + skill build on.** `quest.rewardShare` is **money-only**
  (quest reward money ÷ level money earned).
- **`OptimalityMonotonicity` was scoped to the quest-free economy** (`config.Quests.Clear()`). Splicing quests
  into baseline broke it: at optimality 1.0 only **2** quests complete (the perfect player grows corn for
  orders and **skips** the wheat-harvest quest, banking less XP and finishing **slower**, 3189s, than the 0.65
  player at 2663s). That is a **real economy interaction** between an action-specific quest reward and the
  optimality dial, not a dial regression — and the guard's documented intent is the *dial*, orthogonal to
  quests (with `Quests` empty the sim path is byte-identical to pre-quest). **M5 should weigh** whether
  action-rewarding quests + the monotonicity guard need reconciling, and whether `progression-v1.json` (the
  other tracked config, still quest-free) should also carry quests.
- **`versions/live.json` is a transient `read` dump** (uncommitted). The default read `--out` is
  `versions/live.json`; `--config live` sims it.

**Assumptions:**
- **Auto-collect is immediate and untimed** — an always-present optimal player collects a ready quest at once,
  and the sim charges no action-time for it. If M5 wants collection to cost sim time or be optimality-gated,
  it becomes a `PlayerAgent` decision (new `AgentDecision.T`, runner case) rather than a reflex drain.
- **Completions are counted on `QuestCompleted`, reward income on `QuestCollected`** (one loop-iteration later,
  top-level). If a level boundary falls between the two, a completion and its reward can attribute to adjacent
  levels — acceptable for a sim, and rare.

**Gotchas for later milestones:**
- **Auto-collect MUST stay top-level, never reentrant.** Collecting straight inside the `QuestCompleted`
  dispatch **stack-overflows**: the completion fires inside a `MoneyChanged`→level-up→`AwardXp` cascade, and the
  reward's own XP levels up → grants money → completes another quest → collects again, without bound. The game
  never hits this because the player collects at a discrete tap; `QuestCollector` enqueues and `SimRunner`
  drains at the loop head to reproduce that altitude.
- **`sim`/`eval`/`session` read a *saved* config (default `versions/baseline.json`), not live Unity.** After any
  quest asset change, `read` to refresh a config before its quests show up in a sim. `baseline.json` is a
  **pre-quest tuned snapshot** that differs materially from live Unity (storage cap, XP curve, unlock levels) —
  do **not** `read --out versions/baseline.json` to "fix" it; that re-tunes the canonical config.
- **Enums are carried by name at the reader seam** (`EnumName<>`), parsed back by name in `ConfigProjector`
  (`Enum.Parse`). A reordered Core enum can't silently reassign a quest kind — but the quest `.asset` files
  still serialise the enum **by integer index** (append-only, same as M1's warning).

## Milestone 05 — Quest Authoring in the Tool + `balance_game` Skill
**Status:** ✅ Complete · **Date:** 2026-07-23

**Built:** The balance tool can now **create / reorder / delete** quests, `write --apply` pushes them into
Unity as real `QuestSO` assets, and the `balance_game` skill knows how to drive quest balance. Tools-only
milestone (no `Assets/` game code changed; `Assets/` is touched only as the *target* of `write --apply`).
- **Structural quest ops — a scoped `quest` sub-verb** (`Cli/Program.cs`), not new `patch` ops: `quest
  create|delete|move|list`, config→config (`--config`/`--out`, never Unity), mirroring `patch`'s discipline and
  `session`'s two-word-verb pattern. A quest is a whole object, not one numeric scalar — a non-scalar patch op
  would fight AGENTS' "one path = one numeric scalar" grammar. `create` takes goal/reward/condition flags (one
  `--condition Kind:amount[:arg]`, one `--reward-resource id:amount`; multi-condition quests are authored by
  editing the config JSON). `move --to <i>` = reorder the `GameConfigSO.quests` list position (the resolved
  meaning of "reorder").
- **Write-back — `Unity/AssetWriter.cs` `DiffQuests`** + Apply path. Create = new `Quest_<id>.asset` (+ `.meta`,
  guid allocated at plan time) mirroring `InsertRecipe`/`BuildRecipeAsset`. Reorder/create/delete all regenerate
  the `GameConfig.quests` reference block **byte-for-byte** (grant-block-rewrite template) — one path for all
  three; a pure scalar edit leaves the block identical so a round-trip plans nothing. Delete removes the
  `.asset`+`.meta`. Scalar quest edits (`reward.{xp,money,gems}`, `goal.amount`, `conditions[n].amount`) use
  positional nested finders (`FindNestedScalar`/`FindConditionAmountLine`) since they're 4-space-nested — the
  flat `SetScalar` can't reach them. Editing an existing quest's goal kind/target or condition/reward-resource
  *structure* is refused loud (only the scalars move). Never reserializes; a change is a minimal diff.
- **Boot-rule mirror — `Unity/BootRules.cs` `ValidateQuests`** now mirrors `BootValidator.ValidateQuest`
  (condition amount ranges, a `QuestCompleted` names a real non-self quest, goal amount > 0, HarvestCrops needs a
  targetId, rewards ≥ 0, reward-resource amounts > 0). Runs over the whole incoming config in `Plan()`, so
  deleting a quest a surviving chain still references is refused **for free** before any byte is written.
- **Contract + skill in lockstep.** `AGENTS.md`: new `quest` verb in the table, a *Quest authoring* section, and
  the `write` capability paragraph now lists the quest structural set + refusals. `SKILL.md`: quest interview
  questions (→ `quest.completions`/`quest.rewardShare`), a quest-authoring block in the loop, and the export
  note (write covers quests; always playtest after). The skill invents no verb/metric.
- **Tests:** `WriterTests.cs` +8 (55/55 total, was 47): new-quest insertion+block-rewrite, unreferenced delete,
  referenced-delete refusal, reorder-only block rewrite, reward.xp scalar edit, condition-amount edit, goal-kind
  structural refusal, dangling-prerequisite refusal. The `RoundTripPlansNoChanges` and `RoundTripPassesBootRules`
  canaries stay green (the live 3 quests pass the new mirror; a round-trip plans zero quest changes).

**Verified (automated, no user):**
- **CLI round-trip:** `read` live → `quest create` (FulfillOrders, MinLevel:2, +25xp/+$50) + `move` + `delete
  quest.harvest` → `sim` shows the new quest completing in-sim (`[quests 1 +25xp +$50]` at L5) → `write --apply`
  produced a **minimal, correct diff** (2-line `GameConfig.quests` edit dropping harvest + appending the new
  guid; a new `Quest_quest_fulfill.asset`+`.meta` matching Unity's exact byte format; `Quest_Harvest.asset`+meta
  deleted) — 0 scalar/level/upgrade changes.
- **Unity load:** refreshed AssetDatabase, entered playmode — `GameConfigSO.quests` on disk read
  `[quest.starter, quest.chain, quest.fulfill]`, GameBoot completed (playmode running = BootValidator did **not**
  throw), zero game-side exceptions (the only two console exceptions were my own `script-execute` typos). Then
  reverted the test write (`git checkout` + `rm` the created asset) — `Assets/` clean again.
- **Skill path:** goal with `quest.completions`+`quest.rewardShare` → `session start` → bare `eval` baseline →
  `quest create` on `config.current` + `eval --session` (completions 0.25→0.5) → `eval --session` moving
  `quests/quest.fulfill/reward.money` (rewardShare 0.018→0.069, journaled) → `sweep` a quest knob → `session
  report`. Guardrails hold: an out-of-bounds quest knob refused, an invented metric throws loud.

**Deviations from the plan:** none material. The doc allowed "new ops **or** a scoped `quest` sub-verb"; chose
the sub-verb (rationale above). Generated asset name is `Quest_<id-underscored>` (e.g. `Quest_quest_fulfill`),
mirroring `InsertRecipe`'s naming — cosmetic, Unity keys by guid.

**Tech debt:**
- `quest create` supports one `--condition` and one `--reward-resource` inline (covers all current quest shapes,
  which have 0–1 conditions). A quest needing 2+ conditions/reward-resources is authored by editing the config
  JSON directly, then `write`. Extend the flags to repeatable if a real multi-condition quest appears.
- A `quest create/move/delete` on a session's `config.current.json` is journaled only by the *next* bare
  `eval --session --rationale` (the structural edit itself isn't a journal line — `quest` is outside the `eval`
  primitive). The rationale carries the "what"; the config diff carries the structure. Acceptable; noted for the
  skill.

**Assumptions:**
- The `QuestSO` script guid is stolen from an existing quest asset at write time (fallback
  `3c9335b8e6a8345d2ae72fd9e7e239ee`), so it can't drift from the project — same trick as `ResolveRecipeScriptGuid`.
  If ALL quests are ever deleted and then one created in the same run, it falls back to the constant.
- Empty-string quest fields serialize as `key: ` (trailing space) and enum kinds as their **integer index** —
  matched to Unity's exact output, verified against `Quest_Starter.asset`. Enums stay append-only (M1's warning).

**FLAGS (M4 inherited decisions, now resolved / re-flagged for whoever tunes next):**
- **`progression-v1.json` still does NOT carry quests.** M4 flagged this open. Decision: **left quest-free.** M5
  is tool/skill plumbing, not a retune of the canonical progression config; splicing quests into progression-v1
  would silently change its economy (the same monotonicity break M4 saw in baseline) without a balance pass to
  justify it. Whoever does the next *balance* run on progression-v1 should decide whether it gains quests — and
  if so, author them via the `quest` verb (now available) rather than hand-editing JSON. Reversible.
- **`OptimalityMonotonicity` guard stays scoped to the quest-free economy** (`config.Quests.Clear()`, M4's
  choice). Decision: **kept as-is, not reconciled.** The guard's documented intent is the *dial*, orthogonal to
  quests; action-rewarding quests legitimately break dial-monotonicity (a perfect player skips a
  harvest-specific quest), which is a real economy interaction, not a dial regression. Reconciling it would mean
  either a quest-aware monotonicity model (speculative, YAGNI) or dropping the guard (loses dial coverage) —
  neither earns its keep now. If quests become central to progression tuning, revisit.

**Gotchas for later milestones:**
- **The `quest` verb is config→config; `write --apply` is the only thing that touches `Assets/`** and is gated
  in the skill. A quest authored/tuned in a session is invisible to Unity until export — then **playtest**, since
  the sim doesn't model boot/UI (a quest with `reward.xp = 0` collects with no particle in-game, per M1).
- **`write` diffs incoming config against a FRESH `read` of live Unity**, not against baseline. To get a
  quest-only diff, `read` live to a scratch config first, author on that, then `write` — otherwise baseline's
  pre-quest tuning shows up as a large scalar diff alongside the quest change.
- **`GameConfig.quests` is a 2-space block list** (`  quests:` + `  - {fileID…}` items), distinct from the
  4-space `grants:` block. `BuildQuestsBlock`/`ApplyQuestBlockRewrite` handle both the block and empty (`[]`) forms.
