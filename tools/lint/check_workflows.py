#!/usr/bin/env python3
"""Every workflow that reports a check must be able to wake auto-merge.

`F-13`'s auto-merge waits until all checks on a commit have passed, and it re-examines a pull
request when a `workflow_run` event tells it one finished. GitHub does not start a workflow run from
an event created with `GITHUB_TOKEN`, so `check_suite` is no use for our own workflows — which means
`workflow_run.workflows` has to name every workflow that produces a check.

Miss one and auto-merge skips with "checks are still running", nothing wakes it, and the pull
request sits green until the hourly backstop. That happened twice: once because `check_suite` was
being relied on at all, and again the next day when `unity-tests` was added and not listed. The
second time is why this check exists — the failure is silent, costs an hour, and looks like the
automation being broken rather than incomplete.

Checked here rather than left to review because nothing about adding a workflow suggests you also
have to edit a different one.
"""

from __future__ import annotations

import sys
from pathlib import Path

import yaml

REPO = Path(__file__).resolve().parent.parent.parent
WORKFLOWS = REPO / ".github/workflows"
AUTO_MERGE = WORKFLOWS / "auto-merge.yml"

#: Triggers that make a workflow report a check against a commit.
REPORTING_TRIGGERS = ("push", "pull_request")


def triggers_of(document: dict) -> dict:
    """The `on:` block. PyYAML reads a bare `on` as the boolean True."""
    for key in ("on", True):
        if key in document:
            value = document[key]
            if isinstance(value, dict):
                return value
            if isinstance(value, list):
                return {name: None for name in value}
            if isinstance(value, str):
                return {value: None}
    return {}


def main() -> int:
    problems: list[str] = []

    if not AUTO_MERGE.is_file():
        print(f"workflows: {AUTO_MERGE.name} is missing")
        return 1

    auto = yaml.safe_load(AUTO_MERGE.read_text(encoding="utf-8"))
    auto_triggers = triggers_of(auto)
    workflow_run = auto_triggers.get("workflow_run") or {}
    listed = set(workflow_run.get("workflows") or [])

    if not listed:
        problems.append("auto-merge.yml has no workflow_run.workflows list, so nothing it waits "
                        "for can ever wake it")

    reporting: dict[str, str] = {}
    for path in sorted(WORKFLOWS.glob("*.yml")):
        if path == AUTO_MERGE:
            continue
        try:
            document = yaml.safe_load(path.read_text(encoding="utf-8"))
        except yaml.YAMLError as error:
            problems.append(f"{path.name}: will not parse: {error}")
            continue
        if not isinstance(document, dict):
            problems.append(f"{path.name}: is not a mapping")
            continue

        name = document.get("name") or path.stem
        if any(trigger in triggers_of(document) for trigger in REPORTING_TRIGGERS):
            reporting[name] = path.name

    for name, filename in sorted(reporting.items()):
        if name not in listed:
            problems.append(
                f"{filename} runs on push or pull_request, so it reports a check, but "
                f"auto-merge.yml does not list \"{name}\" in workflow_run.workflows. Auto-merge "
                "would skip while it ran and never be woken when it finished."
            )

    unknown = listed - set(reporting) - {"Unity licence", "Create Unity project"}
    for name in sorted(unknown):
        problems.append(f"auto-merge.yml waits on \"{name}\", which is not a workflow in this "
                        "repository. A name that matches nothing silently waits forever.")

    if problems:
        print(f"workflows: {len(problems)} problem(s):")
        for problem in problems:
            print(f"  {problem}")
        return 1

    print(f"workflows: {len(reporting)} reporting workflow(s), all named in auto-merge's triggers")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
