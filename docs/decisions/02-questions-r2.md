# VoidDay — Spec Questions, Round 2

Same rules: answer under each `**A:**`, write `d` to take the default.

Short round. Most of R1 landed cleanly — this is the fallout from four answers that changed the shape, plus a few small ones.

---

## A. The economy has no floor (critical — this is a real bug, not a nitpick)

Your #11 answer made everything a convert. Your #59 recipe list has no recipe that produces something from nothing. Your #61 start is 1 wheat, 1 corn, no cash. Your #21 consumes inputs at queue time. Your #7 says no direct selling.

Consequence: **wheat at zero is unrecoverable.** Nothing makes wheat except wheat. Corn is the same. Fulfill an order that takes your last wheat — or queue `1 wheat → 1 egg` at the henhouse — and the run is permanently dead with no way back. This is reachable on the first play, and with no starting cash there's no bailout.

**1. How do we put a floor under it?**
Default: the field gets **Fallow** recipes — `0 → 1 wheat` and `0 → 1 corn`, on a deliberately slow timer (~60s). Always available, mathematically guarantees recovery, thematically fine, and the slow timer means it's a safety net rather than a strategy. Everything else stays a convert.
Other options: (b) starting cash + a shop that sells raw resources — but that contradicts #7/#8; (c) orders are forbidden from requesting your last unit — fragile, and doesn't cover the henhouse case; (d) accept the softlock, debug reset only.
**A:**
Agreed. 0->1 recipe that is very slow, compared to 1->2 which is fast for both raw crops.

**2. What generates Order Board orders — i.e. what do they ask for?** R1 covered payout (#6) but never generation. If orders can request resources you can't yet produce, slots clog with unfulfillable garbage.
Default: random pick from resources the player has *unlocked a station for*, quantity scaled to player level, weighted toward higher-tier goods as level rises.
**A:**
Yeah sure. Some smart, procedural order-generation system.

**3. Can an order request more of a resource than the player currently holds?**
Default: yes — that's the pull that drives production.
**A:**
Exactly, yes.

---

## B. Storage caps (new — the silo appeared in #60)

The original doc had no storage caps. #60 introduced "silo (storage upgrades)" and #61 pre-places it at start, which means caps are in from minute one. It's undefined.

**4. Confirm: resources now have a storage cap, and the silo raises it.**
Default: yes.
**A:**
Agreed.

**5. One global cap across all resources, or a per-resource cap?**
Default: one global cap on the *sum* of all resources held. Simplest to read, and makes the silo a real decision.
**A:**
Per-resource, but silo upgrade will upgrade all caps at once.

**6. Heads up — in HayDay, silo = crops and barn = everything else. You've got silo = storage upgrades and barn = universal upgrades, which is a different meaning. Intentional?**
Default: yes, intentional, keep your meaning.
**A:**
Yes, intentional, but may be confusing for ppl expecting HayDay. So let's change it to something else. Ideas?

**7. What happens when storage is full?** This interacts with station-blocking (#20).
Default: you can't collect a completed job — the station stays blocked and shows a "storage full" state. Jobs already queued still run and then block. Nothing is ever destroyed.
**A:**
Agreed.

---

## C. The VoidPet Station vanished

Your original doc lists three station types: Generator, Upgrade, and VoidPet ("Provide area bonus to VoidPets nearby, may have conditions/limits"). The #60 station list is: field, henhouse, pasture, creamery, bakery, order board, silo, barn. No VoidPet Station.

**8. Cut, deferred, or did it get folded into something?**
Default: deferred — not in the first build, stays in the spec as a later addition.
**A:**
Agreed.

---

## D. Relationships need a rule system (fallout from #41)

You chose rule-generated over authored pairs. I think that's right, but it means traits need something to match on, and no such structure exists yet.

**9. Do traits get tags/categories that rules match against?**
Default: yes. Each trait has a tag (e.g. `speed`, `yield`, `cost`, `xp`) and an element/theme (e.g. `void`, `verdant`, `ember`).
**A:**
No no no nothing like that. Let's say a trait has a name, and a list of effects. Each effect has:
- A name (what it's called)
- A type (what it effects),
- A value (by how much)
- [Optional] A trigger (when it occurs. If none, then is always occurring)
  - [Optional] A trigger probability (% chance of occurring on trigger. If none, then 100%)
- [Optional] A condition (only when X is true. if none, then is always true)


Example #1:

Name: Hard worker
Type: assigned station speed
Value: +25%
Trigger: None
Trigger Prob: None
Condition: None

Example #2:

Name: Thrifty
Type: assigned station recipe cost
Value: -15%
Trigger: None
Trigger Prob: None
Condition: None


Example #3:

Name: Friendship with <Specific VoidPet>
Type: local station speed
Value: +15%
Trigger: None
Trigger Prob: None
Condition: Within X units of <Specific VoiddPet>

Example #4:

Name: Cow Lover
Type: local station yield
Value: x3
Trigger: On order complete
Trigger Prob: 20%
Condition: Assigned to pasture


Also: we should be able to procedurally generate a description for any given trait based on data. Ex:

Cow Lover: When assigned to pasture, 20% chance of x3 yield on order complete.


**10. What's the shape of a relationship rule?**
Default: `tag A + tag B → bonus`, with a same-tag case and a same-theme case. E.g. two `speed` pets → bigger speed bonus to both; two `void` pets → an unrelated third bonus. Rules live in JSON, evaluated in order, first match wins.
**A:**
When VoidPets form relationship, they both get a relationship trait. The condition of that trait only kicks in when they are assigned within range of one another. All traits should be capable of working together simultaneously (IE one activating does not prevent another from activating)

**11. What can a relationship bonus actually do?**
Default: same modifier vocabulary as upgrades and world events — job speed, costs, payouts, yield. No new mechanics.
**A:**
Agreed

**12. Per #40, relationships form over time with a heart icon then a popup. How long, and does the relationship survive if the pets are later moved apart or unassigned?**
Default: ~30s of continuous proximity to form; once formed it's permanent and survives separation, but the *bonus* only applies while they're back in range.
**A:**
Agreed.

**13. Can one pet hold several relationships at once?**
Default: yes, with everyone in range. Bonuses stack.
**A:**
yes.

**14. The trait list itself — 6 species × 1–2 traits. Do you want to author these, or should I invent placeholders?**
Default: I invent them into JSON, you tune/rewrite after you've played it.
**A:**
Agreed.

---

## E. Small stuff

**15. Confirm the blocking rule from #20:** job completes → output sits at the station → the next queued job does **not** start until you collect. So a 3-deep queue only advances on collection, and that's exactly the friction VoidPets relieve.
Default: yes, as stated.
**A:**
Agreed.

**16. `1 milk → 1 cream` is the only recipe producing cream, and cream isn't an input to anything.** It's a terminal good — its only use is selling to orders. Intentional?
Default: yes, intentional. Some goods exist purely as order fodder.
**A:**
Yes. The only good that is NEVER sold as-is is wheat.

**17. #48 (popup first time, toast after) — with no save (#56), "first time" resets every session.** So you'd see the explainer popup once per reload. Fine for a prototype?
Default: fine, leave it.
**A:**
Agreed.
