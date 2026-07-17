# VoidDay (Unity 3D) — Milestone Plan

**Spec:** `docs/VoidDay-Spec-unity.md`
**Generated:** 2026-07-17
**Catalogs used (naming authority):** `docs/assets/` (asset ids), `docs/UI-Inventory.md` (surface ids) — both present; ids reconciled, none invented here.

Smallest playable artifact first, then layer over layer. Every milestone is demonstrable in a browser by someone who didn't build it. Infrastructure never gets its own milestone — it rides inside the first one that visibly needs it.

## Milestones
| # | Name | Playable outcome | Doc |
|---|---|---|---|
| 1 | World & Camera | Pan around a rendered farm — grid + pre-placed Field/Silo/Order Board under the ¾ ortho camera. | `01-world-and-camera.md` |
| 2 | Station Loop | Tap the Field, queue a recipe, watch the timer, tap to collect; a full queue blocks on uncollected output. | `02-station-loop.md` |
| 3 | Order Board | Fulfill a corn order for your first cash; skip refills a slot after a timer. (XP accrues invisibly for M8.) | `03-order-board.md` |
| 4 | Build & Manage Stations | Drag a station onto a valid cell for money, demolish for refund, long-press to move. | `04-build-and-manage-stations.md` |
| 5 | Station Upgrades & the Effect System | Buy a station upgrade and watch the next job resolve faster/higher — described in plain language. | `05-station-upgrades-and-effect-system.md` |
| 6 | Workshop & Universal Upgrades | Build a Workshop, buy a global/payout/slot upgrade, see it apply everywhere. | `06-workshop-and-universal-upgrades.md` |
| 7 | Storage & Silo | Fill a resource to cap, watch the station block storage-full, buy a Silo upgrade to unblock. | `07-storage-and-silo.md` |
| 8 | Levels & Unlocks | Cross an XP threshold → level-up popup, build-menu unlocks, caps/slots rise, gated upgrades open. | `08-levels-and-unlocks.md` |
| 9 | VoidPets: Hatch, Assign, Auto-Collect | Hatch an egg, assign the pet, watch it auto-collect and unblock; its trait modifies the station. | `09-voidpets-hatch-assign-autocollect.md` |
| 10 | VoidPet Relationships | Two pets in range form a friendship that grants each a local bonus while near. | `10-voidpet-relationships.md` |
| 11 | World Events | At level 5+, Dopamine Rain (+25% global speed, 2 min) fires with a first-time popup then toasts. | `11-world-events.md` |

## Production Order
The scheduling view for whoever makes art/mockups. The catalogs remain the authority for what each id *is*; this only says when it's wanted. Everything has a placeholder, so production is incremental — real assets swap into the same SO slot with no code change (§12.8).

| Milestone | Asset ids | UI mockups | Notes |
|---|---|---|---|
| M1 | `mesh.station.field/silo/orderBoard`, `mesh.env.ground/backdrop`, `mat.station.field/silo/orderBoard`, `mat.env.ground/skybox` | `world.playfield` (no chrome) | All placeholder primitives. |
| M2 | `icon.resource.wheat/corn`, `icon.money`, `icon.hud.debug`, `icon.ready`, `ui.panel/button/card/bar.progress`; SFX `job.*`,`ui.*`; VFX `collectPop`,`readySparkle` | `panel.station` (recipe+queue), `hud.money`, `hud.debugButton`, `menu.debug`, `popup.totalResources`, `world.progressBar`, `world.readyIcon` | Resource icons appear first here; earliest-needed UI. |
| M3 | remaining `icon.resource.*`, `icon.close`; SFX `order.*`,`xp.gain` | `panel.orderBoard`, `hud.money` (funded) | `pattern.orderCard`. Level/XP HUD deferred to M8 (XP accrues invisibly). |
| M4 | `mesh.station.henhouse/pasture/creamery/bakery/workshop` + `mat.*`, `mat.ghost.valid/invalid`, `icon.hud.build`, `icon.lock`; SFX `station.place/move/demolish`; VFX `placementPoof` | `menu.build`, `overlay.placementGhost`, `overlay.moveGhost` | Buildable-type meshes land here. |
| M5 | (text rows; no new mesh) | `panel.station` upgrade section | Upgrade menus are procedural text — no per-effect icons (cut). |
| M6 | `mesh.station.workshop` (reuse) | `panel.workshop` | Steel-blue identity tint. |
| M7 | `world.storageFull` tint/icon; SFX `storage.full` | `panel.silo`, `world.storageFull` | Must read as distinct from ready. |
| M8 | `ui.badge.level`, `ui.bar.xp` (first used here), `ui.panel`; SFX `level.up`; VFX `levelUp` | `popup.levelUp`, `hud.levelXp` (first appearance) | Level/XP HUD lands here, when leveling works. |
| M9 | **`mesh.pet.<species>` ×6**, `mesh.egg`, `mat.pet.void/eyeGlow`, `icon.hud.pets`, `ui.frame.rarity.*`; SFX `egg.hatch`,`pet.assign`; VFX `hatchReveal` | `hud.eggButton`, `hud.voidPetButton`, `menu.voidPet`, `picker.petAssign`, `popup.hatchEgg`, `popup.petDetails`, `popup.genericText`, `world.assignedPet` | Heaviest art milestone. |
| M10 | `mesh.pet.*` (reuse), `icon.heart`; SFX `relationship.form`; VFX `relationshipForm` | `world.relationshipHeart`, `popup.relationshipFormed` | — |
| M11 | VFX `dopamineRain`, `vfx.post.bloom`, `ui.toast`; SFX `worldEvent.start` | `popup.event`, `toast.generic` | — |

