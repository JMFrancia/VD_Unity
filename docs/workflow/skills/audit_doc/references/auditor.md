# Shared spec — the cold auditor (DETECT primitive)

This is the reusable detection unit for `audit_doc` (single doc) and `audit_project` (all docs). Both skills delegate the "find inaccuracies" step to a **cold, independent sub-agent** driven by the prompt below. Do not re-implement detection inline — spawn a `general-purpose` sub-agent with this contract so each verdict is independent of whoever wrote (or last edited) the doc.

**Why cold + independent:** an audit that re-derives the doc from the same mental model that wrote it is worthless. The auditor must treat the doc as suspect and falsify each claim against real `file:line` evidence. (This is the exact failure mode that motivated the skill: a doc can be internally self-consistent yet wrong — only a code cross-check catches it.)

---

## Auditor prompt (fill the `{...}` slots, then spawn a general-purpose sub-agent)

> You are an independent, adversarial fact-checker. An architecture document describes a software system. Your job is to verify the document's factual claims against the ACTUAL SOURCE CODE — **the code is the source of truth, the document is the suspect.** Assume the document contains errors and try to find them. Do NOT confirm a claim by re-reasoning from the document's own framing; confirm only against code you have read.
>
> **Inputs**
> - Document to audit: `{doc_path}`
> - Primary source: `{system_folder}` (all source files, recursive)
> - External code the doc references (audit these too — drift hides here): `{external_referenced_paths}`
>
> **Verify each of these against the code, citing `file:line` evidence:**
> 1. Every interface / contract listed exists with the stated methods & signatures.
> 2. Every event: the type exists, has the stated payload fields, and the named broadcasters/subscribers actually publish/subscribe to it (enumerate the REAL subscriber set — omissions are the classic error).
> 3. Every claimed dependency is real (assembly refs, imports, external packages).
> 4. Config values / constants / version numbers match.
> 5. Strict rules & invariants — spot-check the falsifiable ones against the code.
> 6. Diagram claims — named types/methods in class/state/flow/sequence diagrams exist; mermaid is syntactically valid.
> 7. Cross-doc links (`see X_Architecture.md`) resolve to real files.
> 8. Any other concrete, cheaply-falsifiable claim.
>
> **Report ONLY problems.** For each finding, give:
> - **section** — where in the doc (heading / line).
> - **claim** — the exact doc assertion (quote it).
> - **severity** — one of:
>   - `WRONG` — factually incorrect.
>   - `MISLEADING` — technically defensible but likely to mislead a future editor.
>   - `NITPICK` — cosmetic / trivial.
>   - `STALE` — describes code that no longer exists (deletion candidate).
>   - `REDUNDANT` — duplicates something stated better elsewhere (deletion candidate).
> - **evidence** — `file:line` + what the code actually shows.
> - **suggested_fix** — the correction, or "delete" for STALE/REDUNDANT.
>
> Also actively look for **omissions** in enumerated lists (e.g. a subscriber the doc's own prose mentions but a list drops) and **internal inconsistencies** (section A contradicts section B).
>
> End with a one-line **verdict**: count of `WRONG` + `MISLEADING` (the "of consequence" set), and whether the doc's factual spine is sound.
>
> You are **read-only** — do not modify any files.

---

## Findings schema (what the caller expects back)

A list of findings, each: `{ section, claim, severity, evidence, suggested_fix }`, plus a verdict line with the consequential count. When fanning out over many docs (`audit_project`), tag each finding with its `doc` so the aggregator can group and sort.

**"Of consequence" = `WRONG` + `MISLEADING`.** `NITPICK` is logged, not necessarily fixed. `STALE`/`REDUNDANT` feed the prune step (under the deletion guard in `repair-loop.md`).

## Re-audit independence

After repairs, the re-audit is a **fresh** run of this same contract over the edited doc — always spawn a NEW sub-agent; never resume the prior one, which would carry the mental model of its previous pass and defeat independence. Convergence = a full pass returns zero `WRONG` + `MISLEADING`.
