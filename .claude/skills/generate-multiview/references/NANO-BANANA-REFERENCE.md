# Nano Banana Pro Prompting Reference

Quick reference for prompting Nano Banana Pro (Gemini 3 Pro Image) effectively.

## Core Principle

**Image refs carry the design. Text prompts only describe what to change.**

When reference images are available, they are the primary signal. Keep the text prompt short — describe only the delta (camera angle, background, simplifications). Do NOT re-describe in text what the reference already shows. Long descriptive text competes with the image signal and produces generic output that ignores the ref.

**Write natural language, not tag soup.**

Nano Banana Pro is a "thinking" model that understands intent, physics, and composition. Brief it like a human artist, not a keyword matcher.

```
# BAD: Tag soup
woman, office, 4k, realistic, portrait, professional

# GOOD: Natural direction
A woman in a grey henchman jumpsuit sits at a cluttered cubicle,
phone pressed to ear, soft smile on her face. Warm afternoon light
streams through floor-to-ceiling windows behind her. Close-up framing,
shallow depth of field, photorealistic style.
```

## Prompt Structure

**[Subject + Adjectives]** doing **[Action]** in **[Location/Context]**. **[Composition/Camera]**. **[Lighting/Atmosphere]**. **[Style/Media]**.

### Six Essential Elements

1. **Subject**: Who/what with defining traits
2. **Action**: What's happening (even if subtle: "smiling", "leaning forward")
3. **Location**: Setting context
4. **Composition**: Framing, camera angle
5. **Lighting**: Light source, quality, mood
6. **Style**: Aesthetic, medium, quality level

## Composition Terms

| Term               | Meaning                                 |
| ------------------ | --------------------------------------- |
| `extreme close-up` | Eyes/mouth only, intense emotion        |
| `close-up`         | Face and shoulders                      |
| `medium shot`      | Waist up                                |
| `full shot`        | Entire body                             |
| `low angle`        | Camera below subject (power, dominance) |
| `high angle`       | Camera above subject (vulnerability)    |
| `dutch angle`      | Tilted frame (tension, unease)          |
| `eye level`        | Neutral, conversational                 |

## Technical Precision

Replace vague terms with specific instructions:

| Vague               | Precise                                       |
| ------------------- | --------------------------------------------- |
| "zoom"              | "85mm lens at f/2.8"                          |
| "good lighting"     | "three-point lighting, key at 45°, soft fill" |
| "blurry background" | "shallow depth of field, f/1.8, bokeh"        |
| "dramatic"          | "hard rim light, deep shadows, chiaroscuro"   |

## Character Consistency

Nano Banana Pro supports up to 14 reference images for consistency.

When using character refs:

- Explicitly state: "Match facial features exactly to reference image"
- Describe only what changes: expression, pose, action
- Don't re-describe static features already in the ref

## Multi-Reference Image Labeling

When passing multiple reference images, **label them explicitly** in your prompt to avoid ambiguity:

```
- Image 1: Princess Elara character reference
- Image 2: Confession booth location reference
- Scene: Head and shoulders, face dominant in frame. Camera directly in front.
  night lighting. Elara speaks to camera, shifting between composure and sarcasm.
```

For multi-character shots:

```
- Image 1: Princess Elara character reference
- Image 2: Stabitha character reference (sentient sword)
- Image 3: Rose ceremony hall location reference
- Scene: Stabitha SLAMS into Elara's outstretched hand. Glows purple-pink.
```

**Key practices:**

- **Specify which image contains what** — Prevents the model from guessing
- **Use explicit references** — "the character from Image 1" when clarity needed
- **Add brief descriptors** — "(sentient sword)" helps for non-obvious characters
- **Scene section last** — Camera, lighting, and action go in the Scene section

Without labels, the model may confuse which reference is which, especially with multiple characters or when character/location refs look similar.

## Portrait Optimization (9:16)

For vertical portrait format:

