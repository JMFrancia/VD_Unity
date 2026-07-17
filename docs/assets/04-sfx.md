# Assets — SFX

**Spec:** `docs/VoidDay-Spec-unity.md` · **Style guide:** `docs/StyleGuide.md`
**Count:** 20

## Format

- **Register:** soft, rounded, "poppy" — gentle pops/chimes/whooshes, never harsh or realistic-foley (StyleGuide Audio / Q17).
- **Void flavor:** hatch, level-up, and void-events carry a faint shimmer/reverb tail ordinary farm SFX don't — the sonic version of the reserved violet glow (StyleGuide Audio).
- **Delivery:** short **`.ogg` or `.wav`, mono**, normalized. One clip per distinct player action / system event, sourced from the **§15 event catalog**. A View listener plays them off domain events (never Core).
- **Naming:** `sfx.<domain>.<action>` mirroring the event name.

## Assets

| id | What | Trigger event (§15) | Qty | Spec |
|---|---|---|---|---|
| `sfx.ui.tap` | Soft button/panel tap | (UI, input layer) | 1 | §12 |
| `sfx.ui.open` | Panel/menu open | (UI) | 1 | §12 |
| `sfx.ui.close` | Panel/menu close | (UI) | 1 | §12 |
| `sfx.station.place` | Station placed (soft thunk) | `station:built` | 1 | §4.3, §12.2 |
| `sfx.station.move` | Station picked up / moved | `station:moved` | 1 | §12.2 |
| `sfx.station.demolish` | Station demolished | `station:demolished` | 1 | §4.3 |
| `sfx.job.queue` | Recipe queued | `job:queued` | 1 | §4.4 |
| `sfx.job.complete` | Job finished, ready to collect | `job:completed` | 1 | §4.4 |
| `sfx.job.collect` | **Collect-pop — the hero SFX** | `job:collected` | 1 | §4.4 |
| `sfx.job.cancel` | Job cancelled | `job:cancelled` | 1 | §4.4 |
| `sfx.order.fulfill` | Order fulfilled (cash chime) | `order:fulfilled` | 1 | §6 |
| `sfx.order.skip` | Order skipped | `order:skipped` | 1 | §6 |
| `sfx.order.refill` | Slot refilled (subtle) | `order:slotRefilled` | 1 | §6 |
| `sfx.storage.full` | Collection refused, storage full (soft error) | `storage:full` | 1 | §4.4, §7 |
| `sfx.xp.gain` | XP tick (subtle) | `xp:gained` | 1 | §9 |
| `sfx.level.up` | Level-up fanfare (void shimmer) | `level:up` | 1 | §9 |
| `sfx.egg.hatch` | Egg reveal sparkle | `egg:hatched` | 1 | §10.1 |
| `sfx.pet.assign` | Pet assigned/unassigned (soft) | `pet:assigned` / `pet:unassigned` | 1 | §10.3 |
| `sfx.relationship.form` | Friendship formed (warm chime) | `relationship:formed` | 1 | §10.5 |
| `sfx.worldEvent.start` | Void-event sting (whoosh) | `worldEvent:started` | 1 | §11 |

## Notes

- **Grouped, not 1:1 with every event.** Some §15 events share a clip: `pet:assigned`/`unassigned` → one `sfx.pet.assign`; `egg:granted` folds into `sfx.level.up`/`order.fulfill` (the moment that granted it) rather than its own cue; `money:changed` folds into `sfx.order.fulfill`. This keeps the set at 20 without a silent event.
- **`upgrade:purchase`** — the spec has no distinct upgrade-purchase event in §15; reuse `sfx.order.fulfill` (a purchase-confirm chime) until one exists. Flagged, not added.
- Priority for first pass: `sfx.job.collect`, `sfx.order.fulfill`, `sfx.ui.tap`, `sfx.level.up` — the four most-heard cues.
