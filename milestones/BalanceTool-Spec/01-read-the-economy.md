# Milestone 01 — Read the Economy

**Demonstrable outcome:** run `balance read` and get one JSON file containing every economy number in the
game — recipes, stations, upgrades, levels, orders, resources — that you can check line-by-line against the
inspector and find correct.

## Goal
Stand up the tool and prove it can see the game. This is the milestone that establishes the one-way
dependency: a .NET project that compiles the real Core and parses Unity's assets, with Unity none the wiser.
Everything later reads from the `BalanceConfig` born here.

## Build This
- **`.gitignore` fix, first.** Add `!tools/**/*.csproj`, `tools/**/bin/`, `tools/**/obj/`. The existing bare
  `*.csproj` rule (**line 20**, re-verified 2026-07-22 — earlier notes said line 22) would silently untrack
  the project file. There is still no `bin/` rule and `/[Oo]bj/` remains root-anchored.
- **`tools/VoidDay.Balance/VoidDay.Balance.csproj`** (net9.0), globbing `../../Assets/Core/**/*.cs`.
  Dependencies: YamlDotNet, Newtonsoft.Json. Confirm the Core sources compile outside Unity — this is the
  load-bearing assumption of the whole tool.
- **`Unity/GuidIndex.cs`** — scan `Assets/**/*.meta` for `guid:` → `guid → path` map.
- **`Unity/AssetReader.cs`** — the 5-line YAML preprocessor (drop `%YAML`, drop `%TAG`, rewrite
  `--- !u!114 &11400000` to `---`), then YamlDotNet. Traverse from `GameConfig.asset`: `stationRoster` →
  `StationSO` → `recipes[]` + `upgrades[]` → `RecipeSO` → resource refs; plus `orderConfig`, `xpConfig`,
  `levels`, `startingResources`, and the `[Header("Gems")]` block (`startingGems`, `secondsPerGem`,
  `minGemCost`).
- **`GemConfig` in the schema**, and `LevelEntryKind.Gems` handled in level grants. Gems shipped in the game
  (`fe0e83c` / `a4b8ad2`) — these are real authored fields, not speculative ones.
- **`Unity/SceneScanner.cs`** — line-scan `Farm.unity` for `m_SourcePrefab` GUIDs, keep those resolving under
  `Assets/Prefabs/Stations/`, map prefab → `StationSO` → `stationType`, count. No YAML parser.
- **`Schema/BalanceConfig.cs`** and friends, per the spec's Data Structures section.
- **Enum mapping via the real Core enums** — `(EffectType)0 → "StationSpeed"`. JSON always carries the name.
- **CLI:** `balance read --project ../.. --out versions/baseline.json`.

## Do NOT Build This
- **Any writing.** Reading only; M2 owns the writer. Do not "just add" a quick write path — surgical patching
  is a milestone's worth of care.
- **The server or any UI** → M4. This milestone's interface is a command and a file.
- **Anything simulation-shaped** → M3. No `CoreHarness`, no player, no ticking.
- **Support for effect types with no teeth** — the reader parses whatever is authored, but don't build
  validation or affordances for `Global*` / `OrderPayout` / `OrderSlots` / `BuildCost`.

## Context
First milestone — nothing exists. Establishes:
- **New:** `tools/VoidDay.Balance/` (csproj, `Schema/`, `Unity/`, `Cli/`), `versions/baseline.json`.
- **Touched in the Unity project:** `.gitignore` only. Nothing under `Assets/`.

## Principles
- **One-way dependency** (spec, *The agnosticism rule*): nothing under `Assets/` may change. `git status` on
  `Assets/` stays clean.
- **Fail loud at the boundary** (CLAUDE.md): an unresolvable GUID, a missing referenced asset, or an enum
  value with no name throws naming the file and field. Never default-fill.
- **Verify APIs against what's installed:** confirm YamlDotNet's actual API against the package you pull,
  and confirm the Core sources compile under net9.0 before building on top of them.
- **★ Never reorder a Core enum to "tidy" it.** `LevelEntryKind.Gems` was appended rather than inserted
  precisely so existing serialized `kind:` indices stayed valid. Reordering silently reassigns every authored
  grant in `Levels.asset`, and the tool would faithfully report a different game than the one that runs.

## Definition of Done
- `dotnet run --project tools/VoidDay.Balance -- read` writes `versions/baseline.json` with no errors.
- Spot-checks match the inspector exactly: `Recipe_Field_WheatGrow.duration`, `Station_Field.cap` /
  `buildCost` / `unlockLevel`, `Upgrade_Silo_Cap` tier costs and effect amounts, all 20 level thresholds, and
  all ten `OrderConfig` fields.
- `startingStations` reads 1 Field, 1 Silo, 1 Order Board — scanned from the scene, not hardcoded.
- Enums appear as strings, not ints — including `LevelEntryKind.Gems` (**value 6**, appended after `Money`).
- The gem block reads `startingGems` 5, `secondsPerGem` 30, `minGemCost` 1.
- **Level 3's grant reads as `2 gems`, not `$150`.** Gems M01 replaced it — every level already carried a
  Money grant and a level may hold at most one reward. If the reader shows `$150` there, it is reading a
  stale asset, not a correct one.
- Blanking a referenced asset produces a loud, named error rather than a silent hole.
- `git status` shows no change under `Assets/`.

## How to Test
1. Run the read command; confirm it exits clean.
2. Open `baseline.json` beside the Unity inspector and walk the spot-check list above.
3. Search the JSON for `"type": 0` — there should be none; enum values are names.
4. Temporarily rename a `ResourceSO` referenced by a recipe, re-run, and confirm a named error.
5. `git status` — `Assets/` clean.

**Acceptance cases covered:** QA-1, and the reader half of QA-5.
