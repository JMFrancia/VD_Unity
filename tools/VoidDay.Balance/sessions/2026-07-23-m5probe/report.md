# Balancing session — 2026-07-23-m5probe

_Generated from `journal.jsonl` (3 iterations). Every claim below traces to a journal line or to `config.start.json` / `config.current.json` — nothing is narrated._

## Goal — `quest-pacing`

| Metric | Scope | Bound | Weight |
|---|---|---|---|
| quest.completions | L2-5 | min 1 | 1 |
| quest.rewardShare | L2-5 | max 0.3 | 1 |

## Loss trajectory

Starting loss **3** → final loss **3** over 3 iteration(s) (unchanged).

| # | Loss | Change | Rationale |
|---|---|---|---|
| 1 | 3 | — | baseline as found |
| 2 | 3 | — | authored a level-2 FulfillOrders quest to raise quest.completions on L2-5 |
| 3 | 3 | — | raise fulfill reward money to test quest.rewardShare cap |

## Iterations

### Iteration 1 — loss 3

- **Rationale:** baseline as found
- **Patch:** (none — re-evaluation)
- **Config hash:** `05f5a8dbba55`
- **Top contributors:** quest.completions@L2-5 3, quest.rewardShare@L2-5 0

### Iteration 2 — loss 3

- **Rationale:** authored a level-2 FulfillOrders quest to raise quest.completions on L2-5
- **Patch:** (none — re-evaluation)
- **Config hash:** `a3ed90dcf2d1`
- **Top contributors:** quest.completions@L2-5 3, quest.rewardShare@L2-5 0

### Iteration 3 — loss 3

- **Rationale:** raise fulfill reward money to test quest.rewardShare cap
- **Patch:** `quests/quest.fulfill/reward.money` = 200
- **Config hash:** `e2170a915d73`
- **Top contributors:** quest.completions@L2-5 3, quest.rewardShare@L2-5 0

## Final loss breakdown

| Target | Contribution |
|---|---|
| quest.completions@L2-5 | 3 |
| quest.rewardShare@L2-5 | 0 |

## Diff to export to Unity

1 field(s) differ between `config.start.json` and `config.current.json`. This is what `write --apply` (gated) would push into `Assets/`:

| Path | Start | Current |
|---|---|---|
| `Quests[quest.fulfill]` | (absent) | {
  "Id": "quest.fulfill",
  "Conditions": [
    {
      "Kind": "MinLevel",
      "Amount": 2,
      "Arg": ""
    }
  ],
  "Goal": {
    "Kind": "FulfillOrders",
    "Amount": 3,
    "TargetId": ""
  },
  "Reward": {
    "Xp": 25,
    "Money": 200,
    "Gems": 0,
    "Resources": []
  }
} |

