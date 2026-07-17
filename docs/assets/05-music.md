# Assets — Music

**Spec:** `docs/VoidDay-Spec-unity.md` · **Style guide:** `docs/StyleGuide.md`
**Count:** 1

## Format

- **Delivery:** **`.ogg`, seamless loop**, stereo. One track for the prototype.
- **Style:** warm, gentle, slightly dreamy/spacey ambient — mellow, non-intrusive, loops forever (StyleGuide Audio / Q16). It should sit *under* the poppy SFX, never compete with the collect-pop.
- **Register:** cozy-cosmic — the audio equivalent of "warm farm on a soft violet island."

## Assets

| id | What | States/Variants | Qty | Spec | Placeholder |
|---|---|---|---|---|---|
| `music.ambient.loop` | Main background ambient loop | — | 1 | §12, StyleGuide Audio | silence / any royalty-free calm loop |

## Notes

- **Adaptive / layered music is out of scope** for the prototype (StyleGuide Q16) — flagged for later, not produced.
- No per-context tracks (no separate menu/event music) — the single loop plays throughout. A void-event gets an SFX sting (`sfx.worldEvent.start`), not a music change.
