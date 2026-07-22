# Shared spec — the repair loop (REPAIR primitive)

The reusable resolution unit for `audit_doc` (one doc) and `audit_project` (each selected doc). Given a doc and a findings list from `auditor.md`, converge the doc to accurate + lean, then stamp it. Do not re-implement repair inline — follow this.

**Precondition:** a findings list already exists (from a cold-auditor pass). Repair never runs without one.

**Silence is never consent.** Every pause/confirmation in this loop is a HARD STOP. If the environment returns no user answer (timeout, non-interactive run, empty/aborted response), treat it as **STOP** — do not resolve, delete, prune, or commit on an assumed default. Halt and wait, even if the harness prompts you to continue. A "recommended" option only speeds an explicit choice; it is never something to act on when the user is absent.

---

## The loop (per doc)

Repeat until a full audit pass returns **no findings of consequence** (`WRONG` + `MISLEADING` == 0), or the iteration cap (default **4**) is hit:

1. **Resolve consequential findings.** Fix every `WRONG` and `MISLEADING` finding in the doc, using the finding's `evidence` (`file:line`) as the authority. Fix `NITPICK`s only when trivially cheap in passing; otherwise log and skip.
2. **Apply deletions under the guard** (see below) for `STALE` / `REDUNDANT` findings.
3. **Judgment calls → pause and ask the user.** Whenever a finding is a genuine keep-vs-delete call, or the auditor flagged something as `WRONG` that may actually be true-but-not-mechanically-verifiable guidance, do NOT guess — surface it (play `afplay /System/Library/Sounds/Glass.aiff`) and let the user decide.
4. **Re-audit** — fresh cold pass per `auditor.md` over the edited doc. Zero consequential findings → converged, exit. Otherwise iterate.
5. **Anti-thrash** — if the cap is hit with findings still open (e.g. a fix keeps surfacing a new issue), STOP and report the unresolved set to the user rather than looping forever.

## Deletion guard (less is more — but safe)

Only remove content that is:
- (a) **factually wrong and unfixable**, or
- (b) **describing code that no longer exists** (`STALE`), or
- (c) **redundant duplication** of something stated better elsewhere (`REDUNDANT`).

**Never** delete true "why"/rationale, strict rules, or anti-patterns merely because they are verbose or not literally present in the code — that judgment content is the doc's highest value. When unsure whether something is "wrong" or "not mechanically verifiable but still true guidance," **keep it** (and, if material, ask the user).

**Report every deletion** — what was cut and which clause (a/b/c) justified it. Deletions are the riskiest action; they must be reviewable, never silent. A deletion of more than a couple of passages, or of any strict-rule / anti-pattern, is a **pause trigger** — confirm with the user first.

## Prune pass (once accurate)

After the loop converges, do one deliberate trim under the same guard: collapse redundancy, cut passages describing deleted code, tighten anything that survived only because it was true-but-bloated. Keep every strict rule, anti-pattern, and "why."

## Stamp the audit (per doc, on completion)

An audit is a re-verification, so update the doc's existing footer — **one footer, not two.** Rewrite the `Last verified:` line to the **current date + time** (`date`) and **current commit** (`git rev-parse --short HEAD`), prepending a short dated note summarizing what the audit changed and preserving the existing footer history chain (match the project's `*_Architecture.md` footer style). If drift was assessed as **None**, still refresh the timestamp (records that it was checked).

**If the doc has no footer** (created before the convention, or by hand), append one at the end of the file:

```markdown
---
*Last verified: <date time> — commit `<short-sha>` (<one-line audit note>)*
```

## Source is off-limits

Never modify source, tests, or config to make the doc "right." The only file repaired is the `*_Architecture.md`.
