# WAN 2.6 Prompting Reference

Quick reference for prompting WAN 2.6 video generation effectively.

**This document is the authority for WAN prompt construction.** All skills that generate WAN prompts (especially `generate-shots`) must follow these patterns.

## Episode Schema → WAN Mapping

Episode shots have separate fields for static vs temporal content:

| Episode Field | WAN Usage              | Purpose                                    |
| ------------- | ---------------------- | ------------------------------------------ |
| `composition` | NOT sent to WAN        | For Nano Banana keyframe (static frame 1)  |
| `motion`      | Motion prompt input    | What changes during the shot               |
| `dialogue`    | Audio + motion context | Lip-sync + auto-generated delivery phrases |

**Why this split?**

- WAN receives the keyframe IMAGE (from `composition`)
- WAN receives the motion TEXT (from `motion`)
- No need to re-describe what's already in the image

## Prompt Structure

Follow this arc: **Subject → Motion/Dialogue → Camera → Style**

This aligns with the official Alibaba formula: `Subject + Scene + Motion + Aesthetic Control + Stylization`

For **image-to-video** (our primary mode), focus on temporal changes—don't re-describe what's in the keyframe.

```
[Character action/dialogue]. [Camera movement]. [Lighting shifts]. [Mood].
```

## Prompt Extension

WAN uses **LLM-based prompt expansion** by default (`prompt_extend: true` on fal.ai). The model automatically enriches simple prompts with cinematic detail.

**Implication**: Don't over-engineer prompts. Clear, simple descriptions work well:

- ✅ "Camera moves closer to subject" → Model expands to appropriate cinematography
- ❌ "Smooth cinematic dolly push-in with shallow depth of field bokeh" → Redundant, may conflict

The official docs state: "Extending the prompts can effectively enrich the details in the generated videos, further enhancing video quality."

When `prompt_extend: false`, be more explicit with camera and motion terms.

## Dialogue Shots (With Audio)

When a shot has dialogue, **include the dialogue in the motion prompt**. WAN 2.6 uses audio input for lip-sync, but the prompt should reinforce who is speaking and what emotion to convey.

### Dialogue Prompt Format

```
[Character description] says [delivery]: '[Short dialogue excerpt]'. [Motion cues]. [Camera].
```

### Examples

```
# GOOD - Dialogue in prompt
The woman speaks expressively: 'I can't believe you did that.'
Subtle upper body movement, natural hand gestures. Static camera.

# GOOD - With delivery notes
He says quietly, leaning forward: 'We need to leave now.'
Slight weight shift, focused expression. Gentle push-in.

# GOOD - Multi-character (specify who talks)
Close-up. The man speaks firmly: 'This ends tonight.'
Jaw tenses, eyes narrow. Subtle camera drift.

# BAD - No dialogue context (what we used to do)
Subtle natural movement, breathing, blinking, expression changes.
```

### Dialogue Best Practices

| Guideline              | Details                                               |
| ---------------------- | ----------------------------------------------------- |
| **Quote key words**    | Include 3-8 words from the dialogue line              |
| **Specify speaker**    | "The woman says" not just "speaking"                  |
| **Add delivery notes** | "speaking quietly", "calling out", "trembling voice"  |
| **Keep it short**      | Under 10 words of dialogue for 5s clips               |
| **One speaker focus**  | For multi-speaker shots, focus on the primary speaker |

### Audio Requirements for Lip-Sync

| Requirement  | Spec                                                   |
| ------------ | ------------------------------------------------------ |
| **Format**   | WAV 16-bit 16kHz mono preferred (MP3 acceptable)       |
| **Duration** | 8-20s for reliable native sync; >20s may drift         |
| **Quality**  | Clear, dry audio; remove background music              |
| **Loudness** | Stable levels (~-16 LUFS for voice)                    |
| **Face**     | Frontal, neutral expression, well-lit, no obstructions |

### Motion for Speaking Characters

Low-to-medium motion strength prevents "rubbery" lips:

- "Subtle upper body movement"
- "Natural gestures while speaking"
- "Slight weight shift, focused expression"
- "Listens intently, subtle reactions" (for non-speaking character)

### ElevenLabs Tag → WAN Mapping

Dialogue lines contain `[tags]` for ElevenLabs TTS. These are auto-parsed at generation time
to build WAN motion prompts. See `src/utils/dialogue-tags.ts`.

