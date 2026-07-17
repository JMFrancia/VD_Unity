# VoidDay — Spec Questions, Round 1

**How to use:** Answer inline under each `**A:**`. Every question has a **Default** — if you don't care, just write `d` and move on. Terse answers are fine; I'm reading these to update the spec, not to admire them.

Questions are numbered sequentially so you can reference them by number if you'd rather answer in a batch.

---

## 1. The Sell Side (biggest hole)

`View orders from order station` is the only mention of an order station, and it isn't in the Stations list. Money drives the whole loop and nothing currently produces sell orders.

**1. Where do sell orders live?**
Default: a free **Order Board** that exists from the start, always on the map, tapped to open.
**A:**
Agreed.

**2. How many sell orders are available at once?**
Default: 3 slots, raised by player level.
**A:**
Agreed.

**3. When a slot is fulfilled, how does it refill?**
Default: refills after a timer (e.g. 60s).
**A:**
Agreed.

**4. Do orders expire on their own?**
Default: no.
**A:**
Agreed.

**5. Can the player skip/reject an order they don't want?**
Default: yes, free, slot immediately starts its refill timer.
**A:**
Agreed. What is mechanism to do so? A button?

**6. What does fulfilling an order pay?**
Default: cash + XP, both derived from the ingredients' base values × a multiplier.
**A:**
Agreed.

**7. Can the player sell resources directly, outside of orders?**
Default: no. Orders are the only cash source.
**A:**
Agreed.

**8. Are there money sinks other than building and upgrading?**
Default: no.
**A:**
Not right now.

---

## 2. Terminology (this one blocks the event catalog)

"Order" currently means two different things: a station order (a craft job you queue) and a sell order (fulfill for cash). Event names like `order:completed` are genuinely ambiguous, and `Increases order caps` under level unlocks is unreadable — can't tell if it means job queue depth or sell order slots.

**9. Rename one of them. Which convention?**
Default: station-side = **Job** (`job:queued`, `job:completed`, `job:collected`), sell-side stays **Order** (`order:fulfilled`).
**A:**
Agreed.

**10. Given the rename — `Increases order caps` under level unlocks means which one?**
Default: both are level-gated, tracked separately (job queue depth per station, and Order Board slot count).
**A:**
Agreed.

---

## 3. Stations — Model

**11. Is `Generator Station` one type that covers both producers (wheat field, no input) and converters (bakery, wheat → bread), or two distinct types?**
Default: one type. The recipe decides — a recipe with an empty input list is a producer.
**A:**
Agreed. Though just like with HayDay, everything is a convert. A wheat field spends 1 wheat to produce 2 wheat, for example.

**12. Placement: grid or free-form?**
Default: snap-to-grid, one station per cell, no overlap.
**A:**
Agreed.

**13. Map size, and is the whole thing buildable?**
Default: fixed grid from JSON (e.g. 20×30), all buildable, no terrain restrictions.
**A:**
Agreed.

**14. Do stations occupy more than one cell?**
Default: 1×1 for now, but the schema carries width/height so it can change.
**A:**
Agreed.

---

## 4. Stations — Economy & Rules

**15. Cost to build is implied but never stated — money only, or money + resources?**
Default: money only.
**A:**
Agreed.

**16. Cost to move an already-built station?**
Default: free.
**A:**
Agreed.

**17. Can you demolish/sell a station? Refund?**
Default: yes, refunds 50% of build cost.
**A:**
Agreed.

**18. Station caps exist (levels "increase station caps") — what are the starting caps?**
Default: per station type, defined in JSON; starts at 2 for the first producer, 1 for everything else.
**A:**
Agreed.

**19. How deep is the job queue?**
Default: 3, raised by level and/or station upgrade.
**A:**
Agreed.

**20. A job finishes and the player doesn't collect it. Does the station block, or does the next job start and outputs stack?**
Default: next job starts, outputs stack at the station awaiting collection (HayDay behavior).
**A:**
Block. This is incentive to assign a VoidPet, who will collect for you.

**21. Are inputs consumed when a job is *queued* or when it *starts*?**
Default: at queue time. (Means you can't queue what you can't afford — simpler and more readable to the player.)
**A:**
Agreed.

**22. Can you cancel a queued job? Refund the inputs?**
Default: yes, full refund if it hasn't started; no refund once it's running.
**A:**
Agreed.

---

## 5. The Station Panel (missing from the UI section entirely)

The UI section covers HUD, popups, and menus — but not the panel you get when you tap a station. It's the most-used screen in the game and everything routes through it.

**23. Confirm the station panel carries: recipe list, job queue display, collect button, station upgrades, VoidPet assignment slot. Anything else? Anything cut?**
Default: exactly that list, as tabs or sections in one panel.
**A:**
No collect button. Players collect by tapping the station when it's in a "job complete" state. Assigned VoidPet collects automatically for player.

