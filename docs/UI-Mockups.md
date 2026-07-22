# VoidDay — UI Mockups Manifest

**Purpose:** the bridge between the Figma mockups and the Unity build. When it's time to
implement a surface, a Claude session reads the surface's **behavior contract** from
`docs/UI-Inventory.md` and its **look/layout** from the Figma frame listed here (live, via
the Figma MCP — not a flattened export). See *How to translate* below.

**Figma file:** `X3UE3am9wbX0bKrfFOx8x0`
**URL:** https://www.figma.com/design/X3UE3am9wbX0bKrfFOx8x0
**Reference resolution:** 1080 × 1920 (portrait). Set the Unity `CanvasScaler` to *Scale With
Screen Size*, reference resolution **1080 × 1920**, match = 0.5 (or per-surface as needed).
All frame coordinates below are in this space, so RectTransform math is 1:1 after anchoring.

---

## Surface → Figma node map

Read a node with the Figma MCP: `get_design_context` / `get_metadata` / `get_screenshot`
using `fileKey` above and the node id below.

| UI-Inventory id | Figma frame | node id |
|---|---|---|
| *(HUD, composite in context)* | Screen — Gameplay HUD | `1:2` |
| `hud.*` (element states) | Sheet — HUD elements | `37:2` |
| `menu.build` | Screen — menu.build | `20:2` |
| `menu.voidPet` | Screen — menu.voidPet | `21:2` |
| `menu.debug` | Screen — menu.debug | `22:2` |
| `picker.petAssign` | Screen — picker.petAssign | `23:2` |
| **`panel.station` — CHOSEN** | Screen — panel.station **ALT (Full HayDay)** | `42:2` |
| `panel.station` — alt kept | Screen — panel.station (HayDay-simple) | `5:2` |
| `panel.orderBoard` | Screen — panel.orderBoard | `14:2` |
| `panel.workshop` | Screen — panel.workshop | `17:2` |
| **`panel.silo` — CHOSEN** | Screen — panel.silo v2 (shared pool) | `65:2` |
| `panel.silo` — superseded | Screen — panel.silo (per-resource caps) | `19:2` |
| `popup.levelUp` | Screen — popup.levelUp | `24:2` |
| `popup.hatchEgg` | Screen — popup.hatchEgg | `25:2` |
| `popup.petDetails` | Screen — popup.petDetails | `26:2` |
| `popup.totalResources` | Screen — popup.totalResources | `27:2` |
| `popup.relationshipFormed` | Screen — popup.relationshipFormed | `28:2` |
| `popup.event` | Screen — popup.event | `29:2` |
| `popup.genericText` | Screen — popup.genericText | `33:21` |
| `toast.generic` | Sheet — Toasts | `36:2` |
| `overlay.placementGhost` / `overlay.moveGhost` | Sheet — Overlays | `35:7` |
| `world.progressBar` / `readyIcon` / `storageFull` / `relationshipHeart` | Sheet — In-world UI | `34:2` |
| **`hud.gems`** (M1) | Screen — Gameplay HUD (gems) | `69:2` |
| **`popup.skipConfirm`** (M2) | Screen — popup.skipConfirm | `71:2` |
| **`panel.orderBoard` — refilling slot w/ skip** (M2) | Screen — panel.orderBoard (skip) | `69:73` |
| **`world.timerSkip`** (M3) | Sheet — world.timerSkip | `69:126` |
| **gem element states + open choices** | Sheet — gem elements | `71:88` |

### Design decision: `panel.station` uses the ALT (Full HayDay)

The chosen model is `42:2` — a **world-view**, not an all-in-one modal:
- Recipe selection is a **floating popup** near the building: recipe icon tiles → selected
  recipe shows `have/need` + timer + Queue.
- The **job queue moved out of the panel** — it renders as slots **under the building**
  (extends `world.station`).
- **Upgrades and pet assignment are NOT in this surface.** They need their own access points
  (TBD — see below).

