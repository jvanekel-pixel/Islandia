#!/usr/bin/env python3
"""Turns Unity's NUnit result file into a line a person can read.

Unity writes the result of `-runTests` as NUnit XML and says almost nothing useful on stdout. A CI
run that prints twenty thousand lines of editor log and no test count is a run nobody reads, so this
pulls out the numbers and names the failures.

The counts matter beyond this run: RK-07 is that a session quietly weakens a gate to get green, and
a test total that drops between two runs is the cheapest signal there is.

Usage:  tools/unity/test_summary.py <results.xml> [--markdown]
Exits non-zero if the file says anything failed, so it can be the gate itself.
"""

from __future__ import annotations

import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path


def main(argv: list[str]) -> int:
    if not argv:
        print("test_summary: give me a results file")
        return 2

    path = Path(argv[0])
    markdown = "--markdown" in argv

    if not path.is_file():
        # Not the same as zero tests: the runner did not get far enough to write a file, and
        # saying "0 tests passed" would read as success.
        print(f"test_summary: {path} does not exist — the test run did not produce results")
        return 1

    try:
        root = ElementTree.parse(path).getroot()
    except ElementTree.ParseError as error:
        print(f"test_summary: {path} will not parse: {error}")
        return 1

    total = int(root.get("total", 0))
    passed = int(root.get("passed", 0))
    failed = int(root.get("failed", 0))
    skipped = int(root.get("skipped", 0))
    inconclusive = int(root.get("inconclusive", 0))
    duration = root.get("duration", "?")

    failures = [
        case for case in root.iter("test-case")
        if case.get("result") in ("Failed", "Error")
    ]

    if markdown:
        print("| | |")
        print("| :-- | --: |")
        print(f"| Tests | {total} |")
        print(f"| Passed | {passed} |")
        print(f"| Failed | {failed} |")
        print(f"| Skipped | {skipped} |")
        print(f"| Seconds | {duration} |")
    else:
        print(f"unity tests: {passed}/{total} passed, {failed} failed, {skipped} skipped"
              f" ({duration}s)")

    for case in failures:
        name = case.get("fullname") or case.get("name")
        message = (case.findtext("failure/message") or "").strip().splitlines()
        first = message[0] if message else "no message"
        print(f"  FAILED {name}: {first}")

    if total == 0:
        # An empty run is not a pass. It usually means the assembly did not compile, or the
        # platform filter matched nothing — both of which look like success if nobody checks.
        print("test_summary: the run contained no tests at all, which is not a pass")
        return 1

    return 1 if failed or inconclusive or failures else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
