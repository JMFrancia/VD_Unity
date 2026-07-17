# VoidDay — Game Spec

A farming/pipeline-management prototype inspired by HayDay. Portrait mobile, touch-only, HTML5.

**Status:** consolidated from the original pitch + three rounds of Q&A (R1/R2/R3). This is the source of truth for milestone decomposition.

**Companion docs:** `CLAUDE.md` (architecture rules — data-driven, event-driven, MVC boundary). This spec does not restate those rules; it assumes them.

---

## 1. Core Loop

The player builds stations that convert resources into other resources on timers, then sells the results to the Order Board for cash and XP. Cash buys more stations and upgrades. XP levels the farm, which unlocks new station types, higher caps, and new upgrades. VoidPets are collectable familiars assigned to stations to auto-collect and grant bonuses; pets near each other form relationships that grant further bonuses.

```
resources ──queue job──> station ──timer──> output ──collect──> resources
                                                                    │
                                                              fulfill order
                                                                    │
                                                            cash + XP + eggs
                                                                    │
                                              ┌─────────────────────┼──────────────┐
                                          build/upgrade         level up        VoidPets
                                              │                     │              │
                                              └──── more throughput ─┴──────────────┘
```

**The tension:** stations **block** on uncollected output (§4.4). Every completed job demands a tap. This is deliberate friction, and relieving it is what VoidPets are *for*.

---

## 2. Platform

- Portrait phone dimensions. Touch/drag only — no keyboard.
- WebGL (via Unity)
- Top-down view of an open grassy area.
- Placeholder art: colored rects with text labels. See §12.6.

---

## 3. The Effect System

**This is the spine of the game. Build it deliberately; everything else hangs off it.**

VoidPet traits, relationship traits, station upgrades, universal upgrades, and world events are all *the same thing*: something that emits Effects. One schema, one resolver, one description generator. Adding a new effect `type` once makes it available to every system simultaneously.

### 3.1 Schema

```ts
type Effect = {
  id: string;              // internal, for debugging — not player-facing
  type: EffectType;        // what it touches
  value: {
    op: 'flat' | 'pct' | 'mult';
    amount: number;
  };
  resource?: string;       // scopes the effect to one resource (see §3.2)
  range?: number;          // grid cells, required by local.* / pet.* types
  trigger?: TriggerType;   // omitted = passive, always active
  triggerChance?: number;  // 0–100, omitted = 100
  condition?: Condition;   // omitted = always true
};

type Trait = {
  id: string;
  name: string;            // player-facing ("Cow Lover")
  effects: Effect[];
};
```

The **trait** carries the player-facing name. Effects have ids for debugging but are never individually surfaced.

### 3.2 `EffectType` vocabulary

| Type | Touches |
|---|---|
| `station.speed` | job timer at the pet's/upgrade's own station |
| `station.cost` | recipe input cost at own station |
| `station.yield` | job output quantity at own station |
| `station.queueDepth` | job queue depth at own station |
| `local.speed` / `local.cost` / `local.yield` | same, but every station within N grid cells |
| `global.speed` / `global.cost` / `global.yield` | every station on the map |
| `build.cost` | station build cost |
| `order.payout` | cash from fulfilling an order |
| `order.slots` | Order Board slot count |
| `xp.gain` | XP from all sources |
| `storage.cap` | per-resource storage cap |
| `egg.chance` | egg drop rate on order fulfillment (§10.1) |
| `pet.effectStrength` | scales the magnitude of another pet's effects |
| `pet.autoCollectSpeed` | how fast a pet auto-collects |

**`station.*` vs `local.*` vs `pet.*`:** `station.*` is the emitter's own station. `local.*` reaches every station within `range` grid cells (Manhattan, per §10.4). `pet.*` reaches other *VoidPets* within `range` — required by the pitch's "a trait that affects nearby assigned VoidPets" and by the deferred VoidPet Station's area bonus. `range` is per-effect, so different traits can have different reach.

