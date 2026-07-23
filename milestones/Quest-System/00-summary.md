# Quest System — Milestone Plan

**Design record:** `plans/quest-system.md` (reasoning; this plan supersedes it where they
disagree).
**Generated:** 2026-07-23
**Mode:** prototype (`/design_feature --prototype`).

## Milestones
| # | Name | Playable outcome | Doc |
|---|------|------------------|-----|
| 1 | Quest Engine + Quest Menu | Meet conditions → quest granted; do the goal → progress bar fills (never backward); open menu (button under debug button), tap a ready quest → collect XP+resources with particles. Full loop. | `01-engine-and-menu.md` |
| 2 | New-Quest Toast | On grant, a toast slides in with the quest description (reuses `ToastController`). | `02-new-quest-toast.md` |
| 3 | Progress Pill | On progress, a pill drops from behind the XP bar (bar animates up), retracts; on completion stays ≥20s flashing green (tap=collect); if untapped, retracts and the quest-menu button flashes green until all collected. | `03-progress-pill.md` |
| 4 | Quests in the Headless Sim | CLI `sim`/`eval` runs the real quest rules (shared Core compile), reports quest completions + reward income, and quest scalar knobs are movable. | `04-quests-in-sim.md` |
| 5 | Quest Authoring in the Tool + `balance_game` | Create/reorder/delete quests in a session; `write --apply` writes `QuestSO` assets back to Unity; the skill knows how to drive quests. | `05-quest-authoring-tool.md` |

M1–M3 are in-editor playable. **M4–M5 are offline-tool milestones** verified via the CLI
(and, for M5, a follow-up Unity playtest of the exported asset) — a deliberate deviation from
"playable in the editor," because the user explicitly scoped the balance tool into this
feature. See *Decisions Made*.

## Production Order
| Milestone | Assets | UI mockups (Figma-first) | Notes |
|-----------|--------|--------------------------|-------|
| 1 | Quest-menu button icon `[placeholder OK]`; quest row/panel chrome `[placeholder OK]`; reuses `EarnParticle.prefab` | Figma frames `01 · Quest Menu` + `02 · Quest Button (normal)` — **approved** | Ship 2–3 example `Quest_*.asset` |
| 2 | Quest toast icon `[placeholder OK]` | Figma frame `03 · New-Quest Toast` — **approved** | View-only |
| 3 | Pill chrome/bar `[placeholder OK]`; reuses `EarnParticle.prefab` | Figma frame `04 · Progress Pill` (progress + completion) + `02` flashing-green button — **approved** | Highest-risk UI (animation) |
| 4 | none | none | Tool-side |
| 5 | none (tool *creates* quest assets) | none | Tool-side + skill |

