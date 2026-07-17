---
name: generate-audio
description: >
  Generate game audio assets including sound effects, music, and voice lines.
  Use when the user wants to create SFX, background music, fanfares, jingles,
  victory stingers, or character speech. Wraps ElevenLabs with candidate
  workflow, variant generation, and voice discovery.
metadata:
  assetBot:
    commands:
      - asset-bot generate audio
    references:
      - ../../../references/CLI-REFERENCE.md
---

# generate-audio

Audio generation orchestrator for game assets. Sits above `api-elevenlabs` and adds candidate workflow, variant generation, and voice discovery.

**Use this skill for generating game audio assets.** Use `api-elevenlabs` directly only for configuration, voice design/cloning, and testing.

## CLI Commands

```bash
# Generate sound effects
asset-bot generate audio --type sfx --prompt "sword clashing against metal shield, heavy impact" --category combat-sfx --json

# Generate music with duration
asset-bot generate audio --type music --prompt "orchestral victory fanfare, triumphant brass" --category music --duration-sec 5 --json

# Generate voice line
asset-bot generate audio --type voice --prompt "You shall not pass!" --category dialogue --voice-id <elevenlabs-voice-id> --json

# Multiple candidates
asset-bot generate audio --type sfx --prompt "footsteps on stone floor" --category ambient-sfx --count 4 --json
```

For exact parameters and flags, run `asset-bot generate audio --help` or see `../../../references/CLI-REFERENCE.md`.

## Functions

| Function               | Purpose                          |
| ---------------------- | -------------------------------- |
| `generateSFX(args)`    | Generate sound effect candidates |
| `generateMusic(args)`  | Generate music candidates        |
| `generateSpeech(args)` | Generate speech audio            |
| `listVoices()`         | List available ElevenLabs voices |
| `findVoice(args)`      | Find a voice by name             |

## generateSFX Parameters

| Param         | Type   | Default     | Notes                                  |
| ------------- | ------ | ----------- | -------------------------------------- |
| `description` | string | required    | Specific materials, intensity, context |
| `outputPath`  | string | required    | Directory for candidates               |
| `durationSec` | number | API default | 0.5–22 seconds                         |
| `numVariants` | number | 3           | Candidates to generate                 |

## generateMusic Parameters

| Param         | Type   | Default      | Notes                                            |
| ------------- | ------ | ------------ | ------------------------------------------------ |
| `prompt`      | string | required     | Genre, tempo, instruments, mood                  |
| `outputPath`  | string | required     | Directory for candidates                         |
| `durationSec` | number | **required** | 1–600 seconds. No default — must always specify! |
| `numVariants` | number | 2            | Candidates to generate                           |

## generateSpeech Parameters

| Param             | Type   | Default     | Notes                        |
| ----------------- | ------ | ----------- | ---------------------------- |
| `text`            | string | required    | Supports v3 audio tags       |
| `voiceId`         | string | required    | ElevenLabs voice ID          |
| `outputPath`      | string | required    | Output file path             |
| `modelId`         | string | `eleven_v3` | TTS model                    |
| `stability`       | number | 0.5         | 0–1, lower = more expressive |
| `similarityBoost` | number | 0.8         | 0–1, voice similarity        |
| `speed`           | number | 1.0         | 0.5–2.0, speech speed        |

## Prompt Tips

### SFX

- Be specific about material: "sword on metal shield" not "combat"
- Include intensity: "heavy impact", "soft tap", "gentle rustle"
- Mention context: "indoor echo", "outdoor open", "underwater muffled"

### Music

- Include genre/style: "orchestral", "chiptune", "synthwave"
- Specify tempo: "fast-paced", "slow", "moderate"
- Mention instruments: "piano", "strings", "brass"
- Add mood: "triumphant", "melancholic", "tense"
- For loops: include "loopable" in prompt (hint only — use `util-loop-audio` for true seamless loops)

### Short Clips (Fanfares, Stingers, Jingles)

**CRITICAL:** Always specify `durationSec` — there is no default!

| Use Case          | Duration | Notes                                   |
| ----------------- | -------- | --------------------------------------- |
| Victory fanfare   | 3-8s     | Short, punchy, triumphant               |
| Level complete    | 3-5s     | Quick celebratory burst                 |
| Game over sting   | 2-4s     | Brief, somber or dramatic               |
| UI confirm jingle | 1-3s     | Very short                              |
| Menu loop         | 30-60s   | Use `util-loop-audio` for seamless loop |

### Speech

- Use audio tags for expression: `[whispers]`, `[shouts]`, `[pause]`
- Set stability 0.3–0.5 for emotional scenes, 0.8–0.9 for narration

## Integration

- `api-elevenlabs` — Raw API wrapper for all ElevenLabs operations
- `util-loop-audio` — Seamless loop creation via ffmpeg crossfade