**The `resource` scope:** any `*.cost` or `*.yield` type may carry a `resource` field narrowing it to one resource. This is what expresses the pitch's *"universal cost decrease for specific resource"* — `global.cost` with `resource: "wheat"` means wheat costs less everywhere. Omitted = applies to all resources.

### 3.3 `TriggerType` vocabulary

`job.queued`, `job.completed`, `job.collected`, `order.fulfilled`, `station.built`, `pet.hatched`, `levelUp`

Omitted = passive modifier, always applied. Present = fires once on that event, subject to `triggerChance`.

### 3.4 `Condition` vocabulary

`assignedTo: <stationType>` · `withinRange: <petId, n>` · `withinRange: <stationType, n>` · `resourceAbove: <resource, n>` · `playerLevelAbove: <n>`

Omitted = always true.

`withinRange: <petId>` requires that **both** pets are currently assigned — an unassigned pet sitting in the menu is not "near" anything. Per R2 #10: *"the condition of that trait only kicks in when they are assigned within range of one another."*

### 3.5 Resolver — stacking math

Effects never suppress each other; all applicable effects apply simultaneously. For a given `type`, resolve in this fixed order:

1. Sum all `flat` amounts, add.
2. Sum all `pct` amounts, apply once. (`+25%` and `+25%` = **+50%**, not ×1.5625.)
3. Multiply all `mult` amounts in sequence.

Predictable, designer-readable, and avoids runaway stacking.

### 3.6 Procedural descriptions

One function generates a player-facing sentence from any Trait's effect data. Serves VoidPet details, upgrade menus, relationship popups, and world event popups.

> **Cow Lover:** When assigned to a pasture, 20% chance of ×3 yield on job completion.
>
> **Hard Worker:** +25% speed at its station.
>
> **Thrifty:** −15% recipe cost at its station.

These three are the user's own hand-written examples (R2 #9) and serve as the reference cases for the generator.

---

## 4. Stations

### 4.1 Model

One station type covers everything — the **recipe** decides behavior. Every recipe is a convert; there are no free producers except Fallow (§5.2).

- **Placement:** snap-to-grid, one station per cell, no overlap.
- **Map:** fixed grid from JSON (~20×30), entirely buildable, no terrain restrictions.
- **Footprint:** 1×1 for now. Schema carries `width`/`height` so this can change.

### 4.2 Station list

| Station | Produces | Notes |
|---|---|---|
| Field | wheat, corn | Pre-placed at start ×1 |
| Henhouse | eggs | |
| Pasture | milk | |
| Creamery | cream, cheese | |
| Bakery | bread, cornbread, brioche, cheesecake | |
| Order Board | — | Free, pre-placed, sell orders (§6) |
| Silo | — | Pre-placed, storage cap upgrades (§7) |
| Workshop | — | Universal upgrades (§8) |

The Silo and Workshop hold nothing — resources are a global pool of numbers, per the pitch. Both are buildings whose only function is selling upgrades, which is the pitch's "Upgrade Stations: place to purchase specific universal upgrades."

