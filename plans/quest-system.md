# Quest System — Design Record

> This is the *reasoning* record. The buildable plan is the milestone set in
> `milestones/Quest-System/` — where the two disagree, the milestone docs win
> (and `00-summary.md` says so).

## What it is

Data-driven, event-driven quests. A designer authors a `QuestSO` with **conditions**
(when it's granted), one **goal** (what to do), a **reward** (XP + optional resources),
and a **description** generated from the goal. A pure-C# quest engine listens to the
existing event bus, tracks progress (monotonic — never decreases), marks quests
ready-to-collect at 100%, and grants rewards on collect. A completed quest is never
granted twice. Three display surfaces: a scrollable quest menu, a new-quest toast, and
an animated progress pill that drops from behind the XP bar.

Additionally: the offline balance tool (`tools/VoidDay.Balance`) must model quests in
its headless sim **and** be able to create/edit/reorder them, with the `balance_game`
skill knowing how to drive that.

## The decisive architectural fact

`tools/VoidDay.Balance/VoidDay.Balance.csproj` globs `..\..\Assets\Core\**\*.cs`
straight into its own compile. Because `Assets/Core` is engine-free pure C#, the tool
**shares the real Core economy code** — it does not re-implement it. The "one-way
mirror" rule is about *knowledge direction* (`Assets/` never references the tool), not
about duplicating logic.

**Consequence that settles the whole design:** put quest *rules* in `Assets/Core` as
engine-free pure C# (which the Core boundary rule requires anyway), and the offline sim
runs the real quest logic for free. The tool then only needs *data* mirroring, *wiring*,
*metrics*, *knobs*, and *write-back* — never a re-implementation of quest behavior.

## Approaches considered

**A — Pure-Core `QuestLog` + enum-discriminator SO data (CHOSEN).**
Quest rules live in `Assets/Core/Rules/QuestLog.cs`, a plain C# object constructed in
`GameBoot` that subscribes directly to the `EventBus` (the bus is Core), exactly like
`OrderBoard` / `Progression`. Quest data is a `QuestSO` using flat `[Serializable]`
structs with enum discriminators + generic payload fields — the project's established
pattern (`Effect` / `Effect.Condition` / `LevelGrant`), *not* `[SerializeReference]`
(which the codebase never uses). Reuses `ToastController`, `EarnBurstController`
particles, the `Image`-fill-bar idiom, and the `ResourcePillRail` "slide from behind a
pill" pattern.

**B — Quest logic in a Systems-layer MonoBehaviour (REJECTED).**
Would break the Core boundary rule (rule 3 in CLAUDE.md) *and* — because the tool only
shares `Assets/Core`, not `Assets/Systems` — force a by-hand mirror of quest logic inside
`CoreHarness` (the way it already hand-mirrors `ProgressionSystem`/`UpgradesSystem`),
recreating exactly the drift/maintenance burden the shared compile avoids. The balance
requirement makes A strictly better, not just cleaner.

## Integration points (verified during design — see LOG for file:line)

- **Event bus:** `Assets/Core/Events/EventBus.cs` (`Subscribe<T>`/`Unsubscribe<T>`/`Publish<T>`).
  Events catalog: `Assets/Core/Events/GameEvents.cs`.
- **Goal-relevant events already emitted:** `OrderFulfilled`, `JobCollected` (= crops
  harvested), `MoneyChanged` (filter `Delta>0` for *earned*), `UpgradePurchased`,
  `StationBuilt`, `XpGained`, `LevelUp`, `ResourceChanged`, `GemsChanged`.
- **Reward sinks:** `Wallet.Add(int)`, `GemPurse.Add(int)`, `ResourcePool.Add(string,int)`,
  `Progression.AwardXp(int, string)`.
- **Condition state reads:** `Progression.PlayerLevel`, `ResourcePool.Get(id)`,
  `UpgradeSystem.TierOf(stationId, trackId)`, quest-completed set (internal).
- **Data pattern to copy:** `Assets/Core/Model/Effect.cs` (flat struct + enum + `switch`,
  throws on unknown enum), `Assets/Data/LevelSO.cs` (`LevelGrant` list).
- **Config seam:** add `List<QuestSO> quests` to `GameConfigSO`; validate in
  `BootValidator`; construct `QuestLog` in `GameBoot` alongside `OrderBoard`/`Progression`.
- **UI reuse:** `Assets/View/ToastController.cs` + `Assets/Prefabs/UI/Toast.prefab`;
  `Assets/View/EarnBurstController.cs` + `Assets/Prefabs/UI/EarnParticle.prefab`;
  fill-bar idiom `SiloPanel.capacityBarFill`; pill-from-behind `Assets/View/ResourcePillRail.cs`;
  HUD host `Assets/View/Hud.cs`, `HudCanvas/DebugButton` at anchored `{24,-24}` top-left.
- **Tool:** `BalanceConfig` schema (`Schema/BalanceConfig.cs`), reader
  (`Unity/EconomyReader.cs` + `Unity/RawAssets.cs`), harness (`Sim/CoreHarness.cs` mirrors
  `GameBoot`), metrics (`Sim/MetricsCollector.cs`), knobs (`Agent/Patch.cs` + `bounds.json`),
  write-back (`Unity/AssetWriter.cs`), contract (`AGENTS.md`), skill
  (`.claude/skills/balance_game/SKILL.md`).
