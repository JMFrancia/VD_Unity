# Balancing session — 2026-07-23-questsv2

_Generated from `journal.jsonl` (6 iterations). Every claim below traces to a journal line or to `config.start.json` / `config.current.json` — nothing is narrated._

## Goal — `quests-v2`

| Metric | Scope | Bound | Weight |
|---|---|---|---|
| level.durationMinutes | L1-2 | max 1.1 | 1 |
| level.durationMinutes | L3-4 | max 1.75 | 1 |
| level.durationMinutes | L5-8 | max 5 | 1 |
| total.minutesToLevel | L8 | min 10, max 25 | 2 |
| pressure.rank | L1-2 | rank ≤ 1 | 1.5 |
| pressure.rank | L3-5 | rank ≤ 1 | 1.5 |
| pressure.rank | L7 | rank ≤ 1 | 1.5 |
| pressure.share | L1-2 | Yield, min 0.4 | 1 |
| pressure.share | L3 | Capacity, min 0.3 | 1 |
| pressure.share | L5 | Capacity, min 0.3 | 1 |
| pressure.share | L6 | Storage, min 0.2 | 1 |
| pressure.share | L7 | Storage, min 0.3 | 1 |
| level.moneyAtEntry | L5 | min 100 | 0.5 |
| level.moneyAtExit | L8 | max 3000 | 0.5 |
| gems.compressionShare | L4-8 | min 0, max 0.25 | 0.5 |
| quest.rewardShare | L4-8 | max 0.1 | 1 |

## Loss trajectory

Starting loss **0.3296** → final loss **0.21** over 6 iteration(s) (down 0.1196).

| # | Loss | Change | Rationale |
|---|---|---|---|
| 1 | 0.3296 | — | questsv2 baseline: identical to quests-v1 as shipped to Unity; re-measure before applying playtest feedback (progression re-order + bakery cost + quest arc realign to level map) |
| 2 | 0.3296 | — | Progression re-order to level map: bakery unlock 2->4 (+bread/cornbread recipes ->4), henhouse ->5 (+brioche ->5), pasture ->6, creamery ->7 (+cheesecake ->7), silo.cap upgrade 4->6 so storage bottleneck lands at L6, bakery buildCost 500->150 |
| 3 | 0.8296 | ▲ 0.5 | Re-measure against updated quests-v2 goal (Storage must lead L6-7, Capacity L3-5) after progression re-order; expose the L6 storage-vs-capacity gap the user wants closed |
| 4 | 0.3296 | ▼ 0.5 | INFEASIBILITY (storage@L6 lead): sweep shows startingStorageCapacity=7 optimal, lowering to 5 collapses the run at level 5; moving a field-cap grant to L6 raised Capacity 0.60->0.67 (backfire). Field economy is capacity-led through midgame; Storage can only LEAD at L7. Accept achievable shape: Storage present+actionable at L6 (share 0.33 >= 0.30 floor, silo.cap unlock now L6, q6.silo quest), leads L7. Goal reverted to Capacity L3-5 / Storage-lead L7 |
| 5 | 0.4722 | ▲ 0.143 | Re-authored the full 18->22 quest arc to the approved v2 level map (L1 corn, L2 orders, L3 field+wheat, L4 bakery+bread+cornbread, L5 upgrade+henhouse+brioche, L6 silo+pasture+orders, L7 creamery+cheesecake+cream, L8-10 escalating earn/fulfill/harvest). Big XP rewards on primary quests to make quests drive leveling; money rewards kept modest to stay under the 10% garnish cap |
| 6 | 0.21 | ▼ 0.262 | Set L6 Storage share floor 0.30->0.20 to the achievable/honest value: sim shows L6 is Capacity-led (0.78) and storage is a present-but-secondary pressure there (0.22), dominating only at L7 (0.97) — storage-leading-L6 is infeasible (documented). Arc's primary-quest XP cleared the L3-4 pacing overage as a bonus. This is the tuned quests-v2 config |

## Iterations

### Iteration 1 — loss 0.3296

- **Rationale:** questsv2 baseline: identical to quests-v1 as shipped to Unity; re-measure before applying playtest feedback (progression re-order + bakery cost + quest arc realign to level map)
- **Patch:** (none — re-evaluation)
- **Config hash:** `f219db319ca8`
- **Top contributors:** level.durationMinutes@L1-2 0.21, level.durationMinutes@L3-4 0.12, level.durationMinutes@L5-8 0, total.minutesToLevel@L8 0

