# Video Generation Guide

Guide for generating video assets (idle animations, transitions, character movements) using the api-fal skill.

## Which Model Should I Use?

### Kling 2.6 Pro — Loops and Transitions

**Input requirements:** Images must be at least **300×300 pixels**. Smaller images (e.g. 64×64 pixel art) must be upscaled first via `util-upscale`.

Use when you need:
- **Idle loops** — Character breathing, weight shifting, blinking
- **Seamless loops** — Set `endImagePath = startImagePath`
- **Start-to-end transitions** — Provide both start and end frames
- **5 or 10 second clips**

Strengths: Clean loops, consistent character appearance, good at subtle motion.

### WAN 2.6 — Complex Motion

Use when you need:
- **More dynamic motion** — Walking, running, action sequences
- **Longer clips** — Up to 15 seconds
- **Seed control** — Reproducible results
- **Complex environmental motion** — Particles, weather, lighting changes

Strengths: Natural-looking complex motion, longer durations, seed reproducibility.

### WAN FLF2V — Controlled A→B Transitions

Use when you need:
- **Precise start and end states** — Door opening, item pickup, state change
- **Guaranteed endpoints** — Both first and last frames are exact
- **Morphing effects** — Character expression changes, object transformations

Strengths: Exact control over start and end frames, reliable interpolation.
Fixed ~5 second duration.

## Decision Tree

```
Need a seamless loop? ─── Yes ──→ Kling 2.6 Pro (start = end)
         │
         No
         │
Need exact start AND end frames? ─── Yes ──→ WAN FLF2V
         │
         No
         │
Duration > 10 seconds? ─── Yes ──→ WAN 2.6 (up to 15s)
         │
         No
         │
Complex motion (walking, actions)? ─── Yes ──→ WAN 2.6
         │
         No
         │
Simple motion from single image ──→ Kling 2.6 Pro
```

## Prompt Writing Rules

All prompts must use **literal, physical descriptions only**. No metaphors.

### Good Prompts

- `"subtle breathing, chest rising and falling, slight weight shift from left foot to right foot"`
- `"character turns head 30 degrees to the left, blinks twice"`
- `"door handle rotates downward 45 degrees, door swings open away from camera"`
- `"leaves fall from tree, wind blows left to right, clouds move slowly"`

### Bad Prompts (Avoid These)

- `"character comes alive"` — Model interprets literally, may generate resurrection scene
- `"breathing life into the scene"` — Confusing, non-physical
- `"the warrior radiates power"` — Generates glowing effects
- `"feeling the breeze"` — Abstract, no physical motion described

### Idle Animation Prompts

For character idle loops, describe specific physical movements:

```
"subtle breathing, chest expanding and contracting slightly,
slight weight shift, gentle head tilt, minimal movement,
standing in place, idle stance"
```

Negative prompt:
```
"large movements, walking, running, jumping, aggressive motion,
camera movement, zoom, pan, sudden changes"
```

## Negative Prompts by Use Case

### Idle Loops
```
large movements, walking, running, jumping, aggressive motion,
camera movement, zoom, pan, sudden changes, text, watermark
```

### Action Sequences
```
static, frozen, no movement, text, watermark, blurry,
low quality, distorted face, extra limbs
```

### Environmental
```
text, watermark, people appearing, objects spawning,
sudden scene changes, glitch, artifacts
```

## Common Patterns

### Idle Loop (Kling)
```typescript
generateVideoKling26Pro({
  prompt: "subtle breathing, slight weight shift, gentle idle stance, minimal movement",
  startImagePath: "portrait.png",
  endImagePath: "portrait.png",  // Same image = seamless loop
  negativePrompt: "large movements, walking, camera movement",
  durationSec: 5,
  outputPath: "out/",
})
```

### Character Action (WAN)
```typescript
generateVideoWAN26({
  prompt: "character walks forward three steps, arms swinging naturally at sides",
  imagePath: "character.png",
  negativePrompt: "static, frozen, text, watermark",
  durationSec: 5,
  outputPath: "out/",
})
```

### State Transition (WAN FLF2V)
```typescript
generateVideoWANFLF2V({
  prompt: "treasure chest lid opens, gold coins visible inside, soft glow",
  firstFramePath: "chest_closed.png",
  lastFramePath: "chest_open.png",
  negativePrompt: "text, watermark, sudden changes",
  outputPath: "out/",
})
```

## Integration with Asset Bot

Video assets use the `mp4` output format and can have these metadata fields:
- `durationSec` — Video duration
- `fps` — Frames per second
- `looping` — Whether the video loops seamlessly

Video generation APIs in asset-type schemas:
- `kling-video` — Kling 2.6 Pro
- `wan-video` — WAN 2.6
- `wan-flf2v` — WAN First-Last-Frame

## Retry Behavior

All video functions use exponential backoff retry (3 attempts, 1s/2s/4s delays).
Video generation can take 30-120 seconds per call depending on duration and resolution.

## Cost Considerations

- Kling 2.6 Pro: Higher cost per second, best quality for loops
- WAN 2.6: Moderate cost, good balance for general video
- WAN FLF2V: Lower cost, fixed short duration

Generate candidates at 480p first to evaluate, then regenerate winners at 720p.
