#!/usr/bin/env python3
"""Check that links and file references inside the repository's own docs resolve.

A broken path in the plan sends an autonomous session looking for a file that
is not there. Only internal references are checked; external URLs are not
fetched.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
#: Reference-only material: carried over verbatim, so its links are not ours to fix.
EXCLUDE = ("docs/blueprint/", "reference/drafts/")

LINK = re.compile(r"\[[^\]]*\]\(([^)\s]+)\)")
#: Backticked paths that look like repository paths, e.g. `docs/plan/STATE.md`.
BACKTICK_PATH = re.compile(r"`((?:docs|tools|contract|content|Assets|Packages|releases|infra)/[A-Za-z0-9_./*-]+)`")
#: Backticked plan files referred to by bare name, e.g. `02-ARCHITECTURE.md`.
PLAN_FILE = re.compile(r"`(\d{2}[A-Za-z]?-[A-Z-]+\.md)`")

#: Paths that name something a work package will create, not something present now.
def is_future(target: str) -> bool:
    return any(token in target for token in ("*", "<", "docs/reports/M", "docs/reports/device",
                                             "docs/runbook", "releases/", "contract/", "content/",
                                             "Assets/", "Packages/", "tools/device/", "Library/"))


def main() -> int:
    problems: list[str] = []
    checked = 0

    for markdown in sorted(REPO.rglob("*.md")):
        relative = markdown.relative_to(REPO).as_posix()
        if relative.startswith(EXCLUDE) or "/.git/" in relative:
            continue
        text = markdown.read_text(encoding="utf-8")
        checked += 1

        for match in LINK.finditer(text):
            target = match.group(1)
            if target.startswith(("http://", "https://", "mailto:", "#")):
                continue
            target = target.split("#")[0]
            if not target or is_future(target):
                continue
            if not (markdown.parent / target).resolve().exists():
                problems.append(f"{relative}: link to '{target}' does not resolve")

        for match in BACKTICK_PATH.finditer(text):
            target = match.group(1)
            if is_future(target):
                continue
            if not (REPO / target).exists():
                problems.append(f"{relative}: references '{target}', which does not exist")

        for match in PLAN_FILE.finditer(text):
            if not (REPO / "docs" / "plan" / match.group(1)).exists():
                problems.append(f"{relative}: references plan file '{match.group(1)}', which does not exist")

    if problems:
        for problem in sorted(set(problems)):
            print(f"markdown: {problem}")
        print(f"\nmarkdown: {len(set(problems))} problem(s)")
        return 1

    print(f"markdown: {checked} documents checked, every internal reference resolves")
    return 0


if __name__ == "__main__":
    sys.exit(main())