**24. How do you collect? Tap the station directly on the map, or a button inside the panel?**
Default: both — tap a ready station on the map to collect everything; the panel also has a collect button.
**A:**
I don't see a need for a collect button inside the panel.

---

## 6. Upgrades

**25. Upgrade Station is listed as a *building*, but universal upgrades are bought "from an upgrade menu." Which is it?**
Default: it's a building you place and tap, and tapping it opens the universal upgrade menu.
**A:**
Agreed.

**26. What are the universal upgrades? The doc has no examples.** (Even 3–4 is enough to pin the schema.)
Default: global job-speed %, global build-cost %, Order Board payout %, extra Order Board slot.
**A:**
Agreed.

**27. What do station-specific upgrades do?**
Default: job speed, queue depth, output quantity.
**A:**
Agreed.

**28. Are upgrades one-shot or tiered? Do costs scale?**
Default: tiered, with per-tier costs listed explicitly in JSON (not a formula).
**A:**
Agreed.

---

## 7. XP & Levels

**29. Which actions grant XP?**
Default: collecting a job output, fulfilling an order, building a station, hatching an egg.
**A:**
Agreed.

**30. Level curve shape and level cap?**
Default: explicit table in JSON (no formula), 20 levels for now.
**A:**
Agreed. I will want to build a separate tool exclusively for game balance, which can make direct changes to that same JSON.

**31. "Unlocks game world events" — do world events genuinely not exist until some level?**
Default: yes, first event unlocks at level 5.
**A:**
Agreed. Also most world events will have a minimum level to appear.

**32. When a level unlocks something, is it auto-granted or does it just become purchasable?**
Default: station types and caps are auto-granted; upgrades become purchasable.
**A:**
Agreed.

---

## 8. VoidPets (deepest unknowns after the sell side)

**33. How does the player get eggs?** The doc says "may also receive VoidPet Eggs" but never says from what.
Default: level-up rewards, plus a small chance on order fulfillment.
**A:**
Agreed.

**34. Is there a species list? Roughly how many VoidPets exist at prototype scale?**
Default: 6 species for now.
**A:**
Agreed.

**35. Are traits fixed per species, or rolled per individual pet?**
Default: fixed per species — authored in JSON, no rolling.
**A:**
Agreed.

**36. How many traits per pet?**
Default: 1, with rarer pets having 2.
**A:**
Agreed.

**37. Rarity tiers — what are they, and does rarity affect trait count/power?**
Default: Common / Rare / Epic. Higher rarity = more/stronger traits.
**A:**
Agreed.

**38. Auto-collect: instant when a job completes, or on its own timer?**
Default: instant on job completion.
**A:**
Agreed.

**39. "Range" / "nearby" is used for VoidPet Stations and for relationships — measured how?**
Default: grid cells, Manhattan distance, radius defined in JSON.
**A:**
Agreed.  

**40. Relationships: how do they form?**
Default: instantly when two assigned pets are within range of each other.
**A:**
Not instantly. When placed, a temporary heart icon appears over their head along with the head of other within range. After time is complete, a popup appears that they've formed a friendship, and what traits they get as a result.

**41. Relationships — authored per-pair, or generated from trait rules?** *(This is the single biggest scope fork in the doc. Per-pair is N² hand-written content; rule-generated is a system.)*
Default: authored per-pair in JSON, and only for a handful of pairs. Unlisted pairs simply have no relationship.
**A:**
I prefer rule-generated.

**42. One pet per station, or several?**
Default: one.
**A:**
Agreed.

**43. Can you unassign freely, or is there a cost/cooldown?**
Default: free, instant.
**A:**
Agreed.

**44. Can you get duplicate pets?**
Default: no — dupes are rerolled into an unowned species.
**A:**
Agreed.

---

## 9. World Events

**45. How often do events fire?**
Default: random interval from JSON, roughly every few minutes.
**A:**
Agreed.

**46. What's the actual event list?** (Even 2–3 examples pins the schema.)
Default: 2 flavor-only events, 1 temporary effect (e.g. "Void Surge: all job timers −25% for 60s").
**A:**
Agreed. The temporary effect should be a dopamine rain, which speeds up all job timers 25% for 2 minutes.

**47. What is a "temporary effect" allowed to touch?**
Default: same modifier set the upgrades use — job speed, costs, payouts.
**A:**
Agreed.

**48. How is the player notified?**
Default: toast for flavor, popup for anything with a real effect.
**A:**
Determined by type on a case-by-case basis. I'd like it if ones with real effect showed popup the first time to explain, and toast every time after.

