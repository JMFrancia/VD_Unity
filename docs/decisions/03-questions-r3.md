# VoidDay — Spec Questions, Round 3

Last round, I think. Almost all of this is nailing down the effect system you specified in R2 #9 — it's now the spine of the game, so it's worth 10 minutes of precision.

Same rules: `d` takes the default.

---

## A. The Effect system (from R2 #9)

**1. Confirm the big one: one `Effect` schema powers everything.**
VoidPet traits, relationship traits, station upgrades, universal upgrades, and world events are all just "a thing that emits effects." Same schema, same resolver, same procedural description generator. A new effect `type` added once becomes available to every system at once.
Default: yes. This is the single highest-leverage structural decision in the spec.
**A:**
Agreed.

**2. `value` needs an operation — your examples use three different maths.**
`+25%` (percent), `-15%` (percent), `x3` (multiplier). And flat adds (`+2 storage`) will want to exist too.
Default: `value: { op: "pct" | "mult" | "flat", amount: number }`. So `+25%` = `{op:"pct", amount:25}`, `x3` = `{op:"mult", amount:3}`, `+2` = `{op:"flat", amount:2}`.
**A:**
Agreed.

**3. Stacking math — two effects hit the same number. What happens?**
You said in #10 that everything works simultaneously, so both apply. But `+25%` and `+25%` could mean +50% or ×1.5625.
Default: within a type, all `flat` sum first, then all `pct` sum and apply once (so +25% and +25% = +50%), then all `mult` multiply. Predictable, designer-readable, and avoids runaway stacking.
**A:**
Agreed

**4. The `type` vocabulary.** This is the list of things an effect can touch. Everything needs to be on it or it can't be expressed.
Default:
- `station.speed` — job timer on the assigned station
- `station.cost` — recipe input cost at the assigned station
- `station.yield` — job output quantity
- `station.queueDepth`
- `local.speed` / `local.cost` / `local.yield` — same, but for stations within range (see #7)
- `global.speed` / `global.cost` / `global.yield` — all stations
- `build.cost`
- `order.payout` — cash from fulfilling
- `order.slots`
- `xp.gain`
- `storage.cap`
- `egg.chance`

Anything missing? Anything you'd cut?
**A:**
What is egg.chance? Otherwise agreed.

**5. The `trigger` vocabulary.**
Default: `job.queued`, `job.completed`, `job.collected`, `order.fulfilled`, `station.built`, `pet.hatched`, `levelUp`. Omitted = always active (a passive modifier rather than a fired event).
**A:**
Agreed

**6. The `condition` vocabulary.**
Default: `assignedTo: <stationType>`, `withinRange: <petId>`, `withinRange: <stationType>`, `resourceAbove: <resource, n>`, `playerLevelAbove: <n>`. Omitted = always true.
**A:**
Agreed

**7. Define "local."** Your example #3 uses `local station speed` and your example #4 uses `local station yield`, but "local" isn't defined anywhere.
Default: all stations within N grid cells (Manhattan, per R1 #39) of the station the pet is assigned to. N comes from JSON per-effect, so different traits can have different reach.
**A:**
I think I just meant "assigned", but I like what you're suggesting too.

**8. Your Cow Lover example reads "Trigger: On order complete" but the effect is `yield` — which is job output, not a sale.** I read that as the Job/Order collision from R1 #9 resurfacing (which is a decent sign the rename earned its keep).
Default: you meant `job.completed`. Cow Lover = "When assigned to a pasture, 20% chance of ×3 yield on job completion."
**A:**
I meant job.completed.

**9. Trait vs. effect naming.** You said a trait has a name and a list of effects, and each effect *also* has a name — but every example shows a single name.
Default: the trait carries the player-facing name ("Cow Lover"); effects have an internal id for debugging but aren't individually surfaced. The description generator composes the trait's line from all its effects.
**A:**
Agreed.

---

## B. The last real hole — what's *in* a relationship trait? (R1 #41 / R2 #10)

You chose rule-generated. R2 #10 gave me the mechanism — both pets gain a trait whose condition is proximity to the other. But nothing says what **determines its content**.

**10. What generates the relationship trait's effect?**
Default: **derived from the pair's existing traits.** Each species carries an `affinity` field naming an effect type it "pushes" (Hard Worker → `speed`, Thrifty → `cost`). When two pets befriend, each gains a trait granting a `local.<partner's affinity>` bonus — i.e. you get a taste of what your friend is good at, but only while you're near them. Magnitude from JSON, scaled by the rarer of the two.
Other options: (b) flat random pick from an effect table, weighted by rarity; (c) an explicit rule table matched on species pair, evaluated in order (closer to authored, but with a fallback so no pair is empty).
**A:**
Agreed.

---

## C. One small consequence

**11. R2 #16: "the only good that is NEVER sold as-is is wheat."** That's a constraint on order generation (R2 #2) — wheat is excluded from the order pool even though it's an unlocked resource.
Default: yes, wheat is order-pool excluded. Corn stays sellable.
**A:**
Agreed.

---

## D. Naming (R2 #6)

**12. Silo and Barn need new names — they currently mean something different from HayDay's silo/barn and you flagged that as confusing.**

| Storage | Universal upgrades |
|---|---|
| **Vault** *(rec)* | **Workshop** *(rec)* |
| Cache | Nexus |
| Depot | Foundry |
| Reservoir | Lab |
| Stockpile | Sanctum |

Default: **Vault** + **Workshop** — no HayDay collision, both self-explanatory on sight, neither needs a tooltip. **Cache** + **Nexus** if you want it more Void-flavored.
**A:**
Workshop.