| ElevenLabs Tag               | WAN Motion Phrase            |
| ---------------------------- | ---------------------------- |
| `[quietly]`, `[whispers]`    | "speaks softly"              |
| `[shouts]`, `[screams]`      | "speaks forcefully"          |
| `[slowly]`                   | "speaks deliberately"        |
| `[flatly]`, `[deadpan]`      | "speaks flatly"              |
| `[sad]`                      | "speaks sadly"               |
| `[sarcastic]`                | "speaks sarcastically"       |
| `[nervously]`, `[hesitates]` | "speaks nervously"           |
| `[curious]`                  | "speaks curiously"           |
| `[sighs]`                    | "sighs" (action, prepended)  |
| `[laughs]`, `[chuckles]`     | "laughs" (action, prepended) |
| `[pauses]`                   | (ignored - TTS handles)      |

**Auto-generated prompt example:**

```
Input line: "[quietly] They call it The Season... [sighs]"
Output: "The woman sighs, speaks softly: 'They call it The Season...' Subtle gestures, natural expression."
```

Speaker descriptions are gender-aware: "The man", "The woman", or "The character" (fallback for non-binary/unknown).

## Action Shots (No Dialogue)

For shots without dialogue, `shot.motion` becomes the primary WAN input:

```json
{
  "composition": "Character at window, tense posture, rain on glass",
  "motion": "Turns suddenly, surprised expression forming. Hair sways with movement."
}
```

WAN receives: `"Character turns suddenly, surprised expression forming. Hair sways with movement. [Camera movement]."`

To explicitly block unintended speech in action shots, add to negative prompt:

```
dialogue, mouth movement, lip sync, talking, speaking
```

## Silence Padding

Silence padding controls timing around speech in dialogue and narration shots.

### Dialogue Shots

For natural lip-sync timing, dialogue audio is padded with silence before sending to WAN:

| Padding          | Default | Purpose                                               |
| ---------------- | ------- | ----------------------------------------------------- |
| **Pre-silence**  | 1.0s    | Character appears on screen before speaking           |
| **Post-silence** | 0.25s   | Mouth closes naturally, motion continues after speech |

**Without padding:**

- Frame 1: Character already speaking (jarring start)
- Last frame: May have lingering mouth movement or abrupt cut

**With padding:**

- Frames 1-24: Character present, not speaking yet
- Dialogue begins naturally after 1 second
- Speech ends, 0.25s of natural motion follows
- Clean cut point with closed mouth

### Narration Shots

Narration shots default to **no padding** (audio starts immediately). Use `targetDurationSec` for centered audio placement:

```json
{
  "id": "shot-02",
  "targetDurationSec": 15,
  "composition": "Cat sitting on windowsill...",
  "motion": "Rain trails down glass...",
  "narration": {
    "characterId": "narrator",
    "text": "Have patience with everything unresolved..."
  }
}
```

Result with 6s audio: `4.5s pre + 6s narration + 4.5s post = 15s`

### Per-Shot Override

In `episode.json`, override defaults for specific shots:

```json
{
  "id": "shot-01",
  "preSilenceSec": 2.0,
  "postSilenceSec": 0.5,
  "composition": "...",
  "motion": "...",
  "dialogue": [...]
}
```

Use cases:

- **Dramatic entrance**: `preSilenceSec: 2.0` - character appears, builds tension
- **Lingering reaction**: `postSilenceSec: 1.0` - hold on expression after line
- **Quick cut**: `preSilenceSec: 0, postSilenceSec: 0` - no padding (rare)
- **Centered narration**: `targetDurationSec: 15` - auto-calculate equal pre/post padding

### Priority Logic

1. **Both `preSilenceSec` AND `postSilenceSec` set**: Use explicit values, ignore `targetDurationSec`
2. **`targetDurationSec` set**: Calculate padding (centered, or with explicit pre/post)
3. **Only `preSilenceSec` or `postSilenceSec` set**: Use that value + default for other
4. **Nothing set**: Dialogue defaults (1.0s/0.25s) or Narration defaults (0s/0s)

### Overflow Handling

If total duration (audio + padding) exceeds 15s, padding is prorated:

- Requested: 5s audio + 6s pre + 6s post = 17s
- Available padding: 15s - 5s = 10s
- Result: 5s pre + 5s post (preserves ratio)

### Implementation Note

Silence is applied **per-segment** after audio slicing:

- First segment of shot: gets pre-silence
- Last segment of shot: gets post-silence
- Middle segments (for >15s audio): no padding

This preserves line boundary accuracy for smart segmentation.

