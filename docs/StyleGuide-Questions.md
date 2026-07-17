# VoidDay — Style Guide Questions (Round 1)

**How to answer:** Write your answer under each question. Writing `d` accepts the recommended default. Most should be `d`; the few you change are the ones that matter. Hand the file back when done.

The defaults below are written to hang together as **one coherent direction** — if you `d` everything, you get the *"cozy-cosmic"* look described in Q1. Change Q1 and several later defaults shift with it, so start there.

---

## A. Tone & Direction *(highest leverage — do these first)*

### Q1. The Void tension — what IS the register?
The game is HayDay-cozy, but everything is "Void" (VoidDay, VoidPets, Void eggs). That pull between *cozy farm* and *void* is the core art question. Where does it land?

- **(a) Cozy-cosmic (DEFAULT).** Warm, charming farm that happens to float in a soft, dreamy void/space. VoidPets are cute, glowing void-creatures (think friendly blobs/slimes/wisps), not scary. The "void" reads as *whimsical-mysterious and inviting*, never threatening. Broadest appeal, closest to HayDay's warmth while earning the name.
- **(b) Eerie-cozy.** Genuinely a little unsettling under the cuteness — muted colors, an uncanny calm, pets that are endearing but slightly *off*. The name is a promise of mild dread. More distinctive, narrower appeal, more art-direction risk.
- **(c) Pure cozy, "Void" is just a name.** Ignore the theming entirely; straight bright HayDay farm, pets are ordinary cute animals. Safest, but throws away the one hook that distinguishes this from HayDay.

**Answer:** `d`

### Q2. Reference works — what to take from each
Default references and the specific thing to borrow:

- **HayDay** — chunky readable buildings, generous rounded UI, satisfying collect-pop feedback, warm daylight palette. *(Take the readability & juice; not its realistic-illustrated texturing.)*
- **Ooblets / Slime Rancher** — friendly blobby creature design, bouncy personality, appealing simple 3D forms. *(Take the creature charm & bounce.)*
- **Cosmic/void accent from something like Cult of the Lamb's palette (the pretty parts, not the gore)** — a single glowing otherworldly hue used sparingly. *(Take the one-accent-glow discipline; not the darkness.)*

**Answer (add/remove/swap any):**

Yes to HayDay and Cosmic/Void accent. NO to creature design. In fact, we actually already have the exact creature designs because it's an existing IP. I just need to convert them from 2D to 3D.

---

## B. Technique & Rendering

### Q3. Shading model (URP lit is already fixed by spec §12.6 — this is *how* it's lit)
- **(a) Flat / toon-ish lit (DEFAULT).** URP lit but low-spec: soft ambient, one key light, minimal specular, colors read as clean flat-ish fills. Cheap, cohesive with primitives, forgiving of untextured meshes.
- **(b) Full PBR / realistic-ish.** Metallic/smoothness, shadows, the HayDay-illustrated look in 3D. More setup, worse with primitives, slower to first-playable.

**Answer:** `d`

### Q4. Outlines?
- **(a) No outlines (DEFAULT).** Rely on silhouette + tint. Simplest; add later if forms don't read.
- **(b) Yes — a soft dark outline** (toon/ink look) for pop and readability under the tilted camera.

**Answer:** `d`

### Q5. Shape language
- **(a) Round & soft (DEFAULT).** Rounded buildings, blobby pets, soft corners everywhere. Reinforces cozy; matches the creature refs.
- **(b) Mixed** — soft pets, chunkier/more angular stations for a built-vs-alive contrast.

**Answer:** `d`

### Q6. Post-processing / "void glow"
- **(a) Subtle bloom on void elements only (DEFAULT).** A gentle bloom so pets, eggs, and void-events (e.g. Dopamine Rain) glow; the farm itself stays grounded. This is what sells "cosmic" cheaply. URP Volume, no per-asset work.
- **(b) None for now.** Add in a polish pass.
- **(c) Heavier atmosphere** — vignette, color grading, the whole scene feels otherworldly.

**Answer:** `d`

---

## C. Color

### Q7. Base environment palette (the ground/void the farm sits on)
- **(a) Warm grass on a deep-but-soft void (DEFAULT).** Sunny green farm tiles/ground; the surrounding "world" is a soft dark-violet/indigo star-field rather than a blue sky. Cozy island-in-the-void read.
- **(b) Full bright daylight** — blue sky, no void backdrop (pairs with Q1c).
- **(c) Muted/desaturated** (pairs with Q1b eerie).

**Answer:** `d`

