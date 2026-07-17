# Assets — Fonts

**Spec:** `docs/VoidDay-Spec-unity.md` · **Style guide:** `docs/StyleGuide.md`
**Count:** 1 family

## Format

- **Direction:** one **rounded friendly sans** — Fredoka / Baloo / Nunito family feel (StyleGuide Type & UI / Q14). Cozy-cosmic warmth, thumb-friendly on a phone.
- **Weights:** **Bold** (headers, numbers), **SemiBold** (buttons/labels), **Regular** (body/blurbs).
- **Unity delivery:** a **TextMeshPro SDF font asset** generated per family (one SDF asset covering the weights, or one per weight). SDF so text stays crisp at any zoom/DPI.
- **Italics:** VoidPet quotes render *italic in quotation marks* (spec §12.4). If the chosen family lacks a true italic, use TMP's oblique/skew fallback.

## Assets

| id | What | States/Variants | Qty | Spec | Placeholder |
|---|---|---|---|---|---|
| `font.rounded` | Primary rounded-sans UI family (→ TMP SDF asset) | Bold / SemiBold / Regular | 1 family | §12.4, StyleGuide Q14 | TMP default (LiberationSans SDF) |

## Notes

- **Final family pick is an Open item** (StyleGuide) — any of Fredoka/Baloo/Nunito satisfies the direction; confirm one before generating the SDF asset.
- No separate bitmap/pixel font needed (not a pixel-art game — StyleGuide Format).
- One family covers HUD, panels, popups, and toasts; the void accent affects color, not typeface.
