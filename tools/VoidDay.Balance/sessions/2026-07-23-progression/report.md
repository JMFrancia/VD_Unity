# Balancing session — 2026-07-23-progression

_Generated from `journal.jsonl` (8 iterations). Every claim below traces to a journal line or to `config.start.json` / `config.current.json` — nothing is narrated._

## Goal — `progression-v1`

| Metric | Scope | Bound | Weight |
|---|---|---|---|
| level.durationMinutes | L1-2 | max 1.1 | 1 |
| level.durationMinutes | L3-4 | max 1.75 | 1 |
| level.durationMinutes | L5-8 | max 5 | 1 |
| total.minutesToLevel | L8 | min 10, max 25 | 2 |
| pressure.rank | L1 | rank ≤ 1 | 1.5 |
| pressure.rank | L2 | rank ≤ 1 | 1.5 |
| pressure.rank | L3-5 | rank ≤ 1 | 1.5 |
| pressure.rank | L6-7 | rank ≤ 1 | 1.5 |
| pressure.share | L1-2 | Yield, min 0.4 | 1 |
| pressure.share | L3 | Capacity, min 0.3 | 1 |
| pressure.share | L5 | Capacity, min 0.3 | 1 |
| pressure.share | L6 | Storage, min 0.25 | 1 |
| pressure.share | L7 | Storage, min 0.3 | 1 |
| level.moneyAtEntry | L5 | min 100 | 0.5 |
| level.moneyAtExit | L8 | max 3000 | 0.5 |
| gems.compressionShare | L4-8 | min 0, max 0.25 | 0.5 |

## Loss trajectory

Starting loss **7.4469** → final loss **1.21** over 8 iteration(s) (down 6.2369).

| # | Loss | Change | Rationale |
|---|---|---|---|
| 1 | 7.4469 | — | baseline as found — current live config, before any progression changes |
| 2 | 10.904 | ▲ 3.457 | Encode prescribed unlock schedule: bakery+wheat+bread at L2, cornbread+field-speed at L3, henhouse/pasture+brioche+silo/field upgrades at L4, cheesecake at L5; field cap 1; 0 starting gems |
| 3 | 9.5654 | ▼ 1.339 | Early XP curve: L2=6 (1 corn + 1 order), L3=20 (2 wheat + bread + sell), ramping L4=45..L8=500 to keep reach-L8 in the 12-25 min band |
| 4 | 9.0872 | ▼ 0.478 | Add structural grants (writer-refused, will be editor-authored at export): L2 +5 gems bonus, L3 +1 field, L4 +1 field cap |
| 5 | 6.154 | ▼ 2.933 | Tighten storage cap 30->7 so goods back up and Storage pressure appears in the midgame; lowest loss in a 5-14 sweep |
| 6 | 5.9026 | ▼ 0.251 | Cut money faucet 12->3 so cash stops ballooning and the player becomes cash-bound (Income pressure) when expanding; also brings L8 exit cash toward the not-swimming cap |
| 7 | 0.8296 | ▼ 5.073 | Realign goal to the achievable natural juggle: Yield(L1-2) -> Capacity(L3-5) -> Storage(L6-7), per user decision to accept it rather than force Income/Throughput |
| 8 | 1.21 | ▲ 0.38 | Remove L2 Money grant: BootValidator forbids >1 reward grant (Money/Gems) per level (level-up popup shows one reward); keep the requested 5-gem bonus. Caught by Unity boot validation, invisible to the sim. |

## Iterations

### Iteration 1 — loss 7.4469

- **Rationale:** baseline as found — current live config, before any progression changes
- **Patch:** (none — re-evaluation)
- **Config hash:** `e58d063e7209`
- **Top contributors:** pressure.rank@L4 1.5, pressure.rank@L6 1.5, pressure.share@L4 1, pressure.share@L6 1

### Iteration 2 — loss 10.904

- **Rationale:** Encode prescribed unlock schedule: bakery+wheat+bread at L2, cornbread+field-speed at L3, henhouse/pasture+brioche+silo/field upgrades at L4, cheesecake at L5; field cap 1; 0 starting gems
- **Patch:** `stations/bakery/unlockLevel` = 2, `stations/henhouse/unlockLevel` = 4, `stations/field/cap` = 1, `recipes/field.wheatGrow/unlockLevel` = 2, `recipes/field.fallowWheat/unlockLevel` = 2, `recipes/bakery.bread/unlockLevel` = 2, `recipes/bakery.cornbread/unlockLevel` = 3, `recipes/bakery.brioche/unlockLevel` = 4, `recipes/bakery.cheesecake/unlockLevel` = 5, `upgrades/field.speed/unlockLevel` = 3, `upgrades/field.queue/unlockLevel` = 4, `upgrades/field.yield/unlockLevel` = 4, `upgrades/silo.cap/unlockLevel` = 4, `gems.startingGems` = 0
- **Config hash:** `21ffc96def49`
- **Top contributors:** pressure.rank@L4 1.5, pressure.rank@L6 1.5, pressure.rank@L7 1.5, level.durationMinutes@L1-2 1.25

### Iteration 3 — loss 9.5654

- **Rationale:** Early XP curve: L2=6 (1 corn + 1 order), L3=20 (2 wheat + bread + sell), ramping L4=45..L8=500 to keep reach-L8 in the 12-25 min band
- **Patch:** `levels[1].xpThreshold` = 6, `levels[2].xpThreshold` = 20, `levels[3].xpThreshold` = 45, `levels[4].xpThreshold` = 90, `levels[5].xpThreshold` = 180, `levels[6].xpThreshold` = 320, `levels[7].xpThreshold` = 500
- **Config hash:** `c0dafeea0e05`
- **Top contributors:** pressure.rank@L4 1.5, pressure.rank@L6 1.5, pressure.rank@L7 1.5, pressure.share@L4 1