### Q8. The single Void accent hue
One glowing otherworldly color, reserved for void things (pets' glow, eggs, relationship hearts?, void world-events). Default: **cyan-violet glow (~`#8B5CF6` violet ↔ `#22D3EE` cyan range)**. Reserving it to void elements is what makes them feel special.

**Answer (name a hue or `d`):** `d`

### Q9. Station tint scheme (spec §12.6 says each station type gets a `placeholderColor` — this sets the scheme)
- **(a) Warm naturalistic-by-function (DEFAULT).** Field = green, Henhouse = warm tan/red, Pasture = grassy, Creamery = cream/white, Bakery = golden-brown, Silo = grey-metal, Workshop = steel-blue, Order Board = wood-brown. Readable, intuitive.
- **(b) Bright arbitrary distinct palette** — max distinguishability, less thematic.

**Answer:** `d`

### Q10. State-signal colors (must be consistent everywhere)
Default mapping:
- **Ready to collect** → the void accent glow + bounce (draws the eye to what needs a tap).
- **Working** → neutral progress bar (white/green fill on dark track).
- **Storage-full / blocked** → warning amber/red tint + icon.
- **Locked** → desaturated grayscale + lock icon (spec §12.3).
- **Valid / invalid placement ghost** → green tint / red tint (spec §12.2).

**Answer (adjust any):** `d`

---

## D. Motion

### Q11. Easing character
- **(a) Snappy & bouncy (DEFAULT).** Quick overshoot-and-settle on pops, collects, placements. HayDay/Ooblets juice. Matches round shapes.
- **(b) Smooth & gentle.** Calmer, slower easing.

**Answer:** `d`

### Q12. Idle life
- **(a) Alive (DEFAULT).** Assigned VoidPets do a small idle bob/breathe; stations are static. Cheap charm, focuses motion on the characters.
- **(b) Fully static** until acted on (faster, less charming).

**Answer:** `d`

---

## E. Type & UI

### Q13. UI chrome character
- **(a) Chunky, rounded, tactile (DEFAULT).** HayDay-lineage: fat rounded panels, big rounded buttons, soft drop shadows, generous padding. Reads great on a phone, thumb-friendly.
- **(b) Flat & minimal.** Thin, modern, less playful.

**Answer:** `d`

### Q14. Typeface direction
- **(a) Rounded friendly sans (DEFAULT).** A soft geometric/rounded sans (e.g. Baloo / Fredoka / Nunito family feel) for that cozy-cosmic warmth. One family, 2–3 weights.
- **(b) Something with more character** (specify), e.g. a slightly quirky display face for headers + clean sans for body.

**Answer:** `d`

### Q15. Iconography style
- **(a) Filled, rounded, single-weight (DEFAULT).** Chunky filled icons matching the UI; resource icons as simple rounded 3D-ish or flat symbols. Consistent with chrome.
- **(b) Outlined/line icons.**

**Answer:** `d`

---

## F. Audio *(you already confirmed SFX + music are in scope)*

### Q16. Music direction
- **(a) Cozy-cosmic ambient loop (DEFAULT).** Warm, gentle, slightly dreamy/spacey background loop — mellow, non-intrusive, loops forever. One track for the prototype.
- **(b) Upbeat farm-sim tune** (brighter, more melodic, HayDay-ish).
- **(c) Adaptive/layered** (out of scope for prototype — flag if you want it later).

**Answer:** `d`

### Q17. SFX register
- **(a) Soft, rounded, "poppy" (DEFAULT).** Gentle pops/chimes/whooshes — satisfying collect-pop, soft UI taps, a sparkly hatch/level-up chime, a whooshy void-event sting. Nothing harsh or realistic. One SFX per distinct player action / system event (driven off the §15 event catalog).
- **(b) More realistic/foley** farm sounds.

**Answer:** `d`

### Q18. Feel (MoreMountains) — captured, not blocking
Noted from our chat: you're considering **MoreMountains Feel** for juice. Recommendation on record: **hold off until a dedicated "juice" milestone**, hand-roll the §12.6 tweens now (Feel is inspector-wired and doesn't speed up first-playable; adopting it later is an additive swap, not a rewrite). This question just confirms the plan.

- **(a) Agree — defer Feel, hand-roll placeholder juice now (DEFAULT).**
- **(b) Adopt Feel from the start** anyway.

**Answer:** `d`

---

## G. Anything else
Freeform: any reference image, existing VoidPet sketch, color you love/hate, or hard constraint I should know?

**Answer:**
I will provide some voidPet examples, ask me for them.
