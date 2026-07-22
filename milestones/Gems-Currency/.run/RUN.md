# Gems Currency — Run 2026-07-22

**Range:** 01–03 · **Status:** complete
**Estimate:** 45–80 min wall-clock · 300k–550k tokens · confidence medium-high

## Resolved up front

- **Gem pill placement / egg-button reflow** → There are no eggs in the current project; `hud.eggButton` does not exist. Ignore the "move `hud.eggButton` down 116px" line in the approved deviation entirely — it is not relevant. Place the gem pill at anchor/pivot `(1,1)`, `anchoredPosition (-24,-144)`, size `240×96` exactly as `01-the-currency.md` specifies, and reflow nothing. Do not halt over the missing egg button.
- **Gem colours** → Add a `gemAccent` field to `UiThemeSO` for the gem-cyan `#22D3EE`. Reuse the existing `lockedBg` / `lockedText` for the unaffordable state rather than adding near-duplicate colours from the mockup (`#D6CDBB` / `#A89E8C`). Author static cyan chrome in the prefab.
- **Gem icon** → No gem sprite exists on disk. A placeholder is authorised (M1 says so explicitly). Do not stop to commission art. The mockup asks for a 4-point polygon rather than a text `◆` — use a simple authored placeholder; do not run the art pipeline.
- **Stale doc notes** → `docs/UI-Mockups.md` claims the Unity MCP "returns 401"; that is stale, the MCP works. Figma node `34:2` is stale for M3 — use `69:126`.

| # | Milestone | Status | Commit | Notes |
|---|-----------|--------|--------|-------|
| 01 | The Currency | complete | fe0e83c | GemPurse + events + gem pill + cheat; EditMode 83 → 93 green |
| 02 | Skip Any Timer | complete | a4b8ad2 | Core TimeSkip rule + 3 owner hooks + 3 events + stacking confirm popup |
| 03 | In-World Skip | complete | 1822352 | Job + construction radials priced and tappable; EditMode 106/106 green |

## Flags raised (for end-of-run review)

### M01
- **Level 3's $150 Money grant was REPLACED by a 2-gem Gems grant** in `Levels.asset`. Every level 2..20 already had a Money grant, and M1's own widened rule allows at most one reward (Money OR Gems) per level, so adding was impossible. The curve now pays $150 less overall; gems enter play at level 3.
- `LevelEntryKind.Gems` appended after Money (value 6), not inserted — existing serialized `kind:` indices stay valid. Reordering that enum later would silently rewrite every authored grant.
- `Producer` owns the gem debug cheat AND the gem reset (`DebugAddMoneyRequested` lives on `OrderBoardSystem` instead). Gems have no owning system yet. If a later milestone gives gems a real owner, the cheat should move with it.
- `Progression`'s ctor gained a `GemPurse` (6th arg, before `gated`); `Producer.Init` gained `(GemPurse gems, int startingGems)` mid-signature. Both test fixtures fixed; any new construction site must supply them.
- A level may now hold at most ONE reward counting Money+Gems together; authoring both is a hard boot failure. Lifting it requires `LevelUpPopup.BuildReward` to render the whole rewards list, not `rewards[0]`.
- The egg-button reflow was NOT applied (per the pre-flight answer); deferral recorded in `LOG.md`.

### M02
- **`TimeSkipSystem` is now the gems' owning system.** It took BOTH the `DebugAddGemsRequested` cheat and the `DebugResetRequested` purse reset off `Producer` — so `Producer.Init` loses M01's `(GemPurse, int startingGems)` params and `Producer` no longer references gems at all. (This supersedes the M01 flag about Producer owning the cheat.)
- **`SkipConfirmPopup` does NOT publish `ExclusiveUiOpened`** (the project's first non-exclusive/stacking surface) but DOES subscribe `ExclusiveUiClosed → Close`, so it can never outlive the surface it stacks on. That publish/listen asymmetry is the pattern future stacking popups should copy.
- All three timer reads use a `-1` "no live timer here" sentinel (`HeadSecondsRemaining` / `SiteSecondsRemaining` / the OrderRefill branch) — that is what lets one pricing rule serve three unrelated owners.
- The popup's subtitle copy ("Order slot refills instantly.") is authored scene text with **no serialized field** — M3 skipping a job will show that sentence unless it is made per-kind.

### M03
- **`GameBoot`:** moved `constructionSiteView.Init` DOWN into the Init block rather than hoisting OrderBoard construction — it now depends on `TimeSkip`. Nothing publishes construction events between the old and new call sites.
- **`SkipConfirmPopup` subtitle is now per-`TimerKind`:** a `subtitleText` field + `jobSubtitle`/`constructionSubtitle`/`orderRefillSubtitle` strings, bound on open. A 4th timer kind must add a string or `SubtitleFor` throws (deliberately loud). (Resolves M02's flag.)
- **`TimerWidget.SetCost(gems <= 0)` disables `tapCollider`** as well as hiding the cost row — "priced" and "tappable" are the same state by construction, so a visible-but-unpriceable radial can't swallow a tap. The milestone doc assumed no enable/disable logic was needed.
- **`StationStateWidget.prefab` needed NO edit** — its nested Timer is a real prefab instance of `TimerWidget.prefab`, so the collider and cost row propagated. `TimerWidget.prefab` is the single authoring point for both radials.

## Environment findings (carry to later milestones)

- **Playmode is frozen at `frameCount = 1`.** The editor cannot be foregrounded and `Application.runInBackground = true` (confirmed True inside playmode) does NOT help. `screenshot-game-view` returns frame 1 forever — it shows a stale HUD while looking live. Cross-check state via `script-execute` instead. For a real post-frame-1 shot: temporarily flip the overlay canvases to `ScreenSpaceCamera` in playmode and use `screenshot-camera` (playmode-only, discarded on exit; verify scene `IsDirty=false` after).
- `ProjectSettings/ProjectSettings.asset` is dirty and deliberately uncommitted — DOTween writes a `DOTWEEN` scripting define on every AssetDatabase refresh, plus a `runInBackground` flip. Mixed provenance; leave it alone.
- The pre-existing staged rename `plans/balance-tool.md → docs/BalanceTool-Spec.md` is still in the index, untouched. Leave it.
