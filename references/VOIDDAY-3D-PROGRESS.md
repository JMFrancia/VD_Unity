# VoidDay — Asset Generation Progress & Handoff

_Working log for the Asset Bot ⇄ Meshy/Nano Banana pipeline. Resume from here._

## ⚠️ DECISION CHANGE — PETS ARE NOW 2D BILLBOARDS (2026-07-17)
**This supersedes the 3D creature track below.** After reviewing the flat IP art vs. the AI concepts, the user chose to render **VoidPets as 2D billboards** (the original flat art, background-removed, on camera-facing quads under the ¾ ortho camera — 2.5D, à la Don't Starve). Rationale: the originals are flat graphic silhouettes; the one direct 3D bake (`pet-joy-textured`) proved they reconstruct into garbage, and billboards preserve the IP exactly. Spec updated accordingly (`docs/VoidDay-Spec-unity.md` §2, §10.3, §12.6, §12.8, §14).

Consequences:
- The **6 creature GLBs + `pet-joy-v2.glb`** (Recipe A, "user-approved" below) are **no longer the pet pipeline.** Kept on disk, not deleted — do **not** continue/decimate/bridge them for pets unless the user reverses this.
- **Pet asset pipeline is now:** background-remove the flat `pet-<species>.png` → transparent sprite → billboard. Tooling: `tools/asset-prep/cutout.py` (local color-key, needs a py venv w/ Pillow+numpy) → outputs `references/voidpet-ip/cutouts/<species>-cut.png`. Previews via `tools/asset-prep/make_previews.py`; wired into `_view.html` ("VoidPet billboards" section).
- **Cringe** cutout still has a coil-interior artifact — needs a quick cleanup before final.
- **Stations are unaffected — they remain 3D** (see the station line in TODO). Station generation is the active next task.

## Setup (done)
- Asset Bot initialized in this repo (`.asset-bot/`). Keys in `.env` (gitignored). Both authenticate.
- **2D = Nano Banana** (Google GenAI). Billing is ON (free tier was blocked at first). Works.
- **3D = Meshy via the official Meshy MCP server** (`@meshy-ai/meshy-mcp-server`, user scope). Asset Bot's own `generate 3d` CANNOT use the Meshy key (it routes through fal/Tripo) — so all 3D goes through the MCP tools (`meshy_image_to_3d`, `meshy_text_to_3d`, `meshy_retexture`, etc.).
- Meshy balance: ~**977 credits** remaining (started 1052).

## HARD RULES
- **All 3D models must be LOW POLY** (WebGL target). Use `model_type:"lowpoly"` on every Meshy call; verify triangle count. Henhouse=8k ✓, Joy v2=19k (can decimate further).
- Prompts literal; **style from reference images, not text**; void accent (`#8B5CF6`) only on void things.

## THE WORKING 3D RECIPE (proven)
**Concept-first is mandatory** — raw flat IP silhouettes reconstruct into garbage (Joy v1 = foil blob). Do:
1. **Nano Banana** → shaded, dimensional 3/4 hero render (real volume + StyleGuide palette). For creatures, feed the flat `pet-*.png` as `--refs` (absolute path!) so shape/identity is preserved.
2. **Meshy `image_to_3d`**, `model_type:"lowpoly"`, `target_formats:["glb"]`.
   - **Textured** (`should_texture:true`) = **30 credits**, but color came back WASHED OUT/pale (Meshy `remove_lighting` default strips the near-black). 
   - **Untextured** (`should_texture:false`) = **5 credits**, and matches StyleGuide (creatures are near-black tint + emissive, material applied in Unity). **Recommended for creatures.**

### Cost facts (corrected — verified 2026-07-17)
- `image_to_3d` price is set by **model tier**, NOT by texturing: **meshy-6/latest = 20 cr, meshy-5 = 5 cr** (base). Texturing on meshy-6 added ~10 (Joy v2 textured = 30 cr; the 6 untextured meshy-6 creatures = 20 cr each). The earlier "untextured = 5 cr" was an untested estimate — real untextured meshy-6 = **20 cr**.
- `retexture` = 10 cr · `text_to_3d` preview = 5–20, +refine 10.
- Nano Banana image ≈ $0.02–0.04 each.
- This batch: 6 concepts (NB, ~$0.18) + 6× meshy-6 untextured = **120 cr**. Balance ~977 → ~857.

## Species (7 chosen, IP sheet cropped)
Cringe, Grumpy, Merry, Apathy, Wonder, Conviction, Joy.
Individual flat refs: `references/voidpet-ip/pet-<species>.png` (+ `voidpets-sheet.png`).

## Assets generated so far
| Asset | File | Status |
|---|---|---|
| Wheat icon ×2 | `references/2d-review/icon-wheat-0{1,2}.png` | test, has glow-halo artifact |
| Egg icon | `references/2d-review/icon-egg-01.png` | test |
| Henhouse concept (2D) | `references/2d-review/concept-henhouse.png` | ✅ good |
| Henhouse 3D | `references/stations/station-henhouse.glb` (+`_base_color.png`) | ✅ **keeper**, 8k tris, textured. Task `019f717c` |
| Joy concept (2D) | `references/2d-review/concept-joy.png` | ✅ good |
| Joy v2 3D | `references/voidpet-ip/pet-joy-v2.glb` (+`_base_color.png`) | ✅ good geometry, 19k tris, color washed out. Task `019f7185` |
| Joy v1 3D | `references/voidpet-ip/pet-joy-textured.glb` | ❌ bad (flat-silhouette proof) |
| Cringe 3D | `references/voidpet-ip/pet-cringe.glb` (concept `concept-cringe.png`) | ✅ **15,737 tris**, untextured. Task `019f71a7-92d0` |
| Grumpy 3D | `references/voidpet-ip/pet-grumpy.glb` (concept `concept-grumpy.png`) | ✅ **16,908 tris**, untextured. Task `019f71a7-9ff3` |
| Merry 3D | `references/voidpet-ip/pet-merry.glb` (concept `concept-merry.png`) | ✅ **18,686 tris**, untextured. Task `019f71a7-a80f` (ran slow, finished fine) |
| Apathy 3D | `references/voidpet-ip/pet-apathy.glb` (concept `concept-apathy.png`) | ✅ **16,820 tris**, untextured. Task `019f71a7-b3d9` |
| Wonder 3D | `references/voidpet-ip/pet-wonder.glb` (concept `concept-wonder.png`) | ✅ **16,130 tris**, untextured. Task `019f71a7-bcea` |
| Conviction 3D | `references/voidpet-ip/pet-conviction.glb` (concept `concept-conviction.png`) | ✅ **9,234 tris**, untextured. Task `019f71a7-c55d` |
| Henhouse (imported) | Asset Bot record `henhouse` (stations) + `Assets/Art/Models/Stations/station-henhouse.glb` | ✅ **imported** to both |
| Field 3D | `references/stations/station-field.glb` (concept `concept-field.png`) | ✅ **18,719 tris**, textured. Task `019f7313-1230`. Heavy (dense sprouts) — decimate candidate |
| Pasture 3D | `references/stations/station-pasture.glb` (concept `concept-pasture.png`) | ✅ **5,223 tris**, textured. Task `019f7313-1d40` |
| Creamery 3D | `references/stations/station-creamery.glb` (concept `concept-creamery.png`) | ✅ **8,104 tris**, textured. Task `019f7313-260d` |
| Bakery 3D | `references/stations/station-bakery.glb` (concept `concept-bakery.png`) | ✅ **8,121 tris**, textured. Task `019f7313-2f91` |
| Order Board 3D | `references/stations/station-orderboard.glb` (concept `concept-orderboard.png`) | ✅ **9,689 tris**, textured. Task `019f7313-381e` |
| Silo 3D | `references/stations/station-silo.glb` (concept `concept-silo.png`) | ✅ **19,984 tris**, textured. Task `019f7313-43f5`. Heavy (lumpy clay surface) — decimate candidate |
| Workshop 3D | `references/stations/station-workshop.glb` (concept `concept-workshop.png`) | ✅ **7,693 tris**, textured. Task `019f7313-4c88` |
| Pet billboards ×7 | `references/voidpet-ip/cutouts/<species>-cut.png` (apathy, conviction, cringe, grumpy, joy, merry, wonder) | ✅ **current pet pipeline** (2D billboards). Cringe needs coil-interior cleanup. Low-res (~135px) — final wants higher-res source |

## Preview / gallery
Rotatable viewer (relaunch after refresh):
```bash
cd "/Users/joefrancia/Desktop/VoidPet/VD_Unity/references" && python3 -m http.server 8788
# open http://localhost:8788/_view.html   (3D models + 2D images)
```
GLB tri-counter: `node <scratchpad>/glbtris.cjs <file.glb>` (or reuse the pattern — parses GLB JSON chunk).
Meshy render thumbnails: `GET https://api.meshy.ai/openapi/v1/image-to-3d/<task_id>` → `thumbnail_url`.

## CREATURE RECIPE — LOCKED ✅ (Recipe A, user-approved)
**Untextured lowpoly + Unity void-material.** For each of the remaining 6 species:
1. Nano Banana shaded 3/4 concept from the flat `pet-<species>.png` (absolute `--refs`), StyleGuide palette.
2. `meshy_image_to_3d`, `model_type:"lowpoly"`, `should_texture:false`, `target_formats:["glb"]` — **5 cr each (~30 cr total)**.
3. Color/emissive comes from the near-black + `#8B5CF6` emissive material applied in Unity (not baked).
Rejected alternatives: B multiview (more cost, only if silhouette fidelity proves inadequate), C textured+remove_lighting:false (color washed out on Joy v2).

## TODO
- [~] ~~Lock creature recipe — A (untextured lowpoly)~~ **SUPERSEDED 2026-07-17: pets are 2D billboards (see banner at top).** 3D creature GLBs kept but not the pipeline.
- [x] Batch remaining 6 creatures with Recipe A — done, but **now superseded by billboards** (GLBs retained on disk, unused for pets).
- [ ] **Pet billboards:** clean up Cringe coil interior; consider higher-res source art; import the 7 cutouts into Unity as sprites when building the pet view.
- [x] Henhouse imported → Asset Bot record `henhouse` (stations) + copied to Unity `Assets/Art/Models/Stations/`.
- [x] Station line: 7 more stations (field, pasture, creamery, bakery, orderBoard, silo, workshop) via concept-first + lowpoly. **DONE 2026-07-17** — all textured, in `references/stations/`. Concept renders in `references/2d-review/concept-<name>.png`. Meshy 857→647 (210 cr, 30 ea). **Not yet imported to Unity or bridged to Asset Bot records** (henhouse pattern) — next step.
- [ ] Import the 7 station GLBs → `Assets/Art/Models/Stations/` + Asset Bot `stations` records (like henhouse); wire mesh refs onto StationSOs when they exist.
- [ ] Decide whether to decimate field (18.7k) + silo (20k) toward ~8k tris (others already 5–10k). Others: pasture 5.2k, workshop 7.7k, creamery/bakery 8.1k, orderboard 9.7k.
- [ ] Decide whether to decimate creatures toward ~8k tris.
- [ ] (UI 2D handled via Figma plugin, not Asset Bot — per user.)
- [ ] After user approves creatures in gallery: bridge the 6 creature GLBs into Asset Bot records (`assets import --category creatures`), like henhouse was. (Henhouse already bridged.)

## Notes / quirks
- Nano Banana adds an unwanted **glow-ring/halo** on icons; add negative "no glow, no ring, no border" if generating icons.
- `generate image --refs` needs an **ABSOLUTE** ref path (project-relative resolves under `.asset-bot/`).
- Background `python3 -m http.server` viewers do NOT survive session exit — relaunch as above.