- **1-2 characters max** — more creates composition issues
- **Favor close-ups and medium shots** — full body wastes vertical space
- **Subject in upper 2/3** — leave room for text overlays if needed
- **Vertical elements** — doorways, windows, standing figures work well

## Lighting Quick Reference

| Mood            | Lighting Setup                  |
| --------------- | ------------------------------- |
| Warm, domestic  | Soft key from window, warm fill |
| Office/neutral  | Overhead fluorescent, flat fill |
| Dramatic/tense  | Hard side light, deep shadows   |
| Heroic/powerful | Low key with rim light          |
| Vulnerable      | Soft top light, minimal shadows |
| Horror/unease   | Underlit, green/blue cast       |

## Editing, Not Re-rolling

If an image is 80% correct, use conversational edits:

- "Change the tie to green"
- "Move the character slightly left"
- "Make the expression more concerned"
- "Darken the background shadows"

Use spatial language:

- "In the background..."
- "On the left side..."
- "The object in her hand..."

## ⚠️ Multi-Panel/Triptych Issue

**Problem**: Nano Banana Pro can interpret complex prompts as requests for multi-panel outputs (triptychs, grids, collages). This is a feature of the model for "complex compositions with character consistency across multiple moments."

**Solution**: Our `generateImage()` function automatically appends anti-panel language to all prompts:

```
Single unified image composition. Do not create a collage, grid, triptych, diptych, or multi-panel layout.
```

If you still get paneled shots, strengthen the prompt with:

- Explicit framing: `"close-up portrait"`, `"single full-body shot"`
- Single-shot terms: `"single frame"`, `"one continuous image"`
- Avoid: `"sequence"`, `"different angles"`, `"multiple views"`, `"showing X and Y"` (can trigger panels)

## Multi-View / Turnaround Sheets

**Individual-view rotation is NOT supported** — Nano Banana cannot rotate a subject to a different viewing angle when given a single reference image. The reference matching signal overwhelms any angle directives in the text prompt.

**However, turnaround sheets DO work.** When given a layout template (2x2 grid with labeled quadrants) alongside a subject reference, Nano Banana can generate a composed turnaround sheet showing front, right, back, and left views in a single image. The `generate-multiview` skill's `nb-turnsheet` method automates this:

1. Auto-generates a 2x2 layout template
2. Calls Nano Banana once with source image + layout template as references
3. Splits the resulting sheet into 4 individual view PNGs

### When to Use Each Method

| Method         | Best For                                         | Skill                |
| -------------- | ------------------------------------------------ | -------------------- |
| `nb-turnsheet` | **Subjects** (characters, props, vehicles)       | `generate-multiview` |
| `qwen`         | **Scenes/locations** (environments, backgrounds) | `generate-multiview` |

For scenes where the camera orbits a location, use **Qwen Multi-Angle** (`method: "qwen"` in `generate-multiview`), which has a LoRA specifically trained for camera rotation.

## Anti-Patterns to Avoid

- **Tag soup**: "woman, office, 4k, realistic, portrait" — no context, random results
- **Vague adjectives**: "professional photo", "good lighting" — underspecified
- **Re-rolling for small fixes**: Use edit instructions instead
- **Omitting purpose**: Tell the model what this is for (product shot, emotional scene, etc.)
- **Multiple subjects without framing**: Can trigger triptych mode - always specify single composition

## Technical Limits

- **Resolution**: Native up to 4K
- **Reference images**: Up to 14 (6 with high fidelity)
- **Text rendering**: Excellent for headlines, logos, signs
- **Aspect ratios**: Any standard ratio, specify explicitly

## Sources

- [Nano Banana Pro Prompting Tips (Google Blog)](https://blog.google/products/gemini/prompting-tips-nano-banana-pro/)
- [Nano Banana Pro Strategies (DEV Community)](https://dev.to/googleai/nano-banana-pro-prompting-guide-strategies-1h9n)
- [Generating Consistent Imagery with Gemini (Towards Data Science)](https://towardsdatascience.com/generating-consistent-imagery-with-gemini/)