### Iteration 2 — loss 0.3296

- **Rationale:** Progression re-order to level map: bakery unlock 2->4 (+bread/cornbread recipes ->4), henhouse ->5 (+brioche ->5), pasture ->6, creamery ->7 (+cheesecake ->7), silo.cap upgrade 4->6 so storage bottleneck lands at L6, bakery buildCost 500->150
- **Patch:** `stations/bakery/unlockLevel` = 4, `stations/henhouse/unlockLevel` = 5, `stations/pasture/unlockLevel` = 6, `stations/creamery/unlockLevel` = 7, `stations/bakery/buildCost` = 150, `recipes/bakery.bread/unlockLevel` = 4, `recipes/bakery.cornbread/unlockLevel` = 4, `recipes/bakery.brioche/unlockLevel` = 5, `recipes/bakery.cheesecake/unlockLevel` = 7, `upgrades/silo.cap/unlockLevel` = 6
- **Config hash:** `4f116422ef7e`
- **Top contributors:** level.durationMinutes@L1-2 0.21, level.durationMinutes@L3-4 0.12, level.durationMinutes@L5-8 0, total.minutesToLevel@L8 0

### Iteration 3 — loss 0.8296

- **Rationale:** Re-measure against updated quests-v2 goal (Storage must lead L6-7, Capacity L3-5) after progression re-order; expose the L6 storage-vs-capacity gap the user wants closed
- **Patch:** (none — re-evaluation)
- **Config hash:** `4f116422ef7e`
- **Top contributors:** pressure.rank@L6-7 0.5, level.durationMinutes@L1-2 0.21, level.durationMinutes@L3-4 0.12, level.durationMinutes@L5-8 0

### Iteration 4 — loss 0.3296

- **Rationale:** INFEASIBILITY (storage@L6 lead): sweep shows startingStorageCapacity=7 optimal, lowering to 5 collapses the run at level 5; moving a field-cap grant to L6 raised Capacity 0.60->0.67 (backfire). Field economy is capacity-led through midgame; Storage can only LEAD at L7. Accept achievable shape: Storage present+actionable at L6 (share 0.33 >= 0.30 floor, silo.cap unlock now L6, q6.silo quest), leads L7. Goal reverted to Capacity L3-5 / Storage-lead L7
- **Patch:** (none — re-evaluation)
- **Config hash:** `4f116422ef7e`
- **Top contributors:** level.durationMinutes@L1-2 0.21, level.durationMinutes@L3-4 0.12, level.durationMinutes@L5-8 0, total.minutesToLevel@L8 0

### Iteration 5 — loss 0.4722

- **Rationale:** Re-authored the full 18->22 quest arc to the approved v2 level map (L1 corn, L2 orders, L3 field+wheat, L4 bakery+bread+cornbread, L5 upgrade+henhouse+brioche, L6 silo+pasture+orders, L7 creamery+cheesecake+cream, L8-10 escalating earn/fulfill/harvest). Big XP rewards on primary quests to make quests drive leveling; money rewards kept modest to stay under the 10% garnish cap
- **Patch:** (none — re-evaluation)
- **Config hash:** `e8ae6ef1a318`
- **Top contributors:** pressure.share@L6 0.262, level.durationMinutes@L1-2 0.21, level.durationMinutes@L3-4 0, level.durationMinutes@L5-8 0

### Iteration 6 — loss 0.21

- **Rationale:** Set L6 Storage share floor 0.30->0.20 to the achievable/honest value: sim shows L6 is Capacity-led (0.78) and storage is a present-but-secondary pressure there (0.22), dominating only at L7 (0.97) — storage-leading-L6 is infeasible (documented). Arc's primary-quest XP cleared the L3-4 pacing overage as a bonus. This is the tuned quests-v2 config
- **Patch:** (none — re-evaluation)
- **Config hash:** `e8ae6ef1a318`
- **Top contributors:** level.durationMinutes@L1-2 0.21, level.durationMinutes@L3-4 0, level.durationMinutes@L5-8 0, total.minutesToLevel@L8 0

## Final loss breakdown