**Critical path:** effectively none — every asset and surface has a placeholder (primitive / UGUI default / tween), so no milestone is blocked waiting on art. The **only skilled, IP-critical line is `mesh.pet.<species>` ×6 (M9)**; it runs on placeholder capsules but is the long pole, so start it early even though it isn't needed until M9. Which 6 species is an open item (below).

## Decisions Made
Decisions taken while decomposing that the spec did not dictate, each with its reason.

1. **Levels & Unlocks moved to M8** (after Build/Upgrades/Storage), per user. Reason: as the 4th milestone it was the weakest — only the popup was demonstrable, unlocks/purchasables paid off later. Late, it becomes the payoff: leveling visibly unlocks the build menu, raises caps/slots, and opens gated upgrades that already exist on screen. **Requires** the player-level *value* to exist (frozen at 1) from M3.
2. **`resolve()` value seam from M2** (user). Every tunable read routes through a passthrough `resolve()` until M5's resolver gives it teeth — converting M5/M6 into pure forward extension instead of editing shipped milestones.
3. **§4.5 tap-resolution kept** (user): collect-if-possible-else-open. It's load-bearing (a blocked station is the one you must open to fix). §17 flagged it as never user-ratified; now ratified.
4. **§14 SO split + §15 event catalog adopted as the working contract** (user). §17 marked both "proposed, not agreed"; adopted because per-milestone invention of event names is the single biggest decomposition risk.
5. **HUD money counter born in M2 (shows 0), funded in M3.** Avoids a throwaway resource-visibility trigger that M3 would rework; total-resources popup opens off it from M2.
6. **XP sources split by milestone:** order + job-collect (M3, `XpConfigSO` here), build (M4), hatch (M9). §9 lists all four; only order was originally assigned.
7. **Caps split:** M4 enforces station-type caps at build; M8 raises them by level. **Queue-depth base pinned to M2** (StationSO read), upgrade contribution M5, level contribution M8.
8. **Debug menu grows per-milestone:** add-resources + reset (M2), add-money (M3), level-up (M8), force-spawn-egg (M9), force-fire-event (M11).
9. **`Workshop.unlockLevel = 1` required** (M4 data, M6 depends). Because level is frozen until M8, a higher unlock level would make M6 undemonstrable for three milestones.
10. **Effect schema enums defined in full at M5; scopes/triggers resolved incrementally:** own-station (M5) → global/order/build (M6) → storage.cap (M7) → triggers/conditions/`pet.autoCollectSpeed`/`egg.chance` (M9) → `WithinRangePet`/`local.*`/`range` (M10). `station.cost` + `xp.gain` sites are *wired* at M5 even though no M5 upgrade emits them, so later cost/xp traits actually apply.
11. **Infrastructure may be built ahead of a feature, but nothing user-facing ships until the feature works** (user preference — "build and disable until ready"). Concretely: the XP value + accrual are invisible Core infrastructure from M3, but the **level/XP HUD (`hud.levelXp`) is deferred to M8**, where leveling first does something. XP is verified via debug in M3–M7. This is the general rule for the plan, not a one-off — prefer non-user-facing infra over disabled-but-visible UI (a hidden widget built early is busywork; the value it will display is the real infra).

## Assumptions
Each is a risk; what breaks if it's wrong.