> **On the name "Silo":** it was briefly renamed to Vault over a HayDay collision (HayDay's silo *holds* crops; ours sells cap upgrades). Reverted — the confusion is theoretical, it's one tap to resolve, "Vault" was never actually approved, and Silo fits the place-noun pattern that "Storage" breaks. It's a `displayName` in JSON; change it freely once it's on a real button.

### 4.3 Economy

- **Build cost:** money only.
- **Move:** free.
- **Demolish:** 50% refund of build cost.
- **Caps:** per station type, from JSON, raised by level. Starts at 2 Fields, 1 of everything else.
- **Queue depth:** 3 by default, raised by level and station upgrades.

### 4.4 Job rules

- **Inputs consumed at queue time.** You cannot queue what you cannot afford. Naturally self-limits queue depth early.
- **Cancel:** full refund if not yet started; no refund once running.
- **Blocking:** a completed job's output **sits at the station and the next queued job does not start until collected.** A 3-deep queue only advances on collection. This is the core friction.
- **Collection:** tap the station on the map when it's in the "job complete" state. There is **no collect button** in the panel.
- **Storage full:** collection is refused, station stays blocked, shows a "storage full" state. Queued jobs still run, then block behind it. **Nothing is ever destroyed.**

### 4.5 Station panel

Carries:

- Recipe list (queue a job)
- Job queue display (with cancel)
- Station upgrades
- VoidPet assignment slot

No collect button — collection is a map tap.

**Tap resolution:** a tap on a station **collects if collection is possible; otherwise it opens the panel.** So a ready station collects on the first tap and opens its panel on the second, and a storage-full station — where collection is refused (§4.4) — opens the panel immediately.

> This rule matters more than it looks. A blocked station is exactly the one you want to open, because the panel is where you assign the VoidPet that unblocks it. "Tap only ever collects" would lock you out of the fix at the moment you need it.

---

## 5. Resources & Recipes

### 5.1 Resources

**Raw:** wheat, corn, eggs, milk
**Processed:** cream, cheese, bread, cornbread, brioche, cheesecake

Wheat and corn are the only self-replicating resources (1 → 2). Everything else is a net drain, which makes field count the effective ceiling on the whole economy.

> That ceiling is an observation about the recipe list as specified, not a stated design goal — nobody has decided yet whether it's a feature. Flagged because it's the kind of property that's much cheaper to notice now than after tuning.

### 5.2 Recipes

| Station | Recipe | Speed |
|---|---|---|
| Field | 1 wheat → 2 wheat | fast |
| Field | 1 corn → 2 corn | fast |
| Field | **Fallow:** 0 → 1 wheat | **very slow** |
| Field | **Fallow:** 0 → 1 corn | **very slow** |
| Henhouse | 1 wheat → 1 egg | |
| Pasture | 2 corn → 1 milk | |
| Creamery | 1 milk → 1 cream | |
| Creamery | 2 milk → 1 cheese | |
| Bakery | 2 wheat → 1 bread | |
| Bakery | 1 wheat + 1 corn → 1 cornbread | |
| Bakery | 1 wheat + 1 egg → 1 brioche | |
| Bakery | 1 wheat + 1 egg + 1 cheese → 1 cheesecake | |

**Fallow exists to prevent a softlock.** Because everything is a convert and nothing else produces wheat or corn from nothing, hitting zero wheat would otherwise be permanently unrecoverable (fulfill an order with your last wheat, or convert wheat → egg at the henhouse, and the run is dead — with no starting cash there's no bailout). Fallow is always available and deliberately slow: a safety net, never a strategy.

**Cream is terminal** — produced by nothing else, consumed by nothing. Its only use is order fodder. Intentional.

**Timers are per-recipe and optional.** The pitch says recipes convert resources *"sometimes with a timer"*, so `recipes.json` treats the timer as optional — an absent timer means the job completes instantly. Every launch recipe has one; the schema allows for instant recipes without a rewrite.

### 5.3 Starting state

No cash. 1 wheat, 1 corn. Pre-placed: **1 Field, 1 Silo, 1 Order Board.**

---

## 6. Order Board

Free, pre-placed, always on the map. Map-only entry — no HUD button. Tap to open.

- **Slots:** 3, raised by player level and the `order.slots` effect.
- **Refill:** a fulfilled or skipped slot refills after a timer (~60s).
- **Expiry:** orders never expire on their own.
- **Skip:** an X on each order card. Single tap, no confirm, free. Slot immediately begins refilling.
- **Payout:** cash + XP, both derived from the requested ingredients' base values × a multiplier.
- **Orders may request more than the player currently holds** — that's the pull that drives production.

### 6.1 Generation

Procedural. Random pick from resources the player has unlocked a station for, quantity scaled to player level, weighted toward higher-tier goods as level rises.

**Wheat is excluded from the order pool** — it's the one good never sold as-is. Corn is sellable.

---

## 7. Storage

- **Per-resource caps.** Each resource has its own cap.
- **The Silo raises all caps at once** (one upgrade track, applies globally).
- Full storage blocks collection per §4.4 — never destroys anything.

---

## 8. Upgrades

Both kinds are **tiered**, with per-tier costs listed explicitly in JSON (no formula), and both emit Effects (§3).

- **Station upgrades** — bought in the station panel. Job speed, queue depth, output yield.
- **Universal upgrades** — bought at the **Workshop** (a building you place and tap). Global job speed %, global build cost %, order payout %, extra order slot.
- **Silo upgrades** — storage cap. Bought at the **Silo** (a building you place and tap). Raises every resource's cap at once.

---

## 9. XP & Levels

- **XP from:** collecting a job output, fulfilling an order, building a station, hatching an egg.
- **Curve:** explicit table in JSON, no formula. 20 levels for the prototype.
- **Unlocks:** station types and caps are **auto-granted**; upgrades become **purchasable**.
- World events unlock at level 5, and most individual events carry their own minimum level.

> A separate balance tool that reads/writes this JSON is planned. Out of scope here — see §14.

---

## 10. VoidPets

### 10.1 Acquisition

Eggs from level-up rewards, plus a small chance on order fulfillment (modifiable via `egg.chance`). Tap the egg in the hatch popup to reveal the pet.

**No duplicates** — a dupe roll is rerolled into an unowned species.

### 10.2 Species & traits

6 species for the prototype. Traits are **fixed per species**, authored in JSON, never rolled. 1 trait normally, 2 for rarer pets.

**Rarity:** Common / Rare / Epic. Higher rarity = more and stronger traits.

Placeholder species and traits to be invented into JSON and tuned after play.

### 10.3 Assignment

- One pet per station, assignable to generator stations only.
- Assign/unassign is free and instant.
- **An assigned pet auto-collects** — instantly on job completion, which is what unblocks the station (§4.4).
- The pet renders on top of its assigned station.

### 10.4 Range

Grid cells, **Manhattan distance**, radius from JSON.

### 10.5 Relationships

**Formation:** two assigned pets within range of each other show a heart icon over both heads. After ~30s of continuous proximity, a popup announces the friendship and the traits gained.

**Persistence:** once formed, permanent — survives separation. The *bonus* only applies while they're back in range (enforced by the trait's own `withinRange` condition).

**Multiplicity:** a pet can hold relationships with everyone in range. Bonuses stack per §3.5.

**Content — how the trait is generated:** each species carries an `affinity` field naming an effect type it pushes (Hard Worker → `speed`, Thrifty → `cost`). On befriending, **each pet gains a trait granting a `local.<partner's affinity>` bonus** — you get a taste of what your friend is good at, but only while near them. Magnitude from JSON, scaled by the rarer of the two.

The generated trait is an ordinary Trait (§3.1) with a `withinRange: <partnerId, n>` condition. Nothing special-cases it.

> Name pattern: *"Friendship with \<Pet\>"* → `local.speed +15%`, condition `withinRange: <thatPet>`.

---

## 11. World Events

Unlocked at level 5. Fire on a random interval from JSON (roughly every few minutes). Most events carry a minimum level.

- **Effects:** the same Effect vocabulary as everything else. No new mechanics.
- **Notification:** per-event, by type. Events with a real effect show a **popup the first time** (to explain), and a **toast every time after**. Flavor-only events are toast-only.
  - With no save (§13), "first time" resets each session — acceptable for now.

**Launch set:** 2 flavor-only events + **Dopamine Rain** — `global.speed +25%` for 2 minutes.

---

## 12. UI

### 12.1 HUD

| Element | Position | Behavior |
|---|---|---|
| Build menu button | bottom left | Tap to open/close |
| Money | top right | Tap opens total-resource popup, tap again to close |
| Level badge + XP bar | top center | |
| Debug menu button | top left | Tap to open/close |
| VoidPet menu button | bottom right | **Only appears once the player has their first VoidPet** |

### 12.2 Placement interaction

Drag-based, HayDay-style:

1. Open the build menu, **drag** a station out of it.
2. The menu **retracts** as you drag off it, so the whole screen is placeable.
3. Drop on a valid cell to place.
4. **Cancel** by dropping on an invalid cell, or by dragging back over the build menu button (which re-opens it).

**Moving an existing station:** long-press to pick up, drag, tap to confirm.

> Tap-then-tap doesn't work here — a ghost can't follow a finger that's been lifted.

### 12.3 Menus

- **Build menu** — available stations + money costs. Locked stations show a lock icon in grayscale.
- **VoidPet menu** — all collected pets; tap one for its details popup.

### 12.4 Popups

- **Level up** — congrats, new level, unlock list, reward list.
- **Hatch egg** — shows egg, tap to hatch, reveals pet.
- **VoidPet details** — picture, blurb, italicized quote in quotation marks, rarity, traits (procedurally described, §3.6), current station assignment.
- **Total resources** — every resource and the amount held.
- **Relationship formed** — the two pets, the traits gained.
- **Generic text popup** — data-driven dialogue window.
- **Event popup** — data-driven.
- **Toasts** — temporary, corner, data-driven.

### 12.5 Camera

Pan and pinch-zoom. Min/max zoom from JSON. Panning clamped to map bounds.

### 12.6 Placeholder art

Colored rect + text label per station. Progress bar while working. Bouncing icon when ready to collect. Distinct "storage full" state. Assigned VoidPet renders on top of its station.

### 12.7 Debug menu

Starting set — **grow this as we go, whenever a debug affordance would save time:**

add money · add resources · **level up** (grant exactly enough XP) · force-spawn egg · force-fire world event · reset

---

## 13. Session & Persistence

Session-only. No save/load for the prototype; localStorage later.

Timers do **not** advance while the tab is closed — but **timers are stored as absolute timestamps**, so offline progress is a small change later rather than a rewrite.

Reset lives in the debug menu.

---

## 14. Data Files

> **This inventory is proposed, not agreed.** The file split was never put to the user — the pitch says only "all data stored in JSON" and lists example categories. Treat the split as a starting point; the *contents* are all sourced.

All values are placeholders to be invented and then tuned by play. The scaffold's current `data/input.json`, `data/game.json`, `data/player.json` are superseded by this inventory.

| File | Holds |
|---|---|
| `game.json` | grid dimensions, cell size, camera min/max zoom, bounds |
| `resources.json` | resource ids, display names, base values, starting storage caps, `sellable` flag, **icon path** |
| `recipes.json` | inputs, outputs, optional timers, station type |
| `stations.json` | build costs, caps, footprint, unlock level, recipe ids, **sprite path**, placeholder color |
| `orders.json` | slot count, refill timer, payout multipliers, generation weights |
| `upgrades.json` | station + universal + silo tiers, costs, emitted effects |
| `levels.json` | XP thresholds, per-level unlocks and rewards |
| `xp.json` | XP granted per action |
| `voidpets.json` | species, rarity, blurb, quote, affinity, traits + effects, **sprite path** |
| `relationships.json` | formation time, range, affinity → effect magnitude table |
| `events.json` | world events, intervals, min levels, effects, notification type |
| `start.json` | starting cash, resources, pre-placed stations |

**Asset paths live in data, never in code.** The pitch asks for placeholders that are "easy to replace later," and `CLAUDE.md` names asset paths as a tunable. Every station, resource, and VoidPet carries its own sprite/icon path, so swapping placeholder art for real art is a JSON edit. Until art exists, stations render as a colored rect (§12.6) using `placeholderColor`.

**Wheat's order-pool exclusion is expressed once**, as `sellable: false` on wheat in `resources.json`. Order generation (§6.1) reads that flag. Do not duplicate it as a separate exclusion list in `orders.json` — one rule, one home.

---

## 15. Event Catalog

> **This catalog is engineering-derived, not agreed.** It was drafted from `CLAUDE.md`'s event-driven rule and the Job/Order naming convention the user approved (R1 #9). The individual event names and every payload shape are proposed. It's here because milestones that each invent their own event names is the single most likely way this decomposition goes wrong — but it hasn't been reviewed.

Systems talk only through the bus. Emitters describe **what happened**, never what should happen in response.

**Boot:** `data:loaded` · `game:started`

**Input intents** (emitted by scenes, never acted on by scenes):
`input:placeRequested {stationType, cell}` · `input:moveRequested {stationId, cell}` · `input:stationTapped {stationId}` · `input:jobQueueRequested {stationId, recipeId}` · `input:jobCancelRequested {stationId, jobIndex}` · `input:orderFulfillRequested {orderId}` · `input:orderSkipRequested {orderId}` · `input:upgradePurchaseRequested {upgradeId}` · `input:petAssignRequested {petId, stationId}` · `input:petUnassignRequested {petId}`

**Stations:** `station:built` · `station:moved` · `station:demolished` · `station:blocked {stationId, reason}`

**Jobs:** `job:queued` · `job:started` · `job:completed {stationId, outputs}` · `job:collected {stationId, outputs, byPet?}` · `job:cancelled`

**Economy:** `resource:changed {resource, delta, total}` · `storage:full {resource}` · `money:changed {delta, total}`

**Orders:** `order:generated {slot, order}` · `order:fulfilled {orderId, payout, xp}` · `order:skipped {orderId}` · `order:slotRefilled {slot}`

**Progression:** `xp:gained {amount, source}` · `level:up {level, unlocks, rewards}` · `unlock:granted {kind, id}`

**VoidPets:** `egg:granted {source}` · `egg:hatched {petId, species}` · `pet:assigned` · `pet:unassigned` · `relationship:forming {petA, petB, progress}` · `relationship:formed {petA, petB, traits}`

**World events:** `worldEvent:started {eventId, effects, duration}` · `worldEvent:ended {eventId}`

**Effects:** `effects:recalculated`

> Note there are no `ui:*` events. UI systems (toasts, popups) *listen* to domain events and decide for themselves whether to show something — per `CLAUDE.md`, an emitter never says `showToast`.

---

## 16. Deferred

Explicitly out of scope for the first build. Named here so nobody builds them early.

- **VoidPet Station** — from the original pitch: an area bonus to nearby VoidPets. Deferred, not cut (R2 #8). The `pet.*` effect types (§3.2) are what it will need; `local.*` is station-scoped and will not serve.
- **In-game tuning screen** — the pitch asked for a screen that writes JSON permanently. **A browser page cannot write to `data/`**; this needs a Vite dev-server plugin with a write endpoint. Deferred in favor of editing JSON directly (Vite hot-reloads). The eventual plan is a **separate balance app that exports JSON for copy/paste**, not an in-game screen.
- **Save/load** (§13), **offline timer progress** (§13).
- **Money sinks** beyond building and upgrading — "not right now" (R1 #8).

**Not deferred — decided:** there is no direct resource selling. Orders are the only cash source (R1 #7). That's a permanent economic decision, not a someday-item, and it's why the Order Board is load-bearing.

---

## 17. Open Items

- All numbers — costs, timers, XP values, level thresholds, payout multipliers, storage caps, trait magnitudes — are to be invented as placeholders into JSON and tuned by play. Explicitly approved (R1 #62).
- The 6 species, their traits, blurbs, quotes, and affinities are placeholders to be invented and rewritten after play (R2 #14).

**Genuinely unanswered — needs a decision:**

- **§15's event catalog and §14's file split** are proposed, not agreed (see the notes on each).
- **§4.5's tap-resolution rule** (collect-if-possible, else open panel) is a decision made while writing this spec, not one the user made. It resolves a real lockout but deserves a look.
