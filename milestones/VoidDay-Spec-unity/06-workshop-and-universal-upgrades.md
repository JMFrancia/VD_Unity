# Milestone 06 — Workshop & Universal Upgrades

**Playable outcome:** Build a Workshop, buy a universal upgrade there (global job speed, global build cost, order payout, or an extra order slot), and see it apply across the whole farm at once.

## Goal
Extends the effect resolver from own-station reach to **map-wide and economy-wide scopes** — the first effects that touch more than the emitter's own station. It proves the resolver generalizes exactly as §3 intends, and it's the second building the player places (so it depends on M4's build system and validates the Workshop-at-level-1 constraint).

## Build This
- **Workshop as a placed building** (§4.2, §8): tap-to-open, sells universal upgrades. Placed via M4's build flow — **`Workshop.unlockLevel` must be 1** so it can be built before M8.
- **`panel.workshop`** (§8): a list of `pattern.purchaseRow`s, one per universal upgrade — effect via the procedural description (§3.6), per-tier money cost (explicit on `UpgradeSO`), tier progression, `Buy` / `Maxed`.
- **Universal upgrades** (§8): global job speed %, global build cost %, order payout %, extra order slot.
- **Extend the resolver vocabulary** to the scopes these need: `global.speed` / `global.cost` / `global.yield` (every station on the map), `build.cost` (station build cost, wired at M4's build-cost read site), `order.payout` (wired at M3's payout read site), `order.slots` (wired at M3's slot-count read site). All still **passive** (no triggers) — this milestone adds scope reach, not trigger machinery.
- **Wire the seams M3/M4 planted**: order payout, order-slot count, and build cost now route through the resolver so a Workshop upgrade actually moves them. This is extension of existing read-sites, not a rewrite.

## Do NOT Build This
- **Triggers, conditions, `triggerChance`** → M9.
- **`storage.cap` / Silo** → M7.
- **`local.*` / `WithinRangePet` / range** → M10.
- **Level-gating of which universal upgrades appear** → M8 (the gate read may exist; its activation is M8).
- **A new build flow** — Workshop uses M4's placement as-is.
- **`xp.gain` producers** — the site is wired (M5); a universal upgrade *could* emit `xp.gain` as content, but don't invent one unless tuning wants it.

## Context
Builds on M4 (build/place, `build.cost` read-site), M3 (payout + slot read-sites), M5 (resolver, description generator, `UpgradeSO`). Adds to the spine:
- **Events added:** none new — reuses `input:upgradePurchaseRequested`, `effects:recalculated`. (Order-slot changes surface via existing order events.)
- **Data added:** `UpgradeSO` universal tiers + their `Effect[]` (global/order/build scopes); `StationSO` Workshop entry with `unlockLevel = 1`.
- **Systems touched:** `Core/Rules/EffectResolver` (new scopes), `Systems/Upgrades` (universal purchases); `resolve()` sites in `Systems/OrderBoard` (payout, slots) and `Systems/BuildPlacement` (build cost) now active; `View/WorkshopPanel`.

## Principles
- **The spine generalizes** (§3): adding `global.*`/`order.*`/`build.*` should be new *scope resolution*, not new resolver code paths per type. If you're writing a bespoke branch per upgrade, stop.
- **Event-driven** (rule 2): an order-payout change is observed by the Order Board re-reading resolved payout on `effects:recalculated`; the Workshop never calls the Order Board.
- **Data-driven** (rule 1): every magnitude and cost on `UpgradeSO`; the resolver reads `Effect` structs.
- **Test the core** (CLAUDE.md): global-scope stacking and order-payout resolution are pure-C# economy — cover the payout-with-effect and slot-count-with-effect cases.
- **Verify Unity APIs**: nothing new beyond M4/M5 patterns.

## Assets Required
- `mesh.station.workshop`, `mat.station.workshop` [placeholder OK — from M4]
- Universal upgrade rows are procedural text + `ui.button`/`ui.card` [placeholder OK]; steel-blue Workshop identity tint `#5B7A99` (StyleGuide) [placeholder OK]
- **SFX** [placeholder OK]: reuse `sfx.order.fulfill` as purchase-confirm (per M5)

## UI Mockups Required
- `panel.workshop` — [mockup needed]; rows are `pattern.purchaseRow`.

## Definition of Done
- A Workshop can be built (it's a level-1 unlock) and tapped to open its panel of universal upgrades.
- Buying "global +% job speed" speeds up jobs at **every** station, not just one.
- Buying "order payout +%" makes the next fulfilled order pay more; "extra order slot" adds a slot to the Order Board; "global build cost −%" lowers the next station's build cost.
- Effects stack with station upgrades per §3.5.

## How to Test
1. Build a Workshop (confirm it's placeable at level 1).
2. Note job timers at two different stations; buy global speed; confirm **both** speed up.
3. Buy an extra order slot → the Order Board now shows one more slot.
4. Note an order's payout; buy order-payout %; fulfill → it pays more.
5. Note a station's build cost in the menu; buy global build-cost −%; confirm the menu cost drops.
6. Confirm a global effect and a station upgrade on the same station stack additively per §3.5.