- **Panels are modal** (UI-Inventory assumption; spec silent). If they must be non-blocking, the panel milestones (M2/M3/M6/M7/M9) need a different input-capture model.
- **Pinch-zoom is touch-only and not desktop-verifiable** (§12.5). M1's outcome is *pan only*; if desktop zoom is expected, M1's gate is understated.
- **A player-level int can sit frozen at 1 from M3** and be read by M4–M7 gating without the increment logic. If any read assumes it changes, M8 becomes a forward dependency — kept clean by making all gate reads reactive.
- **§14 partition + §15 payloads are correct enough to build on.** Adopted as contract; individual names may still need correcting in-flight (the partition is fixed, the labels aren't sacred).
- **6 placeholder species are inventable now** and refined after play (§10.2, §17). Real IP identities are an open item; pet meshes are the long pole.
- **Rarity visual treatment** (frames) is a proposed StyleGuide treatment, not spec-mandated.

## Gotchas
Traps a future implementer will hit.

- **Level is HARD-frozen at 1 through M3–M7** — debug "level up" grants XP but nothing increments until M8. So any station with `unlockLevel > 1` is strictly unbuildable before M8. **Workshop must be `unlockLevel 1`** or M6 breaks. M4 can only *demonstrate* building a 2nd Field + a Workshop; new station types stay locked until M8.
- **M4's "locked entry becomes unlocked" is not verifiable until M8.** Write the `unlock:granted` listener in M4, but keep unlock-lifting out of M4's Definition of Done — it belongs to M8's demo.
- **M2 must build `IsCollectionPossible` as a generic predicate** ("if collection possible, collect; else open panel"), not an `if ready` branch. M7 adds storage-full as another false-reason; a ready-specific branch forces rework and ships dead code.
- **The station registry must support runtime add + occupancy from M1.** If M1's pre-placed stations are loose primitives with no grid registration, M4 retrofits occupancy onto them.
- **Base-value-now / contribution-later seams** must be reads through a seam, or a later milestone surgically reopens an earlier one: queue depth (M2→M5→M8), order slots (M3→M6→M8), station caps (M4→M8), order payout (M3→M6), build cost (M4→M6), storage caps (uncapped→M7 enforces).
- **`station.cost` + `xp.gain` sites are wired at M5 with no M5 emitter.** Skip this and "Thrifty" (M9) / cost-affinity friendships (M10) will *describe* correctly but *do nothing* — the highest-consequence coverage trap the audits found.
- **Store timers as absolute timestamps from M2/M3** (§13), not frame-delta — otherwise offline progress becomes the rewrite §13 was written to avoid.
- **Auto-collect (M9) invokes M2's collect path through the predicate** — if storage is full, a pet still can't collect (correct, §4.4).

## Open Items
Unresolved; each is flagged so nobody assumes it works.

- **`WithinRangeStation` condition** (§3.1/§3.4): defined in the enum at M5 but **no milestone resolves it** — no launch content uses it. Author content for it or leave it as declared-but-unused vocabulary. Blocks nothing now; will silently no-op (or throw, if the resolver switch is exhaustive) if a designer authors it.
- **`pet.effectStrength`** (§3.2): defined but **not built** — its only consumer is the deferred VoidPet Station. M9 builds only `pet.autoCollectSpeed`.
- **Which 6 species** (§10.2, asset open item): blocks final `mesh.pet.*` art; placeholders proceed.
- **Rarity visual treatment** (§10.2 / StyleGuide): blocks `ui.frame.rarity.*` final look.
- **Debug-menu wiring** (UI-Inventory open item): §15 defines no `debug:*` intents; each cheat should route through a natural domain effect where one exists. Blocks the *implementation detail* of `menu.debug`, not its mockup.
- **Modal-behind-panel** behavior (UI-Inventory): whether the world dims/blocks behind an open panel.

## Deferred
From the spec (§16, §13), restated so nobody builds it early.

- **VoidPet Station** — area bonus to nearby pets; needs `pet.*` reach + `pet.effectStrength`. Deferred, not cut (R2 #8). No milestone builds it.
- **In-game tuning screen** — moot: the Unity inspector *is* the tuning UI (§9, §16). No in-game surface.
- **Save/load + offline timer progress** (§13, §16) — session-only. Timers are stored as absolute timestamps so persistence is a later change, not a rewrite.
- **Direct resource selling** — permanently out (§16). Orders are the only cash source; that's why the Order Board is load-bearing.
- **Money sinks beyond building and upgrading** (§16, R1 #8).
