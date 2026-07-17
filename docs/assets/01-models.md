# Assets — Models (3D) & Materials

**Spec:** `docs/VoidDay-Spec-unity.md` · **Style guide:** `docs/StyleGuide.md`
**Count:** 17 meshes · 13 materials

## Format

- **Delivery:** every mesh ships as a **prefab / mesh + material** referenced by the entity's SO (`StationSO`, `VoidPetSpeciesSO`, `ResourceSO` — spec §14). Never a sprite/texture path. A real asset drops into the exact SO slot the placeholder occupies (spec §12.8).
- **Scale:** **1 Unity unit = 1 grid cell** (StyleGuide Format). Station mesh ≈ **0.9 unit** (leaves a gutter); VoidPet mesh ≈ **0.5–0.7 unit** tall, sits on top of its station (spec §10.3); must read at min zoom under the ¾ ~55–60° camera (spec §12.5).
- **Rendering:** URP **toon-ish lit**, no outlines, **untextured** — color via material tint / vertex color (StyleGuide Technique). Bloom (URP Volume) catches emissive void elements only.
- **Rigs / animations:** **none for the prototype.** Idle bob/breathe and ready-hop are **View-layer tweens**, not skeletal animation (StyleGuide Motion). Skeletal rigs deferred.
- **World-space UI** (progress bar, ready icon, hearts) is **not** a mesh — see `02-ui.md`; it billboards to the camera (spec §12.6).

## Assets — Stations

One primitive silhouette per station type, tinted by `placeholderColor` (spec §12.6, §4.2). Real meshes swap in per §12.8.

| id | What | States/Variants | Qty | Spec | Placeholder |
|---|---|---|---|---|---|
| `mesh.station.field` | Field (wheat/corn) | idle only¹ | 1 | §4.2 | flat quad |
| `mesh.station.henhouse` | Henhouse (eggs) | idle only¹ | 1 | §4.2 | cube |
| `mesh.station.pasture` | Pasture (milk) | idle only¹ | 1 | §4.2 | primitive TBD² |
| `mesh.station.creamery` | Creamery (cream/cheese) | idle only¹ | 1 | §4.2 | primitive TBD² |
| `mesh.station.bakery` | Bakery (breads) | idle only¹ | 1 | §4.2 | primitive TBD² |
| `mesh.station.orderBoard` | Order Board (sell) | idle only¹ | 1 | §4.2, §6 | primitive TBD² |
| `mesh.station.silo` | Silo (cap upgrades) | idle only¹ | 1 | §4.2, §7 | cylinder |
| `mesh.station.workshop` | Workshop (universal upgrades) | idle only¹ | 1 | §4.2, §8 | primitive TBD² |

¹ **Station state visuals do NOT multiply the mesh.** Working / ready / storage-full / ghost are shared overlays + tints from `02-ui.md` and `03-vfx.md` driven by the View layer (spec §12.6). One mesh per station, not one-per-state.
² Style guide leaves Pasture/Creamery/Bakery/Workshop/Order Board primitives TBD — pick distinct silhouettes so eight stations don't read as identical cubes (spec §12.6).

## Assets — VoidPets

Existing IP, converted 2D→3D (StyleGuide References). Silhouette-first near-black body, indigo sheen, emissive glowing eyes. Portrait for menu/details is a **camera render of this mesh** (decided) — no separate portrait asset.

| id | What | States/Variants | Qty | Spec | Placeholder |
|---|---|---|---|---|---|
| `mesh.pet.<species>` | VoidPet creature mesh, 1 per species | 6 species (from IP) | 6 | §10.2 | dark tinted capsule + 1 emissive eye |
| `mesh.egg` | Void egg (hatch popup + on-grant) | 1 design³ | 1 | §10.1, §12.4 | small emissive ovoid/sphere |

³ Egg is one design (decided). Rarity-tinted eggs would be +2; left as an Open item, not produced.

**Species selection is an Open item** — the IP sheet has ~15+ emotion-named creatures (Grumpy, Curious, Determination, Joy…); the prototype needs **6** (spec §10.2). Which 6 blocks this whole line. Ids should be the species' data key, e.g. `mesh.pet.determination`.

## Assets — Environment

| id | What | States/Variants | Qty | Spec | Placeholder |
|---|---|---|---|---|---|
| `mesh.env.ground` | Farm ground plane on XZ (~20×30 grid) | 1 | 1 | §4.1, §12.5 | tinted Unity Plane |
| `mesh.env.backdrop` | Void backdrop / soft violet star-field around the island | 1 | 1 | §12.5, StyleGuide Color | skybox material or tinted quad |

Grid/cell placement visuals (cell highlight lines) are a View overlay, not a mesh — see `02-ui.md`.

## Materials

Placeholder policy is material-driven (spec §12.6). All tunable hex from StyleGuide Color.

| id | What | Qty | Notes |
|---|---|---|---|
| `mat.station.<type>` | Per-station `placeholderColor` tint, URP lit | 8 | Warm naturalistic-by-function (StyleGuide Q9 table) |
| `mat.pet.void` | Near-black body `#12121C` + slate planes | 1 | Shared across species; per-species tint later |
| `mat.pet.eyeGlow` | Emissive void accent `#8B5CF6` (eyes/rim) | 1 | Bloom target |
| `mat.ghost.valid` | Translucent green `#5FD35F` placement ghost | 1 | Spec §12.2 |
| `mat.ghost.invalid` | Translucent red `#D9534F` placement ghost | 1 | Spec §12.2 |
| `mat.env.ground` | Warm grass `#7DBE5A` | 1 | StyleGuide Color |
| `mat.env.skybox` | Soft indigo void `#1A1430`→`#2A2048` | 1 | StyleGuide Color |

## Notes

- **Textures are deliberately out of scope** for the prototype (untextured, tint-only per StyleGuide). No albedo/normal/roughness maps enumerated. When real IP art arrives it may bring textures — that's an SO material swap, not a schema change.
- The **ghost** reuses the station mesh with a ghost material — not a separate mesh (spec §12.2).
- **VoidPet Station** mesh is **deferred** (spec §16) — see `00-summary.md` Deferred.
