# Feature Mockup Prompt Reference

Guide for writing mockup panel prompts for the `pipeline-feature-mockup` skill.

## What Is a Feature Mockup?

A set of 9:16 portrait panels showing a mobile game feature across its key states. Each panel is a standalone image generated via Nano Banana Pro with shared style references for visual consistency.

Typical panel count: 3-6 (default 4).

## Panel Archetypes

Most features follow a progression through these states. Pick the ones that best communicate the feature:

| Archetype                  | Purpose                                        | Example                                             |
| -------------------------- | ---------------------------------------------- | --------------------------------------------------- |
| **Empty / Setup**          | Show the feature before the player engages     | An empty offering platform with faint UI prompts    |
| **Active / In-Progress**   | Show the feature mid-use with partial progress | Familiars channeling energy, progress bar at 50%    |
| **Payoff / Reward**        | Show the satisfying completion moment          | Overflowing cornucopia, harvest button, reward rain |
| **Lucky Draw / Shop**      | Show reward selection or purchase UI           | Prize wheel, tiered chests, placement rewards       |
| **Leaderboard / Social**   | Show competitive or co-op elements             | Team rankings, score comparison, rival balloons     |
| **Task Board / Checklist** | Show daily tasks, missions, or objectives      | Clipboard with checkboxes, progress tracker         |

Not every feature needs all archetypes. A simple feature might only need 3 panels (setup, active, payoff). A complex event might need 5-6.

## Panel Prompt Structure

Each panel prompt should follow this format:

```
[Scene description — where are we, what does the player see]
[Central elements — the main interactive objects, characters, effects]
[UI elements — buttons, bars, labels, tooltips described literally]
[Composition — shot type, camera angle, framing]
[Lighting — source, quality, mood appropriate to the moment]
[Style — aesthetic anchors that match the target game]
```

This maps to Nano Banana's six essential elements (subject, action, location, composition, lighting, style) but organized around the mockup use case.

## Writing Good Panel Prompts

### Describe UI Text Literally

AI models render text literally. Describe exactly what text appears and where.

```
GOOD: A large button labeled "TAP TO HARVEST!" floats above the cornucopia
BAD:  A call-to-action button encouraging the player to collect rewards
```

### Describe Animations as Frozen Moments

Mockups are static images. Describe what the animation looks like at one frame.

```
GOOD: Familiars performing small dances with arms raised, one sitting cross-legged
BAD:  Familiars doing cute channeling animations
```

### Be Specific About Visual Effects

```
GOOD: A thick stream of green magical energy with leafy particles flowing from the platform to the horn
BAD:  Energy flowing between the two objects
```

### Describe UI Layout Spatially

```
GOOD: Progress bar in the top center showing "Grove Essence" at 50%. Timer below: "04h 22m"
BAD:  A progress bar and timer showing the remaining time
```

### Avoid Triptych Triggers

Nano Banana can interpret complex prompts as requests for multi-panel layouts. The pipeline automatically appends anti-panel language, but avoid these terms in your prompts:

- "sequence of", "showing X then Y"
- "multiple views", "different angles"
- "before and after", "stages of"

Each panel is a single unified image. Describe one moment, one view.

## Mapping a Spec to Panels

### From a Short Spec

User says: "A co-op event where teams race to fill hot air balloons with dice rolls"

Think about the player journey:

1. **What does the player see first?** (The race overview, leaderboard)
2. **What does the player do?** (Complete tasks, earn dice)
3. **What's the core interaction?** (Roll dice, watch balloon inflate)
4. **What's the reward?** (Placement rewards, prize chests)

### From a Detailed Spec

Extract the key visual moments. Each panel should show a distinct player state — don't cram two states into one panel. If the spec describes 6 distinct moments, consider 4-5 panels covering the most important ones.

### Panel Ordering

Order panels by the player's chronological experience:

1. Overview or context-setting panel first
2. Task/action panels in the middle
3. Payoff/reward panel last

## Style Ref Strategy

Place 2-3 screenshots from the target game in `projects/<id>/refs/style/`. These carry the visual design — your prompt should only describe the delta.

When style refs are active, keep the style section of your prompt short:

```
WITH REFS:    "Matching the cozy isometric style of the reference images."
WITHOUT REFS: "Cozy isometric game art, soft cel-shading, warm pastel palette,
               rounded shapes, small detailed characters, lush vegetation,
               gentle ambient occlusion, storybook aesthetic."
```

Let the refs do the heavy lifting.

## Example: Full Panel Set

Feature: "Pump the Hot Air Balloon" co-op event

**Panel 1 — Race Overview:**
Four hot air balloons floating in a bright blue sky above fluffy clouds. The player's team balloon is in the foreground, slightly larger, partially deflated with saggy fabric. Three rival balloons in the background at varying heights. A wooden leaderboard hangs from a cloud in the top right corner listing four teams. Event timer in top center reads "2d 14h". Score text below the player's balloon reads "1,200m". Medium shot, slightly low angle looking up at the balloons. Bright daylight with soft cloud shadows. Matching the cozy isometric style of the reference images. Single unified image composition.

**Panel 2 — Task Board:**
Close-up of a clipboard-style overlay on a parchment background. Three task strips pinned to the board. First task reads "Standard Summon 3 Times" with a green checkmark. Second task reads "Summon Relationship Trinkets 5 Times" with a partial progress indicator. Third task reads "Level Up a Familiar 2 Times" with a partial progress indicator. Next to the completed first task, a small button reads "Buy Extra Die: 50" with a purple crystal icon. Header bar at the top reads "Team Daily Goals: 4/12" with subtext "Reach 6/12 for Bonus Points!" A glowing six-sided die icon floats next to unfinished tasks. Eye level, flat composition. Soft warm interior lighting. Matching the cozy isometric style of the reference images. Single unified image composition.

**Panel 3 — Dice Roll Interaction:**
Wide view of the sky race with the player's balloon prominent. A large magical six-sided die tumbles in the center of the screen, landing face showing the number 6. A blast of golden wind shoots from the die into the player's balloon. The balloon fabric snaps tight and expands upward, rising above the rival balloons. A golden "Team Combo!" badge flashes near the die. Floating text reads "+600 Altitude!" above the balloon. Dice counter in the bottom right reads "Dice Remaining: 0". Medium shot, eye level. Bright sky with golden sparkle effects. Matching the cozy isometric style of the reference images. Single unified image composition.

**Panel 4 — Event Rewards:**
A cloud platform displaying three reward chests. Gold chest on the left labeled "1st Place" overflowing with coins, a glowing avatar frame, and dice. Silver chest in the center labeled "2nd Place" with moderate coins. Small coin pouch on the right labeled "Participation." The player's balloon floats triumphantly next to the gold chest with a celebration banner. Warm golden light illuminating the chests from above. Eye level, centered composition. Matching the cozy isometric style of the reference images. Single unified image composition.