---

## 10. HUD & Interaction Gaps

**49. There's no VoidPet Menu button on the HUD — the menu is specified but has no entry point. Where does it go?**
Default: bottom-right.
**A:**
Agreed, but ONLY once player has first VoidPet.

**50. Nothing on the HUD shows level or XP progress. Add it?**
Default: yes — level badge + XP bar, top-center.
**A:**
Agreed.

**51. The Order Board has no HUD entry point either. Is it map-only (tap the building), or is there a HUD button too?**
Default: map-only.
**A:**
Agreed.

**52. How does placement work from the build menu — drag out of the menu onto the map, or tap the menu then tap the map?**
Default: tap the menu entry, then a ghost preview follows your finger; tap a valid cell to confirm, tap a cancel button to back out.
**A:**
This default doesn't make sense. If you tap (IE let go), it can't follow your finger. We have to drag. if you want to cancel, drag it to invalid spot OR back onto menu. Like in HayDay, menu should retract when you drag off it (so you can place it anywhere on screen) but you can drag over build menu button to re-open it if you want to cancel.

**53. How do you move an existing station?**
Default: long-press to pick up, drag, tap to confirm placement.
**A:**
Agreed.

**54. What's in the debug menu?**
Default: add money, add resources, grant XP, force-spawn egg, force-fire world event, reset save.
**A:**
Generally speaking I think we should build in debug options as we go, but this is a good list to start. Grant XP should be level up, which just grants enough to level up.

**55. Camera zoom limits, and is panning bounded to the map?**
Default: min/max zoom from JSON, panning clamped to map bounds.
**A:**
Agreed.
---

## 11. Session & Persistence

**56. Save/load at all, or session-only?**
Default: session-only for the prototype; localStorage later.
**A:**
Agreed.

**57. Do timers advance while the tab is closed?** *(HayDay's entire design assumes yes — this decision reaches a long way.)*
Default: no for the prototype, but timers are stored as absolute timestamps so offline progress is a small change later.
**A:**
Agreed.

**58. Is there a reset?**
Default: yes, in the debug menu.
**A:**
Agreed.
---

## 12. Data Content (the spec defines shape, but has zero values)

**59. What's the resource list?** Raw ingredients and processed goods.
Default: raw = wheat, corn, egg; processed = bread, cornbread, cake.
**A:**
Total generator stations and their resources:
- field: wheat, corn
- henhouse: eggs
- pasture: milk
- creamery: cream, cheese
- bakery: bread, cornbread, brioche, cheesecake

Total recipes (incredients, not time):
1 wheat -> 2 wheat
1 corn -> 2 corn
1 wheat -> 1 egg
2 wheat -> 1 bread
1 wheat + 1 corn -> 1 cornbread
1 wheat + 1 egg -> 1 brioche
2 corn -> 1 milk
1 milk -> 1 cream
2 milk -> 1 cheese
1 wheat + 1 egg + 1 cheese -> 1 cheesecake

**60. What's the station list for the prototype?**
Default: Wheat Field, Corn Field, Chicken Coop, Bakery, Order Board, Upgrade Station.
**A:**
field, henhouse, pasture, creamery, bakery, order board, silo (storage upgrades), barn (universal upgrades)

**61. Starting state — money, resources, pre-placed stations?**
Default: some starting cash, no resources, one Wheat Field and the Order Board pre-placed.
**A:**
No starting cash, 1 field, 1 wheat, 1 corn, the silo, and the order board pre-placed

**62. Are you fine with me inventing all placeholder numbers (costs, timers, XP values, level thresholds, payouts) directly into the JSON, for you to tune afterward?**
Default: yes — that's what the data layer is for.
**A:**
Yes

---

## 13. One Technical Flag

**63. "All data tunable via separate screen, results permanently saved to JSON."** A browser page can't write to your `data/` folder. This needs a Vite dev-server plugin exposing a write endpoint — very doable, but it's real work, and in the doc it reads like a small feature. Where does it land?
Default: cut it for now. Tune by editing JSON directly; Vite hot-reloads. Revisit once the loop is fun.
**A:**
Fine for now. I'll want a separate app that exports JSON, I can copy/paste directly, but we can do that later.

---

## 14. Placeholder Art

**64. How does a station show its state — idle vs working vs ready-to-collect?**
Default: colored rect with a text label, a progress bar while working, and a bouncing icon above it when ready.
**A:**
Fine for now. We should also have VoidPet appear on top of it if assigned.

---

## 15. Open Floor

**65. Anything in the doc you already know is wrong, stale, or aspirational that I should ignore?**
**A:**
No.

**66. Anything you want in the prototype that isn't in the doc at all?**
**A:**
Hopefully not!