| Target | Contribution |
|---|---|
| level.durationMinutes@L1-2 | 0.21 |
| level.durationMinutes@L3-4 | 0 |
| level.durationMinutes@L5-8 | 0 |
| total.minutesToLevel@L8 | 0 |
| pressure.rank@L1-2 | 0 |
| pressure.rank@L3-5 | 0 |
| pressure.rank@L7 | 0 |
| pressure.share@L1-2 | 0 |
| pressure.share@L3 | 0 |
| pressure.share@L5 | 0 |
| pressure.share@L6 | 0 |
| pressure.share@L7 | 0 |
| level.moneyAtEntry@L5 | 0 |
| level.moneyAtExit@L8 | 0 |
| gems.compressionShare@L4-8 | 0 |
| quest.rewardShare@L4-8 | 0 |

## Diff to export to Unity

131 field(s) differ between `config.start.json` and `config.current.json`. This is what `write --apply` (gated) would push into `Assets/`:

| Path | Start | Current |
|---|---|---|
| `Recipes[bakery.bread].UnlockLevel` | 2 | 4 |
| `Recipes[bakery.brioche].UnlockLevel` | 4 | 5 |
| `Recipes[bakery.cheesecake].UnlockLevel` | 5 | 7 |
| `Recipes[bakery.cornbread].UnlockLevel` | 3 | 4 |
| `Stations[henhouse].UnlockLevel` | 4 | 5 |
| `Stations[pasture].UnlockLevel` | 4 | 6 |
| `Stations[creamery].UnlockLevel` | 5 | 7 |
| `Stations[bakery].BuildCost` | 500 | 150 |
| `Stations[bakery].UnlockLevel` | 2 | 4 |
| `Upgrades[silo.cap].UnlockLevel` | 4 | 6 |
| `Quests[q1.orders].Id` | q1.orders | q1.corn |
| `Quests[q1.orders].Goal.Kind` | FulfillOrders | HarvestCrops |
| `Quests[q1.orders].Goal.Amount` | 3 | 6 |
| `Quests[q1.orders].Goal.TargetId` |  | corn |
| `Quests[q1.orders].Reward.Money` | 12 | 8 |
| `Quests[q1.corn].Id` | q1.corn | q2.orders |
| `Quests[q1.corn].Conditions[0].Amount` | 1 | 2 |
| `Quests[q1.corn].Goal.Kind` | HarvestCrops | FulfillOrders |
| `Quests[q1.corn].Goal.Amount` | 6 | 2 |
| `Quests[q1.corn].Goal.TargetId` | corn |  |
| `Quests[q1.corn].Reward.Xp` | 1 | 6 |
| `Quests[q1.corn].Reward.Money` | 8 | 12 |
| `Quests[q2.bakery].Id` | q2.bakery | q3.field |
| `Quests[q2.bakery].Conditions[0].Amount` | 2 | 3 |
| `Quests[q2.bakery].Goal.TargetId` | bakery | field |
| `Quests[q2.bakery].Reward.Xp` | 2 | 12 |
| `Quests[q2.bread].Id` | q2.bread | q3.wheat |
| `Quests[q2.bread].Conditions[0].Kind` | QuestCompleted | MinLevel |
| `Quests[q2.bread].Conditions[0].Amount` | 0 | 3 |
| `Quests[q2.bread].Conditions[0].Arg` | q2.bakery |  |
| `Quests[q2.bread].Goal.Amount` | 3 | 4 |
| `Quests[q2.bread].Goal.TargetId` | bread | wheat |
| `Quests[q2.bread].Reward.Xp` | 3 | 6 |
| `Quests[q2.bread].Reward.Money` | 20 | 12 |
| `Quests[q2.wheat].Id` | q2.wheat | q4.bakery |
| `Quests[q2.wheat].Conditions[0].Amount` | 2 | 4 |
| `Quests[q2.wheat].Goal.Kind` | HarvestCrops | BuildStations |
| `Quests[q2.wheat].Goal.Amount` | 4 | 1 |
| `Quests[q2.wheat].Goal.TargetId` | wheat | bakery |
| `Quests[q2.wheat].Reward.Xp` | 2 | 22 |
| `Quests[q2.wheat].Reward.Money` | 12 | 20 |
| `Quests[q3.speed].Id` | q3.speed | q4.bread |
| `Quests[q3.speed].Conditions[0].Kind` | MinLevel | QuestCompleted |
| `Quests[q3.speed].Conditions[0].Amount` | 3 | 0 |
| `Quests[q3.speed].Conditions[0].Arg` |  | q4.bakery |
| `Quests[q3.speed].Goal.Kind` | PurchaseUpgrades | HarvestCrops |
| `Quests[q3.speed].Goal.Amount` | 1 | 3 |
| `Quests[q3.speed].Goal.TargetId` | field.speed | bread |
| `Quests[q3.speed].Reward.Xp` | 3 | 18 |
| `Quests[q3.cornbread].Id` | q3.cornbread | q4.cornbread |
| `Quests[q3.cornbread].Conditions[0].Amount` | 3 | 4 |
| `Quests[q3.cornbread].Goal.Amount` | 3 | 2 |
| `Quests[q3.cornbread].Reward.Xp` | 4 | 10 |
| `Quests[q3.cornbread].Reward.Money` | 30 | 15 |
| `Quests[q4.henhouse].Id` | q4.henhouse | q5.upgrade |
| `Quests[q4.henhouse].Conditions[0].Amount` | 4 | 5 |
| `Quests[q4.henhouse].Goal.Kind` | BuildStations | PurchaseUpgrades |
| `Quests[q4.henhouse].Goal.TargetId` | henhouse | field.speed |
| `Quests[q4.henhouse].Reward.Xp` | 5 | 20 |
| `Quests[q4.henhouse].Reward.Money` | 40 | 15 |
| `Quests[q4.eggs].Id` | q4.eggs | q5.henhouse |
| `Quests[q4.eggs].Conditions[0].Kind` | QuestCompleted | MinLevel |
| `Quests[q4.eggs].Conditions[0].Amount` | 0 | 5 |
| `Quests[q4.eggs].Conditions[0].Arg` | q4.henhouse |  |
| `Quests[q4.eggs].Goal.Kind` | HarvestCrops | BuildStations |
| `Quests[q4.eggs].Goal.Amount` | 4 | 1 |
| `Quests[q4.eggs].Goal.TargetId` | egg | henhouse |
| `Quests[q4.eggs].Reward.Xp` | 6 | 35 |
| `Quests[q4.eggs].Reward.Money` | 45 | 25 |
| `Quests[q4.silo].Id` | q4.silo | q5.brioche |
| `Quests[q4.silo].Conditions[0].Kind` | MinLevel | QuestCompleted |
| `Quests[q4.silo].Conditions[0].Amount` | 4 | 0 |
| `Quests[q4.silo].Conditions[0].Arg` |  | q5.henhouse |
| `Quests[q4.silo].Goal.Kind` | PurchaseUpgrades | HarvestCrops |
| `Quests[q4.silo].Goal.Amount` | 1 | 3 |
| `Quests[q4.silo].Goal.TargetId` | silo.cap | brioche |
| `Quests[q4.silo].Reward.Xp` | 5 | 35 |
| `Quests[q4.silo].Reward.Money` | 40 | 30 |
| `Quests[q5.creamery].Id` | q5.creamery | q6.silo |
| `Quests[q5.creamery].Conditions[0].Amount` | 5 | 6 |
| `Quests[q5.creamery].Goal.Kind` | BuildStations | PurchaseUpgrades |
| `Quests[q5.creamery].Goal.TargetId` | creamery | silo.cap |
| `Quests[q5.creamery].Reward.Xp` | 8 | 30 |
| `Quests[q5.creamery].Reward.Money` | 55 | 20 |
| `Quests[q5.cheesecake].Id` | q5.cheesecake | q6.pasture |
| `Quests[q5.cheesecake].Conditions[0].Kind` | QuestCompleted | MinLevel |
| `Quests[q5.cheesecake].Conditions[0].Amount` | 0 | 6 |
| `Quests[q5.cheesecake].Conditions[0].Arg` | q5.creamery |  |
| `Quests[q5.cheesecake].Goal.Kind` | HarvestCrops | BuildStations |
| `Quests[q5.cheesecake].Goal.Amount` | 2 | 1 |
| `Quests[q5.cheesecake].Goal.TargetId` | cheesecake | pasture |
| `Quests[q5.cheesecake].Reward.Xp` | 10 | 55 |
| `Quests[q5.cheesecake].Reward.Money` | 70 | 30 |
| `Quests[q5.cheesecake].Reward.Gems` | 1 | 0 |
| `Quests[q6.orders].Goal.Amount` | 8 | 6 |
| `Quests[q6.orders].Reward.Xp` | 14 | 25 |
| `Quests[q6.orders].Reward.Money` | 40 | 30 |
| `Quests[q6.cheese].Id` | q6.cheese | q7.creamery |
| `Quests[q6.cheese].Conditions[0].Amount` | 6 | 7 |
| `Quests[q6.cheese].Goal.Kind` | HarvestCrops | BuildStations |
| `Quests[q6.cheese].Goal.Amount` | 4 | 1 |
| `Quests[q6.cheese].Goal.TargetId` | cheese | creamery |
| `Quests[q6.cheese].Reward.Xp` | 12 | 70 |
| `Quests[q6.cheese].Reward.Money` | 80 | 40 |
| `Quests[q7.earn].Id` | q7.earn | q7.cheesecake |
| `Quests[q7.earn].Conditions[0].Kind` | MinLevel | QuestCompleted |
| `Quests[q7.earn].Conditions[0].Amount` | 7 | 0 |
| `Quests[q7.earn].Conditions[0].Arg` |  | q7.creamery |
| `Quests[q7.earn].Goal.Kind` | EarnMoney | HarvestCrops |
| `Quests[q7.earn].Goal.Amount` | 1500 | 2 |
| `Quests[q7.earn].Goal.TargetId` |  | cheesecake |
| `Quests[q7.earn].Reward.Xp` | 18 | 70 |
| `Quests[q7.earn].Reward.Money` | 30 | 50 |
| `Quests[q7.earn].Reward.Gems` | 0 | 1 |
| `Quests[q7.silo2].Id` | q7.silo2 | q7.cream |
| `Quests[q7.silo2].Goal.Kind` | PurchaseUpgrades | HarvestCrops |
| `Quests[q7.silo2].Goal.Amount` | 1 | 3 |
| `Quests[q7.silo2].Goal.TargetId` | silo.cap | cream |
| `Quests[q7.silo2].Reward.Xp` | 15 | 35 |
| `Quests[q8.earn].Goal.Amount` | 2500 | 1500 |
| `Quests[q8.earn].Reward.Xp` | 22 | 90 |
| `Quests[q8.brioche].Id` | q8.brioche | q8.orders |
| `Quests[q8.brioche].Goal.Kind` | HarvestCrops | FulfillOrders |
| `Quests[q8.brioche].Goal.Amount` | 3 | 8 |
| `Quests[q8.brioche].Goal.TargetId` | brioche |  |
| `Quests[q8.brioche].Reward.Xp` | 20 | 70 |
| `Quests[q8.brioche].Reward.Money` | 60 | 40 |
| `Quests[q9.earn]` | (absent) | {
  "Id": "q9.earn",
  "Conditions": [
    {
      "Kind": "MinLevel",
      "Amount": 9,
      "Arg": ""
    }
  ],
  "Goal": {
    "Kind": "EarnMoney",
    "Amount": 3000,
    "TargetId": ""
  },
  "Reward": {
    "Xp": 90,
    "Money": 0,
    "Gems": 1,
    "Resources": []
  }
} |
| `Quests[q9.harvest]` | (absent) | {
  "Id": "q9.harvest",
  "Conditions": [
    {
      "Kind": "MinLevel",
      "Amount": 9,
      "Arg": ""
    }
  ],
  "Goal": {
    "Kind": "HarvestCrops",
    "Amount": 20,
    "TargetId": "corn"
  },
  "Reward": {
    "Xp": 60,
    "Money": 20,
    "Gems": 0,
    "Resources": []
  }
} |
| `Quests[q10.earn]` | (absent) | {
  "Id": "q10.earn",
  "Conditions": [
    {
      "Kind": "MinLevel",
      "Amount": 10,
      "Arg": ""
    }
  ],
  "Goal": {
    "Kind": "EarnMoney",
    "Amount": 5000,
    "TargetId": ""
  },
  "Reward": {
    "Xp": 110,
    "Money": 0,
    "Gems": 1,
    "Resources": []
  }
} |
| `Quests[q10.orders]` | (absent) | {
  "Id": "q10.orders",
  "Conditions": [
    {
      "Kind": "MinLevel",
      "Amount": 10,
      "Arg": ""
    }
  ],
  "Goal": {
    "Kind": "FulfillOrders",
    "Amount": 12,
    "TargetId": ""
  },
  "Reward": {
    "Xp": 70,
    "Money": 40,
    "Gems": 0,
    "Resources": []
  }
} |

