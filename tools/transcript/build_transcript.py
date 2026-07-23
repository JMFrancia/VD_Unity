"""Rebuild the curated AI session log in docs/ai-transcript/ from local Claude Code history.

Re-runnable: picks up new sessions automatically, so the transcript can be regenerated
at any point as the project continues. Reads the local session store, applies the
redaction rules in redactions.json, and writes the session index plus the per-day
prompt log. Only human-typed prompts are extracted -- tool calls, agent sidechains,
and assistant output are all dropped.

Usage:  python3 tools/transcript/build_transcript.py
"""
import json, glob, os, re, sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "docs", "ai-transcript")

# Deliberately gitignored: the patterns contain the strings they redact, so
# committing them would defeat the redaction. See REDACTIONS.md for the public
# record. Refuse to run without it rather than publishing raw prompts.
RULES_PATH = os.path.join(HERE, "redactions.json")
if not os.path.exists(RULES_PATH):
    sys.exit(f"Refusing to build: {RULES_PATH} is missing, so prompts would be "
             f"published unredacted. See REDACTIONS.md.")
RULES = json.load(open(RULES_PATH))

# Every local session store whose transcripts belong to this project.
SESSION_GLOB = os.path.expanduser("~/.claude/projects/-Users-joefrancia-Desktop-VoidPet-*")


def strip_harness_noise(text):
    """Remove harness-injected blocks that were never typed by a human."""
    for tag in ("system-reminder", "local-command-stdout", "command-message", "command-name"):
        text = re.sub(rf"<{tag}>.*?</{tag}>", "", text, flags=re.S)
    return text.strip()


def redact(text):
    for rule in RULES["literal"]:
        text = text.replace(rule["find"], rule["replace"])
    for rule in RULES["regex"]:
        text = re.sub(rule["find"], rule["replace"], text)
    return text


def load_sessions():
    sessions = []
    for store in sorted(glob.glob(SESSION_GLOB)):
        for path in sorted(glob.glob(os.path.join(store, "*.jsonl"))):
            title, prompts = None, []
            for line in open(path, errors="replace"):
                try:
                    rec = json.loads(line)
                except json.JSONDecodeError:
                    continue  # partial trailing write from a live session
                if rec.get("aiTitle"):
                    title = rec["aiTitle"]
                if rec.get("type") != "user" or rec.get("isSidechain"):
                    continue
                if (rec.get("origin") or {}).get("kind") != "human":
                    continue
                content = rec.get("message", {}).get("content")
                if isinstance(content, list):
                    content = "".join(
                        b.get("text", "") for b in content
                        if isinstance(b, dict) and b.get("type") == "text"
                    )
                if not isinstance(content, str):
                    continue
                content = redact(strip_harness_noise(content))
                if content:
                    prompts.append({"ts": rec.get("timestamp", ""), "text": content})
            if prompts:
                sessions.append({
                    "id": os.path.basename(path)[:8],
                    "title": title or "(untitled)",
                    "start": prompts[0]["ts"],
                    "prompts": prompts,
                })

    cutoff = RULES.get("exclude_before", {}).get("date", "")
    excluded = [s for s in sessions
                if (cutoff and s["start"][:10] < cutoff)
                or any(e["title_contains"].lower() in s["title"].lower()
                       for e in RULES["exclude_sessions"])]
    sessions = [s for s in sessions if s not in excluded]
    sessions.sort(key=lambda s: s["start"])
    kept, forks = dedupe_forks(sessions)
    return kept, excluded + forks


SUBSTANTIAL = 40   # chars; short prompts like "Go ahead" recur across unrelated sessions
OVERLAP = 0.6      # fraction of a session's substantial prompts already seen -> it's a fork


def dedupe_forks(sessions):
    """Drop forked sessions that replay another session's prompts.

    Resuming or rewinding a conversation writes a second transcript repeating the
    earlier prompts verbatim. Publishing both shows the same exchange twice, so
    keep whichever fork went furthest and discard the rest. Overlap is measured
    only over substantial prompts -- filler like "Go ahead" or "Commit pls"
    appears in most sessions and would otherwise collapse unrelated ones.
    """
    def meaty(session):
        return {p["text"] for p in session["prompts"] if len(p["text"]) >= SUBSTANTIAL}

    kept, dropped = [], []
    for s in sorted(sessions, key=lambda x: (-len(x["prompts"]), x["start"]), reverse=False):
        texts = meaty(s)
        if texts and any(len(texts & meaty(k)) >= OVERLAP * len(texts) for k in kept):
            dropped.append(s)
            continue
        kept.append(s)
    kept.sort(key=lambda s: s["start"])
    return kept, dropped


def write_index(sessions):
    total = sum(len(s["prompts"]) for s in sessions)
    days = sorted({s["start"][:10] for s in sessions})
    with open(os.path.join(OUT, "sessions.md"), "w") as f:
        f.write("# Session Index\n\n")
        f.write(f"{total} prompts across {len(sessions)} sessions, "
                f"{days[0]} to {days[-1]}.\n\n")
        f.write("Generated by `tools/transcript/build_transcript.py`. "
                "Full prompts are in [`prompts/`](prompts/), one file per day.\n\n")
        f.write("| # | Date | Session | Prompts |\n|---|---|---|---|\n")
        for i, s in enumerate(sessions, 1):
            day = s["start"][:10]
            f.write(f"| {i} | {day} | [{s['title']}](prompts/day-{day}.md) "
                    f"| {len(s['prompts'])} |\n")


def write_prompt_log(sessions):
    by_day = defaultdict(list)
    for s in sessions:
        by_day[s["start"][:10]].append(s)

    os.makedirs(os.path.join(OUT, "prompts"), exist_ok=True)
    for day, day_sessions in sorted(by_day.items()):
        count = sum(len(s["prompts"]) for s in day_sessions)
        with open(os.path.join(OUT, "prompts", f"day-{day}.md"), "w") as f:
            f.write(f"# {day}\n\n")
            f.write(f"{count} prompts across {len(day_sessions)} sessions. "
                    "Every prompt below is verbatim except where marked "
                    "`[... redacted]`; see `tools/transcript/REDACTIONS.md` "
                    "for what was removed and why.\n")
            for s in day_sessions:
                f.write(f"\n---\n\n## {s['title']}\n\n")
                f.write(f"`{s['id']}` · {s['start'][11:16]} UTC · "
                        f"{len(s['prompts'])} prompts\n\n")
                for p in s["prompts"]:
                    f.write(f"**[{p['ts'][11:16]}]**\n\n{p['text']}\n\n")

    # Never delete anything; a day that stops being generated (a new exclusion
    # rule, say) would otherwise leave a stale file that still looks published.
    expected = {f"day-{d}.md" for d in by_day}
    stale = sorted(set(os.listdir(os.path.join(OUT, "prompts"))) - expected)
    return by_day, stale


def main():
    os.makedirs(OUT, exist_ok=True)
    sessions, excluded = load_sessions()
    if not sessions:
        sys.exit(f"No sessions found under {SESSION_GLOB}")
    write_index(sessions)
    by_day, stale = write_prompt_log(sessions)

    total = sum(len(s["prompts"]) for s in sessions)
    print(f"{len(sessions)} sessions, {total} prompts, {len(by_day)} days -> {OUT}")
    for s in excluded:
        print(f"  excluded: {s['title']} ({len(s['prompts'])} prompts)")
    for name in stale:
        print(f"  STALE (no longer generated, remove by hand): prompts/{name}")


if __name__ == "__main__":
    main()
