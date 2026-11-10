#!/usr/bin/env python3
"""Check that the plan is internally consistent.

The backlog is the thing autonomous sessions take work from, so a dangling
dependency or a reference to a file that does not exist is not a typo: it is a
session that stalls at three in the morning with nobody watching. This runs on
every change.

Checks:
  1. BACKLOG.yaml parses, and every package has the required fields.
  2. Work-package ids are unique.
  3. Every dependency names a real package, and appears before its dependent, so
     a driver reading top to bottom never has to look ahead.
  4. Milestones appear in one contiguous, ascending run.
  5. Every `refs` entry resolves to a file that exists.
  6. Every acceptance criterion ends with a verification level.
  7. Every package id mentioned in the plan prose exists in the backlog.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

import yaml

REPO = Path(__file__).resolve().parent.parent.parent
PLAN = REPO / "docs" / "plan"
BACKLOG = PLAN / "BACKLOG.yaml"

REF_ROOTS = {"bp": "docs/blueprint", "dr": "reference/drafts", "au": "docs/plan/audit"}
REQUIRED_FIELDS = ("id", "title", "ms", "risk", "deps", "refs", "accept", "status")
VALID_STATUS = {"todo", "in-progress", "done", "blocked", "needs-human", "cut"}
VALID_RISK = {"low", "med", "high"}
LEVEL = re.compile(r"\[L[0-7]\](\[L[0-7]\])*\s*$")
ID_IN_PROSE = re.compile(r"`((?:F|S|V|W|U|C|B|P|I|A|PET|Q|R|SB|D)-[A-Z0-9-]+[a-z]?|M\d+-RETRO)`")


def fail(problems: list[str], message: str) -> None:
    problems.append(message)


def main() -> int:
    problems: list[str] = []
    document = yaml.safe_load(BACKLOG.read_text(encoding="utf-8"))
    packages = document["packages"]

    ids = [p.get("id") for p in packages]
    position = {}
    for index, package in enumerate(packages):
        pid = package.get("id")
        if pid in position:
            fail(problems, f"duplicate work-package id: {pid}")
        position[pid] = index

    for package in packages:
        pid = package.get("id", "<no id>")
        for field in REQUIRED_FIELDS:
            if field not in package:
                fail(problems, f"{pid}: missing field '{field}'")
        if package.get("status") not in VALID_STATUS:
            fail(problems, f"{pid}: status {package.get('status')!r} is not one of {sorted(VALID_STATUS)}")
        if package.get("risk") not in VALID_RISK:
            fail(problems, f"{pid}: risk {package.get('risk')!r} is not one of {sorted(VALID_RISK)}")
        if not package.get("accept"):
            fail(problems, f"{pid}: has no acceptance criteria")

        for dependency in package.get("deps", []):
            if dependency not in position:
                fail(problems, f"{pid}: depends on {dependency}, which is not a work package")
            elif position[dependency] > position[pid]:
                fail(problems, f"{pid}: depends on {dependency}, which comes later in the file")

        for criterion in package.get("accept", []):
            if not LEVEL.search(criterion):
                fail(problems, f"{pid}: acceptance criterion has no verification level: {criterion[:70]}...")

        for ref in package.get("refs", []):
            kind, _, rest = ref.partition(" ")
            if kind == "adr":
                continue
            name = rest.split(" §")[0] if rest else ""
            if kind in REF_ROOTS:
                if not (REPO / REF_ROOTS[kind] / name).exists():
                    fail(problems, f"{pid}: ref '{ref}' does not exist")
            else:
                plain = kind.split(" §")[0]
                if not (PLAN / plain).exists():
                    fail(problems, f"{pid}: ref '{ref}' does not exist")

    milestones: list[str] = []
    for package in packages:
        ms = package.get("ms")
        if ms not in milestones:
            milestones.append(ms)
    numbers = [int(m.lstrip("M")) for m in milestones if re.fullmatch(r"M\d+", m or "")]
    if numbers != sorted(numbers):
        fail(problems, f"milestones are not in ascending order: {milestones}")
    if len(set(milestones)) != len(milestones):
        fail(problems, f"a milestone's packages are not contiguous: {milestones}")

    known = set(position)
    for markdown in sorted(PLAN.rglob("*.md")):
        if markdown.parent.name == "audit":
            continue
        for match in ID_IN_PROSE.finditer(markdown.read_text(encoding="utf-8")):
            if match.group(1) not in known:
                fail(problems, f"{markdown.relative_to(REPO)}: mentions {match.group(1)}, which is not a work package")

    if problems:
        for problem in problems:
            print(f"plan: {problem}")
        print(f"\nplan: {len(problems)} problem(s)")
        return 1

    criteria = sum(len(p["accept"]) for p in packages)
    print(f"plan: {len(packages)} work packages, {criteria} acceptance criteria, {len(milestones)} milestones, all refs resolve")
    return 0


if __name__ == "__main__":
    sys.exit(main())