## Image-to-Video Mode

When animating from a keyframe:

- Focus on **temporal changes**: camera motion, lighting shifts, character animation
- Don't re-describe existing image elements
- Describe the **motion arc**, not the static state

```
# BAD: Describing the image
A woman sits at a desk with a phone.

# GOOD: Describing the motion
Character shifts weight, slight smile forming. Subtle camera push-in.
Warm light shifts as clouds pass window.
```

## Camera Movement Terms

Use explicit directional language:

| Term                 | Meaning                          | Notes                                           |
| -------------------- | -------------------------------- | ----------------------------------------------- |
| `push` / `push-in`   | Move toward subject              |                                                 |
| `pull` / `pull-back` | Move away from subject           |                                                 |
| `pan`                | Horizontal rotation              | Keep gentle: 10-20° max to avoid warping        |
| `tilt`               | Vertical rotation                | Specify start/end: "tilt up from waist to face" |
| `orbit`              | Circle around subject            |                                                 |
| `track`              | Follow subject movement          |                                                 |
| `dolly`              | Smooth forward/backward on track | More predictable than "zoom"                    |
| `crane`              | Vertical movement up/down        |                                                 |
| `static`             | No camera movement               | Best for dialogue close-ups                     |

### Motion Intensity Modifiers

- **Minimal**: "subtle drift", "gentle movement", "slight breathing"
- **Moderate**: "smooth track", "flowing motion", "steady push"
- **Dramatic**: "dynamic sweep", "rapid movement", "energetic pan"

## Subject Description

Be specific about the subject:

- Physical traits: "a man with a scarred face in a grey hoodie"
- NOT character names: "Gary" (model doesn't know who Gary is)
- Action state: "leaning forward conspiratorially", "frozen mid-laugh"

### Preventing Identity Drift

To keep characters consistent across shots:

- **Use consistent labels**: Pick one descriptor and stick with it ("the detective", "the woman in red")
- **Don't switch labels**: Avoid "woman" → "girl" → "person" across shots
- **Anchor with 3-5 stable identifiers**: hair color/style, clothing color, distinctive accessory
- **Repeat key traits**: If she has "curly auburn hair", mention it in each shot prompt

## Duration Constraints

| Duration | Use Case                                               |
| -------- | ------------------------------------------------------ |
| `5s`     | Quick reactions, short dialogue, transitions           |
| `10s`    | Standard dialogue exchanges, most shots                |
| `15s`    | Monologues, held emotional moments, establishing shots |

## Aspect Ratio

Always specify for portrait video:

```
9:16 vertical portrait format
```

## Lighting & Style

Include lighting direction:

- "warm key light from window"
- "harsh overhead fluorescent"
- "golden hour backlighting"
- "dramatic rim lighting"

Include quality markers:

- "photorealistic, cinematic, high detail"
- "film grain, shallow depth of field"

## Negative Prompts

Use to prevent common artifacts (max 500 chars):

```
low quality, blurry, distorted faces, unnatural movement,
text, watermarks, shaky camera, static poses, T-pose,
mannequin pose, stiff posture, black bars, letterboxing
```

## Multi-Motion Priority

One primary motion beats many conflicting motions:

- **Primary motion**: "character turns head slowly"
- **Secondary motion** (optional): "hair sways gently"
- **Freeze what should stay static**: "background remains stable", "face stays consistent"
- **Avoid**: "character jumps while waving and spinning" (too many competing actions)

When motion gets chaotic, simplify to one action and explicitly freeze everything else.

## Failure Modes & Fixes

Common problems and how to address them:

| Problem                    | Symptoms                                            | Fix                                                                                                |
| -------------------------- | --------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| **Subject morphs**         | Face changes between frames, identity drifts        | Add stronger subject anchors (3-5 identifiers), use consistent labels, add "face stable" to prompt |
| **Too much chaos**         | Multiple competing movements, unpredictable results | Remove extra actions, declare one primary motion, use simpler camera (static or slow dolly)        |
| **Pan looks warped**       | Distortion during horizontal camera movement        | Reduce pan angle to 10-15°, add "keep horizon level", add "keep subject centered"                  |
| **Handheld is nauseating** | Excessive shake, motion sickness                    | Replace "handheld" with "steady" or "stabilized", or specify "subtle micro-shake" only             |
| **Rubbery lips**           | Unnatural mouth movement during dialogue            | Use low-to-medium motion strength, simpler body movement, static camera                            |
| **Background instability** | Environment shifts or morphs                        | Add explicit freeze: "background remains stable", reduce overall motion                            |

### Stability Constraints

Add a constraints phrase at the end of motion prompts to prevent artifacts:

- "Keep face stable and consistent"
- "Avoid warping, extra limbs, flicker"
- "Background remains stable"
- "Horizon level, no camera jitter"

Keep constraints short—too many can dilute the look.

## Multi-Shot Mode (WAN 2.6)

WAN 2.6 supports generating internal scene transitions within a single shot. This is used for establishing shots and action sequences that benefit from multiple beats.

**Constraints:**

- Works with action shots AND narration shots
- NOT for dialogue shots (lip-sync requires single continuous video)
- Duration must be 5-10s (I2V multi-shot limit)
- 2-4 segments per shot
- Each segment: 2-5s

### Multi-Shot Prompt Format

Following fal.ai's guide, timestamps have no colon:

```
[Atmosphere from composition]

Shot 1 [0-3s] [segment.motion]. [segment.camera].
Shot 2 [3-7s] [segment.motion]. [segment.camera].
Shot 3 [7-10s] [segment.motion]. [segment.camera].
```

### Example

```
Warm dawn light on city skyline, atmospheric haze.

Shot 1 [0-3s] Wide view, subtle light shift. Static.
Shot 2 [3-7s] Push toward central tower. Dolly forward.
Shot 3 [7-10s] Orbit to reveal harbor, boats. Orbit right.
```

### How It Works

Claude provides `multiShotSegments` during script writing. At generation time:

1. Segments are validated (continuous, complete, 5-10s total)
2. `buildMultiShotWANPrompt()` formats the prompt with timestamps
3. WAN is called with `multiShots: true`
4. On failure, falls back to single-shot using first segment

### Best Practices

- Use for establishing shots, action sequences, time passages
- Each segment gets its own camera instruction
- Variable segment durations (2-5s each) based on motion complexity
- When using `multiShotSegments`, the top-level `motion` field is ignored

## Technical Limits

- **Resolution**: Up to 1080p (no 4K)
- **Duration**: 5, 10, or 15 seconds only
- **Prompt length**: 800 characters max (text-to-video)
- **Negative prompt**: 500 characters max
- **Image input**: Max 100MB, 360-2000px dimensions

## Resolution Quirks (720p 9:16)

WAN's "720p" output is **not** standard 720x1280. Observed behavior:

| Input                      | Output   | Notes                |
| -------------------------- | -------- | -------------------- |
| 768x1376 (Nano Banana)     | 716x1284 | Scaled down, cropped |
| 716x1284 (extracted frame) | 716x1286 | +2px height drift    |

**Problem**: Dimension drift across chained segments causes visual glitches (vertical stretch on first frame).

**Solution**: Normalize all inputs to 720x1280 (divisible by 16) using center-crop before sending to WAN. See `resizeImageForWAN()` in `assemble-episode.ts`.

Per [Alibaba Cloud docs](https://www.alibabacloud.com/help/en/model-studio/image-to-video-api-reference): output dimensions must be divisible by 16, and "the final aspect ratio may have a slight deviation."

## Sources

### Official Alibaba Documentation

- [WAN Video Generation Prompts Recipe](https://www.alibabacloud.com/blog/model-studio-wan-video-generation-prompts-recipe_602777) — Canonical prompt formulas from Alibaba
- [WAN Image-to-Video API Reference](https://www.alibabacloud.com/help/en/model-studio/image-to-video-api-reference) — Parameters, limits, technical specs
- [GitHub: Wan-Video/Wan2.1](https://github.com/Wan-Video/Wan2.1) — Official model repository
- [GitHub: Wan-Video/Wan2.2](https://github.com/Wan-Video/Wan2.2) — Latest model with camera control

### fal.ai Guides

- [WAN 2.6 Prompt Guide (fal.ai)](https://fal.ai/learn/devs/wan-2-6-prompt-guide-mastering-all-three-generation-modes)
- [WAN 2.6 Developer Guide (fal.ai)](https://fal.ai/learn/devs/wan-26-developer-guide-mastering-next-generation-video-generation)

### HuggingFace

- [Wan2.1-T2V-14B Model Card](https://huggingface.co/Wan-AI/Wan2.1-T2V-14B)
- [How To Prompt WAN Models (Community Guide)](https://huggingface.co/blog/MonsterMMORPG/how-to-prompt-wan-models-full-tutorial-and-guide)
