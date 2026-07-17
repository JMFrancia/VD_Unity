# Milestone 05 — Station Upgrades & the Effect System

**Playable outcome:** Buy a station upgrade in a station's panel and watch its next job finish faster (or yield more), with the upgrade's benefit written out as a plain-language sentence.

## Goal
The Effect system — "the spine of the game; everything hangs off it" (§3). It lands here, in the first milestone that *visibly* needs it, and gives the `resolve()` seam M2 planted its teeth: the seam stops being a passthrough and starts summing real effects. Sized to **passive, own-station modifiers** (what station upgrades need); later milestones extend the vocabulary. Station upgrades are the visible payload that proves it works.

## Build This
- **The Effect + Trait schema** (§3.1): plain `[System.Serializable]` C# in `Core/Model` — `Effect`, `Trait`, `EffectValue`, `Condition`, and the enums (`EffectOp`, `EffectType`, `TriggerType`, `ConditionType`). No `using UnityEngine`, so they serialize into the inspector *and* resolve headless. Define the **full** enum vocabularies now (every `EffectType`/`TriggerType`/`ConditionType` member) even though only a subset resolves this milestone — the schema is authored once.
- **The resolver** (§3.5) in `Core/Rules`: for a given `type`, sum `Flat`, sum `Pct` and apply once, then multiply `Mult` in sequence. Pure C#, testable headless.
- **Wire the resolver into the `resolve()` seam**: replace the passthrough for **own-station** `station.speed`, `station.yield`, `station.cost`, `station.queueDepth`, and the **`xp.gain`** award site. Note `station.cost` is wired even though no upgrade this milestone emits it — so M9/M10 cost-affinity traits (e.g. "Thrifty") actually apply where they're consumed (queue-time input cost, M2). Same for `xp.gain`: wired at M3's XP-award site so a later `xp.gain` emitter takes effect.
- **Procedural description generator** (§3.6): one function producing a player-facing sentence from any Trait's effects. The three §3.6 examples (Cow Lover, Hard Worker, Thrifty) are the reference cases. Serves upgrades now; pets/relationships/events reuse it.
- **Station upgrades** (§8): tiered, per-tier cost explicit on `UpgradeSO` (no formula), emitting `Effect[]`. The three station tracks: job speed, queue depth, output yield.
- **Upgrade rows in `panel.station`** (`pattern.purchaseRow`): effect via the procedural description, money cost, current→next tier, `Buy` / `Maxed`. Buying emits `input:upgradePurchaseRequested`.
- **`effects:recalculated`**: emit when the active effect set changes so views/systems re-read resolved values.
- **Passive scope only:** effects with `TriggerType.None` and own-station reach. `condition` may be `None` or a simple always-evaluable one; leave trigger-fired and range/proximity conditions to M9/M10.
- **Boot validation for effects** (§3.1): reject malformed combinations loudly (e.g. `triggerChance` 0 treated as unset→100, local/pet types missing `range`).

## Do NOT Build This
- **`global.*` / `order.*` / `build.cost` scopes** → M6.
- **`storage.cap`** → M7.
- **Triggers (`JobCompleted` etc.), `triggerChance` firing, `egg.chance`, `pet.autoCollectSpeed`** → M9.
- **`WithinRangePet` / `local.*` / per-effect range** → M10.
- **`pet.effectStrength`, `WithinRangeStation`** → not built this prototype (flagged in summary Open Items). Define the enum members; do not resolve them.
- **Universal/Silo upgrade panels** → M6/M7.
- **Level-gating of which upgrades are purchasable** → M8 (build the gate read here if simplest, but its activation is M8).

## Context
Builds on M2 (`resolve()` seam, station panel, Producer), M3 (XP-award site). Adds to the spine:
- **Events added:** `input:upgradePurchaseRequested {upgradeId}`; `effects:recalculated`.
- **Data added:** `Effect`/`Trait`/`Condition`/`EffectValue` + enums (`Core/Model`); `UpgradeSO` → model (station tiers, per-tier cost, `Effect[]`).
- **Systems touched:** new `Core/Rules/EffectResolver`, `Core/Rules/TraitDescription`, `Systems/Upgrades`; `resolve()` seam call-sites in `Systems/Producer` (speed/yield/cost/queueDepth) and `Systems/Progression` (xp.gain) now route through the resolver; `View/StationPanel` (upgrade rows).

## Principles
- **The Effect system is the spine** (§3): one schema, one resolver, one description generator. Adding an `EffectType` later must not need a new resolver — build it type-agnostic.
- **The Core boundary** (rule 3): `Effect`/`Trait` are pure C# so they cross into Core unprojected (§14 — the one exception to SO→Model projection). The resolver never sees a `*SO`.
- **Test the core** (CLAUDE.md): the resolver's stacking math (`+25%` and `+25%` = `+50%`, not `×1.5625`) and the description generator's three reference sentences are exactly what to cover with EditMode tests.
- **Data-driven** (rule 1): every upgrade magnitude and cost is on `UpgradeSO`; code reads `Effect` structs, never literals.
- **No abstraction until the third occurrence** (CLAUDE.md speed rule): the resolver is genuinely shared (rule 1 of the spine), so it's not premature — but don't over-generalize conditions/triggers you aren't resolving yet.

## Assets Required
- No new meshes. Upgrade rows are text + `ui.button` + `ui.card` [placeholder OK]. Upgrade menus use procedural text, **no per-effect icons** (asset doc: cut).
- **SFX** [placeholder OK]: reuse `sfx.order.fulfill` as the purchase-confirm chime (asset doc note — no distinct upgrade event in §15).

## UI Mockups Required
- `panel.station` upgrade section (station-upgrade `pattern.purchaseRow`s) — [mockup needed]

## Definition of Done
- A station panel shows tiered upgrade rows with plain-language effect descriptions (e.g. "+25% speed at its station"), a money cost, and current→next tier.
- Buying a speed upgrade makes the *next* job's timer visibly shorter; buying yield makes the output quantity larger; both stack per the §3.5 rules.
- The three §3.6 reference sentences generate correctly.
- Reaching top tier shows "Maxed" with no button.
- EditMode tests for the resolver stacking math and the three descriptions pass.

## How to Test
1. Open the Field panel → confirm upgrade rows with readable descriptions and costs.
2. Note a recipe's timer; buy the speed upgrade; queue the same recipe → timer is shorter.
3. Buy it again → confirm two `+25%` stack to `+50%` (not `×1.5625`) — time the job or check a debug readout.
4. Buy a yield upgrade → the same recipe outputs more.
5. Max a track → row shows "Maxed", no button.
6. Run the EditMode tests → resolver + description tests green.
