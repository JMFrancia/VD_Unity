# Milestone 03 — Order Board

**Playable outcome:** Tap the Order Board, fulfill an order for held goods to earn your first cash and XP, skip an order you don't want, and watch a used slot refill after a timer.

## Goal
The only cash source in the game (§16) and the first XP source, so it comes right after the station loop: now producing goods *means* something. It also breaks the economy's chicken-and-egg — you grind corn from the pre-placed Field (corn self-replicates 1→2 and is sellable; wheat is not) and fulfill corn orders for the money M4 will let you spend. Introduces the player-progression Core value (level + XP total) that M4–M7 read and M8 later increments.

## Build This
- **`panel.orderBoard`** (§6): 3 slots (base from `OrderConfigSO`, read via the seam). Each filled slot is a `pattern.orderCard` — requested goods with have/need signals, cash+XP reward, `Fill` (enabled only when all goods held), corner `X` to skip. A used slot shows a refill timer (~60s) instead of a card. Orders never expire.
- **Order generation** (§6.1) in `Core/Rules`: procedural, `System.Random` (never `UnityEngine.Random`), operating on `Core/Model` resource objects. Random pick from resources the player has a station for, quantity scaled to level, weighted toward higher tiers as level rises. **Wheat excluded** by reading `sellable=false` off the wheat resource model — not a separate exclusion list.
- **Payout** (§6): cash + XP derived from requested ingredients' base values × multiplier from `OrderConfigSO`. Read through the seam (M6 adds `order.payout`).
- **Fulfill / skip** (§6): fulfill consumes goods, grants cash + XP, refills the slot after the timer; skip refills immediately, free, no confirm.
- **The player-progression value** (§9, this is the split that lets M8 sit late): a Core `playerLevel` int **starting at 1** and an `xpTotal`. XP **accrues but the level does not increment yet** (M8 owns thresholds/increment). Expose both as read-only Core state — **invisible infrastructure**, no HUD (see below).
- **XP accrual** (§9): `xp:gained` on **order fulfillment** and on **job collection** (a listener on M2's `job:collected`). `XpConfigSO` supplies per-action XP. XP awards route through the seam (M5 adds `xp.gain`).
- **HUD:** make `hud.money` meaningful (wire the counter M2 built to `money:changed` — do **not** rebuild it). **No level/XP UI this milestone** — leveling can't do anything until M8, so nothing about it goes on screen yet. The XP total accrues in Core as invisible infrastructure; verify it in M3–M7 with a **debug readout**, not a HUD element. `hud.levelXp` (badge + bar) is built in M8, when it finally reflects something that moves.
- **Order-slot count read through a seam** so M6's `order.slots` and M8's level-raise extend it rather than rewrite it.
- **Debug** (§12.7): add **add money** to `menu.debug`.

## Do NOT Build This
- **Level-up / thresholds / unlock grants / the level-up popup** → M8. The level value is read here but never increments.
- **Any level/XP HUD (`hud.levelXp`)** → M8. XP accrues invisibly in Core; do not surface a level badge or XP bar for a mechanic that can't move yet (build-and-not-user-facing).
- **The `order.payout` / `order.slots` effects** → M6 (base values only here, through the seam).
- **The `xp.gain` effect** → M5 gives the seam teeth; base XP only here.
- **Eggs on fulfillment (`egg.chance`)** → M9.
- **Direct resource selling** → never (§16). Orders are the only cash source.
- **Building stations to unlock more order variety** → M4.

## Context
Builds on M2 (resource pool, `job:collected`, money counter, bus). Adds to the spine:
- **Events added:** `input:orderFulfillRequested {orderId}`, `input:orderSkipRequested {orderId}`; `order:generated {slot, order}`, `order:fulfilled {orderId, payout, xp}`, `order:skipped {orderId}`, `order:slotRefilled {slot}`; `money:changed {delta, total}`; `xp:gained {amount, source}`.
- **Data added:** `OrderConfigSO` → model (slot count, refill timer, payout multipliers, generation weights); `XpConfigSO` → model (XP per action); `ResourceSO.sellable` read (wheat=false); `ResourceSO.baseValue` read for payout.
- **Systems touched:** new `Core/Rules/OrderGeneration`, `Core/Rules/OrderPricing`, `Systems/OrderBoard`, `Systems/Progression` (holds `playerLevel`/`xpTotal`, awards XP); `View/OrderBoardPanel`, `View/HUD` (money meaning, level/XP bar).

## Principles
- **Core randomness is `System.Random`** (§6.1, CLAUDE.md rule 3) — order generation is testable headless; this is exactly the economy core the project says to test.
- **One rule, one home** (§14): wheat's exclusion is `sellable=false` on the model, read by generation. Never duplicate it as an exclusion list.
- **Event-driven** (rule 2): the money counter and cash chime *listen* to `order:fulfilled`/`money:changed`; the Order Board never calls the HUD or audio.
- **Test the core** (CLAUDE.md): order pricing and generation are pure C# — cover them with EditMode tests (payout math, wheat exclusion, level scaling).
- **Data-driven** (rule 1): slot count, refill timer, multipliers, weights, XP-per-action — all SO, via the seam where a later effect will touch them.

## Assets Required
- `mesh.station.orderBoard`, `mat.station.orderBoard` [placeholder OK — from M1]
- `icon.resource.*` for whatever orders can request (corn earliest; others as their producers unlock) [placeholder OK]
- `icon.money` [placeholder OK — from M2], `icon.close` [placeholder OK — X glyph]
- (`ui.bar.xp`, `ui.badge.level` are **not** needed here — the level/XP HUD is deferred to M8.)
- **SFX** [placeholder OK]: `sfx.order.fulfill` (cash chime — high-priority cue), `sfx.order.skip`, `sfx.order.refill`, `sfx.xp.gain`

## UI Mockups Required
- `panel.orderBoard` — [mockup needed]; slots use `pattern.orderCard`.
- `hud.money` (now funded) — [mockup needed]. (`hud.levelXp` deferred to M8 — no level UI until leveling works.)

## Definition of Done
- Grinding corn (`1 corn → 2 corn`) from the start state produces enough corn to fulfill a corn order; fulfilling grants cash (money counter rises) and XP (accrues in Core — verify via a debug readout, **no XP bar on screen**).
- An order requesting more than you hold shows the shortfall in the warning color and its `Fill` disabled.
- Skipping an order clears it and starts a ~60s refill; a fulfilled slot also refills after the timer.
- No order ever requests wheat.
- There is **no level/XP HUD** yet; the level stays at 1 and XP accumulates invisibly for M8 to consume.

## How to Test
1. From the start state, queue `1 corn → 2 corn` repeatedly (collect each) until you hold several corn.
2. Tap the Order Board → confirm 3 slots, at least one a corn order with a have/need signal.
3. Fulfill it → money counter rises, the slot shows a refill timer; confirm XP rose via a debug readout (no XP bar on screen).
4. Confirm no slot ever requests wheat; confirm a slot requesting more corn than you hold shows the shortfall and a disabled `Fill`.
5. Skip an order → it clears immediately and begins refilling.
6. Keep fulfilling; confirm (via debug) XP accrues while no level/XP UI appears and the level stays frozen at 1.
7. Debug → add money; confirm the counter reflects it (prep for M4).
