#!/usr/bin/env python3
"""Tests for the auto-merge trigger-coverage check.

Run with: make tools-test

This check exists because its absence cost two hours of pull requests sitting green: once when
auto-merge relied on `check_suite` for workflows GitHub will not re-trigger from, and again the next
day when `unity-tests` was added and nobody thought to name it in a different workflow's triggers.
So these tests care mostly about whether it can still *fail*.
"""

from __future__ import annotations

import contextlib
import io
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(REPO / "tools" / "lint"))

import check_workflows  # noqa: E402

AUTO_MERGE = """\
name: Auto-merge
on:
  pull_request:
    types: [opened, labeled]
  workflow_run:
    workflows: [{listed}]
    types: [completed]
jobs:
  merge:
    runs-on: ubuntu-latest
    steps:
      - run: echo merging
"""

REPORTER = """\
name: {name}
on:
  push:
    paths: ['**']
  pull_request:
jobs:
  go:
    runs-on: ubuntu-latest
    steps:
      - run: echo checking
"""

DISPATCH_ONLY = """\
name: {name}
on:
  workflow_dispatch:
jobs:
  go:
    runs-on: ubuntu-latest
    steps:
      - run: echo manual
"""


@contextlib.contextmanager
def workflows(listed: list[str], reporters: list[str], dispatch_only: list[str] | None = None):
    """A fake .github/workflows directory, with check_workflows pointed at it."""
    with tempfile.TemporaryDirectory() as raw:
        directory = Path(raw)
        names = ", ".join(f'"{name}"' for name in listed)
        (directory / "auto-merge.yml").write_text(AUTO_MERGE.format(listed=names), encoding="utf-8")
        for name in reporters:
            (directory / f"{name}.yml").write_text(REPORTER.format(name=name), encoding="utf-8")
        for name in dispatch_only or []:
            (directory / f"{name}.yml").write_text(DISPATCH_ONLY.format(name=name), encoding="utf-8")

        original = check_workflows.WORKFLOWS, check_workflows.AUTO_MERGE
        check_workflows.WORKFLOWS = directory
        check_workflows.AUTO_MERGE = directory / "auto-merge.yml"
        try:
            yield directory
        finally:
            check_workflows.WORKFLOWS, check_workflows.AUTO_MERGE = original


def run() -> tuple[int, str]:
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        code = check_workflows.main()
    return code, out.getvalue()


class TheCheck(unittest.TestCase):
    def test_a_fully_listed_set_passes(self) -> None:
        with workflows(listed=["checks", "unity-tests"], reporters=["checks", "unity-tests"]):
            code, output = run()
        self.assertEqual(code, 0, output)
        self.assertIn("all named", output)

    def test_an_unlisted_reporting_workflow_fails(self) -> None:
        # The exact mistake of 2026-11-11: unity-tests added, auto-merge not told.
        with workflows(listed=["checks"], reporters=["checks", "unity-tests"]):
            code, output = run()
        self.assertEqual(code, 1)
        self.assertIn("unity-tests.yml", output)
        self.assertIn("never be woken", output)

    def test_it_names_every_omission_not_just_the_first(self) -> None:
        with workflows(listed=["checks"], reporters=["checks", "unity-tests", "content"]):
            code, output = run()
        self.assertEqual(code, 1)
        self.assertIn("unity-tests", output)
        self.assertIn("content", output)

    def test_a_dispatch_only_workflow_need_not_be_listed(self) -> None:
        # It reports no check against a commit, so auto-merge never waits on it.
        with workflows(listed=["checks"], reporters=["checks"], dispatch_only=["manual-thing"]):
            code, output = run()
        self.assertEqual(code, 0, output)

    def test_waiting_on_a_workflow_that_does_not_exist_fails(self) -> None:
        # A name that matches nothing waits forever, silently.
        with workflows(listed=["checks", "a-workflow-nobody-wrote"], reporters=["checks"]):
            code, output = run()
        self.assertEqual(code, 1)
        self.assertIn("not a workflow in this repository", output)

    def test_an_empty_trigger_list_fails(self) -> None:
        with workflows(listed=[], reporters=["checks"]):
            code, output = run()
        self.assertEqual(code, 1)
        self.assertIn("no workflow_run.workflows list", output)

    def test_the_bare_on_key_is_read_as_a_trigger_not_a_boolean(self) -> None:
        # PyYAML turns an unquoted `on:` into the boolean True, so a naive reader sees no
        # triggers at all and passes everything.
        with workflows(listed=["checks"], reporters=["checks"]) as directory:
            document = (directory / "checks.yml").read_text()
            self.assertIn("\non:", document, "the fixture must use a bare on: for this to mean anything")
            code, output = run()
        self.assertEqual(code, 0, output)

    def test_the_real_repository_passes(self) -> None:
        code, output = run()
        self.assertEqual(code, 0, output)


if __name__ == "__main__":
    unittest.main()