**Consequences still to reconcile in `docs/UI-Inventory.md`** before building this surface:
1. Queue display → new in-world element on `world.station`.
2. Station upgrades → needs its own surface/opener (doesn't exist yet).
3. `picker.petAssign` → needs a new way to open it (the old panel slot is gone).

The HayDay-simple version (`5:2`) keeps all of that in one panel and is retained as a fallback
in case we switch back.

### Design decision: storage is ONE shared pool (Hay Day's silo), not per-resource caps

**2026-07-21, user decision.** `panel.silo` v2 (`65:2`) supersedes `19:2`. The spec's §7 model —
per-resource caps raised together by one global upgrade — was replaced with Hay Day's actual model:
**a single shared capacity across every good.** 40 wheat and 10 corn fill a 50-pool exactly as 50
wheat would, so hoarding any one good squeezes every other. That pressure is the mechanic.

Consequences:
1. **§7 of the spec is now wrong** and needs updating — it still says "each resource has its own cap".
2. **`storage.cap` raises one number**, not N caps. `Effect.resource` goes unused for this type.
3. The surface gained a **contents list** (what's in the silo) and a **capacity bar**, both Hay Day
   staples; the upgrade is a single `pattern.purchaseRow` track paid in **money** (Hay Day uses
   expansion materials — deliberately not copied; that is its own economy subsystem, deferred).
4. Two pools (Hay Day's Silo + Barn) were considered and **deferred** — the Barn slot is already the
   Workshop (R3 #12), and with only wheat + corn there is nothing to split. Revisit when products exist.

### Design decision: the gems currency surfaces (2026-07-22, user-approved)

Mocked before implementation per the standing "Figma before Unity scene surgery" rule. The
milestone plan in `milestones/Gems-Currency/` hand-authored these directly; that was gated and
replaced with these frames.

1. **Gems are cyan `#22D3EE`, not the void violet `#8B5CF6`.** Violet is the StyleGuide's single
   reserved void accent and is already spent on eggs, pets and hearts — a violet gem beside the
   violet `hud.eggButton` reads as egg-related. Cyan is the sanctioned cool end of the same accent.
   The rejected violet variant is kept in `Sheet — gem elements` § A.
2. **The gem glyph is a 4-point polygon, never a text `◆`.** One shape across the pill, the cost
   chip, the skip button and the in-world radial, so the real icon later drops into one slot.
3. **`popup.skipConfirm`'s primary button is gem-cyan, not the `popup.genericText` destructive red.**
   Spending a currency is not a destructive act.
4. **`popup.skipConfirm` is mocked over a live Order Board.** The board stays rendered behind the
   dim — this is the visual statement of M2's tier-2 decision that the popup does **not** publish
   `ExclusiveUiOpened`, making it the project's first non-exclusive surface.
5. **Copy is "Skip the wait?" + a cost chip**, not the milestone doc's literal `"Skip for ◆2?"`.
   The cost carries more weight as a chip than inline in the title.

**⚠️ The HUD reflow this forced.** M1 specifies the gem pill at anchor/pivot `(1,1)`,
`anchoredPosition (-24, -144)` and says "Reflow nothing else." That is not achievable:
**`hud.eggButton` already occupies y=176–298 on the right edge**, which is exactly that slot. The
approved resolution moves the egg button **down 116px**, so the right edge stacks
**money → gems → egg**. Implementations must do the same rather than overlap them.

---

## How to translate a surface into Unity

1. **Auth Unity MCP first** — it currently returns `401`. Re-authenticate (`unity-initial-setup`
   / the `authenticate` flow) before any editor work.
2. **Read the contract** — the surface's entry in `docs/UI-Inventory.md`: its `input:*` intents,
   the Core state / domain events it reads, and its states.
3. **Read the look** — `get_design_context` on the node id above for structure + measurements;
   `get_screenshot` for a visual reference.
4. **Build data-driven, not hardcoded** — colors/sizes → a **UI theme SO** (StyleGuide roles,
   `docs/StyleGuide.md`); labels/costs/timers → the gameplay SOs. Never inline a mockup value.
5. **Build clean UGUI** — Canvas per context (screen-space HUD; a prefab per panel/popup).
   Use anchors + LayoutGroups; do **not** transcribe absolute Figma x/y as fixed positions.
   World-space UI (progress bar, ready icon, hearts, queue slots) billboards to the camera.
6. **Keep the layer boundary** — the View emits intents and renders from Core state; it holds
   no rule (see `CLAUDE.md`). The mockup informs layout only.
7. **Verify in Play** — one surface at a time.

## Notes

- The mockups are a **reference**, not a literal export target. Auto-generated UGUI from a
  flattened design is brittle; read for intent, then hand-build.
- Fonts in the mockups use **Nunito** (ExtraBold / SemiBold / Regular) as a stand-in for the
  StyleGuide's rounded-sans (Fredoka / Baloo / Nunito family). Swap the final family in the
  theme SO.
- Icons in the mockups are **colored-shape placeholders** per the placeholder policy — real
  icons are a later asset pass, dropped into the same SO slots.