### Iteration 4 — loss 9.0872

- **Rationale:** Add structural grants (writer-refused, will be editor-authored at export): L2 +5 gems bonus, L3 +1 field, L4 +1 field cap
- **Patch:** (none — re-evaluation)
- **Config hash:** `f327d803bd97`
- **Top contributors:** pressure.rank@L4 1.5, pressure.rank@L7 1.5, pressure.share@L4 1, pressure.share@L5 1

### Iteration 5 — loss 6.154

- **Rationale:** Tighten storage cap 30->7 so goods back up and Storage pressure appears in the midgame; lowest loss in a 5-14 sweep
- **Patch:** `global.startingStorageCapacity` = 7
- **Config hash:** `fcd23aee9b22`
- **Top contributors:** pressure.rank@L6 1.5, pressure.rank@L4 1, pressure.share@L6 1, pressure.share@L8 1

### Iteration 6 — loss 5.9026

- **Rationale:** Cut money faucet 12->3 so cash stops ballooning and the player becomes cash-bound (Income pressure) when expanding; also brings L8 exit cash toward the not-swimming cap
- **Patch:** `orders.cashMultiplier` = 3
- **Config hash:** `d7ec40d94605`
- **Top contributors:** pressure.rank@L6 1.5, pressure.rank@L4 1, pressure.share@L6 1, pressure.share@L8 1

### Iteration 7 — loss 0.8296

- **Rationale:** Realign goal to the achievable natural juggle: Yield(L1-2) -> Capacity(L3-5) -> Storage(L6-7), per user decision to accept it rather than force Income/Throughput
- **Patch:** (none — re-evaluation)
- **Config hash:** `d7ec40d94605`
- **Top contributors:** pressure.rank@L6-7 0.5, level.durationMinutes@L1-2 0.21, level.durationMinutes@L3-4 0.12, level.durationMinutes@L5-8 0

### Iteration 8 — loss 1.21

- **Rationale:** Remove L2 Money grant: BootValidator forbids >1 reward grant (Money/Gems) per level (level-up popup shows one reward); keep the requested 5-gem bonus. Caught by Unity boot validation, invisible to the sim.
- **Patch:** (none — re-evaluation)
- **Config hash:** `1aba183efc22`
- **Top contributors:** pressure.share@L7 1, level.durationMinutes@L1-2 0.21, level.durationMinutes@L3-4 0, level.durationMinutes@L5-8 0

## Final loss breakdown

| Target | Contribution |
|---|---|
| pressure.share@L7 | 1 |
| level.durationMinutes@L1-2 | 0.21 |
| level.durationMinutes@L3-4 | 0 |
| level.durationMinutes@L5-8 | 0 |
| total.minutesToLevel@L8 | 0 |
| pressure.rank@L1 | 0 |
| pressure.rank@L2 | 0 |
| pressure.rank@L3-5 | 0 |
| pressure.rank@L6-7 | 0 |
| pressure.share@L1-2 | 0 |
| pressure.share@L3 | 0 |
| pressure.share@L5 | 0 |
| pressure.share@L6 | 0 |
| level.moneyAtEntry@L5 | 0 |
| level.moneyAtExit@L8 | 0 |
| gems.compressionShare@L4-8 | 0 |

## Diff to export to Unity

25 field(s) differ between `config.start.json` and `config.current.json`. This is what `write --apply` (gated) would push into `Assets/`:

| Path | Start | Current |
|---|---|---|
| `Global.StartingStorageCapacity` | 30 | 7 |
| `Gems.StartingGems` | 5 | 0 |
| `Orders.CashMultiplier` | 12 | 3 |
| `Recipes[bakery.bread].UnlockLevel` | 1 | 2 |
| `Recipes[bakery.brioche].UnlockLevel` | 1 | 4 |
| `Recipes[bakery.cheesecake].UnlockLevel` | 1 | 5 |
| `Recipes[bakery.cornbread].UnlockLevel` | 1 | 3 |
| `Stations[field].Cap` | 2 | 1 |
| `Stations[henhouse].UnlockLevel` | 3 | 4 |
| `Stations[bakery].UnlockLevel` | 7 | 2 |
| `Upgrades[field.queue].UnlockLevel` | 2 | 4 |
| `Upgrades[field.speed].UnlockLevel` | 1 | 3 |
| `Upgrades[field.yield].UnlockLevel` | 3 | 4 |
| `Upgrades[silo.cap].UnlockLevel` | 1 | 4 |
| `Levels[1].XpThreshold` | 20 | 6 |
| `Levels[1].Grants[1].Kind` | Money | Gems |
| `Levels[1].Grants[1].Amount` | 100 | 5 |
| `Levels[2].XpThreshold` | 50 | 20 |
| `Levels[2].Grants[1]` | (absent) | {
  "Kind": "StationCap",
  "TargetStation": "field",
  "Amount": 1
} |
| `Levels[3].XpThreshold` | 100 | 45 |
| `Levels[3].Grants[2]` | (absent) | {
  "Kind": "StationCap",
  "TargetStation": "field",
  "Amount": 1
} |
| `Levels[4].XpThreshold` | 175 | 90 |
| `Levels[5].XpThreshold` | 275 | 180 |
| `Levels[6].XpThreshold` | 400 | 320 |
| `Levels[7].XpThreshold` | 560 | 500 |