**UI mockups — DONE & APPROVED (2026-07-23):**
[VoidDay — Quest System UI](https://www.figma.com/design/oNpLZGKUGhyd07cxCAKQB4) — frames
`01 · Quest Menu`, `02 · Quest Button` (normal + flashing-green), `03 · New-Quest Toast`,
`04 · Progress Pill` (progress + completion states). Styled to the live HUD (cream panels,
Baloo 2, green progress bars, purple debug button; quest button = gold checklist icon).
The Figma-first gate for M1–M3 is satisfied — build against these frames.

**Critical path:** the Figma-first gate is now cleared (mockups approved). Everything else
uses placeholders. No art blocks a milestone.

## Decisions Made
- **Quest rules in `Assets/Core` (pure C#), not `Assets/Systems`.** Required by the Core
  boundary rule *and* it makes the balance sim run the real quest logic for free (the tool
  globs `Assets/Core/**/*.cs` into its own compile). This is the load-bearing decision. See
  `plans/quest-system.md` for the rejected alternative (logic in a Systems MonoBehaviour).
- **Data via enum-discriminator flat structs, not `[SerializeReference]`.** Matches the
  project's established `Effect`/`Condition`/`LevelGrant` pattern; `[SerializeReference]` is
  used nowhere in the codebase.
- **Unity SOs are the source of truth; the tool reads and writes them back** (user decision).
  The tool never becomes the authoring origin — the inspector stays the tuning UI.
- **M4/M5 verified via CLI, not Unity Play** (user scoped tool work in). Framed as the tool's
  equivalent of "playable"; M5 adds a Unity playtest of the exported asset as its real gate.
- **Progress measured from grant-time baseline + running max** (see Gotchas) — the mechanism
  that enforces the spec's "progress can never go backward."

## Assumptions
Each is a risk; what breaks if wrong:
- **"Reorder" means reordering the `GameConfigSO.quests` list position** (confirmed by user
  2026-07-23) — not steps within a quest or a first-class chain sequence.
- **No save system exists**, so quest completion state is in-memory and resets on reload. If a
  save system exists/arrives, quests need to persist granted/completed/collected sets. →
  Would add a persistence task.
- **The pill shows one quest at a time (newest-progress-wins).** If simultaneous multi-quest
  progress needs individual pills, M3 grows a queue. → M3 rework only.
- **Quest goal/condition kinds are limited to what the example quests exercise**, added on
  demand. If a broad kind set is wanted up front, M1 grows. → M1 scope only.
- **`GameBootParityTests` canary covers the `CoreHarness`↔`GameBoot` mirror** and will flag if
  `QuestLog` construction drifts. If it doesn't cover the new construction, M4 needs a manual
  reconcile note. → M4 verification step catches this.

## Gotchas
- **Progress monotonicity is not free.** "Earn $500" must snapshot a baseline at grant time
  and track the running max of (current − baseline), because `MoneyChanged` fires on *spends*
  too (negative `Delta`) and lifetime totals include pre-grant activity. Naively binding
  progress to a live counter will make bars jump and regress. This lives in `QuestLog` (M1).
- **"Money earned" has no dedicated event** — it's `MoneyChanged{Delta,Total}`. Filter
  `Delta>0` for earn-goals (or correlate with `OrderFulfilled.Payout`). Same shape for gems.
- **"Crop harvested" = `JobCollected`** (has an `Outputs` list + a `ByPet` flag). "Silo
  upgraded" and "field built" are `UpgradePurchased` / `StationBuilt` — there's no
  silo-specific event.
- **Enums are serialized by integer index in several places** (`LevelEntryKind`, effect
  enums). New quest enums must be **append-only**; never reorder.
- **The tool shares `Assets/Core` by compile-glob, not by mirroring.** A single stray
  `using UnityEngine` in quest Core code breaks the tool build. If M4 can't compile quest
  logic, fix the Core boundary in M1 — do not mirror the logic in the tool.
- **`CoreHarness` mirrors `GameBoot` and also hand-mirrors the two Systems-layer behaviors**
  (`ProgressionSystem`, `UpgradesSystem`) that live outside Core. Keeping `QuestLog` pure Core
  means the harness only needs to *construct* it — no behavior mirror. Keep it that way.
- **`AssetWriter` refuses list add/remove/reorder everywhere except recipe insertion.** M5's
  create/reorder/delete is genuinely new write territory; budget for it and reuse the
  `InsertRecipe` + grant-block-regeneration templates. Never switch to full reserialization.
- **`AGENTS.md` is the contract the `balance_game` skill defers to** — the skill is forbidden
  from inventing verbs/metrics. Every M4/M5 quest capability must be documented there in the
  same change, or the skill silently can't use it.
- **The sim ignores UI/boot rules by design** — toasts, the 20s pill window, and the menu
  don't exist in a sim run. Don't expect (or model) them there; always playtest exported
  quests in Unity (M5 gate).

## Open Items
- ~~**Definition of "reorder"**~~ — **RESOLVED (2026-07-23):** "reorder" means reordering the
  `GameConfigSO.quests` list position. M5 builds against that; no other interpretation.

## Deferred
Not in any milestone; do not build early:
- Cross-session persistence / save integration for quest state.
- A quest debug menu / dev tooling (prototype mode adds none).
- Multi-pill stacking for simultaneous progress.
- A GUI authoring surface in the balance workbench (CLI + skill only).
- Quest goal/condition kinds beyond what example quests need.

## Testing
No TDD/coverage gate (prototype). M1–M3 verify by pressing Play (each doc's *How to Test*).
M4–M5 verify via the CLI, and M5 additionally by opening Unity and playing an exported quest.
The one place tests matter (per CLAUDE.md — the pure-C# economy core): `QuestLog`'s progress
math (baseline snapshot + monotonic max, condition evaluation, reward application) is pure
Core with no Unity deps, so a small EditMode test there is cheap and worth it. The tool's
`GameBootParityTests` canary must stay green after M4 adds `QuestLog` to the harness.
